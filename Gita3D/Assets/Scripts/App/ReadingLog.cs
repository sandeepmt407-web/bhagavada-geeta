using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Gita.Data;

namespace Gita.App
{
    /// <summary>
    /// What the reader has marked and what they have been through.
    ///
    /// Bookmarks are a short list, so they are stored as plain references. Progress
    /// covers all 701 verses, so it is a bit per verse packed into base64 - about
    /// 120 characters, rather than a comma-separated list that would grow to kilobytes.
    /// </summary>
    public static class ReadingLog
    {
        const string KeyBookmarks = "gita.bookmarks";
        const string KeyProgress = "gita.progress";

        /// <summary>Raised when a bookmark is added or removed, or a verse is marked read.</summary>
        public static event Action Changed;

        static readonly HashSet<int> _bookmarks = new();   // flat corpus indices
        static byte[] _progress;                            // one bit per verse
        static bool _loaded;

        // ---- bookmarks -----------------------------------------------------

        public static bool IsBookmarked(int chapter, int verse)
        {
            EnsureLoaded();
            int i = GitaDatabase.FlatIndexOf(chapter, verse);
            return i >= 0 && _bookmarks.Contains(i);
        }

        /// <summary>Adds or removes a bookmark. Returns the state afterwards.</summary>
        public static bool ToggleBookmark(int chapter, int verse)
        {
            EnsureLoaded();
            int i = GitaDatabase.FlatIndexOf(chapter, verse);
            if (i < 0) return false;

            bool on;
            if (_bookmarks.Contains(i)) { _bookmarks.Remove(i); on = false; }
            else { _bookmarks.Add(i); on = true; }

            SaveBookmarks();
            Changed?.Invoke();
            return on;
        }

        /// <summary>Bookmarked verses in corpus order.</summary>
        public static List<Verse> Bookmarks()
        {
            EnsureLoaded();
            var indices = new List<int>(_bookmarks);
            indices.Sort();

            var result = new List<Verse>(indices.Count);
            foreach (int i in indices)
            {
                var v = GitaDatabase.AtFlatIndex(i);
                if (v != null) result.Add(v);
            }
            return result;
        }

        public static int BookmarkCount
        {
            get { EnsureLoaded(); return _bookmarks.Count; }
        }

        // ---- progress ------------------------------------------------------

        /// <summary>Records that a verse has been read. Cheap enough to call on every view.</summary>
        public static void MarkRead(int chapter, int verse)
        {
            EnsureLoaded();
            int i = GitaDatabase.FlatIndexOf(chapter, verse);
            if (i < 0 || _progress == null) return;

            int b = i >> 3, bit = 1 << (i & 7);
            if (b >= _progress.Length || (_progress[b] & bit) != 0) return;  // already read

            _progress[b] |= (byte)bit;
            SaveProgress();
            Changed?.Invoke();
        }

        public static bool HasRead(int chapter, int verse)
        {
            EnsureLoaded();
            int i = GitaDatabase.FlatIndexOf(chapter, verse);
            if (i < 0 || _progress == null) return false;
            int b = i >> 3;
            return b < _progress.Length && (_progress[b] & (1 << (i & 7))) != 0;
        }

        /// <summary>How many verses have been read, across the whole corpus.</summary>
        public static int ReadCount
        {
            get
            {
                EnsureLoaded();
                if (_progress == null) return 0;
                int n = 0;
                foreach (byte b in _progress)
                {
                    // Brian Kernighan's count: loops once per set bit, not per bit.
                    byte v = b;
                    while (v != 0) { v &= (byte)(v - 1); n++; }
                }
                return n;
            }
        }

        /// <summary>How many verses of one chapter have been read.</summary>
        public static int ReadCountIn(int chapter)
        {
            EnsureLoaded();
            int n = 0;
            foreach (var verse in GitaDatabase.VersesOf(chapter))
                if (HasRead(verse.c, verse.v)) n++;
            return n;
        }

        public static void ResetProgress()
        {
            EnsureLoaded();
            if (_progress != null) Array.Clear(_progress, 0, _progress.Length);
            SaveProgress();
            Changed?.Invoke();
        }

        // ---- persistence ---------------------------------------------------

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            int total = Mathf.Max(GitaDatabase.Verses.Length, 1);
            _progress = new byte[(total + 7) >> 3];

            var packed = PlayerPrefs.GetString(KeyProgress, "");
            if (!string.IsNullOrEmpty(packed))
            {
                try
                {
                    var bytes = Convert.FromBase64String(packed);
                    Array.Copy(bytes, _progress, Mathf.Min(bytes.Length, _progress.Length));
                }
                catch (FormatException)
                {
                    Debug.LogWarning("[Gita] Reading progress was unreadable and has been reset.");
                }
            }

            var marks = PlayerPrefs.GetString(KeyBookmarks, "");
            if (string.IsNullOrEmpty(marks)) return;
            foreach (var part in marks.Split(','))
                if (int.TryParse(part, out int i) && i >= 0 && i < total)
                    _bookmarks.Add(i);
        }

        static void SaveBookmarks()
        {
            var sb = new StringBuilder();
            foreach (int i in _bookmarks)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(i);
            }
            PlayerPrefs.SetString(KeyBookmarks, sb.ToString());
            PlayerPrefs.Save();
        }

        static void SaveProgress()
        {
            if (_progress == null) return;
            PlayerPrefs.SetString(KeyProgress, Convert.ToBase64String(_progress));
            PlayerPrefs.Save();
        }
    }
}
