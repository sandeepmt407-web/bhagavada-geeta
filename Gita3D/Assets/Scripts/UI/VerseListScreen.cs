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
    /// A scrolling list of verses, shared by search, bookmarks and the collections.
    /// Subclasses supply a title and fill the list; the rows, the empty state and the
    /// tap-through to the reader are handled here.
    /// </summary>
    public abstract class VerseListScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Chapters;

        /// <summary>Search, Saved and Collections all carry the banner at their foot.</summary>
        public override BannerSlot Banner => BannerSlot.Bottom;

        protected abstract string Title { get; }

        /// <summary>Height reserved under the header, for a search box or similar.</summary>
        protected virtual float HeaderExtra => 0f;

        protected RectTransform Header { get; private set; }
        protected ScrollRect Scroll { get; private set; }

        /// <summary>The list body. Subclasses may add their own rows to it.</summary>
        protected RectTransform Content { get; private set; }
        protected readonly List<GameObject> Rows = new();

        TextMeshProUGUI _emptyLabel, _countLabel;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            var scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.88f), UIKit.Solid);
            scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            float headerHeight = 190f + HeaderExtra;

            Header = UIKit.Node("Header", Root).TopBand(0f, headerHeight);
            var bg = UIKit.Panel("Bg", Header, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", Header, OnBack);
            var rt = back.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 84f);
            rt.anchoredPosition = new Vector2(Theme.Gutter + 55f, -100f);
            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 44f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            var title = UIKit.Text("Title", Header, Title,
                Theme.Serif, Theme.SizeHeading, Theme.Cream, TextAlignmentOptions.Center);
            title.rectTransform.TopBand(64f, 52f, Theme.Gutter + 140f);
            title.overflowMode = TextOverflowModes.Ellipsis;
            // At the larger text sizes the heading is taller than its band, and an
            // ellipsised line that does not fit is not drawn at all.
            title.Fit();

            _countLabel = UIKit.Text("Count", Header, "",
                Theme.Sans, 23f, Theme.Muted, TextAlignmentOptions.Center);
            _countLabel.rectTransform.TopBand(116f, 28f, Theme.Gutter + 140f);
            _countLabel.characterSpacing = 4f;

            var rule = UIKit.Panel("Rule", Header, Theme.Saffron.WithAlpha(0.35f), UIKit.Solid);
            rule.rectTransform.BottomBand(0f, 2f);

            BuildHeaderExtra(Header);

            // ---- list -----------------------------------------------------
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, headerHeight, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            Content = UIKit.Node("Content", viewport);
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = new Vector2(1f, 1f);
            Content.pivot = new Vector2(0.5f, 1f);
            Content.offsetMin = Vector2.zero;
            Content.offsetMax = Vector2.zero;

            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 28, 90);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = Content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scroll = viewport.gameObject.AddComponent<ScrollRect>();
            Scroll.content = Content;
            Scroll.viewport = viewport;
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Elastic;
            Scroll.elasticity = 0.1f;
            Scroll.decelerationRate = 0.135f;
            Scroll.scrollSensitivity = 28f;

            _emptyLabel = UIKit.Text("Empty", Root, "",
                Theme.Sans, 28f, Theme.Muted, TextAlignmentOptions.Center);
            _emptyLabel.rectTransform.Inset(Theme.Gutter * 1.6f, headerHeight + 140f,
                Theme.Gutter * 1.6f, 0f);
            _emptyLabel.gameObject.SetActive(false);
        }

        protected virtual void BuildHeaderExtra(RectTransform header) { }

        /// <summary>Empties the list, including any rows a subclass added itself.</summary>
        protected void ClearRows()
        {
            foreach (var go in Rows) if (go != null) Destroy(go);
            Rows.Clear();
        }

        protected void SetCaption(string text) =>
            _countLabel.text = text ?? "";

        protected void SetEmptyMessage(string text)
        {
            bool any = string.IsNullOrEmpty(text);
            _emptyLabel.gameObject.SetActive(!any);
            _emptyLabel.text = text ?? "";
        }

        protected void RelayoutList()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            Scroll.verticalNormalizedPosition = 1f;
        }

        protected virtual void OnBack() => AppRoot.Instance?.GoBack();

        // ------------------------------------------------------------------

        /// <summary>Replaces the list. Pass an empty list with a message for the empty state.</summary>
        protected void Show(IReadOnlyList<Verse> verses, string emptyMessage, string countSuffix = null)
        {
            foreach (var go in Rows) if (go != null) Destroy(go);
            Rows.Clear();

            bool any = verses != null && verses.Count > 0;
            _emptyLabel.gameObject.SetActive(!any);
            _emptyLabel.text = any ? "" : emptyMessage;
            _countLabel.text = any
                ? $"{verses.Count} {(countSuffix ?? (verses.Count == 1 ? "VERSE" : "VERSES"))}"
                : "";

            if (any)
                foreach (var verse in verses)
                    AddRow(verse);

            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            Scroll.verticalNormalizedPosition = 1f;
        }

        void AddRow(Verse verse)
        {
            var host = UIKit.Node($"V{verse.c}_{verse.v}", Content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(196f);
            el.minHeight = el.preferredHeight;

            var btn = UIKit.Tappable("Tap", host,
                () => AppRoot.Instance?.OpenVerse(verse.c, verse.v),
                Theme.TwilightLit.WithAlpha(0.60f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            var reference = UIKit.Text("Ref", btn.transform, $"{verse.c}.{verse.v}",
                Theme.SansBold, 26f, Theme.Saffron, TextAlignmentOptions.TopLeft);
            reference.rectTransform.TopBand(Theme.Scaled(20f), Theme.Scaled(34f), 28f);
            reference.Fit(0.6f);
            reference.characterSpacing = 4f;

            // A dot for a verse already read, so progress is visible while browsing.
            if (ReadingLog.HasRead(verse.c, verse.v))
            {
                var dot = UIKit.Raw("Read", btn.transform, Theme.Saffron.WithAlpha(0.55f), UIKit.Circle);
                var drt = dot.rectTransform;
                drt.anchorMin = drt.anchorMax = new Vector2(1f, 1f);
                drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(13f, 13f);
                drt.anchoredPosition = new Vector2(-30f, -34f);
            }

            var snippet = UIKit.Text("Text", btn.transform,
                Snippet(GitaDatabase.TextOf(verse, AppSettings.Edition), 150),
                Theme.Serif, 29f, Theme.Parchment, TextAlignmentOptions.TopLeft);
            snippet.rectTransform.TopBand(Theme.Scaled(60f), Theme.Scaled(124f), 28f);
            snippet.overflowMode = TextOverflowModes.Ellipsis;
            snippet.Fit(0.6f);
            snippet.lineSpacing = 6f;
            snippet.overflowMode = TextOverflowModes.Ellipsis;

            Rows.Add(host.gameObject);
        }

        static string Snippet(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= max) return text;
            int cut = text.LastIndexOf(' ', Mathf.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd(',', ';', '.', ' ') + "…";
        }
    }
}
