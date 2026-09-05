using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WindowsBridge.Controller;
using WindowsBridge.Diagnostics;
using WindowsBridge.Protocol;

namespace WindowsBridge.Networking;

/// <summary>
/// High-throughput, zero-allocation UDP receiver listening on the local network.
/// Works across both traditional Wi-Fi routers and Phone Hotspot connections.
/// </summary>
public sealed class UdpServer : IDisposable
{
    private readonly int _port;
    private readonly ControllerState _controllerState;
    private readonly Statistics _statistics;
    private readonly VirtualController.VirtualGamepad? _virtualGamepad;
    private readonly Action<string>? _logAction;
    private readonly CancellationTokenSource _cts = new();

    private UdpClient? _udpClient;
    private Task? _listenerTask;
    private long _lastValidSequence;
    private string? _lastConnectedIp;

    public UdpServer(
        int port,
        ControllerState controllerState,
        Statistics statistics,
        VirtualController.VirtualGamepad? virtualGamepad = null,
        Action<string>? logAction = null)
    {
        _port = port;
        _controllerState = controllerState;
        _statistics = statistics;
        _virtualGamepad = virtualGamepad;
        _logAction = logAction;
    }

    public void Start()
    {
        _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, _port));
        _logAction?.Invoke($"[INFO] UDP server started, listening on port {_port}");
        _listenerTask = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        if (_udpClient == null) return;

        try
        {
            while (!_cts.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _udpClient.ReceiveAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    _logAction?.Invoke($"[WARN] Socket error: {ex.Message}");
                    break;
                }

                ProcessIncomingDatagram(result.Buffer, result.RemoteEndPoint);
            }
        }
        catch (Exception ex)
        {
            _logAction?.Invoke($"[ERROR] Listener error: {ex.Message}");
        }
    }

    private void ProcessIncomingDatagram(byte[] buffer, IPEndPoint senderEndPoint)
    {
        if (buffer.Length == 0)
        {
            _statistics.RecordInvalidPacket(senderEndPoint);
            return;
        }

        // Validate and parse datagram
        if (!ControllerPacket.TryParse(buffer, out var packet, out var error))
        {
            _statistics.RecordInvalidPacket(senderEndPoint);
            _logAction?.Invoke($"[WARN] Invalid packet rejected: {error}");
            return;
        }

        // Sequence number validation: Drop stale or out-of-order packets
        if (_lastValidSequence > 0 && packet!.SequenceNumber <= _lastValidSequence)
        {
            _statistics.RecordStalePacket(senderEndPoint);
            // Do not log every stale packet to prevent console flood during high packet loss
            return;
        }

        // Check if new phone IP connected
        var senderIpStr = senderEndPoint.Address.ToString();
        if (_lastConnectedIp != senderIpStr)
        {
            _lastConnectedIp = senderIpStr;
            _logAction?.Invoke($"[INFO] Phone connected: {senderIpStr} (Seq #{packet!.SequenceNumber})");
        }

        // Record metrics, update controller state, and drive virtual gamepad
        _statistics.RecordValidPacket(packet!, senderEndPoint, _lastValidSequence);
        _lastValidSequence = packet!.SequenceNumber;
        _controllerState.UpdateFromPacket(packet!);
        _virtualGamepad?.Update(packet!);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _udpClient?.Dispose();
        _cts.Dispose();
    }
}
