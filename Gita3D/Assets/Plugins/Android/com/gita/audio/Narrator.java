package com.gita.audio;

import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.media.AudioAttributes;
import android.os.Build;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;
import android.speech.tts.Voice;

import java.util.Locale;
import java.util.Set;

/**
 * Narration through Android's own TextToSpeech engine.
 *
 * Chosen over shipped audio files because the corpus is 701 verses across several
 * languages: recording that is a studio project, and the files would dwarf the app.
 * TTS is offline once the voice data is installed, and it follows whichever language
 * the reader has selected.
 *
 * Two things here are about how human it sounds, which was the first thing anyone said
 * about it. The engine is chosen rather than taken as given - Google's, where it is
 * installed, because its voices are the best ones generally available on Android - and
 * then the best individual voice for the language is picked out of everything that
 * engine offers, rather than accepting the default, which on most devices is the oldest
 * and most synthetic of the set. It is still a speech synthesiser and will still sound
 * like one; it should no longer sound like the worst one on the phone.
 *
 * Every method is safe to call from Unity's thread. The engine itself is created and
 * driven on the Android UI thread, which is what TextToSpeech expects.
 */
public final class Narrator {

    private static final String GOOGLE_TTS = "com.google.android.tts";

    private static TextToSpeech sTts;
    private static volatile boolean sReady;
    private static volatile boolean sFailed;
    private static volatile boolean sSpeaking;
    private static volatile String sCurrentLocale = "";
    private static volatile String sVoiceName = "";
    private static volatile String sRequestedVoice = "";

    // Enough of the last utterance to say it again if the first attempt fails.
    private static volatile boolean sVoiceNeedsNetwork;
    private static volatile boolean sRetrying;
    private static volatile String sLastText = "";
    private static volatile String sNextText = "";
    private static volatile String sNextLocale = "";
    private static volatile String sNextVoice = "";
    private static volatile float sLastRate = 1f;
    private static volatile float sLastPitch = 1f;

    private Narrator() { }

