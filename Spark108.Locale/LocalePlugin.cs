using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using System.Runtime.Serialization;
using System.Xml;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spark108.Locale
{
    [BepInPlugin("ru.spark108.locale", "Spark108.Locale", "0.2.0")]
    public sealed class LocalePlugin : BaseUnityPlugin
    {
        private Translation translation;
        private TMP_FontAsset fallbackFont;
        private float nextScan;

        private void Awake()
        {
            try
            {
                translation = Translation.Load(Path.GetDirectoryName(Info.Location));
                Logger.LogInfo($"Loaded {translation.Language} with {translation.Count} translation entries.");
            }
            catch (Exception error) when (error is IOException || error is SerializationException ||
                error is ArgumentException || error is UnauthorizedAccessException || error is XmlException)
            {
                Logger.LogError($"Could not load the locale: {error.Message}");
                enabled = false;
            }
        }

        private bool InitializeFont()
        {
            if (!string.IsNullOrWhiteSpace(translation.FontFamily))
            {
                fallbackFont = TMP_FontAsset.CreateFontAsset(translation.FontFamily, translation.FontStyle, 48);
                if (fallbackFont)
                    return true;

                Logger.LogWarning($"Font {translation.FontFamily} ({translation.FontStyle}) is unavailable. Using Liberation Sans.");
            }

            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (font.name != "LiberationSans")
                    continue;

                fallbackFont = TMP_FontAsset.CreateFontAsset(font);
                return fallbackFont;
            }

            return false;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScan)
                return;

            nextScan = Time.unscaledTime + 0.5f;
            if (!fallbackFont && !InitializeFont())
                return;

            foreach (var text in Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (text.textPreprocessor is LocalizedText)
                    continue;

                var input = text.GetComponentInParent<TMP_InputField>(true);
                if (input && input.textComponent == text)
                    continue;

                text.textPreprocessor = new LocalizedText(text, fallbackFont, translation);
                text.havePropertiesChanged = true;
                text.SetVerticesDirty();
                text.SetLayoutDirty();
            }
        }

        private sealed class LocalizedText : ITextPreprocessor
        {
            private readonly TMP_Text text;
            private readonly TMP_FontAsset fallbackFont;
            private readonly Translation translation;
            private readonly ITextPreprocessor previous;

            public LocalizedText(TMP_Text text, TMP_FontAsset fallbackFont, Translation translation)
            {
                this.text = text;
                this.fallbackFont = fallbackFont;
                this.translation = translation;
                previous = text.textPreprocessor;
            }

            public string PreprocessText(string source)
            {
                source = previous?.PreprocessText(source) ?? source;
                var translated = translation.Get(source);
                if (translated == source || !text.font || text.font == fallbackFont)
                    return translated;

                var fallbacks = text.font.fallbackFontAssetTable;
                if (fallbacks == null)
                {
                    fallbacks = new List<TMP_FontAsset>();
                    text.font.fallbackFontAssetTable = fallbacks;
                }

                if (!fallbacks.Contains(fallbackFont))
                    fallbacks.Add(fallbackFont);
                return translated;
            }
        }
    }
}
