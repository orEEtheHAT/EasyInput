# EasyInput 0.2.0

One `PlayerInputSettings` component owns the player's controls, rebinding and user settings. Controllers, menus and prompts share the same runtime actions. Source assets remain configuration.

## Install and first run

1. Use Unity **6000.3**. Install this package from disk through **Window > Package Management > Package Manager > + > Install package from disk**, selecting `package.json`. After publishing, the Git URL is `https://github.com/orEEtheHAT/EasyInput.git#v0.2.0`. Input System **1.20.0**, IMGUI and JSON modules are UPM dependencies.
2. Set **Edit > Project Settings > Player > Other Settings > Active Input Handling = Input System Package (New)** (Both also works); restart if Unity requests it.
3. In Package Manager, open **EasyInput > Samples > Getting Started > Import**.
4. Select **Tools > EasyInput > Create Getting Started Scene**. The scene is created at `Assets/EasyInput Example/GettingStarted.unity`. Press **Play**.
5. The settings panel opens immediately. Move the sensitivity slider, select **Interact**, click the **KeyboardMouse: E** binding, release the mouse button and press **K**. The prompt changes to K. **Escape** cancels a pending rebind; after ten seconds it times out. Space is already Jump, so assigning it to Interact is rejected with an explanation.
6. Select **Move** and rebind each WASD part individually. Each binding has Reset; **Reset all** restores user values and all binding paths. Mouse buttons and gamepad slots are supported separately.
7. **Capture** stores a settings snapshot in memory. Change a slider or binding, then **Restore** to recover that snapshot. Persistence to disk belongs to your save system.
8. Close settings. Move the mouse or the gamepad's right stick to rotate the preview cube; press **E** (or the rebound key) / **West** to increment the interaction counter. **Tab / Start** reopens settings. In the panel, use arrows/D-pad, left/right on sensitivity rows, **Enter / South** to select, **Escape / East** to close. Meaningful gamepad input switches prompts; small stick drift does not.

Expected result: settings apply immediately, new bindings appear in the prompt, UI prevents gameplay actions, and closing restores only the requests that have actually been released. The cube is an input preview; the optional EasyMovement sample supplies the FPS integration.

## Your player

Add **EasyInput > Player Input Settings**. Leave Source Actions and Profile empty to use the standard resources. Add **EasyInput > Input Settings Panel** for a standalone settings view. An **Input Prompt** supplies TextChanged and SpriteChanged UnityEvents to your chosen view; it also exposes Text and Presentation. Optional uGUI and localized views live in Samples.

The included resources are `Runtime/Resources/EasyInputDefaults.inputactions`, `EasyInputStandard.asset`, `EasyInputAiming.asset`, and the Glyphs directory. Use **Create > EasyInput > Input Profile / Input Glyph Set** for project-owned configurations. User values live in `player.Settings`, not in those SO assets. Default maps are Gameplay, UI and Building. With a custom action asset, preserve stable IDs and configure matching context map names.

```csharp
using EasyInput;
using UnityEngine;
using UnityEngine.InputSystem;

InputAction move = player.Action("Gameplay/Move");
Vector2 axes = move.ReadValue<Vector2>();
InputAction look = player.Resolve(originalLookReference); // resolves by stable action ID
Vector2 degrees = player.ReadLookDelta(look, Time.deltaTime);

player.MouseSensitivity = 1.5f; // user multiplier; default base = .08 degrees/pixel
var settings = player.Settings;
settings.InvertY = true;
player.SetSettings(settings);

var binding = player.GetPresentation("Gameplay/Interact"); // Text, Sprite, composite Parts
var operation = player.BeginRebind(player.Action("Gameplay/Interact"), 0, this);
player.CancelRebind();
player.ResetBinding(player.Action("Gameplay/Interact"), 0);

InputSettingsSnapshot saved = player.CaptureSettings();
string json = JsonUtility.ToJson(saved);
bool restored = player.RestoreSettingsJson(json, out string error);

InputContextHandle menu = player.AcquireContext(this, InputContextRequest.UI());
// When THIS window closes (also OnDisable):
menu.Dispose();
```

Subscribe to SettingsChanged / PresentationChanged / RebindChanged in OnEnable and unsubscribe in OnDisable. Consumers never own action Enable/Disable. PairDevices optionally isolates local players; select devices with Settings.DevicePreference and SelectGamepad. Only one player should own the shared desktop cursor.

## Optional packages

Import **EasyMovement** after installing EasyMovement and ECM2. Put **EasyMovement Input** on FP_Player_Sample and assign Player Input Settings. Its existing motor and cameras remain in use. The adapter temporarily replaces the standalone input reader, respects scripted movement blocks, reads the shared actions and applies look scaling once. For FPS building, apply the First Person Preset, select through the shared BuildingActor session, and call Enter(); Exit() releases its context. Interaction includes shared prompts, throw, item rotation/distance and analog controls. Detailed setup and required integration API generation for Building, Interaction, Combat, Feedbacks, Localization, uGUI and EasyUI: [Integrations](Documentation~/Integrations.md).

EasyUI's own **EasyInput sample** uses this same API for its full window. EasyInput does not define a window stack. Localized Input Prompt uses LocalizedString plus EasyLocalization and falls back to ordinary text.

## Validation and limits

See [Validation](Documentation~/Validation.md) for actual test/build results and [Contracts](Documentation~/Contracts.md) for ownership, conflict, cursor and save-format rules. Test source is in Tests; add `com.oreethehat.easy-input` to the project's manifest `testables` and install Unity Test Framework to run it. Optional adapters have separate assemblies and declared dependencies.

Xbox, PlayStation, Switch Pro and generic gamepads use Input System layouts and family-specific glyph sets. Virtual-device tests verify package logic; they do not certify physical hardware, wireless transport or console support. Physical devices have not been certified by this implementation task. All included geometric icon artwork is original; see [asset provenance](Third%20Party%20Notices.md).

## Sample compatibility

The samples shipped with this release require EasyInput **0.2.0+**. Core installation remains independent of other Easy packages. Install only the dependencies for the sample you need, then import it once from Package Manager. The table gives the supported minimum versions for this adapter generation; older snapshots may lack its public APIs. Unity dependencies declared in package.json are installed automatically.

| Sample | Additional requirements | Import first |
|---|---|---|
| Getting Started | None | — |
| EasyMovement | EasyMovement 5.1.0+, ECM2 (separate installation; tested with 1.4.3) | — |
| EasyInteraction | EasyInteraction 0.3.0+ | — |
| EasyBuilding | EasyBuilding 0.3.0+ | — |
| EasyCombat | EasyCombat 0.7.0+ | — |
| EasyFeedbacks | EasyFeedbacks 1.8.0+ | — |
| Localization | EasyLocalization 0.2.0+, Unity Localization 1.5.13+ | — |
| UGUI | uGUI 2.0.0+ | — |
| Benchmarks | None | — |

Keep one copy of each sample assembly. Existing imports may remain in their current Assets folder; review and merge updates before reimporting so local edits and asset/action GUIDs are preserved.
