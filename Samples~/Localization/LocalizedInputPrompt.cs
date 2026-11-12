using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization;

namespace EasyInput.Integrations
{
    [AddComponentMenu("EasyInput/Integrations/Localized Input Prompt")]
    public sealed class LocalizedInputPrompt : MonoBehaviour
    {
        public PlayerInputSettings Player;
        public InputActionReference Action;
        public string ActionName = "Gameplay/Interact";
        [Tooltip("Use a normal localized string with a literal {button} placeholder; disable Smart on that entry.")]
        public LocalizedString Text = new LocalizedString();
        public string Fallback = "Interact [{button}]";
        public UnityEvent<string> TextChanged = new UnityEvent<string>();
        public string CurrentText { get; private set; }
        private CancellationTokenSource pending;
        private PlayerInputSettings subscribed;
        private int revision;
        private void OnEnable()
        {
            if (!Player) Player = GetComponentInParent<PlayerInputSettings>();
            subscribed = Player;
            if (subscribed) subscribed.PresentationChanged += Refresh;
            EasyLocalization.Localization.Changed += Refresh;
            Text.StringChanged += OnLocalizedText;
            Refresh();
        }
        private void OnDisable()
        {
            ++revision; pending?.Cancel(); pending?.Dispose(); pending = null;
            if (subscribed) subscribed.PresentationChanged -= Refresh; subscribed = null;
            EasyLocalization.Localization.Changed -= Refresh; Text.StringChanged -= OnLocalizedText;
        }
        private void OnLocalizedText(string _) => Refresh();
        public async void SetLanguage(string code)
        { try { await EasyLocalization.Localization.SetLanguageAsync(code); } catch (OperationCanceledException) { } catch (Exception exception) { Debug.LogException(exception, this); } }
        public async void Refresh()
        {
            if (!Player || !isActiveAndEnabled) return;
            int request = ++revision; pending?.Cancel(); pending?.Dispose(); pending = new CancellationTokenSource();
            var token = pending.Token;
            var presentation = Action ? Player.GetPresentation(Action) : Player.GetPresentation(ActionName);
            CurrentText = Fallback.Replace("{button}", presentation.Text); TextChanged.Invoke(CurrentText);
            try
            {
                string format = await EasyLocalization.Localization.TextAsync(Text, fallback: Fallback, cancellation: token);
                if (request != revision || !this || !isActiveAndEnabled) return;
                CurrentText = format.Replace("{button}", presentation.Text); TextChanged.Invoke(CurrentText);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { if (request == revision) Debug.LogWarning("Localized Input Prompt kept its fallback: " + exception.Message, this); }
        }
    }
}
