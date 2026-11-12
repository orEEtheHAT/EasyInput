# Changelog

## 0.2.0 — 2026-11-12

- Added stable-ID or named `InputActionLink` configuration for optional adapter actions without cloning or taking ownership of the player's runtime actions.
- Movement adapters respect shared manual-input blocks. Interaction adapters supply live binding/glyph/toggle presentation, item rotation/distance/throw, secondary/target cycling and analog use, with independently owned look restrictions and cancellation of their own operations.
- Added explicit building Enter/Exit with FPS and legacy strategy presets using the canonical borrowed actor session.
- PlayerFeedbackContext preserves explicit devices and explicit silence; keyboard-owned feedback no longer falls back to another player's gamepad. Localized prompts use the public localization lifecycle and quietly cancel obsolete requests.
- Setting a binding to its existing effective path succeeds as a no-op; new conflicts remain rejected. Existing map/action/binding IDs are preserved and seven actions were added.
- Added adapter setup diagnostics and integration regressions; the standalone benchmark documents calibrated allocation-event and frame-byte counters.

## 0.1.0

- One runtime InputActionAsset per player, stable-ID references and independent consumer lifetime.
- Sensitivity, inversion, radial stick deadzone, SO profiles and versioned capture/restore.
- Interactive per-binding/composite-part rebinding, conflict rejection, cancel, timeout and reset.
- Owner-specific map/action restrictions, nested cursor requests and held-input release gating.
- Device selection, reconnect handling, family glyphs, event-driven prompts and text fallback.
- Standalone settings example and optional integration adapters.
- EditMode/PlayMode regressions and a standalone benchmark harness.
