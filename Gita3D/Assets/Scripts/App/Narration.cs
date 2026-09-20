using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gita.App
{
    /// <summary>
    /// Speaks verses through Android's TextToSpeech engine.
    ///
    /// Everything here degrades quietly: on a device with no engine, on a language with
    /// no installed voice, or on any platform that is not Android, the calls become
    /// no-ops and <see cref="Available"/> reports false so the UI can hide its controls.
    /// </summary>
    public static class Narration
    {
        /// <summary>Voice-data availability for a locale.</summary>
        public enum VoiceStatus { Unsupported = 0, NeedsDownload = 1, Ready = 2 }

        static bool _initCalled;
        static bool _unavailable;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass _narrator;
#endif

        /// <summary>True once the engine has started and is usable.</summary>
        public static bool Available
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_unavailable) return false;
                try { return _narrator != null && _narrator.CallStatic<bool>("isReady"); }
                catch { _unavailable = true; return false; }
#else
                return false;
#endif
            }
        }

        public static bool IsSpeaking
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_unavailable || _narrator == null) return false;
                try { return _narrator.CallStatic<bool>("isSpeaking"); }
                catch { return false; }
#else
                return false;
#endif
            }
        }


        /// <summary>
        /// The voice the engine settled on, empty when unknown. Shown on the settings
        /// screen: it is the one piece of evidence a reader has about why narration
        /// sounds the way it does, and which voice to replace if they want better.
        /// </summary>
        public static string VoiceName
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (_unavailable || _narrator == null) return "";
                try { return _narrator.CallStatic<string>("voiceName") ?? ""; }
                catch { return ""; }
#else
                return "";
#endif
            }
        }
        /// <summary>Starts the engine. Safe to call more than once; returns immediately.</summary>
        public static void Init()
        {
            if (_initCalled) return;
            _initCalled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                _narrator = new AndroidJavaClass("com.gita.audio.Narrator");
                _narrator.CallStatic("init");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Narration unavailable ({e.GetType().Name}) - continuing silently.");
                _unavailable = true;
            }
#endif
        }

        /// <summary>Whether the device can actually speak a given locale.</summary>
        public static VoiceStatus StatusOf(string locale)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return VoiceStatus.Unsupported;
            try { return (VoiceStatus)_narrator.CallStatic<int>("localeStatus", locale ?? ""); }
            catch { return VoiceStatus.Unsupported; }
#else
            return VoiceStatus.Unsupported;
