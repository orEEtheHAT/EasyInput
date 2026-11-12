using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace EasyInput
{
    public sealed partial class PlayerInputSettings
    {
        private InputDevice activeDevice;
        private Gamepad selectedGamepad;
        private InputDevice[] pairedDevices;
        private readonly List<InputDevice> allowedDevices = new List<InputDevice>(4);
        public InputDevice ActiveDevice => activeDevice;
        public Gamepad ActiveGamepad => activeDevice as Gamepad;
        public InputDeviceFamily DeviceFamily { get; private set; }
        public string ActiveControlScheme => DeviceFamily == InputDeviceFamily.KeyboardMouse ? "KeyboardMouse" : "Gamepad";

        /// <summary>Optional device isolation for local players. Null returns to automatic single-player selection.</summary>
        public void PairDevices(params InputDevice[] devices)
        {
            EnsureInitialized();
            pairedDevices = devices == null ? null : (InputDevice[])devices.Clone();
            RefreshDevices();
        }
        public void SelectGamepad(Gamepad gamepad)
        {
            selectedGamepad = gamepad;
            RefreshDevices();
            if (gamepad != null && gamepad.added && IsPaired(gamepad)) SetActiveDevice(gamepad);
        }
        private bool IsPaired(InputDevice device)
        {
            if (pairedDevices == null) return true;
            for (int i = 0; i < pairedDevices.Length; ++i) if (pairedDevices[i] == device) return true;
            return false;
        }
        private void RefreshDevices()
        {
            if (!initialized) return;
            allowedDevices.Clear();
            if (selectedGamepad == null || !selectedGamepad.added || !selectedGamepad.enabled || !IsPaired(selectedGamepad))
            {
                selectedGamepad = null;
                foreach (var gamepad in Gamepad.all) if (gamepad.enabled && IsPaired(gamepad)) { selectedGamepad = gamepad; break; }
            }
            foreach (var device in InputSystem.devices)
            {
                if (!IsPaired(device) || !device.enabled) continue;
                if ((device is Keyboard || device is Mouse) && settings.DevicePreference != InputDevicePreference.Gamepad) allowedDevices.Add(device);
                else if (device == selectedGamepad && settings.DevicePreference != InputDevicePreference.KeyboardMouse) allowedDevices.Add(device);
            }
            // Input System already removes a disconnected device from resolved controls while
            // preserving other controls' phases. Avoid turning that into a full binding rebuild.
            bool sameDevices = actions.devices.HasValue;
            int liveCount = 0;
            if (sameDevices) foreach (var device in actions.devices.Value)
            {
                if (!device.added || !device.enabled) continue;
                liveCount++;
                if (!allowedDevices.Contains(device)) sameDevices = false;
            }
            sameDevices &= liveCount == allowedDevices.Count;
            if (!sameDevices)
            {
                bool previousMutation = applyingBindings; applyingBindings = true;
                try
                {
                    actions.devices = allowedDevices.ToArray();
                    // Finish any deferred resolution while the ownership guard is active.
                    foreach (var map in actions.actionMaps) foreach (var action in map.actions) _ = action.controls.Count;
                }
                finally { applyingBindings = previousMutation; }
            }
            if (activeDevice == null || !allowedDevices.Contains(activeDevice))
            {
                InputDevice fallback = null;
                if (settings.DevicePreference == InputDevicePreference.Gamepad) fallback = selectedGamepad;
                else foreach (var device in allowedDevices) if (device is Keyboard || device is Mouse) { fallback = device; break; }
                SetActiveDevice(fallback ?? (allowedDevices.Count > 0 ? allowedDevices[0] : null));
            }
            RecomputeContexts();
        }
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Disconnected || change == InputDeviceChange.Removed || change == InputDeviceChange.Disabled ||
                change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled)
            {
                if ((change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled) &&
                    (device == activeDevice || device == selectedGamepad))
                {
                    // Input System resets the removed device. Reset actions too, so hold consumers receive cancellation.
                    foreach (var map in actions.actionMaps) foreach (var action in map.actions)
                        if (action.activeControl?.device == device) action.Reset();
                    CancelRebind();
                    activeDevice = null;
                }
                RefreshDevices();
            }
        }
        private void OnInputEvent(InputEventPtr evt, InputDevice device)
        {
            if (!IsPaired(device) || (!evt.IsA<StateEvent>() && !evt.IsA<DeltaStateEvent>())) return;
            if (Rebind?.IsActive == true)
            {
                var cancel = device is Keyboard keyboard ? keyboard.escapeKey : device is Gamepad pad ? pad.buttonEast : null;
                if (cancel != null && !cancel.isPressed && cancel.ReadValueFromEvent(evt, out float pressed) && pressed >= cancel.pressPointOrDefault)
                {
                    // Independent of disabled UI actions. Consume this press so it cannot also close a window.
                    CancelRebind(); evt.handled = true; return;
                }
            }
            if (device is Gamepad gamepad)
            {
                if (settings.DevicePreference == InputDevicePreference.KeyboardMouse) return;
                float threshold = Mathf.Max(.25f, settings.StickDeadzoneMin + .1f);
                bool meaningful = gamepad.leftStick.ReadValueFromEvent(evt, out Vector2 left) && left.sqrMagnitude > threshold * threshold ||
                    gamepad.rightStick.ReadValueFromEvent(evt, out Vector2 right) && right.sqrMagnitude > threshold * threshold;
                if (!meaningful) meaningful = HasButtonPress(evt, device);
                if (!meaningful) return;
                if (selectedGamepad != gamepad) { selectedGamepad = gamepad; RefreshDevices(); }
                SetActiveDevice(device);
            }
            else if (settings.DevicePreference != InputDevicePreference.Gamepad && device is Keyboard)
            { if (HasButtonPress(evt, device)) SetActiveDevice(device); }
            else if (settings.DevicePreference != InputDevicePreference.Gamepad && device is Mouse mouse)
            {
                if (HasButtonPress(evt, device) || mouse.delta.ReadValueFromEvent(evt, out Vector2 delta) && delta.sqrMagnitude >= 4 ||
                    mouse.scroll.ReadValueFromEvent(evt, out Vector2 scroll) && scroll.sqrMagnitude >= 1) SetActiveDevice(device);
            }
        }
        private static bool HasButtonPress(InputEventPtr evt, InputDevice device)
        {
            foreach (var control in evt.EnumerateChangedControls(device, .5f))
                if (!control.noisy && control is ButtonControl button && !(control.parent is StickControl) && button.ReadValueFromEvent(evt) > .5f) return true;
            return false;
        }
        private void SetActiveDevice(InputDevice device)
        {
            var family = ClassifyDevice(device);
            bool changed = activeDevice != device || DeviceFamily != family;
            activeDevice = device; DeviceFamily = family;
            if (changed) { presentationCache.Clear(); deviceChanged.Raise(); presentationChanged.Raise(); }
        }
        public static InputDeviceFamily ClassifyDevice(InputDevice device)
        {
            if (!(device is Gamepad)) return InputDeviceFamily.KeyboardMouse;
            string identity = (device.layout + " " + device.description.manufacturer + " " + device.description.product).ToLowerInvariant();
            if (identity.Contains("dualshock") || identity.Contains("dualsense") || identity.Contains("playstation")) return InputDeviceFamily.PlayStation;
            if (identity.Contains("switch") || identity.Contains("nintendo")) return InputDeviceFamily.SwitchPro;
            if (identity.Contains("xinput") || identity.Contains("xbox")) return InputDeviceFamily.Xbox;
            return InputDeviceFamily.GenericGamepad;
        }
    }
}
