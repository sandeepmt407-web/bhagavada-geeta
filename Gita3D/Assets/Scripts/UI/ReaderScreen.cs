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
    /// Reads one shloka at a time: Sanskrit, transliteration, and a chosen reading of
    /// it. Swipe or tap to move through the corpus continuously - the end of a chapter
    /// runs straight into the next.
    ///
    /// The tabs are built from the corpus rather than hard-coded, so they follow the
    /// language the reader picked: the main translation, its poetic counterpart in the
    /// same language, Hindi (always offered), and the word-by-word gloss.
    ///
    /// Audio is one control, not two. There was a speaker in the tab row and a separate
    /// mute in the header, which is two different answers to the same question; now a
    /// single play/stop button in the footer both speaks the verse and decides whether
    /// the next one speaks itself.
    /// </summary>
    public sealed class ReaderScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Reader;

        /// <summary>
        /// At the top: the footer holds Next, tapped on every verse, and a banner under it
        /// would collect accidental clicks.
        /// </summary>
        public override BannerSlot Banner => BannerSlot.Top;

        enum Tab { Translation, Poetic, Hindi, WordByWord }

        /// <summary>A tab as actually offered for the current language.</summary>
        readonly struct TabSpec
        {
            public readonly Tab Kind;
            public readonly string Label;
            public readonly int Edition;   // -1 for the word-by-word gloss

            public TabSpec(Tab kind, string label, int edition)
            {
                Kind = kind; Label = label; Edition = edition;
            }
        }

        int _flatIndex;
        Tab _tab = Tab.Translation;

        TextMeshProUGUI _chapterTitle, _position, _reference, _translit, _body, _attribution;
        TextMeshProUGUI _bookmarkGlyph;
        Image _bookmarkBg;
        ShapedText _sanskrit, _bodyDevanagari;
        RectTransform _content, _chipBar;
        ScrollRect _scroll;

        Image _scrim;

        // the shloka switch, on the verse itself
        Image _sanskritBg, _sanskritIcon;
        TextMeshProUGUI _sanskritLabel;

        // the one audio control
        Image _audioBg, _playIcon;
        RectTransform _pauseIcon;
        TextMeshProUGUI _audioLabel;

        readonly List<TabSpec> _tabs = new();
        readonly List<GameObject> _chipObjects = new();

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            // The set behind the text should still read as a place, so this is as light as
            // each look allows - which is not a constant. The mood director says how much
            // darkening its current sky needs and this follows it; the bars at either end
            // are near-opaque on their own, so the body is the only part relying on it.
            _scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.70f), UIKit.Solid);
            _scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            BuildHeader();
            BuildScroll();
            _chipBar = UIKit.Node("Chips", Root)
                .BottomBand(bottom: 176f, height: 116f, inset: Theme.Gutter);
            BuildFooter();
        }

        // ------------------------------------------------------------------

        void BuildHeader()
        {
            var header = UIKit.Node("Header", Root).TopBand(0f, 200f);

            var bg = UIKit.Panel("Bg", header, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", header, () => AppRoot.Instance?.GoBack());
            var backRt = back.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.sizeDelta = new Vector2(112f, 92f);
            backRt.anchoredPosition = new Vector2(Theme.Gutter + 56f, -112f);
            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 50f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            // Two actions on the right. Audio used to be a third; it now lives in the
            // footer as a single play/stop control.
            var share = HeaderButton(header, "Share", 46f, ShareCurrent);
            var shareGlyph = UIKit.Text("Glyph", share.transform, "↗",
                Theme.Sans, 36f, Theme.Cream, TextAlignmentOptions.Center);
            shareGlyph.rectTransform.Inset(0f, 0f, 0f, 0f);

            var mark = HeaderButton(header, "Bookmark", 142f, ToggleBookmark);
            _bookmarkBg = mark.image;
            _bookmarkGlyph = UIKit.Text("Glyph", mark.transform, "",
                Theme.Sans, 34f, Theme.Cream, TextAlignmentOptions.Center);
            _bookmarkGlyph.rectTransform.Inset(0f, 0f, 0f, 0f);

            _chapterTitle = UIKit.Text("Chapter", header, "",
                Theme.Serif, 34f, Theme.Cream, TextAlignmentOptions.Center);
            _chapterTitle.rectTransform.TopBand(58f, 48f, Theme.Gutter + 220f);
            _chapterTitle.overflowMode = TextOverflowModes.Ellipsis;
            _chapterTitle.Fit();

            _position = UIKit.Text("Position", header, "",
                Theme.Sans, 23f, Theme.Muted, TextAlignmentOptions.Center);
            _position.rectTransform.TopBand(114f, 36f, Theme.Gutter + 220f);
            _position.characterSpacing = 3f;
            _position.Fit();

            var rule = UIKit.Panel("Rule", header, Theme.Saffron.WithAlpha(0.35f), UIKit.Solid);
            rule.rectTransform.BottomBand(0f, 2f);
        }

        /// <summary>An icon button in the header, measured in from the right edge.</summary>
        static Button HeaderButton(RectTransform header, string name, float inset,
            System.Action onTap)
        {
            var btn = UIKit.Tappable(name, header, onTap, Theme.TwilightLit.WithAlpha(0.7f));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(88f, 90f);
            rt.anchoredPosition = new Vector2(-Theme.Gutter - inset, -112f);
            return btn;
        }

        void BuildScroll()
        {
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, 200f, 0f, 304f);
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
            layout.padding = new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 44, 80);
            layout.spacing = Theme.GapM;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildReferenceRow();

            _sanskrit = ShapedText.Create("Sanskrit", _content,
                Theme.Devanagari, NativeText.SerifDevanagari, Theme.SizeVerseSa,
                Theme.Cream, NativeText.AlignCenter, lineSpacing: 1.28f);

            _translit = UIKit.Text("Translit", _content, "",
                Theme.Serif, 32f, Theme.Muted, TextAlignmentOptions.Center);
            _translit.lineSpacing = 8f;
            _translit.fontStyle = FontStyles.Italic;

            AddRule(_content);

            _attribution = UIKit.Text("Attribution", _content, "",
                Theme.Sans, 23f, Theme.Saffron.WithAlpha(0.7f), TextAlignmentOptions.Center);
            _attribution.characterSpacing = 5f;

            _body = UIKit.Text("Body", _content, "",
                Theme.Serif, Theme.SizeBody, Theme.Parchment, TextAlignmentOptions.TopLeft);
            _body.lineSpacing = 12f;

            _bodyDevanagari = ShapedText.Create("BodyDevanagari", _content,
                Theme.DevanagariUI, NativeText.SansDevanagari, Theme.SizeBody,
                Theme.Parchment, NativeText.AlignStart, lineSpacing: 1.4f);
            _bodyDevanagari.gameObject.SetActive(false);

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.1f;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = 28f;

            // Horizontal flicks turn the page; the ScrollRect above handles vertical.
            var swipe = viewport.gameObject.AddComponent<SwipeArea>();
            swipe.onSwipeLeft = Next;
            swipe.onSwipeRight = Previous;
        }


        /// <summary>
        /// The verse number, with the shloka control beside it.
        ///
        /// The control belongs here rather than in settings or the footer: it sits on
        /// the Sanskrit it decides the fate of, and the line it shares was empty space
        /// either side of a centred "2.47", so it costs no height. It is a standing
        /// switch, not a play button - whichever way it is set, every verse opens
        /// reading that, so nobody presses play on each page.
        /// </summary>
        void BuildReferenceRow()
        {
            var row = UIKit.Node("ReferenceRow", _content);
            var el = row.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = Theme.Scaled(66f);
            el.minHeight = el.preferredHeight;

            _reference = UIKit.Text("Reference", row, "",
                Theme.SansBold, Theme.SizeCaption, Theme.Saffron, TextAlignmentOptions.Center);
            _reference.rectTransform.Inset(0f, 0f, 0f, 0f);
            _reference.characterSpacing = 8f;

            var btn = UIKit.Tappable("Shloka", row, ToggleSanskrit,
                Theme.TwilightLit.WithAlpha(0.7f));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(168f, Theme.Scaled(60f));
            rt.anchoredPosition = Vector2.zero;
            btn.image.sprite = UIKit.RoundedSoft;
            _sanskritBg = btn.image;

            _sanskritIcon = UIKit.Raw("Play", btn.transform, Theme.Saffron, UIKit.Triangle);
            var icon = _sanskritIcon.rectTransform;
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(24f, 25f);
            icon.anchoredPosition = new Vector2(20f, 0f);

            // Set in Latin, not "संस्कृत". Labels outside the verse blocks go through
            // TextMeshPro, which cannot form the conjunct in that word - it would render
            // visibly wrong on a handset, which is the one thing this app must not do to
            // Devanagari.
            _sanskritLabel = UIKit.Text("Label", btn.transform, "SHLOKA",
                Theme.SansBold, 21f, Theme.Parchment, TextAlignmentOptions.Center);
            _sanskritLabel.characterSpacing = 3f;
            _sanskritLabel.rectTransform.Inset(48f, 0f, 12f, 0f);
            _sanskritLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _sanskritLabel.Fit(0.6f);
        }

        /// <summary>
        /// The shloka switch: whether the Sanskrit is read.
        ///
        /// Independent of the footer control. With both on, every verse gives the shloka
        /// first and then its meaning.
        /// </summary>
        void ToggleSanskrit()
        {
            bool on = !AppSettings.NarrateSanskrit;
            AppSettings.NarrateSanskrit = on;

            if (on)
            {
                SpeakCurrent();
            }
            else
            {
                Narration.Stop();
                if (AppSettings.AudioEnabled) SpeakCurrent();
                else AudioSession.End();
            }
            UpdateSanskritButton();
            UpdateAudioButton();
        }

        void UpdateSanskritButton()
        {
            bool on = AppSettings.NarrateSanskrit;
            if (_sanskritBg != null)
                _sanskritBg.color = on ? Theme.Saffron.WithAlpha(0.28f)
                                       : Theme.TwilightLit.WithAlpha(0.7f);
            if (_sanskritIcon != null)
                _sanskritIcon.color = on ? Theme.SaffronLit : Theme.Muted;
            if (_sanskritLabel != null)
                _sanskritLabel.color = on ? Theme.SaffronLit : Theme.Parchment;
        }
        static void AddRule(Transform parent)
        {
            var host = UIKit.Node("Rule", parent);
            var el = host.gameObject.AddComponent<LayoutElement>();
            el.preferredHeight = 1.5f;
            el.minHeight = 1.5f;

            var img = host.gameObject.AddComponent<Image>();
            img.sprite = UIKit.Solid;
            img.type = Image.Type.Sliced;
            img.color = Theme.Saffron.WithAlpha(0.22f);
            img.raycastTarget = false;
        }

        void BuildFooter()
        {
            var footer = UIKit.Node("Footer", Root).BottomBand(0f, 164f);

            var bg = UIKit.Panel("Bg", footer, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var rule = UIKit.Panel("Rule", footer, Theme.Saffron.WithAlpha(0.22f), UIKit.Solid);
            rule.rectTransform.TopBand(0f, 1.5f);

            MakeNavButton(footer, "Prev", "← Previous", 0f, Previous);
            MakeNavButton(footer, "Next", "Next →", 1f, Next);
            BuildAudioButton(footer);
        }

        /// <summary>
        /// The single audio control, in the middle of the footer where a thumb lands.
        ///
        /// Playing stops it and leaves the rest of the reading silent; starting it
        /// speaks this verse and lets the following ones speak themselves. One button,
        /// one meaning - which is the whole point of merging it with what used to be a
        /// separate mute.
        /// </summary>
        void BuildAudioButton(RectTransform footer)
        {
            var btn = UIKit.Tappable("Audio", footer, ToggleAudio, Theme.TwilightLit.WithAlpha(0.8f));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 104f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            btn.image.sprite = UIKit.RoundedSoft;
            _audioBg = btn.image;

            // Play: a drawn triangle rather than a glyph, so nothing depends on which
            // symbols happen to be in the bundled fonts.
            _playIcon = UIKit.Raw("Play", btn.transform, Theme.Saffron, UIKit.Triangle);
            var play = _playIcon.rectTransform;
            play.anchorMin = play.anchorMax = new Vector2(0.5f, 0.5f);
            play.pivot = new Vector2(0.5f, 0.5f);
            play.sizeDelta = new Vector2(34f, 36f);
            play.anchoredPosition = new Vector2(2f, 14f);

            // Stop: two bars, built the same way.
            _pauseIcon = UIKit.Node("Pause", btn.transform);
            _pauseIcon.anchorMin = _pauseIcon.anchorMax = new Vector2(0.5f, 0.5f);
            _pauseIcon.pivot = new Vector2(0.5f, 0.5f);
            _pauseIcon.sizeDelta = new Vector2(34f, 36f);
            _pauseIcon.anchoredPosition = new Vector2(0f, 14f);
            Bar(_pauseIcon, -9f);
            Bar(_pauseIcon, 9f);

            _audioLabel = UIKit.Text("Label", btn.transform, "LISTEN",
                Theme.SansBold, 19f, Theme.Parchment, TextAlignmentOptions.Bottom);
            _audioLabel.rectTransform.Inset(0f, 0f, 0f, 12f);
            _audioLabel.characterSpacing = 4f;
            _audioLabel.Fit(0.6f);
        }

        static void Bar(RectTransform parent, float x)
        {
            var img = UIKit.Raw("Bar", parent, Theme.Saffron, UIKit.Solid);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(10f, 34f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        void MakeNavButton(RectTransform parent, string name, string label, float anchorX,
            System.Action action)
        {
            var btn = UIKit.Tappable(name, parent, action);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.sizeDelta = new Vector2(280f, 104f);
            rt.anchoredPosition = new Vector2(anchorX == 0f ? Theme.Gutter : -Theme.Gutter, 0f);

            var text = UIKit.Text("Label", btn.transform, label,
                Theme.SansBold, 27f, Theme.Cream,
                anchorX == 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            text.rectTransform.Inset(6f, 0f, 6f, 0f);
            text.characterSpacing = 2f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.Fit(0.55f);
        }

        // ------------------------------------------------------------------
        // tabs
        // ------------------------------------------------------------------

        /// <summary>
        /// Works out which readings this language can offer, then lays out one chip per
        /// reading. Rebuilt whenever the language changes.
        /// </summary>
        void RebuildTabs()
        {
            _tabs.Clear();
            string lang = AppSettings.Language;
            int main = AppSettings.Edition;

            _tabs.Add(new TabSpec(Tab.Translation, "Translation", main));

            // The poetic reading of the same language, where one exists.
            int alternate = GitaDatabase.AlternateEditionFor(lang, main);
            if (alternate >= 0)
            {
                bool poetic = GitaDatabase.EditionAt(alternate)?.poetic ?? false;
                _tabs.Add(new TabSpec(Tab.Poetic, poetic ? "Poetic" : "Alternate", alternate));
            }

            // Hindi is always offered, except when it is already the chosen language -
            // there it would only duplicate the Translation tab.
            if (lang != "hi")
            {
                int hindi = GitaDatabase.FirstEditionOf("hi");
                if (hindi >= 0) _tabs.Add(new TabSpec(Tab.Hindi, "Hindi", hindi));
            }

            _tabs.Add(new TabSpec(Tab.WordByWord, "Word by word", -1));

            if (!_tabs.Exists(t => t.Kind == _tab)) _tab = Tab.Translation;

            LayoutChips();
        }

        void LayoutChips()
        {
            foreach (var go in _chipObjects) if (go != null) Destroy(go);
            _chipObjects.Clear();

            const float gap = 10f;
            float total = _tabs.Count;
            float cursor = 0f;

            foreach (var spec in _tabs)
            {
                var cell = Cell(cursor, 1f, total, gap);
                cursor += 1f;

                var captured = spec.Kind;
                var btn = UIKit.Tappable("Tap", cell, () => SetTab(captured),
                    Theme.TwilightLit.WithAlpha(0.7f));
                btn.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);

                var text = UIKit.Text("Label", btn.transform, spec.Label,
                    Theme.Sans, _tabs.Count >= 4 ? 22f : 25f, Theme.Parchment,
                    TextAlignmentOptions.Center);
                text.rectTransform.Inset(4f, 0f, 4f, 0f);
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.Fit(0.55f);

                _chipObjects.Add(cell.gameObject);
            }
        }

        RectTransform Cell(float start, float weight, float total, float gap)
        {
            var cell = UIKit.Node($"Chip{start}", _chipBar);
            cell.anchorMin = new Vector2(start / total, 0.18f);
            cell.anchorMax = new Vector2((start + weight) / total, 0.82f);
            cell.offsetMin = new Vector2(gap * 0.5f, 0f);
            cell.offsetMax = new Vector2(-gap * 0.5f, 0f);
            return cell;
        }

        void PaintChips()
        {
            for (int i = 0; i < _tabs.Count && i < _chipObjects.Count; i++)
            {
                var btn = _chipObjects[i].GetComponentInChildren<Image>();
                var label = _chipObjects[i].GetComponentInChildren<TextMeshProUGUI>();
                bool on = _tabs[i].Kind == _tab;
                if (btn != null)
                    btn.color = on ? Theme.Saffron.WithAlpha(0.92f) : Theme.TwilightLit.WithAlpha(0.7f);
                if (label != null)
                    label.color = on ? Theme.NightDeep : Theme.Parchment;
            }
        }

        // ------------------------------------------------------------------
        // state
        // ------------------------------------------------------------------

        public void SetVerse(int chapter, int verse)
        {
            int index = GitaDatabase.FlatIndexOf(chapter, verse);
            _flatIndex = index >= 0 ? index : 0;
            if (IsVisible) Refresh();
        }

        public override void OnShow()
        {
            AppSettings.Changed += OnSettingsChanged;
            RebuildTabs();
            Refresh();
        }

        public override void OnHidden()
        {
            AppSettings.Changed -= OnSettingsChanged;
            Narration.Stop();
            AudioSession.End();
        }

        void OnSettingsChanged()
        {
            if (!IsVisible) return;
            RebuildTabs();
            Refresh(speak: false);
        }

        void Next()
        {
            if (_flatIndex >= GitaDatabase.Verses.Length - 1) return;
            _flatIndex++;

            // Every fifth verse read forward brings an interstitial. The page turns
            // underneath it, and the new verse is read aloud once it has gone.
            bool advert = Ads.Instance != null && Ads.Instance.VerseCompleted(OnAdvertDismissed);
            Refresh(speak: !advert);
        }

        void OnAdvertDismissed()
        {
            if (IsVisible && AppSettings.AudioEnabled) SpeakCurrent();
        }

        void Previous()
        {
            if (_flatIndex <= 0) return;
            _flatIndex--;
            Refresh();
        }

        /// <summary>
        /// Switches which reading is shown, and reads it aloud in its own language.
        ///
        /// Choosing the Hindi tab is a request to hear the Hindi, so it starts narrating
        /// in a Hindi voice straight away rather than waiting to be asked twice. Picking
        /// a tab is also a request for a meaning rather than the shloka, so it leaves
        /// shloka mode the same way the footer control does.
        /// </summary>
        void SetTab(Tab tab)
        {
            _tab = tab;
            AppSettings.AudioEnabled = true;
            Refresh();
        }

        /// <summary>
        /// The footer control: whether the translation is read.
        ///
        /// It does not touch the shloka switch. The two are independent, so a reader can
        /// have the Sanskrit, the meaning, both in turn, or neither. Turning it on reads
        /// the verse now as well as the ones after it.
        /// </summary>
        void ToggleAudio()
        {
            bool on = !AppSettings.AudioEnabled;
            AppSettings.AudioEnabled = on;

            if (on)
            {
                SpeakCurrent();
            }
            else
            {
                Narration.Stop();
                if (!AppSettings.NarrateSanskrit) AudioSession.End();
            }
            UpdateAudioButton();
        }

        void ToggleBookmark()
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            if (verse == null) return;
            ReadingLog.ToggleBookmark(verse.c, verse.v);
            UpdateBookmarkButton();
        }

        void UpdateBookmarkButton()
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            bool on = verse != null && ReadingLog.IsBookmarked(verse.c, verse.v);
            if (_bookmarkGlyph != null)
            {
                _bookmarkGlyph.text = on ? "★" : "☆";   // filled or hollow star
                _bookmarkGlyph.color = on ? Theme.Saffron : Theme.Muted;
            }
            if (_bookmarkBg != null)
                _bookmarkBg.color = on ? Theme.Saffron.WithAlpha(0.22f) : Theme.TwilightLit.WithAlpha(0.7f);
        }

        void ShareCurrent()
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            if (verse == null) return;
            VerseShare.Share(verse, ActiveEdition());
        }

        /// <summary>
        /// Shows stop while a verse is being read and play the rest of the time. The
        /// label says which, because an icon alone is not enough on a control that has
        /// just changed meaning.
        /// </summary>
        void UpdateAudioButton()
        {
            bool active = AppSettings.AudioEnabled;

            if (_playIcon != null) _playIcon.gameObject.SetActive(!active);
            if (_pauseIcon != null) _pauseIcon.gameObject.SetActive(active);

            if (_audioLabel != null)
            {
                _audioLabel.text = active ? "STOP" : "LISTEN";
                _audioLabel.color = active ? Theme.SaffronLit : Theme.Parchment;
            }
            if (_audioBg != null)
                _audioBg.color = active
                    ? Theme.Saffron.WithAlpha(0.26f)
                    : Theme.TwilightLit.WithAlpha(0.8f);
        }

        protected override void Update()
        {
            base.Update();
            if (!IsVisible) return;

            // Follows the mood through its crossfade, so the text never has to sit on a
            // sky that has become too bright for it.
            if (_scrim != null)
            {
                var mood = AppRoot.Instance?.Mood;
                if (mood != null) _scrim.color = Theme.NightDeep.WithAlpha(mood.Scrim);
            }
        }


        /// <summary>The edition currently on screen, falling back to the chosen one.</summary>
        int ActiveEdition()
        {
            foreach (var spec in _tabs)
                if (spec.Kind == _tab && spec.Edition >= 0) return spec.Edition;
            return AppSettings.Edition;
        }
        /// <summary>
        /// Reads the verse according to the two switches, which are independent.
        ///
        /// SHLOKA on reads the Sanskrit. The footer control on reads the translation
        /// that is on screen, in that reading's own language. With both on, the shloka
        /// comes first and the meaning follows it, which is the order anyone reciting
        /// would use. With neither, nothing is read.
        /// </summary>
        void SpeakCurrent()
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            if (verse == null) return;

            bool wantSanskrit = AppSettings.NarrateSanskrit;
            bool wantMeaning = AppSettings.AudioEnabled;
            if (!wantSanskrit && !wantMeaning) return;

            string saText = wantSanskrit ? StripVerseNumber(verse.sa) : null;

            string meaningText = null, meaningLocale = null, meaningVoice = null;
            if (wantMeaning)
            {
                // Read whatever is on screen, in that edition's own language - the Hindi
                // tab should give a Hindi voice, not an English one mispronouncing it.
                int edition = ActiveEdition();
                meaningText = GitaDatabase.TextOf(verse, edition, out edition);

                // Credit the edition the text actually came from, so a fallback is not
                // read aloud in the wrong language's voice.
                var record = GitaDatabase.EditionAt(edition);
                meaningLocale = record?.tts ?? "en-IN";
                meaningVoice = AppSettings.VoiceFor(record?.lang);
            }

            bool haveSa = !string.IsNullOrWhiteSpace(saText);
            bool haveMeaning = !string.IsNullOrWhiteSpace(meaningText);
            if (!haveSa && !haveMeaning) return;

            if (haveSa)
            {
                Narration.SpeakPair(
                    saText, AppSettings.SanskritTts,
                    AppSettings.VoiceFor(AppSettings.SanskritVoiceLang),
                    haveMeaning ? meaningText : "", meaningLocale, meaningVoice,
                    AppSettings.SpeechRate);
            }
            else
            {
                Narration.Speak(meaningText, meaningLocale, AppSettings.SpeechRate,
                    voiceName: meaningVoice);
            }

            AudioSession.Begin();   // hold the screen awake while a verse is read
        }

        /// <summary>
        /// Removes the verse number the corpus carries at the end of each shloka.
        ///
        /// Every one of the 701 ends with something like "।।2.47।।", which is a printing
        /// convention, not part of the verse. Left in, the speech engine reads it out as
        /// a number after the line, which is both wrong and jarring. The last verse ends
        /// with a single closing danda rather than two, so the count is not assumed.
        /// </summary>
        static string StripVerseNumber(string sanskrit)
        {
            if (string.IsNullOrEmpty(sanskrit)) return sanskrit;
            // । is the danda and ॥ the double danda. Written as codepoints
            // rather than as characters so the pattern cannot be broken by anything
            // that re-saves this file in the wrong encoding.
            return System.Text.RegularExpressions.Regex.Replace(
                sanskrit, "[।॥]+\\s*[0-9][0-9.,\\-\\s]*[।॥]*\\s*$", "")
                .TrimEnd();
        }

        void Refresh(bool speak = true)
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            if (verse == null) return;
            if (_tabs.Count == 0) RebuildTabs();

            var chapter = GitaDatabase.GetChapter(verse.c);
            int total = chapter?.verseCount ?? GitaDatabase.VersesOf(verse.c).Count;

            _chapterTitle.text = chapter != null ? chapter.translit : $"Chapter {verse.c}";
            _position.text = $"CHAPTER {verse.c}  ·  VERSE {verse.v} OF {total}";
            _reference.text = $"{verse.c}.{verse.v}";
            _sanskrit.SetText(verse.sa);
            _translit.text = verse.tr;

            // Dress the set for this verse: the light, the air and the framing all
            // follow what the verse is about.
            AppRoot.Instance?.Mood?.GoTo(verse.c, verse.v);

            bool wordByWord = _tab == Tab.WordByWord;
            int edition = ActiveEdition();

            // Resolve the text first: a gap in one edition falls back to another, and
            // the credit must name whoever actually wrote the line on screen.
            string bodyText = wordByWord ? "" : GitaDatabase.TextOf(verse, edition, out edition);
            var record = GitaDatabase.EditionAt(edition);
            bool devanagariBody = !wordByWord && record != null && record.lang == "hi";

            _attribution.text = wordByWord
                ? "WORD-BY-WORD MEANINGS"
                : record?.translator?.ToUpperInvariant() ?? "";

            _body.gameObject.SetActive(!devanagariBody);
            _bodyDevanagari.gameObject.SetActive(devanagariBody);

            if (devanagariBody)
            {
                _bodyDevanagari.SetText(bodyText);
            }
            else if (wordByWord)
            {
                _body.text = FormatWordByWord(verse.wm);
                _body.font = Theme.Sans;
            }
            else
            {
                _body.text = bodyText;
                _body.font = Theme.Serif;
            }

            PaintChips();
            UpdateAudioButton();
            UpdateSanskritButton();
            UpdateBookmarkButton();

            // Text length varies by an order of magnitude across the corpus, so the
            // layout has to be rebuilt before the scroll position means anything.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.verticalNormalizedPosition = 1f;

            AppSettings.RememberPosition(verse.c, verse.v);
            ReadingLog.MarkRead(verse.c, verse.v);

            if (speak && AppSettings.AudioEnabled) SpeakCurrent();
            else if (speak) { Narration.Stop(); AudioSession.End(); }
        }

        /// <summary>
        /// The gloss arrives as "word—meaning; word—meaning". Set each pair on its own
        /// line, with the Sanskrit term picked out.
        /// </summary>
        static string FormatWordByWord(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";

            var sb = new System.Text.StringBuilder();
            foreach (var pair in raw.Split(';'))
            {
                var entry = pair.Trim();
                if (entry.Length == 0) continue;

                int dash = entry.IndexOf('—');
                if (dash < 0) dash = entry.IndexOf('-');

                if (sb.Length > 0) sb.Append('\n');
                if (dash > 0)
                {
                    sb.Append("<color=#E8A33D>").Append(entry[..dash].Trim()).Append("</color>  ")
                      .Append(entry[(dash + 1)..].Trim());
                }
                else sb.Append(entry);
            }
            return sb.ToString();
        }
    }
}
