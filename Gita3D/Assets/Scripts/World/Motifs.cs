using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gita.World
{
    /// <summary>
    /// The drifting element in front of the set: dust on the battlefield, petals for the
    /// devotional chapters, sparks for the eleventh, stars for the sixth, and so on.
    ///
    /// Every kind is built once and left in place with its emission at zero. Changing
    /// mood raises the rate on one and drops it on the others, which means the old
    /// element thins out while the new one gathers instead of popping. A system that is
    /// not emitting and holds no particles costs essentially nothing per frame, so the
    /// nine of them together are cheaper than rebuilding one on every page turn.
    ///
    /// They all share the authored dust material. Particle colour comes from the system
    /// rather than the material, so one additive material serves every kind and they
    /// batch together.
    /// </summary>
    public sealed class Motifs : MonoBehaviour
    {
        readonly Dictionary<Motif, ParticleSystem> _systems = new();

        public static Motifs Create(Transform parent, Material shared)
        {
            var go = new GameObject("Motifs");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<Motifs>();
            m.BuildAll(shared);
            return m;
        }

        void BuildAll(Material shared)
        {
            foreach (Motif kind in System.Enum.GetValues(typeof(Motif)))
            {
                if (kind == Motif.None) continue;
                _systems[kind] = Build(kind, shared);
            }
        }

        /// <summary>
        /// Crossfades to a kind. Rate and colours come from the mood, so the same
        /// element can read differently in two chapters that share it.
        /// </summary>
        public void Show(Motif kind, float rate, Color near, Color far)
        {
            foreach (var pair in _systems)
            {
                var emission = pair.Value.emission;
                bool on = pair.Key == kind;
                emission.rateOverTime = on ? Mathf.Max(0f, rate) : 0f;

                if (!on) continue;

                var main = pair.Value.main;
                main.startColor = new ParticleSystem.MinMaxGradient(near, far);
                if (!pair.Value.isPlaying) pair.Value.Play();
            }
        }


        /// <summary>
        /// Advances every live system by a number of seconds. Particles do not run
        /// outside play mode, so a still rendered for look-development would otherwise
        /// show an empty sky.
        /// </summary>
        public void Simulate(float seconds)
        {
            foreach (var ps in _systems.Values)
            {
                if (ps == null) continue;
                ps.Simulate(seconds, withChildren: true, restart: true);
            }
        }
        // ------------------------------------------------------------------

        ParticleSystem Build(Motif kind, Material shared)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 340;

            var emission = ps.emission;
            emission.rateOverTime = 0f;          // raised by Show

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            var noise = ps.noise;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = shared;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 1;

            switch (kind)
            {
                // Hanging dust, lifting slowly on the morning air. The original.
                case Motif.Dust:
                    go.transform.position = new Vector3(0f, 6f, 22f);
                    main.startLifetime = 26f;
                    main.startSpeed = 0.28f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.15f);
                    main.gravityModifier = -0.004f;
                    shape.scale = new Vector3(70f, 14f, 60f);
                    SetNoise(noise, 0.42f, 0.16f, 0.12f);
                    break;

                // Motes of light climbing out of the ground, unhurried.
                case Motif.Embers:
                    go.transform.position = new Vector3(0f, 2f, 20f);
                    main.startLifetime = 20f;
                    main.startSpeed = 0.42f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
                    main.gravityModifier = -0.02f;
                    shape.scale = new Vector3(56f, 6f, 46f);
                    SetNoise(noise, 0.55f, 0.22f, 0.16f);
                    break;

                // Offerings, falling. Larger, slower, and turning as they go.
                case Motif.Petals:
                    go.transform.position = new Vector3(0f, 15f, 19f);
                    main.startLifetime = 24f;
                    main.startSpeed = 0.18f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.30f);
                    main.gravityModifier = 0.02f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    shape.scale = new Vector3(52f, 4f, 40f);
                    SetNoise(noise, 0.85f, 0.14f, 0.10f);
                    var rotation = ps.rotationOverLifetime;
                    rotation.enabled = true;
                    rotation.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 1.7f;
                    break;

                // Points held still, high up. Nothing moves in the sixth chapter.
                case Motif.Stars:
                    go.transform.position = new Vector3(0f, 34f, 46f);
                    main.startLifetime = 40f;
                    main.startSpeed = 0.02f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.34f);
                    main.gravityModifier = 0f;
                    main.maxParticles = 420;
                    shape.scale = new Vector3(130f, 40f, 90f);
                    SetNoise(noise, 0.06f, 0.05f, 0.02f);
                    // A slow pulse, so the field of points is never quite static.
                    var twinkle = ps.sizeOverLifetime;
                    twinkle.enabled = true;
                    twinkle.size = new ParticleSystem.MinMaxCurve(1f, Curves.Twinkle());
                    break;

                // The eleventh chapter: fast, bright, and far too many.
                case Motif.Sparks:
                    go.transform.position = new Vector3(0f, 3f, 21f);
                    main.startLifetime = 6.5f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 4.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
                    main.gravityModifier = -0.09f;
                    main.maxParticles = 560;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 34f;
                    shape.radius = 24f;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    SetNoise(noise, 1.5f, 0.45f, 0.5f);
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 2.6f;
                    break;

                // A wind across the field, carrying leaves sideways.
                case Motif.Leaves:
                    go.transform.position = new Vector3(-26f, 8f, 20f);
                    main.startLifetime = 26f;
                    main.startSpeed = 1.5f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.26f);
                    main.gravityModifier = 0.014f;
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(2f, 16f, 44f);
                    shape.rotation = new Vector3(0f, 90f, 0f);   // blow along +X
                    SetNoise(noise, 1.1f, 0.19f, 0.22f);
                    var spin = ps.rotationOverLifetime;
                    spin.enabled = true;
                    spin.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 2.0f;
                    break;

                // Three strands at once: the colour range does the work here.
                case Motif.Motes:
                    go.transform.position = new Vector3(0f, 7f, 20f);
                    main.startLifetime = 22f;
                    main.startSpeed = 0.5f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.20f);
                    main.gravityModifier = 0f;
                    shape.scale = new Vector3(60f, 16f, 46f);
                    SetNoise(noise, 1.35f, 0.30f, 0.26f);
                    break;

                // Light coming down rather than up. The last chapters.
                case Motif.Light:
                    go.transform.position = new Vector3(0f, 22f, 20f);
                    main.startLifetime = 26f;
                    main.startSpeed = 0.55f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.24f);
                    main.gravityModifier = 0.012f;
                    shape.scale = new Vector3(58f, 6f, 44f);
                    SetNoise(noise, 0.5f, 0.12f, 0.09f);
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 1.4f;
                    break;
            }

            // Every kind fades in and out rather than appearing and vanishing.
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Curves.FadeInOut());

            return ps;
        }

        static void SetNoise(ParticleSystem.NoiseModule noise,
            float strength, float frequency, float scroll)
        {
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = scroll;
        }

        /// <summary>Curves and gradients shared by the motif systems.</summary>
        static class Curves
        {
            public static Gradient FadeInOut()
            {
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.22f),
                        new GradientAlphaKey(1f, 0.72f), new GradientAlphaKey(0f, 1f)
                    });
                return g;
            }

            public static AnimationCurve Twinkle()
            {
                var c = new AnimationCurve();
                c.AddKey(0f, 0.2f);
                c.AddKey(0.25f, 1f);
                c.AddKey(0.5f, 0.55f);
                c.AddKey(0.75f, 1f);
                c.AddKey(1f, 0.2f);
                return c;
            }
        }
    }
}
