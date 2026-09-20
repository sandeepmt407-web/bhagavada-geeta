using System;
using UnityEngine;
using Gita.Data;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

namespace Gita.App
{
    /// <summary>
    /// A verse each morning.
    ///
    /// Android repeating notifications carry fixed text, so a single daily repeat would
    /// show the same verse forever. Instead this schedules one notification per day for
    /// the next fortnight, each carrying that day's verse, and re-schedules on every
    /// launch. Opening the app even once a fortnight keeps the queue full.
    /// </summary>
    public static class DailyVerse
    {
        const string ChannelId = "gita_daily_verse";
        const string KeyEnabled = "gita.daily.enabled";
        const string KeyHour = "gita.daily.hour";
        const int DaysAhead = 14;

        public static event Action Changed;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(KeyEnabled, 1) == 1;   // on by default; the
            set                                              // system dialog is the
            {                                                // real consent gate
                if (value == Enabled) return;
                PlayerPrefs.SetInt(KeyEnabled, value ? 1 : 0);
                PlayerPrefs.Save();
                Apply();
                Changed?.Invoke();
            }
        }

        /// <summary>Hour of the day, 0-23, when the verse arrives.</summary>
        public static int Hour
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyHour, 7), 0, 23);
            set
            {
                int h = Mathf.Clamp(value, 0, 23);
                if (h == Hour) return;
                PlayerPrefs.SetInt(KeyHour, h);
                PlayerPrefs.Save();
                Apply();
                Changed?.Invoke();
            }
        }

        /// <summary>Call once at boot, and again whenever the setting changes.</summary>
        public static void Apply()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                if (!Enabled) return;

                RegisterChannel();
                RequestPermissionIfNeeded();
                ScheduleAhead();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Gita] Could not schedule the daily verse: {e.Message}");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void RegisterChannel()
        {
            var channel = new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = "Daily verse",
                Description = "One verse of the Gita each morning",
                Importance = Importance.Default,
                CanShowBadge = true,
                EnableLights = false,
                EnableVibration = false,
                LockScreenVisibility = LockScreenVisibility.Public,
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
        }

        /// <summary>
        /// Android 13 and later require the user to allow notifications. The request is
        /// asynchronous; scheduling proceeds either way, and the queued notifications
        /// simply stay silent until permission is granted.
        /// </summary>
        static void RequestPermissionIfNeeded()
        {
            try { _ = new PermissionRequest(); }
            catch (Exception e) { Debug.LogWarning($"[Gita] Notification permission: {e.Message}"); }
        }

        static void ScheduleAhead()
        {
            var now = DateTime.Now;
            var first = new DateTime(now.Year, now.Month, now.Day, Hour, 0, 0);
            if (first <= now.AddMinutes(1)) first = first.AddDays(1);

            for (int day = 0; day < DaysAhead; day++)
            {
                var when = first.AddDays(day);
                var verse = GitaDatabase.VerseOfTheDay(when);
                if (verse == null) continue;

                string body = GitaDatabase.TextOf(verse, AppSettings.Edition);
                if (string.IsNullOrWhiteSpace(body)) continue;

                var notification = new AndroidNotification
                {
                    Title = $"Bhagavad Gita {verse.c}.{verse.v}",
                    Text = Shorten(body, 160),
                    FireTime = when,
                    ShouldAutoCancel = true,
                    // Carries the reference so a tap could open straight to the verse.
                    IntentData = $"{verse.c}.{verse.v}",
                };
                AndroidNotificationCenter.SendNotification(notification, ChannelId);
            }
        }
#endif

        /// <summary>
        /// The verse a tap arrived on, or null. Lets the app open where the reader was
        /// invited rather than at the home screen.
        /// </summary>
        public static Verse OpenedFromNotification()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var intent = AndroidNotificationCenter.GetLastNotificationIntent();
                if (intent == null) return null;

                var parts = (intent.Notification.IntentData ?? "").Split('.');
                if (parts.Length != 2) return null;
                if (!int.TryParse(parts[0], out int c) || !int.TryParse(parts[1], out int v))
                    return null;
                return GitaDatabase.GetVerse(c, v);
            }
            catch
            {
                return null;
            }
#else
            return null;
#endif
        }

        static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            int cut = text.LastIndexOf(' ', Mathf.Min(max, text.Length - 1));
            if (cut < max / 2) cut = max;
            return text[..cut].TrimEnd(',', ';', '.', ' ') + "…";
        }
    }
}
