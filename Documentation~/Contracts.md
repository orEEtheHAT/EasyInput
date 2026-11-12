# Input ownership and integration contract

All APIs run on Unity's main thread. `PlayerInputSettings` owns a clone of one source `InputActionAsset`, including its stable action/binding IDs. Controllers, prompts, UI and rebinding use `player.Actions`, `player.Action("Gameplay/Move")` or `player.Resolve(sourceReference)`. Do not clone actions in consumers. Do not use another `PlayerInput` to instantiate a second copy. Disabling a consumer removes its subscriptions; it does not disable the player's actions.

## Window requests

```csharp
InputContextHandle lease = player.AcquireContext(this, InputContextRequest.UI());
// On closing this window, including OnDisable:
lease.Dispose(); // Safe to repeat or release out of order.
```

Each open window owns a distinct handle. All `BlockedMaps` and `BlockedActions` restrictions apply together; another request cannot undo them. `EnabledMaps` are additive to the profile's DefaultMaps. The highest Priority with a non-Inherit cursor wins, and the newest request wins ties. Inherit returns to the profile policy; an Inherit profile restores the pointer state captured on player enable. Locked hides and locks; Free shows an unlocked pointer; Hidden hides an unlocked pointer; Confined shows a confined pointer (platform support applies).

`InputContextRequest.UI()` enables UI, blocks Gameplay and Building, and requests Free at priority 100. Building() enables Building and blocks Gameplay at priority 10. These convenience requests target the included asset. For another asset, pass its map names explicitly:

```csharp
var request = new InputContextRequest {
    EnabledMaps = new[] { "Menus" }, BlockedMaps = new[] { "Player" },
    Cursor = InputCursorPolicy.Free, Priority = 100
};
var lookOnly = new InputContextRequest { BlockedActions = new[] { "Gameplay/Look" } };
```

Owner must be a live Unity object. Disabled Behaviours, inactive GameObjects and destroyed/unloaded scene owners are pruned on the next player Update; call Dispose immediately in your close/disable path. Disabling the player releases all requests and cancels rebinding. Re-enable consumers/windows must acquire fresh requests. Disabling an action generates cancellation. Re-enabling waits until that action's held buttons/sticks are neutral; there is no synthetic press after closing a menu.

Use `ControlsCursor` on only the local player that owns the shared OS pointer. Device isolation for local players is explicit with `PairDevices(keyboard, mouse, gamepad)`. Default automatic selection is intended for one local player. UI action map names are configuration, not automatic aliases to EasyMovement's standalone Player map.

Device removal cancels input from that device. EasyInput preserves the native phase/value of another device's held action. In Input System 1.20, disconnecting a device bound to the same action can still clear that action's `IsPressed()` latch even while its keyboard value remains 1 and phase remains Performed; a comparison test reproduces this with an unwrapped InputActionAsset too. Release/repress after a device topology change if a polling consumer relies on IsPressed. EasyInput does not synthesize a new gameplay press. Unknown HID devices exposed as Joystick instead of Gamepad need a custom Input System Gamepad layout; this version targets Gamepad layouts.

Input System UI modules normally call Enable/Disable themselves. The included uGUI adapter assigns references into the existing runtime asset, disables its module during rebind, and restores previous configuration on detach. The EasyInput owner reconciles external action lifetime changes to the active requests. Always test a custom UI module's event timing; do not assign gameplay bindings to UI actions.

## Rebinding and persistence

`BeginRebind(action, index, owner)` blocks all maps, waits 150 ms and release of the opening button, then uses Input System `PerformInteractiveRebinding`. Escape and Gamepad East are reserved for cancellation and cannot be assigned interactively. They are read from Input System events independently of the UI module, including the release-wait stage; the cancellation event is consumed, so another press is required to close a window. UI buttons driven by a disabled uGUI module cannot be clicked during capture; use the reserved controls or call CancelRebind from a separate input route. Timeout defaults to ten real-time seconds. A composite header is not assignable: choose each Up/Down/Left/Right part. The binding's standard control scheme restricts devices. Binding operations are disposed on finish/cancel, owner loss, player disable and active-device loss.

The first version rejects an occupied binding without changing either binding. Conflicts include the target map and maps concurrently enabled when rebinding starts, within overlapping control-scheme groups. Separate UI/Gameplay contexts may reuse a key. Identical control paths are checked; semantic equivalence of custom layout aliases, modifier chords and custom composite interactions is outside this version's conflict policy.

CaptureSettings returns format Version 1, named user values and standard binding-overrides JSON with stable binding GUIDs. Restore validates before applying. Missing settings use current profile defaults; unknown setting keys and removed binding IDs are ignored. New actions retain defaults. Keep action and binding IDs when editing the source asset. Invalid ranges, duplicate IDs, unsupported versions and malformed JSON are rejected. Processor/interaction overrides are configuration and are rejected in user save data. The game chooses storage and persistence timing; EasyInput does not write PlayerPrefs or files.

## Units and profiles

Mouse input is displacement in pixels per update; profile MouseDegreesPerPixel converts it to yaw/pitch degrees. Stick input is a normalized rate; GamepadDegreesPerSecond converts it to degrees per second and ReadLookDelta multiplies by deltaTime once. User sensitivities and LookMultiplier multiply these bases. Positive pitch means look up, matching EasyMovement. Inversion is applied once. The controller owns movement speed, gravity, acceleration, pitch limits and physics.

`easyInputStick` reads the raw stick and applies one radial deadzone; it replaces, rather than stacks with, the control's built-in deadzone. Add it to each custom stick binding (Move and Look). Do not add another deadzone/scale/time processor to those bindings. UI navigation can keep the standard Input System processors. Custom bindings using ordinary stickDeadzone retain their authored processing and do not receive EasyInput's user deadzone.

ScriptableObject profiles are configuration. SetProfile changes base sensitivity, default maps and glyphs while retaining the player's current user values. ResetSettings explicitly resets user values to that profile's defaults. Aiming is an example profile at half look sensitivity. Events SettingsChanged, BindingsChanged, ProfileChanged, DeviceChanged, PresentationChanged, ContextChanged and RebindChanged isolate callback exceptions; event dispatch allocates no invocation-list array.
