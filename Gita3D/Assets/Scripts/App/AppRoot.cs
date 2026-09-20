using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using Gita.Data;
using Gita.UI;
using Gita.World;

namespace Gita.App
{
    public enum ScreenId { Home, Chapters, Reader, Language, Search, Bookmarks, Collections }

    /// <summary>
    /// Single entry point. Builds the world, the camera and the interface at runtime,
    /// then owns navigation between screens. The scene itself holds only this component.
    /// </summary>
    public sealed class AppRoot : MonoBehaviour
    {
        public static AppRoot Instance { get; private set; }

        public CameraDirector Director { get; private set; }
        public CinematicWorld World { get; private set; }
        public PostFX Grade { get; private set; }
        public MoodDirector Mood { get; private set; }
        public Canvas Canvas { get; private set; }

        ReaderScreen _reader;

        readonly Dictionary<ScreenId, ScreenBase> _screens = new();
        readonly List<ScreenId> _history = new();

        ScreenId _current = ScreenId.Home;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            ConfigureRuntime();

            GitaDatabase.Load();
            AppSettings.Validate();
            AppSettings.ApplyDeviceDefault();
            Theme.LoadFonts();
            AppSettings.ApplyTextScale();
            Narration.Init();
            DailyVerse.Apply();
            Ambience.Create(transform);

            var cam = BuildCamera();
            World = CinematicWorld.Create();
            Director = CameraDirector.Create(cam);
            Grade = PostFX.Create(Director);
            Mood = MoodDirector.Create(World, Director, Grade);

            ApplyDeviceTier();
            BuildInterface();

            // Home is always the bottom of the stack. On a first run the language
            // choice is put in front of it, so the reader starts in their own language.
            // Home is always what opens. The language picker is reachable from the pill
            // at the top of it; putting it in front on a first run meant the app opened
            // on a settings page.
            GoTo(ScreenId.Home, pushHistory: false, fade: 0.8f);

            // A tap on the daily notification opens straight to that verse.
            var invited = DailyVerse.OpenedFromNotification();
            if (invited != null) OpenVerse(invited.c, invited.v);

            ApplyCommandLine();
        }


        /// <summary>
        /// Opens straight to a verse when started with "-gita-verse 11.12", and takes
        /// "-gita-screen Reader", "-gita-textsize 3" and "-gita-shot out.png" alongside it.
        ///
        /// This exists for the desktop preview build, which is how the layout gets
        /// looked at without a handset: without it every inspection means clicking
        /// through Home and the chapter list first. Android never passes command-line
        /// arguments, so it is inert in the shipped app.
        /// </summary>
        void ApplyCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-gita-verse") continue;

                var parts = args[i + 1].Split('.');
                if (parts.Length != 2) break;
                if (!int.TryParse(parts[0], out int chapter)) break;
                if (!int.TryParse(parts[1], out int verse)) break;

