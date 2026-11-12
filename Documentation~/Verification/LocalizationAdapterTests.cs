using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using EasyInput.Integrations;
using Object = UnityEngine.Object;

namespace EasyInput.IntegrationTests
{
    public sealed class LocalizationAdapterTests
    {
        private LocalizationSettings previous;
        private EasyLocalization.LocalizationConfig previousConfig;
        private readonly List<Object> created = new List<Object>();
        private GameObject root;
        [SetUp] public void Setup()
        {
            previous = LocalizationSettings.Instance;
            previousConfig = previous ? EasyLocalization.Localization.Configuration : null;
            EasyLocalization.Localization.Reset();
        }
        [UnityTearDown] public IEnumerator Teardown()
        {
            if (root) Object.DestroyImmediate(root);
            EasyLocalization.Localization.Reset();
            LocalizationSettings.Instance = previous;
            foreach (var asset in created) if (asset) Object.DestroyImmediate(asset);
            created.Clear();
            // Restore the same explicit config, without leaving callbacks bound to test settings.
            if (previous && previous.GetAvailableLocales().Locales.Count > 0)
                yield return Wait(EasyLocalization.Localization.ReinitializeAsync(previousConfig));
        }
        private sealed class TestLocales : ILocalesProvider
        {
            public List<Locale> Locales { get; } = new List<Locale>();
            public Locale GetLocale(LocaleIdentifier id) => Locales.FirstOrDefault(x => x.Identifier == id);
            public void AddLocale(Locale locale) => Locales.Add(locale);
            public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
        }
        private sealed class TestTables : ITableProvider
        {
            public readonly Dictionary<string, StringTable> Values = new Dictionary<string, StringTable>();
            public AsyncOperationHandle<T> ProvideTableAsync<T>(string collection, Locale locale) where T : LocalizationTable =>
                Values.TryGetValue(locale.Identifier.Code, out var value) ? Addressables.ResourceManager.CreateCompletedOperation(value as T, (string)null) : default;
        }
        private static IEnumerator Wait(Task task)
        {
            float end = Time.realtimeSinceStartup + 15;
            while (!task.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
            Assert.That(task.IsCompleted, Is.True); if (task.IsFaulted) throw task.Exception.GetBaseException(); Assert.That(task.IsCanceled, Is.False);
        }
        [UnityTest] public IEnumerator NativeLocalizedStringTracksLanguageRebindAndDisable()
        {
                var settings = ScriptableObject.CreateInstance<LocalizationSettings>(); created.Add(settings);
                var locales = new TestLocales(); var tables = new TestTables();
                foreach (string code in new[] { "en", "ru" })
                {
                    var locale = Locale.CreateLocale(code); created.Add(locale); locales.AddLocale(locale);
                    var shared = ScriptableObject.CreateInstance<SharedTableData>(); created.Add(shared); shared.TableCollectionName = "EasyInput Test"; shared.AddKey("use", 1001);
                    var table = ScriptableObject.CreateInstance<StringTable>(); created.Add(table); table.SharedData = shared; table.LocaleIdentifier = code;
                    table.AddEntry(1001, code == "en" ? "Use [{button}]" : "Использовать [{button}]"); tables.Values.Add(code, table);
                }
                settings.SetAvailableLocales(locales); settings.SetStringDatabase(new LocalizedStringDatabase { TableProvider = tables }); LocalizationSettings.Instance = settings;
                LocalizationSettings.StartupLocaleSelectors.Clear(); LocalizationSettings.StartupLocaleSelectors.Add(new SpecificLocaleSelector { LocaleId = "en" });
                var config = ScriptableObject.CreateInstance<EasyLocalization.LocalizationConfig>(); created.Add(config); config.fallbackLanguage = "en";
                yield return Wait(EasyLocalization.Localization.ReinitializeAsync(config)); yield return Wait(EasyLocalization.Localization.SetLanguageAsync("en"));
                root = new GameObject("Localized input"); root.SetActive(false); var player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false;
                var prompt = root.AddComponent<LocalizedInputPrompt>(); prompt.Player = player; prompt.Text = new LocalizedString("EasyInput Test", 1001);
                root.SetActive(true); yield return null; yield return null;
                Assert.That(prompt.CurrentText, Does.StartWith("Use ["));
                Assert.That(player.TrySetBinding(player.Action("Gameplay/Interact"), 0, "<Keyboard>/k", out _), Is.True); yield return null;
                Assert.That(prompt.CurrentText, Does.Contain("K").IgnoreCase);
                yield return Wait(EasyLocalization.Localization.SetLanguageAsync("ru")); yield return null;
                Assert.That(prompt.CurrentText, Does.StartWith("Использовать [")); Assert.That(prompt.CurrentText, Does.Contain("K").IgnoreCase);
                prompt.enabled = false; int changes = 0; prompt.TextChanged.AddListener(_ => ++changes);
                player.TrySetBinding(player.Action("Gameplay/Interact"), 0, "<Keyboard>/l", out _);
                yield return Wait(EasyLocalization.Localization.SetLanguageAsync("en")); yield return null; Assert.That(changes, Is.Zero);
        }
    }
}
