#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using EasyInput.Integrations;
using EasyInteraction;
using EasyInteraction.Integrations.Movement;
using EasyInteraction.Integrations.Building;
using EasyMovement.FirstPerson;
using EasyBuilding;
using EasyFeedbacks;
using EasyUI;
using EasyUI.Integrations.Input;
using Object = UnityEngine.Object;

namespace EasyInput.IntegrationTests
{
    public sealed class WaveValueAction : InteractionAction
    {
        public float LastValue;
        protected override bool OnExecute(InteractionActor actor) => true;
        protected override void OnValueChanged(InteractionActor actor, float value) => LastValue = value;
    }
    public sealed class Wave1Tests
    {
        private readonly List<Object> assets = new List<Object>();
        private readonly List<InputDevice> devices = new List<InputDevice>();
        private GameObject root;
        private PlayerInputSettings player;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad pad;
        private Scene scene;
        private Scene targetScene;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorBehavior;
        private InputSettings.UpdateMode updateMode;
        private T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        private GameObject New(string name, bool active = true)
        { var value = new GameObject(name); value.SetActive(active); value.transform.SetParent(root.transform); return value; }
        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private void Pad(GamepadState state) { InputSystem.QueueStateEvent(pad, state); InputSystem.Update(); }
        [UnitySetUp] public IEnumerator Setup()
        {
            background = InputSystem.settings.backgroundBehavior; editorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode; updateMode = InputSystem.settings.updateMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            devices.Add(keyboard); devices.Add(mouse); devices.Add(pad);
            scene = SceneManager.CreateScene("EasyInput Wave1 Physics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            root = new GameObject("Wave1 player"); SceneManager.MoveGameObjectToScene(root, scene); root.SetActive(false);
            player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false; root.SetActive(true); player.PairDevices(keyboard, mouse, pad);
            InputSystem.Update(); yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            try
            {
                if (root) Object.DestroyImmediate(root);
                foreach (var asset in assets) if (asset) Object.DestroyImmediate(asset); assets.Clear();
                foreach (var device in devices) if (device.added) InputSystem.RemoveDevice(device); devices.Clear();
                if (targetScene.IsValid() && targetScene.isLoaded) yield return SceneManager.UnloadSceneAsync(targetScene);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            finally
            { InputSystem.settings.backgroundBehavior = background; InputSystem.settings.editorInputBehaviorInPlayMode = editorBehavior; InputSystem.settings.updateMode = updateMode; }
        }
        private EasyMovementInput Movement()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.oreethehat.easy-movement/FirstPerson/Prefabs/FP_Player_Sample.prefab");
            Assert.That(prefab, Is.Not.Null);
            var go = Object.Instantiate(prefab, root.transform); go.SetActive(false);
            var controller = go.GetComponentInChildren<FirstPersonCharacterController>(true);
            var adapter = controller.gameObject.AddComponent<EasyMovementInput>(); adapter.Player = player;
            go.SetActive(true); return adapter;
        }
        private EasyInteractionInput Interaction(out InteractionActor actor)
        {
            var go = New("Actor", false); go.transform.position = Vector3.up * 20;
            actor = go.AddComponent<InteractionActor>(); actor.PlayerRoot = go.transform; actor.SelectionMode = InteractionSelectionMode.Explicit; actor.RequireVisibility = false; actor.AutomaticUpdate = false; actor.Range = 8;
            go.AddComponent<InteractionInput>(); var adapter = go.AddComponent<EasyInteractionInput>(); adapter.Player = player;
            go.SetActive(true); return adapter;
        }
        private T Target<T>(InteractionActor actor, string name = "Target", float offset = 2) where T : InteractionAction
        {
            var go = New(name); go.transform.position = actor.transform.position + Vector3.forward * offset;
            var action = go.AddComponent<T>(); Physics.SyncTransforms(); actor.SelectTarget(action.Target); return action;
        }
        private WindowManager Windows()
        {
            var go = New("Windows"); go.AddComponent<RectTransform>(); var manager = go.AddComponent<WindowManager>();
            var provider = go.AddComponent<EasyInputWindowProvider>(); provider.Player = player; manager.InputProvider = provider;
            manager.EventSystem = New("EventSystem").AddComponent<EventSystem>(); manager.Initialize(); return manager;
        }
        private UIWindow Window(WindowManager manager, string name)
        {
            var go = New(name, false); go.AddComponent<RectTransform>(); go.transform.SetParent(manager.transform);
            var window = go.AddComponent<UIWindow>(); window.Policy = new WindowPolicy { BlockGameplay = true, Cursor = WindowCursor.Visible, FadeIn = 0, FadeOut = 0 }; return window;
        }
        [UnityTest] public IEnumerator ApproachAndIndependentBlocksPreserveScriptedMotorAcrossCallbacksAndDisable()
        {
            var adapter = Movement(); yield return null; var controller = adapter.Character;
            var actor = controller.gameObject.AddComponent<InteractionActor>(); actor.AutomaticUpdate = false;
            var motor = controller.gameObject.AddComponent<EasyMovementInteractionMotor>();
            var destination = New("Approach destination").transform; destination.position = controller.transform.position + new Vector3(4,0,3);
            Keys(Key.Space, Key.C, Key.LeftShift); Assert.That(controller.jumpInputPressed, Is.True);
            motor.Begin(destination); Assert.That(controller.jumpInputPressed, Is.False); Assert.That(controller.crouchInputPressed, Is.False);
            using var second = controller.AcquireInputBlock(root); Assert.That(controller.TrySetNoclip(true), Is.True); motor.Tick(.016f);
            var scripted = controller.GetMovementDirection(); Assert.That(scripted.sqrMagnitude, Is.GreaterThan(.5));
            controller.Jump(); controller.Crouch(); controller.Sprint(); float boosted = controller.GetMaxSpeed();
            Keys(); Keys(Key.W, Key.Space, Key.C, Key.LeftShift, Key.F8);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(200,100) }); InputSystem.Update();
            adapter.SendMessage("Update");
            Assert.That(controller.GetMovementDirection(), Is.EqualTo(scripted)); Assert.That(controller.jumpInputPressed, Is.True); Assert.That(controller.crouchInputPressed, Is.True);
            Assert.That(controller.IsNoclipActive, Is.True); Assert.That(controller.GetMaxSpeed(), Is.EqualTo(boosted));
            adapter.enabled = false; Assert.That(controller.GetMovementDirection(), Is.EqualTo(scripted)); Assert.That(controller.jumpInputPressed, Is.True);
            motor.Stop(); Assert.That(controller.InputBlocked, Is.True); adapter.enabled = true;
            controller.StopJumping(); controller.UnCrouch(); controller.StopSprinting(); controller.TrySetNoclip(false);
            second.Dispose(); adapter.SendMessage("Update");
            Assert.That(controller.jumpInputPressed, Is.False, "Held jump must not resume after block"); Assert.That(controller.crouchInputPressed, Is.False);
            Keys(); adapter.SendMessage("Update"); Keys(Key.Space); Assert.That(controller.jumpInputPressed, Is.True);
        }
        [UnityTest] public IEnumerator PresenterFollowsSharedBindingDeviceGlyphProfileAndToggle()
        {
            var adapter = Interaction(out var actor); Target<EventAction>(actor);
            var presenter = actor.gameObject.AddComponent<InteractionPresenter>(); presenter.Refresh();
            Assert.That(presenter.ActiveInputSource, Is.SameAs(adapter));
            Assert.That(player.TrySetBinding(player.Action("Gameplay/Interact"), 0, "<Keyboard>/k", out var reason), Is.True, reason);
            yield return null; Assert.That(presenter.Prompt.Binding, Does.Contain("K").IgnoreCase);
            player.SelectGamepad(pad); presenter.Refresh(); Assert.That(presenter.Prompt.Binding, Is.EqualTo(player.GetPresentation("Gameplay/Interact").Text));
            var profile = Asset<InputProfile>(); var glyphs = Asset<InputGlyphSet>(); glyphs.Family = player.DeviceFamily;
            var texture = new Texture2D(2,2); assets.Add(texture); var sprite = Sprite.Create(texture, new Rect(0,0,2,2), Vector2.one * .5f); assets.Add(sprite);
            glyphs.Glyphs = new[] { new InputGlyph { ControlPath = "<Gamepad>/buttonWest", Text = "Custom West", Sprite = sprite } }; profile.GlyphSets = new[] { glyphs };
            player.SetProfile(profile); adapter.ToggleInsteadOfHold = true; yield return null;
            Assert.That(presenter.Prompt.Binding, Is.EqualTo("Custom West")); Assert.That(presenter.Prompt.BindingIcon, Is.SameAs(sprite)); Assert.That(presenter.ToggleInsteadOfHold, Is.True);
            presenter.InputSource = adapter; adapter.enabled = false; presenter.Refresh(); Assert.That(presenter.ActiveInputSource, Is.Null);
            presenter.InputSource = null; presenter.Refresh(); Assert.That(presenter.ActiveInputSource, Is.SameAs(actor.GetComponent<InteractionInput>()));
        }
        [UnityTest] public IEnumerator GrabRotateDistanceAndThrowUseSharedInputAndConsumeOnlyLook()
        {
            var adapter = Interaction(out var actor); var grab = Target<GrabAction>(actor); grab.Body.useGravity = false; grab.RotationMode = GrabRotation.KeepInitial;
            Keys(Key.E); Assert.That(grab.IsHeld, Is.True); Keys(); Keys(Key.R);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40,0), scroll = new Vector2(0,120) }); InputSystem.Update();
            adapter.SendMessage("Update"); Assert.That(adapter.IsConsumingLook, Is.True); Assert.That(player.Action("Gameplay/Look").enabled, Is.False); Assert.That(player.Action("Gameplay/Move").enabled, Is.True);
            InputSystem.Update(); Keys(); adapter.SendMessage("Update");
            for (int i=0;i<50;i++) { grab.SendMessage("FixedUpdate"); scene.GetPhysicsScene().Simulate(.02f); }
            Assert.That(Quaternion.Angle(Quaternion.identity, grab.Body.rotation), Is.GreaterThan(2));
            Assert.That(grab.Body.position.z, Is.GreaterThan(2.1f), "Scroll changes desired hold distance");
            Assert.That(player.Action("Gameplay/Look").enabled, Is.True);
            Keys(Key.G); Assert.That(grab.IsHeld, Is.False); Assert.That(grab.Body.linearVelocity.z, Is.GreaterThan(5));
            yield return null;
        }
        [UnityTest] public IEnumerator ToggleAndNestedWindowsCancelOwnedGrabWithoutReleasingOtherContexts()
        {
            var adapter = Interaction(out var actor); var grab = Target<GrabAction>(actor); adapter.ToggleInsteadOfHold = true;
            Keys(Key.E); Keys(); Assert.That(grab.IsHeld, Is.True);
            Keys(Key.R); adapter.SendMessage("Update"); Assert.That(adapter.IsConsumingLook, Is.True);
            var manager = Windows(); var outer = manager.Open(Window(manager,"Outer")); var inner = manager.Open(Window(manager,"Inner"), outer);
            Assert.That(grab.IsHeld, Is.False); Assert.That(adapter.IsConsumingLook, Is.False); Assert.That(player.ContextRequestCount, Is.EqualTo(2));
            adapter.enabled = false; Assert.That(player.ContextRequestCount, Is.EqualTo(2)); inner.Close(); Assert.That(player.Action("Gameplay/Move").enabled, Is.False);
            Keys(); outer.Close(); yield return null; Assert.That(player.ContextRequestCount, Is.Zero); Assert.That(player.Action("Gameplay/Move").enabled, Is.True);
            adapter.enabled = true; actor.SelectTarget(grab.Target); Keys(Key.E); Keys(); Assert.That(grab.IsHeld, Is.True); adapter.enabled = false; Assert.That(grab.IsHeld, Is.False);
        }
        [UnityTest] public IEnumerator AnalogAndSecondaryUsePublicActorOperations()
        {
            var adapter = Interaction(out var actor); var value = Target<WaveValueAction>(actor); value.InputGesture = InteractionInputGesture.Analog;
            var presenter = actor.gameObject.AddComponent<InteractionPresenter>(); presenter.Refresh(); Assert.That(presenter.Activate(value), Is.True);
            Pad(new GamepadState { rightStick = new Vector2(.525f,0) }); adapter.SendMessage("Update");
            Assert.That(value.LastValue, Is.EqualTo(.5f).Within(.02)); Assert.That(adapter.IsConsumingLook, Is.True);
            presenter.Release(value); adapter.SendMessage("Update"); Pad(new GamepadState()); Assert.That(adapter.IsConsumingLook, Is.False);
            var first = Target<EventAction>(actor,"Two actions",3); var second = first.gameObject.AddComponent<EventAction>(); second.ActionId = "secondary";
            first.Target.Refresh(); actor.SelectTarget(first.Target); int calls = 0; second.OnExecuteEvent.AddListener(()=>calls++);
            Keys(Key.F); Assert.That(calls, Is.EqualTo(1)); yield return null;
        }
        [UnityTest] public IEnumerator BuildingUsesCanonicalSessionAndExplicitFpsModeThenPreservesNestedUi()
        {
            var actor = New("Builder").AddComponent<BuildingActor>(); var interaction = actor.gameObject.AddComponent<BuildingInteractionContext>();
            var go = New("World",false); var placer = go.AddComponent<BuildingPlacer>(); placer.Actor = actor; placer.ShowHelp = false;
            var adapter = go.AddComponent<EasyBuildingInput>(); adapter.Player = player; adapter.UseFirstPersonPreset(); go.SetActive(true);
            var shared = interaction.GetSession(placer.System); Assert.That(placer.Session, Is.SameAs(shared)); Assert.That(adapter.IsBuilding, Is.False);
            var definition = Asset<BuildingDefinition>(); Assert.That(shared.Select(definition), Is.True); Assert.That(adapter.Enter(), Is.True); Assert.That(adapter.Enter(), Is.True);
            Assert.That(player.ContextRequestCount, Is.EqualTo(1)); Assert.That(player.Action("Gameplay/Move").enabled, Is.True); Assert.That(player.Action("Gameplay/Look").enabled, Is.True); Assert.That(player.Action("Gameplay/Fire").enabled, Is.False);
            using var replacement = placer.System.CreateSession(actor); placer.SetSession(replacement); replacement.Select(definition);
            var manager = Windows(); var outer = manager.Open(Window(manager,"Building modal")); var inner = manager.Open(Window(manager,"Building child"),outer);
            adapter.Exit(); adapter.Exit(); Assert.That(replacement.Selected, Is.Null); Assert.That(shared.Selected, Is.SameAs(definition)); Assert.That(replacement.IsDisposed, Is.False);
            Assert.That(player.ContextRequestCount, Is.EqualTo(2)); inner.Close(); Assert.That(player.Action("Gameplay/Move").enabled, Is.False); outer.Close(); yield return null;
            Assert.That(adapter.Enter(), Is.True); Pad(new GamepadState().WithButton(GamepadButton.East)); adapter.SendMessage("Update"); Assert.That(adapter.IsBuilding, Is.False);
            Assert.That(player.Action("Gameplay/Crouch").enabled, Is.False, "Held building cancel cannot crouch after exit"); Pad(new GamepadState()); yield return null;
            Assert.That(player.Action("Gameplay/Crouch").enabled, Is.True);
            adapter.Enter(); adapter.enabled = false; Assert.That(adapter.IsBuilding, Is.False); Assert.That(player.ContextRequestCount, Is.Zero); Assert.That(replacement.IsDisposed, Is.False);
            adapter.Mode = BuildingInputMode.Strategy; adapter.AutoEnterOnEnable = true; adapter.enabled = true; Assert.That(player.Action("Gameplay/Move").enabled, Is.False);
        }
        [UnityTest] public IEnumerator NextTargetAndRebindCancellationUseTheConfiguredSharedActions()
        {
            // Interaction's stock target queries use Unity's default Physics scene.
            targetScene = SceneManager.CreateScene("EasyInput target queries",new CreateSceneParameters(LocalPhysicsMode.None));
            SceneManager.MoveGameObjectToScene(root, targetScene);
            var adapter = Interaction(out var actor);
            var first = Target<EventAction>(actor,"First",2); first.gameObject.AddComponent<BoxCollider>();
            var next = Target<EventAction>(actor,"Next",3); next.gameObject.AddComponent<BoxCollider>();
            first.Target.Refresh(); next.Target.Refresh();
            Assert.That(actor.gameObject.scene.GetPhysicsScene(), Is.EqualTo(Physics.defaultPhysicsScene));
            actor.SelectionMode = InteractionSelectionMode.Nearest; Physics.SyncTransforms(); actor.RefreshTarget();
            Assert.That(actor.CurrentTarget, Is.SameAs(first.Target)); Keys(Key.B); Assert.That(actor.CurrentTarget, Is.SameAs(next.Target)); Keys();
            Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(next.gameObject);
            var grab = Target<GrabAction>(actor); adapter.ToggleInsteadOfHold = true; Keys(Key.E); Keys(); Assert.That(grab.IsHeld, Is.True);
            using var window = player.AcquireContext(root, new InputContextRequest { BlockedActions = new[] { "Gameplay/Fire" } });
            var operation = player.BeginRebind(player.Action("Gameplay/Interact"),0,player);
            Assert.That(grab.IsHeld, Is.False); operation.Dispose(); Assert.That(player.ContextRequestCount, Is.EqualTo(1));
            Assert.That(player.Action("Gameplay/Fire").enabled, Is.False); yield return null;
        }
        [UnityTest] public IEnumerator FpsBuildingRoutesRotationAndConfirmationIntoTheCurrentSession()
        {
            var go = New("Placement world",false); var placer = go.AddComponent<BuildingPlacer>(); placer.ShowHelp = false;
            var adapter = go.AddComponent<EasyBuildingInput>(); adapter.Player=player; adapter.UseFirstPersonPreset();
            var camera = New("Placement camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0,4,-4); camera.transform.LookAt(Vector3.zero); placer.ViewCamera = camera;
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube); surface.transform.SetParent(root.transform); surface.transform.position = new Vector3(0,-.1f,0); surface.transform.localScale = new Vector3(20,.2f,20);
            var definition=Asset<BuildingDefinition>(); definition.Prefab=New("Building prefab",false); definition.CheckObstacles=false;
            go.SetActive(true); placer.Select(definition); Assert.That(adapter.Enter(), Is.True);
            PlacedBuilding placed=null; placer.Session.Confirmed += value=>placed=value;
            int jumps=0; player.Action("Gameplay/Jump").performed += _=>jumps++;
            Keys(Key.R); adapter.SendMessage("Update"); Keys(); Pad(new GamepadState().WithButton(GamepadButton.South)); adapter.SendMessage("Update");
            Assert.That(placed, Is.Not.Null,placer.Status); Assert.That(Mathf.Abs(Mathf.DeltaAngle(placed.transform.eulerAngles.y,90)), Is.LessThan(.1f));
            Assert.That(placer.Session.Selected, Is.Null); Assert.That(adapter.IsBuilding, Is.False); Assert.That(jumps, Is.Zero);
            Assert.That(player.Action("Gameplay/Jump").enabled, Is.False); Pad(new GamepadState()); yield return null;
            Pad(new GamepadState().WithButton(GamepadButton.South)); Assert.That(jumps, Is.EqualTo(1));
            Pad(new GamepadState()); placer.Select(definition); Assert.That(adapter.Enter(), Is.True);
            int shots=0; player.Action("Gameplay/Fire").performed += _=>shots++;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left)); InputSystem.Update(); adapter.SendMessage("Update");
            Assert.That(adapter.IsBuilding, Is.False); Assert.That(shots, Is.Zero); Assert.That(player.Action("Gameplay/Fire").enabled, Is.False);
            InputSystem.QueueStateEvent(mouse,new MouseState()); InputSystem.Update(); yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Left)); InputSystem.Update(); Assert.That(shots, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FeedbackProviderPreservesExplicitDeviceAndEveryContextValue()
        {
            var provider = root.AddComponent<PlayerFeedbackContext>(); provider.Player = player; player.SelectGamepad(pad);
            var other = InputSystem.AddDevice<Gamepad>(); devices.Add(other); other.MakeCurrent();
            var context = new FeedbackContext(new Vector3(3,4,5), Quaternion.Euler(12,23,34), root.transform, root.transform, .4f, .7f, new Vector3(2,3,4), FeedbackPositionMode.WorldPosition);
            var result = provider.Prepare(context); Assert.That(result.TargetGamepad, Is.SameAs(pad));
            Assert.That(result.WorldPosition, Is.EqualTo(context.WorldPosition)); Assert.That(result.Rotation, Is.EqualTo(context.Rotation)); Assert.That(result.Source, Is.SameAs(context.Source)); Assert.That(result.Follow, Is.SameAs(context.Follow));
            Assert.That(result.Intensity, Is.EqualTo(context.Intensity)); Assert.That(result.AudioMultiplier, Is.EqualTo(context.AudioMultiplier)); Assert.That(result.VisualScale, Is.EqualTo(context.VisualScale)); Assert.That(result.Positioning, Is.EqualTo(context.Positioning));
            Assert.That(provider.Prepare(context.WithGamepad(other)).TargetGamepad, Is.SameAs(other)); yield return null;
        }
    }
}
#endif

