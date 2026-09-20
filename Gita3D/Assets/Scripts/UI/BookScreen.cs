using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Gita.App;
using Gita.Data;
using Gita.World;

namespace Gita.UI
{
    /// <summary>
    /// The Gita read as a book: one chapter at a time, set as continuous prose with
    /// verse numbers in the margin of the line, and narrated straight through.
    ///
    /// The text is split into paragraphs of a few verses each rather than one block per
    /// chapter. That keeps each rasterised Devanagari block inside the texture limit,
    /// and it gives narration a natural unit to speak and highlight.
    /// </summary>
    public sealed class BookScreen : ScreenBase
    {
        public override Shot CameraShot => Shot.Contemplation;

        const int VersesPerParagraph = 4;

        int _chapter = 1;
        RectTransform _content;
        ScrollRect _scroll;
        TextMeshProUGUI _title, _translit, _position;
        Image _playIcon;
        TextMeshProUGUI _playLabel;

        class Paragraph
        {
            public string Text;          // what is displayed and spoken
            public int FirstVerse;
            public TextMeshProUGUI Tmp;  // set for Latin scripts
            public ShapedText Shaped;    // set for Devanagari
            public Image Highlight;

            public void Tint(Color c)
            {
                if (Tmp != null) Tmp.color = c;
                if (Shaped != null) Shaped.SetColor(c);
            }
        }

        readonly List<Paragraph> _paragraphs = new();

        // narration state
        bool _playing;
        int _speakingIndex = -1;
        float _startGuard;   // brief window after Speak before IsSpeaking is trusted

        public override void Build(RectTransform parent)
        {
            base.Build(parent);

            var scrim = UIKit.Panel("Scrim", Root, Theme.NightDeep.WithAlpha(0.93f), UIKit.Solid);
            scrim.rectTransform.Inset(0f, 0f, 0f, 0f);

            BuildHeader();
            BuildScroll();
            BuildFooter();
        }

        // ------------------------------------------------------------------

        void BuildHeader()
        {
            var header = UIKit.Node("Header", Root).TopBand(0f, 196f);
            var bg = UIKit.Panel("Bg", header, Theme.NightDeep.WithAlpha(0.98f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);

            var back = UIKit.Tappable("Back", header, () =>
            {
                StopNarration();
                AppRoot.Instance?.GoBack();
            });
            var rt = back.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(110f, 84f);
            rt.anchoredPosition = new Vector2(Theme.Gutter + 55f, -104f);
            var arrow = UIKit.Text("Arrow", back.transform, "←",
                Theme.Sans, 44f, Theme.Cream, TextAlignmentOptions.Center);
            arrow.rectTransform.Inset(0f, 0f, 0f, 0f);

            _title = UIKit.Text("Title", header, "",
                Theme.Serif, 32f, Theme.Cream, TextAlignmentOptions.Center);
            _title.rectTransform.TopBand(58f, 46f, Theme.Gutter + 130f);
            _title.overflowMode = TextOverflowModes.Ellipsis;

            _translit = UIKit.Text("Translit", header, "",
                Theme.Sans, 20f, Theme.Muted, TextAlignmentOptions.Center);
            _translit.rectTransform.TopBand(108f, 32f, Theme.Gutter + 130f);
            _translit.characterSpacing = 4f;

            var rule = UIKit.Panel("Rule", header, Theme.Saffron.WithAlpha(0.35f), UIKit.Solid);
            rule.rectTransform.BottomBand(0f, 2f);
        }

        void BuildScroll()
        {
            var viewport = UIKit.Node("Viewport", Root);
            viewport.Inset(0f, 196f, 0f, 168f);
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
            layout.padding = new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 44, 120);
            layout.spacing = 26f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Elastic;
            _scroll.elasticity = 0.1f;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = 28f;
        }

