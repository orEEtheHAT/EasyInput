using System;
using System.Collections.Generic;
using EasyInteraction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EasyInput.Integrations
{
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(InteractionActor))]
    [AddComponentMenu("EasyInput/Integrations/EasyInteraction Input")]
    public sealed class EasyInteractionInput : MonoBehaviour, IInteractionInputSource
    {
        public PlayerInputSettings Player;
        public InteractionActor Actor;
        [SerializeField, FormerlySerializedAs("ToggleInsteadOfHold")] private bool toggleInsteadOfHold;
        public bool ToggleInsteadOfHold
        {
            get => toggleInsteadOfHold;
            set { if (toggleInsteadOfHold == value) return; toggleInsteadOfHold = value; NotifyPresentation(); }
        }
        public bool ItemControls = true;
        public bool ConsumeLookWhileRotating = true;
        public bool ConsumeLookWhileAnalog = true;
        [Min(0)] public float MouseRotationMultiplier = 3.125f;
        [Min(0)] public float GamepadRotationMultiplier = .5625f;
        [Min(0), Tooltip("Meters per mouse scroll unit; GrabAction owns distance limits.")] public float MouseDistancePerUnit = .002f;
        [Min(0), Tooltip("Meters per second for a full gamepad distance axis.")] public float GamepadDistancePerSecond = 1.5f;
        [Min(0)] public float MouseAnalogScale = .1f;
        [Min(0)] public float GamepadAnalogScale = 1;
        public InputActionLink Interact = new InputActionLink("Gameplay/Interact");
        public InputActionLink CancelAction = new InputActionLink("Gameplay/CancelInteraction");
        public InputActionLink NextAction = new InputActionLink("Gameplay/NextInteraction");
        public InputActionLink Secondary = new InputActionLink("Gameplay/SecondaryInteraction");
        public InputActionLink NextTarget = new InputActionLink("Gameplay/NextTarget");
        public InputActionLink ThrowItem = new InputActionLink("Gameplay/ThrowItem");
        public InputActionLink RotateItem = new InputActionLink("Gameplay/RotateItem");
        public InputActionLink ItemRotation = new InputActionLink("Gameplay/ItemRotation");
        public InputActionLink ItemDistance = new InputActionLink("Gameplay/ItemDistance");
        public InputActionLink AnalogValue = new InputActionLink("Gameplay/InteractionValue");
        public InputActionLink Look = new InputActionLink("Gameplay/Look");
        public string BindingLabel { get; private set; } = "";
        public Sprite BindingIcon { get; private set; }
        public event Action PresentationChanged;
        public bool IsConsumingLook => lookLease != null && !lookLease.IsReleased;

        private InputAction interact, cancel, next, secondary, nextTarget, throwItem, rotate, rotation, distance, analog, look;
        private InteractionAction pressed, secondaryPressed;
        private InteractionTicket pressedTicket, secondaryTicket;
        private readonly List<(InteractionTicket ticket, InputAction source)> owned = new List<(InteractionTicket, InputAction)>(4);
        private InteractionInput standalone;
        private bool standaloneEnabled, connected, updatingLook, cancellingOwned;
        private PlayerInputSettings subscribed;
        private InputContextHandle lookLease;
        private bool CanRead => connected && subscribed && subscribed.isActiveAndEnabled && Actor && Actor.isActiveAndEnabled;

        private void OnEnable()
        {
            if (!Actor) Actor = GetComponent<InteractionActor>();
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player || !Actor) { Debug.LogError("EasyInteraction Input: assign Player Input Settings and Interaction Actor.", this); enabled = false; return; }
            try
            {
                interact = Interact.Resolve(Player, true); cancel = CancelAction.Resolve(Player); next = NextAction.Resolve(Player);
                secondary = Secondary.Resolve(Player); nextTarget = NextTarget.Resolve(Player); throwItem = ThrowItem.Resolve(Player);
                rotate = RotateItem.Resolve(Player); rotation = ItemRotation.Resolve(Player); distance = ItemDistance.Resolve(Player);
                analog = AnalogValue.Resolve(Player); look = Look.Resolve(Player);
                if (look != null && (rotation == look || analog == look)) throw new ArgumentException("Use separate ItemRotation / InteractionValue actions sharing Look's bindings, so consuming Look cannot disable item input.");
            }
            catch (ArgumentException e) { Debug.LogError(e.Message, this); enabled = false; return; }
            standalone = Actor.GetComponent<InteractionInput>(); if (standalone) { standaloneEnabled = standalone.enabled; standalone.enabled = false; }
            subscribed = Player; connected = true;
            interact.performed += Press; interact.canceled += Release;
            if (cancel != null) cancel.performed += Cancel;
            if (next != null) next.performed += Next;
            if (nextTarget != null) nextTarget.performed += CycleTarget;
            if (secondary != null) { secondary.performed += SecondaryPress; secondary.canceled += SecondaryRelease; }
            if (throwItem != null) throwItem.performed += Throw;
            if (rotate != null) { rotate.performed += RotationState; rotate.canceled += RotationState; }
            subscribed.PresentationChanged += RefreshPresentation; subscribed.ContextChanged += ContextChanged;
            Actor.ActionStateChanged += ActorStateChanged;
            RefreshPresentation(); UpdateLookLease();
        }
        private void RefreshPresentation()
        {
            var value = subscribed ? subscribed.GetPresentation(interact) : default;
            BindingLabel = value.Text ?? ""; BindingIcon = value.Sprite; NotifyPresentation();
        }
        private void NotifyPresentation()
        {
            var handlers = PresentationChanged; if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList()) try { handler(); } catch (Exception e) { Debug.LogException(e, this); }
        }
        private void Press(InputAction.CallbackContext _)
        {
            if (!CanRead) return;
            Actor.RefreshTarget(); BeginPress(Actor.SelectedAction, interact, ref pressed, ref pressedTicket);
        }
        private void BeginPress(InteractionAction action, InputAction source, ref InteractionAction tracked, ref InteractionTicket ticket)
        {
            EndPress(ref tracked, ref ticket, false);
            if (!action || !Actor.Press(action, toggleInsteadOfHold)) return;
            tracked = action; ticket = Actor.GetTicket(action);
            for (int i = owned.Count - 1; i >= 0; --i) if (!owned[i].ticket.IsActive) owned.RemoveAt(i);
            if (ticket.IsActive) owned.Add((ticket, source));
            UpdateLookLease();
        }
        private void EndPress(ref InteractionAction action, ref InteractionTicket ticket, bool cancelOperation)
        {
            // A toggled hold still belongs to this reader after physical release. Keep its ticket
            // so context cancellation / disable can close precisely that operation later.
            if (!cancelOperation && toggleInsteadOfHold && Actor && action &&
                (ticket.IsActive || Actor.CommandAdapter && Actor.CommandAdapter.IsRunning(action)))
            { Actor.Release(action, true); return; }
            var current = action; var captured = ticket; action = null; ticket = default;
            if (!Actor || !current) return;
            if (Actor.CommandAdapter) Actor.Release(current, cancelOperation ? false : toggleInsteadOfHold);
            else if (captured.IsActive)
            {
                if (cancelOperation) Actor.Cancel(captured);
                else Actor.Release(current, toggleInsteadOfHold);
            }
        }
        private void Release(InputAction.CallbackContext _)
        { EndPress(ref pressed, ref pressedTicket, !interact.enabled); UpdateLookLease(); }
        private void SecondaryRelease(InputAction.CallbackContext _)
        { EndPress(ref secondaryPressed, ref secondaryTicket, secondary?.enabled != true); UpdateLookLease(); }
        private void Cancel(InputAction.CallbackContext _)
        {
            if (!CanRead) return;
            if (Actor.CommandAdapter) Actor.CommandAdapter.Cancel(); else Actor.Cancel();
            owned.Clear(); pressed = secondaryPressed = null; pressedTicket = secondaryTicket = default; UpdateLookLease();
        }
        private void Next(InputAction.CallbackContext _) { if (CanRead) Actor.CycleAction(); }
        private void CycleTarget(InputAction.CallbackContext _) { if (CanRead) Actor.CycleTarget(); }
        private void SecondaryPress(InputAction.CallbackContext _)
        {
            if (!CanRead) return;
            Actor.RefreshTarget(); var selected = Actor.SelectedAction; Actor.CycleAction(); var candidate = Actor.SelectedAction;
            if (candidate && candidate != selected) BeginPress(candidate, secondary, ref secondaryPressed, ref secondaryTicket);
            else
            {
                var held = Actor.GetActive<GrabAction>(); var use = held ? held.Target.FindAction("use") : null;
                if (use) Actor.TryExecuteDirect(use);
            }
        }
        private void Throw(InputAction.CallbackContext _) { if (CanRead && ItemControls) { Actor.GetActive<GrabAction>()?.Throw(Actor); UpdateLookLease(); } }
        private void RotationState(InputAction.CallbackContext _) => UpdateLookLease();
        private void ActorStateChanged(InteractionActionEvent _) => UpdateLookLease();
        private InteractionAction AnalogInteraction()
        {
            if (!CanRead || analog?.enabled != true) return null;
            var active = Actor.GetActive(InteractionInputGesture.Analog);
            if (active) return active;
            var selected = Actor.SelectedAction;
            if (Actor.CommandAdapter && selected && selected.InputGesture == InteractionInputGesture.Analog && Actor.CommandAdapter.IsRunning(selected)) return selected;
            return null;
        }
        private void ContextChanged()
        {
            if (interact?.enabled != true) EndPress(ref pressed, ref pressedTicket, true);
            if (secondary?.enabled != true) EndPress(ref secondaryPressed, ref secondaryTicket, true);
            CancelOwned(false);
            UpdateLookLease();
        }
        private void CancelOwned(bool all)
        {
            if (cancellingOwned) return; cancellingOwned = true;
            try
            {
            for (int i = owned.Count - 1; i >= 0; --i)
            {
                var value = owned[i];
                if (!all && value.ticket.IsActive && value.source.enabled) continue;
                owned.RemoveAt(i);
                if (Actor && value.ticket.IsActive) Actor.Cancel(value.ticket);
            }
            }
            finally { cancellingOwned = false; }
        }
        private void UpdateLookLease()
        {
            if (updatingLook) return; updatingLook = true;
            try
            {
                bool rotateHeld = CanRead && ItemControls && rotate?.enabled == true && rotate.IsPressed() && rotation?.enabled == true && Actor.GetActive<GrabAction>();
                bool wanted = CanRead && look != null && (ConsumeLookWhileRotating && rotateHeld || ConsumeLookWhileAnalog && AnalogInteraction());
                if (wanted && (lookLease == null || lookLease.IsReleased))
                    lookLease = subscribed.AcquireContext(this, new InputContextRequest { BlockedActions = new[] { look.id.ToString() } });
                else if (!wanted) { var old = lookLease; lookLease = null; old?.Dispose(); }
            }
            finally { updatingLook = false; }
        }
        private void Update()
        {
            UpdateLookLease(); if (!CanRead) return;
            var operation = AnalogInteraction();
            if (operation)
            {
                float value = analog.ReadValue<float>() * (analog.activeControl?.device is Gamepad ? GamepadAnalogScale : MouseAnalogScale);
                if (Actor.CommandAdapter) Actor.CommandAdapter.SetValue(operation, value); else Actor.SetValue(operation, value);
            }
            var held = ItemControls ? Actor.GetActive<GrabAction>() : null; if (!held) return;
            if (rotate?.enabled == true && rotate.IsPressed() && rotation?.enabled == true)
                held.Rotate(subscribed.ReadLookDelta(rotation, Time.deltaTime) * (rotation.activeControl?.device is Gamepad ? GamepadRotationMultiplier : MouseRotationMultiplier));
            if (distance?.enabled == true)
            {
                float value = distance.ReadValue<float>();
                if (value != 0) held.AdjustDistance(value * (distance.activeControl?.device is Gamepad ? GamepadDistancePerSecond * Time.deltaTime : MouseDistancePerUnit));
            }
        }
        private void OnDisable()
        {
            if (!connected) return; connected = false;
            subscribed.PresentationChanged -= RefreshPresentation; subscribed.ContextChanged -= ContextChanged;
            if (Actor) Actor.ActionStateChanged -= ActorStateChanged;
            EndPress(ref pressed, ref pressedTicket, true); EndPress(ref secondaryPressed, ref secondaryTicket, true);
            CancelOwned(true);
            var old = lookLease; lookLease = null; old?.Dispose();
            interact.performed -= Press; interact.canceled -= Release;
            if (cancel != null) cancel.performed -= Cancel; if (next != null) next.performed -= Next;
            if (nextTarget != null) nextTarget.performed -= CycleTarget;
            if (secondary != null) { secondary.performed -= SecondaryPress; secondary.canceled -= SecondaryRelease; }
            if (throwItem != null) throwItem.performed -= Throw;
            if (rotate != null) { rotate.performed -= RotationState; rotate.canceled -= RotationState; }
            if (standalone) standalone.enabled = standaloneEnabled;
            BindingLabel = ""; BindingIcon = null; NotifyPresentation(); subscribed = null;
        }
    }
}
