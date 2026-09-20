package com.gita.audio;

import android.app.Activity;
import android.content.Context;
import android.os.Bundle;
import android.speech.tts.TextToSpeech;
import android.speech.tts.UtteranceProgressListener;

import java.util.Locale;

/**
 * Narration through Android's own TextToSpeech engine.
 *
 * Chosen over shipped audio files because the corpus is 701 verses across several
 * languages: recording that is a studio project, and the files would dwarf the app.
 * TTS is offline once the voice data is installed, and it follows whichever language
 * the reader has selected.
 *
 * Every method is safe to call from Unity's thread. The engine itself is created and
 * driven on the Android UI thread, which is what TextToSpeech expects.
 */
public final class Narrator {

    private static TextToSpeech sTts;
    private static volatile boolean sReady;
    private static volatile boolean sFailed;
    private static volatile boolean sSpeaking;
    private static volatile String sCurrentLocale = "";

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

    /** Creates the engine. Returns immediately; poll {@link #isReady()}. */
    public static void init() {
        if (sTts != null || sFailed) return;

        final Context ctx = activity();
        if (ctx == null) { sFailed = true; return; }

        onUi(new Runnable() {
            @Override public void run() {
                try {
                    sTts = new TextToSpeech(ctx, new TextToSpeech.OnInitListener() {
                        @Override public void onInit(int status) {
                            if (status == TextToSpeech.SUCCESS) {
                                sReady = true;
                            } else {
                                sFailed = true;
                            }
                        }
                    });
                    sTts.setOnUtteranceProgressListener(new UtteranceProgressListener() {
                        @Override public void onStart(String id) { sSpeaking = true; }
                        @Override public void onDone(String id) { sSpeaking = false; }
                        @Override public void onError(String id) { sSpeaking = false; }
                        @Override public void onStop(String id, boolean interrupted) { sSpeaking = false; }
                    });
                } catch (Throwable t) {
                    sFailed = true;
                }
            }
        });
    }

    public static boolean isReady()  { return sReady && sTts != null; }
    public static boolean isFailed() { return sFailed; }
    public static boolean isSpeaking() { return sSpeaking; }

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

    /** Speaks text, replacing anything currently being spoken. */
    public static void speak(final String text, final String localeTag,
                             final float rate, final float pitch) {
        if (!isReady() || text == null || text.length() == 0) return;

        onUi(new Runnable() {
            @Override public void run() {
                try {
                    if (!localeTag.equals(sCurrentLocale)) {
                        int r = sTts.setLanguage(parse(localeTag));
                        if (r == TextToSpeech.LANG_MISSING_DATA ||
                            r == TextToSpeech.LANG_NOT_SUPPORTED) {
                            // Fall back to the device default rather than going silent.
                            sTts.setLanguage(Locale.getDefault());
                        }
                        sCurrentLocale = localeTag;
                    }
                    sTts.setSpeechRate(rate <= 0f ? 1f : rate);
                    sTts.setPitch(pitch <= 0f ? 1f : pitch);

                    sSpeaking = true;
                    Bundle params = new Bundle();
                    sTts.speak(text, TextToSpeech.QUEUE_FLUSH, params, "gita");
                } catch (Throwable t) {
                    sSpeaking = false;
                }
            }
        });
    }

    public static void stop() {
        sSpeaking = false;
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
