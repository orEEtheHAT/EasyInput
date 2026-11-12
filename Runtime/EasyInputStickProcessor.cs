using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EasyInput
{
    /// <summary>One radial deadzone, replacing the device stick's built-in deadzone rather than stacking with it.</summary>
    public sealed class EasyInputStickProcessor : InputProcessor<Vector2>
    {
        public float min = .125f, max = .925f;
        public override Vector2 Process(Vector2 value, InputControl control)
        {
            if (control is StickControl stick) value = stick.ReadUnprocessedValue();
            float magnitude = value.magnitude;
            if (magnitude <= min) return Vector2.zero;
            return value * (Mathf.Clamp01((magnitude - min) / Mathf.Max(.001f, max - min)) / magnitude);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register() => InputSystem.RegisterProcessor<EasyInputStickProcessor>("easyInputStick");
    }
}
