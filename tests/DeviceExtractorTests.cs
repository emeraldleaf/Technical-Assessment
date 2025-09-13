using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SignalBooster.Configuration;
using SignalBooster.Models;
using SignalBooster.Services;
using Xunit;

namespace SignalBooster.Tests;

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
        await _fileReader.Received(1).ReadTextAsync(filePath);
        _textParser.Received(1).ParseDeviceOrder(noteText);
        await _textParser.DidNotReceive().ParseDeviceOrderAsync(Arg.Any<string>());
        await _apiClient.Received(1).PostDeviceOrderAsync(expectedOrder);
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
        await _fileReader.Received(1).ReadTextAsync(filePath);
        await _textParser.Received(1).ParseDeviceOrderAsync(noteText);
        _textParser.DidNotReceive().ParseDeviceOrder(Arg.Any<string>());
        await _apiClient.Received(1).PostDeviceOrderAsync(expectedOrder);
    }
}