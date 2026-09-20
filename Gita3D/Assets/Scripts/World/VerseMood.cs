using UnityEngine;

namespace Gita.World
{
    /// <summary>Which animated element drifts through the frame.</summary>
    public enum Motif
    {
        None,
        Dust,     // the battlefield itself: dry, warm, hanging in the air
        Embers,   // slow rising motes of light
        Petals,   // falling, warm, unhurried - offerings
        Stars,    // still points high in a night sky
        Sparks,   // fast, bright, upward - the cosmic form
        Leaves,   // drifting sideways on a wind across the field
        Motes,    // three colours at once, for the three gunas
        Light,    // soft descent, pale gold
    }

    /// <summary>
    /// One complete look: light, air, sky, grade, camera and the drifting element.
    /// A mood is a whole visual treatment of the set rather than a colour tweak, which
    /// is why every one of these carries a camera framing as well.
    /// </summary>
    public sealed class MoodLook
    {
        public string Name = "";

        // light
        public Color SunColor = new(1f, 0.74f, 0.45f);
        public float SunIntensity = 1.7f;
        public Color FillColor = new(0.42f, 0.53f, 0.82f);
        public float FillIntensity = 0.30f;
        public float AmbientIntensity = 2.15f;

        // air
        public Color FogColor = new(0.62f, 0.42f, 0.30f);
        public float FogDensity = 0.011f;

        // sky
        public Color SkyTint = new(0.5f, 0.5f, 0.5f);
        public float SkyExposure = 0.42f;
        public float SkyRotation = 200f;

        // grade
        public float Bloom = 0.9f;
        public Color BloomTint = new(1f, 0.86f, 0.68f);
        public float Saturation = 6f;
        public float Contrast = 8f;
        public Color ColorFilter = new(1f, 0.97f, 0.93f);
        public float Vignette = 0.20f;

        // the drifting element
        public Motif Motif = Motif.Dust;
        public float MotifRate = 14f;
        public Color MotifNear = new(1f, 0.85f, 0.62f, 0.50f);
        public Color MotifFar = new(1f, 0.72f, 0.45f, 0.18f);

        /// <summary>
        /// How much the reader screen has to darken the set behind its text.
        ///
        /// A fixed value cannot work across ten looks: the eleventh chapter is a blazing
        /// gold sky and cream text disappears into it, while the sixth is close to black
        /// and the same amount of scrim leaves nothing of the scene to see.
        /// </summary>
        public float Scrim = 0.70f;

        // camera, as an offset on top of whatever shot the screen asked for
        public Vector3 CamOffset = Vector3.zero;
        public Vector3 TargetOffset = Vector3.zero;
        public float FovDelta = 0f;
    }

    /// <summary>
    /// Gives every verse in the corpus its own treatment of the set.
    ///
    /// Ten looks, one per movement of the argument, mapped chapter by chapter and then
    /// corrected verse by verse where a chapter turns: the despair of the opening runs
    /// ten verses into the second chapter, and the answer only begins after it. On top
    /// of that, each individual verse shifts the sky, the haze and the framing by a
    /// small deterministic amount, so no two pages are quite the same image and the
    /// same verse is always the same image.
    /// </summary>
    public static class VerseMood
    {
        public enum Mood
        {
            Despair, Duty, Knowledge, Devotion, Meditation,
            Cosmic, Divine, Field, Gunas, Liberation
        }

        // ------------------------------------------------------------------
        // the looks
        // ------------------------------------------------------------------

