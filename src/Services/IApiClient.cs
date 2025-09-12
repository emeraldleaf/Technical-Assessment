using SignalBooster.Models;

namespace SignalBooster.Services;

public interface IApiClient
{
    Task PostDeviceOrderAsync(DeviceOrder deviceOrder);
}