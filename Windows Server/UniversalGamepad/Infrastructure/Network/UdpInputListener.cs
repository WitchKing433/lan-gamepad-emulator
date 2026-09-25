using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UniversalGamepad.Core.Interfaces;

namespace UniversalGamepad.Infrastructure.Network;

public class UdpInputListener : IInputListener
{
    private Socket _listenSocket;
    private CancellationTokenSource _cts;

    public event Action<string, ReadOnlyMemory<byte>> OnPacketReceived;

    public void Start(int port)
    {
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));

        _cts = new CancellationTokenSource();
        Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listenSocket?.Close();
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        byte[] sharedBuffer = new byte[6];
        Memory<byte> memoryBuffer = sharedBuffer;

        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _listenSocket.ReceiveFromAsync(memoryBuffer, SocketFlags.None, remoteEndPoint);

                if (result.ReceivedBytes >= 1)
                {
                    string clientIp = ((IPEndPoint)result.RemoteEndPoint).Address.ToString();
                    OnPacketReceived?.Invoke(clientIp, memoryBuffer.Slice(0, result.ReceivedBytes));
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // Network error fallback
            }
        }
    }
}