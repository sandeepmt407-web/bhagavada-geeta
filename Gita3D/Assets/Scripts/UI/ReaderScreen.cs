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
    /// </summary>
    public sealed class ReaderScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Reader;

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
        TextMeshProUGUI _muteGlyph, _bookmarkGlyph;
        Image _muteBg, _bookmarkBg;
        ShapedText _sanskrit, _bodyDevanagari;
        RectTransform _content, _chipBar;
        ScrollRect _scroll;

        readonly List<TabSpec> _tabs = new();
        readonly List<GameObject> _chipObjects = new();

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            var scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.86f), UIKit.Solid);
            scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            BuildHeader();
            BuildScroll();
            _chipBar = UIKit.Node("Chips", Root)
                .BottomBand(bottom: 158f, height: 108f, inset: Theme.Gutter);
            BuildFooter();
        }

        // ------------------------------------------------------------------

        void BuildHeader()
        {
            var header = UIKit.Node("Header", Root).TopBand(0f, 190f);

            var bg = UIKit.Panel("Bg", header, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", header, () => AppRoot.Instance?.GoBack());
            var backRt = back.GetComponent<RectTransform>();
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0.5f, 0.5f);
            backRt.sizeDelta = new Vector2(104f, 84f);
            backRt.anchoredPosition = new Vector2(Theme.Gutter + 52f, -106f);
            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 44f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            // Three actions on the right, in order of how often they are wanted:
            // mute nearest the thumb, then share, then the bookmark.
            var mute = HeaderButton(header, "Mute", 42f, ToggleMute);
            _muteBg = mute.image;
            _muteGlyph = UIKit.Text("Glyph", mute.transform, "",
                Theme.Sans, 32f, Theme.Cream, TextAlignmentOptions.Center);
            _muteGlyph.rectTransform.Inset(0f, 0f, 0f, 0f);

            var share = HeaderButton(header, "Share", 130f, ShareCurrent);
            var shareGlyph = UIKit.Text("Glyph", share.transform, "↗",
                Theme.Sans, 32f, Theme.Cream, TextAlignmentOptions.Center);
            shareGlyph.rectTransform.Inset(0f, 0f, 0f, 0f);

            var mark = HeaderButton(header, "Bookmark", 218f, ToggleBookmark);
            _bookmarkBg = mark.image;
            _bookmarkGlyph = UIKit.Text("Glyph", mark.transform, "",
                Theme.Sans, 30f, Theme.Cream, TextAlignmentOptions.Center);
            _bookmarkGlyph.rectTransform.Inset(0f, 0f, 0f, 0f);

            _chapterTitle = UIKit.Text("Chapter", header, "",
                Theme.Serif, 29f, Theme.Cream, TextAlignmentOptions.Center);
            _chapterTitle.rectTransform.TopBand(64f, 42f, Theme.Gutter + 272f);
            _chapterTitle.overflowMode = TextOverflowModes.Ellipsis;

            _position = UIKit.Text("Position", header, "",
                Theme.Sans, 20f, Theme.Muted, TextAlignmentOptions.Center);
            _position.rectTransform.TopBand(110f, 32f, Theme.Gutter + 272f);
            _position.characterSpacing = 3f;

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
            rt.sizeDelta = new Vector2(80f, 82f);
            rt.anchoredPosition = new Vector2(-Theme.Gutter - inset, -106f);
            return btn;
        }

        void BuildScroll()
        {
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, 190f, 0f, 300f);
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

            _reference = UIKit.Text("Reference", _content, "",
                Theme.SansBold, Theme.SizeCaption, Theme.Saffron, TextAlignmentOptions.Center);
            _reference.characterSpacing = 8f;

            _sanskrit = ShapedText.Create("Sanskrit", _content,
                Theme.Devanagari, NativeText.SerifDevanagari, Theme.SizeVerseSa,
                Theme.Cream, NativeText.AlignCenter, lineSpacing: 1.28f);

            _translit = UIKit.Text("Translit", _content, "",
                Theme.Serif, 26f, Theme.Muted, TextAlignmentOptions.Center);
            _translit.lineSpacing = 8f;
            _translit.fontStyle = FontStyles.Italic;

            AddRule(_content);

            _attribution = UIKit.Text("Attribution", _content, "",
                Theme.Sans, 19f, Theme.Saffron.WithAlpha(0.7f), TextAlignmentOptions.Center);
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
            var footer = UIKit.Node("Footer", Root).BottomBand(0f, 150f);

            var bg = UIKit.Panel("Bg", footer, Theme.NightDeep.WithAlpha(0.97f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var rule = UIKit.Panel("Rule", footer, Theme.Saffron.WithAlpha(0.22f), UIKit.Solid);
            rule.rectTransform.TopBand(0f, 1.5f);

            MakeNavButton(footer, "Prev", "← Previous", 0f, Previous);
            MakeNavButton(footer, "Next", "Next →", 1f, Next);
        }

        void MakeNavButton(RectTransform parent, string name, string label, float anchorX,
            System.Action action)
        {
            var btn = UIKit.Tappable(name, parent, action);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.5f);
            rt.pivot = new Vector2(anchorX, 0.5f);
            rt.sizeDelta = new Vector2(320f, 96f);
            rt.anchoredPosition = new Vector2(anchorX == 0f ? Theme.Gutter : -Theme.Gutter, 0f);

            var text = UIKit.Text("Label", btn.transform, label,
                Theme.SansBold, 24f, Theme.Cream, TextAlignmentOptions.Center);
            text.rectTransform.Inset(0f, 0f, 0f, 0f);
            text.characterSpacing = 3f;
        }

        // ------------------------------------------------------------------
        // tabs
        // ------------------------------------------------------------------

        /// <summary>
        /// Works out which readings this language can offer, then lays out one chip per
        /// reading plus a narrow speaker. Rebuilt whenever the language changes.
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
            const float speakerWeight = 0.5f;   // the speaker chip is icon-only
            float total = _tabs.Count + speakerWeight;
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
                    Theme.Sans, _tabs.Count >= 4 ? 18f : 20f, Theme.Parchment,
                    TextAlignmentOptions.Center);
                text.rectTransform.Inset(4f, 0f, 4f, 0f);
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.textWrappingMode = TextWrappingModes.NoWrap;

                _chipObjects.Add(cell.gameObject);
            }

            // Speak the verse now, whatever the auto-narration setting is.
            var speakerCell = Cell(cursor, speakerWeight, total, gap);
            var speaker = UIKit.Tappable("Listen", speakerCell, SpeakCurrent,
                Theme.TwilightLit.WithAlpha(0.7f));
            speaker.GetComponent<RectTransform>().Inset(0f, 0f, 0f, 0f);
            var glyph = UIKit.Text("Glyph", speaker.transform, "▶",
                Theme.Sans, 22f, Theme.Saffron, TextAlignmentOptions.Center);
            glyph.rectTransform.Inset(0f, 0f, 0f, 0f);
            _chipObjects.Add(speakerCell.gameObject);
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
            Refresh();
        }

        void Previous()
        {
            if (_flatIndex <= 0) return;
            _flatIndex--;
            Refresh();
        }

        void SetTab(Tab tab)
        {
            _tab = tab;
            Refresh(speak: false);
        }

        void ToggleMute()
        {
            AppSettings.AudioEnabled = !AppSettings.AudioEnabled;
            if (!AppSettings.AudioEnabled) { Narration.Stop(); AudioSession.End(); }
            else SpeakCurrent();
            UpdateMuteButton();
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

        void UpdateMuteButton()
        {
            bool on = AppSettings.AudioEnabled;
            if (_muteGlyph != null)
            {
                _muteGlyph.text = on ? "♪" : "✕";
                _muteGlyph.color = on ? Theme.Saffron : Theme.Muted;
            }
            if (_muteBg != null)
                _muteBg.color = on ? Theme.Saffron.WithAlpha(0.22f) : Theme.TwilightLit.WithAlpha(0.7f);
        }

        /// <summary>The edition currently on screen, falling back to the chosen one.</summary>
        int ActiveEdition()
        {
            foreach (var spec in _tabs)
                if (spec.Kind == _tab && spec.Edition >= 0) return spec.Edition;
            return AppSettings.Edition;
        }

        void SpeakCurrent()
        {
            var verse = GitaDatabase.AtFlatIndex(_flatIndex);
            if (verse == null) return;

            // Read whatever is on screen, in that edition's own language - switching to
            // the Hindi tab should give a Hindi voice, not an English one.
            int edition = ActiveEdition();
            string text = GitaDatabase.TextOf(verse, edition, out edition);
            if (string.IsNullOrWhiteSpace(text)) return;

            // Use the edition the text actually came from, so a fallback is not read
            // aloud in the wrong language's voice.
            var record = GitaDatabase.EditionAt(edition);
            Narration.Speak(text, record?.tts ?? "en-IN", AppSettings.SpeechRate);
            AudioSession.Begin();   // hold the screen awake while a verse is read
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
            UpdateMuteButton();
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
