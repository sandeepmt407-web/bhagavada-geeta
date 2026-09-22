using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gita.App;
using Gita.Data;
using Gita.World;

namespace Gita.UI
{
    /// <summary>
    /// Choice of language, translator and narration.
    ///
    /// Only the translation changes. The Sanskrit and its transliteration are the text
    /// itself and stay exactly as they are in every language.
    /// </summary>
    public sealed class LanguageScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Contemplation;

        RectTransform _content;
        readonly List<GameObject> _rows = new();

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            var scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.90f), UIKit.Solid);
            scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            BuildHeader();
            BuildScroll();
            BuildDoneButton();
        }

        void BuildHeader()
        {
            var header = UIKit.Node("Header", Root).TopBand(0f, 194f);
            var bg = UIKit.Panel("Bg", header, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", header, () => AppRoot.Instance?.GoBack());
            var rt = back.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 84f);
            rt.anchoredPosition = new Vector2(Theme.Gutter + 55f, -100f);
            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 44f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            var title = UIKit.Text("Title", header, "Language & Narration",
                Theme.Serif, Theme.SizeHeading, Theme.Cream, TextAlignmentOptions.Center);
            title.rectTransform.TopBand(72f, 56f, Theme.Gutter + 130f);

            var rule = UIKit.Panel("Rule", header, Theme.Saffron.WithAlpha(0.35f), UIKit.Solid);
            rule.rectTransform.BottomBand(0f, 2f);
        }

        void BuildScroll()
        {
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, 194f, 0f, 162f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            _content = UIKit.Node("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = Vector2.zero;
            _content.offsetMax = Vector2.zero;

            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 32, 80);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 28f;
        }

        void BuildDoneButton()
        {
            var host = UIKit.Node("Done", Root).BottomBand(24f, 104f, Theme.Gutter);
            var btn = UIKit.Tappable("Btn", host, () => AppRoot.Instance?.GoBack(), Theme.Saffron);
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);
            var label = UIKit.Text("Label", btn.transform, "DONE",
                Theme.SansBold, Theme.SizeLabel, Theme.NightDeep, TextAlignmentOptions.Center);
            label.rectTransform.Inset(0f, 0f, 0f, 0f);
            label.characterSpacing = 8f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.Fit(0.5f);
        }

        public override void OnShow() => Rebuild();

        /// <summary>Leaving this screen at all counts as having chosen.</summary>
        public override void OnHidden() => AppSettings.MarkLanguageChosen();

        // ------------------------------------------------------------------

        void Rebuild()
        {
            foreach (var go in _rows) if (go != null) Destroy(go);
            _rows.Clear();

            Caption("The Sanskrit verse never changes. This chooses the translation "
                    + "you read and the voice that reads it.");

            Heading("TEXT SIZE");
            TextSizeRow();
            TextSizeSample();

            Heading("LANGUAGE");
            foreach (var (lang, label) in GitaDatabase.Languages())
            {
                bool selected = AppSettings.Language == lang;
                string captured = lang;
                Row(label, Subtitle(lang), selected, () => SelectLanguage(captured));
            }

            Heading("TRANSLATION BY");
            foreach (int index in GitaDatabase.EditionsFor(AppSettings.Language))
            {
                var edition = GitaDatabase.EditionAt(index);
                if (edition == null) continue;
                bool selected = AppSettings.Edition == index;
                int captured = index;
                Row(edition.translator, null, selected, () =>
                {
                    AppSettings.Edition = captured;
                    Narration.Stop();
                    Rebuild();
                });
            }

            Heading("NARRATION");
            Row(AppSettings.AudioEnabled ? "Read verses aloud" : "Narration off",
                NarrationHint(),
                AppSettings.AudioEnabled,
                () =>
                {
                    AppSettings.AudioEnabled = !AppSettings.AudioEnabled;
                    if (!AppSettings.AudioEnabled) Narration.Stop();
                    Rebuild();
                });

            VoiceRows();
            SpeedRow();
            VoiceNote();

            Heading("BACKGROUND MUSIC");
            Row(AppSettings.MusicEnabled ? "Bansuri under the reading" : "Background music is off",
                AppSettings.MusicEnabled
                    ? "Steps out of the way while a verse is being read"
                    : null,
                AppSettings.MusicEnabled,
                () =>
                {
                    AppSettings.MusicEnabled = !AppSettings.MusicEnabled;
                    Rebuild();
                });

            Heading("DAILY VERSE");
            Row(DailyVerse.Enabled ? "A verse each morning" : "Daily verse is off",
                DailyVerse.Enabled ? null : "Turn this on to be sent one verse a day",
                DailyVerse.Enabled,
                () =>
                {
                    DailyVerse.Enabled = !DailyVerse.Enabled;
                    Rebuild();
                });

            if (DailyVerse.Enabled) HourRow();

            // Only where the law gives the reader a standing choice about advertising
            // data - the EEA, the UK and Switzerland. Everywhere else there is nothing to
            // choose, so no row.
            if (Ads.PrivacyOptionsRequired)
            {
                Heading("PRIVACY");
                Row("Privacy choices", "What adverts may do with your data", false,
                    () => Ads.Instance?.ShowPrivacyOptions(Rebuild));
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void SelectLanguage(string lang)
        {
            var list = GitaDatabase.EditionsFor(lang);
            if (list.Count == 0) return;
            AppSettings.Edition = list[0];
            Narration.Stop();
            Rebuild();
        }

        static string Subtitle(string lang)
        {
            int count = GitaDatabase.EditionsFor(lang).Count;
            return count == 1 ? "1 translation" : $"{count} translations";
        }

        /// <summary>Tells the reader when the device has no voice for this language.</summary>
        static string NarrationHint()
        {
            if (!AppSettings.AudioEnabled) return null;

            var edition = AppSettings.Current;
            if (edition == null) return null;

            if (!Narration.Available)
                return "No speech engine on this device";

            return Narration.StatusOf(edition.tts) switch
            {
                Narration.VoiceStatus.Ready => null,
                Narration.VoiceStatus.NeedsDownload =>
                    "Voice data for this language is not installed - add it in Android settings",
                _ => "This device has no voice for this language",
            };
        }

        // ---- row builders ---------------------------------------------------

        void Caption(string text)
        {
            var t = UIKit.Text("Caption", _content, text,
                Theme.Sans, 25f, Theme.Muted, TextAlignmentOptions.TopLeft);
            t.lineSpacing = 8f;
            _rows.Add(t.gameObject);
        }

        void Heading(string text)
        {
            var host = UIKit.Node("HeadingHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(62f);
            el.minHeight = el.preferredHeight;

            var t = UIKit.Text("Heading", host, text,
                Theme.SansBold, Theme.SizeCaption, Theme.Saffron, TextAlignmentOptions.BottomLeft);
            t.rectTransform.Inset(0f, 0f, 0f, 8f);
            t.characterSpacing = 8f;
            _rows.Add(host.gameObject);
        }

        void Row(string title, string subtitle, bool selected, System.Action onTap)
        {
            bool twoLine = !string.IsNullOrEmpty(subtitle);

            var host = UIKit.Node("Row", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(twoLine ? 134f : 98f);
            el.minHeight = el.preferredHeight;

            var btn = UIKit.Tappable("Tap", host, onTap,
                selected ? Theme.Saffron.WithAlpha(0.20f) : Theme.TwilightLit.WithAlpha(0.55f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            bool devanagari = ContainsDevanagari(title);
            var label = UIKit.Text("Label", btn.transform, title,
                devanagari ? Theme.DevanagariUI : Theme.Sans,
                28f, selected ? Theme.SaffronLit : Theme.Cream,
                TextAlignmentOptions.Left);
            label.rectTransform.Inset(28f, twoLine ? Theme.Scaled(16f) : 0f, 96f,
                twoLine ? Theme.Scaled(54f) : 0f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.Fit(0.55f);

            if (twoLine)
            {
                var sub = UIKit.Text("Sub", btn.transform, subtitle,
                    Theme.Sans, 22f, Theme.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Inset(28f, Theme.Scaled(70f), 96f, 10f);
                sub.textWrappingMode = TextWrappingModes.NoWrap;
                sub.overflowMode = TextOverflowModes.Ellipsis;
                sub.Fit(0.55f);
            }

            if (selected)
            {
                var tick = UIKit.Text("Tick", btn.transform, "✓",
                    Theme.SansBold, 36f, Theme.Saffron, TextAlignmentOptions.Center);
                var rt = tick.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64f, 64f);
                rt.anchoredPosition = new Vector2(-46f, 0f);
            }

            _rows.Add(host.gameObject);
        }


        /// <summary>
        /// Text size, with a line of real verse set at the chosen size underneath it.
        /// A name like "Large" means nothing on its own; seeing the words at that size
        /// is the only way to judge it, and the choice is made straight away rather
        /// than after leaving the screen and finding out.
        /// </summary>
        void TextSizeRow()
        {
            var host = UIKit.Node("TextSizeHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(96f);
            el.minHeight = el.preferredHeight;

            var options = AppSettings.TextSizes;
            int chosen = AppSettings.TextSizeStep;

            for (int i = 0; i < options.Length; i++)
            {
                var (name, scale) = options[i];
                bool on = i == chosen;

                var cell = UIKit.Node($"Size{i}", host);
                cell.anchorMin = new Vector2(i / (float)options.Length, 0f);
                cell.anchorMax = new Vector2((i + 1) / (float)options.Length, 0f);
                cell.pivot = new Vector2(0.5f, 0f);
                cell.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
                cell.offsetMax = new Vector2(i == options.Length - 1 ? 0f : -6f, 0f);
                cell.sizeDelta = new Vector2(cell.sizeDelta.x, Theme.Scaled(80f));

                float captured = scale;
                var btn = UIKit.Tappable("Tap", cell, () =>
                    {
                        AppSettings.TextScale = captured;
                        Rebuild();
                    },
                    on ? Theme.Saffron.WithAlpha(0.92f) : Theme.TwilightLit.WithAlpha(0.55f));
                btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

                // The letter is drawn at that step's own size, so the row itself is the
                // sample: the choices are visibly different heights.
                var letter = UIKit.Text("A", btn.transform, "A",
                    Theme.Serif, 22f + i * 7f, on ? Theme.NightDeep : Theme.Cream,
                    TextAlignmentOptions.Center, scalable: false);
                letter.rectTransform.Inset(0f, 0f, 0f, 22f);

                var t = UIKit.Text("Label", btn.transform, name.ToUpperInvariant(),
                    Theme.SansBold, 15f, on ? Theme.NightDeep : Theme.Muted,
                    TextAlignmentOptions.Bottom, scalable: false);
                t.rectTransform.Inset(0f, 0f, 0f, 8f);
                t.characterSpacing = 3f;
            }

            _rows.Add(host.gameObject);
        }

        /// <summary>A real line of the text, set at whatever size is currently chosen.</summary>
        void TextSizeSample()
        {
            var t = UIKit.Text("Sample", _content,
                "You have a right to your actions, but never to the fruit of them.",
                Theme.Serif, Theme.SizeBody, Theme.Parchment, TextAlignmentOptions.TopLeft);
            t.lineSpacing = 10f;
            _rows.Add(t.gameObject);
        }
        /// <summary>Narration speed, as three discrete choices rather than a slider.</summary>
        void SpeedRow()
        {
            var host = UIKit.Node("SpeedHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(128f);
            el.minHeight = el.preferredHeight;

            var label = UIKit.Text("Label", host, "READING PACE",
                Theme.SansBold, 22f, Theme.Muted, TextAlignmentOptions.TopLeft);
            label.rectTransform.TopBand(0f, 32f, 4f);
            label.characterSpacing = 6f;

            var options = new (string name, float rate)[]
            {
                ("Slow", 0.72f), ("Natural", 0.88f), ("Brisk", 1.05f)
            };

            for (int i = 0; i < options.Length; i++)
            {
                var (name, rate) = options[i];
                bool on = Mathf.Abs(AppSettings.SpeechRate - rate) < 0.02f;

                var cell = UIKit.Node($"Speed{i}", host);
                cell.anchorMin = new Vector2(i / 3f, 0f);
                cell.anchorMax = new Vector2((i + 1) / 3f, 0f);
                cell.pivot = new Vector2(0.5f, 0f);
                cell.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
                cell.offsetMax = new Vector2(i == 2 ? 0f : -6f, 0f);
                cell.sizeDelta = new Vector2(cell.sizeDelta.x, Theme.Scaled(76f));

                float captured = rate;
                var btn = UIKit.Tappable("Tap", cell, () =>
                    {
                        AppSettings.SpeechRate = captured;
                        Rebuild();
                    },
                    on ? Theme.Saffron.WithAlpha(0.92f) : Theme.TwilightLit.WithAlpha(0.55f));
                btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

                var t = UIKit.Text("Label", btn.transform, name,
                    Theme.Sans, 24f, on ? Theme.NightDeep : Theme.Parchment,
                    TextAlignmentOptions.Center);
                t.rectTransform.Inset(0f, 0f, 0f, 0f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.Fit(0.5f);
            }

            _rows.Add(host.gameObject);
        }


        /// <summary>
        /// Where the voice comes from, and what to do about it.
        ///
        /// The app does not ship recordings - 701 verses in several languages is a
        /// studio project - so it speaks through whatever engine the phone has. It now
        /// asks for the best voice that engine offers instead of the default one, but
        /// the ceiling is still the device's, and the reader deserves to know that
        /// rather than concluding the app simply sounds bad.
        /// </summary>
        void VoiceNote()
        {
            if (!AppSettings.AudioEnabled) return;

            string voice = Narration.VoiceName;
            string text = "The voice comes from your phone's speech engine, not from the app. "
                        + "Installing Google Speech Services, and choosing a high-quality "
                        + "voice in Android's language settings, makes a large difference.";
            if (!string.IsNullOrEmpty(voice)) text += $"\nCurrently using: {voice}";

            Caption(text);
        }

        /// <summary>
        /// Every Indian voice this device can actually speak with, listed so the reader
        /// can pick one and hear it.
        ///
        /// No two phones carry the same set - it depends on the engine, the Android
        /// version and which voice data the owner installed - so the list is read off
        /// the device rather than guessed at. Tapping a voice both selects it and speaks
        /// a line in it, because a name like "Voice 3, high quality" tells nobody
        /// anything until they have heard it.
        /// </summary>
        void VoiceRows()
        {
            if (!AppSettings.AudioEnabled) return;

            var edition = AppSettings.Current;
            if (edition == null) return;

            var voices = Narration.Voices(edition.tts);
            if (voices.Count == 0)
            {
                if (Narration.Available)
                    Caption("This device lists no voice for this language. "
                            + "Installing Google Speech Services usually adds several.");
                return;
            }

            Heading("VOICE");

            string chosen = AppSettings.VoiceFor(edition.lang);
            bool anyChosen = !string.IsNullOrEmpty(chosen);

            // "Best available" stays first and is the default: a reader who does not
            // want to audition six voices should not have to.
            Row("Best available", "Chosen automatically for this language", !anyChosen, () =>
            {
                AppSettings.SetVoiceFor(edition.lang, "");
                Narration.Speak(SampleLine(edition.lang), edition.tts, AppSettings.SpeechRate);
                Rebuild();
            });

            for (int i = 0; i < voices.Count; i++)
            {
                var voice = voices[i];
                Row(voice.Describe(i), voice.Detail, voice.Name == chosen, () =>
                {
                    AppSettings.SetVoiceFor(edition.lang, voice.Name);
                    Narration.Speak(SampleLine(edition.lang), edition.tts,
                        AppSettings.SpeechRate, voiceName: voice.Name);
                    Rebuild();
                });
            }
        }

        /// <summary>A line of the text itself to audition a voice on, not a test phrase.</summary>
        static string SampleLine(string lang) => lang switch
        {
            "hi" => "तुम्हारा अधिकार केवल कर्म करने में है, उसके फलों में कभी नहीं।",
            "fr" => "Ton droit est à l'action seule, jamais à ses fruits.",
            _ => "You have a right to your actions, but never to the fruit of them.",
        };
        /// <summary>When the daily verse arrives. Four sensible hours rather than a clock.</summary>
        void HourRow()
        {
            var host = UIKit.Node("HourHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(128f);
            el.minHeight = el.preferredHeight;

            var label = UIKit.Text("Label", host, "ARRIVES AT",
                Theme.SansBold, 22f, Theme.Muted, TextAlignmentOptions.TopLeft);
            label.rectTransform.TopBand(0f, 32f, 4f);
            label.characterSpacing = 6f;

            var options = new (string name, int hour)[]
            {
                ("5 am", 5), ("6 am", 6), ("7 am", 7), ("9 pm", 21),
            };

            for (int i = 0; i < options.Length; i++)
            {
                var (name, hour) = options[i];
                bool on = DailyVerse.Hour == hour;

                var cell = UIKit.Node($"Hour{i}", host);
                cell.anchorMin = new Vector2(i / (float)options.Length, 0f);
                cell.anchorMax = new Vector2((i + 1) / (float)options.Length, 0f);
                cell.pivot = new Vector2(0.5f, 0f);
                cell.offsetMin = new Vector2(i == 0 ? 0f : 6f, 0f);
                cell.offsetMax = new Vector2(i == options.Length - 1 ? 0f : -6f, 0f);
                cell.sizeDelta = new Vector2(cell.sizeDelta.x, Theme.Scaled(76f));

                int captured = hour;
                var btn = UIKit.Tappable("Tap", cell, () =>
                    {
                        DailyVerse.Hour = captured;
                        Rebuild();
                    },
                    on ? Theme.Saffron.WithAlpha(0.92f) : Theme.TwilightLit.WithAlpha(0.55f));
                btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

                var t = UIKit.Text("Label", btn.transform, name,
                    Theme.Sans, 23f, on ? Theme.NightDeep : Theme.Parchment,
                    TextAlignmentOptions.Center);
                t.rectTransform.Inset(0f, 0f, 0f, 0f);
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.Fit(0.5f);
            }

            _rows.Add(host.gameObject);
        }

        static bool ContainsDevanagari(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (var ch in s)
                if (ch >= 'ऀ' && ch <= 'ॿ') return true;
            return false;
        }
    }
}
