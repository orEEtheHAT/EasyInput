using System;
using UnityEngine;

namespace EasyInput
{
    public enum InputDevicePreference { Automatic, KeyboardMouse, Gamepad }
    public enum InputDeviceFamily { KeyboardMouse, Xbox, PlayStation, SwitchPro, GenericGamepad }
    public enum InputCursorPolicy { Inherit, Locked, Free, Confined, Hidden }

    [Serializable]
    public struct UserInputSettings : IEquatable<UserInputSettings>
    {
        [Range(.05f, 10f)] public float MouseSensitivity;
        [Range(.05f, 10f)] public float GamepadSensitivity;
        public bool InvertX, InvertY;
        [Range(0f, .9f)] public float StickDeadzoneMin;
        [Range(.1f, 1f)] public float StickDeadzoneMax;
        public InputDevicePreference DevicePreference;
        public static UserInputSettings Default => new UserInputSettings
        { MouseSensitivity = 1, GamepadSensitivity = 1, StickDeadzoneMin = .125f, StickDeadzoneMax = .925f };
        public bool IsValid => Finite(MouseSensitivity) && MouseSensitivity >= .05f && MouseSensitivity <= 10 &&
            Finite(GamepadSensitivity) && GamepadSensitivity >= .05f && GamepadSensitivity <= 10 &&
            Finite(StickDeadzoneMin) && StickDeadzoneMin >= 0 && StickDeadzoneMin <= .9f &&
            Finite(StickDeadzoneMax) && StickDeadzoneMax <= 1 && StickDeadzoneMax > StickDeadzoneMin &&
            Enum.IsDefined(typeof(InputDevicePreference), DevicePreference);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public bool Equals(UserInputSettings other) => MouseSensitivity == other.MouseSensitivity &&
            GamepadSensitivity == other.GamepadSensitivity && InvertX == other.InvertX && InvertY == other.InvertY &&
            StickDeadzoneMin == other.StickDeadzoneMin && StickDeadzoneMax == other.StickDeadzoneMax && DevicePreference == other.DevicePreference;
    }

    [Serializable]
    public sealed class InputContextRequest
    {
        public string[] EnabledMaps = Array.Empty<string>();
        [Tooltip("All owners' restrictions apply. Use * to block every map.")]
        public string[] BlockedMaps = Array.Empty<string>();
        [Tooltip("Optional individual actions by map/name or stable ID. Useful for blocking Look while retaining Move.")]
        public string[] BlockedActions = Array.Empty<string>();
        public int Priority;
        public InputCursorPolicy Cursor = InputCursorPolicy.Inherit;
        public static InputContextRequest UI(int priority = 100) => new InputContextRequest
        { EnabledMaps = new[] { "UI" }, BlockedMaps = new[] { "Gameplay", "Building" }, Priority = priority, Cursor = InputCursorPolicy.Free };
        public static InputContextRequest Building(int priority = 10) => new InputContextRequest
        { EnabledMaps = new[] { "Building" }, BlockedMaps = new[] { "Gameplay" }, Priority = priority, Cursor = InputCursorPolicy.Locked };
        /// <summary>Standard FPS building preset. Move/Look/Sprint and Settings stay available.</summary>
        public static InputContextRequest BuildingFirstPerson(int priority = 10) => new InputContextRequest
        {
            EnabledMaps = new[] { "Building" }, Priority = priority, Cursor = InputCursorPolicy.Locked,
            BlockedActions = new[] { "Gameplay/Fire", "Gameplay/Aim", "Gameplay/Jump", "Gameplay/Crouch", "Gameplay/Interact",
                "Gameplay/CancelInteraction", "Gameplay/NextInteraction", "Gameplay/SecondaryInteraction", "Gameplay/NextTarget",
                "Gameplay/ThrowItem", "Gameplay/RotateItem", "Gameplay/ItemRotation", "Gameplay/ItemDistance", "Gameplay/InteractionValue" }
        };
        internal InputContextRequest Copy() => new InputContextRequest
        { EnabledMaps = (string[])(EnabledMaps ?? Array.Empty<string>()).Clone(), BlockedMaps = (string[])(BlockedMaps ?? Array.Empty<string>()).Clone(), BlockedActions = (string[])(BlockedActions ?? Array.Empty<string>()).Clone(), Priority = Priority, Cursor = Cursor };
    }

    [CreateAssetMenu(menuName = "EasyInput/Input Profile")]
    public sealed class InputProfile : ScriptableObject
    {
        [Tooltip("Degrees per mouse pixel. No deltaTime is applied to mouse displacement.")]
        public Vector2 MouseDegreesPerPixel = new Vector2(.08f, .08f);
        [Tooltip("Degrees per second at full stick. Time is applied once by ReadLookDelta.")]
        [Min(0)] public float GamepadDegreesPerSecond = 160;
        [Min(0)] public float LookMultiplier = 1;
        public UserInputSettings Defaults = UserInputSettings.Default;
        public string[] DefaultMaps = { "Gameplay" };
        public InputCursorPolicy DefaultCursor = InputCursorPolicy.Locked;
        public InputGlyphSet[] GlyphSets = Array.Empty<InputGlyphSet>();
    }
}
