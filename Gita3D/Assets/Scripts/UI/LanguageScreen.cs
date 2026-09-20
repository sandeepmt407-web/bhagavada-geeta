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
            var header = UIKit.Node("Header", Root).TopBand(0f, 180f);
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
            viewport.Inset(0f, 180f, 0f, 150f);
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

            SpeedRow();

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
                Theme.Sans, 21f, Theme.Muted, TextAlignmentOptions.TopLeft);
            t.lineSpacing = 8f;
            _rows.Add(t.gameObject);
        }

        void Heading(string text)
        {
            var host = UIKit.Node("HeadingHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = 62f;
            el.minHeight = 62f;

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
            el.preferredHeight = twoLine ? 116f : 86f;
            el.minHeight = el.preferredHeight;

            var btn = UIKit.Tappable("Tap", host, onTap,
                selected ? Theme.Saffron.WithAlpha(0.20f) : Theme.TwilightLit.WithAlpha(0.55f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            bool devanagari = ContainsDevanagari(title);
            var label = UIKit.Text("Label", btn.transform, title,
                devanagari ? Theme.DevanagariUI : Theme.Sans,
                25f, selected ? Theme.SaffronLit : Theme.Cream,
                TextAlignmentOptions.Left);
            label.rectTransform.Inset(28f, twoLine ? 14f : 0f, 96f, twoLine ? 46f : 0f);

            if (twoLine)
            {
                var sub = UIKit.Text("Sub", btn.transform, subtitle,
                    Theme.Sans, 19f, Theme.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Inset(28f, 62f, 96f, 10f);
            }

            if (selected)
            {
                var tick = UIKit.Text("Tick", btn.transform, "✓",
                    Theme.SansBold, 32f, Theme.Saffron, TextAlignmentOptions.Center);
                var rt = tick.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64f, 64f);
                rt.anchoredPosition = new Vector2(-46f, 0f);
            }

            _rows.Add(host.gameObject);
        }

        /// <summary>Narration speed, as three discrete choices rather than a slider.</summary>
        void SpeedRow()
        {
            var host = UIKit.Node("SpeedHost", _content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = 112f;
            el.minHeight = 112f;

            var label = UIKit.Text("Label", host, "READING PACE",
                Theme.SansBold, 19f, Theme.Muted, TextAlignmentOptions.TopLeft);
            label.rectTransform.TopBand(0f, 28f, 4f);
            label.characterSpacing = 6f;

            var options = new (string name, float rate)[]
            {
                ("Slow", 0.7f), ("Natural", 0.85f), ("Brisk", 1.05f)
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
                cell.sizeDelta = new Vector2(cell.sizeDelta.x, 66f);

                float captured = rate;
                var btn = UIKit.Tappable("Tap", cell, () =>
                    {
                        AppSettings.SpeechRate = captured;
                        Rebuild();
                    },
                    on ? Theme.Saffron.WithAlpha(0.92f) : Theme.TwilightLit.WithAlpha(0.55f));
                btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

                var t = UIKit.Text("Label", btn.transform, name,
                    Theme.Sans, 21f, on ? Theme.NightDeep : Theme.Parchment,
                    TextAlignmentOptions.Center);
                t.rectTransform.Inset(0f, 0f, 0f, 0f);
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
