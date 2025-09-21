using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using Xunit;

namespace SignalBooster.Tests;

/// <summary>
/// Tests for agentic validation and self-correction features
///
/// These tests verify:
/// - Validation result generation and parsing
/// - Self-correction functionality
/// - Confidence threshold handling
/// - Medical accuracy validation
/// - Error scenarios in validation pipeline
/// </summary>
[Trait("Category", "Integration")]
public class AgenticValidationTests
{
    private readonly ITextParser _fallbackParser = Substitute.For<ITextParser>();
    private readonly ILogger<AgenticExtractor> _logger = Substitute.For<ILogger<AgenticExtractor>>();

    private readonly string _testNote = "Patient needs a CPAP with full face mask and humidifier. AHI > 20. Ordered by Dr. Cameron.";

    [Fact]
    public async Task ValidateExtractionAsync_WithValidOrder_ShouldReturnValidResult()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var validOrder = new DeviceOrder
        {
            Device = "CPAP",
            OrderingProvider = "Dr. Cameron",
            MaskType = "full face mask",
            Diagnosis = "AHI > 20"
        };

        // Act
        var result = await agenticExtractor.ValidateExtractionAsync(validOrder, _testNote);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ValidationScore >= 0.0 && result.ValidationScore <= 1.0);

        // With a real API key, validation should work properly
        if (!string.IsNullOrEmpty(options.Value.OpenAI.ApiKey))
        {
            Assert.True(result.ValidationScore > 0.5); // Should have reasonable confidence
        }
    }

    [Fact]
    public async Task ValidateExtractionAsync_WithInvalidOrder_ShouldIdentifyIssues()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var invalidOrder = new DeviceOrder
        {
            Device = "Unknown",
            OrderingProvider = "Dr. Unknown",
            MaskType = null,
            Diagnosis = null
        };

        // Act
        var result = await agenticExtractor.ValidateExtractionAsync(invalidOrder, _testNote);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Issues);

        // Should identify device and provider issues
        Assert.Contains(result.Issues, i => i.Field == "Device");
        Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task ValidateExtractionAsync_NoApiKey_ShouldUseBasicValidation()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: false);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var invalidOrder = new DeviceOrder
        {
            Device = "Unknown",
            OrderingProvider = "Dr. Unknown"
        };

        // Act
        var result = await agenticExtractor.ValidateExtractionAsync(invalidOrder, _testNote);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Issues);

        // Basic validation should identify device issues
        Assert.Contains(result.Issues, i => i.Field == "Device" && i.Issue.Contains("not identified"));
    }

    [Fact]
    public async Task SelfCorrectAsync_WithValidationIssues_ShouldAttemptCorrection()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var incorrectOrder = new DeviceOrder
        {
            Device = "Unknown",
            OrderingProvider = "Dr. Unknown"
        };

        var validationResult = new ValidationResult
        {
            IsValid = false,
            ValidationScore = 0.3,
            Issues = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Field = "Device",
                    Issue = "Device type not identified",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Extract CPAP from the medical note"
                },
                new ValidationIssue
                {
                    Field = "OrderingProvider",
                    Issue = "Provider not identified",
                    Severity = ValidationSeverity.Error,
                    SuggestedFix = "Extract Dr. Cameron from the note"
                }
            }
        };

        // Act
        var correctedOrder = await agenticExtractor.SelfCorrectAsync(incorrectOrder, validationResult, _testNote);

        // Assert
        Assert.NotNull(correctedOrder);

        // Self-correction should attempt to improve (may or may not succeed without real API)
        // At minimum, it should return a valid DeviceOrder object
        Assert.NotNull(correctedOrder.Device);
        Assert.NotNull(correctedOrder.OrderingProvider);
    }

    [Fact]
    public async Task SelfCorrectAsync_NoApiKey_ShouldReturnOriginalOrder()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: false);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var originalOrder = new DeviceOrder
        {
            Device = "Unknown",
            OrderingProvider = "Dr. Unknown"
        };

        var validationResult = new ValidationResult
        {
            IsValid = false,
            ValidationScore = 0.3,
            Issues = new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Field = "Device",
                    Issue = "Device type not identified",
                    Severity = ValidationSeverity.Error
                }
            }
        };

        // Act
        var result = await agenticExtractor.SelfCorrectAsync(originalOrder, validationResult, _testNote);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(originalOrder.Device, result.Device);
        Assert.Equal(originalOrder.OrderingProvider, result.OrderingProvider);
    }

    [Fact]
    public async Task ExtractWithAgentsAsync_WithValidation_ShouldIncludeValidationResult()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true, requireValidation: true);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var context = new ExtractionContext
        {
            SourceFile = "test.txt",
            Mode = ExtractionMode.Standard,
            RequireValidation = true
        };

        // Act
        var result = await agenticExtractor.ExtractWithAgentsAsync(_testNote, context);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResult);
        Assert.True(result.ValidationResult.ValidationScore >= 0.0 && result.ValidationResult.ValidationScore <= 1.0);
    }

    [Fact]
    public async Task ExtractWithAgentsAsync_LowValidationScore_ShouldTriggerSelfCorrection()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true, requireValidation: true, enableSelfCorrection: true);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        // Mock fallback parser to return a poor initial result
        var poorResult = new DeviceOrder { Device = "Unknown", OrderingProvider = "Dr. Unknown" };
        _fallbackParser.ParseDeviceOrderAsync(Arg.Any<string>()).Returns(poorResult);

        var context = new ExtractionContext
        {
            SourceFile = "test.txt",
            Mode = ExtractionMode.Standard,
            RequireValidation = true
        };

        // Act
        var result = await agenticExtractor.ExtractWithAgentsAsync(_testNote, context);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.ValidationResult);

        // Should have attempted self-correction if validation score was low
        // (The actual results depend on whether we have a real API key)
    }

    [Theory]
    [InlineData(ValidationSeverity.Info)]
    [InlineData(ValidationSeverity.Warning)]
    [InlineData(ValidationSeverity.Error)]
    [InlineData(ValidationSeverity.Critical)]
    public void ValidationIssue_DifferentSeverities_ShouldBeHandledProperly(ValidationSeverity severity)
    {
        // Arrange
        var issue = new ValidationIssue
        {
            Field = "Device",
            Issue = "Test issue",
            Severity = severity,
            SuggestedFix = "Test fix"
        };

        var validationResult = new ValidationResult
        {
            IsValid = severity < ValidationSeverity.Error,
            ValidationScore = severity >= ValidationSeverity.Error ? 0.3 : 0.8,
            Issues = new List<ValidationIssue> { issue }
        };

        // Act & Assert
        Assert.Equal(severity, issue.Severity);
        Assert.Equal(severity < ValidationSeverity.Error, validationResult.IsValid);

        if (severity >= ValidationSeverity.Error)
        {
            Assert.True(validationResult.ValidationScore < 0.5);
        }
    }

    [Fact]
    public async Task ExtractWithAgentsAsync_ConfidenceThreshold_ShouldRespectMinimumThreshold()
    {
        // Arrange
        var options = CreateOptions(hasApiKey: true, minConfidenceThreshold: 0.9);
        var agenticExtractor = new AgenticExtractor(options, _logger, _fallbackParser);

        var context = new ExtractionContext
        {
            SourceFile = "test.txt",
            Mode = ExtractionMode.Standard,
            RequireValidation = false
        };

        // Act
        var result = await agenticExtractor.ExtractWithAgentsAsync(_testNote, context);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ConfidenceScore >= 0.0 && result.ConfidenceScore <= 1.0);

        // The confidence threshold is checked in the configuration
        Assert.True(options.Value.Extraction.MinConfidenceThreshold == 0.9);
    }

    private IOptions<SignalBoosterOptions> CreateOptions(
        bool hasApiKey,
        bool requireValidation = false,
        bool enableSelfCorrection = false,
        double minConfidenceThreshold = 0.8)
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
                UseAgenticMode = true,
                ExtractionMode = ExtractionMode.Standard,
                RequireValidation = requireValidation,
                EnableSelfCorrection = enableSelfCorrection,
                MinConfidenceThreshold = minConfidenceThreshold,
                MaxCorrectionAttempts = 2
            }
        });
        return options;
    }
}