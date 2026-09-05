using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WindowsBridge.Controller;
using WindowsBridge.Diagnostics;

namespace WindowsBridge.UI;

/// <summary>
/// Lightweight, flicker-free terminal dashboard displaying real-time controller states,
/// network health, sequence tracking, and development event logs.
/// </summary>
public sealed class Dashboard : IDisposable
{
    private readonly ControllerState _state;
    private readonly Statistics _statistics;
    private readonly int _listenPort;
    private readonly ConcurrentQueue<string> _recentLogs = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _renderTask;

    public Dashboard(ControllerState state, Statistics statistics, int listenPort)
    {
        _state = state;
        _statistics = statistics;
        _listenPort = listenPort;
    }

    public void AddLog(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _recentLogs.Enqueue(timestamped);
        while (_recentLogs.Count > 5)
        {
            _recentLogs.TryDequeue(out _);
        }
    }

    public void Start()
    {
        try
        {
            Console.CursorVisible = false;
        }
        catch
        {
            // Non-interactive console
        }

        _renderTask = Task.Run(RenderLoopAsync);
    }

    private async Task RenderLoopAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(33)); // ~30 fps UI tick

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                Render();
            }
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }
    }

    private void Render()
    {
        var (steering, throttle, brake, handbrake, gearUp, gearDown, isConnected, lastSeq) = _state.Snapshot();
        var (phoneIp, total, valid, stale, invalid, dropped, statsSeq, pps, latency) = _statistics.Snapshot();

        string connectionStatusStr;
        if (isConnected)
        {
            connectionStatusStr = "CONNECTED";
        }
        else if (total > 0)
        {
            connectionStatusStr = "PHONE DISCONNECTED (WATCHDOG NEUTRAL ENGAGED)";
        }
        else
        {
            connectionStatusStr = "WAITING FOR PHONE";
        }

        var sb = new StringBuilder();

        sb.AppendLine("==================================================================");
        sb.AppendLine("                   PHONE RACING WHEEL BRIDGE                      ");
        sb.AppendLine("==================================================================");
        sb.AppendLine();
        sb.AppendLine($"Connection:      {connectionStatusStr}");
        sb.AppendLine($"Transport:       Wi-Fi UDP (Port {_listenPort})");
        sb.AppendLine($"Phone IP:        {phoneIp}");
        sb.AppendLine($"Packets/sec:     {pps,5:F1}");
        sb.AppendLine($"Last Sequence:   {lastSeq}");
        sb.AppendLine($"Invalid Packets: {invalid}");
        sb.AppendLine($"Stale Packets:   {stale} (Dropped: {dropped})");
        sb.AppendLine($"Latency:         {(latency > 0 ? $"{latency:F0} ms" : "n/a")}");
        sb.AppendLine();
        sb.AppendLine("-------------------------- CONTROLLER ----------------------------");
        sb.AppendLine();

        // Steering
        var steerPct = (int)Math.Round(steering * 100);
        var steerBar = BuildSteeringBar(steering);
        sb.AppendLine($"Steering:   {steerPct,4}%  {steerBar}");

        // Throttle
        var thrtlPct = (int)Math.Round(throttle * 100);
        var thrtlBar = BuildProgressBar(throttle, 24);
        sb.AppendLine($"Throttle:   {thrtlPct,4}%  {thrtlBar}");

        // Brake
        var brakePct = (int)Math.Round(brake * 100);
        var brakeBar = BuildProgressBar(brake, 24);
        sb.AppendLine($"Brake:      {brakePct,4}%  {brakeBar}");
        sb.AppendLine();

        // Buttons
        sb.AppendLine($"Handbrake:  {(handbrake ? "[ ON  ]" : "[ OFF ]")}");
        sb.AppendLine($"Gear Up:    {(gearUp ? "[ ON  ]" : "[ OFF ]")}");
        sb.AppendLine($"Gear Down:  {(gearDown ? "[ ON  ]" : "[ OFF ]")}");
        sb.AppendLine();

        sb.AppendLine("----------------------- EVENT LOGS & STATUS ----------------------");
        foreach (var log in _recentLogs)
        {
            sb.AppendLine($"  {log}");
        }

        if (_recentLogs.IsEmpty)
        {
            sb.AppendLine($"  [INFO] Listening on port {_listenPort}. Ready for phone on Wi-Fi or Hotspot.");
        }

        sb.AppendLine();
        sb.AppendLine("Press [Ctrl+C] to exit. Target game: Forza (Windows PC)");

        try
        {
            Console.SetCursorPosition(0, 0);
            Console.Write(sb.ToString());
        }
        catch
        {
            // Fallback for redirected standard output
        }
    }

    private static string BuildSteeringBar(double value)
    {
        const int halfWidth = 12;
        var chars = new char[halfWidth * 2 + 1];
        Array.Fill(chars, ' ');
        chars[halfWidth] = '|';

        var offset = (int)Math.Round(value * halfWidth);
        offset = Math.Clamp(offset, -halfWidth, halfWidth);

        if (offset < 0)
        {
            for (int i = halfWidth + offset; i < halfWidth; i++)
                chars[i] = '=';
            chars[halfWidth + offset] = '<';
        }
        else if (offset > 0)
        {
            for (int i = halfWidth + 1; i <= halfWidth + offset; i++)
                chars[i] = '=';
            chars[halfWidth + offset] = '>';
        }

        return $"[{new string(chars)}]";
    }

    private static string BuildProgressBar(double value, int width)
    {
        var filled = (int)Math.Round(Math.Clamp(value, 0.0, 1.0) * width);
        var bar = new string('=', filled).PadRight(width, ' ');
        return $"[{bar}]";
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            Console.CursorVisible = true;
        }
        catch
        {
            // Ignore
        }
        _cts.Dispose();
    }
}
