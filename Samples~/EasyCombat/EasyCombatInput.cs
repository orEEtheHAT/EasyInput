using EasyCombat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput.Integrations
{
    [AddComponentMenu("EasyInput/Integrations/EasyCombat Input")]
    public sealed class EasyCombatInput : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public DamageEmitter Emitter;
        public Transform Aim;
        public bool Automatic = true;
        private InputAction fire;
        private void OnEnable()
        {
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Emitter) Emitter = GetComponent<DamageEmitter>();
            if (!Player || !Emitter) { Debug.LogError("EasyCombat Input: assign Player Input Settings and Damage Emitter.", this); enabled = false; return; }
            fire = Player.Action("Gameplay/Fire");
        }
        private void Update()
        {
            if (fire == null || !Emitter || (Automatic ? !fire.IsPressed() : !fire.WasPressedThisFrame())) return;
            var aim = Aim ? Aim : transform; Emitter.TryFire(aim.position, aim.forward);
        }
    }
}
