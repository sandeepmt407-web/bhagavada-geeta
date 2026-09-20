using System;
using UnityEngine;
using Gita.Data;
using Gita.UI;

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
        const string KeyTextScale = "gita.textScale";
        const string KeyMusic     = "gita.musicEnabled";
        const string KeyVoice     = "gita.voice.";   // + language code
        const string KeySanskrit  = "gita.narrateSanskrit";

        /// <summary>Raised whenever a setting changes, so open screens can refresh.</summary>
        public static event Action Changed;

        static int _edition;
        static bool _audioEnabled;
        static float _speechRate;
        static float _textScale;
        static bool _musicEnabled;
        static bool _narrateSanskrit;
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



        /// <summary>
        /// The bansuri bed under the whole app. Independent of <see cref="AudioEnabled"/>:
        /// wanting the music without a voice reading to you, or a voice without the
        /// music, are both perfectly ordinary and neither should imply the other.
        /// </summary>
        public static bool MusicEnabled
        {
            get { EnsureLoaded(); return _musicEnabled; }
            set
            {
                EnsureLoaded();
                if (value == _musicEnabled) return;
                _musicEnabled = value;
                PlayerPrefs.SetInt(KeyMusic, value ? 1 : 0);
                PlayerPrefs.Save();
                Ambience.Instance?.Refresh();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The voice this app ships with as its default for English.
        ///
        /// One of Google's India-locale network voices, chosen by listening rather than
        /// by score. It is only a preference: a device that does not carry it falls back
        /// to the best voice it does have, and because it needs a connection the narrator
        /// retries offline if the engine fails mid-verse.
        /// </summary>
        public const string DefaultEnglishVoice = "en-in-x-ena-network";

        /// <summary>
        /// The voice the reader chose for a language, or the shipped default.
        ///
        /// Stored per language because the choice does not carry over: an English voice
        /// is no use to the Hindi tab, and the reader switching language should not have
        /// to choose again every time they switch back.
        /// </summary>
        public static string VoiceFor(string lang)
        {
            string key = KeyVoice + (lang ?? "en");
            if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetString(key, "");

            // Unset means nobody has chosen yet, which is not the same as having chosen
            // "best available" - that choice is stored as an empty string.
            return (lang ?? "en") == "en" ? DefaultEnglishVoice : "";
        }

        public static void SetVoiceFor(string lang, string voiceName)
        {
            PlayerPrefs.SetString(KeyVoice + (lang ?? "en"), voiceName ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }


        /// <summary>
        /// Whether narration reads the shloka or what it means.
        ///
        /// This is a standing choice, not a per-page one: whichever is set, every verse
        /// opens reading that, so nobody has to press play on each page. The control for
        /// it sits beside the Sanskrit on the verse itself, because that is the thing it
        /// decides the fate of.
        /// </summary>
        public static bool NarrateSanskrit
        {
            get { EnsureLoaded(); return _narrateSanskrit; }
            set
            {
                EnsureLoaded();
                if (value == _narrateSanskrit) return;
                _narrateSanskrit = value;
                PlayerPrefs.SetInt(KeySanskrit, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// The voice locale for the Sanskrit.
        ///
        /// Android has no Sanskrit voice - there is no sa-IN - so the shloka is read by a
        /// Hindi one, which at least shares the script. Hindi pronunciation rules are not
        /// Sanskrit's, and anyone who knows the difference will hear it.
        /// </summary>
        public const string SanskritTts = "hi-IN";
        public const string SanskritVoiceLang = "hi";
        // ---- text size ------------------------------------------------------

        /// <summary>The size the app ships at, before anyone changes it.</summary>
        public const float DefaultTextScale = 1.18f;   // "Large"

        /// <summary>The four steps offered on the settings screen.</summary>
        public static readonly (string label, float scale)[] TextSizes =
        {
            ("Small",  0.88f),
            ("Medium", 1.00f),
            ("Large",  1.18f),
            ("Larger", 1.40f),
        };

        /// <summary>
        /// Multiplies the whole type scale. Medium is the authored size; the steps
        /// either side exist because reading comfort is not the same for everyone and
        /// scripture in particular gets read by people who have been reading a long time.
        /// </summary>
        public static float TextScale
        {
            get { EnsureLoaded(); return _textScale; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp(value, 0.85f, 1.6f);
                if (Mathf.Approximately(clamped, _textScale)) return;
                _textScale = clamped;
                PlayerPrefs.SetFloat(KeyTextScale, _textScale);
                PlayerPrefs.Save();
                Theme.SetTextScale(_textScale);
                Changed?.Invoke();
            }
        }

        /// <summary>Index into <see cref="TextSizes"/> nearest the stored scale.</summary>
        public static int TextSizeStep
        {
            get
            {
                float s = TextScale;
                int best = 1;
                float bestGap = float.MaxValue;
                for (int i = 0; i < TextSizes.Length; i++)
                {
                    float gap = Mathf.Abs(TextSizes[i].scale - s);
                    if (gap >= bestGap) continue;
                    bestGap = gap;
                    best = i;
                }
                return best;
            }
        }

        /// <summary>Pushes the stored size into the type scale. Called once at boot.</summary>
        public static void ApplyTextScale()
        {
            EnsureLoaded();
            Theme.SetTextScale(_textScale);
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
            _musicEnabled = PlayerPrefs.GetInt(KeyMusic, 1) == 1;    // and the bed under it
            _narrateSanskrit = PlayerPrefs.GetInt(KeySanskrit, 0) == 1;
            _speechRate = PlayerPrefs.GetFloat(KeyRate, 0.88f);      // a shade slower than default
            // Large is the shipped default, not Medium: this text gets read for a long
            // sitting, often by people who have been reading a long time.
            _textScale = Mathf.Clamp(
                PlayerPrefs.GetFloat(KeyTextScale, DefaultTextScale), 0.85f, 1.6f);
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
