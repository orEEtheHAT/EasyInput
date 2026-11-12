# Optional integrations

Import only the samples whose dependencies are installed. The base EasyInput runtime needs only its package.json dependencies. Each optional sample owns a separate assembly; its Editor folder supplies setup diagnostics. All consumers read the player's runtime actions and leave their lifetime to PlayerInputSettings.

These EasyInput 0.2.0 adapters require EasyMovement 5.1.0+, EasyInteraction/EasyBuilding 0.3.0+, EasyCombat 0.7.0+, EasyFeedbacks 1.8.0+ or EasyLocalization 0.2.0+ only for the corresponding imported sample. See the [sample compatibility table](../README.md#sample-compatibility) for native package requirements. EasyUI 0.2.0+ owns the separate EasyInput window adapter. Older adapter generations may lack the public APIs below.

| Sample | Required public API | Setup |
|---|---|---|
| EasyMovement | FirstPersonCharacterController.InputBlocked, InputBlockedChanged, AcquireInputBlock; ECM2 | Put EasyMovement Input beside the First Person Character Controller in FP_Player_Sample. Assign the player's PlayerInputSettings. |
| EasyInteraction | IInteractionInputSource; InteractionPresenter.InputSource; Actor/GrabAction APIs | Put EasyInteraction Input beside InteractionActor, assign the same Player. Add InteractionPresenter for text/icon presentation. |
| EasyBuilding | BuildingPlacer.SessionChanged/SetSession/TryConfirm/CancelSelection; canonical BuildingActor session | Put EasyBuilding Input beside BuildingPlacer, assign Player, Placer.ViewCamera and Placer.Actor. Apply Use First Person Preset for an FPS. |
| EasyCombat | DamageEmitter | Assign Player, DamageEmitter and Aim. Combat owns damage, attack rules and cooldown. |
| EasyFeedbacks | IFeedbackContextProvider.Prepare and explicit-none-aware FeedbackContext.WithDefaultGamepad | Add PlayerFeedbackContext to the player and assign Player. Supply it to the optional Context Provider on event adapters. InputFeedback also works directly with Player. |
| Localization | Localization.Changed, Reset, ReinitializeAsync, TextAsync; Unity Localization | Add LocalizedInputPrompt and assign Player and LocalizedString. A non-Smart entry can contain `Interact [{button}]`. |
| UGUI | InputSystemUIInputModule, uGUI | Put InputUIActions beside the module and assign Player; request UI context when a menu opens. |
| EasyUI (sample owned by EasyUI) | EasyInputWindowProvider | Assign it as WindowManager.InputProvider. It owns a separate EasyInput context for each window. Do not also attach InputUIActions to that module. |
| Benchmarks | Base package | Development player with `-easyInputReport <absolute-path.json>`. |

## One FPS player

1. Import EasyMovement, EasyInteraction and EasyBuilding samples after installing their dependencies.
2. Add one PlayerInputSettings on the player root, or an ancestor kept alive across character replacement. Leave its source and profile empty for defaults.
3. Add EasyMovementInput on FirstPersonCharacterController and EasyInteractionInput on InteractionActor. Assign the same Player. Leave the existing standalone readers present; adapters disable and restore them automatically.
4. Add InteractionPresenter on the actor. Leave Input Source empty to select the active EasyInput adapter automatically, or assign it explicitly. After Interact is rebound, the presenter updates its label and icon without restarting the action/actor.
5. Add a BuildingActor to this player. Set BuildingPlacer.Actor and the Interaction building context to that same actor. Set Placer.ViewCamera to the FPS camera. In EasyBuildingInput click **Use First Person Preset**. This changes Mode to FirstPerson, disables AutoEnterOnEnable and uses screen center targeting.
6. Select a definition in the actor's shared session, then call Enter. Cancel exits; another window may still own restrictions after exit.

```csharp
// Both the Interaction building action and the Placer borrow this same actor/world session.
BuildingSession session = buildingActor.GetSession(placer.System);
session.Select(definition);
buildingInput.Enter();
// Button, UI callback or gameplay cancellation:
buildingInput.Exit();
```

Enter/Exit are idempotent. IsBuilding and ModeChanged expose the mode. Exit, input Cancel and disabling/destroying the adapter cancel the current selection and release only this adapter's context. A selection ending externally also ends the mode. Disabling this adapter does not Dispose the borrowed session. It reads the current Placer.Session after SetSession or Actor changes and detaches from the previous session's events. When an explicit session is disposed, use Placer.SetSession(activeSession) or SetSession(null) before entering again.

FirstPerson enables the Building map while retaining Gameplay Move, Look, Sprint and Settings. It blocks Fire, Aim, Jump, Crouch and interaction/item actions. Jump/Crouch share gamepad South/East with Confirm/Cancel; release gates prevent the building button from triggering a jump/crouch immediately after exit. Strategy blocks all Gameplay. Legacy scenes retain Mode=Strategy and AutoEnterOnEnable=true. EnterBuildingContext=false retains externally owned context control; the adapter still needs enabled Building actions. Placer.InputBlocked and other EasyInput owners remain effective. A modal UI temporarily hides the preview; closing the UI restores the still-active building mode.

The Placer's private action clones and BuildingPalette's private rebinding are separate from EasyInput. Configure the shared actions through EasyInput settings. GameplayMap and ExtraBlockedActions cover custom assets; remapped conflicts (for example a custom Reload) should be included explicitly. FPS preserved actions should have bindings distinct from building controls.

## Movement ownership

EasyMovementInput respects Character.InputBlocked in Update and every input callback. A scripted motor obtains `character.AcquireInputBlock(owner)` before setting its intents, and disposes that handle when finished. The block affects manual readers, not scripted SetMovementDirection/SetRotation. Independent handles coexist. The adapter releases its own held buttons once on entering the block, then leaves the scripted motor untouched, including during canceled callbacks and disable. Held buttons require release and a fresh press after the last block; continuous movement/look can resume.

The adapter mirrors sensitivity/gamepadLookSpeed/invertLook into the disabled standalone reader for inspector visibility and restores its prior values on detach. Mouse displacement and stick speed are scaled once by Player.ReadLookDelta. Motor physics and movement speeds stay in EasyMovement/ECM2.

## Interaction controls and presentation

| Action | Keyboard/mouse | Gamepad default |
|---|---|---|
| Interact | E | West |
| Cancel / next action | Q / V | North / Right Shoulder |
| Secondary action / next target | F / B | Select / D-pad Right |
| Throw held item | G | D-pad Left |
| Rotate held item | Hold R + mouse movement | Hold Left Shoulder + Right Stick |
| Hold distance | Mouse wheel | D-pad Up / Down |
| Analog interaction | Mouse X | Right Stick X |

Defaults preserve all pre-wave action and binding IDs; item and secondary actions were appended. Check the player's asset for any project-specific overrides. Multiple bindings and original source InputActionReferences resolve into the same Player.Actions by stable ID.

Advanced binding fields use InputActionLink: Reference takes priority; otherwise Name is `Map/Action`. Clear an optional name to disable that feature. Older custom assets may omit new actions; only Interact is required. Re-enable an adapter after changing its link configuration during Play Mode. Rebinding the resolved runtime action through Player does not require re-enabling.

ItemRotation and InteractionValue must be distinct actions from Look; they can share physical bindings. During rotation or active analog interaction the adapter owns a context blocking only Look. It releases that request on completion/cancel, disabled item actions, UI/rebinding and disable. Owned grabs/toggled operations are canceled when their initiating input is blocked or the adapter detaches; unrelated operations started through other owners are preserved.

Mouse item rotation uses the player's degrees/pixel times MouseRotationMultiplier (default 3.125; standard .08 degrees/pixel gives .25 degrees/pixel). Stick rotation uses the player's degrees/second, one deltaTime, and GamepadRotationMultiplier (.5625; standard 160 gives 90 degrees/second). Distance is meters per scroll unit or meters/second for a gamepad axis. Analog passes a normalized scalar to Actor/CommandAdapter.SetValue; the action owns any integration over time. `easyInputAxis` applies the user's deadzone to scalar stick bindings; `easyInputStick` does the same for Vector2. Do not add a second sensitivity/time stage in consumers.

BindingLabel, BindingIcon, ToggleInsteadOfHold and PresentationChanged implement IInteractionInputSource. Device, glyph profile and binding changes propagate to InteractionPresenter. Disabling the adapter restores the old InteractionInput; an explicitly assigned disabled InputSource remains disabled instead of falling back.

## Feedback and localization

PlayerFeedbackContext.Prepare uses WithDefaultGamepad: it preserves an explicit target or WithoutGamepad() request and every other context value. Otherwise it supplies Player.ActiveGamepad. Attach it to the same player and assign it to the Context Provider field on UI/Interaction/Building/Combat feedback adapters and CharacterStepController in EasyMovement 5.1.0+. It does not create service managers; add the relevant EasyFeedbacks manager (for example GamepadVibrationManager or AudioManager). When the active device is keyboard/mouse, ActiveGamepad is null; WithDefaultGamepad(null) explicitly disables gamepad fallback, so another player using Gamepad.current is not vibrated. Legacy FeedbackContext.From/default retains fallback for standalone users. InputFeedback uses the same player-aware rule even without an assigned ContextProvider.

LocalizedInputPrompt uses standard LocalizedString, listens to Localization.Changed and player presentation, cancels superseded requests and keeps a plain-text fallback. SetLanguage uses the public localization API. Lifecycle cancellation is quiet. Tests replacing LocalizationSettings.Instance must disable their views, call Localization.Reset(), replace the settings, and await ReinitializeAsync(config). Restore the prior settings/config using the same sequence; no reflection into private reset methods is needed.
