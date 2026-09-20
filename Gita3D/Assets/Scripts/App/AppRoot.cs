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
    public enum ScreenId { Home, Chapters, Reader, Book, Language, Search, Bookmarks, Collections }

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
        public Canvas Canvas { get; private set; }

        ReaderScreen _reader;
        BookScreen _book;

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
            Narration.Init();
            DailyVerse.Apply();

            var cam = BuildCamera();
            World = CinematicWorld.Create();
            Director = CameraDirector.Create(cam);
            Grade = PostFX.Create(Director);

            ApplyDeviceTier();
            BuildInterface();

            // Home is always the bottom of the stack. On a first run the language
            // choice is put in front of it, so the reader starts in their own language.
            GoTo(ScreenId.Home, pushHistory: false, fade: 0.8f);

            // A tap on the daily notification opens straight to that verse.
            var invited = DailyVerse.OpenedFromNotification();
            if (invited != null) OpenVerse(invited.c, invited.v);
            else if (!AppSettings.HasChosenLanguage)
                GoTo(ScreenId.Language, pushHistory: true, fade: 0.6f);
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

            // Everything sits inside the safe area, clear of notches and the gesture bar.
            var safe = UIKit.Node("SafeArea", canvasGo.transform);
            var safeArea = safe.gameObject.AddComponent<UnityEngine.UI.SafeArea>();
            safeArea.Edges = UnityEngine.UI.SafeArea.SafeAreaMode.Top
                           | UnityEngine.UI.SafeArea.SafeAreaMode.Bottom
                           | UnityEngine.UI.SafeArea.SafeAreaMode.Left
                           | UnityEngine.UI.SafeArea.SafeAreaMode.Right;

            Add<HomeScreen>(ScreenId.Home, safe);
            Add<ChaptersScreen>(ScreenId.Chapters, safe);
            _reader = Add<ReaderScreen>(ScreenId.Reader, safe);
            _book = Add<BookScreen>(ScreenId.Book, safe);
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

        public void OpenBook(int chapter)
        {
            _book.Open(chapter);
            GoTo(ScreenId.Book);
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