#endif
        }


        /// <summary>One voice the device can speak with.</summary>
        public readonly struct VoiceOption
        {
            public readonly string Name;      // engine identifier, e.g. en-in-x-ahp-local
            public readonly string Locale;    // en_IN
            public readonly int Quality;      // 100 very low .. 500 very high
            public readonly bool NeedsNetwork;

            public VoiceOption(string name, string locale, int quality, bool needsNetwork)
            {
                Name = name; Locale = locale; Quality = quality; NeedsNetwork = needsNetwork;
            }

            /// <summary>
            /// A name a reader can choose between. The engine's own identifiers are
            /// things like "en-in-x-ahp-local", which say nothing to anyone, so they are
            /// numbered and described by what actually distinguishes them.
            /// </summary>
            public string Describe(int index)
            {
                string grade = Quality >= 500 ? "Very high quality"
                             : Quality >= 400 ? "High quality"
                             : Quality >= 300 ? "Standard quality"
                             : "Basic quality";
                return $"Voice {index + 1}  ·  {grade}";
            }

            public string Detail => NeedsNetwork
                ? "Needs a connection"
                : "Works offline";
        }

        /// <summary>
        /// The voices installed on this device for a language.
        ///
        /// There is no published list of what any given phone carries - it depends on
        /// the engine, the Android version and which voice data the owner installed - so
        /// this asks the device rather than assuming. With <paramref name="indiaOnly"/>
        /// the list is narrowed to India-locale voices, and falls back to the language's
        /// other voices if the device has none.
        /// </summary>
        public static List<VoiceOption> Voices(string localeTag, bool indiaOnly = true)
        {
            var list = new List<VoiceOption>();

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return list;
            try
            {
                string raw = _narrator.CallStatic<string>("listVoices", localeTag ?? "", indiaOnly);
                Parse(raw, list);

                // A language with no India-locale voice on this device would otherwise
                // offer nothing at all, which is worse than offering what it has.
                if (list.Count == 0 && indiaOnly)
                {
                    raw = _narrator.CallStatic<string>("listVoices", localeTag ?? "", false);
                    Parse(raw, list);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Could not list voices: {e.Message}");
            }
#endif
            return list;
        }

        static void Parse(string raw, List<VoiceOption> into)
        {
            if (string.IsNullOrEmpty(raw)) return;

            foreach (var line in raw.Split('\n'))
            {
                var f = line.Split('|');
                if (f.Length < 4) continue;
                int.TryParse(f[2], out int quality);
                into.Add(new VoiceOption(f[0], f[1], quality, f[3] == "1"));
            }

            // Best first: quality descending, and offline ahead of network at equal
            // quality, because a voice that fails without signal is not really a choice.
            into.Sort((a, b) =>
            {
                int byQuality = b.Quality.CompareTo(a.Quality);
                if (byQuality != 0) return byQuality;
                int byNetwork = a.NeedsNetwork.CompareTo(b.NeedsNetwork);
                if (byNetwork != 0) return byNetwork;
                return string.CompareOrdinal(a.Name, b.Name);
            });
        }
        /// <summary>Speaks text, cutting off whatever is currently being spoken.</summary>
        /// <remarks>
        /// The pitch sits slightly below the engine default. Most Android voices are
        /// tuned bright for navigation prompts, which is the wrong register for this
        /// text; a few per cent down reads as measured rather than as chirpy.
        /// </remarks>
        public static void Speak(string text, string locale, float rate = 0.88f, float pitch = 0.96f,
            string voiceName = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return;
            try
            {
                // Very long passages make some engines fail outright; the book view can
                // hand over a whole chapter, so cap it and let the caller chunk.
                if (text.Length > 3800) text = text.Substring(0, 3800);
                _narrator.CallStatic("speak", text, locale ?? "en-IN", rate, pitch, voiceName ?? "");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Speak failed: {e.Message}");
                _unavailable = true;
            }
#endif
        }


        /// <summary>
        /// Speaks one passage and then another in a different language.
        ///
        /// Used for a shloka followed by its translation. The pair has to be handed over
        /// together: the speech engine applies a language change immediately rather than
        /// per queued utterance, so the second passage has to wait for the first to
        /// finish before its own voice can be selected.
        /// </summary>
        public static void SpeakPair(
            string firstText, string firstLocale, string firstVoice,
            string secondText, string secondLocale, string secondVoice,
            float rate = 0.88f, float pitch = 0.96f)
        {
            if (string.IsNullOrWhiteSpace(firstText))
            {
                Speak(secondText, secondLocale, rate, pitch, secondVoice);
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return;
            try
            {
                if (firstText.Length > 3800) firstText = firstText.Substring(0, 3800);
                if (!string.IsNullOrEmpty(secondText) && secondText.Length > 3800)
                    secondText = secondText.Substring(0, 3800);

                _narrator.CallStatic("speakPair",
                    firstText, firstLocale ?? "en-IN", firstVoice ?? "",
                    secondText ?? "", secondLocale ?? "", secondVoice ?? "",
                    rate, pitch);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] SpeakPair failed: {e.Message}");
                _unavailable = true;
            }
#endif
        }
        public static void Stop()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return;
            try { _narrator.CallStatic("stop"); } catch { /* nothing useful to do */ }
#endif
        }

        public static void Shutdown()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_narrator == null) return;
            try { _narrator.CallStatic("shutdown"); } catch { /* nothing useful to do */ }
            _narrator = null;
            _initCalled = false;
#endif
        }
    }
}
