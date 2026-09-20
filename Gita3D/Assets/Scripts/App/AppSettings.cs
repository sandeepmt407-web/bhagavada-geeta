using System;
using UnityEngine;
using Gita.Data;

namespace Gita.App
{
    /// <summary>
    /// Everything the reader chooses, persisted across sessions. Small enough for
    /// PlayerPrefs, and PlayerPrefs is the one store guaranteed present on Android.
    /// </summary>
    public static class AppSettings
    {
        const string KeyEdition   = "gita.edition";
        const string KeyChosen    = "gita.languageChosen";
        const string KeyAudio     = "gita.audioEnabled";
        const string KeyRate      = "gita.speechRate";
        const string KeyLastC     = "gita.lastChapter";
        const string KeyLastV     = "gita.lastVerse";

        /// <summary>Raised whenever a setting changes, so open screens can refresh.</summary>
        public static event Action Changed;

        static int _edition;
        static bool _audioEnabled;
        static float _speechRate;
        static bool _loaded;

        /// <summary>Index into <see cref="GitaDatabase.Editions"/>.</summary>
        public static int Edition
        {
            get { EnsureLoaded(); return _edition; }
            set
            {
                EnsureLoaded();
                int clamped = Mathf.Clamp(value, 0, Mathf.Max(0, GitaDatabase.Editions.Length - 1));
                if (clamped == _edition) return;
                _edition = clamped;
                PlayerPrefs.SetInt(KeyEdition, _edition);
                PlayerPrefs.SetInt(KeyChosen, 1);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>The current edition record, or null before the corpus is loaded.</summary>
        public static Edition Current => GitaDatabase.EditionAt(Edition);

        public static string Language => Current?.lang ?? "en";

        /// <summary>False until the reader has been through the language screen once.</summary>
        public static bool HasChosenLanguage
        {
            get { EnsureLoaded(); return PlayerPrefs.GetInt(KeyChosen, 0) == 1; }
        }

        /// <summary>
        /// Records that the reader has seen and dismissed the language screen.
        ///
        /// Dismissing counts even if nothing was changed: accepting the default is a
        /// choice. Setting this only inside the Edition setter meant anyone happy with
        /// the default was asked again on every launch.
        /// </summary>
        public static void MarkLanguageChosen()
        {
            EnsureLoaded();
            if (PlayerPrefs.GetInt(KeyChosen, 0) == 1) return;
            PlayerPrefs.SetInt(KeyChosen, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Speak each verse automatically as it opens.</summary>
        public static bool AudioEnabled
        {
            get { EnsureLoaded(); return _audioEnabled; }
            set
            {
                EnsureLoaded();
                if (value == _audioEnabled) return;
                _audioEnabled = value;
                PlayerPrefs.SetInt(KeyAudio, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>Narration speed. 1 is the engine default; scripture reads better slower.</summary>
        public static float SpeechRate
        {
            get { EnsureLoaded(); return _speechRate; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp(value, 0.5f, 1.5f);
                if (Mathf.Approximately(clamped, _speechRate)) return;
                _speechRate = clamped;
                PlayerPrefs.SetFloat(KeyRate, _speechRate);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        // ---- reading position ---------------------------------------------

        public static void RememberPosition(int chapter, int verse)
        {
            PlayerPrefs.SetInt(KeyLastC, chapter);
            PlayerPrefs.SetInt(KeyLastV, verse);
            PlayerPrefs.Save();
        }

        public static bool TryGetPosition(out int chapter, out int verse)
        {
            chapter = PlayerPrefs.GetInt(KeyLastC, 0);
            verse = PlayerPrefs.GetInt(KeyLastV, 0);
            return chapter > 0 && verse > 0;
        }

        // ---- first run ------------------------------------------------------

        /// <summary>
        /// Picks a sensible default from the device locale, so the first screen is
        /// already in the reader's language before they touch anything.
        /// </summary>
        public static void ApplyDeviceDefault()
        {
            EnsureLoaded();
            if (HasChosenLanguage || GitaDatabase.Editions.Length == 0) return;

            string want = Application.systemLanguage == SystemLanguage.Hindi ? "hi" : "en";
            for (int i = 0; i < GitaDatabase.Editions.Length; i++)
            {
                if (GitaDatabase.Editions[i].lang != want) continue;
                _edition = i;
                PlayerPrefs.SetInt(KeyEdition, i);
                PlayerPrefs.Save();
                return;
            }
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _edition = PlayerPrefs.GetInt(KeyEdition, 0);
            _audioEnabled = PlayerPrefs.GetInt(KeyAudio, 1) == 1;   // narration on by default
            _speechRate = PlayerPrefs.GetFloat(KeyRate, 0.85f);      // a shade slower than default
        }

        /// <summary>Re-clamps the stored edition once the corpus is known.</summary>
        public static void Validate()
        {
            EnsureLoaded();
            if (GitaDatabase.Editions.Length == 0) return;
            _edition = Mathf.Clamp(_edition, 0, GitaDatabase.Editions.Length - 1);
        }
    }
}
