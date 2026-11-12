# EasyInput 0.1.0 baseline validation — 2026-11-12

These results describe the first-version archive. Later IntegrationWave1 changes have a separate [report](IntegrationWave1.md); the baseline counts below do not certify those changes.

Validated on Windows with Unity **6000.3.22f1** and Input System **1.20.0**. Daily integration work used the existing Games Factory editor through Unity CLI with an explicit project path and the shared atomic editor lock. Source preparation and standalone players ran outside that lock. No source files in other Easy packages were changed by this implementation.

## Completed checks

| Check | Result | Evidence |
|---|---|---|
| Games Factory compilation | Passed, no compiler errors | [compile](Reports/compile.json) |
| Package EditMode tests | **27/27 passed** | [EditMode](Reports/tests-editor.json) |
| Games Factory PlayMode tests | **25/25 passed** | [PlayMode](Reports/tests-playmode.json) |
| Clean tarball installation | Passed, no optional Easy packages | [installation](Reports/clean-install.json) |
| Clean-project EditMode | **27/27 passed** | [clean EditMode](Reports/clean-editmode.json) |
| Clean-project PlayMode | **7/7 passed** | [clean PlayMode](Reports/clean-playmode.json) |
| Main Windows Development build | Succeeded; 0 errors, 0 warnings | [build](Reports/build-result.json) |
| Clean Windows Development build | Succeeded; 0 errors, 0 warnings | [installation](Reports/clean-install.json) |
| Both standalone benchmark players | Exit 0; live virtual input and subscriber cleanup verified | [performance](Performance.md) |
| Base and EasyMovement examples | Launched, panel verified, game view inspected | [base image](Images/getting-started.png), [movement image](Images/easy-movement.png) |

The 25 PlayMode cases comprise 7 core input tests, 7 optional-adapter tests and 11 EasyUI integration cases. Both final builds completed without warnings.

The package tests cover runtime cloning and source-ID resolution; composite parts; exact-path conflict rejection and reuse in independent maps; single/all reset; JSON capture/restore, unknown/removed binding IDs, missing settings, corrupt/future data; 30/60/144 FPS look units; device drift, switching, disconnect and reconnect; nested/out-of-order/disabled/destroyed owners; partial Look restrictions; held-input release gating; callback failure and re-entry; rebinding keyboard/mouse/gamepad; opening-click release; Escape/East cancellation and timeout; prompt updates; and scene unload.

Additional regressions check rebinding an already-enabled action and then blocking it: ownership reconciliation must not re-enter Input System's temporary binding-state rebuild. Another test compares device-removal state with an equivalent native InputActionAsset and records the Input System 1.20 IsPressed-latch limitation described in [Contracts](Contracts.md).

Tests feed virtual devices while the Editor is unfocused. Input update mode, focus policy and temporary edit-mode feature flags are restored in finally blocks. The virtual vibration test intercepts Input System device commands, permits unfocused editor preview only for its lifetime, and verifies that the selected player's gamepad receives the command even when a different gamepad is globally current.

## Verified integrations

- **EasyMovement 5.0.1 / installed ECM2 1.5:** the real prefab's controller receives exactly 3.2 degrees for 20 mouse pixels at user multiplier 2; settings restore updates its public sensitivity fields; replacing the character retains the same runtime action asset; detaching restores its standalone reader.
- **EasyInteraction 0.2.0:** rebound Interact executes the public Actor/EventAction path, UI blocks it, and detaching restores the standalone input component.
- **EasyBuilding 0.2.0:** shared Building context and ExternalTargeting cooperate; disabling this adapter preserves another owner's InputBlocked request.
- **EasyCombat:** rebound Fire reaches DamageEmitter, respects its cooldown and stops under UI restrictions.
- **EasyFeedbacks 1.7.0:** a real vibration preset/service/manager routes the native rumble command to the selected virtual gamepad. Audio, VFX and physical motor output were not hardware-tested.
- **EasyLocalization 0.1.0 / Unity Localization 1.5.13:** a native LocalizedString updates across English/Russian, rebinding and disable. The test uses an in-memory native table provider in a configured Addressables environment; the adapter's fallback remains ordinary text.
- **uGUI 2.0.0 and EasyUI 0.1.0:** one runtime action asset, independent window leases, partial blocking, custom Player+UI maps, Hidden/Inherit cursor behavior, re-entry/exception safety, module Enable/Disable and cancellation before window closure. The IWindowInputProvider adapter and window classes belong to EasyUI's EasyInput sample.

The combined optional-adapter test fixture is supplied under `Documentation~/Verification`. To reproduce it, install all dependencies listed in [Integrations](Integrations.md), import the corresponding EasyInput samples and copy that fixture into a local test folder. It is excluded from the base package's compiled assemblies so standalone installation stays independent.

## Examples and reproduction

In Games Factory open `Assets/PackageTests/EasyInput/GettingStarted.unity` or `Assets/PackageTests/EasyInput/EasyMovement.unity`, then Play. The base scene opens settings immediately; in the FPS scene press Tab/Start. Sensitivity, inversion, deadzones, rebinding, per-binding reset, Capture and Restore are available. Use Escape/East to cancel rebinding and a separate press to close settings.

For a new project, follow the package README: import Getting Started and run **Tools > EasyInput > Create Getting Started Scene**. Import the optional EasyMovement sample and use its corresponding menu command after installing ECM2/EasyMovement. The standalone example is at `Builds/EasyInput/EasyInput.exe` in the development workspace. Run it with `-batchmode -nographics -easyInputReport <absolute-path.json>` to reproduce the benchmark.

The final clean verification project lives outside imported assets at `Library/EasyInputClean`; its manifest installed a tarball of this package with Input System and Unity Test Framework, without other Easy packages. Its first-run scene, player build and both test suites succeeded. Only this package's files are included in the distributable archive; local scenes, ECM2, other packages and executable builds are excluded.

## Compatibility and limits

Virtual XInputController, DualShockGamepad and SwitchProControllerHID layouts select Xbox, PlayStation and Switch glyph sets; generic Gamepad uses generic labels. **No physical controller, console platform, wireless transport or hardware latency was certified.** An HID exposed as Joystick needs a suitable Input System Gamepad layout.

Conflicts compare exact effective paths in overlapping active contexts/groups. Custom layout aliases and modifier-chord equivalence are not normalized. Interactive cancellation reserves Escape and Gamepad East. uGUI pointer buttons are suspended during capture; cancellation uses the independent reserved controls or public API. Input System 1.20 may clear IsPressed's latch during bound-device topology changes; release/repress if a polling consumer relies on it. The phase/value comparison and removed-device cancellation are tested.

The baseline adapter omitted advanced Interaction controls; IntegrationWave1 adds them through the public APIs. Building's old private palette rebinder does not edit EasyInput's shared actions. Multiple local players require explicit PairDevices and one designated OS-cursor owner. These boundaries and units are documented in Contracts/Integrations.

During parallel development, the shared Editor was blocked by a separate EasyLocalization HTTP test. The user authorized a restart; the verified Games Factory process was restarted once, compilation recovered, and the final successful tests above were rerun. This did not require modifying another package's source or changing the EasyInput contract.
