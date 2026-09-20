package com.gita.text;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Typeface;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;

import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.HashMap;

/**
 * Rasterises text using Android's own text stack, which shapes Devanagari correctly:
 * matra reordering, conjunct formation and reph positioning all come for free via
 * HarfBuzz and ICU. Unity's text engine does none of that, so Sanskrit rendered
 * through TextMeshPro comes out visibly wrong.
 *
 * The result is returned as an 8-byte header (width, height, little-endian int32)
 * followed by one alpha byte per pixel. Only coverage is sent across JNI - the tint
 * is applied on the Unity side - which keeps the payload to a quarter of RGBA.
 */
public final class TextRasterizer {

    private static final HashMap<String, Typeface> TYPEFACES = new HashMap<String, Typeface>();
    private static Context sContext;

    private TextRasterizer() { }

    /**
     * Resolves the Unity activity reflectively, so no Context has to be marshalled
     * across JNI. Passing one is a common source of signature-resolution failures.
     */
    private static Context context() {
        if (sContext != null) return sContext;
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            Object activity = player.getDeclaredField("currentActivity").get(null);
            sContext = (Context) activity;
        } catch (Throwable ignored) {
            // Leave null; the caller falls back to the default typeface.
        }
        return sContext;
    }

    private static Typeface typeface(String assetPath) {
        if (assetPath == null || assetPath.length() == 0) return Typeface.DEFAULT;

        Typeface cached = TYPEFACES.get(assetPath);
        if (cached != null) return cached;

        Typeface resolved = Typeface.DEFAULT;
        try {
            Context ctx = context();
            if (ctx != null) {
                resolved = Typeface.createFromAsset(ctx.getAssets(), assetPath);
            }
        } catch (Throwable ignored) {
            // A missing bundled font is not fatal: Android's own font fallback
            // covers Devanagari on every device that ships the script.
        }
        TYPEFACES.put(assetPath, resolved);
        return resolved;
    }

    /**
     * @param text            the string to lay out
     * @param fontAsset       path inside the APK assets folder, or "" for the system face
     * @param textSizePx      text size in real device pixels
     * @param maxWidthPx      wrapping width in real device pixels
     * @param alignCode       0 = start, 1 = centre, 2 = end
     * @param lineSpacingMult line height multiplier
     * @param maxHeightPx     hard ceiling, guards against a runaway allocation
     * @return header + alpha coverage, or null if anything went wrong
     */
    public static byte[] rasterize(String text,
                                   String fontAsset,
                                   float textSizePx,
                                   int maxWidthPx,
                                   int alignCode,
                                   float lineSpacingMult,
                                   int maxHeightPx) {
        try {
            if (text == null) text = "";
            if (maxWidthPx < 8) maxWidthPx = 8;
            if (maxWidthPx > 4096) maxWidthPx = 4096;
            if (maxHeightPx < 8) maxHeightPx = 8;
            if (maxHeightPx > 4096) maxHeightPx = 4096;
            if (textSizePx < 1f) textSizePx = 1f;
            if (lineSpacingMult < 0.5f) lineSpacingMult = 0.5f;

            TextPaint paint = new TextPaint(TextPaint.ANTI_ALIAS_FLAG);
            paint.setTypeface(typeface(fontAsset));
            paint.setTextSize(textSizePx);
            paint.setColor(Color.WHITE);
            paint.setSubpixelText(true);

            Layout.Alignment align;
            if (alignCode == 1)      align = Layout.Alignment.ALIGN_CENTER;
            else if (alignCode == 2) align = Layout.Alignment.ALIGN_OPPOSITE;
            else                     align = Layout.Alignment.ALIGN_NORMAL;

            StaticLayout layout = StaticLayout.Builder
                    .obtain(text, 0, text.length(), paint, maxWidthPx)
                    .setAlignment(align)
                    .setLineSpacing(0f, lineSpacingMult)
                    .setIncludePad(true)
                    .build();

            int width = maxWidthPx;
            int height = layout.getHeight();
            if (height < 1) height = 1;
            if (height > maxHeightPx) height = maxHeightPx;

            Bitmap bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            Canvas canvas = new Canvas(bitmap);
            layout.draw(canvas);

            int[] argb = new int[width * height];
            bitmap.getPixels(argb, 0, width, 0, 0, width, height);
            bitmap.recycle();

            byte[] out = new byte[8 + width * height];
            ByteBuffer header = ByteBuffer.wrap(out, 0, 8).order(ByteOrder.LITTLE_ENDIAN);
            header.putInt(width);
            header.putInt(height);

            for (int i = 0, n = argb.length; i < n; i++) {
                out[8 + i] = (byte) (argb[i] >>> 24);
            }
            return out;
        } catch (Throwable t) {
            return null;
        }
    }

    /** Measures without rasterising, for layout passes that only need a height. */
    public static int measureHeight(String text,
                                    String fontAsset,
                                    float textSizePx,
                                    int maxWidthPx,
                                    float lineSpacingMult) {
        try {
            if (text == null) text = "";
            if (maxWidthPx < 8) maxWidthPx = 8;

            TextPaint paint = new TextPaint(TextPaint.ANTI_ALIAS_FLAG);
            paint.setTypeface(typeface(fontAsset));
            paint.setTextSize(textSizePx);

            StaticLayout layout = StaticLayout.Builder
                    .obtain(text, 0, text.length(), paint, maxWidthPx)
                    .setLineSpacing(0f, lineSpacingMult)
                    .setIncludePad(true)
                    .build();

            return layout.getHeight();
        } catch (Throwable t) {
            return -1;
        }
    }
}
