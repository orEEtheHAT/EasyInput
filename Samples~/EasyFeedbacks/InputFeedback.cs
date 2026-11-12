using EasyFeedbacks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput.Integrations
{
    [AddComponentMenu("EasyInput/Integrations/Input Feedback")]
    public sealed class InputFeedback : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public InputActionReference Action;
        public string ActionName = "Gameplay/Interact";
        public FeedbackPreset Preset;
        [Tooltip("Optional IFeedbackContextProvider; PlayerFeedbackContext preserves an explicit target gamepad.")]
        public MonoBehaviour ContextProvider;
        private InputAction action;
        private void OnEnable()
        {
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player) { Debug.LogError("Input Feedback: assign Player Input Settings.", this); return; }
            action = Action ? Player.Resolve(Action) : Player.Action(ActionName);
            if (action != null) action.performed += Play;
        }
        private void OnDisable() { if (action != null) action.performed -= Play; action = null; }
        private void Play(InputAction.CallbackContext _) => PlayFeedback();
        public void PlayFeedback()
        {
            if (!Preset || !Player) return;
            var context = FeedbackContext.From(transform);
            if (ContextProvider is IFeedbackContextProvider provider && ContextProvider.isActiveAndEnabled) context = provider.Prepare(context);
            else context = context.WithDefaultGamepad(Player.ActiveGamepad);
            FeedbackService.Play(Preset, context);
        }
    }
}
