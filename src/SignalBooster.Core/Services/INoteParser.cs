using SignalBooster.Core.Common;
using SignalBooster.Core.Models;

namespace SignalBooster.Core.Services;

public interface INoteParser
{
    Result<PhysicianNote> ParseNoteFromText(string noteText);
    Result<DeviceOrder> ExtractDeviceOrder(PhysicianNote note);
}