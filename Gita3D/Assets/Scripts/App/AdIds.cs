namespace Gita.App
{
    /// <summary>
    /// The AdMob identifiers for com.theops.bhagvadgeeta (publisher pub-5452237321152820),
    /// all in one place. The app ID goes into the Android manifest at build time, through
    /// ProjectConfig.ConfigureAds; the two unit IDs are read at runtime.
    ///
    /// These serve real adverts. Never tap them on your own phone - AdMob counts that as
    /// invalid traffic. Register the phone in AdMob under Settings, Test devices, and it
    /// gets test adverts with these same IDs.
    /// </summary>
    public static class AdIds
    {
        public const string AndroidApp = "ca-app-pub-5452237321152820~7184288158";
        public const string Banner = "ca-app-pub-5452237321152820/1904875466";
        public const string Interstitial = "ca-app-pub-5452237321152820/5652548786";

        // Google's test IDs, which only ever serve test adverts:
        //   app           ca-app-pub-3940256099942544~3347511713
        //   banner        ca-app-pub-3940256099942544/9214589741
        //   interstitial  ca-app-pub-3940256099942544/1033173712

        /// <summary>Google's test publisher: anything under it serves test adverts only.</summary>
        const string TestPublisher = "ca-app-pub-3940256099942544";

        public static bool UsingTestIds =>
            AndroidApp.StartsWith(TestPublisher) ||
            Banner.StartsWith(TestPublisher) ||
            Interstitial.StartsWith(TestPublisher);
    }
}
