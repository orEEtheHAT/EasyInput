using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput
{
    /// <summary>Standalone IMGUI settings view. All gameplay/navigation input uses this player's New Input System actions.</summary>
    [AddComponentMenu("EasyInput/Input Settings Panel")]
    public sealed class InputSettingsPanel : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public bool OpenOnStart;
        public string[] RebindActions = { "Gameplay/Interact", "Gameplay/Jump", "Gameplay/Move", "Gameplay/Fire", "Gameplay/Look" };
        public bool IsOpen => context != null && !context.IsReleased;
        private InputContextHandle context;
        private InputAction open, close, navigate, submit, reset;
        private InputSettingsSnapshot saved;
        private int actionIndex, selectedRow;
        private string message = "Changes apply immediately. Escape closes. Tab opens settings.";
        private double nextNavigation;
        private GUIStyle titleStyle;
        private void Start()
        {
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player) { Debug.LogError("Input Settings Panel: assign Player Input Settings.", this); enabled = false; return; }
            open = Player.Actions.FindAction("Gameplay/Settings"); close = Player.Actions.FindAction("UI/Cancel");
            navigate = Player.Actions.FindAction("UI/Navigate"); submit = Player.Actions.FindAction("UI/Submit");
            reset = Player.Actions.FindAction("UI/Reset");
            if (OpenOnStart) Open();
        }
        public void Open()
        {
            if (!Player || IsOpen) return;
            context = Player.AcquireContext(this, InputContextRequest.UI()); selectedRow = 0;
        }
        public void Close()
        {
            if (Player && Player.Rebind?.Owner == this && Player.Rebind.IsActive) Player.CancelRebind();
            context?.Dispose(); context = null;
        }
        private void OnDisable() => Close();
        private void Update()
        {
            if (!Player) return;
            if (!IsOpen) { if (open?.WasPressedThisFrame() == true) Open(); return; }
            if (Player.Rebind?.IsActive == true) return;
            if (close?.WasPressedThisFrame() == true) { Close(); return; }
            Vector2 direction = navigate?.ReadValue<Vector2>() ?? Vector2.zero;
            if (direction.sqrMagnitude > .25f && Time.realtimeSinceStartupAsDouble >= nextNavigation)
            {
                nextNavigation = Time.realtimeSinceStartupAsDouble + .2;
                if (Mathf.Abs(direction.y) > .5f) selectedRow = (selectedRow + (direction.y < 0 ? 1 : RowCount - 1)) % RowCount;
                else if (selectedRow == 0) Player.MouseSensitivity = Mathf.Clamp(Player.MouseSensitivity + Mathf.Sign(direction.x) * .1f, .05f, 10);
                else if (selectedRow == 1) Player.GamepadSensitivity = Mathf.Clamp(Player.GamepadSensitivity + Mathf.Sign(direction.x) * .1f, .05f, 10);
                else if (selectedRow == 4) { var s = Player.Settings; s.StickDeadzoneMin = Mathf.Clamp(s.StickDeadzoneMin + Mathf.Sign(direction.x) * .025f, 0, Mathf.Min(.9f, s.StickDeadzoneMax - .05f)); Player.SetSettings(s); }
                else if (selectedRow == 5) { var s = Player.Settings; s.StickDeadzoneMax = Mathf.Clamp(s.StickDeadzoneMax + Mathf.Sign(direction.x) * .025f, s.StickDeadzoneMin + .05f, 1); Player.SetSettings(s); }
            }
            if (submit?.WasPressedThisFrame() == true) Activate(selectedRow);
            if (reset?.WasPressedThisFrame() == true && SelectedAction != null && selectedRow >= 8 && selectedRow < 8 + SelectedAction.bindings.Count && !SelectedAction.bindings[selectedRow - 8].isComposite)
                Player.ResetBinding(SelectedAction, selectedRow - 8);
        }
        private InputAction SelectedAction => Player && RebindActions.Length > 0 ? Player.Actions.FindAction(RebindActions[actionIndex], false) : null;
        private int RowCount => 12 + (SelectedAction?.bindings.Count ?? 0);
        private void Activate(int row)
        {
            var action = SelectedAction;
            if (row == 2) { var s = Player.Settings; s.InvertY = !s.InvertY; Player.SetSettings(s); }
            else if (row == 3) { var s = Player.Settings; s.InvertX = !s.InvertX; Player.SetSettings(s); }
            else if (row == 6) { var s = Player.Settings; s.DevicePreference = (InputDevicePreference)(((int)s.DevicePreference + 1) % 3); Player.SetSettings(s); }
            else if (row == 7) { actionIndex = (actionIndex + 1) % Math.Max(1, RebindActions.Length); selectedRow = 7; }
            else if (row >= 8 && action != null && row < 8 + action.bindings.Count)
            {
                int binding = row - 8;
                if (!action.bindings[binding].isComposite) Player.BeginRebind(action, binding, this);
            }
            else if (row == RowCount - 4) { saved = Player.CaptureSettings(); message = "Captured settings in memory."; }
            else if (row == RowCount - 3) { message = saved == null ? "Capture settings first." : Player.RestoreSettings(saved, out var error) ? "Settings restored." : error; }
            else if (row == RowCount - 2) { Player.ResetSettings(); message = "Restored defaults."; }
            else if (row == RowCount - 1) Close();
        }
        private bool RowButton(int row, string label, params GUILayoutOption[] options) => GUILayout.Button((selectedRow == row ? "> " : "") + label, options);
        private void OnGUI()
        {
            if (!IsOpen || !Player) return;
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            float scale = Mathf.Max(.65f, Screen.height / 900f);
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(18, 18, 600, Math.Min(860, Screen.height / scale - 36)), GUI.skin.box);
            GUILayout.Label("EasyInput — Controls", titleStyle);
            GUILayout.Label("Device: " + Player.DeviceFamily + " | D-pad: navigate, South: select, East: close");
            bool rebinding = Player.Rebind?.IsActive == true;
            GUI.enabled = !rebinding;
            GUILayout.Label((selectedRow == 0 ? "> " : "") + "Mouse sensitivity: " + Player.MouseSensitivity.ToString("0.00"));
            float mouse = GUILayout.HorizontalSlider(Player.MouseSensitivity, .05f, 5);
            if (!Mathf.Approximately(mouse, Player.MouseSensitivity)) Player.MouseSensitivity = mouse;
            GUILayout.Label((selectedRow == 1 ? "> " : "") + "Gamepad sensitivity: " + Player.GamepadSensitivity.ToString("0.00"));
            float gamepad = GUILayout.HorizontalSlider(Player.GamepadSensitivity, .05f, 5);
            if (!Mathf.Approximately(gamepad, Player.GamepadSensitivity)) Player.GamepadSensitivity = gamepad;
            if (RowButton(2, "Invert vertical: " + Player.Settings.InvertY)) Activate(2);
            if (RowButton(3, "Invert horizontal: " + Player.Settings.InvertX)) Activate(3);
            var settings = Player.Settings;
            GUILayout.Label((selectedRow == 4 ? "> " : "") + "Stick deadzone start: " + settings.StickDeadzoneMin.ToString("0.00"));
            float deadzone = GUILayout.HorizontalSlider(settings.StickDeadzoneMin, 0, Mathf.Min(.9f, settings.StickDeadzoneMax - .05f));
            if (!Mathf.Approximately(deadzone, settings.StickDeadzoneMin)) { settings.StickDeadzoneMin = deadzone; Player.SetSettings(settings); }
            GUILayout.Label((selectedRow == 5 ? "> " : "") + "Stick reaches full speed at: " + settings.StickDeadzoneMax.ToString("0.00"));
            float outer = GUILayout.HorizontalSlider(settings.StickDeadzoneMax, settings.StickDeadzoneMin + .05f, 1);
            if (!Mathf.Approximately(outer, settings.StickDeadzoneMax)) { settings.StickDeadzoneMax = outer; Player.SetSettings(settings); }
            if (RowButton(6, "Device mode: " + settings.DevicePreference)) Activate(6);
            var action = SelectedAction;
            if (RowButton(7, "Action: " + (action?.name ?? "Missing action") + " (next)")) Activate(7);
            if (action != null) for (int i = 0; i < action.bindings.Count; ++i)
            {
                if (action.bindings[i].isComposite) { GUILayout.Label("Composite: " + action.bindings[i].name + " — rebind each part below"); continue; }
                GUILayout.BeginHorizontal();
                var b = action.bindings[i];
                if (RowButton(8 + i, (b.isPartOfComposite ? b.name + ": " : b.groups + ": ") + Player.GetPresentation(action, i).Text)) Activate(8 + i);
                if (GUILayout.Button("Reset", GUILayout.Width(65))) Player.ResetBinding(action, i);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (RowButton(RowCount - 4, "Capture")) Activate(RowCount - 4);
            if (RowButton(RowCount - 3, "Restore")) Activate(RowCount - 3);
            if (RowButton(RowCount - 2, "Reset all")) Activate(RowCount - 2);
            GUILayout.EndHorizontal();
            GUILayout.Label("Backspace / North resets the selected binding.");
            if (RowButton(RowCount - 1, "Close settings")) Close();
            GUI.enabled = true;
            GUILayout.Space(8);
            GUILayout.Label(Player.Rebind != null ? Player.Rebind.Message : message);
            if (rebinding && GUILayout.Button("Cancel rebinding (Escape)")) Player.CancelRebind();
            GUILayout.EndArea(); GUI.matrix = matrix;
        }
    }
}
