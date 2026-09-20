using System;
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

        /// <summary>Speaks text, cutting off whatever is currently being spoken.</summary>
        public static void Speak(string text, string locale, float rate = 0.85f, float pitch = 1f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_unavailable || _narrator == null) return;
            try
            {
                // Very long passages make some engines fail outright; the book view can
                // hand over a whole chapter, so cap it and let the caller chunk.
                if (text.Length > 3800) text = text.Substring(0, 3800);
                _narrator.CallStatic("speak", text, locale ?? "en-IN", rate, pitch);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Speak failed: {e.Message}");
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
