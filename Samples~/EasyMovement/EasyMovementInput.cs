using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using EasyMovement.FirstPerson;

namespace EasyInput.Integrations
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(FirstPersonCharacterController))]
    [AddComponentMenu("EasyInput/Integrations/EasyMovement Input")]
    public sealed class EasyMovementInput : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public FirstPersonCharacterController Character;
        public float MinimumPitch = -80, MaximumPitch = 80;
        public InputActionLink Move = new InputActionLink("Gameplay/Move");
        public InputActionLink Look = new InputActionLink("Gameplay/Look");
        public InputActionLink JumpAction = new InputActionLink("Gameplay/Jump");
        public InputActionLink CrouchAction = new InputActionLink("Gameplay/Crouch");
        public InputActionLink SprintAction = new InputActionLink("Gameplay/Sprint");
        public InputActionLink NoclipAction = new InputActionLink("Gameplay/ToggleNoclip");
        private FirstPersonCharacterControllerInput standalone;
        private bool standaloneEnabled, oldInvert, connected;
        private Vector2 oldSensitivity;
        private float oldSpeed;
        private InputAction move, look, jump, crouch, sprint, noclip;
        private bool holdingJump, holdingCrouch, holdingSprint;
        private bool waitJump, waitCrouch, waitSprint, waitNoclip, wroteMovement;
        private Vector3 lastMovement;
        private bool CanRead => Character && Character.isActiveAndEnabled && !Character.InputBlocked && Player && Player.isActiveAndEnabled;

        private void OnEnable()
        {
            if (!Character) Character = GetComponent<FirstPersonCharacterController>();
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player || !Character) { Debug.LogError("EasyMovement Input: assign Player Input Settings and the EasyMovement character.", this); enabled = false; return; }
            try
            {
                move = Move.Resolve(Player, true); look = Look.Resolve(Player, true); jump = JumpAction.Resolve(Player);
                crouch = CrouchAction.Resolve(Player); sprint = SprintAction.Resolve(Player); noclip = NoclipAction.Resolve(Player);
            }
            catch (System.ArgumentException e) { Debug.LogError(e.Message, this); enabled = false; return; }
            standalone = Character.GetComponent<FirstPersonCharacterControllerInput>();
            if (standalone)
            {
                standaloneEnabled = standalone.enabled; oldSensitivity = standalone.sensitivity; oldSpeed = standalone.gamepadLookSpeed; oldInvert = standalone.invertLook;
                standalone.enabled = false;
            }
            if (jump != null) { jump.performed += Jump; jump.canceled += StopJump; }
            if (crouch != null) { crouch.performed += Crouch; crouch.canceled += StopCrouch; }
            if (sprint != null) { sprint.performed += Sprint; sprint.canceled += StopSprint; }
            if (noclip != null) { noclip.performed += Noclip; noclip.canceled += StopNoclip; }
            Character.InputBlockedChanged += BlockChanged;
            Player.SettingsChanged += ApplySettings; Player.ProfileChanged += ApplySettings;
            connected = true; RequirePhysicalRelease(); ApplySettings();
        }
        private void ApplySettings()
        {
            if (!standalone || !Player) return;
            standalone.sensitivity = Player.MouseDegreesPerPixel;
            standalone.gamepadLookSpeed = Player.GamepadDegreesPerSecond;
            standalone.invertLook = !Player.Settings.InvertY;
        }
        private static bool PhysicallyPressed(InputAction action)
        {
            if (action != null) foreach (var control in action.controls)
                if (control.device.added && control is ButtonControl button && button.isPressed) return true;
            return false;
        }
        private void RequirePhysicalRelease()
        { waitJump = PhysicallyPressed(jump); waitCrouch = PhysicallyPressed(crouch); waitSprint = PhysicallyPressed(sprint); waitNoclip = PhysicallyPressed(noclip); }
        private void ReleaseOwnedButtons()
        {
            if (Character)
            {
                if (holdingJump) Character.StopJumping();
                if (holdingCrouch) Character.UnCrouch();
                if (holdingSprint) Character.StopSprinting();
            }
            holdingJump = holdingCrouch = holdingSprint = false;
        }
        private void BlockChanged(bool blocked)
        {
            // The first transition releases this reader's old intents before the scripted provider writes.
            // Further cancellation/disable callbacks while blocked must leave the provider untouched.
            if (blocked) { ReleaseOwnedButtons(); wroteMovement = false; }
            RequirePhysicalRelease();
        }
        private void Jump(InputAction.CallbackContext _)
        { if (!CanRead) { waitJump = true; return; } if (waitJump || Character.IsNoclipActive) return; holdingJump = true; Character.Jump(); }
        private void StopJump(InputAction.CallbackContext _)
        { if (!PhysicallyPressed(jump)) waitJump = false; if (CanRead && holdingJump) Character.StopJumping(); holdingJump = false; }
        private void Crouch(InputAction.CallbackContext _)
        { if (!CanRead) { waitCrouch = true; return; } if (waitCrouch || Character.IsNoclipActive) return; holdingCrouch = true; Character.Crouch(); }
        private void StopCrouch(InputAction.CallbackContext _)
        { if (!PhysicallyPressed(crouch)) waitCrouch = false; if (CanRead && holdingCrouch) Character.UnCrouch(); holdingCrouch = false; }
        private void Sprint(InputAction.CallbackContext _)
        { if (!CanRead) { waitSprint = true; return; } if (waitSprint) return; holdingSprint = true; Character.Sprint(); }
        private void StopSprint(InputAction.CallbackContext _)
        { if (!PhysicallyPressed(sprint)) waitSprint = false; if (CanRead && holdingSprint) Character.StopSprinting(); holdingSprint = false; }
        private void Noclip(InputAction.CallbackContext _)
        { if (!CanRead) { waitNoclip = true; return; } if (!waitNoclip) Character.ToggleNoclip(); }
        private void StopNoclip(InputAction.CallbackContext _) { if (!PhysicallyPressed(noclip)) waitNoclip = false; }
        private void ClearOwnedMovement()
        {
            if (Character && !Character.InputBlocked && wroteMovement && Character.GetMovementDirection() == lastMovement)
                Character.SetMovementDirection(Vector3.zero);
            wroteMovement = false;
        }
        private void Update()
        {
            if (!connected) return;
            if (!waitJump || !PhysicallyPressed(jump)) waitJump = false;
            if (!waitCrouch || !PhysicallyPressed(crouch)) waitCrouch = false;
            if (!waitSprint || !PhysicallyPressed(sprint)) waitSprint = false;
            if (!waitNoclip || !PhysicallyPressed(noclip)) waitNoclip = false;
            if (!CanRead)
            {
                if (Character && !Character.InputBlocked) { ReleaseOwnedButtons(); ClearOwnedMovement(); }
                return;
            }
            Vector2 axes = move.ReadValue<Vector2>();
            Vector2 degrees = Player.ReadLookDelta(look, Time.deltaTime);
            Character.AddControlYawInput(degrees.x); Character.AddControlPitchInput(degrees.y, MinimumPitch, MaximumPitch);
            Transform body = Character.transform;
            if (Character.IsNoclipActive)
            {
                Transform aim = Character.LookPivot ? Character.LookPivot : body;
                float vertical = (!waitJump && jump?.IsPressed() == true ? 1 : 0) - (!waitCrouch && crouch?.IsPressed() == true ? 1 : 0);
                lastMovement = body.right * axes.x + aim.forward * axes.y + body.up * vertical;
            }
            else lastMovement = body.TransformDirection(new Vector3(axes.x, 0, axes.y));
            Character.SetMovementDirection(lastMovement); wroteMovement = true;
        }
        private void OnDisable()
        {
            if (!connected) return;
            if (jump != null) { jump.performed -= Jump; jump.canceled -= StopJump; }
            if (crouch != null) { crouch.performed -= Crouch; crouch.canceled -= StopCrouch; }
            if (sprint != null) { sprint.performed -= Sprint; sprint.canceled -= StopSprint; }
            if (noclip != null) { noclip.performed -= Noclip; noclip.canceled -= StopNoclip; }
            if (Character) Character.InputBlockedChanged -= BlockChanged;
            if (Player) { Player.SettingsChanged -= ApplySettings; Player.ProfileChanged -= ApplySettings; }
            if (Character && !Character.InputBlocked) { ReleaseOwnedButtons(); ClearOwnedMovement(); }
            holdingJump = holdingCrouch = holdingSprint = wroteMovement = false;
            if (standalone) { standalone.sensitivity = oldSensitivity; standalone.gamepadLookSpeed = oldSpeed; standalone.invertLook = oldInvert; standalone.enabled = standaloneEnabled; }
            connected = false;
        }
    }
}
