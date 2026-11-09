package com.alsphalt.apkgame;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

/**
 * Minimal, dependency-free replacement for FileProvider: exposes exactly one file - the
 * APK staged for an update - to whatever app handles the package-install intent.
 *
 * Not exported, so no other app can read it without a per-intent read grant.
 */
public class ApkFileProvider extends ContentProvider {

    public static final String AUTHORITY = "com.alsphalt.apkgame.updates";

    static File updatesDir(Context context) {
        File dir = new File(context.getExternalFilesDir(null), "updates");
        if (!dir.exists() && !dir.mkdirs()) {
            // Fall back to internal storage; the installer can still read the stream.
            dir = new File(context.getFilesDir(), "updates");
            //noinspection ResultOfMethodCallIgnored
            dir.mkdirs();
        }
        return dir;
    }

    static File apkFile(Context context) {
        return new File(updatesDir(context), "darknote-update.apk");
    }

    static Uri uriFor(File file) {
        return new Uri.Builder()
                .scheme("content")
                .authority(AUTHORITY)
                .appendPath(file.getName())
                .build();
    }

    private File resolve(Uri uri) throws FileNotFoundException {
        String name = uri.getLastPathSegment();
        if (name == null || name.contains("/") || name.contains("..") || name.isEmpty()) {
            throw new FileNotFoundException("Bad update path: " + uri);
        }
        File dir = updatesDir(getContext());
        File file = new File(dir, name);
        String dirPath;
        String filePath;
        try {
            dirPath = dir.getCanonicalPath();
            filePath = file.getCanonicalPath();
        } catch (IOException e) {
            throw new FileNotFoundException("Cannot resolve update path: " + e);
        }
        if (!filePath.startsWith(dirPath + File.separator)) {
            throw new FileNotFoundException("Path escapes update dir: " + uri);
        }
        if (!file.isFile()) {
            throw new FileNotFoundException("No such update file: " + uri);
        }
        return file;
    }

    @Override
    public boolean onCreate() {
        return true;
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (!"r".equals(mode)) {
            throw new FileNotFoundException("Update provider is read-only");
        }
        return ParcelFileDescriptor.open(resolve(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection,
                        String[] selectionArgs, String sortOrder) {
        File file;
        try {
            file = resolve(uri);
        } catch (FileNotFoundException e) {
            return null;
        }
        String[] cols = projection != null ? projection
                : new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE};
        MatrixCursor cursor = new MatrixCursor(cols, 1);
        Object[] row = new Object[cols.length];
        for (int i = 0; i < cols.length; i++) {
            if (OpenableColumns.DISPLAY_NAME.equals(cols[i])) {
                row[i] = file.getName();
            } else if (OpenableColumns.SIZE.equals(cols[i])) {
                row[i] = file.length();
            }
        }
        cursor.addRow(row);
        return cursor;
    }

    @Override
    public String getType(Uri uri) {
        return "application/vnd.android.package-archive";
    }

    @Override
    public Uri insert(Uri uri, ContentValues values) {
        return null;
    }

    @Override
    public int delete(Uri uri, String selection, String[] selectionArgs) {
        return 0;
    }

    @Override
    public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) {
        return 0;
    }
}
