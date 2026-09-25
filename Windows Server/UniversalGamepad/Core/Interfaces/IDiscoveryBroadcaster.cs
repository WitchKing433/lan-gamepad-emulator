namespace UniversalGamepad.Core.Interfaces;

public interface IDiscoveryBroadcaster
{
    void Start(int port);
    void Stop();
}
