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
    /// Determines the local IPv4 address of the physical Wi-Fi or Ethernet adapter,
    /// explicitly filtering out virtual switches (WSL, Hyper-V, VirtualBox, VMware).
    /// </summary>
    public static string GetBestLocalIpAddress()
    {
        try
        {
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

            // 1. Search physical Wi-Fi adapters first
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (IsVirtualAdapter(ni)) continue;

                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
                {
                    var ip = GetIpv4FromInterface(ni);
                    if (!string.IsNullOrEmpty(ip)) return ip;
                }
            }

            // 2. Search physical Ethernet adapters
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (IsVirtualAdapter(ni)) continue;

                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet)
                {
                    var ip = GetIpv4FromInterface(ni);
                    if (!string.IsNullOrEmpty(ip)) return ip;
                }
            }

            // 3. Fallback: Any non-virtual, non-loopback active interface
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                if (IsVirtualAdapter(ni)) continue;

                var ip = GetIpv4FromInterface(ni);
                if (!string.IsNullOrEmpty(ip)) return ip;
            }
        }
        catch
        {
            // Fallback
        }

        return "127.0.0.1";
    }

    public static List<(string Name, string Ip)> GetAllLocalIpv4Addresses()
    {
        var list = new List<(string Name, string Ip)>();
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                if (IsVirtualAdapter(ni)) continue;

                var ip = GetIpv4FromInterface(ni);
                if (!string.IsNullOrEmpty(ip))
                {
                    list.Add((ni.Name, ip));
                }
            }
        }
        catch { }
        return list;
    }

    private static bool IsVirtualAdapter(System.Net.NetworkInformation.NetworkInterface ni)
    {
        var name = ni.Name.ToLowerInvariant();
        var desc = ni.Description.ToLowerInvariant();
        return name.Contains("vethernet") || name.Contains("wsl") || name.Contains("virtual") ||
               name.Contains("vmware") || name.Contains("hyper-v") ||
               desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("wsl") ||
               desc.Contains("vmware");
    }

    private static string? GetIpv4FromInterface(System.Net.NetworkInformation.NetworkInterface ni)
    {
        var ipProps = ni.GetIPProperties();
        foreach (var addr in ipProps.UnicastAddresses)
        {
            if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
            {
                return addr.Address.ToString();
            }
        }
        return null;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
