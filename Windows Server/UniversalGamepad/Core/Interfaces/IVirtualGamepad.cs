using UniversalGamepad.Core.Models;

namespace UniversalGamepad.Core.Interfaces;

public interface IVirtualGamepad
{
    void Connect();
    void Disconnect();
    void Update(GamepadState state);
}
