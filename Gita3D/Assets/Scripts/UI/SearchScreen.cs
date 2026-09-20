using UnityEngine;
using TMPro;
using Gita.App;
using Gita.Data;

namespace Gita.UI
{
    /// <summary>
    /// Search across the corpus. Matches the chosen translation, the transliteration
    /// and the Devanagari, so "karma", "karmany" and कर्म all find something.
    /// </summary>
    public sealed class SearchScreen : VerseListScreen
    {
        protected override string Title => "Search";
        protected override float HeaderExtra => 104f;

        TMP_InputField _field;
        string _pending;
        float _debounce;

        protected override void BuildHeaderExtra(RectTransform header)
        {
            var host = UIKit.Node("SearchBox", header)
                .BottomBand(bottom: 16f, height: 80f, inset: Theme.Gutter);

            _field = UIInput.Field("Field", host, "Search the Gita…",
                onChanged: v =>
                {
                    // Debounced: a scan of 701 verses on every keystroke is wasteful
                    // and makes typing feel heavy.
                    _pending = v;
                    _debounce = 0.22f;
                });
            ((RectTransform)_field.transform).Inset(0f, 0f, 0f, 0f);
        }

        public override void OnShow()
        {
            Show(System.Array.Empty<Verse>(),
                "Type a word or a phrase.\n\nTry “duty”, “fear”, “peace”\nor a reference like 2.47.");
            if (_field != null)
            {
                _field.text = "";
                _field.ActivateInputField();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (_debounce <= 0f) return;

            _debounce -= Time.unscaledDeltaTime;
            if (_debounce > 0f) return;

            Run(_pending);
        }

        void Run(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Show(System.Array.Empty<Verse>(),
                    "Type a word or a phrase.\n\nTry “duty”, “fear”, “peace”\nor a reference like 2.47.");
                return;
            }

            // A bare reference like "2.47" jumps straight to that verse.
            var direct = ParseReference(query);
            if (direct != null)
            {
                Show(new[] { direct }, "");
                return;
            }

            var hits = GitaDatabase.Search(query, AppSettings.Edition);
            Show(hits, $"Nothing found for “{query.Trim()}”.");
        }

        /// <summary>Recognises "2.47", "2:47" and "2 47" as a verse reference.</summary>
        static Verse ParseReference(string query)
        {
            var parts = query.Trim().Split('.', ':', ' ');
            if (parts.Length != 2) return null;
            if (!int.TryParse(parts[0], out int c) || !int.TryParse(parts[1], out int v)) return null;
            return GitaDatabase.GetVerse(c, v);
        }
    }
}
