using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using WindowsBridge.Protocol;

namespace WindowsBridge.VirtualController;

/// <summary>
/// Emulates a genuine virtual Xbox 360 controller on Windows using ViGEmBus.
/// Enables games like Forza Horizon to natively detect the phone as a steering wheel/gamepad.
/// </summary>
public sealed class VirtualGamepad : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _controller;
    private bool _isAvailable;
    private string _statusMessage = "NOT INITIALIZED";

    public bool IsAvailable => _isAvailable;
    public string StatusMessage => _statusMessage;

    public void Initialize(Action<string>? logAction = null)
    {
        try
        {
            _client = new ViGEmClient();
            _controller = _client.CreateXbox360Controller();
            _controller.AutoSubmitReport = false; // Batch updates atomically per packet
            _controller.Connect();
            _isAvailable = true;
            _statusMessage = "XBOX 360 (ACTIVE)";
            logAction?.Invoke("[INFO] Virtual Xbox 360 controller connected via ViGEmBus");
        }
        catch (VigemBusNotFoundException)
        {
            _isAvailable = false;
            _statusMessage = "DRIVER NOT INSTALLED";
            logAction?.Invoke("[WARN] ViGEmBus driver not found. Run scripts\\install_vigem_driver.bat to enable Forza controller.");
        }
        catch (VigemBusAccessFailedException)
        {
            _isAvailable = false;
            _statusMessage = "BUS ACCESS DENIED (RUN AS ADMIN)";
            logAction?.Invoke("[WARN] ViGEmBus access failed. Try running as Administrator.");
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _statusMessage = "UNAVAILABLE";
            logAction?.Invoke($"[WARN] Virtual controller disabled: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps normalized phone controller telemetry to Xbox 360 axes, triggers, and buttons.
    /// </summary>
    public void Update(ControllerPacket packet)
    {
        if (!_isAvailable || _controller == null) return;

        try
        {
            // Steering: -1.0 .. +1.0 -> -32768 .. +32767 (Left Thumbstick X)
            var stickX = (short)Math.Clamp(Math.Round(packet.Steering * 32767.0), short.MinValue, short.MaxValue);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, stickX);

            // Throttle: 0.0 .. 1.0 -> 0 .. 255 (Right Trigger)
            var rightTrigger = (byte)Math.Clamp(Math.Round(packet.Throttle * 255.0), 0, 255);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, rightTrigger);

            // Brake: 0.0 .. 1.0 -> 0 .. 255 (Left Trigger)
            var leftTrigger = (byte)Math.Clamp(Math.Round(packet.Brake * 255.0), 0, 255);
            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, leftTrigger);

            // Handbrake -> Button A (Standard Forza e-brake)
            _controller.SetButtonState(Xbox360Button.A, packet.Handbrake);

            // Gear Up -> Right Shoulder (RB)
            _controller.SetButtonState(Xbox360Button.RightShoulder, packet.GearUp);

            // Gear Down -> Left Shoulder (LB)
            _controller.SetButtonState(Xbox360Button.LeftShoulder, packet.GearDown);

            // Submit atomic report to ViGEmBus
            _controller.SubmitReport();
        }
        catch
        {
            // Transient report errors must not interrupt the receiver loop
        }
    }

    /// <summary>
    /// Safety neutral safeguard: centers stick, releases triggers, and releases all buttons.
    /// Triggered by watchdog on phone signal loss.
    /// </summary>
    public void ResetToNeutral()
    {
        if (!_isAvailable || _controller == null) return;

        try
        {
            _controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
            _controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);
            _controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
            _controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);

            _controller.SetButtonState(Xbox360Button.A, false);
            _controller.SetButtonState(Xbox360Button.B, false);
            _controller.SetButtonState(Xbox360Button.X, false);
            _controller.SetButtonState(Xbox360Button.Y, false);
            _controller.SetButtonState(Xbox360Button.RightShoulder, false);
            _controller.SetButtonState(Xbox360Button.LeftShoulder, false);

            _controller.SubmitReport();
        }
        catch
        {
            // Ignore
        }
    }

    public void Dispose()
    {
        if (_controller != null)
        {
            try
            {
                ResetToNeutral();
                _controller.Disconnect();
            }
            catch { }
            _controller = null;
        }

        _client?.Dispose();
        _client = null;
    }
}
