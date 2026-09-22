using System;
using System.Collections.Generic;
using UnityEngine;
using Gita.UI;
#if UNITY_ANDROID
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace Gita.App
{
    /// <summary>Where a screen carries its banner, if it carries one at all.</summary>
    public enum BannerSlot { None, Top, Bottom }

    /// <summary>
    /// Advertising, through AdMob: one banner that moves between the screens that carry
    /// it, and an interstitial after every few verses read.
    ///
    /// The banner sits at the top of the reader and at the bottom of the lists. The
    /// reader's footer holds Next, which is tapped on every verse, and a banner directly
    /// under the most-pressed control in the app is the textbook accidental-click
    /// placement - AdMob counts those clicks as invalid. The lists have nothing tappable
    /// at their foot, so the bottom is free there.
    ///
    /// The interface is held clear of the banner rather than drawn underneath it. The
    /// banner is a native Android view floating over Unity, so its height goes to the
    /// safe-area fitter as one more inset - but only once an advert has actually arrived.
    /// Offline, or with nothing to show, every screen is laid out exactly as before.
    ///
    /// Only Android has an implementation. Everywhere else, the Windows preview build
    /// included, every call does nothing and no screen is ever inset.
    /// </summary>
    public sealed class Ads : MonoBehaviour
    {
        /// <summary>Verses read forward between one interstitial and the next.</summary>
        public const int VersesPerInterstitial = 5;

        /// <summary>
        /// The least time between two interstitials, however quickly the pages turn.
        /// Five verses take minutes to read, so a reader never meets this; it is there for
        /// someone flicking through, who would otherwise get an advert every few seconds.
        /// Zero counts verses alone.
        /// </summary>
        const float MinSecondsBetweenInterstitials = 60f;

        /// <summary>How long to leave it after a request fails before asking again.</summary>
        const float RetrySeconds = 30f;

        public static Ads Instance { get; private set; }

        SafeAreaFitter _fitter;
        BannerSlot _slot = BannerSlot.None;

        int _versesSinceAd;
        float _lastInterstitialAt = -MinSecondsBetweenInterstitials;

        // The ads SDK answers on Android's threads. Anything that touches Unity is queued
        // here and run from Update, on the main thread.
        readonly Queue<Action> _pending = new();

#if UNITY_ANDROID
        bool _started, _ready;
        float _density = 1f;

        BannerView _banner;
        bool _bannerShown, _bannerLoaded, _bannerLoading;
        float _bannerRetryAt;
        float _bannerWidth, _bannerHeight;   // device pixels
        Vector4 _placedFor;
        Vector2Int _placedScreen;

        InterstitialAd _interstitial;
        bool _interstitialLoading;
        float _interstitialRetryAt;
        Action _dismissed;
#endif

        public static Ads Create(Transform parent, SafeAreaFitter fitter)
        {
            var go = new GameObject("~Ads");
            go.transform.SetParent(parent, false);
            var ads = go.AddComponent<Ads>();
            ads._fitter = fitter;
            Instance = ads;
            ads.GatherConsent();
            return ads;
        }

        // ------------------------------------------------------------------
        // consent and start-up
        // ------------------------------------------------------------------

        /// <summary>
        /// Asks Google's consent platform whether this reader has to be asked first, and
        /// asks them if so. Outside the EEA, the UK and Switzerland the answer is no and
        /// nothing appears. No advert is requested until this allows it.
        /// </summary>
        void GatherConsent()
        {
#if UNITY_ANDROID
            ConsentInformation.Update(new ConsentRequestParameters(), error => Post(() =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[Gita] Consent status could not be updated: {error.Message}");
                    Begin();
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Post(() =>
                {
                    if (formError != null)
                        Debug.LogWarning($"[Gita] Consent form failed: {formError.Message}");
                    Begin();
                }));
            }));

            // A choice made in an earlier session is enough to start on, without waiting
            // for the update above to come back.
            Begin();
#endif
        }

#if UNITY_ANDROID
        void Begin()
        {
            if (_started || !ConsentInformation.CanRequestAds()) return;
            _started = true;

            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            // General-audience adverts only. These sit beside scripture.
            MobileAds.SetRequestConfiguration(new RequestConfiguration
            {
                MaxAdContentRating = MaxAdContentRating.G,
            });

            MobileAds.Initialize(_ => Post(() =>
            {
                _ready = true;
                _density = Mathf.Max(0.5f, MobileAds.Utils.GetDeviceScale());
                LoadInterstitial();

                // The reader may already be on a screen that carries a banner - the
                // daily notification opens straight to a verse.
                Place(_slot);
            }));
        }
