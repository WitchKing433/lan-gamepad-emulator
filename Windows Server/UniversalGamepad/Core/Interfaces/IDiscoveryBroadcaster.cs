using System;

namespace UniversalGamepad.Core.Interfaces;

public interface IDiscoveryBroadcaster
{
    event Action<Exception>? OnErrorOccurred;
    void Start(int port);
    void Stop();
}
