using System;

namespace UniversalGamepad.Core.Interfaces;

public interface IInputListener
{
    event Action<string, ReadOnlyMemory<byte>> OnPacketReceived;
    event Action<Exception>? OnErrorOccurred;
    event Action<Exception>? OnPacketProcessingError;
    void Start(int port);
    void Stop();
}