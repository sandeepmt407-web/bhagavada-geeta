using System.Collections.Generic;

namespace Gita.Data
{
    /// <summary>
    /// Curated ways into the text for a reader who does not want to start at 1.1.
    ///
    /// Chosen by hand rather than by keyword search, and every reference was checked
    /// against the translation before being listed here - a search for "fear" returns
    /// verses that merely contain the word, which is not the same as a verse about it.
    /// </summary>
    public static class Themes
    {
        public sealed class Theme
        {
            public string Name;
            public string Blurb;
            public (int chapter, int verse)[] Verses;
        }

        public static readonly Theme[] All =
        {
            new()
            {
                Name = "Fear and anxiety",
                Blurb = "For the moment before a hard thing",
                Verses = new[] { (2, 3), (2, 40), (2, 56), (4, 10), (16, 1), (18, 66) },
            },
            new()
            {
                Name = "Duty and action",
                Blurb = "On doing the work in front of you",
                Verses = new[] { (2, 47), (2, 48), (3, 8), (3, 19), (3, 35), (18, 47) },
            },
            new()
            {
                Name = "Death and the self",
                Blurb = "What is born, what never was born",
                Verses = new[] { (2, 12), (2, 13), (2, 20), (2, 22), (2, 23), (2, 27) },
            },
            new()
            {
                Name = "Devotion",
                Blurb = "A leaf, a flower, a fruit, some water",
                Verses = new[] { (9, 22), (9, 26), (9, 34), (12, 6), (18, 65), (18, 66) },
            },
            new()
            {
                Name = "Knowledge",
                Blurb = "Nothing in this world purifies like it",
                Verses = new[] { (2, 29), (4, 38), (4, 39), (7, 19), (13, 8), (18, 63) },
            },
            new()
            {
                Name = "Peace of mind",
                Blurb = "For the unsteady, there is no peace",
                Verses = new[] { (2, 66), (2, 70), (2, 71), (5, 29), (6, 7), (6, 19) },
            },
            new()
            {
                Name = "Letting go",
                Blurb = "Attachment, desire, anger, and the way out",
                Verses = new[] { (2, 62), (2, 63), (2, 64), (5, 10), (6, 35), (12, 15) },
            },
            new()
            {
                Name = "The Supreme",
                Blurb = "Seated in the hearts of all beings",
                Verses = new[] { (7, 7), (9, 4), (10, 20), (10, 41), (11, 32), (15, 15) },
            },
        };

        /// <summary>Resolves a theme's references to verses, skipping any that are absent.</summary>
        public static List<Verse> VersesOf(Theme theme)
        {
            var result = new List<Verse>();
            if (theme?.Verses == null) return result;

            foreach (var (chapter, verse) in theme.Verses)
            {
                var v = GitaDatabase.GetVerse(chapter, verse);
                if (v != null) result.Add(v);
            }
            return result;
        }
    }
}