                OpenVerse(chapter, verse);
                break;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-gita-textsize") continue;
                if (int.TryParse(args[i + 1], out int step) &&
                    step >= 0 && step < AppSettings.TextSizes.Length)
                {
                    AppSettings.TextScale = AppSettings.TextSizes[step].scale;
                }
                break;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-gita-screen") continue;
                if (System.Enum.TryParse(args[i + 1], ignoreCase: true, out ScreenId screen))
                    GoTo(screen);
                break;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-gita-shot") continue;
                StartCoroutine(CaptureAndQuit(args[i + 1], 6f));
                break;
            }
        }

        /// <summary>
        /// Writes a screenshot and exits, for "-gita-shot out.png".
        ///
        /// Grabbing the window from outside gets the wrong pixels on a scaled display;
        /// asking the app itself gives exactly what it drew. Same reasoning as the verse
        /// argument above, and equally inert on a handset.
        /// </summary>
        System.Collections.IEnumerator CaptureAndQuit(string path, float settle)
        {
            // Long enough for the mood crossfade and the first layout pass to finish.
            yield return new WaitForSecondsRealtime(settle);

            // Read the framebuffer directly rather than through ScreenCapture: that
            // module is not in the project, and pulling it in would add to the shipped
            // app for the sake of a development convenience.
            yield return new WaitForEndOfFrame();

            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);

            yield return null;
            Application.Quit();
        }
        static void ConfigureRuntime()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        Camera BuildCamera()
        {
            var go = new GameObject("MainCamera");
            go.tag = "MainCamera";

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 900f;
            cam.fieldOfView = 45f;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            urp.renderShadows = true;

            go.AddComponent<AudioListener>();
            return cam;
        }

        /// <summary>
        /// Scale the expensive effects to the device. Depth of field and soft shadows
        /// are the first things to go on low-memory or low-core hardware.
        /// </summary>
        void ApplyDeviceTier()
        {
            int memory = SystemInfo.systemMemorySize;   // MB
            int cores = SystemInfo.processorCount;
            bool lowEnd = (memory > 0 && memory < 3072) || (cores > 0 && cores <= 4);
            if (!lowEnd) return;

            Grade.SetDepthOfField(false);
            if (World.Sun != null) World.Sun.shadows = LightShadows.Hard;
            Application.targetFrameRate = 30;
            Debug.Log($"[Gita] Low-end device ({memory}MB, {cores} cores) - reduced effects.");
        }

        void BuildInterface()
        {
            var canvasGo = new GameObject("UICanvas", typeof(RectTransform));
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.pixelPerfect = false;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.25f; // lean toward width so line lengths stay stable
            scaler.referencePixelsPerUnit = 100f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // Everything sits clear of the notch and of the navigation bar. Unity's own
            // SafeArea component was doing this from Screen.safeArea alone, which did not
            // account for the gesture strip here - the footer of the reader ended up
            // underneath the phone's back control.
            var safe = UIKit.Node("SafeArea", canvasGo.transform);
            var fitter = SafeAreaFitter.Attach(safe);
            SystemBarScrim.Create((RectTransform)canvasGo.transform, fitter,
                Theme.NightDeep.WithAlpha(0.9f));

            Add<HomeScreen>(ScreenId.Home, safe);
            Add<ChaptersScreen>(ScreenId.Chapters, safe);
            _reader = Add<ReaderScreen>(ScreenId.Reader, safe);
            Add<LanguageScreen>(ScreenId.Language, safe);
            Add<SearchScreen>(ScreenId.Search, safe);
            Add<BookmarksScreen>(ScreenId.Bookmarks, safe);
            Add<CollectionsScreen>(ScreenId.Collections, safe);
        }

        T Add<T>(ScreenId id, RectTransform parent) where T : ScreenBase
        {
            var go = new GameObject(typeof(T).Name + "Host");
            go.transform.SetParent(transform, false);
            var screen = go.AddComponent<T>();
            screen.Build(parent);
            _screens[id] = screen;
            return screen;
        }

        // ------------------------------------------------------------------
        // navigation
        // ------------------------------------------------------------------

        public void GoTo(ScreenId id, bool pushHistory = true, float fade = 0.35f)
        {
            if (_screens.TryGetValue(_current, out var leaving) && leaving.IsVisible)
            {
                if (pushHistory) _history.Add(_current);
                leaving.Hide(fade * 0.7f);
            }

            _current = id;
            if (!_screens.TryGetValue(id, out var entering)) return;

            entering.Show(fade);
            Director.CutTo(entering.CameraShot, fade * 4.5f);
        }

        public void OpenChapter(int chapter)
        {
            _reader.SetVerse(chapter, 1);
            GoTo(ScreenId.Reader);
        }

        public void OpenVerse(int chapter, int verse)
        {
            _reader.SetVerse(chapter, verse);
            GoTo(ScreenId.Reader);
        }

        public bool GoBack()
        {
            if (_history.Count == 0) return false;
            var previous = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            GoTo(previous, pushHistory: false);
            return true;
        }

        void Update()
        {
            // Android hardware / gesture back.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!GoBack() && _current == ScreenId.Home)
                {
                    Narration.Shutdown();
                    Application.Quit();
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            // Nobody wants narration continuing from a pocket.
            if (paused) Narration.Stop();
        }

        void OnApplicationQuit() => Narration.Shutdown();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
