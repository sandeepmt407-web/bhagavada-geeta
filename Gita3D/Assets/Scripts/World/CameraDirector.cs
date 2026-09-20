using System.Collections.Generic;
using UnityEngine;

namespace Gita.World
{
    /// <summary>Which composition the camera should be holding.</summary>
    public enum Shot
    {
        Establishing,  // wide, the halted chariot between the hosts
        Chapters,      // pulled back and raised, the field laid out below
        Reader,        // close and low, the chariot soft behind the text
        Contemplation  // turned to the open horizon, nothing but sky
    }

    /// <summary>
    /// Drives the camera as a film camera rather than a game camera: every shot is a
    /// framing plus a slow continuous move, and cuts are always blended, never snapped.
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        struct Setup
        {
            public Vector3 position;   // anchor position
            public Vector3 target;     // what the lens is pointed at
            public float fov;
            public Vector3 drift;      // per-axis sway amplitude
            public float driftSpeed;
            public float orbit;        // degrees of slow arc around the target
            public float dolly;        // metres of slow push toward the target
        }

        static readonly Dictionary<Shot, Setup> Shots = new()
        {
            // Low and close: the chariot should read as a monument against the sky,
            // not as a distant speck on an empty plain.
            [Shot.Establishing] = new Setup
            {
                position = new Vector3(-8.8f, 1.9f, 3.4f),
                target   = new Vector3(-0.6f, 3.0f, 16.4f),
                fov = 45f,
                drift = new Vector3(0.18f, 0.09f, 0.12f), driftSpeed = 0.09f,
                orbit = 2.6f, dolly = 1.1f
            },
            [Shot.Chapters] = new Setup
            {
                position = new Vector3(-9.6f, 3.4f, 2.2f),
                target   = new Vector3(-1.0f, 2.6f, 17f),
                fov = 46f,
                drift = new Vector3(0.22f, 0.11f, 0.14f), driftSpeed = 0.07f,
                orbit = 3.6f, dolly = 0f
            },
            [Shot.Reader] = new Setup
            {
                position = new Vector3(-7.4f, 2.1f, 1.6f),
                target   = new Vector3(-0.4f, 5.2f, 18.5f),
                fov = 40f,
                drift = new Vector3(0.10f, 0.06f, 0.08f), driftSpeed = 0.055f,
                orbit = 1.2f, dolly = 0.5f
            },
            [Shot.Contemplation] = new Setup
            {
                position = new Vector3(7.5f, 2.2f, 6f),
                target   = new Vector3(12f, 5.2f, 80f),
                fov = 50f,
                drift = new Vector3(0.16f, 0.11f, 0.1f), driftSpeed = 0.05f,
                orbit = 0f, dolly = 1.2f
            },
        };

        public Camera Cam { get; private set; }

        Setup _from, _to;
        float _blend = 1f;        // 0 = fully on _from, 1 = fully on _to
        float _blendSpeed = 0.5f;
        float _shotTime;          // seconds held on the current shot, drives the slow move

        public Shot Current { get; private set; } = Shot.Establishing;

        // A framing offset laid over the current shot, so the mood of a verse can move
        // the lens without every mood needing a shot of its own. Eased rather than set,
        // because a page turn should not snap the camera.
        Vector3 _moodPos, _moodTarget, _moodPosWanted, _moodTargetWanted;
        float _moodFov, _moodFovWanted;

        /// <summary>Framing to hold on top of the current shot. Reached over about a second.</summary>
        public void SetMoodFraming(Vector3 camOffset, Vector3 targetOffset, float fovDelta)
        {
            _moodPosWanted = camOffset;
            _moodTargetWanted = targetOffset;
            _moodFovWanted = fovDelta;
        }

        public static CameraDirector Create(Camera cam)
        {
            var d = cam.gameObject.AddComponent<CameraDirector>();
            d.Cam = cam;
            d._from = d._to = Shots[Shot.Establishing];
            d._blend = 1f;
            d.Apply(1f);
            return d;
        }

