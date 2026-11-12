using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EasyInput
{
    /// <summary>Optional source reference or map/action name, always resolved into the player's runtime asset.</summary>
    [Serializable]
    public sealed class InputActionLink
    {
        [Tooltip("Optional source action. Its stable ID resolves into Player.Actions; it is never enabled or cloned here.")]
        public InputActionReference Reference;
        [Tooltip("Used when Reference is empty. Empty disables this optional action.")]
        public string Name;
        public InputActionLink() { }
        public InputActionLink(string name) => Name = name;
        public InputAction Resolve(PlayerInputSettings player, bool required = false)
        {
            if (!player) throw new ArgumentNullException(nameof(player));
            var action = Reference ? player.Resolve(Reference) : string.IsNullOrWhiteSpace(Name) ? null : player.Actions.FindAction(Name, false);
            if (required && action == null)
                throw new ArgumentException("EasyInput: assign an action from this player's source asset or a valid map/action name. Missing: " + (Reference ? Reference.name : Name));
            return action;
        }
    }
}