        static readonly MoodLook[] Looks =
        {
            // Despair - the field before first light. Cold, thick, heavy.
            new()
            {
                Name = "Before the light",
                Scrim = 0.58f,
                SunColor = new Color(0.74f, 0.72f, 0.84f), SunIntensity = 1.25f,
                FillColor = new Color(0.36f, 0.42f, 0.70f), FillIntensity = 0.38f,
                AmbientIntensity = 1.95f,
                FogColor = new Color(0.34f, 0.36f, 0.46f), FogDensity = 0.0180f,
                SkyTint = new Color(0.42f, 0.45f, 0.58f), SkyExposure = 0.44f, SkyRotation = 200f,
                Bloom = 0.72f, BloomTint = new Color(0.75f, 0.80f, 1f),
                Saturation = -14f, Contrast = 6f,
                ColorFilter = new Color(0.90f, 0.93f, 1.02f), Vignette = 0.34f,
                Motif = Motif.Dust, MotifRate = 17f,
                MotifNear = new Color(0.72f, 0.76f, 0.88f, 0.34f),
                MotifFar = new Color(0.55f, 0.60f, 0.78f, 0.12f),
                CamOffset = new Vector3(0f, -0.35f, -0.6f),
                TargetOffset = new Vector3(0f, -0.9f, 0f), FovDelta = 2f,
            },

            // Duty - the set as first built: dawn breaking behind the halted chariot.
            new()
            {
                Name = "The work in front of you",
                Scrim = 0.70f,
                Motif = Motif.Dust, MotifRate = 14f,
            },

            // Knowledge - the air clears, the sun climbs, the argument opens out.
            new()
            {
                Name = "What does not die",
                Scrim = 0.76f,
                SunColor = new Color(1f, 0.85f, 0.62f), SunIntensity = 2.0f,
                FillColor = new Color(0.48f, 0.58f, 0.84f), FillIntensity = 0.26f,
                AmbientIntensity = 2.40f,
                FogColor = new Color(0.66f, 0.56f, 0.44f), FogDensity = 0.0085f,
                SkyTint = new Color(0.56f, 0.55f, 0.52f), SkyExposure = 0.52f, SkyRotation = 210f,
                Bloom = 1.00f, BloomTint = new Color(1f, 0.92f, 0.78f),
                Saturation = 8f, Contrast = 9f,
                ColorFilter = new Color(1f, 0.99f, 0.95f), Vignette = 0.18f,
                Motif = Motif.Embers, MotifRate = 22f,
                MotifNear = new Color(1f, 0.93f, 0.74f, 0.55f),
                MotifFar = new Color(1f, 0.82f, 0.55f, 0.16f),
                CamOffset = new Vector3(0f, 0.5f, -0.4f),
                TargetOffset = new Vector3(0f, 0.6f, 0f), FovDelta = 3f,
            },

            // Devotion - warm, close, and strewn with what is offered.
            new()
            {
                Name = "A leaf, a flower, some water",
                Scrim = 0.72f,
                SunColor = new Color(1f, 0.68f, 0.50f), SunIntensity = 1.9f,
                FillColor = new Color(0.52f, 0.44f, 0.72f), FillIntensity = 0.30f,
                AmbientIntensity = 2.30f,
                FogColor = new Color(0.70f, 0.44f, 0.38f), FogDensity = 0.0120f,
                SkyTint = new Color(0.60f, 0.46f, 0.44f), SkyExposure = 0.50f, SkyRotation = 186f,
                Bloom = 1.25f, BloomTint = new Color(1f, 0.78f, 0.62f),
                Saturation = 12f, Contrast = 6f,
                ColorFilter = new Color(1.02f, 0.95f, 0.92f), Vignette = 0.22f,
                Motif = Motif.Petals, MotifRate = 13f,
                MotifNear = new Color(1f, 0.74f, 0.66f, 0.62f),
                MotifFar = new Color(1f, 0.58f, 0.44f, 0.22f),
                CamOffset = new Vector3(0.4f, -0.1f, 0.8f),
                TargetOffset = new Vector3(0f, 0.2f, 0f), FovDelta = -3f,
            },

            // Meditation - night, and the seat made steady.
            new()
            {
                Name = "A lamp in a windless place",
                Scrim = 0.55f,
                SunColor = new Color(0.58f, 0.66f, 0.90f), SunIntensity = 1.05f,
                FillColor = new Color(0.30f, 0.38f, 0.66f), FillIntensity = 0.34f,
                AmbientIntensity = 2.05f,
                FogColor = new Color(0.24f, 0.30f, 0.46f), FogDensity = 0.0105f,
                SkyTint = new Color(0.34f, 0.40f, 0.58f), SkyExposure = 0.38f, SkyRotation = 340f,
                Bloom = 0.70f, BloomTint = new Color(0.70f, 0.80f, 1f),
                Saturation = -6f, Contrast = 10f,
                ColorFilter = new Color(0.94f, 0.97f, 1.06f), Vignette = 0.32f,
                Motif = Motif.Stars, MotifRate = 16f,
                MotifNear = new Color(0.94f, 0.96f, 1f, 1f),
                MotifFar = new Color(0.74f, 0.84f, 1f, 0.35f),
                CamOffset = new Vector3(0f, 0.3f, -1.2f),
                TargetOffset = new Vector3(0f, 1.0f, 0f), FovDelta = -2f,
            },

            // Cosmic - the eleventh chapter. Light that is too much to look at.
            new()
            {
                Name = "A thousand suns",
                Scrim = 0.88f,
                SunColor = new Color(1f, 0.62f, 0.30f), SunIntensity = 3.1f,
                FillColor = new Color(0.70f, 0.40f, 0.66f), FillIntensity = 0.45f,
                AmbientIntensity = 3.00f,
                FogColor = new Color(0.78f, 0.40f, 0.24f), FogDensity = 0.0140f,
                SkyTint = new Color(0.78f, 0.52f, 0.38f), SkyExposure = 0.80f, SkyRotation = 200f,
                Bloom = 1.80f, BloomTint = new Color(1f, 0.70f, 0.42f),
                Saturation = 18f, Contrast = 14f,
                ColorFilter = new Color(1.06f, 0.95f, 0.86f), Vignette = 0.28f,
                Motif = Motif.Sparks, MotifRate = 55f,
                MotifNear = new Color(1f, 0.86f, 0.52f, 0.85f),
                MotifFar = new Color(1f, 0.46f, 0.20f, 0.25f),
                CamOffset = new Vector3(0f, -0.6f, -1.4f),
                TargetOffset = new Vector3(0f, 3.5f, 0f), FovDelta = 8f,
            },

            // Divine - the glories, and the one who speaks them. High and clear.
            new()
            {
                Name = "Whenever dharma declines",
                Scrim = 0.80f,
                SunColor = new Color(1f, 0.88f, 0.72f), SunIntensity = 2.3f,
                FillColor = new Color(0.50f, 0.60f, 0.86f), FillIntensity = 0.24f,
                AmbientIntensity = 2.70f,
                FogColor = new Color(0.72f, 0.66f, 0.60f), FogDensity = 0.0075f,
                SkyTint = new Color(0.62f, 0.60f, 0.62f), SkyExposure = 0.60f, SkyRotation = 150f,
                Bloom = 1.35f, BloomTint = new Color(1f, 0.94f, 0.86f),
                Saturation = 6f, Contrast = 7f,
                ColorFilter = new Color(1f, 0.99f, 0.99f), Vignette = 0.16f,
                Motif = Motif.Light, MotifRate = 18f,
                MotifNear = new Color(1f, 0.96f, 0.86f, 0.55f),
                MotifFar = new Color(0.92f, 0.90f, 1f, 0.16f),
                CamOffset = new Vector3(0f, 1.3f, -1.0f),
                TargetOffset = new Vector3(0f, 3.0f, 0f), FovDelta = 4f,
            },

            // Field - the field and the knower of it; the tree with its roots above.
            new()
            {
                Name = "The field and its knower",
                Scrim = 0.72f,
                SunColor = new Color(0.96f, 0.80f, 0.52f), SunIntensity = 1.75f,
                FillColor = new Color(0.44f, 0.56f, 0.70f), FillIntensity = 0.30f,
                AmbientIntensity = 2.10f,
                FogColor = new Color(0.58f, 0.52f, 0.36f), FogDensity = 0.0100f,
                SkyTint = new Color(0.50f, 0.52f, 0.46f), SkyExposure = 0.46f, SkyRotation = 236f,
                Bloom = 0.85f, BloomTint = new Color(0.96f, 0.92f, 0.72f),
                Saturation = 4f, Contrast = 8f,
                ColorFilter = new Color(0.99f, 1f, 0.95f), Vignette = 0.21f,
                Motif = Motif.Leaves, MotifRate = 11f,
                MotifNear = new Color(0.96f, 0.86f, 0.52f, 0.55f),
                MotifFar = new Color(0.72f, 0.72f, 0.40f, 0.18f),
                CamOffset = new Vector3(-0.5f, -0.2f, 0.4f),
                TargetOffset = new Vector3(0f, -0.4f, 0f), FovDelta = 1f,
            },

            // Gunas - three strands, and the two natures. Unsettled by design.
            new()
            {
                Name = "Three strands",
                Scrim = 0.78f,
                SunColor = new Color(0.92f, 0.76f, 0.62f), SunIntensity = 1.5f,
                FillColor = new Color(0.56f, 0.44f, 0.70f), FillIntensity = 0.36f,
                AmbientIntensity = 1.90f,
                FogColor = new Color(0.50f, 0.42f, 0.42f), FogDensity = 0.0130f,
                SkyTint = new Color(0.46f, 0.42f, 0.48f), SkyExposure = 0.40f, SkyRotation = 96f,
                Bloom = 0.95f, BloomTint = new Color(0.92f, 0.82f, 0.95f),
                Saturation = 2f, Contrast = 11f,
                ColorFilter = new Color(0.99f, 0.96f, 1f), Vignette = 0.26f,
                Motif = Motif.Motes, MotifRate = 20f,
                MotifNear = new Color(1f, 0.90f, 0.62f, 0.50f),
                MotifFar = new Color(0.62f, 0.52f, 0.82f, 0.22f),
                CamOffset = new Vector3(0.8f, 0.1f, -0.3f),
                TargetOffset = new Vector3(-0.4f, 0.3f, 0f), FovDelta = 0f,
            },

            // Liberation - the last chapter. Full sunrise, and the weight put down.
            new()
            {
                Name = "Let go, and be free",
                Scrim = 0.84f,
                SunColor = new Color(1f, 0.90f, 0.70f), SunIntensity = 2.6f,
                FillColor = new Color(0.52f, 0.62f, 0.88f), FillIntensity = 0.22f,
                AmbientIntensity = 2.80f,
                FogColor = new Color(0.80f, 0.68f, 0.54f), FogDensity = 0.0065f,
                SkyTint = new Color(0.66f, 0.62f, 0.58f), SkyExposure = 0.66f, SkyRotation = 200f,
                Bloom = 1.50f, BloomTint = new Color(1f, 0.92f, 0.76f),
                Saturation = 10f, Contrast = 6f,
                ColorFilter = new Color(1.02f, 1f, 0.96f), Vignette = 0.12f,
                Motif = Motif.Light, MotifRate = 26f,
                MotifNear = new Color(1f, 0.94f, 0.76f, 0.62f),
                MotifFar = new Color(1f, 0.86f, 0.60f, 0.20f),
                CamOffset = new Vector3(0f, 1.0f, -0.6f),
                TargetOffset = new Vector3(0f, 2.4f, 0f), FovDelta = 2f,
            },
        };

