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
