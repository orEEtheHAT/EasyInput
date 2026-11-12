using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput
{
    [Serializable] public sealed class InputSettingValue { public string Key; public float Value; }
    [Serializable]
    public sealed class InputSettingsSnapshot
    {
        public int Version = 1;
        public List<InputSettingValue> Values = new List<InputSettingValue>();
        public string BindingOverrides;
    }
    [Serializable] internal sealed class OverrideDocument { public List<OverrideRecord> bindings; }
    [Serializable] internal sealed class OverrideRecord { public string action, id, path, interactions, processors; }

    public sealed partial class PlayerInputSettings
    {
        public InputSettingsSnapshot CaptureSettings()
        {
            var s = Settings;
            return new InputSettingsSnapshot
            {
                BindingOverrides = Actions.SaveBindingOverridesAsJson(),
                Values = new List<InputSettingValue>
                {
                    new InputSettingValue { Key = "mouseSensitivity", Value = s.MouseSensitivity },
                    new InputSettingValue { Key = "gamepadSensitivity", Value = s.GamepadSensitivity },
                    new InputSettingValue { Key = "invertX", Value = s.InvertX ? 1 : 0 },
                    new InputSettingValue { Key = "invertY", Value = s.InvertY ? 1 : 0 },
                    new InputSettingValue { Key = "stickDeadzoneMin", Value = s.StickDeadzoneMin },
                    new InputSettingValue { Key = "stickDeadzoneMax", Value = s.StickDeadzoneMax },
                    new InputSettingValue { Key = "devicePreference", Value = (int)s.DevicePreference }
                }
            };
        }
        public bool RestoreSettingsJson(string json, out string error)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 1024 * 1024) { error = "Settings JSON is empty or too large."; return false; }
            try { return RestoreSettings(JsonUtility.FromJson<InputSettingsSnapshot>(json), out error); }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            { error = "Invalid settings JSON: " + exception.Message; return false; }
        }
        public bool RestoreSettings(InputSettingsSnapshot snapshot, out string error)
        {
            EnsureInitialized();
            if (snapshot == null || snapshot.Version != 1) { error = "Unsupported settings format. Expected Version 1."; return false; }
            var value = profile && profile.Defaults.IsValid ? profile.Defaults : UserInputSettings.Default;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot.Values != null) foreach (var entry in snapshot.Values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key) || !keys.Add(entry.Key) || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                { error = "Settings contain a duplicate, missing or invalid value."; return false; }
                switch (entry.Key)
                {
                    case "mouseSensitivity": value.MouseSensitivity = entry.Value; break;
                    case "gamepadSensitivity": value.GamepadSensitivity = entry.Value; break;
                    case "stickDeadzoneMin": value.StickDeadzoneMin = entry.Value; break;
                    case "stickDeadzoneMax": value.StickDeadzoneMax = entry.Value; break;
                    case "invertX": case "invertY":
                        if (entry.Value != 0 && entry.Value != 1) { error = "Inversion must be 0 or 1."; return false; }
                        if (entry.Key == "invertX") value.InvertX = entry.Value != 0; else value.InvertY = entry.Value != 0;
                        break;
                    case "devicePreference":
                        if (entry.Value != (int)entry.Value) { error = "Invalid device preference."; return false; }
                        value.DevicePreference = (InputDevicePreference)(int)entry.Value; break;
                    // Unknown future setting keys are intentionally ignored; missing keys keep profile defaults.
                }
            }
            if (!value.IsValid) { error = "Settings are outside supported ranges."; return false; }
            string filtered;
            try
            {
                var document = string.IsNullOrEmpty(snapshot.BindingOverrides) ? new OverrideDocument { bindings = new List<OverrideRecord>() } : JsonUtility.FromJson<OverrideDocument>(snapshot.BindingOverrides);
                if (document?.bindings == null || document.bindings.Count > 10000) { error = "Invalid binding override document."; return false; }
                var ids = new HashSet<Guid>();
                var known = new HashSet<Guid>();
                foreach (var map in actions.actionMaps) foreach (var binding in map.bindings) known.Add(binding.id);
                var valid = new List<OverrideRecord>();
                foreach (var binding in document.bindings)
                {
                    if (binding == null || !Guid.TryParse(binding.id, out var id) || !ids.Add(id) || binding.path == null || binding.path.Length > 512 ||
                        binding.path != "" && binding.path != "null" && (!binding.path.StartsWith("<", StringComparison.Ordinal) || !binding.path.Contains(">/")))
                    { error = "Invalid or duplicate binding ID/path."; return false; }
                    // User rebinding owns paths only. Processors/interactions are configuration, not executable save data.
                    if (!string.IsNullOrEmpty(binding.processors) && binding.processors != "null" || !string.IsNullOrEmpty(binding.interactions) && binding.interactions != "null")
                    { error = "Save data may override paths, not processors or interactions."; return false; }
                    binding.processors = "null"; binding.interactions = "null";
                    if (known.Contains(id)) valid.Add(binding);
                }
                filtered = JsonUtility.ToJson(new OverrideDocument { bindings = valid });
            }
            catch (ArgumentException exception) { error = "Invalid binding JSON: " + exception.Message; return false; }
            // Validate and normalize everything before touching the live player.
            CancelRebind();
            applyingBindings = true;
            try { actions.LoadBindingOverridesFromJson(filtered, true); }
            finally { applyingBindings = false; }
            SetSettings(value); ApplyDeadzoneParameters(); RecomputeContexts(); NotifyBindings(); error = null; return true;
        }
    }
}
