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
    private Socket? _broadcastSocket;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private readonly object _lockObject = new();

    public void Start(int port)
    {
        lock (_lockObject)
        {
            if (_broadcastSocket != null) return;

            _broadcastSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _broadcastSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

            _cts = new CancellationTokenSource();
            _broadcastTask = Task.Run(() => BroadcastLoopAsync(port, _cts.Token));
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                try
                {
                    _broadcastTask?.GetAwaiter().GetResult();
                }
                catch
                {
                }
                _cts.Dispose();
                _cts = null;
            }

            if (_broadcastSocket != null)
            {
                _broadcastSocket.Close();
                _broadcastSocket.Dispose();
                _broadcastSocket = null;
            }
        }
    }

    private async Task BroadcastLoopAsync(int port, CancellationToken ct)
    {
        string payload = $"TactPad:{Environment.MachineName}";
        byte[] broadcastData = Encoding.UTF8.GetBytes(payload);
        IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _broadcastSocket!.SendToAsync(broadcastData, SocketFlags.None, broadcastEndPoint);
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
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
