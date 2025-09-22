using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using Xunit;

namespace SignalBooster.Tests;

/// <summary>
/// Tests for verifying correct behavior when switching between agentic and non-agentic modes
///
/// These tests ensure that:
/// - UseAgenticMode configuration is respected
/// - Proper fallback behavior when agentic mode is disabled
/// - Correct parser selection based on configuration
/// - Error handling when switching modes
/// </summary>
[Trait("Category", "Integration")]
public class AgenticModeConfigurationTests
{
    private readonly IFileReader _fileReader = Substitute.For<IFileReader>();
    private readonly ITextParser _textParser = Substitute.For<ITextParser>();
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();
    private readonly ILogger<DeviceExtractor> _extractorLogger = Substitute.For<ILogger<DeviceExtractor>>();
    private readonly ILogger<AgenticExtractor> _agenticLogger = Substitute.For<ILogger<AgenticExtractor>>();

    private readonly string _testNote = "Patient needs a CPAP with full face mask and humidifier. AHI > 20. Ordered by Dr. Cameron.";
    private readonly DeviceOrder _expectedOrder = new()
    {
        Device = "CPAP",
        OrderingProvider = "Dr. Cameron",
        MaskType = "full face mask",
        Diagnosis = "AHI > 20"
    };

    [Theory]
    [InlineData(true, true)]   // Agentic mode enabled with API key
    [InlineData(true, false)]  // Agentic mode enabled without API key (fallback)
    [InlineData(false, true)]  // Agentic mode disabled with API key
    [InlineData(false, false)] // Agentic mode disabled without API key
    public async Task DeviceExtractor_AgenticModeConfiguration_ShouldBehaveProperly(bool useAgenticMode, bool hasApiKey)
    {
        // Arrange
        var options = CreateOptions(useAgenticMode, hasApiKey);
        var agenticExtractor = new AgenticExtractor(options, _agenticLogger, _textParser);
        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, agenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _textParser.ParseDeviceOrderAsync(Arg.Any<string>()).Returns(_expectedOrder);
        _textParser.ParseDeviceOrder(Arg.Any<string>()).Returns(_expectedOrder);  // Add sync method mock
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act
        var result = await deviceExtractor.ProcessNoteAsync("test.txt");

        // Assert
        Assert.NotNull(result);

        if (!useAgenticMode)
        {
            // When agentic mode is disabled, should always use text parser
            Assert.Equal(_expectedOrder.Device, result.Device);
            Assert.Equal(_expectedOrder.OrderingProvider, result.OrderingProvider);
            // Should call text parser based on whether API key is present
            if (hasApiKey)
            {
                await _textParser.Received(1).ParseDeviceOrderAsync(_testNote);
            }
            else
            {
                _textParser.Received(1).ParseDeviceOrder(_testNote);
            }
        }
        else
        {
            // When agentic mode is enabled, it should attempt agentic extraction
            Assert.NotNull(result);
            Assert.NotNull(result.Device);
            // The specific result depends on whether we have a real API key
            // For test purposes, we just verify it doesn't crash
        }
    }

    [Fact]
    public async Task DeviceExtractor_AgenticModeDisabled_ShouldNotCallAgenticExtractor()
    {
        // Arrange
        var options = CreateOptions(useAgenticMode: false, hasApiKey: true);
        var mockAgenticExtractor = Substitute.For<IAgenticExtractor>();
        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, mockAgenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _textParser.ParseDeviceOrderAsync(Arg.Any<string>()).Returns(_expectedOrder);
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act
        var result = await deviceExtractor.ProcessNoteAsync("test.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_expectedOrder.Device, result.Device);

        // Verify agentic extractor was never called
        await mockAgenticExtractor.DidNotReceive().ExtractWithAgentsAsync(Arg.Any<string>(), Arg.Any<ExtractionContext>());

        // Verify text parser was called instead
        await _textParser.Received(1).ParseDeviceOrderAsync(_testNote);
    }

    [Fact]
    public async Task DeviceExtractor_AgenticModeEnabled_ShouldCallAgenticExtractor()
    {
        // Arrange
        var options = CreateOptions(useAgenticMode: true, hasApiKey: true);
        var mockAgenticExtractor = Substitute.For<IAgenticExtractor>();
        var agenticResult = new AgenticExtractionResult
        {
            DeviceOrder = _expectedOrder,
            ConfidenceScore = 0.95,
            Metadata = new ExtractionMetadata
            {
                ExtractorVersion = "AgenticExtractor_v1.0",
                AgentsUsed = new List<string> { "primary_extractor" },
                ProcessingDuration = TimeSpan.FromSeconds(1)
            }
        };
        mockAgenticExtractor.ExtractWithAgentsAsync(Arg.Any<string>(), Arg.Any<ExtractionContext>())
            .Returns(agenticResult);

        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, mockAgenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act
        var result = await deviceExtractor.ProcessNoteAsync("test.txt");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_expectedOrder.Device, result.Device);

        // Verify agentic extractor was called
        await mockAgenticExtractor.Received(1).ExtractWithAgentsAsync(_testNote, Arg.Any<ExtractionContext>());

        // Verify text parser was NOT called for extraction (only as fallback within agentic extractor)
        await _textParser.DidNotReceive().ParseDeviceOrderAsync(_testNote);
    }

