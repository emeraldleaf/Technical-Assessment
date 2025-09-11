using Microsoft.Extensions.Options;
using SignalBooster.Mvp.Configuration;
using SignalBooster.Mvp.Models;

namespace SignalBooster.Mvp.Services;

public class DeviceExtractor
{
    private readonly IFileReader _fileReader;
    private readonly ITextParser _textParser;
    private readonly IApiClient _apiClient;
    private readonly SignalBoosterOptions _options;
    
    public DeviceExtractor(
        IFileReader fileReader, 
        ITextParser textParser,
        IApiClient apiClient,
        IOptions<SignalBoosterOptions> options)
    {
        _fileReader = fileReader;
        _textParser = textParser;
        _apiClient = apiClient;
        _options = options.Value;
    }
    
    public async Task<DeviceOrder> ProcessNoteAsync(string filePath)
    {
        var noteText = await _fileReader.ReadTextAsync(filePath);
        
        // Use async method if LLM is configured, otherwise use sync regex parser
        var deviceOrder = !string.IsNullOrEmpty(_options.OpenAI.ApiKey)
            ? await _textParser.ParseDeviceOrderAsync(noteText)
            : _textParser.ParseDeviceOrder(noteText);
        
        await _apiClient.PostDeviceOrderAsync(deviceOrder);
        
        return deviceOrder;
    }
}