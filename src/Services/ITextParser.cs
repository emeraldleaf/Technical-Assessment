using SignalBooster.Models;

namespace SignalBooster.Services;

public interface ITextParser
{
    DeviceOrder ParseDeviceOrder(string noteText);
    Task<DeviceOrder> ParseDeviceOrderAsync(string noteText); // For LLM parsers
}