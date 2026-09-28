using System;
using UniversalGamepad.Core.Enums;

namespace UniversalGamepad.Core.Interfaces;

public interface IGamepadManager
{
    event Action<int, bool, GamepadType?>? OnSlotChanged;
    void Initialize();
    void Shutdown();
    void HandleClientPacket(string clientIp, ReadOnlySpan<byte> packet);
}
