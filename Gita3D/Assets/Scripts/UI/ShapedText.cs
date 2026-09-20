using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Gita.UI
{
    /// <summary>
    /// A block of correctly shaped text. On Android the glyphs are rasterised by the
    /// platform (which handles Devanagari properly) and shown as a tinted coverage
    /// texture; everywhere else it falls back to TextMeshPro.
    ///
    /// Call sites do not need to know which path is active.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShapedText : MonoBehaviour
    {
        RectTransform _rt;
        LayoutElement _layout;

        RawImage _raw;              // native path
        TextMeshProUGUI _tmp;       // fallback path
        bool _native;

        string _text = "";
        string _androidFont;
        TMP_FontAsset _tmpFont;
        float _sizeRef;             // font size in canvas reference units
        float _lineSpacing = 1.15f;
        int _align;
        Color _color = Color.white;

        float _builtAtWidth = -1f;
        // Inside a layout group the parent reads preferredHeight; standing alone,
        // this block has to size its own rect.
        bool _drivenByLayout;

        public static ShapedText Create(string name, Transform parent,
            TMP_FontAsset tmpFont, string androidFont, float sizeRef, Color color,
            int align = NativeText.AlignStart, float lineSpacing = 1.15f)
        {
            var rt = UIKit.Node(name, parent);
            var st = rt.gameObject.AddComponent<ShapedText>();
            st._rt = rt;
            st._tmpFont = tmpFont;
            st._androidFont = androidFont;
            st._sizeRef = sizeRef;
            st._color = color;
            st._align = align;
            st._lineSpacing = lineSpacing;
            st.Init();
            return st;
        }

        void Init()
        {
            _rt ??= (RectTransform)transform;
            _layout = gameObject.AddComponent<LayoutElement>();
            _native = NativeText.Available;
            _drivenByLayout = _rt.parent != null &&
                              _rt.parent.GetComponent<LayoutGroup>() != null;

            if (_native)
            {
                var host = UIKit.Node("Raster", _rt);
                _raw = host.gameObject.AddComponent<RawImage>();
                _raw.color = _color;
                _raw.raycastTarget = false;
            }
            else
            {
                _tmp = UIKit.Text("Fallback", _rt, "", _tmpFont, _sizeRef, _color,
                    _align switch
                    {
                        NativeText.AlignCenter => TextAlignmentOptions.Top,
                        NativeText.AlignEnd => TextAlignmentOptions.TopRight,
                        _ => TextAlignmentOptions.TopLeft
                    });
                _tmp.lineSpacing = (_lineSpacing - 1f) * 100f * 0.5f;
            }
        }

        public void SetText(string text)
        {
            text ??= "";
            if (_text == text) return;
            _text = text;
            _builtAtWidth = -1f;
            Rebuild();
        }

        public void SetColor(Color color)
        {
            _color = color;
            if (_raw != null) _raw.color = color;
            if (_tmp != null) _tmp.color = color;
        }

        /// <summary>
        /// Re-rasterise when the available width changes. Height changes are ignored,
        /// otherwise setting the layout height would retrigger this immediately.
        /// </summary>
        void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled) return;
            float w = ((RectTransform)transform).rect.width;
            if (w > 1f && Mathf.Abs(w - _builtAtWidth) > 0.5f) Rebuild();
        }

        void OnEnable() => Rebuild();

        void Rebuild()
        {
            if (_rt == null) return;

            float widthRef = _rt.rect.width;
            if (widthRef <= 1f) return; // layout has not run yet
            _builtAtWidth = widthRef;

            if (string.IsNullOrEmpty(_text))
            {
                SetHeight(0f);
                if (_raw != null) _raw.enabled = false;
                if (_tmp != null) _tmp.text = "";
                return;
            }

            if (_native) RebuildNative(widthRef);
            else RebuildFallback(widthRef);
        }

        void RebuildNative(float widthRef)
        {
            // Rasterise at real device resolution so the text stays crisp, then map
            // the result back into canvas reference units for layout.
            float scale = ScaleFactor();
            int widthPx = Mathf.Clamp(Mathf.RoundToInt(widthRef * scale), 8, 4096);
            float sizePx = _sizeRef * scale;

            var tex = NativeText.Render(_text, _androidFont, sizePx, widthPx, _align, _lineSpacing);

            if (tex == null)
            {
                // Native path gave out mid-session; switch this block to TMP for good.
                DegradeToFallback();
                RebuildFallback(widthRef);
                return;
            }

            _raw.enabled = true;
            _raw.texture = tex;
            _raw.color = _color;
            SetHeight(tex.height / scale);
        }

        void RebuildFallback(float widthRef)
        {
            if (_tmp == null) return;
            _tmp.text = _text;
            var size = _tmp.GetPreferredValues(_text, widthRef, 0f);
            SetHeight(size.y);
        }

        void SetHeight(float heightRef)
        {
            _layout.preferredHeight = heightRef;
            _layout.minHeight = 0f;
            if (!_drivenByLayout)
                _rt.sizeDelta = new Vector2(_rt.sizeDelta.x, heightRef);
        }

        void DegradeToFallback()
        {
            _native = false;
            if (_raw != null)
            {
                _raw.enabled = false;
                Destroy(_raw.gameObject);
                _raw = null;
            }
            if (_tmp == null)
            {
                _tmp = UIKit.Text("Fallback", _rt, "", _tmpFont, _sizeRef, _color,
                    _align switch
                    {
                        NativeText.AlignCenter => TextAlignmentOptions.Top,
                        NativeText.AlignEnd => TextAlignmentOptions.TopRight,
                        _ => TextAlignmentOptions.TopLeft
                    });
            }
        }

        float ScaleFactor()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? Mathf.Max(canvas.scaleFactor, 0.1f) : 1f;
        }
    }
}
