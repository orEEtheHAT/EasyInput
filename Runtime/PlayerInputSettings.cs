using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unity.Profiling;

namespace EasyInput
{
    /// <summary>Owns one runtime action asset. Consumers subscribe/read; only this component enables or disables maps.</summary>
    [DefaultExecutionOrder(-1000), DisallowMultipleComponent, AddComponentMenu("EasyInput/Player Input Settings")]
    public sealed partial class PlayerInputSettings : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional. Empty uses the included Gameplay, UI and Building actions.")]
        private InputActionAsset sourceActions;
        [SerializeField, Tooltip("Optional. Empty uses the included standard profile. User values never modify this asset.")]
        private InputProfile profile;
        [Tooltip("Only the player owning the local pointer should control the shared OS cursor.")]
        public bool ControlsCursor = true;
        private InputActionAsset actions;
        private UserInputSettings settings;
        private bool initialized, listening;
        private static readonly ProfilerMarker updateMarker = new ProfilerMarker("EasyInput.Update");
        public InputActionAsset Actions { get { EnsureInitialized(); return actions; } }
        public InputProfile Profile { get { EnsureInitialized(); return profile; } }
        public UserInputSettings Settings { get { EnsureInitialized(); return settings; } }
        private readonly InputSignal settingsChanged = new InputSignal(), bindingsChanged = new InputSignal(), deviceChanged = new InputSignal(), profileChanged = new InputSignal(), contextChanged = new InputSignal(), presentationChanged = new InputSignal();
        public event Action SettingsChanged { add => settingsChanged.Add(value); remove => settingsChanged.Remove(value); }
        public event Action BindingsChanged { add => bindingsChanged.Add(value); remove => bindingsChanged.Remove(value); }
        public event Action DeviceChanged { add => deviceChanged.Add(value); remove => deviceChanged.Remove(value); }
        public event Action ProfileChanged { add => profileChanged.Add(value); remove => profileChanged.Remove(value); }
        public event Action ContextChanged { add => contextChanged.Add(value); remove => contextChanged.Remove(value); }
        public event Action PresentationChanged { add => presentationChanged.Add(value); remove => presentationChanged.Remove(value); }

        public void Configure(InputActionAsset source, InputProfile configuration = null)
        {
            if (initialized) throw new InvalidOperationException("Configure before first use, on an inactive GameObject. Keep the same runtime actions when switching controllers.");
            sourceActions = source;
            profile = configuration;
        }
        private void Awake() => EnsureInitialized();
        private void EnsureInitialized()
        {
            if (initialized) return;
            if (!sourceActions) sourceActions = Resources.Load<InputActionAsset>("EasyInputDefaults");
            if (!profile) profile = Resources.Load<InputProfile>("EasyInputStandard");
            if (!sourceActions) throw new InvalidOperationException("EasyInput: assign an InputActionAsset, or reinstall the included Resources/EasyInputDefaults asset.");
            EasyInputStickProcessor.Register();
            EasyInputAxisProcessor.Register();
            actions = Instantiate(sourceActions);
            actions.name = sourceActions.name + " (runtime player)";
            actions.Disable();
            settings = profile && profile.Defaults.IsValid ? profile.Defaults : UserInputSettings.Default;
            initialized = true;
            ApplyDeadzoneParameters();
            InitializeContexts();
        }
        private void OnEnable()
        {
            EnsureInitialized();
            listening = true;
            InputSystem.onEvent += OnInputEvent;
            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onActionChange += OnActionChange;
            SaveCursor();
            RefreshDevices();
            RecomputeContexts();
        }
        private void OnDisable()
        {
            listening = false;
            CancelRebind();
            InputSystem.onEvent -= OnInputEvent;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onActionChange -= OnActionChange;
            ClearContexts();
            if (actions) actions.Disable();
            RestoreCursor();
        }
        private void OnDestroy()
        {
            if (actions) { actions.Disable(); if (Application.isPlaying) Destroy(actions); else DestroyImmediate(actions); }
            actions = null;
        }
        private void Update()
        {
            using var measurement = updateMarker.Auto();
            PruneContexts();
            ResumeReleasedMaps();
            TickRebind();
        }
        public InputAction Resolve(InputActionReference reference) => reference && reference.action != null ? Resolve(reference.action) : null;
        public InputAction Resolve(InputAction action) => action == null ? null : Actions.FindAction(action.id);
        public InputAction Action(string nameOrId) => Actions.FindAction(nameOrId, true);
        public void SetSettings(UserInputSettings value)
        {
            EnsureInitialized();
            if (!value.IsValid) throw new ArgumentOutOfRangeException(nameof(value), "Use sensitivities 0.05..10 and 0 <= deadzone min < max <= 1.");
            if (settings.Equals(value)) return;
            bool deviceChanged = settings.DevicePreference != value.DevicePreference;
            bool deadzoneChanged = settings.StickDeadzoneMin != value.StickDeadzoneMin || settings.StickDeadzoneMax != value.StickDeadzoneMax;
            settings = value;
            if (deadzoneChanged) ApplyDeadzoneParameters();
            if (deviceChanged) RefreshDevices();
            settingsChanged.Raise();
        }
        public float MouseSensitivity { get => Settings.MouseSensitivity; set { var s = Settings; s.MouseSensitivity = value; SetSettings(s); } }
        public float GamepadSensitivity { get => Settings.GamepadSensitivity; set { var s = Settings; s.GamepadSensitivity = value; SetSettings(s); } }
        public void SetProfile(InputProfile value)
        {
            EnsureInitialized();
            if (profile == value) return;
            profile = value;
            presentationCache.Clear();
            RecomputeContexts();
            profileChanged.Raise();
            presentationChanged.Raise();
        }
        public Vector2 MouseDegreesPerPixel => Vector2.Scale(profile ? profile.MouseDegreesPerPixel : new Vector2(.08f, .08f),
            Vector2.one * (Settings.MouseSensitivity * (profile ? profile.LookMultiplier : 1)));
        public float GamepadDegreesPerSecond => (profile ? profile.GamepadDegreesPerSecond * profile.LookMultiplier : 160) * Settings.GamepadSensitivity;

