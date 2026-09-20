using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gita.Data;

namespace Gita.UI
{
    /// <summary>
    /// Themed ways in. Shows the collections first; choosing one swaps the list to its
    /// verses, and Back steps out of the collection before leaving the screen.
    /// </summary>
    public sealed class CollectionsScreen : VerseListScreen
    {
        protected override string Title => "Collections";

        Themes.Theme _open;

        public override void OnShow()
        {
            _open = null;
            ShowThemes();
        }

        protected override void OnBack()
        {
            // Inside a collection, Back returns to the list of collections.
            if (_open != null)
            {
                _open = null;
                ShowThemes();
                return;
            }
            base.OnBack();
        }

        void ShowThemes()
        {
            ClearRows();
            SetEmptyMessage(null);
            SetCaption($"{Themes.All.Length} COLLECTIONS");

            foreach (var theme in Themes.All)
                AddThemeRow(theme);

            RelayoutList();
        }

        void Open(Themes.Theme theme)
        {
            _open = theme;
            Show(Themes.VersesOf(theme), "This collection is empty.");
            SetCaption($"{theme.Name.ToUpperInvariant()}  ·  {theme.Verses.Length} VERSES");
        }

        void AddThemeRow(Themes.Theme theme)
        {
            var host = UIKit.Node(theme.Name, Content);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(146f);
            el.minHeight = el.preferredHeight;

            var captured = theme;
            var btn = UIKit.Tappable("Tap", host, () => Open(captured),
                Theme.TwilightLit.WithAlpha(0.62f));
            btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

            var name = UIKit.Text("Name", btn.transform, theme.Name,
                Theme.Serif, 34f, Theme.Cream, TextAlignmentOptions.TopLeft);
            name.rectTransform.TopBand(Theme.Scaled(24f), Theme.Scaled(46f), 30f);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.Fit(0.6f);

            var blurb = UIKit.Text("Blurb", btn.transform, theme.Blurb,
                Theme.Sans, 25f, Theme.Muted, TextAlignmentOptions.TopLeft);
            blurb.rectTransform.TopBand(Theme.Scaled(78f), Theme.Scaled(40f), 30f);
            blurb.textWrappingMode = TextWrappingModes.NoWrap;
            blurb.overflowMode = TextOverflowModes.Ellipsis;
            blurb.Fit(0.6f);

            var count = UIKit.Text("Count", btn.transform, theme.Verses.Length.ToString(),
                Theme.SansBold, 34f, Theme.Saffron.WithAlpha(0.8f), TextAlignmentOptions.Center);
            var rt = count.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(70f, 54f);
            rt.anchoredPosition = new Vector2(-46f, 0f);

            Rows.Add(host.gameObject);
        }
    }
}
