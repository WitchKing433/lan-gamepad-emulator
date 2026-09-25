using System;

namespace UniversalGamepad.Core.Interfaces;

public interface IGamepadManager
{
    void Initialize();
    void Shutdown();
    void HandleClientPacket(string clientIp, ReadOnlySpan<byte> packet);
}
