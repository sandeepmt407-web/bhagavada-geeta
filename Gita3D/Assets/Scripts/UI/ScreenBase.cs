using UnityEngine;
using Gita.World;

namespace Gita.UI
{
    /// <summary>
    /// A full-screen view. Each screen owns its own subtree, declares which camera
    /// composition it wants, and fades itself in and out.
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public RectTransform Root { get; private set; }
        protected CanvasGroup Fader;

        /// <summary>The composition the camera holds while this screen is up.</summary>
        public abstract Shot CameraShot { get; }

        float _targetAlpha;
        float _fadeSpeed = 4f;
        bool _settled = true;

        public bool IsVisible => Root != null && Root.gameObject.activeSelf;

        /// <summary>Called once, to construct the subtree. Base sets up Root and the fader.</summary>
        public virtual void Build(RectTransform parent)
        {
            Root = UIKit.Node(GetType().Name, parent);
            Fader = Root.gameObject.AddComponent<CanvasGroup>();
            Fader.alpha = 0f;
            Root.gameObject.SetActive(false);
        }

        /// <summary>Called every time the screen is shown, before the fade starts.</summary>
        public virtual void OnShow() { }

        /// <summary>Called once the screen has fully faded out.</summary>
        public virtual void OnHidden() { }

        public void Show(float duration = 0.35f)
        {
            if (Root == null) return;
            Root.gameObject.SetActive(true);
            OnShow();
            _targetAlpha = 1f;
            _fadeSpeed = 1f / Mathf.Max(duration, 0.01f);
            _settled = false;
            Fader.blocksRaycasts = true;
            Fader.interactable = true;
        }

        public void Hide(float duration = 0.25f)
        {
            if (Root == null || !Root.gameObject.activeSelf) return;
            _targetAlpha = 0f;
            _fadeSpeed = 1f / Mathf.Max(duration, 0.01f);
            _settled = false;
            Fader.blocksRaycasts = false;
            Fader.interactable = false;
        }

        protected virtual void Update()
        {
            if (_settled || Fader == null) return;

            Fader.alpha = Mathf.MoveTowards(Fader.alpha, _targetAlpha, Time.unscaledDeltaTime * _fadeSpeed);

            if (!Mathf.Approximately(Fader.alpha, _targetAlpha)) return;

            _settled = true;
            if (_targetAlpha <= 0f)
            {
                Root.gameObject.SetActive(false);
                OnHidden();
            }
        }
    }
}
