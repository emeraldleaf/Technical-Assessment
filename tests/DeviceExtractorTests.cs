using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using Xunit;

namespace SignalBooster.Tests;

/// <summary>
/// Unit tests for DeviceExtractor class - the main orchestration service
///
/// Test Categories:
/// - Processing flow validation (single file and batch modes)
/// - OpenAI vs Regex parser selection logic
/// - Error handling and logging verification
/// - Dependency coordination (FileReader, TextParser, ApiClient)
///
/// Mocking Strategy:
/// - All dependencies mocked using NSubstitute for isolation
/// - Focus on testing orchestration logic, not implementation details
/// - Verify correct method calls and parameter passing
/// </summary>
[Trait("Category", "Unit")]
public class DeviceExtractorTests
{
    private readonly IFileReader _fileReader = Substitute.For<IFileReader>();
    private readonly ITextParser _textParser = Substitute.For<ITextParser>();
    private readonly IApiClient _apiClient = Substitute.For<IApiClient>();
    private readonly IOptions<SignalBoosterOptions> _options;
    private readonly ILogger<DeviceExtractor> _logger = Substitute.For<ILogger<DeviceExtractor>>();
    private readonly DeviceExtractor _extractor;

    public DeviceExtractorTests()
    {
        _options = Substitute.For<IOptions<SignalBoosterOptions>>();
        _options.Value.Returns(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "" } // No API key = use regex parser
        });

        _extractor = new DeviceExtractor(_fileReader, _textParser, _apiClient, _options, _logger);

        // Clear any received calls to ensure clean test state
        _fileReader.ClearReceivedCalls();
        _textParser.ClearReceivedCalls();
        _apiClient.ClearReceivedCalls();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessNoteAsync_NoOpenAIKey_UsesRegexParser()
    {
        var filePath = "test.txt";
        var noteText = "Patient needs CPAP.";
        var expectedOrder = new DeviceOrder { Device = "CPAP" };

        _fileReader.ReadTextAsync(filePath).Returns(noteText);
        _textParser.ParseDeviceOrder(noteText).Returns(expectedOrder);

        var result = await _extractor.ProcessNoteAsync(filePath);

        Assert.Equal("CPAP", result.Device);
        Received.InOrder(() =>
        {
            _fileReader.ReadTextAsync(filePath);
            _textParser.ParseDeviceOrder(noteText);
            _apiClient.PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order => order.Device == "CPAP"));
        });

        await _fileReader.Received(1).ReadTextAsync(filePath);
        _textParser.Received(1).ParseDeviceOrder(noteText);
        await _textParser.DidNotReceive().ParseDeviceOrderAsync(Arg.Any<string>());
        await _apiClient.Received(1).PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order =>
            order.Device == "CPAP" &&
            order.Device == expectedOrder.Device));

        // Verify no unexpected calls were made
        await _apiClient.DidNotReceive().PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order => order.Device != "CPAP"));
        _textParser.DidNotReceive().ParseDeviceOrder(Arg.Is<string>(text => text != noteText));
        await _fileReader.DidNotReceive().ReadTextAsync(Arg.Is<string>(path => path != filePath));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessNoteAsync_WithOpenAIKey_UsesLlmParser()
    {
        // Create a new extractor with OpenAI configured
        var optionsWithKey = Substitute.For<IOptions<SignalBoosterOptions>>();
        optionsWithKey.Value.Returns(new SignalBoosterOptions
        {
            OpenAI = new OpenAIOptions { ApiKey = "test-key" }
        });
        
        var extractorWithKey = new DeviceExtractor(_fileReader, _textParser, _apiClient, optionsWithKey, _logger);
        
        var filePath = "test.txt";
        var noteText = "Patient needs CPAP.";
        var expectedOrder = new DeviceOrder { Device = "CPAP" };

        _fileReader.ReadTextAsync(filePath).Returns(noteText);
        _textParser.ParseDeviceOrderAsync(noteText).Returns(expectedOrder);

        var result = await extractorWithKey.ProcessNoteAsync(filePath);

        Assert.Equal("CPAP", result.Device);
        Received.InOrder(() =>
        {
            _fileReader.ReadTextAsync(filePath);
            _textParser.ParseDeviceOrderAsync(noteText);
            _apiClient.PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order => order.Device == "CPAP"));
        });

        await _fileReader.Received(1).ReadTextAsync(filePath);
        await _textParser.Received(1).ParseDeviceOrderAsync(noteText);
        _textParser.DidNotReceive().ParseDeviceOrder(Arg.Any<string>());
        await _apiClient.Received(1).PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order =>
            order.Device == "CPAP" &&
            order.Device == expectedOrder.Device));

        // Verify no unexpected calls were made
        await _apiClient.DidNotReceive().PostDeviceOrderAsync(Arg.Is<DeviceOrder>(order => order.Device != "CPAP"));
        await _textParser.DidNotReceive().ParseDeviceOrderAsync(Arg.Is<string>(text => text != noteText));
        await _fileReader.DidNotReceive().ReadTextAsync(Arg.Is<string>(path => path != filePath));
    }
}