        /// <summary>Returns yaw/pitch degrees. Mouse pixels have no time factor; processed stick rate has exactly one.</summary>
        public Vector2 ReadLookDelta(InputAction look, float deltaTime)
        {
            if (look == null || !look.enabled) return Vector2.zero;
            return ScaleLook(look.ReadValue<Vector2>(), look.activeControl?.device is Gamepad, deltaTime);
        }
        public Vector2 ScaleLook(Vector2 value, bool gamepad, float deltaTime)
        {
            value = gamepad ? value * (GamepadDegreesPerSecond * Mathf.Max(0, deltaTime)) : Vector2.Scale(value, MouseDegreesPerPixel);
            if (Settings.InvertX) value.x = -value.x;
            // Positive Y means look up, matching EasyMovement.AddControlPitchInput.
            value.y *= Settings.InvertY ? -1 : 1;
            return value;
        }
        private void ApplyDeadzoneParameters()
        {
            // The default stick bindings explicitly use easyInputStick. This overrides that processor only;
            // callers must not apply another deadzone or sensitivity in their consumer.
            actions.ApplyParameterOverride("easyInputStick:min", settings.StickDeadzoneMin);
            actions.ApplyParameterOverride("easyInputStick:max", settings.StickDeadzoneMax);
            actions.ApplyParameterOverride("easyInputAxis:min", settings.StickDeadzoneMin);
            actions.ApplyParameterOverride("easyInputAxis:max", settings.StickDeadzoneMax);
        }
        private void OnActionChange(object changed, InputActionChange change)
        {
            bool ours = ReferenceEquals(changed, actions) || changed is InputActionMap ownedMap && ReferenceEquals(ownedMap.asset, actions) ||
                changed is InputAction ownedAction && ReferenceEquals(ownedAction.actionMap?.asset, actions);
            // Input System temporarily changes phases while rebuilding binding state. Never re-enter
            // Enable/Disable during that rebuild: it can leave duplicate control monitors behind.
            if (ours && !applyingContexts && !applyingBindings && (change == InputActionChange.ActionEnabled || change == InputActionChange.ActionMapEnabled || change == InputActionChange.ActionDisabled || change == InputActionChange.ActionMapDisabled))
            { RecomputeContexts(); return; }
            if (change != InputActionChange.BoundControlsChanged || applyingBindings) return;
            if (ReferenceEquals(changed, actions) || changed is InputActionMap map && ReferenceEquals(map.asset, actions) ||
                changed is InputAction action && ReferenceEquals(action.actionMap?.asset, actions)) NotifyBindings();
        }
        private void NotifyBindings() { presentationCache.Clear(); bindingsChanged.Raise(); presentationChanged.Raise(); }
    }
}
