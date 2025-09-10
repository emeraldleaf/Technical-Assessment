using System.Text.Json.Serialization;

namespace SignalBooster.Core.Models;

public record DeviceOrder(
    [property: JsonPropertyName("device")] string Device,
    [property: JsonPropertyName("mask_type")] string? MaskType,
    [property: JsonPropertyName("add_ons")] string[]? AddOns,
    [property: JsonPropertyName("qualifier")] string? Qualifier,
    [property: JsonPropertyName("ordering_provider")] string OrderingProvider,
    [property: JsonPropertyName("liters")] string? Liters = null,
    [property: JsonPropertyName("usage")] string? Usage = null,
    [property: JsonPropertyName("diagnosis")] string? Diagnosis = null,
    [property: JsonPropertyName("patient_name")] string? PatientName = null,
    [property: JsonPropertyName("dob")] string? DateOfBirth = null
)
{
    [JsonPropertyName("device_type")]
    public string? DeviceType => Device;
    
    [JsonPropertyName("patient_id")]
    public string PatientId { get; init; } = string.Empty;
    
    [JsonPropertyName("provider")]
    public string Provider => OrderingProvider;
    
    [JsonPropertyName("order_date")]
    public DateTime OrderDate { get; init; } = DateTime.UtcNow;
    
    [JsonPropertyName("specifications")]
    public Dictionary<string, object>? Specifications { get; init; }
}