#endif

        /// <summary>
        /// Where the law gives the reader a standing choice about advertising data - the
        /// EEA, the UK and Switzerland - settings has to offer a way back to it.
        /// </summary>
        public static bool PrivacyOptionsRequired
        {
            get
            {
#if UNITY_ANDROID
                return ConsentInformation.PrivacyOptionsRequirementStatus ==
                       PrivacyOptionsRequirementStatus.Required;
#else
                return false;
#endif
            }
        }

        /// <summary>Shows Google's privacy options form, then runs <paramref name="done"/>.</summary>
        public void ShowPrivacyOptions(Action done)
        {
#if UNITY_ANDROID
            ConsentForm.ShowPrivacyOptionsForm(error => Post(() =>
            {
                if (error != null)
                    Debug.LogWarning($"[Gita] Privacy options form failed: {error.Message}");
                Begin();   // consent may only just have been given
                done?.Invoke();
            }));
#endif
        }

        // ------------------------------------------------------------------
        // banner
        // ------------------------------------------------------------------

        /// <summary>
        /// Moves the banner to where the screen now showing wants it, or hides it. Called
        /// on every change of screen.
        /// </summary>
        public void Place(BannerSlot slot)
        {
            _slot = slot;
#if UNITY_ANDROID
            if (!_ready) return;   // applied once the SDK has started

            if (slot == BannerSlot.None)
            {
                if (_banner != null && _bannerShown)
                {
                    _banner.Hide();
                    _bannerShown = false;
                }
                _fitter.SetBannerClearance(0f, 0f);
                return;
            }

            if (_banner == null)
            {
                CreateBanner();
                return;
            }

            // Moved before it is shown, so it never appears for a frame where it last was.
            PositionBanner();
            if (!_bannerShown)
            {
                _banner.Show();
                _bannerShown = true;
            }
#endif
        }

#if UNITY_ANDROID
        /// <summary>
        /// Created the first time a screen asks for one, not at start-up. Starting it
        /// hidden behind Home would mean loading into a paused view - Hide() pauses it -
        /// which is not something to depend on.
        /// </summary>
        void CreateBanner()
        {
            // Anchored adaptive: the full width of the screen, at the height Google
            // chooses for that width.
            var size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                MobileAds.Utils.GetDeviceSafeWidth());

            // Placed by coordinates, not AdPosition.Top or Bottom: those keep clear of the
            // camera cutout only, and this app draws under the status and navigation bars
            // as well, so the banner would land beneath them. The view stays invisible
            // until an advert arrives, so where it starts does not matter.
            _banner = new BannerView(AdIds.Banner, size, 0, 0);
            _bannerShown = true;

            _banner.OnBannerAdLoaded += () => Post(OnBannerLoaded);
            _banner.OnBannerAdLoadFailed += error => Post(() => OnBannerFailed(error));

            MeasureBanner();
            PositionBanner();
            LoadBanner();
        }

        void MeasureBanner()
        {
            _bannerWidth = _banner.GetWidthInPixels();
            _bannerHeight = _banner.GetHeightInPixels();

            // Adaptive banners on a phone are never shorter than 50dp; if the measurement
            // failed, that keeps the bottom one from settling over the navigation bar.
            if (_bannerHeight <= 0f) _bannerHeight = 50f * _density;
            if (_bannerWidth <= 0f) _bannerWidth = Screen.width;
        }

        /// <summary>
        /// Called for the first advert, and again only if that one never arrived. After
        /// that the SDK refreshes the banner itself, at the rate set on the ad unit in
        /// AdMob (30 seconds), and only while it is on screen - Hide() pauses it. Loading
        /// from here as well would refresh it twice.
        /// </summary>
        void LoadBanner()
        {
            _bannerLoading = true;
            _banner.LoadAd(new AdRequest());
        }

        void OnBannerLoaded()
        {
            _bannerLoading = false;
            _bannerLoaded = true;

            // Measured again now the view certainly exists, then placed with the result.
            MeasureBanner();
            PositionBanner();
        }

        void OnBannerFailed(LoadAdError error)
        {
            _bannerLoading = false;
            Debug.LogWarning($"[Gita] Banner failed to load: {error}");

            // A failed refresh leaves the previous advert in place, so only a banner that
            // has never had one needs asking again.
            if (!_bannerLoaded) _bannerRetryAt = Time.realtimeSinceStartup + RetrySeconds;
        }

        /// <summary>
        /// Puts the banner against the system bar at its end of the screen, and holds the
        /// interface clear of it once there is an advert in it.
        /// </summary>
        void PositionBanner()
        {
            if (_banner == null || _slot == BannerSlot.None) return;

            var bars = _fitter.SystemPixels;   // left, top, right, bottom
            int x = Mathf.Max(0, Mathf.RoundToInt((Screen.width - _bannerWidth) * 0.5f / _density));
            int y = _slot == BannerSlot.Top
                ? Mathf.CeilToInt(bars.y / _density)
                : Mathf.FloorToInt((Screen.height - bars.w - _bannerHeight) / _density);
            _banner.SetPosition(x, y);

            _placedFor = bars;
            _placedScreen = new Vector2Int(Screen.width, Screen.height);

            if (!_bannerLoaded)
            {
                _fitter.SetBannerClearance(0f, 0f);
                return;
            }

            // The plugin truncates when it turns dp into pixels, so measure from where the
            // view actually lands.
            float top = (int)(y * _density);
            if (_slot == BannerSlot.Top) _fitter.SetBannerClearance(top + _bannerHeight, 0f);
            else _fitter.SetBannerClearance(0f, Screen.height - top);
        }
