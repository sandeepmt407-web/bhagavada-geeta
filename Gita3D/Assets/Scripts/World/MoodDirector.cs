using UnityEngine;

namespace Gita.World
{
    /// <summary>
    /// Re-dresses the set for whichever verse is open.
    ///
    /// Every change is a blend rather than a cut - turning a page should feel like the
    /// light moving, not like a different app. A page turn inside one mood is a short
    /// blend of the small per-verse differences; a page turn that crosses into another
    /// mood takes longer, because it is a real change of weather.
    /// </summary>
    public sealed class MoodDirector : MonoBehaviour
    {
        static readonly int SkyTintId = Shader.PropertyToID("_Tint");
        static readonly int SkyExposureId = Shader.PropertyToID("_Exposure");
        static readonly int SkyRotationId = Shader.PropertyToID("_Rotation");

        CinematicWorld _world;
        CameraDirector _camera;
        PostFX _grade;
        Motifs _motifs;
        Material _sky;

        readonly MoodLook _from = new();
        readonly MoodLook _to = new();
        readonly MoodLook _live = new();

        float _blend = 1f;
        float _blendSpeed = 1f;
        float _giTimer;
        bool _settled = true;

        VerseMood.Mood _currentMood = VerseMood.Mood.Duty;
        bool _hasMood;

        /// <summary>The name of the look currently on screen, for the caption.</summary>
        public string CurrentName => _to.Name;

        /// <summary>The motif layers, for the look-development tool.</summary>
        public Motifs Layers => _motifs;

        /// <summary>How far the reader screen should darken the set behind its text.</summary>
        public float Scrim => _live.Scrim;

        public static MoodDirector Create(CinematicWorld world, CameraDirector camera, PostFX grade)
        {
            var go = new GameObject("~MoodDirector");
            var d = go.AddComponent<MoodDirector>();
            d._world = world;
            d._camera = camera;
            d._grade = grade;
            d.Init();
            return d;
        }

        void Init()
        {
            // The skybox is a shared asset; tinting it directly would edit the project.
            if (RenderSettings.skybox != null)
            {
                _sky = new Material(RenderSettings.skybox) { name = "DawnSky (mood)" };
                RenderSettings.skybox = _sky;
            }

            _motifs = Motifs.Create(transform, _world != null ? _world.DustMaterial : null);
            if (_world != null && _world.Dust != null) _world.Dust.gameObject.SetActive(false);

            var opening = VerseMood.LookOf(VerseMood.Mood.Duty);
            Copy(opening, _from);
            Copy(opening, _to);
            Copy(opening, _live);
            Apply(_live);
            ShowMotif(_to);
        }

        /// <summary>
        /// Dresses the set for one verse. Safe to call on every page turn; repeated
        /// calls for the same verse do nothing.
        /// </summary>
        public void GoTo(int chapter, int verse)
        {
            var mood = VerseMood.MoodOf(chapter, verse);
            var look = VerseMood.LookOf(mood);

            // Hold where the light actually is right now, so interrupting a blend
            // half-way never snaps.
            Copy(_live, _from);
            Copy(look, _to);
            Vary(_to, VerseMood.Variation(chapter, verse));

            // Within a mood the change is small and should be quick; between moods it
            // is the whole sky and wants time.
            bool changedMood = !_hasMood || mood != _currentMood;
            float seconds = changedMood ? 2.1f : 0.9f;

            _currentMood = mood;
            _hasMood = true;
            _blend = 0f;
            _blendSpeed = 1f / seconds;
            _settled = false;

            ShowMotif(_to);
        }

        /// <summary>
        /// Dresses the set for a verse with no blend at all. Used by the look-development
        /// tool, which renders a still per mood and has no frames to blend across.
        /// </summary>
        public void Snap(int chapter, int verse)
        {
            GoTo(chapter, verse);
            Copy(_to, _from);
            Copy(_to, _live);
            Apply(_live);
            _blend = 1f;
            _settled = true;
            DynamicGI.UpdateEnvironment();
        }

        void ShowMotif(MoodLook look)
        {
            if (_motifs == null) return;
            _motifs.Show(look.Motif, look.MotifRate, look.MotifNear, look.MotifFar);
        }

        void Update()
        {
            if (_settled) return;

            float dt = Time.deltaTime;
            _blend = Mathf.Min(1f, _blend + dt * _blendSpeed);

            // Smootherstep, matching the camera director, so light and lens move together.
            float t = _blend;
            float e = t * t * t * (t * (t * 6f - 15f) + 10f);

            Lerp(_from, _to, e, _live);
            Apply(_live);

            // Re-deriving ambient from the sky is not cheap enough to do every frame.
            _giTimer -= dt;
            if (_giTimer <= 0f)
            {
                _giTimer = 0.25f;
                DynamicGI.UpdateEnvironment();
            }

            if (_blend < 1f) return;

            _settled = true;
            DynamicGI.UpdateEnvironment();
        }

        // ------------------------------------------------------------------

