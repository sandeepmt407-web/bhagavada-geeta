using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gita.App;
using Gita.Data;
using Gita.World;

namespace Gita.UI
{
    /// <summary>
    /// The opening view: the title over the battlefield, the verse for today, and the
    /// two ways in - the book, or the chapter index. The language control sits at the
    /// top, where it is the first thing a new reader meets.
    /// </summary>
    public sealed class HomeScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Establishing;

        Verse _today;
        ShapedText _saText;
        TextMeshProUGUI _enText, _refText, _langLabel, _continueLabel;
        GameObject _continueRow;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            // Dark at the bottom, clear at the top, so text stays legible over the
            // bright dawn sky without hiding the scene.
            var scrim = UIKit.Raw("Scrim", Root, Theme.NightDeep.WithAlpha(0.93f), UIKit.VGradient);
            scrim.rectTransform.anchorMin = new Vector2(0f, 0f);
            scrim.rectTransform.anchorMax = new Vector2(1f, 0.74f);
            scrim.rectTransform.offsetMin = Vector2.zero;
            scrim.rectTransform.offsetMax = Vector2.zero;

            BuildLanguagePill();
            BuildTitle();
            BuildVerseCard();
            BuildContinueRow();
            BuildButtons();
        }

        void BuildLanguagePill()
        {
            var host = UIKit.Node("LangPill", Root);
            host.anchorMin = host.anchorMax = new Vector2(1f, 1f);
            host.pivot = new Vector2(1f, 1f);
            host.sizeDelta = new Vector2(340f, 76f);
            host.anchoredPosition = new Vector2(-Theme.Gutter, -26f);

            var btn = UIKit.Tappable("Tap", host,
                () => AppRoot.Instance?.GoTo(ScreenId.Language),
                Theme.NightDeep.WithAlpha(0.72f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            var glyph = UIKit.Text("Glyph", btn.transform, "☷",
                Theme.Sans, 24f, Theme.Saffron, TextAlignmentOptions.Center);
            var grt = glyph.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(0f, 0.5f);
            grt.pivot = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(52f, 52f);
            grt.anchoredPosition = new Vector2(38f, 0f);

            _langLabel = UIKit.Text("Label", btn.transform, "",
                Theme.Sans, 20f, Theme.Cream, TextAlignmentOptions.Left);
            _langLabel.rectTransform.Inset(68f, 0f, 20f, 0f);
            _langLabel.overflowMode = TextOverflowModes.Ellipsis;
        }

        void BuildTitle()
        {
            var block = UIKit.Node("Title", Root).TopBand(top: 130f, height: 300f, inset: Theme.Gutter);

            var sanskrit = ShapedText.Create("Sanskrit", block,
                Theme.Devanagari, NativeText.SerifDevanagari, Theme.SizeTitle,
                Theme.Saffron, NativeText.AlignCenter);
            ((RectTransform)sanskrit.transform).TopBand(0f, 78f);
            sanskrit.SetText("श्रीमद्भगवद्गीता");

            var english = UIKit.Text("English", block, "BHAGAVAD GITA",
                Theme.Serif, Theme.SizeDisplay, Theme.Cream, TextAlignmentOptions.Top);
            english.rectTransform.TopBand(86f, 96f);
            english.characterSpacing = 12f;

            var sub = UIKit.Text("Sub", block, "The Song of the Lord  ·  701 verses  ·  18 chapters",
                Theme.Sans, Theme.SizeCaption, Theme.Muted, TextAlignmentOptions.Top);
            sub.rectTransform.TopBand(196f, 40f);
            sub.characterSpacing = 3f;
        }

        void BuildVerseCard()
        {
            var card = UIKit.Node("VerseCard", Root)
                .BottomBand(bottom: 430f, height: 520f, inset: Theme.Gutter);

            var bg = UIKit.Panel("Bg", card, Theme.Twilight.WithAlpha(0.74f), UIKit.RoundedSoft);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var rule = UIKit.Panel("Rule", card, Theme.Saffron.WithAlpha(0.75f), UIKit.Solid);
            rule.rectTransform.TopBand(0f, 3f, 56f);

            var label = UIKit.Text("Label", card, "VERSE FOR TODAY",
                Theme.SansBold, Theme.SizeCaption, Theme.Saffron, TextAlignmentOptions.Top);
            label.rectTransform.TopBand(32f, 34f, 44f);
            label.characterSpacing = 8f;

            _saText = ShapedText.Create("Sanskrit", card,
                Theme.Devanagari, NativeText.SerifDevanagari, Theme.SizeVerseSa - 6f,
                Theme.Cream, NativeText.AlignCenter, lineSpacing: 1.25f);
            ((RectTransform)_saText.transform).TopBand(84f, 170f, 44f);

            _enText = UIKit.Text("Body", card, "",
                Theme.Serif, Theme.SizeBody - 2f, Theme.Parchment, TextAlignmentOptions.Top);
            _enText.rectTransform.TopBand(262f, 190f, 44f);
            _enText.lineSpacing = 8f;

            _refText = UIKit.Text("Ref", card, "",
                Theme.SansBold, Theme.SizeLabel, Theme.Saffron, TextAlignmentOptions.BottomRight);
            _refText.rectTransform.BottomBand(24f, 38f, 44f);

            var tap = UIKit.Tappable("Open", card, OpenToday);
            tap.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);
            tap.image.sprite = UIKit.RoundedSoft;
        }

        void BuildContinueRow()
        {
            var host = UIKit.Node("Continue", Root)
                .BottomBand(bottom: 352f, height: 62f, inset: Theme.Gutter);
            _continueRow = host.gameObject;

            var btn = UIKit.Tappable("Tap", host, ContinueReading);
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            _continueLabel = UIKit.Text("Label", btn.transform, "",
                Theme.Sans, 21f, Theme.SaffronLit, TextAlignmentOptions.Center);
            _continueLabel.rectTransform.Inset(0f, 0f, 0f, 0f);

            _continueRow.SetActive(false);
        }

        void BuildButtons()
        {
            Button(220f, "READ THE BOOK", true, () => AppRoot.Instance?.OpenBook(1));
            Button(92f, "THE EIGHTEEN CHAPTERS", false,
                () => AppRoot.Instance?.GoTo(ScreenId.Chapters));
        }

        void Button(float bottom, string label, bool primary, Action onTap)
        {
            var host = UIKit.Node(label, Root).BottomBand(bottom, 112f, Theme.Gutter);

            var btn = UIKit.Tappable("Btn", host, onTap,
                primary ? Theme.Saffron : Theme.TwilightLit.WithAlpha(0.85f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            var text = UIKit.Text("Label", btn.transform, label,
                Theme.SansBold, Theme.SizeLabel,
                primary ? Theme.NightDeep : Theme.Cream, TextAlignmentOptions.Center);
            text.rectTransform.Inset(0f, 0f, 0f, 0f);
            text.characterSpacing = 6f;
        }

        // ------------------------------------------------------------------

        public override void OnShow()
        {
            AppSettings.Changed += Refresh;
            Refresh();
        }

        public override void OnHidden()
        {
            AppSettings.Changed -= Refresh;
        }

        void Refresh()
        {
            var edition = AppSettings.Current;
            if (_langLabel != null && edition != null)
                _langLabel.text = $"{edition.langLabel} · {edition.translator}";

            _today = GitaDatabase.VerseOfTheDay(DateTime.Now);
            if (_today != null)
            {
                _saText.SetText(FirstLines(_today.sa, 2));
                _enText.text = Truncate(GitaDatabase.TextOf(_today, AppSettings.Edition), 210);
                _refText.text = $"Chapter {_today.c} · Verse {_today.v}";
            }

            if (AppSettings.TryGetPosition(out int c, out int v))
            {
                _continueRow.SetActive(true);
                _continueLabel.text = $"Continue from {c}.{v}  →";
            }
            else _continueRow.SetActive(false);
        }

        void ContinueReading()
        {
            if (AppSettings.TryGetPosition(out int c, out int v))
                AppRoot.Instance?.OpenVerse(c, v);
        }

        void OpenToday()
        {
            if (_today != null) AppRoot.Instance?.OpenVerse(_today.c, _today.v);
        }

        /// <summary>The opening lines of a shloka, so the card never overflows.</summary>
        static string FirstLines(string text, int count)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var kept = new System.Text.StringBuilder();
            int taken = 0;
            foreach (var line in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (taken++ >= count) break;
                if (kept.Length > 0) kept.Append('\n');
                kept.Append(line.Trim());
            }
            return kept.ToString();
        }

        static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            int cut = text.LastIndexOf(' ', Mathf.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd(',', ';', '.', ' ') + "…";
        }
    }
}
