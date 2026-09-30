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

    public event Action<Exception>? OnErrorOccurred;

    public void Start(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        lock (_lockObject)
        {
            if (_broadcastSocket != null) return;

            Socket? socket = null;
            CancellationTokenSource? cancellationSource = null;

            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);

                cancellationSource = new CancellationTokenSource();
                _broadcastSocket = socket;
                _cts = cancellationSource;
                _broadcastTask = Task.Run(() => BroadcastLoopAsync(port, cancellationSource.Token));
            }
            catch
            {
                _broadcastSocket = null;
                _cts = null;
                _broadcastTask = null;

                try { socket?.Dispose(); } catch { }
                try { cancellationSource?.Dispose(); } catch { }

                throw;
            }
        }
    }

    public void Stop()
    {
        lock (_lockObject)
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }

            if (_broadcastSocket != null)
            {
                _broadcastSocket.Close();
                _broadcastSocket.Dispose();
                _broadcastSocket = null;
            }

            try
            {
                _broadcastTask?.GetAwaiter().GetResult();
            }
            catch
            {
            }

            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
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
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    OnErrorOccurred?.Invoke(ex);
                    try
                    {
                        await Task.Delay(2000, ct);
                    }
                    catch
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }
}
