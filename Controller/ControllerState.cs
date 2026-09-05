using System;
using WindowsBridge.Protocol;

namespace WindowsBridge.Controller;

/// <summary>
/// Thread-safe active controller state on the Windows machine.
/// Provides atomic updates and safe neutral resetting.
/// </summary>
public sealed class ControllerState
{
    private readonly object _lock = new();

    public double Steering { get; private set; }
    public double Throttle { get; private set; }
    public double Brake { get; private set; }
    public bool Handbrake { get; private set; }
    public bool GearUp { get; private set; }
    public bool GearDown { get; private set; }

    public long LastSequence { get; private set; }
    public DateTime LastReceivedUtc { get; private set; } = DateTime.MinValue;
    public bool IsConnected { get; private set; }

    public void UpdateFromPacket(ControllerPacket packet)
    {
        lock (_lock)
        {
            Steering = Math.Clamp(packet.Steering, -1.0, 1.0);
            Throttle = Math.Clamp(packet.Throttle, 0.0, 1.0);
            Brake = Math.Clamp(packet.Brake, 0.0, 1.0);
            Handbrake = packet.Handbrake;
            GearUp = packet.GearUp;
            GearDown = packet.GearDown;

            LastSequence = packet.SequenceNumber;
            LastReceivedUtc = DateTime.UtcNow;
            IsConnected = true;
        }
    }

    /// <summary>
    /// Safety reset: immediately clears all controls to neutral zero state.
    /// Triggered by watchdog on connection loss.
    /// </summary>
    public void ResetToNeutral()
    {
        lock (_lock)
        {
            Steering = 0.0;
            Throttle = 0.0;
            Brake = 0.0;
            Handbrake = false;
            GearUp = false;
            GearDown = false;
            IsConnected = false;
        }
    }

    public (double Steering, double Throttle, double Brake, bool Handbrake, bool GearUp, bool GearDown, bool IsConnected, long LastSequence) Snapshot()
    {
        lock (_lock)
        {
            return (Steering, Throttle, Brake, Handbrake, GearUp, GearDown, IsConnected, LastSequence);
        }
    }
}
