# EasyMovement

**Compatibility:** EasyInput 0.2.0+, EasyMovement 5.1.0+, ECM2 (separate installation; tested with 1.4.3). No prerequisite sample imports.

Requires EasyMovement 5.1.0+ and ECM2 (tested with 1.4.3). Import those dependencies, then this sample and run **Tools > EasyInput > Create EasyMovement Scene**. Press Play: WASD/left stick move; mouse/right stick look; Space/South jumps; Shift/left stick press sprints; C/East crouches; Tab/Start opens settings. Change a slider or binding and return to the same character.

For an existing player, add EasyMovement Input to the GameObject containing FirstPersonCharacterController (inside FP_Player_Sample), then assign your PlayerInputSettings. The adapter temporarily disables the standalone reader and restores it on disable. Keep settings on an object that survives character replacement. Motor speeds, gravity and physics remain in EasyMovement/ECM2.
