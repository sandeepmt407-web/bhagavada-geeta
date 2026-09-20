using UnityEngine;

namespace Gita.App
{
    /// <summary>
    /// Keeps a narration session alive while it is running.
    ///
    /// Scope, stated plainly: this holds the screen awake and lets narration continue
    /// while the app is unfocused - switching apps for a moment, or reading with the
    /// screen on. It does not survive the screen being turned off. That needs an
    /// Android foreground service with a media session, which cannot be developed
    /// safely without a device to run it on: an incorrect foregroundServiceType is a
    /// crash on launch from Android 14, and there is no emulator available here that
    /// can run this build.
    /// </summary>
    public static class AudioSession
    {
        // Screen.sleepTimeout is a plain int; SleepTimeout holds the constants for it.
        static int _previous = SleepTimeout.SystemSetting;
        static bool _held;

        public static bool IsActive => _held;

        /// <summary>Call when narration starts.</summary>
        public static void Begin()
        {
            if (_held) return;
            _held = true;

            _previous = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        /// <summary>Call when narration stops or the screen holding it goes away.</summary>
        public static void End()
        {
            if (!_held) return;
            _held = false;

            Screen.sleepTimeout = _previous;
        }
    }
}
