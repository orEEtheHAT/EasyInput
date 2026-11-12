using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace EasyInput.Integrations
{
    [RequireComponent(typeof(InputSystemUIInputModule)), AddComponentMenu("EasyInput/Integrations/uGUI Actions")]
    public sealed class InputUIActions : MonoBehaviour
    {
        public PlayerInputSettings Player;
        private InputSystemUIInputModule module;
        private bool wasEnabled;
        private readonly List<InputActionReference> ownedReferences = new List<InputActionReference>();
        private InputActionReference point, click, scroll, move, submit, cancel;
        private InputActionAsset asset;
        private InputActionReference Link(string path)
        {
            var reference = InputActionReference.Create(Player.Action(path)); ownedReferences.Add(reference); return reference;
        }
        private void OnEnable()
        {
            module = GetComponent<InputSystemUIInputModule>();
            if (!Player) { Debug.LogError("uGUI Actions: assign Player Input Settings from the local player.", this); enabled = false; return; }
            wasEnabled = module.enabled; module.enabled = false;
            asset = module.actionsAsset; point = module.point; click = module.leftClick; scroll = module.scrollWheel;
            move = module.move; submit = module.submit; cancel = module.cancel;
            module.actionsAsset = Player.Actions;
            module.point = Link("UI/Point"); module.leftClick = Link("UI/Click"); module.scrollWheel = Link("UI/Scroll");
            module.move = Link("UI/Navigate"); module.submit = Link("UI/Submit"); module.cancel = Link("UI/Cancel");
            Player.RebindChanged += Refresh; Refresh();
        }
        private void Refresh() { if (module) module.enabled = wasEnabled && Player.Rebind?.IsActive != true; }
        private void OnDisable()
        {
            if (Player) Player.RebindChanged -= Refresh;
            if (module)
            {
                module.enabled = false; module.actionsAsset = asset;
                module.point = point; module.leftClick = click; module.scrollWheel = scroll; module.move = move; module.submit = submit; module.cancel = cancel;
                module.enabled = wasEnabled;
            }
            foreach (var reference in ownedReferences) { if (Application.isPlaying) Destroy(reference); else DestroyImmediate(reference); }
            ownedReferences.Clear();
        }
    }
}