        void BuildFooter()
        {
            var footer = UIKit.Node("Footer", Root).BottomBand(0f, 168f);
            var bg = UIKit.Panel("Bg", footer, Theme.NightDeep.WithAlpha(0.98f), UIKit.Solid);
            bg.rectTransform.Inset(0f, 0f, 0f, 0f);
            var rule = UIKit.Panel("Rule", footer, Theme.Saffron.WithAlpha(0.22f), UIKit.Solid);
            rule.rectTransform.TopBand(0f, 1.5f);

            // previous chapter
            var prev = UIKit.Tappable("Prev", footer, () => GoChapter(_chapter - 1));
            var prt = prev.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(150f, 92f);
            prt.anchoredPosition = new Vector2(Theme.Gutter, 0f);
            var pl = UIKit.Text("L", prev.transform, "←", Theme.Sans, 38f, Theme.Cream,
                TextAlignmentOptions.Center);
            pl.rectTransform.Inset(0f, 0f, 0f, 0f);

            // play / pause narration
            var play = UIKit.Tappable("Play", footer, TogglePlay, Theme.Saffron);
            var playRt = play.GetComponent<RectTransform>();
            playRt.anchorMin = playRt.anchorMax = new Vector2(0.5f, 0.5f);
            playRt.pivot = new Vector2(0.5f, 0.5f);
            playRt.sizeDelta = new Vector2(320f, 96f);
            playRt.anchoredPosition = Vector2.zero;
            _playIcon = play.image;
            _playLabel = UIKit.Text("L", play.transform, "LISTEN",
                Theme.SansBold, 23f, Theme.NightDeep, TextAlignmentOptions.Center);
            _playLabel.rectTransform.Inset(0f, 0f, 0f, 0f);
            _playLabel.characterSpacing = 6f;

            // next chapter
            var next = UIKit.Tappable("Next", footer, () => GoChapter(_chapter + 1));
            var nrt = next.GetComponent<RectTransform>();
            nrt.anchorMin = nrt.anchorMax = new Vector2(1f, 0.5f);
            nrt.pivot = new Vector2(1f, 0.5f);
            nrt.sizeDelta = new Vector2(150f, 92f);
            nrt.anchoredPosition = new Vector2(-Theme.Gutter, 0f);
            var nl = UIKit.Text("L", next.transform, "→", Theme.Sans, 38f, Theme.Cream,
                TextAlignmentOptions.Center);
            nl.rectTransform.Inset(0f, 0f, 0f, 0f);

            _position = UIKit.Text("Pos", footer, "",
                Theme.Sans, 18f, Theme.Muted, TextAlignmentOptions.Center);
            _position.rectTransform.BottomBand(14f, 26f);
            _position.characterSpacing = 4f;
        }

        // ------------------------------------------------------------------

        public void Open(int chapter)
        {
            _chapter = Mathf.Clamp(chapter, 1, 18);
            if (IsVisible) Rebuild();
        }

        public override void OnShow()
        {
            Rebuild();
            AppSettings.Changed += OnSettingsChanged;
        }

        public override void OnHidden()
        {
            AppSettings.Changed -= OnSettingsChanged;
            StopNarration();
        }

        void OnSettingsChanged()
        {
            if (IsVisible) Rebuild();
        }

        void GoChapter(int chapter)
        {
            if (chapter < 1 || chapter > 18) return;
            StopNarration();
            _chapter = chapter;
            Rebuild();
            _scroll.verticalNormalizedPosition = 1f;
        }

