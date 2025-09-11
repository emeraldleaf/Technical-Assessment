using SignalBooster.Mvp.Models;

namespace SignalBooster.Mvp.Services;

public interface IApiClient
{
    Task PostDeviceOrderAsync(DeviceOrder deviceOrder);
}