        // ------------------------------------------------------------------
        // mapping
        // ------------------------------------------------------------------

        /// <summary>The subject of each chapter, in order. Index 0 is chapter 1.</summary>
        static readonly Mood[] ByChapter =
        {
            Mood.Despair,     //  1  the despondency on the field
            Mood.Knowledge,   //  2  sankhya - the imperishable self
            Mood.Duty,        //  3  karma - action without attachment
            Mood.Knowledge,   //  4  knowledge, and the renunciation of action
            Mood.Knowledge,   //  5  renunciation
            Mood.Meditation,  //  6  dhyana - the seat made steady
            Mood.Divine,      //  7  knowledge and realisation
            Mood.Meditation,  //  8  the imperishable, and the hour of passing
            Mood.Devotion,    //  9  the royal secret
            Mood.Divine,      // 10  the glories
            Mood.Cosmic,      // 11  the vision of the universal form
            Mood.Devotion,    // 12  bhakti
            Mood.Field,       // 13  the field and its knower
            Mood.Gunas,       // 14  the three strands of nature
            Mood.Field,       // 15  the eternal tree
            Mood.Gunas,       // 16  two natures, divine and otherwise
            Mood.Devotion,    // 17  three kinds of faith
            Mood.Liberation,  // 18  release
        };

