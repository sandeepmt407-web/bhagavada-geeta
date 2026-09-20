using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gita.World
{
    /// <summary>
    /// Builds Kurukshetra at first light: the halted chariot between two armies,
    /// the moment before the Gita is spoken.
    ///
    /// The whole set is rendered as backlit silhouette against a dawn sky. That is a
    /// deliberate choice - it is the most reverent way to depict Krishna and Arjuna,
    /// it is genuinely cinematic, and it stays cheap enough for mid-range phones.
    /// </summary>
    public sealed class CinematicWorld : MonoBehaviour
    {
        // The sun sits low on +Z; the camera looks that way, so everything is rim-lit.
        public static readonly Vector3 SunDirection = new Vector3(0.06f, -0.085f, -1f).normalized;

        public Transform Chariot { get; private set; }
        public Light Sun { get; private set; }

        readonly List<Material> _owned = new();

        Material _silhouette, _silhouetteFlat, _ground, _cloth, _glow, _dust;

        public static CinematicWorld Create()
        {
            var go = new GameObject("~CinematicWorld");
            var world = go.AddComponent<CinematicWorld>();
            world.BuildAll();
            return world;
        }

        void BuildAll()
        {
            BuildMaterials();
            BuildLighting();
            BuildSky();
            BuildGround();
            BuildRidges();
            BuildArmies();
            BuildChariot();
            // No foreground layer. Spears close to the lens were tried for depth and
            // simply crossed the subject; the massed ranks and the haze already carry it.
            BuildBirds();
            BuildDust();
        }

        // ------------------------------------------------------------------
        // materials
        // ------------------------------------------------------------------

        /// <summary>
        /// Loads a material authored under Resources/Materials. These are real assets
        /// on purpose: a material built at runtime from Shader.Find has no shader
        /// variants in the player, because nothing referenced them at build time. The
        /// additive dust and glare came out as opaque squares that way.
        /// </summary>
        Material Load(string name, string fallbackShader)
        {
            var asset = Resources.Load<Material>("Materials/" + name);
            if (asset != null) return asset;

            Debug.LogWarning($"[Gita] Material asset missing: Materials/{name} - building one at runtime.");
            var shader = Shader.Find(fallbackShader)
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = name };
            _owned.Add(m);
            return m;
        }

        void BuildMaterials()
        {
            _silhouette     = Load("Silhouette",     "Universal Render Pipeline/Lit");
            _silhouetteFlat = Load("SilhouetteFlat", "Universal Render Pipeline/Lit");
            _ground         = Load("Ground",         "Universal Render Pipeline/Lit");
            _cloth          = Load("Banner",         "Universal Render Pipeline/Lit");
            _glow           = Load("SunGlow",        "Universal Render Pipeline/Unlit");
            _dust           = Load("Dust",           "Universal Render Pipeline/Particles/Unlit");
        }

        // ------------------------------------------------------------------
        // lighting and sky
        // ------------------------------------------------------------------

        void BuildLighting()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.LookRotation(SunDirection);

            Sun = go.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(1f, 0.74f, 0.45f);  // low dawn sun, warm
            Sun.intensity = 1.7f;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = 0.86f;
            Sun.shadowBias = 0.06f;
            Sun.shadowNormalBias = 0.5f;

            // A cool fill from the opposite side keeps the shadow side from going flat black.
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(28f, 190f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.42f, 0.53f, 0.82f);
            fill.intensity = 0.30f;
            fill.shadows = LightShadows.None;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.36f, 0.44f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.27f, 0.25f);
            RenderSettings.ambientGroundColor  = new Color(0.10f, 0.07f, 0.06f);
            RenderSettings.ambientIntensity = 1f;

            // Haze is what separates the ranks into depth layers and hides the edge of
            // the ground mesh. Warm, and thick enough to read as dawn air over a plain.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.62f, 0.42f, 0.30f);
            RenderSettings.fogDensity = 0.011f;
        }

        void BuildSky()
        {
            var sky = Resources.Load<Material>("Materials/DawnSky");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 2.15f;
            }
            else
            {
                Debug.LogWarning("[Gita] Dawn sky material missing - falling back to gradient ambient.");
            }

            DynamicGI.UpdateEnvironment();

            // A soft disc of glare sitting on the horizon, where the sun breaks.
            var glow = new GameObject("SunGlow");
            glow.transform.SetParent(transform, false);
            glow.transform.position = -SunDirection * 260f + Vector3.up * 6f;
            glow.transform.rotation = Quaternion.LookRotation(SunDirection);
            var mf = glow.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Quad(150f, 150f);
            var mr = glow.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _glow;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------
        // terrain
        // ------------------------------------------------------------------

        void BuildGround()
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(transform, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Ground(size: 620f, subdiv: 150, amplitude: 2.6f, seed: 1947);

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _ground;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        void BuildRidges()
        {
            // Three overlapping ridge cards at different depths give cheap aerial perspective.
            AddRidge(0, z: 300f, width: 1400f, peak: 26f, seed: 11);
            AddRidge(1, z: 230f, width: 1100f, peak: 18f, seed: 29);
            AddRidge(2, z: 170f, width: 900f,  peak: 11f, seed: 47);
        }

        void AddRidge(int ridgeIndex, float z, float width, float peak, int seed)
        {
            var go = new GameObject($"Ridge_{seed}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, z);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Ridge(width, baseHeight: 1.5f, peak: peak, segments: 220, seed: seed);

            // Nearer ridges are darker; far ones wash out into the haze.
            var mat = Load($"Ridge{ridgeIndex}", "Universal Render Pipeline/Unlit");

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // ------------------------------------------------------------------
        // the two armies
        // ------------------------------------------------------------------

        void BuildArmies()
        {
            var root = new GameObject("Armies").transform;
            root.SetParent(transform, false);

            // Two facing hosts, drawn back from the empty ground where the chariot halts.
            BuildHost(root, "Pandava", side: -1f, seed: 108);
            BuildHost(root, "Kaurava", side: +1f, seed: 216);
        }

        void BuildHost(Transform parent, string name, float side, int seed)
        {
            var host = new GameObject(name).transform;
            host.SetParent(parent, false);

            var rng = new System.Random(seed);
            const int ranks = 9;
            const int perRank = 7;

            for (int r = 0; r < ranks; r++)
            {
                float z = 34f + r * 15.5f;
                float spread = 11f + r * 4.2f;

                for (int i = 0; i < perRank; i++)
                {
                    float x = side * (spread + i * 6.4f + (float)rng.NextDouble() * 2.6f);
                    float jz = z + (float)rng.NextDouble() * 7f;
                    float height = 6.5f + (float)rng.NextDouble() * 3.5f;

                    // Only the nearest ranks pay for CPU cloth animation.
                    bool animate = r < 3;
                    AddBanner(host, new Vector3(x, 0f, jz), height, animate, rng);
                }

                AddMassedRank(host, side, z, spread, rng);
            }
        }

        /// <summary>
        /// The suggestion of a host standing behind its banners: an irregular low band
        /// of forms. Individually modelled soldiers read as crates at this scale, but a
        /// broken silhouette line reads unmistakably as ranked men.
        /// </summary>
        void AddMassedRank(Transform parent, float side, float z, float spread, System.Random rng)
        {
            int count = 14;
            for (int i = 0; i < count; i++)
            {
                float x = side * (spread - 2f + i * 3.6f + (float)rng.NextDouble() * 1.4f);
                float depth = z + 2f + (float)rng.NextDouble() * 5f;
                float h = 1.9f + (float)rng.NextDouble() * 1.1f;
                float w = 1.1f + (float)rng.NextDouble() * 0.9f;

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Rank";
                SafeDestroy(block.GetComponent<Collider>());
                block.transform.SetParent(parent, false);
                block.transform.position = new Vector3(x, h * 0.5f, depth);
                block.transform.localScale = new Vector3(w, h, w * 0.7f);
                block.transform.localRotation =
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 30f - 15f, 0f);

                var mr = block.GetComponent<MeshRenderer>();
                mr.sharedMaterial = _silhouette;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }

        void AddBanner(Transform parent, Vector3 pos, float height, bool animate, System.Random rng)
        {
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Banner";
            SafeDestroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(parent, false);
            pole.transform.position = pos + Vector3.up * (height * 0.5f);
            pole.transform.localScale = new Vector3(0.11f, height * 0.5f, 0.11f);
            var poleR = pole.GetComponent<MeshRenderer>();
            poleR.sharedMaterial = _silhouette;
            poleR.shadowCastingMode = ShadowCastingMode.Off;

            // Pennant hanging from the upper third of the pole.
            var cloth = new GameObject("Cloth");
            cloth.transform.SetParent(pole.transform.parent, false);
            cloth.transform.position = pos + Vector3.up * (height * 0.80f);
            cloth.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 24f - 12f, 0f);

            float w = 1.5f + (float)rng.NextDouble() * 1.1f;
            float h = 1.1f + (float)rng.NextDouble() * 0.7f;

            var mf = cloth.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Cloth(w, h, animate ? 8 : 2, animate ? 6 : 2);
            var mr = cloth.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _cloth;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            if (animate)
            {
                var wave = cloth.AddComponent<ClothWave>();
                wave.amplitude = 0.13f + (float)rng.NextDouble() * 0.06f;
                wave.speed = 1.7f + (float)rng.NextDouble() * 1.1f;
                wave.phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            }
        }

        // ------------------------------------------------------------------
        // the chariot
        // ------------------------------------------------------------------

        void BuildChariot()
        {
            var root = new GameObject("Chariot").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(-1.4f, 0f, 16f);
            root.rotation = Quaternion.Euler(0f, -108f, 0f); // near profile, so the team reads
            Chariot = root;

            // --- car body: a tapered box on a platform
            AddBox(root, "Platform", new Vector3(0f, 1.32f, 0f), new Vector3(2.5f, 0.22f, 1.7f));
            AddBox(root, "Cab",      new Vector3(-0.35f, 1.95f, 0f), new Vector3(1.7f, 1.05f, 1.55f));
            AddBox(root, "Rail",     new Vector3(-0.35f, 2.55f, 0f), new Vector3(1.8f, 0.1f, 1.65f));

            // --- wheels
            AddWheel(root, new Vector3(0.1f,  1.05f,  0.95f));
            AddWheel(root, new Vector3(0.1f,  1.05f, -0.95f));

            // --- axle and draught pole reaching forward to the yoke
            AddBox(root, "Axle", new Vector3(0.1f, 1.05f, 0f), new Vector3(0.14f, 0.14f, 2.1f));
            var shaft = AddBox(root, "Shaft", new Vector3(2.3f, 1.15f, 0f), new Vector3(3.6f, 0.13f, 0.13f));
            shaft.transform.localRotation = Quaternion.Euler(0f, 0f, 3.5f);

            // --- chhatra: the royal parasol over the car
            AddParasol(root, new Vector3(-0.35f, 3.35f, 0f));

            // --- Kapi-dhwaja: Hanuman's banner on the tall staff, Arjuna's ensign
            AddStaffBanner(root, new Vector3(-1.15f, 0f, 0f), staffHeight: 6.4f);

            // The team is deliberately absent. Blocked out from primitives the horses
            // read as a stack of crates, and the halted, unhitched chariot is both the
            // stronger silhouette and the truer image: "sthapaya me ratham".
        }

        GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            SafeDestroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _silhouette;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        GameObject AddCyl(Transform parent, string name, Vector3 localPos, Vector3 scale, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            SafeDestroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _silhouette;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>A spoked chariot wheel - the rim as a ring mesh, twelve spokes, a hub.</summary>
        void AddWheel(Transform parent, Vector3 localPos)
        {
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(parent, false);
            wheel.localPosition = localPos;
            wheel.localRotation = Quaternion.Euler(0f, 90f, 0f); // ring lies in the wheel plane

            const float radius = 1.05f;

            var rim = new GameObject("Rim");
            rim.transform.SetParent(wheel, false);
            var mf = rim.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Ring(radius, radius - 0.11f, 48);
            var mr = rim.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _silhouetteFlat;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            const int spokes = 12;
            for (int i = 0; i < spokes; i++)
            {
                float a = i / (float)spokes * 360f;
                var spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spoke.name = "Spoke";
                SafeDestroy(spoke.GetComponent<Collider>());
                spoke.transform.SetParent(wheel, false);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, a);
                spoke.transform.localPosition = spoke.transform.localRotation * new Vector3(0f, radius * 0.5f, 0f);
                spoke.transform.localScale = new Vector3(0.06f, radius, 0.06f);
                var sr = spoke.GetComponent<MeshRenderer>();
                sr.sharedMaterial = _silhouette;
                sr.shadowCastingMode = ShadowCastingMode.Off;
            }

            AddCyl(wheel, "Hub", Vector3.zero, new Vector3(0.2f, 0.09f, 0.2f), new Vector3(90f, 0f, 0f));
        }

        void AddParasol(Transform parent, Vector3 localPos)
        {
            AddCyl(parent, "ParasolStaff", localPos + Vector3.up * 0.1f,
                new Vector3(0.05f, 0.62f, 0.05f), Vector3.zero);

            var canopy = new GameObject("Canopy");
            canopy.transform.SetParent(parent, false);
            canopy.transform.localPosition = localPos + Vector3.up * 0.62f;

            var mf = canopy.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Cone(radius: 0.96f, height: 0.46f, segments: 28);
            var mr = canopy.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _silhouette;
            mr.shadowCastingMode = ShadowCastingMode.On;

            // Finial
            AddCyl(parent, "Finial", localPos + Vector3.up * 1.12f,
                new Vector3(0.06f, 0.15f, 0.06f), Vector3.zero);
        }

        void AddStaffBanner(Transform parent, Vector3 localPos, float staffHeight)
        {
            AddCyl(parent, "Dhwaja", localPos + Vector3.up * (staffHeight * 0.5f),
                new Vector3(0.09f, staffHeight * 0.5f, 0.09f), Vector3.zero);

            var cloth = new GameObject("DhwajaCloth");
            cloth.transform.SetParent(parent, false);
            cloth.transform.localPosition = localPos + Vector3.up * (staffHeight - 1.5f);
            // Turn the pennant across the chariot so it presents its face, not its edge.
            cloth.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var mf = cloth.AddComponent<MeshFilter>();
            mf.sharedMesh = ProcMesh.Cloth(1.45f, 0.92f, 10, 7);
            var mr = cloth.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _cloth;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var wave = cloth.AddComponent<ClothWave>();
            wave.amplitude = 0.22f;
            wave.speed = 1.55f;
            wave.frequency = 1.9f;
        }

        /// <summary>
        /// A standing horse, blocked out from primitives. At silhouette scale the
        /// proportions carry the read, so the form stays deliberately simple.
        /// </summary>
        void AddHorse(Transform parent, Vector3 localPos, float facing)
        {
            var h = new GameObject("Horse").transform;
            h.SetParent(parent, false);
            h.localPosition = localPos;
            h.localRotation = Quaternion.Euler(0f, facing, 0f);

            // barrel
            AddBox(h, "Barrel", new Vector3(0f, 1.52f, 0f), new Vector3(2.05f, 0.92f, 0.72f));
            // chest and hindquarters, slightly proud of the barrel
            AddBox(h, "Chest",  new Vector3(-0.85f, 1.55f, 0f), new Vector3(0.62f, 1.02f, 0.80f));
            AddBox(h, "Rump",   new Vector3( 0.88f, 1.58f, 0f), new Vector3(0.66f, 1.00f, 0.78f));

            // neck rising from the chest, then the head
            var neck = AddBox(h, "Neck", new Vector3(-1.30f, 2.12f, 0f), new Vector3(0.44f, 1.10f, 0.52f));
            neck.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
            var head = AddBox(h, "Head", new Vector3(-1.74f, 2.66f, 0f), new Vector3(0.74f, 0.34f, 0.34f));
            head.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);

            // legs
            AddBox(h, "LegFL", new Vector3(-0.78f, 0.55f,  0.28f), new Vector3(0.17f, 1.12f, 0.17f));
            AddBox(h, "LegFR", new Vector3(-0.78f, 0.55f, -0.28f), new Vector3(0.17f, 1.12f, 0.17f));
            AddBox(h, "LegBL", new Vector3( 0.82f, 0.55f,  0.28f), new Vector3(0.18f, 1.12f, 0.18f));
            AddBox(h, "LegBR", new Vector3( 0.82f, 0.55f, -0.28f), new Vector3(0.18f, 1.12f, 0.18f));

            // tail
            var tail = AddBox(h, "Tail", new Vector3(1.22f, 1.42f, 0f), new Vector3(0.16f, 0.86f, 0.16f));
            tail.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        }

        // ------------------------------------------------------------------
        // foreground and sky life
        // ------------------------------------------------------------------

        /// <summary>A few birds turning high over the field - the only moving life in frame.</summary>
        void BuildBirds()
        {
            var flock = new GameObject("Birds");
            flock.transform.SetParent(transform, false);

            var rng = new System.Random(77);
            for (int i = 0; i < 7; i++)
            {
                var bird = new GameObject("Bird");
                bird.transform.SetParent(flock.transform, false);
                bird.transform.localScale = Vector3.one * 0.5f;

                var mf = bird.AddComponent<MeshFilter>();
                mf.sharedMesh = BirdMesh();
                var mr = bird.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _silhouetteFlat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;

                var drift = bird.AddComponent<BirdDrift>();
                drift.radius = 20f + (float)rng.NextDouble() * 16f;
                drift.height = 19f + (float)rng.NextDouble() * 8f;
                drift.speed = 0.035f + (float)rng.NextDouble() * 0.03f;
                drift.phase = (float)rng.NextDouble() * Mathf.PI * 2f;
                drift.centre = new Vector3(24f, 0f, 58f);
            }
        }

        /// <summary>A shallow V - enough to read as a bird at this distance.</summary>
        static Mesh BirdMesh()
        {
            var mesh = new Mesh { name = "Bird" };
            mesh.vertices = new[]
            {
                new Vector3(-0.9f, 0.22f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.22f, 0f),
                new Vector3(-0.85f, 0.10f, 0f), new Vector3(0.85f, 0.10f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 3, 1, 4, 2, 3, 1, 0, 4, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------
        // atmosphere
        // ------------------------------------------------------------------

        void BuildDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 6f, 22f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 26f;
            main.startSpeed = 0.28f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.62f, 0.5f), new Color(1f, 0.72f, 0.45f, 0.18f));
            main.maxParticles = 320;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.004f; // motes drift upward on the morning thermals
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = 14f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(70f, 14f, 60f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.42f;
            noise.frequency = 0.16f;
            noise.scrollSpeed = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _dust;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 1;
        }

        /// <summary>
        /// The set is also built from editor tooling (for look-development renders),
        /// where Object.Destroy is not allowed.
        /// </summary>
        static void SafeDestroy(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        void OnDestroy()
        {
            foreach (var m in _owned)
                if (m != null) SafeDestroy(m);
            _owned.Clear();
        }
    }
}
