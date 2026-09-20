using System.Collections.Generic;
using UnityEngine;

namespace Gita.World
{
    /// <summary>
    /// Mesh generators for the Kurukshetra set. Everything the camera sees is built
    /// here at runtime - there are no authored models in the project.
    /// </summary>
    public static class ProcMesh
    {
        /// <summary>
        /// A subdivided ground plane displaced by layered value noise, so the plain
        /// undulates gently instead of reading as a flat card.
        /// </summary>
        public static Mesh Ground(float size, int subdiv, float amplitude, int seed)
        {
            subdiv = Mathf.Clamp(subdiv, 2, 254);
            int n = subdiv + 1;
            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];
            var tris = new int[subdiv * subdiv * 6];

            float half = size * 0.5f;
            float step = size / subdiv;

            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float px = -half + x * step;
                float pz = -half + z * step;

                // Layered noise; the low octave gives broad swells, the high one grit.
                float h = Noise(px * 0.012f, pz * 0.012f, seed) * 1.0f
                        + Noise(px * 0.047f, pz * 0.047f, seed + 17) * 0.35f
                        + Noise(px * 0.130f, pz * 0.130f, seed + 91) * 0.12f;
                h *= amplitude;

                // Flatten the middle so the chariot sits on level ground.
                float d = new Vector2(px, pz).magnitude;
                h *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(14f, 60f, d));

                int i = z * n + x;
                verts[i] = new Vector3(px, h, pz);
                uvs[i] = new Vector2(x / (float)subdiv, z / (float)subdiv);

                // Bake a little dust variation into vertex colour.
                float dust = 0.82f + Noise(px * 0.08f, pz * 0.08f, seed + 303) * 0.18f;
                colors[i] = new Color(dust, dust, dust, 1f);
            }

            int t = 0;
            for (int z = 0; z < subdiv; z++)
            for (int x = 0; x < subdiv; x++)
            {
                int i = z * n + x;
                tris[t++] = i;         tris[t++] = i + n;     tris[t++] = i + 1;
                tris[t++] = i + 1;     tris[t++] = i + n;     tris[t++] = i + n + 1;
            }

            var mesh = new Mesh { name = "Ground" };
            mesh.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A distant ridge: a noisy skyline extruded downward into a flat silhouette card.
        /// Cheap, and reads as far-off hills when placed against the sky.
        /// </summary>
        public static Mesh Ridge(float width, float baseHeight, float peak, int segments, int seed)
        {
            segments = Mathf.Max(segments, 8);
            int n = segments + 1;
            var verts = new Vector3[n * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)segments;
                float x = (t - 0.5f) * width;

                float h = Noise(t * 7.3f, 0.5f, seed) * 1.0f
                        + Noise(t * 19.1f, 1.5f, seed + 51) * 0.4f
                        + Noise(t * 41.7f, 2.5f, seed + 77) * 0.15f;
                h = baseHeight + Mathf.Abs(h) * peak;

                verts[i] = new Vector3(x, h, 0f);
                verts[i + n] = new Vector3(x, -baseHeight * 4f, 0f);
            }

            int k = 0;
            for (int i = 0; i < segments; i++)
            {
                tris[k++] = i;         tris[k++] = i + 1;     tris[k++] = i + n;
                tris[k++] = i + 1;     tris[k++] = i + n + 1; tris[k++] = i + n;
            }

            var mesh = new Mesh { name = "Ridge" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A banner cloth: a grid quad. The wave is applied per-frame by
        /// <see cref="ClothWave"/>, which keeps the mesh itself reusable.
        /// </summary>
        public static Mesh Cloth(float width, float height, int cols, int rows)
        {
            cols = Mathf.Max(cols, 2);
            rows = Mathf.Max(rows, 2);
            int nx = cols + 1, ny = rows + 1;
            var verts = new Vector3[nx * ny];
            var uvs = new Vector2[nx * ny];
            var tris = new int[cols * rows * 6];

            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                float u = x / (float)cols;
                float v = y / (float)rows;
                int i = y * nx + x;
                verts[i] = new Vector3(u * width, (v - 0.5f) * height, 0f);
                uvs[i] = new Vector2(u, v);
            }

            int t = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                int i = y * nx + x;
                tris[t++] = i;          tris[t++] = i + nx;      tris[t++] = i + 1;
                tris[t++] = i + 1;      tris[t++] = i + nx;      tris[t++] = i + nx + 1;
            }

            var mesh = new Mesh { name = "Cloth" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            return mesh;
        }

        /// <summary>
        /// A flat ring (annulus) in the XY plane - the chariot wheel rim and the chakra.
        /// Single-sided; callers that need to see the back face use a no-cull material,
        /// which keeps the normals clean (mirrored triangles average to bad shading).
        /// </summary>
        public static Mesh Ring(float outer, float inner, int segments)
        {
            segments = Mathf.Max(segments, 8);
            var verts = new Vector3[segments * 2];
            var uvs = new Vector2[segments * 2];
            var tris = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                verts[i] = new Vector3(cos * outer, sin * outer, 0f);
                verts[i + segments] = new Vector3(cos * inner, sin * inner, 0f);
                uvs[i] = new Vector2(i / (float)segments, 1f);
                uvs[i + segments] = new Vector2(i / (float)segments, 0f);
            }

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                int a = i, b = j, c = i + segments, d = j + segments;
                tris[t++] = a; tris[t++] = b; tris[t++] = c;
                tris[t++] = b; tris[t++] = d; tris[t++] = c;
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A cone standing on +Y: the royal parasol over the chariot. A squashed
        /// sphere was tried here first and read unmistakably as a flying saucer.
        /// </summary>
        public static Mesh Cone(float radius, float height, int segments)
        {
            segments = Mathf.Max(segments, 6);

            // apex + rim ring + base centre
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 6];

            verts[0] = new Vector3(0f, height, 0f);          // apex
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            verts[segments + 1] = Vector3.zero;              // base centre

            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                int cur = i + 1;
                int next = (i + 1) % segments + 1;
                // side
                tris[t++] = 0; tris[t++] = next; tris[t++] = cur;
                // base, so the underside is not see-through from below
                tris[t++] = segments + 1; tris[t++] = cur; tris[t++] = next;
            }

            var mesh = new Mesh { name = "Cone" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A camera-facing quad, used for glows and light shafts.</summary>
        public static Mesh Quad(float width, float height)
        {
            var mesh = new Mesh { name = "Quad" };
            float hw = width * 0.5f, hh = height * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
                new Vector3(-hw,  hh, 0f), new Vector3(hw,  hh, 0f)
            };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- noise ---------------------------------------------------------
        // Deterministic value noise with smooth interpolation. Self-contained so
        // the scene is identical on every device and every run.

        static float Noise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash(xi,     yi,     seed);
            float b = Hash(xi + 1, yi,     seed);
            float c = Hash(xi,     yi + 1, seed);
            float d = Hash(xi + 1, yi + 1, seed);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v) * 2f - 1f;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