        /// <summary>
        /// Where a chapter's own subject does not begin at its first verse, or where a
        /// passage inside it turns. Each entry covers an inclusive verse range, and the
        /// first match wins.
        /// </summary>
        static readonly (int chapter, int from, int to, Mood mood)[] Passages =
        {
            // The second chapter opens still inside the first chapter's grief; the
            // answer does not begin until 2.11.
            (2, 1, 10, Mood.Despair),
            // The two verses everyone knows: they are about doing the work.
            (2, 47, 48, Mood.Duty),
            // The steady mind, at the close of the chapter.
            (2, 54, 72, Mood.Meditation),
            // "Whenever dharma declines, I come forth."
            (4, 6, 8, Mood.Divine),
            // The renunciate already at peace.
            (5, 24, 29, Mood.Meditation),
            // A leaf, a flower, a fruit, some water.
            (9, 22, 34, Mood.Devotion),
            // Arjuna asks to see, and is shown.
            (11, 1, 8, Mood.Divine),
            // "I am time, grown old, come forth to annihilate the worlds."
            (11, 32, 34, Mood.Cosmic),
            // Krishna returns to his own shape and the terror subsides.
            (11, 47, 55, Mood.Devotion),
            // The tree with its roots above and its branches below.
            (15, 1, 4, Mood.Field),
            // The qualities of one born to a divine destiny.
            (16, 1, 3, Mood.Divine),
            // The last word of the whole song: abandon everything, come to me.
            (18, 62, 66, Mood.Devotion),
        };

        public static Mood MoodOf(int chapter, int verse)
        {
            foreach (var p in Passages)
                if (p.chapter == chapter && verse >= p.from && verse <= p.to) return p.mood;

            if (chapter >= 1 && chapter <= ByChapter.Length) return ByChapter[chapter - 1];
            return Mood.Duty;
        }

        public static MoodLook LookOf(Mood mood) => Looks[(int)mood];

        public static MoodLook LookOf(int chapter, int verse) => LookOf(MoodOf(chapter, verse));

        /// <summary>
        /// A stable number in [0,1) for a verse. The same verse always produces the same
        /// value, so a reader who comes back to 2.47 finds the sky they left.
        /// </summary>
        public static float Variation(int chapter, int verse)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)chapter) * 16777619u;
                h = (h ^ (uint)verse) * 16777619u;
                h ^= h >> 13;
                h *= 2654435761u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