    private static Activity activity() {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            return (Activity) player.getDeclaredField("currentActivity").get(null);
        } catch (Throwable t) {
            return null;
        }
    }

    private static void onUi(Runnable r) {
        Activity a = activity();
        if (a != null) a.runOnUiThread(r);
        else r.run();
    }

    /** Whether a given speech engine is installed on this device. */
    private static boolean hasEngine(Context ctx, String pkg) {
        try {
            ctx.getPackageManager().getPackageInfo(pkg, 0);
            return true;
        } catch (PackageManager.NameNotFoundException e) {
            return false;
        } catch (Throwable t) {
            return false;
        }
    }

    /** Creates the engine. Returns immediately; poll {@link #isReady()}. */
    public static void init() {
        if (sTts != null || sFailed) return;

        final Context ctx = activity();
        if (ctx == null) { sFailed = true; return; }

        onUi(new Runnable() {
            @Override public void run() {
                try {
                    TextToSpeech.OnInitListener listener = new TextToSpeech.OnInitListener() {
                        @Override public void onInit(int status) {
                            if (status == TextToSpeech.SUCCESS) {
                                applyAudioAttributes();
                                sReady = true;
                            } else {
                                sFailed = true;
                            }
                        }
                    };

                    // Google's engine where it exists, the system default otherwise.
                    // Passing a package that is not installed fails initialisation
                    // outright, so this is checked rather than attempted.
                    if (hasEngine(ctx, GOOGLE_TTS)) {
                        sTts = new TextToSpeech(ctx, listener, GOOGLE_TTS);
                    } else {
                        sTts = new TextToSpeech(ctx, listener);
                    }

                    sTts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                        @Override public void onStart(String id) { sSpeaking = true; }
                        @Override public void onDone(String id) {
                            sRetrying = false;
                            // A shloka is followed by its translation, in another
                            // language; if one is waiting, start it before reporting
                            // that speech has stopped.
                            if (!speakQueued()) sSpeaking = false;
                        }
                        @Override public void onStop(String id, boolean interrupted) {
                            sSpeaking = false;
                            sRetrying = false;
                        }

                        /**
                         * The default voice is one of Google's network voices, which is
                         * the best thing most phones have and is also useless on a train.
                         * Rather than ask for a connectivity permission to guess in
                         * advance, this waits for the engine to actually fail and then
                         * says the same line again with the best installed voice. The
                         * reader hears a pause, not silence.
                         */
                        @Override public void onError(String id) {
                            sSpeaking = false;
                            if (sRetrying || !sVoiceNeedsNetwork) { sRetrying = false; sNextText = ""; return; }
                            if (sLastText == null || sLastText.length() == 0) return;

                            sRetrying = true;
                            onUi(new Runnable() {
                                @Override public void run() {
                                    try {
                                        selectBestOffline(parse(sCurrentLocale));
                                        sSpeaking = true;
                                        sTts.speak(sLastText, TextToSpeech.QUEUE_FLUSH,
                                                new Bundle(), "gita-retry");
                                    } catch (Throwable t) {
                                        sSpeaking = false;
                                        sRetrying = false;
                                    }
                                }
                            });
                        }
                    });
                } catch (Throwable t) {
                    sFailed = true;
                }
            }
        });
    }

    /**
     * Routes narration as media rather than as a notification or an alarm, so it
     * follows the media volume the reader already set and ducks the way anything else
     * playing music would.
     */
    private static void applyAudioAttributes() {
        if (sTts == null || Build.VERSION.SDK_INT < 21) return;
        try {
            AudioAttributes attributes = new AudioAttributes.Builder()
                    .setUsage(AudioAttributes.USAGE_MEDIA)
                    .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
                    .build();
            sTts.setAudioAttributes(attributes);
        } catch (Throwable ignored) { }
    }

    public static boolean isReady()  { return sReady && sTts != null; }
    public static boolean isFailed() { return sFailed; }
    public static boolean isSpeaking() { return sSpeaking; }

    /** The voice actually in use, for the diagnostic line on the settings screen. */
    public static String voiceName() { return sVoiceName; }

    /** Parses tags like "en-IN" or "hi-IN" into a Locale. */
    private static Locale parse(String tag) {
        if (tag == null || tag.length() == 0) return Locale.getDefault();
        String[] parts = tag.replace('_', '-').split("-");
        if (parts.length >= 2) return new Locale(parts[0], parts[1]);
        return new Locale(parts[0]);
    }

    /**
     * Whether a locale has usable voice data installed.
     * 0 = not supported, 1 = supported but data must be downloaded, 2 = ready.
     */
    public static int localeStatus(String tag) {
        if (!isReady()) return 0;
        try {
            int r = sTts.isLanguageAvailable(parse(tag));
            if (r == TextToSpeech.LANG_MISSING_DATA) return 1;
            if (r == TextToSpeech.LANG_NOT_SUPPORTED) return 0;
            return 2;
        } catch (Throwable t) {
            return 0;
        }
    }

    /**
     * Finds the best voice the engine has for a language and selects it.
     *
     * Offline voices are preferred over network ones even when the network one scores
     * higher: an app for scripture should work on a train, and a voice that fails
     * silently when the signal drops is worse than one that is merely less smooth.
     * A network voice is taken only when the language has no installed voice at all.
     */
    private static void selectBestVoice(Locale wanted) {
        sVoiceName = "";
        if (Build.VERSION.SDK_INT < 21 || sTts == null) return;

        try {
            Set<Voice> voices = sTts.getVoices();
            if (voices == null || voices.isEmpty()) return;

            Voice bestOffline = null, bestNetwork = null;
            int offlineScore = Integer.MIN_VALUE, networkScore = Integer.MIN_VALUE;

            for (Voice voice : voices) {
                if (voice == null || voice.getLocale() == null) continue;

                Set<String> features = voice.getFeatures();
                if (features != null
                        && features.contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED)) {
                    continue;
                }

                String lang = voice.getLocale().getLanguage();
                if (lang == null || !lang.equalsIgnoreCase(wanted.getLanguage())) continue;

                // Quality first, then a bonus for matching the country too - en-IN
                // should read the Gita rather than en-US, where both are present.
                int score = voice.getQuality();
                String country = voice.getLocale().getCountry();
                if (country != null && country.equalsIgnoreCase(wanted.getCountry())) {
                    score += 250;
                }
                // Lower latency is generally the locally installed, newer voice.
                score -= voice.getLatency() / 100;

                if (voice.isNetworkConnectionRequired()) {
                    if (score > networkScore) { networkScore = score; bestNetwork = voice; }
                } else {
                    if (score > offlineScore) { offlineScore = score; bestOffline = voice; }
                }
            }

            Voice chosen = bestOffline != null ? bestOffline : bestNetwork;
            if (chosen == null) return;

            if (sTts.setVoice(chosen) == TextToSpeech.SUCCESS) {
                sVoiceName = chosen.getName() == null ? "" : chosen.getName();
                sVoiceNeedsNetwork = chosen.isNetworkConnectionRequired();
            }
        } catch (Throwable ignored) {
            // A device that will not enumerate its voices keeps whatever the engine
            // selected for the language; narration still works.
        }
    }


    /**
     * Every voice the engine has for a language, one per line, as
     * {@code name|locale|quality|networkRequired}.
     *
     * There is no published list of what a given phone carries - it depends on the
     * engine, the Android version and which voice data the owner has installed - so
     * the only truthful way to offer a choice is to ask the device and show what comes
     * back.
     *
     * @param langTag    a tag like "en-IN"; only the language part is matched
     * @param indiaOnly  keep only voices whose locale country is India
     */
    public static String listVoices(String langTag, boolean indiaOnly) {
        if (!isReady() || Build.VERSION.SDK_INT < 21) return "";

        try {
            Set<Voice> voices = sTts.getVoices();
            if (voices == null || voices.isEmpty()) return "";

            Locale wanted = parse(langTag);
            StringBuilder sb = new StringBuilder();

            for (Voice voice : voices) {
                if (voice == null || voice.getLocale() == null) continue;

                Set<String> features = voice.getFeatures();
                if (features != null
                        && features.contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED)) {
                    continue;
                }

                String lang = voice.getLocale().getLanguage();
                if (lang == null || !lang.equalsIgnoreCase(wanted.getLanguage())) continue;

                String country = voice.getLocale().getCountry();
                if (indiaOnly && !"IN".equalsIgnoreCase(country)) continue;

                if (sb.length() > 0) sb.append('\n');
                sb.append(voice.getName()).append('|')
                  .append(voice.getLocale().toString()).append('|')
                  .append(voice.getQuality()).append('|')
                  .append(voice.isNetworkConnectionRequired() ? '1' : '0');
            }
            return sb.toString();
        } catch (Throwable t) {
            return "";
        }
    }

    /** Selects a voice by its exact name. Empty clears the choice and restores auto. */

    /**
     * Picks the best voice that does not need a connection.
     *
     * Used only when a network voice has already failed, so unlike selectBestVoice this
     * will not settle for another network one - that would fail the same way.
     */
    private static void selectBestOffline(Locale wanted) {
        if (Build.VERSION.SDK_INT < 21 || sTts == null) return;
        try {
            Set<Voice> voices = sTts.getVoices();
            if (voices == null || voices.isEmpty()) return;

            Voice best = null;
            int bestScore = Integer.MIN_VALUE;

            for (Voice voice : voices) {
                if (voice == null || voice.getLocale() == null) continue;
                if (voice.isNetworkConnectionRequired()) continue;

                Set<String> features = voice.getFeatures();
                if (features != null
                        && features.contains(TextToSpeech.Engine.KEY_FEATURE_NOT_INSTALLED)) {
                    continue;
                }

                String lang = voice.getLocale().getLanguage();
                if (lang == null || !lang.equalsIgnoreCase(wanted.getLanguage())) continue;

                int score = voice.getQuality();
                String country = voice.getLocale().getCountry();
                if (country != null && country.equalsIgnoreCase(wanted.getCountry())) score += 250;

                if (score > bestScore) { bestScore = score; best = voice; }
            }

            if (best != null && sTts.setVoice(best) == TextToSpeech.SUCCESS) {
                sVoiceName = best.getName() == null ? "" : best.getName();
                sVoiceNeedsNetwork = false;
            }
        } catch (Throwable ignored) { }
    }
    private static boolean applyVoice(String name) {
        if (name == null || name.length() == 0 || Build.VERSION.SDK_INT < 21) return false;
        try {
            Set<Voice> voices = sTts.getVoices();
            if (voices == null) return false;
            for (Voice voice : voices) {
                if (voice != null && name.equals(voice.getName())) {
                    if (sTts.setVoice(voice) == TextToSpeech.SUCCESS) {
                        sVoiceName = name;
                        sVoiceNeedsNetwork = voice.isNetworkConnectionRequired();
                        return true;
                    }
                    return false;
                }
            }
        } catch (Throwable ignored) { }
        return false;
    }

    /**
     * Selects a language and voice, if they are not already the ones in force.
     *
     * Pulled out of speak() because a shloka followed by its translation needs this
     * done twice in the one utterance sequence, with a different language each time.
     */
    private static void applyLanguage(String localeTag, String voiceName) {
        String want = voiceName == null ? "" : voiceName;
        if (localeTag.equals(sCurrentLocale) && want.equals(sRequestedVoice)) return;

        Locale wanted = parse(localeTag);
        int r = sTts.setLanguage(wanted);
        if (r == TextToSpeech.LANG_MISSING_DATA || r == TextToSpeech.LANG_NOT_SUPPORTED) {
            // Fall back to the device default rather than going silent.
            wanted = Locale.getDefault();
            sTts.setLanguage(wanted);
        }
        // The reader's own choice wins. If that voice has since been uninstalled,
        // pick the best available rather than going quiet.
        if (!applyVoice(want)) selectBestVoice(wanted);
        sCurrentLocale = localeTag;
        sRequestedVoice = want;
    }

    /** Speaks text, replacing anything currently being spoken. */
    public static void speak(final String text, final String localeTag,
                             final float rate, final float pitch,
                             final String voiceName) {
        speakPair(text, localeTag, voiceName, "", "", "", rate, pitch);
    }

    /**
     * Speaks one passage and then, when it has finished, another in a different
     * language.
     *
     * This exists because the shloka and its translation are not in the same tongue, and
     * TextToSpeech applies a language change immediately rather than per queued
     * utterance - so queueing both and switching language between them would read the
     * second in whatever voice the first left behind. Waiting for the first to finish is
     * the only way to give each its own voice. Pass an empty second text for a single
     * passage.
     */
    public static void speakPair(final String firstText, final String firstLocale,
                                 final String firstVoice,
                                 final String secondText, final String secondLocale,
                                 final String secondVoice,
                                 final float rate, final float pitch) {
        if (!isReady() || firstText == null || firstText.length() == 0) return;

        onUi(new Runnable() {
            @Override public void run() {
                try {
                    applyLanguage(firstLocale, firstVoice);

                    sTts.setSpeechRate(rate <= 0f ? 1f : rate);
                    sTts.setPitch(pitch <= 0f ? 1f : pitch);

                    // Kept so a failed network voice can be retried offline.
                    sLastText = firstText;
                    sLastRate = rate;
                    sLastPitch = pitch;
                    sRetrying = false;

                    sNextText = secondText == null ? "" : secondText;
                    sNextLocale = secondLocale == null ? "" : secondLocale;
                    sNextVoice = secondVoice == null ? "" : secondVoice;

                    sSpeaking = true;
                    sTts.speak(firstText, TextToSpeech.QUEUE_FLUSH, new Bundle(), "gita");
                } catch (Throwable t) {
                    sSpeaking = false;
                    sNextText = "";
                }
            }
        });
    }

    /** Starts the queued second passage, if there is one. Returns true if it did. */
    private static boolean speakQueued() {
        final String text = sNextText;
        if (text == null || text.length() == 0) return false;

        final String locale = sNextLocale;
        final String voice = sNextVoice;
        sNextText = "";

        // Held true across the gap so the interface does not flicker back to "play"
        // between the shloka and its translation.
        sSpeaking = true;

        onUi(new Runnable() {
            @Override public void run() {
                try {
                    applyLanguage(locale, voice);
                    sLastText = text;
                    sRetrying = false;
                    sTts.speak(text, TextToSpeech.QUEUE_FLUSH, new Bundle(), "gita");
                } catch (Throwable t) {
                    sSpeaking = false;
                }
            }
        });
        return true;
    }

    public static void stop() {
        sSpeaking = false;
        sNextText = "";   // drop any translation queued behind a shloka
        if (sTts == null) return;
        onUi(new Runnable() {
            @Override public void run() {
                try { sTts.stop(); } catch (Throwable ignored) { }
            }
        });
    }

    public static void shutdown() {
        sSpeaking = false;
        sReady = false;
        sCurrentLocale = "";
        sNextText = "";
        sRequestedVoice = "";
        final TextToSpeech engine = sTts;
        sTts = null;
        if (engine == null) return;
        onUi(new Runnable() {
            @Override public void run() {
                try { engine.stop(); engine.shutdown(); } catch (Throwable ignored) { }
            }
        });
    }
}
