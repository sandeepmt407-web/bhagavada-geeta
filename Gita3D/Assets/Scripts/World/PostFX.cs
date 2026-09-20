using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Gita.World
{
    /// <summary>
    /// The grade. Builds a global volume at runtime and keeps depth of field focused
    /// on whatever the camera director is currently framing.
    /// </summary>
    public sealed class PostFX : MonoBehaviour
    {
        VolumeProfile _profile;
        DepthOfField _dof;
        Bloom _bloom;
        ColorAdjustments _grade;
        Vignette _vignette;
        CameraDirector _director;

        public static PostFX Create(CameraDirector director)
        {
            var go = new GameObject("~PostFX");
            var fx = go.AddComponent<PostFX>();
            fx._director = director;
            fx.Build();
            return fx;
        }

        void Build()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "GitaGrade";

            // --- tonemapping: ACES rolls the bright dawn sky off gracefully
            var tone = _profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            // --- bloom: the sun blooming over the silhouettes is the whole look
            var bloom = _bloom = _profile.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(1.05f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.68f));
            bloom.highQualityFiltering.Override(false); // costly on mobile, little gain here

            // --- grade: warm the highlights, cool and lift the shadows
            var grade = _grade = _profile.Add<ColorAdjustments>(true);
            grade.postExposure.Override(-0.05f);
            grade.contrast.Override(8f);
            grade.saturation.Override(6f);
            grade.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var shadowsMidHigh = _profile.Add<ShadowsMidtonesHighlights>(true);
            shadowsMidHigh.shadows.Override(new Vector4(0.86f, 0.92f, 1.12f, 0f));
            shadowsMidHigh.midtones.Override(new Vector4(1.02f, 1.00f, 0.97f, 0f));
            shadowsMidHigh.highlights.Override(new Vector4(1.10f, 1.00f, 0.86f, 0f));

            // --- vignette: settles the eye on the centre of frame
            var vignette = _vignette = _profile.Add<Vignette>(true);
            vignette.intensity.Override(0.20f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(new Color(0.05f, 0.04f, 0.09f));

            // --- a whisper of grain, so the gradients in the sky do not band
            var grain = _profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.16f);
            grain.response.Override(0.8f);

            // --- depth of field, tracked to the subject each frame.
            // Gaussian rather than Bokeh: it only softens the far field, which is
            // exactly the effect wanted here, and it costs a fraction as much on a phone.
            _dof = _profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(24f);
            _dof.gaussianEnd.Override(95f);
            _dof.gaussianMaxRadius.Override(1.1f);
            _dof.highQualitySampling.Override(false);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = _profile;
        }

        void LateUpdate()
        {
            if (_dof == null || _director == null) return;

            // Keep the subject sharp and let everything well behind it fall away.
            // Follow smoothly - a hard snap reads as a glitch.
            float wantedStart = _director.FocusDistance * 1.35f;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 3f);
            float start = Mathf.Lerp(_dof.gaussianStart.value, wantedStart, k);
            _dof.gaussianStart.Override(start);
            _dof.gaussianEnd.Override(start * 3.6f);
        }

        /// <summary>
        /// Re-grades for a mood. Called continuously by the mood director while it
        /// crossfades between two verses' looks, so it takes final values rather than
        /// doing any easing of its own.
        /// </summary>
        public void SetGrade(float bloomIntensity, Color bloomTint,
                             float saturation, float contrast, Color colorFilter,
                             float vignette)
        {
            if (_bloom != null)
            {
                _bloom.intensity.Override(bloomIntensity);
                _bloom.tint.Override(bloomTint);
            }
            if (_grade != null)
            {
                _grade.saturation.Override(saturation);
                _grade.contrast.Override(contrast);
                _grade.colorFilter.Override(colorFilter);
            }
            if (_vignette != null) _vignette.intensity.Override(vignette);
        }

        /// <summary>
        /// Bokeh depth of field is expensive on low-end phones; the boot sequence
        /// calls this when it decides the device cannot afford it.
        /// </summary>
        public void SetDepthOfField(bool enabled)
        {
            if (_dof != null) _dof.active = enabled;
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }
    }
}
