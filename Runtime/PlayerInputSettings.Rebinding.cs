using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Object = UnityEngine.Object;

namespace EasyInput
{
    public enum RebindStatus { WaitingForRelease, Listening, Completed, Cancelled, TimedOut, Conflict }
    public sealed class InputRebind : IDisposable
    {
        internal PlayerInputSettings Player;
        internal Object Owner;
        internal InputAction Action;
        internal int BindingIndex;
        internal double StartedAt, Deadline;
        internal readonly List<ButtonControl> Held = new List<ButtonControl>();
        internal readonly HashSet<Guid> ConcurrentMaps = new HashSet<Guid>();
        internal InputContextHandle Context;
        internal InputActionRebindingExtensions.RebindingOperation Operation;
        public RebindStatus Status { get; internal set; }
        public string Message { get; internal set; }
        public bool IsActive => Player != null;
        public void Dispose() { if (Player) Player.CancelRebind(); }
    }
    public sealed partial class PlayerInputSettings
    {
        private bool applyingBindings;
        public InputRebind Rebind { get; private set; }
        private readonly InputSignal rebindChanged = new InputSignal();
        public event Action RebindChanged { add => rebindChanged.Add(value); remove => rebindChanged.Remove(value); }
        public InputRebind BeginRebind(InputActionReference reference, int bindingIndex, Object owner, float timeout = 10) =>
            BeginRebind(Resolve(reference), bindingIndex, owner, timeout);
        public InputRebind BeginRebind(InputAction action, int bindingIndex, Object owner, float timeout = 10)
        {
            action = RequireAction(action);
            ValidateBinding(action, bindingIndex);
            if (!owner) throw new ArgumentNullException(nameof(owner));
            if (timeout <= 0 || float.IsNaN(timeout) || float.IsInfinity(timeout)) throw new ArgumentOutOfRangeException(nameof(timeout));
            CancelRebind();
            var session = new InputRebind { Player = this, Owner = owner, Action = action, BindingIndex = bindingIndex,
                StartedAt = Time.realtimeSinceStartupAsDouble, Deadline = Time.realtimeSinceStartupAsDouble + timeout,
                Status = RebindStatus.WaitingForRelease, Message = "Release the opening button, then press a new control. Esc / East cancels." };
            session.ConcurrentMaps.Add(action.actionMap.id);
            if (action.enabled) foreach (var map in Actions.actionMaps) if (map.enabled) session.ConcurrentMaps.Add(map.id);
            foreach (var device in allowedDevices)
            foreach (var control in device.allControls)
                if (control is ButtonControl button && button.isPressed) session.Held.Add(button);
            Rebind = session;
            session.Context = AcquireContext(owner, new InputContextRequest { BlockedMaps = new[] { "*" }, Priority = int.MaxValue, Cursor = InputCursorPolicy.Free });
            rebindChanged.Raise();
            return session;
        }
        private InputAction RequireAction(InputAction action)
        {
            var result = Resolve(action);
            if (result == null) throw new ArgumentException("The action must belong to this player's source or runtime asset.", nameof(action));
            return result;
        }
        private static void ValidateBinding(InputAction action, int index)
        {
            if (index < 0 || index >= action.bindings.Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (action.bindings[index].isComposite) throw new ArgumentException("Choose a composite part (Up/Down/Left/Right), not the composite header.");
        }
        private void TickRebind()
        {
            var session = Rebind;
            if (session == null || !session.IsActive) return;
            if (!session.Owner || session.Context.IsReleased) { FinishRebind(RebindStatus.Cancelled, "Rebinding cancelled."); return; }
            if (Time.realtimeSinceStartupAsDouble >= session.Deadline) { FinishRebind(RebindStatus.TimedOut, "No input received before the timeout."); return; }
            if (session.Status != RebindStatus.WaitingForRelease || Time.realtimeSinceStartupAsDouble - session.StartedAt < .15) return;
            foreach (var held in session.Held) if (held.device.added && held.isPressed) return;
            session.Status = RebindStatus.Listening;
            session.Message = "Press a new control. Esc / East cancels.";
            try
            {
                session.Operation = session.Action.PerformInteractiveRebinding(session.BindingIndex)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithControlsExcluding("<Gamepad>/buttonEast")
                    .WithMagnitudeHavingToBeGreaterThan(.5f)
                    .OnApplyBinding((_, path) =>
                    {
                        if (!TrySetBindingInternal(session.Action, session.BindingIndex, path, session.ConcurrentMaps, out var reason))
                        { session.Status = RebindStatus.Conflict; session.Message = reason; }
                    })
                    .OnCancel(_ => FinishRebind(RebindStatus.Cancelled, "Rebinding cancelled."))
                    .OnComplete(_ => FinishRebind(session.Status == RebindStatus.Conflict ? RebindStatus.Conflict : RebindStatus.Completed,
                        session.Status == RebindStatus.Conflict ? session.Message : "Binding updated."));
                // Standard control schemes restrict each binding slot to the devices it was authored for.
                // A paired player also excludes controls from other physical devices.
                foreach (var device in InputSystem.devices) if (!IsPaired(device)) session.Operation.WithControlsExcluding(device.path);
                session.Operation.Start();
                rebindChanged.Raise();
            }
            catch { FinishRebind(RebindStatus.Cancelled, "Cannot start rebinding. Check the binding and control scheme."); throw; }
        }
        public void CancelRebind() => FinishRebind(RebindStatus.Cancelled, "Rebinding cancelled.");
        private void FinishRebind(RebindStatus status, string message)
        {
            var session = Rebind;
            if (session == null || !session.IsActive) return;
            session.Player = null; session.Status = status; session.Message = message;
            session.Operation?.Dispose(); session.Operation = null;
            session.Context?.Dispose(); session.Context = null;
            rebindChanged.Raise();
        }
        public bool TrySetBinding(InputAction action, int bindingIndex, string path, out string reason)
        {
            action = RequireAction(action); ValidateBinding(action, bindingIndex);
            var concurrent = new HashSet<Guid> { action.actionMap.id };
            if (action.enabled) foreach (var map in Actions.actionMaps) if (map.enabled) concurrent.Add(map.id);
            return TrySetBindingInternal(action, bindingIndex, path, concurrent, out reason);
        }
        private bool TrySetBindingInternal(InputAction action, int index, string path, HashSet<Guid> concurrent, out string reason)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 512 || !path.StartsWith("<", StringComparison.Ordinal) || !path.Contains(">/"))
            { reason = "Choose a valid Input System control path, for example <Keyboard>/e."; return false; }
            var target = action.bindings[index];
            // Authored shared axes (Look / ItemRotation) are intentional. Keeping an existing
            // effective binding is a no-op, not a newly introduced conflict.
            if (string.Equals(target.effectivePath, path, StringComparison.OrdinalIgnoreCase))
            { reason = null; return true; }
            foreach (var map in Actions.actionMaps)
            {
                if (!concurrent.Contains(map.id)) continue;
                foreach (var candidate in map.actions)
                for (int i = 0; i < candidate.bindings.Count; ++i)
                {
                    var binding = candidate.bindings[i];
                    if (candidate == action && i == index || binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)) continue;
                    if (!GroupsOverlap(target.groups, binding.groups)) continue;
                    if (string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    { reason = "Already used by " + map.name + "/" + candidate.name + (binding.isPartOfComposite ? " (" + binding.name + ")" : "") + ". Choose another control or reset that binding."; return false; }
                }
            }
            applyingBindings = true;
            try { action.ApplyBindingOverride(index, path); }
            finally { applyingBindings = false; }
            reason = null; RecomputeContexts(); NotifyBindings(); return true;
        }
        private static bool GroupsOverlap(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return true;
            foreach (var group in a.Split(';')) foreach (var other in b.Split(';')) if (group == other) return true;
            return false;
        }
        public void ResetBinding(InputAction action, int index)
        {
            CancelRebind(); action = RequireAction(action); ValidateBinding(action, index);
            applyingBindings = true;
            try { action.RemoveBindingOverride(index); }
            finally { applyingBindings = false; }
            RecomputeContexts(); NotifyBindings();
        }
        public void ResetBindings()
        {
            CancelRebind(); applyingBindings = true;
            try { Actions.RemoveAllBindingOverrides(); }
            finally { applyingBindings = false; }
            ApplyDeadzoneParameters(); RecomputeContexts(); NotifyBindings();
        }
        public void ResetSettings()
        { SetSettings(Profile && Profile.Defaults.IsValid ? Profile.Defaults : UserInputSettings.Default); ResetBindings(); }
    }
}
