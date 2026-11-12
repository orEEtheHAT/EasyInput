using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace EasyInput.Tests
{
    public sealed class InputSettingsTests
    {
        private GameObject root, ownerA, ownerB;
        private PlayerInputSettings player;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings.UpdateMode updateMode;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private bool editUpdates;
        [SetUp]
        public void SetUp()
        {
            editUpdates = (bool)typeof(InputSettings).GetMethod("IsFeatureEnabled", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(InputSystem.settings, new object[] { "RUN_PLAYER_UPDATES_IN_EDIT_MODE" });
            InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
            updateMode = InputSystem.settings.updateMode; background = InputSystem.settings.backgroundBehavior;
            editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            root = new GameObject("Input test"); root.SetActive(false);
            player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false;
            root.SetActive(true); Call("OnEnable"); player.PairDevices(keyboard, mouse, gamepad);
            ownerA = new GameObject("Owner A"); ownerB = new GameObject("Owner B");
            InputSystem.Update();
        }
        [TearDown]
        public void TearDown()
        {
            try
            {
                if (root) { Call("OnDisable"); Call("OnDestroy"); Object.DestroyImmediate(root); }
                if (ownerA) Object.DestroyImmediate(ownerA); if (ownerB) Object.DestroyImmediate(ownerB);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
            finally
            {
                InputSystem.settings.updateMode = updateMode; InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
                InputSystem.settings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", editUpdates);
            }
        }
        private void Call(string method) => typeof(PlayerInputSettings).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); Call("Update"); }
        [Test] public void RuntimeCopyResolvesOriginalReferencesWithoutMutatingAssets()
        {
            var source = Resources.Load<InputActionAsset>("EasyInputDefaults");
            var reference = InputActionReference.Create(source.FindAction("Gameplay/Interact"));
            try
            {
                Assert.That(player.Resolve(reference), Is.SameAs(player.Action("Gameplay/Interact")));
                Assert.That(player.Actions, Is.Not.SameAs(source));
                Assert.That(player.TrySetBinding(reference.action, 0, "<Keyboard>/k", out var reason), Is.True, reason);
                Assert.That(reference.action.bindings[0].overridePath, Is.Null);
                Assert.That(player.GetPresentation(reference).Text, Does.Contain("K").IgnoreCase);
            }
            finally { Object.DestroyImmediate(reference); }
        }
        [Test] public void ActionLinksResolveReferencesByIdAndOptionalNamesWithoutOwningLifecycle()
        {
            var link = new InputActionLink("Gameplay/Interact");
            Assert.That(link.Resolve(player), Is.SameAs(player.Action("Gameplay/Interact")));
            link.Name = "NotInstalled/Optional"; Assert.That(link.Resolve(player), Is.Null);
            Assert.Throws<ArgumentException>(() => link.Resolve(player, true));
            var reference = InputActionReference.Create(Resources.Load<InputActionAsset>("EasyInputDefaults").FindAction("Gameplay/Interact"));
            try { link.Reference = reference; Assert.That(link.Resolve(player), Is.SameAs(player.Action("Gameplay/Interact"))); }
            finally { Object.DestroyImmediate(reference); }
        }
        [Test] public void FirstPersonBuildingPresetKeepsMoveLookAndBlocksConflictingGameplay()
        {
            using var lease = player.AcquireContext(ownerA, InputContextRequest.BuildingFirstPerson());
            Assert.That(player.Action("Gameplay/Move").enabled && player.Action("Gameplay/Look").enabled, Is.True);
            Assert.That(player.Action("Gameplay/Sprint").enabled, Is.True);
            Assert.That(player.Action("Gameplay/Fire").enabled || player.Action("Gameplay/Aim").enabled || player.Action("Gameplay/Jump").enabled || player.Action("Gameplay/RotateItem").enabled, Is.False);
            Assert.That(player.Action("Building/Confirm").enabled, Is.True);
            using var ui = player.AcquireContext(ownerB, InputContextRequest.UI());
            Assert.That(player.Action("Gameplay/Move").enabled || player.Action("Building/Confirm").enabled, Is.False);
        }
        [Test] public void AnalogInteractionUsesUserAxisDeadzone()
        {
            var value = player.Settings; value.StickDeadzoneMin = .2f; value.StickDeadzoneMax = 1; player.SetSettings(value);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(.6f, 0) }); InputSystem.Update();
            Assert.That(player.Action("Gameplay/InteractionValue").ReadValue<float>(), Is.EqualTo(.5f).Within(.01));
        }
        [Test] public void NestedContextsReleaseOnlyTheirOwnRestrictions()
        {
            var a = player.AcquireContext(ownerA, InputContextRequest.UI(10));
            var b = player.AcquireContext(ownerB, InputContextRequest.UI(20));
            Assert.That(player.Action("Gameplay/Interact").enabled, Is.False);
            Assert.That(player.Action("UI/Submit").enabled, Is.True);
            a.Dispose(); a.Dispose(); Assert.That(player.ContextRequestCount, Is.EqualTo(1));
            Assert.That(player.Action("Gameplay/Interact").enabled, Is.False);
            b.Dispose(); Assert.That(player.Action("Gameplay/Interact").enabled, Is.True);
        }
        [Test] public void PartialBlockLeavesMovementEnabled()
        {
            using var context = player.AcquireContext(ownerA, new InputContextRequest { BlockedActions = new[] { "Gameplay/Look" } });
            Assert.That(player.Action("Gameplay/Move").enabled, Is.True); Assert.That(player.Action("Gameplay/Look").enabled, Is.False);
        }
        [Test] public void HigherPriorityCursorSurvivesOutOfOrderRelease()
        {
            var a = player.AcquireContext(ownerA, new InputContextRequest { Cursor = InputCursorPolicy.Hidden, Priority = 10 });
            var b = player.AcquireContext(ownerB, new InputContextRequest { Cursor = InputCursorPolicy.Free, Priority = 20 });
            a.Dispose(); Assert.That(player.EffectiveCursor, Is.EqualTo(InputCursorPolicy.Free));
            b.Dispose(); Assert.That(player.EffectiveCursor, Is.EqualTo(player.Profile.DefaultCursor));
        }
        [Test] public void DisabledAndDestroyedOwnersCannotLeakRequests()
        {
            var a = player.AcquireContext(ownerA, InputContextRequest.UI());
            var b = player.AcquireContext(ownerB, InputContextRequest.UI());
            ownerA.SetActive(false); Object.DestroyImmediate(ownerB); Call("Update");
            Assert.That(a.IsReleased && b.IsReleased, Is.True); Assert.That(player.ContextRequestCount, Is.Zero);
        }
        [Test] public void HeldActionIsCancelledAndWaitsForReleaseAfterUI()
        {
            var action = player.Action("Gameplay/Interact"); int count = 0; action.performed += _ => ++count;
            Keys(Key.E); Assert.That(action.IsPressed(), Is.True);
            var context = player.AcquireContext(ownerA, InputContextRequest.UI()); Assert.That(action.IsPressed(), Is.False);
            context.Dispose(); Assert.That(action.enabled, Is.False);
            Keys(); Assert.That(action.enabled, Is.True); Assert.That(count, Is.EqualTo(1));
            Keys(Key.E); Assert.That(count, Is.EqualTo(2));
        }
        [Test] public void ExternalConsumerCannotOverrideContextOrDisableOtherConsumers()
        {
            var action = player.Action("Gameplay/Interact");
            action.Disable(); Assert.That(action.enabled, Is.True);
            using var context = player.AcquireContext(ownerA, InputContextRequest.UI());
            action.Enable(); Assert.That(action.enabled, Is.False);
        }
        [Test] public void CallbackFailureDoesNotLoseContextHandle()
        {
            Action failure = () => throw new InvalidOperationException("expected listener failure");
            player.ContextChanged += failure; LogAssert.Expect(LogType.Exception, new Regex("expected listener failure"));
            var handle = player.AcquireContext(ownerA, InputContextRequest.UI()); player.ContextChanged -= failure;
            handle.Dispose(); Assert.That(player.ContextRequestCount, Is.Zero);
        }
        [Test] public void ReentrantContextCallbackKeepsBothHandlesIndependent()
        {
            InputContextHandle nested = null; bool entered = false;
            Action callback = () => { if (!entered) { entered = true; nested = player.AcquireContext(ownerB, InputContextRequest.UI(200)); } };
            player.ContextChanged += callback; var first = player.AcquireContext(ownerA, InputContextRequest.UI()); player.ContextChanged -= callback;
            Assert.That(player.ContextRequestCount, Is.EqualTo(2)); first.Dispose(); Assert.That(player.Action("Gameplay/Jump").enabled, Is.False);
            nested.Dispose(); Assert.That(player.ContextRequestCount, Is.Zero);
        }
        [Test] public void ConflictRejectsWithoutMutationButIndependentMapsReuseButtons()
        {
            var interact = player.Action("Gameplay/Interact");
            Assert.That(player.TrySetBinding(interact, 0, "<Keyboard>/space", out var error), Is.False);
            Assert.That(error, Does.Contain("Jump")); Assert.That(interact.bindings[0].overridePath, Is.Null);
            Assert.That(player.TrySetBinding(interact, 0, "<Keyboard>/enter", out error), Is.True, error);
        }
        [Test] public void RebindingEnabledActionThenBlockingDoesNotLeaveInputMonitors()
        {
            var action = player.Action("Gameplay/Interact"); int presses = 0; action.performed += _ => presses++;
            Assert.That(player.TrySetBinding(action, 0, "<Keyboard>/k", out _), Is.True); Keys(Key.K);
            var ui = player.AcquireContext(ownerA, InputContextRequest.UI()); Keys(); Keys(Key.K);
            Assert.That(presses, Is.EqualTo(1)); ui.Dispose(); Keys(); Keys(Key.K); Assert.That(presses, Is.EqualTo(2));
        }
        [Test] public void CompositePartsCanRebindAndResetSeparately()
        {
            var move = player.Action("Gameplay/Move");
            Assert.Throws<ArgumentException>(() => player.BeginRebind(move, 0, ownerA));
            Assert.That(player.TrySetBinding(move, 1, "<Keyboard>/i", out _), Is.True);
            Assert.That(player.GetPresentation(move).Parts.Length, Is.EqualTo(4));
            Assert.That(move.bindings[2].overridePath, Is.Null); player.ResetBinding(move, 1); Assert.That(move.bindings[1].overridePath, Is.Null);
        }
        [Test] public void SnapshotRoundTripsAndMissingNewSettingsUseDefaults()
        {
            player.MouseSensitivity = 2; player.GamepadSensitivity = 3;
            player.TrySetBinding(player.Action("Gameplay/Interact"), 0, "<Keyboard>/k", out _);
            var snapshot = player.CaptureSettings(); player.ResetSettings();
            Assert.That(player.RestoreSettingsJson(JsonUtility.ToJson(snapshot), out var error), Is.True, error);
            Assert.That(player.MouseSensitivity, Is.EqualTo(2)); Assert.That(player.GetPresentation("Gameplay/Interact").Text, Does.Contain("K").IgnoreCase);
            snapshot.Values.RemoveAll(x => x.Key == "gamepadSensitivity");
            Assert.That(player.RestoreSettings(snapshot, out error), Is.True, error); Assert.That(player.GamepadSensitivity, Is.EqualTo(1));
        }
        [Test] public void RemovedBindingIsIgnoredAndNewActionsRemainAtDefaults()
        {
            var snapshot = player.CaptureSettings();
            snapshot.BindingOverrides = "{\"bindings\":[{\"action\":\"Removed/Action\",\"id\":\"" + Guid.NewGuid() + "\",\"path\":\"<Keyboard>/j\",\"processors\":\"null\",\"interactions\":\"null\"}]}";
            Assert.That(player.RestoreSettings(snapshot, out var error), Is.True, error);
            Assert.That(player.Action("Gameplay/Jump").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
        }
        [Test] public void CorruptAndFutureSaveAreRejectedAtomically()
        {
            player.MouseSensitivity = 2;
            Assert.That(player.RestoreSettingsJson("not JSON", out _), Is.False);
            var snapshot = player.CaptureSettings(); snapshot.Version = 99;
            Assert.That(player.RestoreSettings(snapshot, out _), Is.False); snapshot.Version = 1;
            snapshot.Values[0].Value = float.NaN; Assert.That(player.RestoreSettings(snapshot, out _), Is.False);
            Assert.That(player.MouseSensitivity, Is.EqualTo(2));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] public void LookScalingIsFrameRateIndependent(int fps)
        {
            var totalMouse = Vector2.zero; var totalStick = Vector2.zero;
            for (int i = 0; i < fps; ++i) { totalMouse += player.ScaleLook(new Vector2(120f / fps, 0), false, 1f/fps); totalStick += player.ScaleLook(Vector2.right, true, 1f/fps); }
            Assert.That(totalMouse.x, Is.EqualTo(9.6f).Within(.001)); Assert.That(totalStick.x, Is.EqualTo(160).Within(.01));
        }
        [Test] public void DeviceDriftIsIgnoredAndMeaningfulInputSwitchesPresentation()
        {
            Keys(Key.E); Assert.That(player.DeviceFamily, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(.04f, 0) }); InputSystem.Update();
            Assert.That(player.DeviceFamily, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West)); InputSystem.Update();
            Assert.That(player.DeviceFamily, Is.EqualTo(InputDeviceFamily.GenericGamepad));
            Assert.That(player.GetPresentation("Gameplay/Interact").Text, Is.Not.Empty);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(4, 0) }); InputSystem.Update();
            Assert.That(player.DeviceFamily, Is.EqualTo(InputDeviceFamily.KeyboardMouse));
        }
        [Test] public void DisconnectWhileHoldingCancelsActionAndReconnectionWorks()
        {
            var jump = player.Action("Gameplay/Jump");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update(); Assert.That(jump.IsPressed(), Is.True);
            InputSystem.RemoveDevice(gamepad); Assert.That(jump.IsPressed(), Is.False);
            InputSystem.AddDevice(gamepad); InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); InputSystem.Update(); Assert.That(jump.IsPressed(), Is.True);
        }
        [Test] public void RemovingUnusedGamepadPreservesNativeKeyboardPhaseAndValue()
        {
            var baseline = Object.Instantiate(Resources.Load<InputActionAsset>("EasyInputDefaults"));
            baseline.devices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>(new InputDevice[] { keyboard, mouse, gamepad });
            var native = baseline.FindAction("Gameplay/Interact"); native.Enable();
            try
            {
                Keys(Key.E); var interact = player.Action("Gameplay/Interact"); Assert.That(interact.IsPressed(), Is.True);
                InputSystem.RemoveDevice(gamepad);
                Assert.That(interact.activeControl, Is.SameAs(keyboard.eKey));
                Assert.That(interact.phase, Is.EqualTo(native.phase));
                Assert.That(interact.ReadValue<float>(), Is.EqualTo(1));
                // Input System 1.20 clears its IsPressed latch here even though phase/value remain.
                // Preserve native behavior instead of synthesizing a second gameplay press.
                Assert.That(interact.IsPressed(), Is.EqualTo(native.IsPressed()));
            }
            finally { baseline.Disable(); Object.DestroyImmediate(baseline); }
        }
        [Test] public void StickDeadzoneIsAppliedOnceAndSettingsDoNotMutateProfile()
        {
            var defaults = player.Profile.Defaults;
            var s = player.Settings; s.StickDeadzoneMin = .2f; s.StickDeadzoneMax = 1; player.SetSettings(s);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(.6f, 0) }); InputSystem.Update();
            Assert.That(player.Action("Gameplay/Look").ReadValue<Vector2>().x, Is.EqualTo(.5f).Within(.01));
            Assert.That(player.Profile.Defaults.Equals(defaults), Is.True);
        }
        [Test] public void ContextReleaseRespectsTheUsersStickDeadzone()
        {
            var value = player.Settings; value.StickDeadzoneMin = .4f; player.SetSettings(value);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(.3f, 0) }); InputSystem.Update();
            Assert.That(player.Action("Gameplay/Look").ReadValue<Vector2>(), Is.EqualTo(Vector2.zero));
            var ui = player.AcquireContext(ownerA, InputContextRequest.UI()); ui.Dispose();
            Assert.That(player.Action("Gameplay/Look").enabled, Is.True);
        }
        [TestCase("XInputController", InputDeviceFamily.Xbox, "A")]
        [TestCase("DualShockGamepad", InputDeviceFamily.PlayStation, "Cross")]
        [TestCase("SwitchProControllerHID", InputDeviceFamily.SwitchPro, "B")]
        public void VirtualControllerLayoutsSelectTheCorrectFamilyGlyph(string layout, InputDeviceFamily family, string label)
        {
            var controller = (Gamepad)InputSystem.AddDevice(layout);
            try
            {
                player.PairDevices(keyboard, mouse, controller); player.SelectGamepad(controller);
                Assert.That(player.DeviceFamily, Is.EqualTo(family)); Assert.That(player.GetPresentation("Gameplay/Jump").Text, Is.EqualTo(label));
            }
            finally { if (controller.added) InputSystem.RemoveDevice(controller); }
        }
        [Test] public void KeepingAnAuthoredSharedAxisIsNotANewBindingConflict()
        {
            var look = player.Action("Gameplay/Look");
            Assert.That(player.TrySetBinding(look, 0, look.bindings[0].effectivePath, out var reason), Is.True, reason);
            Assert.That(player.TrySetBinding(player.Action("Gameplay/Interact"), 0, "<Keyboard>/space", out _), Is.False);
        }
        [Test] public void RebindCancellationReleasesOnlyItsOwnContext()
        {
            using var window = player.AcquireContext(ownerA, InputContextRequest.UI());
            var rebind = player.BeginRebind(player.Action("Gameplay/Interact"), 0, ownerB);
            Assert.That(player.Action("UI/Submit").enabled, Is.False); rebind.Dispose(); rebind.Dispose();
            Assert.That(player.ContextRequestCount, Is.EqualTo(1)); Assert.That(player.Action("Gameplay/Interact").enabled, Is.False);
            Assert.That(player.Action("UI/Submit").enabled, Is.True);
        }
    }
}
