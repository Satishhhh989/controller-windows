using System;
using System.Threading;
using System.Threading.Tasks;
using WindowsBridge.Controller;
using WindowsBridge.Diagnostics;
using WindowsBridge.Networking;
using WindowsBridge.Safety;
using WindowsBridge.UI;

namespace WindowsBridge;

public static class Program
{
    public static async Task Main(string[] args)
    {
        int port = 5000;
        int watchdogTimeoutMs = 200; // 100-250 ms range as requested

        if (args.Length > 0 && int.TryParse(args[0], out var customPort))
        {
            port = customPort;
        }

        if (args.Length > 1 && int.TryParse(args[1], out var customTimeout))
        {
            watchdogTimeoutMs = customTimeout;
        }

        Console.Title = $"Phone Racing Wheel Bridge (Port {port})";
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var state = new ControllerState();
        var statistics = new Statistics();
        using var dashboard = new Dashboard(state, statistics, port);

        using var watchdog = new ConnectionWatchdog(
            state,
            timeoutMs: watchdogTimeoutMs,
            onStateChanged: isConnected =>
            {
                if (isConnected)
                {
                    dashboard.AddLog("[INFO] Connection restored");
                }
                else
                {
                    dashboard.AddLog("[WARN] Phone disconnected — neutral watchdog safeguard engaged");
                }
            });

        using var udpServer = new UdpServer(
            port,
            state,
            statistics,
            logAction: msg => dashboard.AddLog(msg));

        using var discovery = new DeviceDiscovery(port);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Start all bridge services
        dashboard.AddLog($"[INFO] Starting Phone Racing Wheel Bridge on port {port}");
        dashboard.AddLog($"[INFO] Safety watchdog timeout: {watchdogTimeoutMs} ms");
        dashboard.AddLog($"[INFO] Local IP: {DeviceDiscovery.GetBestLocalIpAddress()}");

        watchdog.Start();
        udpServer.Start();
        discovery.Start();
        dashboard.Start();

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }

        Console.Clear();
        Console.WriteLine("Phone Racing Wheel Bridge shut down gracefully.");
    }
}
