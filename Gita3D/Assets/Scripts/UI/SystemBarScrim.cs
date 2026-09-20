using UnityEngine;
using UnityEngine.UI;

namespace Gita.UI
{
    /// <summary>
    /// Continues the dark ground of the interface into the strips behind the notch and
    /// the navigation bar.
    ///
    /// Once the layout is held clear of the system bars there is a band at each end
    /// where the 3D set shows through, and the hard edge where a panel stops reads as a
    /// bug rather than as a design. These two strips close it. They are driven from the
    /// insets the fitter actually applied, so they are exactly as tall as the bars and
    /// disappear entirely on a phone that has none.
    /// </summary>
    public sealed class SystemBarScrim : MonoBehaviour
    {
        SafeAreaFitter _fitter;
        RectTransform _top, _bottom;
        Vector4 _applied = new(-1f, -1f, -1f, -1f);

        public static SystemBarScrim Create(RectTransform canvasRoot, SafeAreaFitter fitter, Color color)
        {
            var host = canvasRoot.gameObject.AddComponent<SystemBarScrim>();
            host._fitter = fitter;
            host._top = Strip(canvasRoot, "TopBarScrim", color, atTop: true);
            host._bottom = Strip(canvasRoot, "BottomBarScrim", color, atTop: false);
            return host;
        }

        static RectTransform Strip(RectTransform parent, string name, Color color, bool atTop)
        {
            var rt = UIKit.Node(name, parent);
            rt.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, 0f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIKit.Solid;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;

            // Behind every screen, which are added to the canvas after this.
            rt.SetAsFirstSibling();
            return rt;
        }

        void LateUpdate()
        {
            if (_fitter == null) return;

            var applied = _fitter.Applied;
            if (applied == _applied) return;
            _applied = applied;

            if (_top != null) _top.sizeDelta = new Vector2(0f, applied.y);
            if (_bottom != null) _bottom.sizeDelta = new Vector2(0f, applied.w);
        }
    }
}
