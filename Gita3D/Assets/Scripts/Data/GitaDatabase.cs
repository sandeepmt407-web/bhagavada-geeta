using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gita.Data
{
    /// <summary>
    /// Loads the complete Gita corpus (18 chapters, 701 verses, every shipped
    /// translation) from Resources and indexes it for O(1) lookup.
    /// Load once at boot; read everywhere.
    /// </summary>
    public static class GitaDatabase
    {
        public static bool IsLoaded { get; private set; }
        public static Chapter[] Chapters { get; private set; } = Array.Empty<Chapter>();
        public static Verse[] Verses { get; private set; } = Array.Empty<Verse>();
        public static Edition[] Editions { get; private set; } = Array.Empty<Edition>();

        static readonly Dictionary<int, List<Verse>> _byChapter = new();
        static readonly Dictionary<int, int> _byRef = new();   // (c<<10|v) -> flat index
        static readonly Dictionary<string, int> _editionById = new();

        static int Key(int c, int v) => (c << 10) | v;

        public static void Load()
        {
            if (IsLoaded) return;

            var asset = Resources.Load<TextAsset>("gita");
            if (asset == null)
            {
                Debug.LogError("[Gita] Resources/gita.json is missing - the corpus cannot be loaded.");
                return;
            }

            GitaPayload payload;
            try
            {
                payload = JsonUtility.FromJson<GitaPayload>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gita] Failed to parse corpus: {e.Message}");
                return;
            }

            if (payload?.chapters == null || payload.verses == null || payload.editions == null)
            {
                Debug.LogError("[Gita] Corpus parsed but is missing chapters, verses or editions.");
                return;
            }

            Chapters = payload.chapters;
            Verses = payload.verses;
            Editions = payload.editions;

            _byChapter.Clear();
            _byRef.Clear();
            _editionById.Clear();

            for (int i = 0; i < Editions.Length; i++)
                _editionById[Editions[i].id] = i;

            for (int i = 0; i < Verses.Length; i++)
            {
                var verse = Verses[i];
                if (!_byChapter.TryGetValue(verse.c, out var list))
                    _byChapter[verse.c] = list = new List<Verse>();
                list.Add(verse);
                _byRef[Key(verse.c, verse.v)] = i;
            }

            IsLoaded = true;
            Debug.Log($"[Gita] Loaded {Chapters.Length} chapters, {Verses.Length} verses, " +
                      $"{Editions.Length} editions.");
        }

        // ---- editions ------------------------------------------------------

        public static int EditionIndex(string id) =>
            id != null && _editionById.TryGetValue(id, out var i) ? i : 0;

        public static Edition EditionAt(int index) =>
            (index >= 0 && index < Editions.Length) ? Editions[index] : null;

        /// <summary>Distinct languages, in the order their first edition appears.</summary>
        public static List<(string lang, string label)> Languages()
        {
            var seen = new HashSet<string>();
            var result = new List<(string, string)>();
            foreach (var e in Editions)
                if (seen.Add(e.lang)) result.Add((e.lang, e.langLabel));
            return result;
        }

        /// <summary>Every edition available in a language, in shipped order.</summary>
        public static List<int> EditionsFor(string lang)
        {
            var result = new List<int>();
            for (int i = 0; i < Editions.Length; i++)
                if (Editions[i].lang == lang) result.Add(i);
            return result;
        }

        /// <summary>
        /// A second reading of the same language, offered beside the main translation.
        /// Prefers the one flagged poetic; otherwise any other edition in that language.
        /// Returns -1 when the language ships only one translation.
        /// </summary>
        public static int AlternateEditionFor(string lang, int exclude)
        {
            int fallback = -1;
            for (int i = 0; i < Editions.Length; i++)
            {
                if (Editions[i].lang != lang || i == exclude) continue;
                if (Editions[i].poetic) return i;
                if (fallback < 0) fallback = i;
            }
            return fallback;
        }

        /// <summary>The first edition in a language, or -1 if that language is not shipped.</summary>
        public static int FirstEditionOf(string lang)
        {
            for (int i = 0; i < Editions.Length; i++)
                if (Editions[i].lang == lang) return i;
            return -1;
        }

        public static string TextOf(Verse verse, int edition) => TextOf(verse, edition, out _);

        /// <summary>
        /// The translated text of a verse, with a fallback for the occasional gap - some
        /// translators follow a recension that omits a verse the Sanskrit includes.
        /// <paramref name="actual"/> reports which edition was really used, so the
        /// attribution on screen never credits a translator who did not write the line.
        /// </summary>
        public static string TextOf(Verse verse, int edition, out int actual)
        {
            actual = edition;
            if (verse?.t == null || verse.t.Length == 0) return "";

            if (edition >= 0 && edition < verse.t.Length && !string.IsNullOrWhiteSpace(verse.t[edition]))
                return verse.t[edition];

            // Prefer another edition in the same language before crossing languages.
            string wanted = EditionAt(edition)?.lang;
            for (int i = 0; i < verse.t.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(verse.t[i])) continue;
                if (Editions[i].lang != wanted) continue;
                actual = i;
                return verse.t[i];
            }

            for (int i = 0; i < verse.t.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(verse.t[i])) continue;
                actual = i;
                return verse.t[i];
            }
            return "";
        }

        // ---- lookup --------------------------------------------------------

        public static Chapter GetChapter(int number)
        {
            foreach (var c in Chapters)
                if (c.number == number) return c;
            return null;
        }

        public static IReadOnlyList<Verse> VersesOf(int chapter) =>
            _byChapter.TryGetValue(chapter, out var list) ? list : (IReadOnlyList<Verse>)Array.Empty<Verse>();

        public static Verse GetVerse(int chapter, int verse) =>
            _byRef.TryGetValue(Key(chapter, verse), out var i) ? Verses[i] : null;

        /// <summary>Flat index into the whole corpus, for paging across chapter ends.</summary>
        public static int FlatIndexOf(int chapter, int verse) =>
            _byRef.TryGetValue(Key(chapter, verse), out var i) ? i : -1;

        public static Verse AtFlatIndex(int i) =>
            (i >= 0 && i < Verses.Length) ? Verses[i] : null;

        /// <summary>A stable verse of the day - the same verse for everyone on a date.</summary>
        public static Verse VerseOfTheDay(DateTime date)
        {
            if (Verses.Length == 0) return null;
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                return Verses[(int)(h % (uint)Verses.Length)];
            }
        }

        /// <summary>Substring search across the Sanskrit, transliteration and one edition.</summary>
        public static List<Verse> Search(string query, int edition, int limit = 60)
        {
            var results = new List<Verse>();
            if (string.IsNullOrWhiteSpace(query)) return results;
            query = query.Trim();

            foreach (var verse in Verses)
            {
                if (results.Count >= limit) break;
                if (Contains(TextOf(verse, edition), query) ||
                    Contains(verse.tr, query) ||
                    Contains(verse.sa, query))
                    results.Add(verse);
            }
            return results;
        }

        static bool Contains(string haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) &&
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
