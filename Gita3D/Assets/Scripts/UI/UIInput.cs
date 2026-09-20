using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Gita.UI
{
    /// <summary>
    /// Runtime construction of a TMP input field. It needs a specific little hierarchy -
    /// a masked viewport holding a text component and a placeholder - which the editor
    /// normally builds for you.
    /// </summary>
    public static class UIInput
    {
        public static TMP_InputField Field(string name, Transform parent, string placeholder,
            Action<string> onChanged, Action<string> onSubmit = null)
        {
            var host = UIKit.Node(name, parent);

            var bg = host.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Theme.TwilightLit.WithAlpha(0.8f);
            bg.raycastTarget = true;

            // The viewport clips the text as it scrolls past the edge.
            var viewport = UIKit.Node("TextArea", host);
            viewport.Inset(26f, 8f, 26f, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var text = UIKit.Text("Text", viewport, "", Theme.Sans, 26f, Theme.Cream,
                TextAlignmentOptions.Left);
            text.rectTransform.Inset(0f, 0f, 0f, 0f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            var hint = UIKit.Text("Placeholder", viewport, placeholder, Theme.Sans, 26f,
                Theme.Muted.WithAlpha(0.75f), TextAlignmentOptions.Left);
            hint.rectTransform.Inset(0f, 0f, 0f, 0f);
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            var field = host.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = bg;
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 48;
            field.caretColor = Theme.Saffron;
            field.customCaretColor = true;
            field.caretWidth = 2;
            field.selectionColor = Theme.Saffron.WithAlpha(0.35f);
            field.restoreOriginalTextOnEscape = false;

            if (onChanged != null) field.onValueChanged.AddListener(v => onChanged(v));
            if (onSubmit != null) field.onSubmit.AddListener(v => onSubmit(v));

            return field;
        }
    }
}
