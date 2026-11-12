using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace EasyInput
{
    [AddComponentMenu("EasyInput/Input Prompt")]
    public sealed class InputPrompt : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public InputActionReference Action;
        [Tooltip("Used if Action is empty. Example: Gameplay/Interact.")]
        public string ActionName = "Gameplay/Interact";
        public string Label = "Interact";
        public UnityEvent<string> TextChanged = new UnityEvent<string>();
        public UnityEvent<Sprite> SpriteChanged = new UnityEvent<Sprite>();
        public string Text { get; private set; }
        public InputPresentation Presentation { get; private set; }
        private PlayerInputSettings subscribed;
        private InputAction resolvedAction;
        private void OnEnable()
        {
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            if (!Player) { Debug.LogError("Input Prompt: assign Player Input Settings, or place the prompt under that player.", this); return; }
            subscribed = Player; resolvedAction = Action ? Player.Resolve(Action) : Player.Actions.FindAction(ActionName, false);
            subscribed.PresentationChanged += Refresh; Refresh();
        }
        private void OnDisable() { if (subscribed) subscribed.PresentationChanged -= Refresh; subscribed = null; }
        public void Refresh()
        {
            if (!Player) return;
            if (resolvedAction == null) resolvedAction = Action ? Player.Resolve(Action) : Player.Actions.FindAction(ActionName, false);
            Presentation = Player.GetPresentation(resolvedAction);
            Text = string.IsNullOrEmpty(Label) ? Presentation.Text : Label + " [" + Presentation.Text + "]";
            TextChanged.Invoke(Text); SpriteChanged.Invoke(Presentation.Sprite);
        }
    }
}
