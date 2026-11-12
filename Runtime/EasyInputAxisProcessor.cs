using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace EasyInput
{
    /// <summary>Per-player deadzone for scalar gamepad axes used by analog interactions.</summary>
    public sealed class EasyInputAxisProcessor : InputProcessor<float>
    {
        public float min = .125f, max = .925f;
        public override float Process(float value, InputControl control)
        {
            if (control is AxisControl axis) value = axis.ReadUnprocessedValue();
            return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - min) / Mathf.Max(.001f, max - min));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register() => InputSystem.RegisterProcessor<EasyInputAxisProcessor>("easyInputAxis");
    }
}
