using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Mvp.Configuration;
using SignalBooster.Mvp.Models;

namespace SignalBooster.Mvp.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly string _endpoint;

    public ApiClient(HttpClient httpClient, IOptions<SignalBoosterOptions> options, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _endpoint = options.Value.Api.Endpoint;
    }

    public async Task PostDeviceOrderAsync(DeviceOrder deviceOrder)
    {
        try
        {
            var json = JsonSerializer.Serialize(deviceOrder, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogInformation("Posting device order to {Endpoint}", _endpoint);
            
            var fullUrl = _httpClient.BaseAddress != null 
                ? new Uri(_httpClient.BaseAddress, _endpoint).ToString()
                : _endpoint;
                
            var response = await _httpClient.PostAsync(fullUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted device order");
            }
            else
            {
                _logger.LogWarning("Failed to post device order. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to post device order to API - continuing execution");
            // Don't throw - this is not critical for MVP operation
        }
    }
}