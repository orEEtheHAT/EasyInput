using System;
using UnityEngine;

namespace EasyInput
{
    [Serializable]
    public sealed class InputGlyph
    {
        [Tooltip("Input System path, e.g. <Gamepad>/buttonSouth or <Keyboard>/space.")]
        public string ControlPath;
        public Sprite Sprite;
        public string Text;
    }
    [CreateAssetMenu(menuName = "EasyInput/Input Glyph Set")]
    public sealed class InputGlyphSet : ScriptableObject
    {
        public InputDeviceFamily Family;
        public InputGlyph[] Glyphs = Array.Empty<InputGlyph>();
        public InputGlyph Find(string path)
        {
            foreach (var glyph in Glyphs)
                if (glyph != null && string.Equals(glyph.ControlPath, path, StringComparison.OrdinalIgnoreCase)) return glyph;
            return null;
        }
    }
    public readonly struct InputPresentation
    {
        public readonly string Text;
        public readonly Sprite Sprite;
        public readonly InputPresentation[] Parts;
        public readonly string BindingId;
        public InputPresentation(string text, Sprite sprite = null, InputPresentation[] parts = null, string bindingId = null)
        { Text = text; Sprite = sprite; Parts = parts ?? Array.Empty<InputPresentation>(); BindingId = bindingId; }
    }
}
