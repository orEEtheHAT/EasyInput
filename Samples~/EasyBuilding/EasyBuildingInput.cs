using System;
using System.Collections.Generic;
using EasyBuilding;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput.Integrations
{
    public enum BuildingInputMode { Strategy, FirstPerson }

    [DisallowMultipleComponent, RequireComponent(typeof(BuildingPlacer)), AddComponentMenu("EasyInput/Integrations/EasyBuilding Input")]
    public sealed class EasyBuildingInput : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public BuildingPlacer Placer;
        [Tooltip("Strategy blocks Gameplay; First Person keeps movement, look and sprint.")]
        public BuildingInputMode Mode;
        [Tooltip("Compatibility default. Disable to enter explicitly with Enter().")]
        public bool AutoEnterOnEnable = true;
        [Tooltip("Disable only when another component owns the Building context.")]
        public bool EnterBuildingContext = true;
        public string GameplayMap = "Gameplay";
        [Tooltip("Additional action names/IDs to block while building, e.g. Gameplay/Reload.")]
        public string[] ExtraBlockedActions = Array.Empty<string>();
        public InputActionLink Confirm = new InputActionLink("Building/Confirm");
        public InputActionLink CancelAction = new InputActionLink("Building/Cancel");
        public InputActionLink RotateRight = new InputActionLink("Building/RotateRight");
        public InputActionLink RotateLeft = new InputActionLink("Building/RotateLeft");
        public InputActionLink Demolish = new InputActionLink("Building/Demolish");
        public bool IsBuilding { get; private set; }
        public event Action<bool> ModeChanged;
        private InputContextHandle context;
        private bool oldExternal, connected;
        private InputAction confirm, cancel, right, left, demolish;
        private BuildingSession observedSession;
        private float yaw;

        [ContextMenu("Use First Person Preset")]
        public void UseFirstPersonPreset()
        {
            Mode = BuildingInputMode.FirstPerson; AutoEnterOnEnable = false; EnterBuildingContext = true;
            if (!Placer) Placer = GetComponent<BuildingPlacer>();
            if (Placer) Placer.AimFromScreenCenter = true;
        }
        private void OnEnable()
        {
            if (!Placer) Placer = GetComponent<BuildingPlacer>();
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player || !Placer) { Debug.LogError("EasyBuilding Input: assign Player Input Settings and Building Placer.", this); enabled = false; return; }
            try
            {
                confirm = Confirm.Resolve(Player, true); cancel = CancelAction.Resolve(Player, true);
                right = RotateRight.Resolve(Player); left = RotateLeft.Resolve(Player); demolish = Demolish.Resolve(Player);
            }
            catch (ArgumentException e) { Debug.LogError(e.Message, this); enabled = false; return; }
            oldExternal = Placer.ExternalTargeting; Placer.ExternalTargeting = true; connected = true;
            Placer.SessionChanged += SessionChanged; SessionChanged(Placer.Session);
            if (AutoEnterOnEnable) Enter();
        }
        /// <summary>Enter after selecting through Placer or the same actor session. Repeated calls are harmless.</summary>
        public bool Enter()
        {
            if (!connected || !isActiveAndEnabled || !Player || !Player.isActiveAndEnabled || !Placer || !Placer.isActiveAndEnabled || Placer.Session == null) return false;
            if (IsBuilding) return true;
            if (EnterBuildingContext)
            {
                try { context = Player.AcquireContext(this, CreateRequest()); }
                catch (ArgumentException e) { Debug.LogError("EasyBuilding Input: " + e.Message, this); return false; }
            }
            IsBuilding = true; yaw = 0; NotifyMode(); return true;
        }
        private InputContextRequest CreateRequest()
        {
            var request = Mode == BuildingInputMode.FirstPerson ? InputContextRequest.BuildingFirstPerson() : InputContextRequest.Building();
            var maps = new List<string>();
            foreach (var action in new[] { confirm, cancel, right, left, demolish })
                if (action?.actionMap != null && !maps.Contains(action.actionMap.name)) maps.Add(action.actionMap.name);
            request.EnabledMaps = maps.ToArray();
            var blocked = new List<string>();
            if (Mode == BuildingInputMode.FirstPerson)
            {
                // Older assets may omit optional item actions. Required bindings were checked on enable.
                foreach (string name in request.BlockedActions)
                {
                    string path = GameplayMap + name.Substring(name.IndexOf('/'));
                    var action = Player.Actions.FindAction(path, false); if (action != null) blocked.Add(action.id.ToString());
                }
            }
            else request.BlockedMaps = string.IsNullOrWhiteSpace(GameplayMap) ? Array.Empty<string>() : new[] { GameplayMap };
            if (ExtraBlockedActions != null) blocked.AddRange(ExtraBlockedActions);
            request.BlockedActions = blocked.ToArray(); return request;
        }
        /// <summary>Cancel the selection and release only this component's context. The borrowed session stays alive.</summary>
        public void Exit() => Exit(true);
        private void Exit(bool cancelSelection)
        {
            bool wasBuilding = IsBuilding; IsBuilding = false;
            var old = context; context = null; old?.Dispose();
            if (Placer)
            {
                if (wasBuilding && cancelSelection) Placer.CancelSelection();
                Placer.HidePreview();
            }
            if (wasBuilding) NotifyMode();
        }
        private void NotifyMode()
        {
            if (ModeChanged == null) return;
            foreach (Action<bool> handler in ModeChanged.GetInvocationList()) try { handler(IsBuilding); } catch (Exception e) { Debug.LogException(e, this); }
        }
        private void SessionChanged(BuildingSession session)
        {
            if (observedSession != null) observedSession.SelectionChanged -= SelectionChanged;
            observedSession = session; yaw = 0;
            if (session != null) session.SelectionChanged += SelectionChanged;
            else Exit(false);
            if (Placer) Placer.HidePreview();
        }
        private void SelectionChanged(BuildingDefinition selected)
        { yaw = 0; if (!selected) Exit(false); }
        private Mouse PlayerMouse()
        {
            var devices = Player.Actions.devices;
            if (!devices.HasValue) return Mouse.current;
            foreach (var device in devices.Value) if (device is Mouse value) return value;
            return null;
        }
        private void Update()
        {
            if (!IsBuilding) return;
            if (!Player || !Player.isActiveAndEnabled || !Placer || !Placer.isActiveAndEnabled) { Exit(); return; }
            // Resolve through Placer on every operation; SetSession/Actor changes never leave a private session behind.
            var session = Placer.Session;
            if (session == null) { Exit(false); return; }
            if (Placer.InputBlocked || !confirm.enabled) { Placer.HidePreview(); return; }
            if (cancel.enabled && cancel.WasPressedThisFrame()) { Exit(); return; }
            var camera = Placer.ViewCamera ? Placer.ViewCamera : Camera.main;
            if (!camera) { Placer.HidePreview(); return; }
            if (right?.enabled == true && right.WasPressedThisFrame()) yaw = (yaw + Placer.RotationStep) % 360;
            if (left?.enabled == true && left.WasPressedThisFrame()) yaw = (yaw - Placer.RotationStep) % 360;
            var mouse = PlayerMouse();
            var point = Mode == BuildingInputMode.FirstPerson || Placer.AimFromScreenCenter || Player.DeviceFamily != InputDeviceFamily.KeyboardMouse || mouse == null
                ? new Vector2(Screen.width * .5f, Screen.height * .5f) : mouse.position.ReadValue();
            var ray = camera.ScreenPointToRay(point);
            Physics.SyncTransforms();
            if (!Placer.gameObject.scene.GetPhysicsScene().Raycast(ray.origin, ray.direction, out var hit, Placer.Reach, Placer.AimLayers, QueryTriggerInteraction.Ignore)) { Placer.HidePreview(); return; }
            if (demolish?.enabled == true && demolish.WasPressedThisFrame())
            { var existing = hit.collider.GetComponentInParent<PlacedBuilding>(); if (existing) Placer.System.TryDemolish(existing, out _, Placer.Session.Actor); }
            var selected = Placer.Session.Selected;
            if (!selected) { Placer.HidePreview(); return; }
            var socket = selected.Placement == PlacementMode.Socket ? Placer.System.FindSocket(selected, hit.point, Placer.SocketRadius) : null;
            var space = socket ? socket.Space : hit.collider.GetComponentInParent<BuildingSpace>();
            var rotation = Quaternion.Euler(0, yaw, 0);
            if (selected.AlignToSurface && !socket) rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;
            var request = new PlacementRequest(hit.point, rotation, socket, space, surfaceNormal: hit.normal);
            Placer.UpdatePreview(request);
            if (confirm.WasPressedThisFrame()) Placer.TryConfirm(request, out _, out _);
        }
        private void OnDisable()
        {
            if (!connected) return;
            connected = false; Exit();
            if (observedSession != null) observedSession.SelectionChanged -= SelectionChanged; observedSession = null;
            if (Placer) { Placer.SessionChanged -= SessionChanged; Placer.ExternalTargeting = oldExternal; }
        }
    }
}
