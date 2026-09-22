using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gita.Data;
using Gita.World;

namespace Gita.UI
{
    /// <summary>The eighteen yogas, as a scrolling index.</summary>
    public sealed class ChaptersScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Chapters;
        public override App.BannerSlot Banner => App.BannerSlot.Bottom;

        const float CardHeightBase = 224f;
        const float CardGap = 18f;

        /// <summary>Cards get taller with the reader's text size; the list just scrolls further.</summary>
        static float CardHeight => Theme.Scaled(CardHeightBase);

        RectTransform _content;
        readonly System.Collections.Generic.List<GameObject> _cards = new();
        float _laidOutAt = -1f;

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            var scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.80f), UIKit.Solid);
            scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            BuildHeader();
            BuildList();
        }

        void BuildHeader()
        {
            var header = UIKit.Node("Header", Root).TopBand(top: 0f, height: 190f);

            var bg = UIKit.Panel("Bg", header, Theme.NightDeep.WithAlpha(0.96f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", header, () => App.AppRoot.Instance?.GoBack());
            back.GetComponent<RectTransform>().TopBand(64f, 84f, 0f);
            back.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
            back.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 84f);
            back.GetComponent<RectTransform>().anchoredPosition = new Vector2(Theme.Gutter + 60f, -106f);

            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 44f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            var title = UIKit.Text("Title", header, "The Eighteen Chapters",
                Theme.Serif, Theme.SizeHeading, Theme.Cream, TextAlignmentOptions.Center);
            title.rectTransform.TopBand(80f, 56f, Theme.Gutter + 140f);

            var rule = UIKit.Panel("Rule", header, Theme.Saffron.WithAlpha(0.35f), UIKit.Solid);
            rule.rectTransform.BottomBand(0f, 2f);
        }

        void BuildList()
        {
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, 190f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            // An invisible graphic so the viewport itself catches drags.
            var catcher = viewport.gameObject.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            var content = UIKit.Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            _content = content;
            LayOutCards();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 28f;
        }


        /// <summary>
        /// Positions the eighteen cards. Rebuilt rather than resized when the reader
        /// changes text size: the cards grow with the type, so every position after the
        /// first one moves.
        /// </summary>
        void LayOutCards()
        {
            if (_content == null) return;

            foreach (var go in _cards) if (go != null) Destroy(go);
            _cards.Clear();

            float y = Theme.GapL;
            foreach (var ch in GitaDatabase.Chapters)
            {
                _cards.Add(BuildCard(_content, ch, y));
                y += CardHeight + CardGap;
            }
            y += Theme.GapL * 2f;   // breathing room at the end of the scroll

            _content.sizeDelta = new Vector2(0f, y);
            _laidOutAt = Theme.TextScale;
        }

        public override void OnShow()
        {
            // Only worth the rebuild if the type has actually changed since last time.
            if (!Mathf.Approximately(_laidOutAt, Theme.TextScale)) LayOutCards();
        }
        GameObject BuildCard(RectTransform parent, Chapter ch, float top)
        {
            var card = UIKit.Node($"Ch{ch.number}", parent);
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.offsetMin = new Vector2(Theme.Gutter, 0f);
            card.offsetMax = new Vector2(-Theme.Gutter, 0f);
            card.anchoredPosition = new Vector2(0f, -top);
            card.sizeDelta = new Vector2(-Theme.Gutter * 2f, CardHeight);

            var btn = UIKit.Tappable("Tap", card,
                () => App.AppRoot.Instance?.OpenChapter(ch.number),
                Theme.TwilightLit.WithAlpha(0.66f));
            var btnRt = btn.GetComponent<RectTransform>();
            btnRt.Inset(0f, 0f, 0f, 0f);
            btn.image.sprite = UIKit.Rounded;

            var body = btn.transform;

            // Chapter number, set large in the left margin.
            var num = UIKit.Text("Num", body, ch.number.ToString("00"),
                Theme.Serif, 62f, Theme.Saffron.WithAlpha(0.85f), TextAlignmentOptions.Center);
            num.Fit(0.6f);
            num.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            num.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            num.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            num.rectTransform.sizeDelta = new Vector2(120f, 90f);
            num.rectTransform.anchoredPosition = new Vector2(84f, Theme.Scaled(6f));

            var count = UIKit.Text("Count", body, $"{ch.verseCount} verses",
                Theme.Sans, 23f, Theme.Muted, TextAlignmentOptions.Center);
            count.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            count.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            count.rectTransform.sizeDelta = new Vector2(140f, 30f);
            count.rectTransform.anchoredPosition = new Vector2(84f, Theme.Scaled(-46f));
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.Fit(0.55f);

            // Titles, stacked to the right of the number.
            const float textLeft = 168f;

            var devanagari = ShapedText.Create("Sa", body,
                Theme.Devanagari, NativeText.SerifDevanagari, 40f,
                Theme.Cream, NativeText.AlignStart);
            var devRt = (RectTransform)devanagari.transform;
            devRt.TopBand(Theme.Scaled(24f), Theme.Scaled(56f), 0f);
            devRt.offsetMin = new Vector2(textLeft, devRt.offsetMin.y);
            devRt.offsetMax = new Vector2(-28f, devRt.offsetMax.y);
            devanagari.SetMaxHeightRef(Theme.Scaled(56f));
            devanagari.SetText(ch.name);

            var translit = UIKit.Text("Tr", body, ch.translit,
                Theme.Serif, 32f, Theme.Saffron.WithAlpha(0.9f), TextAlignmentOptions.TopLeft);
            translit.rectTransform.TopBand(Theme.Scaled(86f), Theme.Scaled(46f), 0f);
            translit.rectTransform.offsetMin = new Vector2(textLeft, translit.rectTransform.offsetMin.y);
            translit.rectTransform.offsetMax = new Vector2(-28f, translit.rectTransform.offsetMax.y);
            translit.textWrappingMode = TextWrappingModes.NoWrap;
            translit.Fit(0.55f);

            var meaning = UIKit.Text("En", body, ch.meaning,
                Theme.Sans, 27f, Theme.Parchment.WithAlpha(0.85f), TextAlignmentOptions.TopLeft);
            meaning.rectTransform.TopBand(Theme.Scaled(140f), Theme.Scaled(66f), 0f);
            meaning.rectTransform.offsetMin = new Vector2(textLeft, meaning.rectTransform.offsetMin.y);
            meaning.rectTransform.offsetMax = new Vector2(-28f, meaning.rectTransform.offsetMax.y);
            meaning.overflowMode = TextOverflowModes.Ellipsis;
            meaning.Fit(0.6f);

            // Hairline separating the number column from the titles.
            var divider = UIKit.Panel("Div", body, Theme.Saffron.WithAlpha(0.18f), UIKit.Solid);
            divider.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            divider.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            divider.rectTransform.sizeDelta = new Vector2(1.5f, CardHeight - 64f);
            divider.rectTransform.anchoredPosition = new Vector2(150f, 0f);

            return card.gameObject;
        }
    }
}
