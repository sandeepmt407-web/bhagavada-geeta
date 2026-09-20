using UnityEngine;
using TMPro;

namespace Gita.UI
{
    /// <summary>
    /// Remembers the size a block of text was authored at, so the whole interface can be
    /// re-sized from one setting without rebuilding any screens.
    ///
    /// Attached automatically by <see cref="UIKit.Text"/> and by
    /// <see cref="ShapedText"/>; nothing else needs to know it exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScaledText : MonoBehaviour
    {
        /// <summary>The size as written in the layout code, before any reader setting.</summary>
        public float BaseSize;

        /// <summary>
        /// How far a block in a fixed-size box may shrink to fit, as a fraction of its
        /// authored size. Only consulted when the block has auto-sizing turned on, which
        /// <see cref="UIKit.Fit"/> does.
        /// </summary>
        public float MinRatio = 0.6f;

        TextMeshProUGUI _tmp;
        ShapedText _shaped;

        public static ScaledText Attach(GameObject go, float baseSize)
        {
            var s = go.GetComponent<ScaledText>() ?? go.AddComponent<ScaledText>();
            s.BaseSize = baseSize;
            s.Bind();
            s.ApplyScale();
            return s;
        }

        void Bind()
        {
            _tmp ??= GetComponent<TextMeshProUGUI>();
            _shaped ??= GetComponent<ShapedText>();
        }

        public void ApplyScale()
        {
            Bind();
            float size = BaseSize * Theme.TextScale;

            if (_tmp != null)
            {
                // A block that may shrink is driven by its bounds, so the reader's
                // setting moves the ceiling rather than the size itself.
                if (_tmp.enableAutoSizing)
                {
                    _tmp.fontSizeMax = size;
                    _tmp.fontSizeMin = size * MinRatio;
                }
                else
                {
                    _tmp.fontSize = size;
                }
            }

            if (_shaped != null) _shaped.SetSizeRef(size);
        }

        /// <summary>
        /// Re-sizes every block in the app, including those on screens that are hidden -
        /// they are inactive, not destroyed, and would otherwise come back at the old
        /// size.
        /// </summary>
        public static void ApplyScaleToAll()
        {
            var all = Object.FindObjectsByType<ScaledText>(FindObjectsInactive.Include);
            foreach (var s in all) s.ApplyScale();
        }
    }
}
