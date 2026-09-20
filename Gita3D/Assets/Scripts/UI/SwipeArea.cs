using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Gita.UI
{
    /// <summary>
    /// Detects a horizontal flick and turns the page.
    ///
    /// This sits on the same object as the ScrollRect, which receives the same drag
    /// events independently. That is deliberate: the ScrollRect is vertical-only, so
    /// it simply ignores sideways movement, and nothing has to be forwarded by hand.
    /// Forwarding would deliver every vertical drag twice.
    /// </summary>
    public sealed class SwipeArea : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action onSwipeLeft;   // finger travels left  -> next verse
        public Action onSwipeRight;  // finger travels right -> previous verse

        /// <summary>Travel needed to commit a page turn, in canvas reference pixels.</summary>
        public float threshold = 110f;

        enum Axis { Undecided, Horizontal, Vertical }

        Axis _axis = Axis.Undecided;
        Vector2 _start;

        public void OnBeginDrag(PointerEventData e)
        {
            _axis = Axis.Undecided;
            _start = e.position;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_axis != Axis.Undecided) return;

            var delta = e.position - _start;
            if (delta.magnitude < 14f) return; // too early to tell the axes apart

            // Bias toward vertical: scrolling is the commoner intent, and a slightly
            // diagonal scroll should never be mistaken for a page turn.
            _axis = Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.5f
                ? Axis.Horizontal
                : Axis.Vertical;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_axis == Axis.Horizontal)
            {
                float dx = e.position.x - _start.x;
                // The threshold is authored in reference pixels; convert to real ones.
                float scale = Mathf.Max(Screen.width / 1080f, 0.1f);
                if (Mathf.Abs(dx) >= threshold * scale)
                {
                    if (dx < 0f) onSwipeLeft?.Invoke();
                    else onSwipeRight?.Invoke();
                }
            }
            _axis = Axis.Undecided;
        }
    }
}
