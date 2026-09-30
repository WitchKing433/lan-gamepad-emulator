using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UniversalGamepad.Core.Interfaces;

namespace UniversalGamepad.Infrastructure.Network;

public class UdpInputListener : IInputListener
{
    private Socket? _listenSocket;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly object _lockObject = new();

    public event Action<string, ReadOnlyMemory<byte>>? OnPacketReceived;
    public event Action<Exception>? OnErrorOccurred;

    public void Start(int port)
    {
        lock (_lockObject)
        {
            if (_listenSocket != null) return;

            try
            {
                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));

                _cts = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            }
            catch
            {
                if (_listenSocket != null)
                {
                    try { _listenSocket.Close(); } catch { }
                    try { _listenSocket.Dispose(); } catch { }
                    _listenSocket = null;
                }

                if (_cts != null)
                {
                    try { _cts.Dispose(); } catch { }
                    _cts = null;
                }

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

            if (_listenSocket != null)
            {
                _listenSocket.Close();
                _listenSocket.Dispose();
                _listenSocket = null;
            }

            try
            {
                _listenTask?.GetAwaiter().GetResult();
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


    private async Task ListenLoopAsync(CancellationToken ct)
    {
        byte[] sharedBuffer = new byte[65535];
        Memory<byte> memoryBuffer = sharedBuffer;
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listenSocket!.ReceiveFromAsync(memoryBuffer, SocketFlags.None, remoteEndPoint);

                if (ct.IsCancellationRequested) break;

                if (result.ReceivedBytes >= 1)
                {
                    string clientIp = ((IPEndPoint)result.RemoteEndPoint).Address.ToString();
                    OnPacketReceived?.Invoke(clientIp, memoryBuffer.Slice(0, result.ReceivedBytes));
                }
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
                }
                break;
            }
        }
    }

}
