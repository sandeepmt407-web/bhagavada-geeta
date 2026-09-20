package com.gita.share;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.provider.MediaStore;

import java.io.OutputStream;

/**
 * Saves a rendered verse image to the picture library and opens the system share
 * sheet for it.
 *
 * Deliberately uses MediaStore rather than a FileProvider: a provider has to be
 * declared in the manifest, and hand-editing Unity's generated manifest is a good way
 * to break an Android build. MediaStore needs no manifest entry and no permission from
 * API 29 up. Below that it is not worth the permission prompt, so sharing falls back
 * to text, which every Android version handles.
 */
public final class Sharer {

    private Sharer() { }

    private static Activity activity() {
        try {
            Class<?> player = Class.forName("com.unity3d.player.UnityPlayer");
            return (Activity) player.getDeclaredField("currentActivity").get(null);
        } catch (Throwable t) {
            return null;
        }
    }

    /** True when an image can be saved and shared on this device. */
    public static boolean canShareImage() {
        return Build.VERSION.SDK_INT >= 29 && activity() != null;
    }

    /**
     * Writes the PNG into Pictures/Bhagavad Gita and opens the share sheet.
     *
     * @return true if the sheet was opened; false means the caller should fall back
     *         to {@link #shareText}.
     */
    public static boolean shareImage(final byte[] png, final String fileName, final String text) {
        final Activity activity = activity();
        if (activity == null || png == null || png.length == 0) return false;
        if (Build.VERSION.SDK_INT < 29) return false;

        try {
            ContentResolver resolver = activity.getContentResolver();

            ContentValues values = new ContentValues();
            values.put(MediaStore.MediaColumns.DISPLAY_NAME, fileName);
            values.put(MediaStore.MediaColumns.MIME_TYPE, "image/png");
            values.put(MediaStore.MediaColumns.RELATIVE_PATH,
                    Environment.DIRECTORY_PICTURES + "/Bhagavad Gita");
            // Hidden from the gallery until the bytes are actually written.
            values.put(MediaStore.MediaColumns.IS_PENDING, 1);

            final Uri uri = resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, values);
            if (uri == null) return false;

            OutputStream out = resolver.openOutputStream(uri);
            if (out == null) return false;
            try {
                out.write(png);
                out.flush();
            } finally {
                out.close();
            }

            ContentValues done = new ContentValues();
            done.put(MediaStore.MediaColumns.IS_PENDING, 0);
            resolver.update(uri, done, null, null);

            activity.runOnUiThread(new Runnable() {
                @Override public void run() {
                    try {
                        Intent send = new Intent(Intent.ACTION_SEND);
                        send.setType("image/png");
                        send.putExtra(Intent.EXTRA_STREAM, uri);
                        if (text != null && text.length() > 0) {
                            send.putExtra(Intent.EXTRA_TEXT, text);
                        }
                        send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
                        activity.startActivity(Intent.createChooser(send, "Share this verse"));
                    } catch (Throwable ignored) { }
                }
            });
            return true;
        } catch (Throwable t) {
            return false;
        }
    }

    /** Plain text share. Works on every version, and is the fallback for images. */
    public static boolean shareText(final String text) {
        final Activity activity = activity();
        if (activity == null || text == null || text.length() == 0) return false;

        activity.runOnUiThread(new Runnable() {
            @Override public void run() {
                try {
                    Intent send = new Intent(Intent.ACTION_SEND);
                    send.setType("text/plain");
                    send.putExtra(Intent.EXTRA_TEXT, text);
                    activity.startActivity(Intent.createChooser(send, "Share this verse"));
                } catch (Throwable ignored) { }
            }
        });
        return true;
    }
}