        /// <summary>Blend to a new composition. Duration is in seconds.</summary>
        public void CutTo(Shot shot, float duration = 2.2f)
        {
            if (shot == Current && _blend >= 1f) return;

            // Start the new blend from wherever the lens actually is right now,
            // so interrupting a move never causes a jump.
            _from = Sample(Mathf.Clamp01(_blend));
            _to = Shots[shot];
            Current = shot;
            _blend = 0f;
            _blendSpeed = 1f / Mathf.Max(duration, 0.05f);
            _shotTime = 0f;
        }

        /// <summary>Jump straight to a composition with no blend. Used by editor look-dev.</summary>
        public void SnapTo(Shot shot)
        {
            _from = _to = Shots[shot];
            Current = shot;
            _blend = 1f;
            _shotTime = 0f;
            Apply(1f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _shotTime += dt;

            float ease = 1f - Mathf.Exp(-dt * 2.2f);
            _moodPos = Vector3.Lerp(_moodPos, _moodPosWanted, ease);
            _moodTarget = Vector3.Lerp(_moodTarget, _moodTargetWanted, ease);
            _moodFov = Mathf.Lerp(_moodFov, _moodFovWanted, ease);
            if (_blend < 1f) _blend = Mathf.Min(1f, _blend + dt * _blendSpeed);
            Apply(_blend);
        }

        Setup Sample(float t)
        {
            // Smootherstep: zero velocity at both ends, so blends read as a camera
            // move rather than a lerp.
            float e = t * t * t * (t * (t * 6f - 15f) + 10f);
            return new Setup
            {
                position   = Vector3.Lerp(_from.position, _to.position, e),
                target     = Vector3.Lerp(_from.target, _to.target, e),
                fov        = Mathf.Lerp(_from.fov, _to.fov, e),
                drift      = Vector3.Lerp(_from.drift, _to.drift, e),
                driftSpeed = Mathf.Lerp(_from.driftSpeed, _to.driftSpeed, e),
                orbit      = Mathf.Lerp(_from.orbit, _to.orbit, e),
                dolly      = Mathf.Lerp(_from.dolly, _to.dolly, e),
            };
        }

        void Apply(float blend)
        {
            var s = Sample(blend);
            s.position += _moodPos;
            s.target += _moodTarget;
            s.fov += _moodFov;
            var pos = s.position;

            // Slow arc around the subject - a handful of degrees over a minute.
            if (Mathf.Abs(s.orbit) > 0.001f)
            {
                float angle = Mathf.Sin(_shotTime * 0.045f) * s.orbit;
                pos = s.target + Quaternion.Euler(0f, angle, 0f) * (pos - s.target);
            }

            // Barely perceptible push in, easing to a stop.
            if (Mathf.Abs(s.dolly) > 0.001f)
            {
                float k = 1f - Mathf.Exp(-_shotTime * 0.06f);
                pos += (s.target - pos).normalized * (s.dolly * k);
            }

            // Handheld-style float, three decorrelated sine pairs.
            float t = _shotTime * s.driftSpeed;
            pos += new Vector3(
                Mathf.Sin(t * 1.00f) * s.drift.x + Mathf.Sin(t * 2.30f) * s.drift.x * 0.28f,
                Mathf.Sin(t * 0.83f + 1.7f) * s.drift.y + Mathf.Sin(t * 1.91f) * s.drift.y * 0.24f,
                Mathf.Sin(t * 0.67f + 3.1f) * s.drift.z);

            Cam.transform.position = pos;
            Cam.transform.rotation = Quaternion.LookRotation((s.target - pos).normalized, Vector3.up);
            Cam.fieldOfView = s.fov;

            // Keep the focal plane on the subject for depth of field.
            FocusDistance = Vector3.Distance(pos, s.target);
        }

        /// <summary>Distance from the lens to the current subject, in metres.</summary>
        public float FocusDistance { get; private set; } = 20f;
    }
}
