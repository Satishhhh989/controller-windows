using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsBridge.Networking;

/// <summary>
/// Broadcasts local discovery beacons over Wi-Fi or Phone Hotspot so the Android phone
/// can automatically find and connect to the Windows laptop.
/// </summary>
public sealed class DeviceDiscovery : IDisposable
{
    private const int DiscoveryPort = 5152;
    private readonly int _listenPort;
    private readonly CancellationTokenSource _cts = new();
    private Task? _broadcastTask;

    public DeviceDiscovery(int listenPort = 5000)
    {
        _listenPort = listenPort;
    }

    public void Start()
    {
        _broadcastTask = Task.Run(BroadcastLoopAsync);
    }

    private async Task BroadcastLoopAsync()
    {
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

        var machineName = Environment.MachineName;
        var localIp = GetBestLocalIpAddress();

        var payload = JsonSerializer.Serialize(new
        {
            service = "PhoneRacingWheel",
            host = machineName,
            ip = localIp,
            port = _listenPort
        });

        var bytes = Encoding.UTF8.GetBytes(payload);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    await client.SendAsync(bytes, bytes.Length, endpoint);
                }
                catch
                {
                    // Ignore transient broadcast socket exceptions
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }
    }

    /// <summary>
    /// Determines the local IPv4 address of the active network adapter
    /// (works seamlessly on standard Wi-Fi routers and Android phone hotspots, 100% offline).
    /// </summary>
    public static string GetBestLocalIpAddress()
    {
        try
        {
            // 1. Fast probe: UDP routing table lookup (no packets are sent over the wire)
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint && !IPAddress.IsLoopback(endPoint.Address))
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            // Expected when offline or connected to an isolated phone hotspot
        }

        try
        {
            // 2. Safe local adapter enumeration (works 100% offline, zero DNS queries)
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;

                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    continue;

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }

        return "127.0.0.1";
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
