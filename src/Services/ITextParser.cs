using SignalBooster.Mvp.Models;

namespace SignalBooster.Mvp.Services;

public interface ITextParser
{
    DeviceOrder ParseDeviceOrder(string noteText);
    Task<DeviceOrder> ParseDeviceOrderAsync(string noteText); // For LLM parsers
}