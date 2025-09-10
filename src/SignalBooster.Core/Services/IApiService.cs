using SignalBooster.Core.Common;
using SignalBooster.Core.Models;
using System.Text.Json.Serialization;

namespace SignalBooster.Core.Services;

public interface IApiService
{
    Task<Result<DeviceOrderResponse>> SendDeviceOrderAsync(DeviceOrder deviceOrder);
}

public sealed class DeviceOrderResponse
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("processed_at")]
    public DateTime ProcessedAt { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}