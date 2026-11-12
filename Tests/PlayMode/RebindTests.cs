using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace EasyInput.Tests
{
    public sealed class RebindTests
    {
        private GameObject root;
        private PlayerInputSettings player;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings.UpdateMode updateMode;
        private InputSettings.BackgroundBehavior background;
        #if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        #endif
        private bool oldRunInBackground;
        [UnitySetUp] public IEnumerator Setup()
        {
            updateMode = InputSystem.settings.updateMode; background = InputSystem.settings.backgroundBehavior;
            oldRunInBackground = Application.runInBackground; Application.runInBackground = true;
            #if UNITY_EDITOR
            editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            #endif
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); gamepad = InputSystem.AddDevice<Gamepad>();
            root = new GameObject("Rebind Test"); root.SetActive(false); player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false; root.SetActive(true);
            player.PairDevices(keyboard, mouse, gamepad); InputSystem.Update(); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            try
            {
                if (root) Object.Destroy(root); yield return null;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            }
            finally
            {
                InputSystem.settings.updateMode = updateMode; InputSystem.settings.backgroundBehavior = background; Application.runInBackground = oldRunInBackground;
                #if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior;
                #endif
            }
        }
        private IEnumerator Listen()
        {
            yield return new WaitForSecondsRealtime(.18f); InputSystem.Update(); yield return null;
            Assert.That(player.Rebind.Status, Is.EqualTo(RebindStatus.Listening));
        }
        private IEnumerator Finish()
        { yield return new WaitForSecondsRealtime(.1f); InputSystem.Update(); yield return null; }
        [UnityTest] public IEnumerator RebindKeyboardDoesNotPerformGameplayAndPromptUpdates()
        {
            int performed = 0; var interact = player.Action("Gameplay/Interact"); interact.performed += _ => ++performed;
            var prompt = root.AddComponent<InputPrompt>();
            var session = player.BeginRebind(interact, 0, player); yield return Listen();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); InputSystem.Update(); yield return Finish();
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Completed)); Assert.That(performed, Is.Zero);
            Assert.That(interact.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/k")); Assert.That(prompt.Text, Does.Contain("K").IgnoreCase);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); InputSystem.Update(); Assert.That(performed, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator EscapeCancelsAndTimeoutReleasesRequests()
        {
            var action = player.Action("Gameplay/Interact");
            var session = player.BeginRebind(action, 0, player); yield return Listen();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); InputSystem.Update(); yield return null;
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Cancelled)); Assert.That(action.bindings[0].overridePath, Is.Null);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            session = player.BeginRebind(action, 0, player, .25f); yield return new WaitForSecondsRealtime(.35f);
            Assert.That(session.Status, Is.EqualTo(RebindStatus.TimedOut)); Assert.That(player.ContextRequestCount, Is.Zero);
        }
        [UnityTest] public IEnumerator ConflictLeavesOriginalBindingUntouched()
        {
            var action = player.Action("Gameplay/Interact"); var session = player.BeginRebind(action, 0, player); yield return Listen();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update(); yield return Finish();
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Conflict)); Assert.That(action.bindings[0].overridePath, Is.Null);
        }
        [UnityTest] public IEnumerator MouseAndGamepadSlotsRebindIndependently()
        {
            var action = player.Action("Gameplay/Interact"); var session = player.BeginRebind(action, 0, player); yield return Listen();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle)); InputSystem.Update(); yield return Finish();
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Completed)); Assert.That(action.bindings[0].effectivePath, Is.EqualTo("<Mouse>/middleButton"));
            InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update();
            // The new item-rotation default now owns Left Shoulder. Free that runtime slot
            // explicitly before testing an independent gamepad rebind; conflicts stay rejected.
            Assert.That(player.TrySetBinding(action, 1, "<Gamepad>/leftShoulder", out _), Is.False);
            using (player.AcquireContext(root, new InputContextRequest { BlockedMaps = new[] { "*" } }))
                player.Action("Gameplay/RotateItem").ApplyBindingOverride(1, string.Empty);
            session = player.BeginRebind(action, 1, player); yield return Listen();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder)); InputSystem.Update(); yield return Finish();
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Completed)); Assert.That(action.bindings[1].effectivePath, Does.EndWith("/leftShoulder"));
        }
        [UnityTest] public IEnumerator OpeningClickMustBeReleasedBeforeCapture()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); InputSystem.Update();
            var session = player.BeginRebind(player.Action("Gameplay/Interact"), 0, player);
            yield return new WaitForSecondsRealtime(.2f); Assert.That(session.Status, Is.EqualTo(RebindStatus.WaitingForRelease));
            InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update(); yield return null;
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Listening)); session.Dispose();
        }
        [UnityTest] public IEnumerator GamepadCancelConsumesPressWithoutClosingUIOrRebinding()
        {
            using var window = player.AcquireContext(player, InputContextRequest.UI());
            int closes = 0; player.Action("UI/Cancel").performed += _ => ++closes;
            var action = player.Action("Gameplay/Jump"); var session = player.BeginRebind(action, 1, player); yield return Listen();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update(); yield return null;
            Assert.That(session.Status, Is.EqualTo(RebindStatus.Cancelled)); Assert.That(action.bindings[1].overridePath, Is.Null); Assert.That(closes, Is.Zero);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); InputSystem.Update(); Assert.That(closes, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SceneUnloadReleasesOnlyOwnersFromThatScene()
        {
            var scene = SceneManager.CreateScene("Temporary input window"); var owner = new GameObject("Window"); SceneManager.MoveGameObjectToScene(owner, scene);
            var first = player.AcquireContext(owner, InputContextRequest.UI()); var second = player.AcquireContext(player, InputContextRequest.UI(200));
            yield return SceneManager.UnloadSceneAsync(scene); yield return null;
            Assert.That(first.IsReleased, Is.True); Assert.That(second.IsReleased, Is.False); Assert.That(player.ContextRequestCount, Is.EqualTo(1));
            second.Dispose(); Assert.That(player.Action("Gameplay/Move").enabled, Is.True);
        }
    }
}
