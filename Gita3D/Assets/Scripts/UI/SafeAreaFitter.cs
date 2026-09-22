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

        /// <summary>How often the insets are asked for once the layout has settled.</summary>
        const float PollSeconds = 0.3f;

        RectTransform _rt;
        Rect _lastSafe;
        Vector2Int _lastScreen;
        Vector4 _lastNative;
        float _settleTimer, _pollTimer;
        Vector2 _banner;   // top, bottom, in device pixels

        /// <summary>The insets actually applied, in canvas reference units.</summary>
        public Vector4 Applied { get; private set; }   // left, top, right, bottom

        /// <summary>
        /// The system bars and the cutout alone, in device pixels - what a banner ad has to
        /// sit against, before any room is made for it.
        /// </summary>
        public Vector4 SystemPixels { get; private set; }   // left, top, right, bottom

        /// <summary>
        /// Holds the layout clear of a banner ad as well as of the system bars. Each value
        /// is the distance in device pixels from that edge of the screen to the far side
        /// of the banner, so it already includes the bar the banner sits against; zero
        /// means there is no banner at that end.
        /// </summary>
        public void SetBannerClearance(float topPixels, float bottomPixels)
        {
            var banner = new Vector2(topPixels, bottomPixels);
            if (banner == _banner) return;
            _banner = banner;
            Apply(force: true);
        }

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

            // Poll every few frames while things settle, then a few times a second for
            // as long as the app runs. The navigation bar comes and goes - fullscreen
            // hides it, a touch brings it back - and nothing the layout listens to
            // changes when it does, so the only way to follow it is to keep asking:
            // the full height while it is hidden, clear of it while it shows. Apply
            // exits early when nothing has changed, so a quiet poll costs a comparison.
            if (_settleTimer > 0f)
            {
                _settleTimer -= Time.unscaledDeltaTime;
                if (Time.frameCount % 15 == 0) SystemInsets.Refresh();
                Apply(force: false);
                return;
            }

            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = PollSeconds;
            Apply(force: false);        // what the last request brought back
            SystemInsets.Refresh();     // and ask again
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

            SystemPixels = new Vector4(left, top, right, bottom);

            // A banner sits against the bar, so its clearance already contains it: the
            // larger of the two, again, rather than the sum.
            top    = Mathf.Max(top,    _banner.x);
            bottom = Mathf.Max(bottom, _banner.y);

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
