using System;
using System.Diagnostics;
using System.Net;
using WindowsBridge.Protocol;

namespace WindowsBridge.Diagnostics;

/// <summary>
/// Telemetry statistics tracking packet rate, phone IP, sequence gaps,
/// stale/invalid packets, and network latency.
/// </summary>
public sealed class Statistics
{
    private readonly object _lock = new();

    public string PhoneIp { get; private set; } = "Not connected";
    public long TotalPackets { get; private set; }
    public long ValidPackets { get; private set; }
    public long InvalidPackets { get; private set; }
    public long StalePackets { get; private set; }
    public long DroppedPackets { get; private set; }
    public long LastSequence { get; private set; }

    public double PacketsPerSecond { get; private set; }
    public double LatencyMs { get; private set; }

    private long _intervalPackets;
    private readonly Stopwatch _ppsStopwatch = Stopwatch.StartNew();

    public void RecordValidPacket(ControllerPacket packet, IPEndPoint senderEndpoint, long previousSequence)
    {
        lock (_lock)
        {
            TotalPackets++;
            ValidPackets++;
            _intervalPackets++;
            LastSequence = packet.SequenceNumber;
            PhoneIp = senderEndpoint.Address.ToString();

            // Detect gaps in sequence numbers
            if (previousSequence > 0 && packet.SequenceNumber > previousSequence + 1)
            {
                DroppedPackets += (packet.SequenceNumber - previousSequence - 1);
            }

            // Approximate one-way latency calculation
            if (packet.Timestamp > 0)
            {
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var delta = nowMs - packet.Timestamp;
                if (delta is >= 0 and < 2000)
                {
                    LatencyMs = (LatencyMs * 0.85) + (delta * 0.15);
                }
            }

            UpdatePps();
        }
    }

    public void RecordStalePacket(IPEndPoint senderEndpoint)
    {
        lock (_lock)
        {
            TotalPackets++;
            StalePackets++;
            PhoneIp = senderEndpoint.Address.ToString();
            UpdatePps();
        }
    }

    public void RecordInvalidPacket(IPEndPoint? senderEndpoint)
    {
        lock (_lock)
        {
            TotalPackets++;
            InvalidPackets++;
            if (senderEndpoint != null)
            {
                PhoneIp = senderEndpoint.Address.ToString();
            }
            UpdatePps();
        }
    }

    private void UpdatePps()
    {
        var elapsed = _ppsStopwatch.ElapsedMilliseconds;
        if (elapsed >= 1000)
        {
            PacketsPerSecond = _intervalPackets * 1000.0 / elapsed;
            _intervalPackets = 0;
            _ppsStopwatch.Restart();
        }
    }

    public (string PhoneIp, long Total, long Valid, long Stale, long Invalid, long Dropped, long LastSeq, double Pps, double Latency) Snapshot()
    {
        lock (_lock)
        {
            if (_ppsStopwatch.ElapsedMilliseconds > 1500 && _intervalPackets == 0)
            {
                PacketsPerSecond = 0;
            }

            return (PhoneIp, TotalPackets, ValidPackets, StalePackets, InvalidPackets, DroppedPackets, LastSequence, PacketsPerSecond, LatencyMs);
        }
    }
}
