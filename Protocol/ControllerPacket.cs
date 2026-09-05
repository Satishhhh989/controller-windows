using System;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsBridge.Protocol;

/// <summary>
/// Packet data model exactly matching the Android phone's transmission.
/// Supports both compact JSON and 24-byte binary datagrams.
/// </summary>
public sealed class ControllerPacket
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("v")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("seq")]
    public long SequenceNumber { get; set; }

    [JsonPropertyName("t")]
    public long Timestamp { get; set; }

    [JsonPropertyName("steer")]
    public double Steering { get; set; }

    [JsonPropertyName("thrtl")]
    public double Throttle { get; set; }

    [JsonPropertyName("brake")]
    public double Brake { get; set; }

    [JsonPropertyName("hb")]
    public bool Handbrake { get; set; }

    [JsonPropertyName("gu")]
    public bool GearUp { get; set; }

    [JsonPropertyName("gd")]
    public bool GearDown { get; set; }

    // Fallback property aliases for compatibility
    [JsonPropertyName("protocolVersion")]
    public int? AltVersion { set { if (value.HasValue) Version = value.Value; } }

    [JsonPropertyName("sequenceNumber")]
    public long? AltSeq { set { if (value.HasValue) SequenceNumber = value.Value; } }

    [JsonPropertyName("timestamp")]
    public long? AltTimestamp { set { if (value.HasValue) Timestamp = value.Value; } }

    [JsonPropertyName("steering")]
    public double? AltSteering { set { if (value.HasValue) Steering = value.Value; } }

    [JsonPropertyName("throttle")]
    public double? AltThrottle { set { if (value.HasValue) Throttle = value.Value; } }

    [JsonPropertyName("handbrake")]
    public bool? AltHb { set { if (value.HasValue) Handbrake = value.Value; } }

    [JsonPropertyName("gearUp")]
    public bool? AltGu { set { if (value.HasValue) GearUp = value.Value; } }

    [JsonPropertyName("gearDown")]
    public bool? AltGd { set { if (value.HasValue) GearDown = value.Value; } }

    /// <summary>
    /// Validates field ranges and protocol constraints.
    /// Rejects malformed values and impossible controller ranges.
    /// </summary>
    public bool IsValid(out string reason)
    {
        if (Version != CurrentVersion)
        {
            reason = $"Unsupported protocol version: {Version} (expected {CurrentVersion})";
            return false;
        }

        if (double.IsNaN(Steering) || double.IsInfinity(Steering) || Steering is < -1.05 or > 1.05)
        {
            reason = $"Steering out of valid range [-1.0, 1.0]: {Steering}";
            return false;
        }

        if (double.IsNaN(Throttle) || double.IsInfinity(Throttle) || Throttle is < -0.05 or > 1.05)
        {
            reason = $"Throttle out of valid range [0.0, 1.0]: {Throttle}";
            return false;
        }

        if (double.IsNaN(Brake) || double.IsInfinity(Brake) || Brake is < -0.05 or > 1.05)
        {
            reason = $"Brake out of valid range [0.0, 1.0]: {Brake}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Try parsing an incoming UDP datagram.
    /// Automatically detects JSON ({...}) vs 24-byte compact binary.
    /// </summary>
    public static bool TryParse(byte[] payload, out ControllerPacket? packet, out string error)
    {
        packet = null;
        error = string.Empty;

        if (payload.Length == 0)
        {
            error = "Empty payload";
            return false;
        }

        // Detect JSON datagram
        if (payload[0] == '{')
        {
            try
            {
                var utf8Span = payload.AsSpan();
                packet = JsonSerializer.Deserialize<ControllerPacket>(utf8Span);

                if (packet == null)
                {
                    error = "Deserialized null packet";
                    return false;
                }

                return packet.IsValid(out error);
            }
            catch (Exception ex)
            {
                error = $"JSON parse error: {ex.Message}";
                return false;
            }
        }

        // Detect 24-byte compact binary datagram
        if (payload.Length >= 24)
        {
            try
            {
                var span = payload.AsSpan();
                var version = span[0];
                var seq = BinaryPrimitives.ReadInt64BigEndian(span.Slice(1, 8));
                var ts = BinaryPrimitives.ReadInt64BigEndian(span.Slice(9, 8));
                var steerRaw = BinaryPrimitives.ReadInt16BigEndian(span.Slice(17, 2));
                var thrtlRaw = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(19, 2));
                var brakeRaw = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(21, 2));
                var buttons = span[23];

                packet = new ControllerPacket
                {
                    Version = version,
                    SequenceNumber = seq,
                    Timestamp = ts,
                    Steering = Math.Clamp(steerRaw / 32767.0, -1.0, 1.0),
                    Throttle = Math.Clamp(thrtlRaw / 65535.0, 0.0, 1.0),
                    Brake = Math.Clamp(brakeRaw / 65535.0, 0.0, 1.0),
                    Handbrake = (buttons & 0x01) != 0,
                    GearUp = (buttons & 0x02) != 0,
                    GearDown = (buttons & 0x04) != 0,
                };

                return packet.IsValid(out error);
            }
            catch (Exception ex)
            {
                error = $"Binary parse error: {ex.Message}";
                return false;
            }
        }

        error = $"Unknown payload format (length {payload.Length} bytes)";
        return false;
    }
}
