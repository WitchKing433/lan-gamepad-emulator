using System;

namespace UniversalGamepad.Core.Interfaces;

public interface IInputListener
{
    event Action<string, ReadOnlyMemory<byte>> OnPacketReceived;
    void Start(int port);
    void Stop();
}