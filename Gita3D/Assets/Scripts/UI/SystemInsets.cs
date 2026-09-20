using UnityEngine;

namespace Gita.UI
{
    /// <summary>
    /// Bridge to com.gita.ui.Insets. Off Android, and on any device where the call
    /// fails, every edge reads zero and the caller falls back to Screen.safeArea.
    /// </summary>
    public static class SystemInsets
    {
        static bool _unavailable;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass _insets;

        static AndroidJavaClass Java
        {
            get
            {
                if (_unavailable) return null;
                if (_insets != null) return _insets;
                try { _insets = new AndroidJavaClass("com.gita.ui.Insets"); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Gita] System insets unavailable ({e.GetType().Name}); " +
                                     "falling back to Screen.safeArea.");
                    _unavailable = true;
                }
                return _insets;
            }
        }
#endif

        /// <summary>Left, top, right, bottom, in device pixels.</summary>
        public static Vector4 Pixels
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                var java = Java;
                if (java == null) return Vector4.zero;
                try
                {
                    if (!java.CallStatic<bool>("isValid")) return Vector4.zero;
                    return new Vector4(
                        java.CallStatic<int>("left"),
                        java.CallStatic<int>("top"),
                        java.CallStatic<int>("right"),
                        java.CallStatic<int>("bottom"));
                }
                catch { _unavailable = true; return Vector4.zero; }
#else
                return Vector4.zero;
#endif
            }
        }

        public static void Refresh()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Java?.CallStatic("refresh"); } catch { _unavailable = true; }
#endif
        }
    }
}