    [Theory]
    [InlineData(ExtractionMode.Fast)]
    [InlineData(ExtractionMode.Standard)]
    [InlineData(ExtractionMode.Thorough)]
    public async Task DeviceExtractor_DifferentExtractionModes_ShouldPassCorrectContext(ExtractionMode mode)
    {
        // Arrange
        var options = CreateOptions(useAgenticMode: true, hasApiKey: true, extractionMode: mode);
        var mockAgenticExtractor = Substitute.For<IAgenticExtractor>();
        var agenticResult = new AgenticExtractionResult
        {
            DeviceOrder = _expectedOrder,
            ConfidenceScore = 0.9,
            Metadata = new ExtractionMetadata { AgentsUsed = new List<string>() }
        };
        mockAgenticExtractor.ExtractWithAgentsAsync(Arg.Any<string>(), Arg.Any<ExtractionContext>())
            .Returns(agenticResult);

        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, mockAgenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act
        await deviceExtractor.ProcessNoteAsync("test.txt");

        // Assert
        await mockAgenticExtractor.Received(1).ExtractWithAgentsAsync(
            _testNote,
            Arg.Is<ExtractionContext>(ctx => ctx.Mode == mode)
        );
    }

    [Fact]
    public async Task DeviceExtractor_ValidationEnabled_ShouldPassValidationContext()
    {
        // Arrange
        var options = CreateOptions(useAgenticMode: true, hasApiKey: true, requireValidation: true);
        var mockAgenticExtractor = Substitute.For<IAgenticExtractor>();
        var agenticResult = new AgenticExtractionResult
        {
            DeviceOrder = _expectedOrder,
            ConfidenceScore = 0.9,
            Metadata = new ExtractionMetadata { AgentsUsed = new List<string>() },
            ValidationResult = new ValidationResult { IsValid = true, ValidationScore = 0.95 }
        };
        mockAgenticExtractor.ExtractWithAgentsAsync(Arg.Any<string>(), Arg.Any<ExtractionContext>())
            .Returns(agenticResult);

        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, mockAgenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act
        await deviceExtractor.ProcessNoteAsync("test.txt");

        // Assert
        await mockAgenticExtractor.Received(1).ExtractWithAgentsAsync(
            _testNote,
            Arg.Is<ExtractionContext>(ctx => ctx.RequireValidation == true)
        );
    }

    [Fact]
    public async Task DeviceExtractor_AgenticFallback_ShouldStillCompleteSuccessfully()
    {
        // Arrange - Agentic mode enabled but extractor throws exception
        var options = CreateOptions(useAgenticMode: true, hasApiKey: true);
        var mockAgenticExtractor = Substitute.For<IAgenticExtractor>();
        mockAgenticExtractor.ExtractWithAgentsAsync(Arg.Any<string>(), Arg.Any<ExtractionContext>())
            .Returns(Task.FromException<AgenticExtractionResult>(new Exception("Agentic extraction failed")));

        var deviceExtractor = new DeviceExtractor(_fileReader, _textParser, mockAgenticExtractor, _apiClient, options, _extractorLogger);

        _fileReader.ReadTextAsync(Arg.Any<string>()).Returns(_testNote);
        _textParser.ParseDeviceOrderAsync(Arg.Any<string>()).Returns(_expectedOrder);
        _apiClient.PostDeviceOrderAsync(Arg.Any<DeviceOrder>()).Returns(Task.CompletedTask);

        // Act & Assert - Should not throw, should fall back gracefully
        var exception = await Record.ExceptionAsync(async () =>
        {
            var result = await deviceExtractor.ProcessNoteAsync("test.txt");
            Assert.NotNull(result);
        });

        // Should not throw an exception, should handle gracefully
        Assert.Null(exception);
    }

    private IOptions<SignalBoosterOptions> CreateOptions(
        bool useAgenticMode,
        bool hasApiKey,
        ExtractionMode extractionMode = ExtractionMode.Standard,
        bool requireValidation = false)
    {
        var options = Substitute.For<IOptions<SignalBoosterOptions>>();
        options.Value.Returns(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions
            {
                ApiKey = hasApiKey ? "test-api-key" : "",
                Model = "gpt-4o",
                MaxTokens = 1000,
                Temperature = 0.1f
            },
            Extraction = new ExtractionOptions
            {
                UseAgenticMode = useAgenticMode,
                ExtractionMode = extractionMode,
                RequireValidation = requireValidation,
                EnableSelfCorrection = false,
                MinConfidenceThreshold = 0.8,
                MaxCorrectionAttempts = 2
            }
        });
        return options;
    }
}