        void Apply(MoodLook look)
        {
            if (_world != null)
            {
                if (_world.Sun != null)
                {
                    _world.Sun.color = look.SunColor;
                    _world.Sun.intensity = look.SunIntensity;
                }
                if (_world.Fill != null)
                {
                    _world.Fill.color = look.FillColor;
                    _world.Fill.intensity = look.FillIntensity;
                }
            }

            RenderSettings.ambientIntensity = look.AmbientIntensity;
            RenderSettings.fogColor = look.FogColor;
            RenderSettings.fogDensity = look.FogDensity;

            if (_sky != null)
            {
                _sky.SetColor(SkyTintId, look.SkyTint);
                _sky.SetFloat(SkyExposureId, look.SkyExposure);
                _sky.SetFloat(SkyRotationId, look.SkyRotation);
            }

            _grade?.SetGrade(look.Bloom, look.BloomTint, look.Saturation,
                             look.Contrast, look.ColorFilter, look.Vignette);

            _camera?.SetMoodFraming(look.CamOffset, look.TargetOffset, look.FovDelta);
        }

        /// <summary>
        /// Nudges a look by a per-verse amount. Small enough that two verses of one
        /// mood are plainly the same weather, large enough that they are not the same
        /// photograph: the sky turns, the haze thickens or thins, and the lens shifts.
        /// </summary>
        static void Vary(MoodLook look, float v)
        {
            float signed = v * 2f - 1f;                      // -1 .. 1

            look.SkyRotation = Mathf.Repeat(look.SkyRotation + signed * 26f, 360f);
            look.FogDensity *= 1f + signed * 0.10f;
            look.SunIntensity *= 1f + signed * 0.06f;
            look.SkyExposure *= 1f + signed * 0.05f;
            look.MotifRate *= 1f + signed * 0.18f;

            look.CamOffset += new Vector3(signed * 0.45f, signed * 0.12f, -Mathf.Abs(signed) * 0.3f);
            look.TargetOffset += new Vector3(signed * -0.35f, signed * 0.20f, 0f);
            look.FovDelta += signed * 1.4f;
        }

        static void Copy(MoodLook src, MoodLook dst)
        {
            dst.Name = src.Name;

            dst.SunColor = src.SunColor;
            dst.SunIntensity = src.SunIntensity;
            dst.FillColor = src.FillColor;
            dst.FillIntensity = src.FillIntensity;
            dst.AmbientIntensity = src.AmbientIntensity;

            dst.FogColor = src.FogColor;
            dst.FogDensity = src.FogDensity;

            dst.SkyTint = src.SkyTint;
            dst.SkyExposure = src.SkyExposure;
            dst.SkyRotation = src.SkyRotation;

            dst.Bloom = src.Bloom;
            dst.BloomTint = src.BloomTint;
            dst.Saturation = src.Saturation;
            dst.Contrast = src.Contrast;
            dst.ColorFilter = src.ColorFilter;
            dst.Vignette = src.Vignette;

            dst.Scrim = src.Scrim;

            dst.Motif = src.Motif;
            dst.MotifRate = src.MotifRate;
            dst.MotifNear = src.MotifNear;
            dst.MotifFar = src.MotifFar;

            dst.CamOffset = src.CamOffset;
            dst.TargetOffset = src.TargetOffset;
            dst.FovDelta = src.FovDelta;
        }

        static void Lerp(MoodLook a, MoodLook b, float t, MoodLook dst)
        {
            dst.Name = t < 0.5f ? a.Name : b.Name;

            dst.SunColor = Color.Lerp(a.SunColor, b.SunColor, t);
            dst.SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t);
            dst.FillColor = Color.Lerp(a.FillColor, b.FillColor, t);
            dst.FillIntensity = Mathf.Lerp(a.FillIntensity, b.FillIntensity, t);
            dst.AmbientIntensity = Mathf.Lerp(a.AmbientIntensity, b.AmbientIntensity, t);

            dst.FogColor = Color.Lerp(a.FogColor, b.FogColor, t);
            dst.FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t);

            dst.SkyTint = Color.Lerp(a.SkyTint, b.SkyTint, t);
            dst.SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, t);
            // Rotation is a circle: take the short way round rather than sweeping the
            // whole sky past the camera.
            dst.SkyRotation = Mathf.Repeat(
                a.SkyRotation + Mathf.DeltaAngle(a.SkyRotation, b.SkyRotation) * t, 360f);

            dst.Bloom = Mathf.Lerp(a.Bloom, b.Bloom, t);
            dst.BloomTint = Color.Lerp(a.BloomTint, b.BloomTint, t);
            dst.Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t);
            dst.Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t);
            dst.ColorFilter = Color.Lerp(a.ColorFilter, b.ColorFilter, t);
            dst.Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t);

            dst.Scrim = Mathf.Lerp(a.Scrim, b.Scrim, t);

            dst.Motif = b.Motif;
            dst.MotifRate = b.MotifRate;
            dst.MotifNear = b.MotifNear;
            dst.MotifFar = b.MotifFar;

            dst.CamOffset = Vector3.Lerp(a.CamOffset, b.CamOffset, t);
            dst.TargetOffset = Vector3.Lerp(a.TargetOffset, b.TargetOffset, t);
            dst.FovDelta = Mathf.Lerp(a.FovDelta, b.FovDelta, t);
        }

        void OnDestroy()
        {
            if (_sky == null) return;
            // Look-development runs this outside play mode, where Destroy is illegal.
            if (Application.isPlaying) Destroy(_sky);
            else DestroyImmediate(_sky);
        }
    }
}
