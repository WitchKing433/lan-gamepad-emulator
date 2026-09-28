using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TactPad.Tester;

internal class Program
{
    private static readonly UdpClient UdpClient = new();
    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 55555);

    private static async Task Main()
    {
        Console.Clear();
        Console.WriteLine("=======================================");
        Console.WriteLine("       TACTPAD - NETWORK TESTER        ");
        Console.WriteLine("=======================================");
        Console.WriteLine("[1] Connect simulated Xbox 360 Controller");
        Console.WriteLine("[2] Connect simulated DualShock 4 Controller");
        Console.WriteLine("[3] Send continuous Input updates (Slot 1)");
        Console.WriteLine("[ESC] Exit Tester");
        Console.WriteLine("=======================================");

        while (true)
        {
            var key = Console.ReadKey(true).Key;

            if (key == ConsoleKey.Escape) break;

            switch (key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    await SendPacketAsync(new byte[] { 0x54, 1, 1, 1 });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Register Xbox 360 to port 55555");
                    break;

                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    await SendPacketAsync(new byte[] { 0x54, 1, 1, 2 });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Register DualShock 4 to port 55555");
                    break;

                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    await SendPacketAsync(new byte[] { 0x54, 2, 6, 0x01, 0x00, 128, 128, 0, 0 });
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sent: Input Data packet (Button A Pressed)");
                    break;
            }
        }
    }

    private static async Task SendPacketAsync(byte[] data)
    {
        try
        {
            await UdpClient.SendAsync(data, data.Length, ServerEndPoint);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Transmission failed: {ex.Message}");
        }
    }
}
