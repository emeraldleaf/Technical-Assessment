namespace SignalBooster.Mvp.Models;

public record DeviceOrder
{
    public string Device { get; init; } = string.Empty;
    public string? Liters { get; init; }
    public string? Usage { get; init; }
    public string? Diagnosis { get; init; }
    public string OrderingProvider { get; init; } = string.Empty;
    public string? PatientName { get; init; }
    public string? Dob { get; init; }
    public string? MaskType { get; init; }
    public string[]? AddOns { get; init; }
    public string? Qualifier { get; init; }
}