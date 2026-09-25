using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniversalGamepad.Core.Interfaces;

namespace UniversalGamepad.Infrastructure.Network;

public class UdpDiscoveryBroadcaster : IDiscoveryBroadcaster
{
    private Socket _broadcastSocket;
    private CancellationTokenSource _cts;

    public void Start(int port)
    {
        _broadcastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _broadcastSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

        _cts = new CancellationTokenSource();
        Task.Run(() => BroadcastLoopAsync(port, _cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _broadcastSocket?.Close();
    }

    private async Task BroadcastLoopAsync(int port, CancellationToken ct)
    {
        string payload = $"UniversalGamepad:{Environment.MachineName}";
        byte[] broadcastData = Encoding.UTF8.GetBytes(payload);

        IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _broadcastSocket.SendToAsync(broadcastData, SocketFlags.None, broadcastEndPoint);
                await Task.Delay(2000, ct);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
            }
        }
    }
}
