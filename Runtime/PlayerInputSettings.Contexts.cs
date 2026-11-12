using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Object = UnityEngine.Object;

namespace EasyInput
{
    public sealed class InputContextHandle : IDisposable
    {
        internal PlayerInputSettings Player;
        internal Object Owner;
        internal InputContextRequest Request;
        public bool IsReleased => Player == null;
        public void Dispose() { var p = Player; Player = null; if (p) p.ReleaseContext(this); }
    }
    public sealed partial class PlayerInputSettings
    {
        private readonly List<InputContextHandle> contexts = new List<InputContextHandle>();
        private bool[] waitingForRelease;
        private InputAction[] ownedActions;
        private bool applyingContexts;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible, cursorSaved;
        public int ContextRequestCount => contexts.Count;
        public InputCursorPolicy EffectiveCursor { get; private set; }
        private void InitializeContexts()
        {
            var list = new List<InputAction>(); foreach (var map in actions.actionMaps) foreach (var action in map.actions) list.Add(action);
            ownedActions = list.ToArray(); waitingForRelease = new bool[ownedActions.Length];
        }
        public InputContextHandle AcquireContext(Object owner, InputContextRequest request)
        {
            EnsureInitialized();
            if (!owner) throw new ArgumentNullException(nameof(owner));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!isActiveAndEnabled) throw new InvalidOperationException("Enable PlayerInputSettings before acquiring a context.");
            ValidateMaps(request.EnabledMaps); ValidateMaps(request.BlockedMaps);
            if (request.BlockedActions != null) foreach (var name in request.BlockedActions)
                if (actions.FindAction(name, false) == null) throw new ArgumentException("EasyInput: blocked action '" + name + "' does not exist.");
            var handle = new InputContextHandle { Player = this, Owner = owner, Request = request.Copy() };
            contexts.Add(handle);
            RecomputeContexts();
            return handle;
        }
        private void ValidateMaps(string[] names)
        {
            if (names == null) return;
            foreach (var name in names)
                if (name != "*" && actions.FindActionMap(name, false) == null)
                    throw new ArgumentException("EasyInput: action map '" + name + "' is missing. Match context map names to your InputActionAsset.");
        }
        internal void ReleaseContext(InputContextHandle handle)
        { if (contexts.Remove(handle)) RecomputeContexts(); }
        private void ClearContexts()
        {
            foreach (var handle in contexts) handle.Player = null;
            contexts.Clear();
        }
        private void PruneContexts()
        {
            bool changed = false;
            for (int i = contexts.Count - 1; i >= 0; --i)
            {
                var owner = contexts[i].Owner;
                bool alive = owner && (!(owner is Behaviour behaviour) || behaviour.isActiveAndEnabled) &&
                    (!(owner is Component component) || component.gameObject.activeInHierarchy) &&
                    (!(owner is GameObject go) || go.activeInHierarchy);
                if (alive) continue;
                contexts[i].Player = null; contexts.RemoveAt(i); changed = true;
            }
            if (changed) RecomputeContexts();
        }
        private static bool ContainsMap(string[] names, string name)
        {
            if (names != null) for (int i = 0; i < names.Length; ++i) if (names[i] == "*" || names[i] == name) return true;
            return false;
        }
        private bool WantsMap(InputActionMap map)
        {
            bool enabled = profile ? ContainsMap(profile.DefaultMaps, map.name) : map.name == "Gameplay";
            foreach (var context in contexts)
                if (ContainsMap(context.Request.EnabledMaps, map.name)) enabled = true;
            foreach (var context in contexts)
                if (ContainsMap(context.Request.BlockedMaps, map.name)) return false;
            return enabled && listening;
        }
        private void RecomputeContexts()
        {
            if (!initialized || applyingContexts) return;
            applyingContexts = true;
            try
            {
            for (int i = 0; i < ownedActions.Length; ++i)
            {
                var action = ownedActions[i];
                if (!WantsAction(action))
                {
                    if (action.enabled) { action.Disable(); waitingForRelease[i] = true; }
                }
                else if (!action.enabled && (!waitingForRelease[i] || ControlsReleased(action)))
                { waitingForRelease[i] = false; action.Enable(); }
            }
            int priority = int.MinValue;
            var cursor = profile ? profile.DefaultCursor : InputCursorPolicy.Locked;
            foreach (var context in contexts)
                if (context.Request.Cursor != InputCursorPolicy.Inherit && context.Request.Priority >= priority)
                { priority = context.Request.Priority; cursor = context.Request.Cursor; }
            EffectiveCursor = cursor;
            ApplyCursor(cursor);
            }
            finally { applyingContexts = false; }
            contextChanged.Raise();
        }
        private bool WantsAction(InputAction action)
        {
            if (!WantsMap(action.actionMap)) return false;
            foreach (var context in contexts) foreach (var name in context.Request.BlockedActions)
                if (actions.FindAction(name, false) == action) return false;
            return true;
        }
        private bool ControlsReleased(InputAction action)
        {
            foreach (var control in action.controls)
            {
                if (control is ButtonControl button && button.isPressed) return false;
                if (control is StickControl stick)
                {
                    float threshold = Mathf.Max(.001f, settings.StickDeadzoneMin);
                    if (stick.ReadUnprocessedValue().sqrMagnitude > threshold * threshold) return false;
                }
                if (control is DpadControl dpad && dpad.ReadValue().sqrMagnitude > .01f) return false;
            }
            return true;
        }
        private void ResumeReleasedMaps()
        {
            bool changed = false;
            for (int i = 0; i < ownedActions.Length; ++i)
            {
                var action = ownedActions[i];
                if (!waitingForRelease[i] || !WantsAction(action) || !ControlsReleased(action)) continue;
                waitingForRelease[i] = false;
                applyingContexts = true;
                try { action.Enable(); } finally { applyingContexts = false; }
                changed = true;
            }
            if (changed) contextChanged.Raise();
        }
        private void SaveCursor()
        {
            if (!ControlsCursor) return;
            savedCursorLock = Cursor.lockState; savedCursorVisible = Cursor.visible; cursorSaved = true;
        }
        private void ApplyCursor(InputCursorPolicy policy)
        {
            if (!ControlsCursor || !listening) return;
            if (policy == InputCursorPolicy.Inherit) { if (cursorSaved) { Cursor.lockState = savedCursorLock; Cursor.visible = savedCursorVisible; } return; }
            Cursor.lockState = policy == InputCursorPolicy.Locked ? CursorLockMode.Locked : policy == InputCursorPolicy.Confined ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = policy != InputCursorPolicy.Locked && policy != InputCursorPolicy.Hidden;
        }
        private void RestoreCursor()
        {
            if (!cursorSaved) return;
            Cursor.lockState = savedCursorLock; Cursor.visible = savedCursorVisible; cursorSaved = false;
        }
    }
}
