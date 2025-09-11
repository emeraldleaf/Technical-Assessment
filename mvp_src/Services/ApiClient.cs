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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("[{Class}.{Method}] Step 1: Serializing device order to JSON. Device: {DeviceType}, Patient: {PatientName}",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), deviceOrder.Device, deviceOrder.PatientName);
            
            var json = JsonSerializer.Serialize(deviceOrder, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var fullUrl = _httpClient.BaseAddress != null 
                ? new Uri(_httpClient.BaseAddress, _endpoint).ToString()
                : _endpoint;
            
            _logger.LogInformation("[{Class}.{Method}] Step 2: Posting device order to {FullUrl}, PayloadSize: {PayloadSize} bytes",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), fullUrl, json.Length);
                
            var response = await _httpClient.PostAsync(fullUrl, content);
            
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[{Class}.{Method}] Step 3: Successfully posted device order. Status: {StatusCode}, Duration: {DurationMs}ms",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("[{Class}.{Method}] Step 3: Failed to post device order. Status: {StatusCode}, Duration: {DurationMs}ms, Reason: {ReasonPhrase}",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), response.StatusCode, stopwatch.ElapsedMilliseconds, response.ReasonPhrase);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Class}.{Method}] Step FAILED: Failed to post device order to API - continuing execution. Error: {ErrorMessage}",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), ex.Message);
            // Don't throw - this is not critical for MVP operation
        }
    }
}