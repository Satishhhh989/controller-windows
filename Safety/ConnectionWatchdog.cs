using System;
using System.Threading;
using System.Threading.Tasks;
using WindowsBridge.Controller;

namespace WindowsBridge.Safety;

/// <summary>
/// Monitors controller packet arrival intervals.
/// If no valid packet arrives within [TimeoutMs] (default 200 ms), automatically
/// resets controller inputs to neutral to prevent vehicle runaway, and marks
/// connection as disconnected. Recovers automatically when packets resume.
/// </summary>
public sealed class ConnectionWatchdog : IDisposable
{
    private readonly ControllerState _state;
    private readonly int _timeoutMs;
    private readonly Action<bool>? _onStateChanged;
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;
    private bool _wasConnected;

    public ConnectionWatchdog(ControllerState state, int timeoutMs = 200, Action<bool>? onStateChanged = null)
    {
        _state = state;
        _timeoutMs = timeoutMs;
        _onStateChanged = onStateChanged;
    }

    public void Start()
    {
        _monitorTask = Task.Run(MonitorLoopAsync);
    }

    private async Task MonitorLoopAsync()
    {
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25)); // Check every 25ms

        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                var snapshot = _state.Snapshot();

                if (snapshot.IsConnected)
                {
                    var timeSinceLast = DateTime.UtcNow - _state.LastReceivedUtc;

                    if (timeSinceLast.TotalMilliseconds > _timeoutMs)
                    {
                        // Watchdog triggered: Reset to neutral immediately
                        _state.ResetToNeutral();

                        if (_wasConnected)
                        {
                            _wasConnected = false;
                            _onStateChanged?.Invoke(false);
                        }
                    }
                    else
                    {
                        if (!_wasConnected)
                        {
                            _wasConnected = true;
                            _onStateChanged?.Invoke(true);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit on Dispose
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
