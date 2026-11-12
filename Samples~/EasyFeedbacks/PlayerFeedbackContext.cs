using EasyFeedbacks;
using UnityEngine;

namespace EasyInput.Integrations
{
    [DisallowMultipleComponent, AddComponentMenu("EasyInput/Integrations/Player Feedback Context")]
    public sealed class PlayerFeedbackContext : MonoBehaviour, IFeedbackContextProvider
    {
        public PlayerInputSettings Player;
        private void Reset() => Player = GetComponentInParent<PlayerInputSettings>();
        private void Awake() { if (!Player) Player = GetComponentInParent<PlayerInputSettings>(); }
        public FeedbackContext Prepare(FeedbackContext context) => context.WithDefaultGamepad(Player ? Player.ActiveGamepad : null);
    }
}
