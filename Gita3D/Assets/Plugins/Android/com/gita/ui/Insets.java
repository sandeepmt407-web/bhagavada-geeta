package com.gita.ui;

import android.app.Activity;
import android.graphics.Rect;
import android.os.Build;
import android.view.DisplayCutout;
import android.view.View;
import android.view.WindowInsets;

/**
 * The size of the system bars, in pixels.
 *
 * Unity is asked to render outside the safe area, and from Android 15 the platform
 * enforces edge-to-edge regardless, so the game surface extends underneath the
 * navigation bar. Unity's own Screen.safeArea reports the display cutout reliably but
 * not always the navigation bar, which is how the reader's controls ended up sitting
 * under the back gesture strip. This reads the real insets and lets the UI keep clear
 * of them.
 *
 * The values are cached: getRootWindowInsets must be called on the UI thread, and the
 * interface needs them synchronously while laying out. Callers poke refresh() when the
 * screen changes and read the cached numbers.
 */
public final class Insets {

    private static volatile int sLeft, sTop, sRight, sBottom;
    private static volatile boolean sValid;

    private Insets() { }

    private static Activity activity() {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            return (Activity) player.getDeclaredField("currentActivity").get(null);
        } catch (Throwable t) {
            return null;
        }
    }

    /** True once at least one successful read has happened. */
    public static boolean isValid() { return sValid; }

    public static int left()   { return sLeft; }
    public static int top()    { return sTop; }
    public static int right()  { return sRight; }
    public static int bottom() { return sBottom; }

    /**
     * Asks Android for the current insets. Returns immediately; the values land on the
     * UI thread a frame or so later. Safe to call every frame, though there is no
     * reason to - the caller polls it while the layout settles and after rotations.
     */
    public static void refresh() {
        final Activity a = activity();
        if (a == null) return;

        a.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    View decor = a.getWindow().getDecorView();
                    WindowInsets insets = decor.getRootWindowInsets();
                    if (insets == null) return;

                    int l, t, r, b;

                    if (Build.VERSION.SDK_INT >= 30) {
                        // System bars and the cutout together: the union is what the
                        // interface must stay clear of.
                        int mask = WindowInsets.Type.systemBars()
                                 | WindowInsets.Type.displayCutout();
                        android.graphics.Insets i = insets.getInsets(mask);
                        l = i.left; t = i.top; r = i.right; b = i.bottom;
                    } else {
                        l = insets.getSystemWindowInsetLeft();
                        t = insets.getSystemWindowInsetTop();
                        r = insets.getSystemWindowInsetRight();
                        b = insets.getSystemWindowInsetBottom();

                        // Below API 30 the cutout is reported separately.
                        if (Build.VERSION.SDK_INT >= 28) {
                            DisplayCutout cut = insets.getDisplayCutout();
                            if (cut != null) {
                                l = Math.max(l, cut.getSafeInsetLeft());
                                t = Math.max(t, cut.getSafeInsetTop());
                                r = Math.max(r, cut.getSafeInsetRight());
                                b = Math.max(b, cut.getSafeInsetBottom());
                            }
                        }
                    }

                    // A bar taller than a third of the screen is not a bar; ignore a
                    // reading that absurd rather than collapsing the layout.
                    Rect frame = new Rect();
                    decor.getWindowVisibleDisplayFrame(frame);
                    int limit = Math.max(frame.height(), frame.width()) / 3;
                    if (l > limit || t > limit || r > limit || b > limit) return;

                    sLeft = l; sTop = t; sRight = r; sBottom = b;
                    sValid = true;
                } catch (Throwable ignored) {
                    // Leave the previous values in place; the UI falls back to
                    // Screen.safeArea on its own.
                }
            }
        });
    }
}
