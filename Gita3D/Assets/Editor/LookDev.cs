using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Gita.World;

namespace Gita.EditorTools
{
    /// <summary>
    /// Builds the Kurukshetra set and renders each camera composition to a PNG, so the
    /// look can be judged without deploying to a handset.
    /// </summary>
    public static class LookDev
    {
        const int Width = 720;
        const int Height = 1280;

        public static void Render()
        {
            var world = CinematicWorld.Create();

            var camGo = new GameObject("LookDevCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 900f;
            cam.allowHDR = true;

            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            urp.renderShadows = true;

            var director = CameraDirector.Create(cam);
            var grade = PostFX.Create(director);

            string outDir = Path.GetFullPath("../build/lookdev");
            Directory.CreateDirectory(outDir);

            foreach (Shot shot in System.Enum.GetValues(typeof(Shot)))
            {
                director.SnapTo(shot);
                Shoot(cam, Path.Combine(outDir, $"{shot}.png"));
            }

            Object.DestroyImmediate(camGo);
            if (grade != null) Object.DestroyImmediate(grade.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);

            Debug.Log($"[LookDev] Wrote shots to {outDir}");
        }

        /// <summary>
        /// One still per mood, so the ten looks can be compared side by side without a
        /// handset. Each is rendered at a verse that actually carries that mood, and the
        /// motif layers are simulated forward first - particles do not run outside play
        /// mode, so an unsimulated still shows an empty sky.
        /// </summary>
        public static void RenderMoods()
        {
            var representative = new (string name, int chapter, int verse)[]
            {
                ("01-despair",    1, 28),
                ("02-duty",       3,  8),
                ("03-knowledge",  2, 20),
                ("04-devotion",  12,  6),
                ("05-meditation", 6, 19),
                ("06-cosmic",    11, 12),
                ("07-divine",    10, 20),
                ("08-field",     13,  2),
                ("09-gunas",     14,  5),
                ("10-liberation",18, 20),
            };

            var previousSky = RenderSettings.skybox;

            var world = CinematicWorld.Create();

            var camGo = new GameObject("LookDevCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 900f;
            cam.allowHDR = true;

            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            urp.renderShadows = true;

            var director = CameraDirector.Create(cam);
            var grade = PostFX.Create(director);
            var mood = MoodDirector.Create(world, director, grade);

            string outDir = Path.GetFullPath("../build/moods");
            Directory.CreateDirectory(outDir);

            foreach (var (name, chapter, verse) in representative)
            {
                mood.Snap(chapter, verse);
                director.SnapTo(Shot.Reader);
                mood.Layers?.Simulate(18f);
                Shoot(cam, Path.Combine(outDir, $"{name}.png"));
            }

            Object.DestroyImmediate(camGo);
            if (mood != null) Object.DestroyImmediate(mood.gameObject);
            if (grade != null) Object.DestroyImmediate(grade.gameObject);
            if (world != null) Object.DestroyImmediate(world.gameObject);
            RenderSettings.skybox = previousSky;

            Debug.Log($"[LookDev] Wrote mood stills to {outDir}");
        }

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR)
            {
                antiAliasing = 1
            };
            cam.targetTexture = rt;

            // Render twice: the first pass warms up the pipeline's transient resources,
            // and post-processing history buffers are not valid until then.
            cam.Render();
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            shot.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log($"[LookDev] {Path.GetFileName(path)}");

            cam.targetTexture = null;
            Object.DestroyImmediate(shot);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