#endif

        // ------------------------------------------------------------------
        // interstitial
        // ------------------------------------------------------------------

        /// <summary>
        /// Counts a verse read forward, and after every fifth shows an interstitial.
        /// Returns true if one is being shown; <paramref name="dismissed"/> then runs once
        /// it has gone, whether it was watched, closed or never appeared.
        /// </summary>
        public bool VerseCompleted(Action dismissed)
        {
            _versesSinceAd++;
#if UNITY_ANDROID
            if (_versesSinceAd < VersesPerInterstitial) return false;
            if (Time.realtimeSinceStartup - _lastInterstitialAt < MinSecondsBetweenInterstitials)
                return false;

            // Not ready yet: keep counting, and show it on the first verse after it is.
            if (_interstitial == null || !_interstitial.CanShowAd())
            {
                LoadInterstitial();
                return false;
            }

            _versesSinceAd = 0;
            _lastInterstitialAt = Time.realtimeSinceStartup;
            _dismissed = dismissed;

            // Nothing of the app's own may sound under the advert. The verse stops, and
            // the bansuri is paused here rather than left to Android, which does not
            // always pause Unity for an advert drawn over it.
            Narration.Stop();
            AudioSession.End();
            AudioListener.pause = true;

            _interstitial.Show();
            return true;
#else
            return false;
#endif
        }

#if UNITY_ANDROID
        void LoadInterstitial()
        {
            if (!_ready || _interstitial != null || _interstitialLoading) return;
            if (Time.realtimeSinceStartup < _interstitialRetryAt) return;

            _interstitialLoading = true;
            InterstitialAd.Load(AdIds.Interstitial, new AdRequest(), (ad, error) => Post(() =>
            {
                _interstitialLoading = false;
                if (error != null || ad == null)
                {
                    Debug.LogWarning($"[Gita] Interstitial failed to load: {error}");
                    _interstitialRetryAt = Time.realtimeSinceStartup + RetrySeconds;
                    return;
                }

                _interstitial = ad;
                ad.OnAdFullScreenContentClosed += () => Post(FinishInterstitial);
                ad.OnAdFullScreenContentFailed += showError => Post(() =>
                {
                    Debug.LogWarning($"[Gita] Interstitial failed to show: {showError}");
                    FinishInterstitial();
                });
            }));
        }

        void FinishInterstitial()
        {
            AudioListener.pause = false;

            _interstitial?.Destroy();
            _interstitial = null;

            var dismissed = _dismissed;
            _dismissed = null;
            dismissed?.Invoke();

            LoadInterstitial();   // ready for the next five
        }
#endif

        // ------------------------------------------------------------------

        void Post(Action action)
        {
            lock (_pending) _pending.Enqueue(action);
        }

        void Update()
        {
            while (true)
            {
                Action next;
                lock (_pending)
                {
                    if (_pending.Count == 0) break;
                    next = _pending.Dequeue();
                }
                try { next(); }
                catch (Exception e) { Debug.LogException(e); }
            }

#if UNITY_ANDROID
            if (_banner == null || _slot == BannerSlot.None) return;

            // The insets settle over the first second or two, and change with the
            // navigation mode; the banner follows them the way the interface does.
            if (_fitter.SystemPixels != _placedFor ||
                Screen.width != _placedScreen.x || Screen.height != _placedScreen.y)
                PositionBanner();

            if (!_bannerLoaded && !_bannerLoading && Time.realtimeSinceStartup >= _bannerRetryAt)
                LoadBanner();
#endif
        }

        void OnDestroy()
        {
#if UNITY_ANDROID
            _banner?.Destroy();
            _interstitial?.Destroy();
#endif
            if (Instance == this) Instance = null;
        }
    }
}
