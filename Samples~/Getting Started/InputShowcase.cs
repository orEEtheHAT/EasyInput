using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput.Samples
{
    public sealed class InputShowcase : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public Transform Preview;
        public InputPrompt Prompt;
        private InputAction look, interact;
        private int interactions;
        private GUIContent label = new GUIContent();
        private void OnEnable()
        {
            if (!Player) return;
            look = Player.Action("Gameplay/Look"); interact = Player.Action("Gameplay/Interact");
            interact.performed += Interact; Player.PresentationChanged += Refresh; Refresh();
        }
        private void OnDisable()
        {
            if (interact != null) interact.performed -= Interact;
            if (Player) Player.PresentationChanged -= Refresh;
        }
        private void Interact(InputAction.CallbackContext context) { ++interactions; Refresh(); }
        private void Refresh() => label.text = "EasyInput\n" + Player.GetPresentation("Gameplay/Interact").Text + ": interact (" + interactions + ")\nTab / Start: settings\nClose settings to rotate the preview with mouse or right stick.\nDevice: " + Player.DeviceFamily;
        private void Update()
        {
            if (!Preview || !Player) return;
            var delta = Player.ReadLookDelta(look, Time.deltaTime);
            Preview.Rotate(Vector3.up, delta.x, Space.World);
            Preview.Rotate(Vector3.right, -delta.y, Space.World);
        }
        private void OnGUI() => GUI.Box(new Rect(Mathf.Max(630, Screen.width - 430), 30, 410, 140), label);
    }
}
