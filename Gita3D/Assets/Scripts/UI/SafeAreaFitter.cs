using UnityEngine;

namespace Gita.UI
{
    /// <summary>
    /// Keeps a RectTransform clear of the notch and, more importantly, of the system
    /// navigation bar.
    ///
    /// Unity's own SafeArea component was doing this from <see cref="Screen.safeArea"/>
    /// alone, which on this build reported the cutout but not the gesture strip, so the
    /// footer of the reader sat underneath the phone's back control. This takes the
    /// larger of Unity's figure and Android's own per-edge insets: whichever source is
    /// right wins, and because it is a maximum rather than a sum the two can never be
    /// counted twice.
    /// </summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>Extra clearance beyond the bar itself, in reference units.</summary>
        const float Breathing = 10f;

        RectTransform _rt;
        Rect _lastSafe;
        Vector2Int _lastScreen;
        Vector4 _lastNative;
        float _settleTimer;

        /// <summary>The insets actually applied, in canvas reference units.</summary>
        public Vector4 Applied { get; private set; }   // left, top, right, bottom

        public static SafeAreaFitter Attach(RectTransform target)
        {
            var f = target.gameObject.AddComponent<SafeAreaFitter>();
            f._rt = target;
            f.Apply(force: true);
            return f;
        }

        void Awake() => _rt ??= GetComponent<RectTransform>();

        void OnEnable()
        {
            // Insets are not necessarily known on the first frame - the window has to be
            // attached first - so keep asking for a couple of seconds after any change.
            _settleTimer = 2.5f;
            SystemInsets.Refresh();
        }

        void Update()
        {
            bool screenChanged = _lastScreen.x != Screen.width || _lastScreen.y != Screen.height;

            if (screenChanged)
            {
                _settleTimer = 2.5f;
                SystemInsets.Refresh();
            }

            if (_settleTimer <= 0f) return;

            // Poll while things settle. Apply itself exits early when nothing has
            // changed, so this costs a comparison per frame for a couple of seconds
            // after a rotation and nothing at all once the layout is stable.
            _settleTimer -= Time.unscaledDeltaTime;
            if (Time.frameCount % 15 == 0) SystemInsets.Refresh();
            Apply(force: false);
        }

        void Apply(bool force)
        {
            if (_rt == null) return;

            var safe = Screen.safeArea;
            var native = SystemInsets.Pixels;
            var screen = new Vector2Int(Screen.width, Screen.height);

            if (!force && safe == _lastSafe && screen == _lastScreen && native == _lastNative) return;

            _lastSafe = safe;
            _lastScreen = screen;
            _lastNative = native;

            if (screen.x <= 0 || screen.y <= 0) return;

            // Unity's view of the safe area, as per-edge insets in pixels.
            float uLeft   = Mathf.Max(0f, safe.xMin);
            float uRight  = Mathf.Max(0f, screen.x - safe.xMax);
            float uBottom = Mathf.Max(0f, safe.yMin);
            float uTop    = Mathf.Max(0f, screen.y - safe.yMax);

            float left   = Mathf.Max(uLeft,   native.x);
            float top    = Mathf.Max(uTop,    native.y);
            float right  = Mathf.Max(uRight,  native.z);
            float bottom = Mathf.Max(uBottom, native.w);

            // A canvas scaled to a reference resolution measures in its own units, so
            // convert out of device pixels before setting offsets.
            var canvas = _rt.GetComponentInParent<Canvas>();
            float scale = canvas != null && canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;

            left   /= scale;
            top    /= scale;
            right  /= scale;
            bottom /= scale;

            // Only the bottom edge gets extra room: that is the one a thumb has to reach
            // past, and padding the top would waste the little height a phone has.
            if (bottom > 0.5f) bottom += Breathing;

            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = new Vector2(left, bottom);
            _rt.offsetMax = new Vector2(-right, -top);

            Applied = new Vector4(left, top, right, bottom);
        }
    }
}
