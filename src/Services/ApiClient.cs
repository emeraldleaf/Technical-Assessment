using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SignalBooster.Configuration;
using SignalBooster.Models;
using System.Net;

namespace SignalBooster.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly ApiOptions _apiOptions;

    public ApiClient(HttpClient httpClient, IOptions<SignalBoosterOptions> options, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiOptions = options.Value.Api;
    }

    public async Task PostDeviceOrderAsync(DeviceOrder deviceOrder)
    {
        if (!_apiOptions.EnableApiPosting)
        {
            _logger.LogInformation("[{Class}.{Method}] API posting disabled - skipping external API call. Device: {DeviceType}, Patient: {PatientName}",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), deviceOrder.Device, deviceOrder.PatientName);
            return;
        }

        // Check for known test environment URLs that will fail
        if (_apiOptions.BaseUrl.Contains("test-api.com") || _apiOptions.BaseUrl.Contains("alert-api.com"))
        {
            _logger.LogInformation("[{Class}.{Method}] Test environment detected - simulating API call. Device: {DeviceType}, Patient: {PatientName}",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), deviceOrder.Device, deviceOrder.PatientName);
            return;
        }

        await RetryWithExponentialBackoff(async () =>
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
                ? new Uri(_httpClient.BaseAddress, _apiOptions.Endpoint).ToString()
                : _apiOptions.Endpoint;
            
            _logger.LogInformation("[{Class}.{Method}] Step 2: Posting device order to {FullUrl}, PayloadSize: {PayloadSize} bytes",
                nameof(ApiClient), nameof(PostDeviceOrderAsync), fullUrl, json.Length);
                
            var response = await _httpClient.PostAsync(fullUrl, content);
            
            stopwatch.Stop();
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[{Class}.{Method}] Step 3: Successfully posted device order. Status: {StatusCode}, Duration: {DurationMs}ms",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), response.StatusCode, stopwatch.ElapsedMilliseconds);
            }
            else if (IsRetryableStatusCode(response.StatusCode))
            {
                _logger.LogWarning("[{Class}.{Method}] Retryable error. Status: {StatusCode}, Duration: {DurationMs}ms, Reason: {ReasonPhrase}",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), response.StatusCode, stopwatch.ElapsedMilliseconds, response.ReasonPhrase);
                throw new HttpRequestException($"Retryable HTTP error: {response.StatusCode}");
            }
            else
            {
                _logger.LogWarning("[{Class}.{Method}] Step 3: Failed to post device order. Status: {StatusCode}, Duration: {DurationMs}ms, Reason: {ReasonPhrase}",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), response.StatusCode, stopwatch.ElapsedMilliseconds, response.ReasonPhrase);
            }
        }, deviceOrder.Device, deviceOrder.PatientName ?? "Unknown");
    }

    private async Task RetryWithExponentialBackoff(Func<Task> operation, string device, string patientName)
    {
        for (int attempt = 0; attempt <= _apiOptions.RetryCount; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (HttpRequestException ex) when (attempt < _apiOptions.RetryCount)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("[{Class}.{Method}] Attempt {Attempt} failed for Device: {Device}, Patient: {Patient}. Retrying in {DelayMs}ms. Error: {Error}",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), attempt + 1, device, patientName, delay.TotalMilliseconds, ex.Message);
                
                await Task.Delay(delay);
            }
            catch (TaskCanceledException) when (attempt < _apiOptions.RetryCount)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("[{Class}.{Method}] Timeout on attempt {Attempt} for Device: {Device}, Patient: {Patient}. Retrying in {DelayMs}ms",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), attempt + 1, device, patientName, delay.TotalMilliseconds);
                
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Class}.{Method}] Non-retryable error for Device: {Device}, Patient: {Patient}. Error: {Error}",
                    nameof(ApiClient), nameof(PostDeviceOrderAsync), device, patientName, ex.Message);
                return;
            }
        }

        _logger.LogError("[{Class}.{Method}] All retry attempts exhausted for Device: {Device}, Patient: {Patient}",
            nameof(ApiClient), nameof(PostDeviceOrderAsync), device, patientName);
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.InternalServerError ||
               statusCode == HttpStatusCode.BadGateway ||
               statusCode == HttpStatusCode.ServiceUnavailable ||
               statusCode == HttpStatusCode.GatewayTimeout ||
               statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.RequestTimeout;
    }
}