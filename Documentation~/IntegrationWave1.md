# IntegrationWave1 validation — 2026-11-12

This integration update keeps package version 0.1.0 and all original action/binding IDs. Release versioning and the shared EasySuite scene are coordinated separately. The older [baseline validation](Validation.md) describes the first-version archive.

## Public behavior

- EasyMovementInput respects independent scripted input blocks, releases only its previously owned buttons, and waits for held buttons to be released before accepting a new press.
- EasyInteractionInput supplies current binding text, glyph and toggle mode to InteractionPresenter. Item rotation, distance, throw, analog value, secondary action and target cycling use shared actions. A scoped context consumes Look; UI/rebinding/disable cancel operations initiated by this reader and release its own context.
- EasyBuildingInput exposes Enter/Exit/IsBuilding. The First Person preset retains movement/look; Strategy retains the previous full Gameplay block. The adapter borrows the current Placer.Session and never creates/disposes its own session.
- PlayerFeedbackContext preserves an explicit TargetGamepad, explicit no-gamepad request and all other context values. Keyboard-only input cannot fall back to another player using Gamepad.current; the direct InputFeedback path follows the same rule. LocalizedInputPrompt listens to the public localization lifecycle event; its fixture restores the previous settings and configuration.
- InputActionLink resolves source references by stable ID or map/action name. Inspector diagnostics explain missing actions and provider types; advanced settings are folded away.

See [integration setup](Integrations.md) for the exact setup, controls, API prerequisites, units and compatibility behavior.

## Checks

| Check | Result | Evidence |
|---|---|---|
| Shared project compilation after adapter/editor import | Passed, no errors | [compile](Reports/IntegrationWave1/wave1-editor-fix-compile.json) |
| Clean package installation without other Easy packages | Passed | [install](Reports/IntegrationWave1/clean-install.json) |
| Clean first-run Windows Development build | Succeeded, 0 errors / 0 warnings | [install](Reports/IntegrationWave1/clean-install.json) |
| Clean EditMode | 31/31 | [XML](Reports/IntegrationWave1/clean-EditMode.xml) |
| Clean PlayMode | 7/7 | [XML](Reports/IntegrationWave1/clean-PlayMode.xml) |
| Original action definitions | 3 maps, 24 actions, 53 bindings unchanged; 7 actions appended | [IDs](Reports/IntegrationWave1/stable-input-ids.json) |
| Standalone benchmark | Exit 0; virtual input and subscriber cleanup verified | [measurements](Reports/IntegrationWave1/benchmark-clean.json) |
| Shared project final EditMode | 31/31 | [results](Reports/IntegrationWave1/final-core-editor.json) |
| Shared project final PlayMode | 37/37, 0 skipped | [results](Reports/IntegrationWave1/final-integrations-playmode.json) |
| Isolated new adapter regressions | 9/9 | [XML](Reports/IntegrationWave1/isolated-adapters.xml) |

Clean validation used Unity 6000.3.22f1 and Input System 1.20.0 in `Library/EasyInputWave1Work/CleanProject`. The validation archive is an internal snapshot, not a new published release. The independent initial clean import caught omitted .meta files in staging; Unity-generated metadata was added before the successful installation/build. A former rebind fixture assumed Left Shoulder was unassigned; it now checks the new conflict, then explicitly frees the rotation binding with actions blocked before testing the rebind. The conflict policy was not weakened.

The native input tests configure virtual devices for unfocused manual input and restore global Input System settings during teardown. Localization teardown uses public Configuration/Reset/ReinitializeAsync instead of reflecting private state. A held stick deliberately keeps Look behind the release gate after its consuming context ends; neutral input and the next player update restore it.

The final 37 shared PlayMode cases comprise 7 core rebind tests, 17 EasyInput adapter/lifecycle tests and 13 EasyUI input-window regressions. The keyboard-only regression intercepts native nonzero RMBL commands: the selected player gamepad receives output, explicit none is preserved, and keyboard input produces no output on another globally-current gamepad. It covers both an assigned PlayerFeedbackContext and the direct InputFeedback path. New movement/interaction/building cases use the real packages, including independent approach blocks, held buttons, presentation/profile changes, physical Grab rotation/distance/throw, analog/secondary/next-target controls, nested windows, canonical session replacement, FPS confirmation/cancel and jump/fire release gates. The final compiler report contains no errors. [Machine-readable summary](Reports/IntegrationWave1/summary.json).

## Measurements and limits

On this Windows/i7-14700KF machine, the Development build measured about 0.57–0.60 microseconds per input read (100,000 iterations each), zero read allocations, and about 16 microseconds for idle core work per frame. One prompt presentation change measured about 13 microseconds; 100 prompts about 378 microseconds; 2,000 prompts about 6.6 milliseconds. Presentation changes allocate formatted text/cache data (226 B, 5.4 KB and 104 KB per change respectively in this benchmark); ordinary reads do not. This is a synthetic local measurement, not a hardware latency claim. No Jobs/Burst was added.

Gamepad tests use virtual devices. Physical controller output, consoles and other operating systems have not been certified. Optional samples require the coordinated API generation described in Integrations; older published versions may not contain those additions. The master EasySuite composition and full common audit belong to the coordinator.