        void Rebuild()
        {
            foreach (var p in _paragraphs)
            {
                if (p.Tmp != null) Destroy(p.Tmp.transform.parent.gameObject);
                else if (p.Shaped != null) Destroy(p.Shaped.gameObject);
            }
            _paragraphs.Clear();

            var chapter = GitaDatabase.GetChapter(_chapter);
            var verses = GitaDatabase.VersesOf(_chapter);
            if (chapter == null || verses.Count == 0) return;

            _title.text = chapter.translit;
            _translit.text = $"CHAPTER {_chapter} OF 18  ·  {chapter.meaning?.ToUpperInvariant()}";
            _position.text = $"{verses.Count} VERSES";

            int edition = AppSettings.Edition;
            bool devanagari = AppSettings.Language == "hi";

            // Chapter opening: the summary, set apart as an epigraph.
            string summary = devanagari && !string.IsNullOrWhiteSpace(chapter.summaryHi)
                ? chapter.summaryHi
                : chapter.summary;
            if (!string.IsNullOrWhiteSpace(summary))
                AddParagraph(summary, 0, devanagari, epigraph: true);

            // Body: verses grouped into paragraphs.
            var sb = new StringBuilder();
            int first = 0;
            int inGroup = 0;

            for (int i = 0; i < verses.Count; i++)
            {
                var verse = verses[i];
                string text = GitaDatabase.TextOf(verse, edition);
                if (string.IsNullOrWhiteSpace(text)) continue;

                if (inGroup == 0) first = verse.v;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append($"<color=#E8A33D><size=70%>{verse.v}</size></color>  ").Append(text);
                inGroup++;

                if (inGroup >= VersesPerParagraph || i == verses.Count - 1)
                {
                    AddParagraph(sb.ToString(), first, devanagari, epigraph: false);
                    sb.Clear();
                    inGroup = 0;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void AddParagraph(string text, int firstVerse, bool devanagari, bool epigraph)
        {
            var paragraph = new Paragraph { Text = StripMarkup(text), FirstVerse = firstVerse };
            var color = epigraph ? Theme.Muted : Theme.Parchment;

            if (devanagari)
            {
                // The platform shaper does not understand rich text, so the verse number
                // markup is dropped for Devanagari and the number is prefixed plainly.
                var shaped = ShapedText.Create($"P{firstVerse}", _content,
                    Theme.DevanagariUI, NativeText.SansDevanagari,
                    epigraph ? 24f : 28f, color, NativeText.AlignStart, lineSpacing: 1.45f);
                shaped.SetText(StripMarkup(text));
                paragraph.Shaped = shaped;
            }
            else
            {
                var host = UIKit.Node($"P{firstVerse}", _content);
                var t = UIKit.Text("Text", host, text,
                    epigraph ? Theme.Sans : Theme.Serif,
                    epigraph ? 24f : 29f, color, TextAlignmentOptions.TopLeft);
                t.lineSpacing = 14f;
                t.rectTransform.Inset(0f, 0f, 0f, 0f);

                // The host carries the layout height, measured from the text.
                var el = host.gameObject.AddComponent<LayoutElement>();
                el.preferredHeight = 10f;   // corrected below, once a width exists
                var sizer = host.gameObject.AddComponent<TextBlockSizer>();
                sizer.Bind(t, el);

                paragraph.Tmp = t;
            }

            _paragraphs.Add(paragraph);
        }

        /// <summary>Rich-text tags are for the eye, never for the speech engine.</summary>
        static string StripMarkup(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            var sb = new StringBuilder(s.Length);
            bool inTag = false;
            foreach (var ch in s)
            {
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (!inTag) sb.Append(ch);
            }
            return sb.ToString();
        }

        // ---- narration -------------------------------------------------------

        void TogglePlay()
        {
            if (_playing) StopNarration();
            else StartNarration(0);
        }

        void StartNarration(int index)
        {
            if (!Narration.Available)
            {
                _playLabel.text = "NO VOICE ON DEVICE";
                return;
            }
            if (index < 0 || index >= _paragraphs.Count) { StopNarration(); return; }

            _playing = true;
            SpeakParagraph(index);
            UpdatePlayButton();
        }

        void SpeakParagraph(int index)
        {
            if (index < 0 || index >= _paragraphs.Count) { StopNarration(); return; }

            if (_speakingIndex >= 0 && _speakingIndex < _paragraphs.Count)
                _paragraphs[_speakingIndex].Tint(Theme.Parchment);

            _speakingIndex = index;
            var paragraph = _paragraphs[index];
            paragraph.Tint(Theme.SaffronLit);

            var edition = AppSettings.Current;
            Narration.Speak(paragraph.Text, edition?.tts ?? "en-IN", AppSettings.SpeechRate);
            _startGuard = 0.6f;

            ScrollTo(index);
        }

        void ScrollTo(int index)
        {
            if (_paragraphs.Count <= 1 || _scroll == null) return;
            float t = index / (float)(_paragraphs.Count - 1);
            _scroll.verticalNormalizedPosition = Mathf.Clamp01(1f - t);
        }

        void StopNarration()
        {
            _playing = false;
            Narration.Stop();
            if (_speakingIndex >= 0 && _speakingIndex < _paragraphs.Count)
                _paragraphs[_speakingIndex].Tint(Theme.Parchment);
            _speakingIndex = -1;
            UpdatePlayButton();
        }

        void UpdatePlayButton()
        {
            if (_playLabel == null) return;
            _playLabel.text = _playing ? "PAUSE" : "LISTEN";
            if (_playIcon != null)
                _playIcon.color = _playing ? Theme.Terracotta : Theme.Saffron;
        }

        protected override void Update()
        {
            base.Update();
            if (!_playing) return;

            // IsSpeaking is false for a moment between the call and the engine starting.
            if (_startGuard > 0f)
            {
                _startGuard -= Time.unscaledDeltaTime;
                return;
            }

            if (!Narration.IsSpeaking)
                SpeakParagraph(_speakingIndex + 1);
        }
    }

    /// <summary>
    /// Drives a LayoutElement from a TMP block's preferred height. TextMeshProUGUI is
    /// itself a layout element, but here the text sits inside a host whose height the
    /// vertical group needs to know.
    /// </summary>
    public sealed class TextBlockSizer : MonoBehaviour
    {
        TextMeshProUGUI _text;
        LayoutElement _element;
        float _lastWidth = -1f;

        public void Bind(TextMeshProUGUI text, LayoutElement element)
        {
            _text = text;
            _element = element;
        }

        void OnRectTransformDimensionsChange() => Refresh();
        void OnEnable() => Refresh();

        void Refresh()
        {
            if (_text == null || _element == null) return;
            float w = ((RectTransform)transform).rect.width;
            if (w <= 1f || Mathf.Abs(w - _lastWidth) < 0.5f) return;
            _lastWidth = w;
            _element.preferredHeight = _text.GetPreferredValues(_text.text, w, 0f).y;
        }
    }
}
