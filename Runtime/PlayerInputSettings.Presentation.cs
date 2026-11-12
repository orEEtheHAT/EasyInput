using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace EasyInput
{
    public sealed partial class PlayerInputSettings
    {
        private readonly Dictionary<(Guid, int), InputPresentation> presentationCache = new Dictionary<(Guid, int), InputPresentation>();
        public InputPresentation GetPresentation(InputActionReference reference, int bindingIndex = -1) => GetPresentation(Resolve(reference), bindingIndex);
        public InputPresentation GetPresentation(string nameOrId, int bindingIndex = -1) => GetPresentation(Action(nameOrId), bindingIndex);
        public InputPresentation GetPresentation(InputAction action, int bindingIndex = -1)
        {
            action = Resolve(action);
            if (action == null) return new InputPresentation("Unassigned action");
            var key = (action.id, bindingIndex);
            if (presentationCache.TryGetValue(key, out var cached)) return cached;
            var result = BuildPresentation(action, bindingIndex); presentationCache[key] = result; return result;
        }
        private InputPresentation BuildPresentation(InputAction action, int bindingIndex)
        {
            if (bindingIndex < 0)
            {
                for (int i = 0; i < action.bindings.Count; ++i)
                {
                    var binding = action.bindings[i];
                    if (binding.isPartOfComposite) continue;
                    if (binding.isComposite)
                    {
                        if (i + 1 < action.bindings.Count && MatchesDevice(action.bindings[i + 1])) { bindingIndex = i; break; }
                    }
                    else if (MatchesDevice(binding) && !string.IsNullOrEmpty(binding.effectivePath)) { bindingIndex = i; break; }
                }
            }
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return new InputPresentation("Unbound");
            if (!action.bindings[bindingIndex].isComposite) return PresentBinding(action, bindingIndex);
            var parts = new List<InputPresentation>();
            var labels = new List<string>();
            for (int i = bindingIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; ++i)
            {
                var part = PresentBinding(action, i);
                parts.Add(part); labels.Add(action.bindings[i].name + ": " + part.Text);
            }
            return new InputPresentation(string.Join(" / ", labels), parts: parts.ToArray(), bindingId: action.bindings[bindingIndex].id.ToString());
        }
        private bool MatchesDevice(InputBinding binding)
        {
            if (!string.IsNullOrEmpty(binding.groups))
                foreach (var group in binding.groups.Split(';')) if (group == ActiveControlScheme) return true;
            var path = binding.effectivePath ?? "";
            bool gamepad = path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase) || path.Contains("Gamepad") || path.Contains("DualShock") || path.Contains("XInput") || path.Contains("SwitchPro");
            return DeviceFamily == InputDeviceFamily.KeyboardMouse ? !gamepad : gamepad;
        }
        private InputPresentation PresentBinding(InputAction action, int index)
        {
            var binding = action.bindings[index];
            string path = binding.effectivePath;
            if (string.IsNullOrEmpty(path)) return new InputPresentation("Unbound", bindingId: binding.id.ToString());
            InputGlyph glyph = null;
            if (profile && profile.GlyphSets != null)
            {
                foreach (var set in profile.GlyphSets) if (set && set.Family == DeviceFamily && (glyph = set.Find(path)) != null) break;
                if (glyph == null && DeviceFamily != InputDeviceFamily.KeyboardMouse)
                    foreach (var set in profile.GlyphSets) if (set && set.Family == InputDeviceFamily.GenericGamepad && (glyph = set.Find(path)) != null) break;
            }
            string label = glyph != null && !string.IsNullOrEmpty(glyph.Text) ? glyph.Text : action.GetBindingDisplayString(index);
            if (string.IsNullOrEmpty(label)) label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return new InputPresentation(label, glyph?.Sprite, bindingId: binding.id.ToString());
        }
    }
}
