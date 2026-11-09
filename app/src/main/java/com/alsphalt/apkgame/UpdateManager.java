package com.alsphalt.apkgame;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.util.Log;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.ByteArrayOutputStream;
import java.lang.ref.WeakReference;
import java.net.HttpURLConnection;
import java.net.URL;
import java.security.MessageDigest;
import java.util.Locale;

/**
 * Wi-Fi-only update checker for DARK NOTE.
 *
 * Credentials: none. The release metadata and APK come from the public GitHub Releases
 * API over an unauthenticated request, so no GitHub token is compiled into, bundled
 * with, or stored by the APK. Publishing a release is done by the GitHub Actions
 * workflow with the runner-scoped GITHUB_TOKEN, which never leaves the runner.
 *
 * Transport rules: HTTPS only, redirects followed manually with a host allow-list,
 * APK checksum pinned to the SHA-256 published in the same release.
 */
public final class UpdateManager {

    private static final String TAG = "DarkNoteUpdate";

    private static final String OWNER = "alsphalt";
    private static final String REPO = "apk-game";
    private static final String RELEASE_TAG = "nightly";

    private static final String RELEASE_URL =
            "https://api.github.com/repos/" + OWNER + "/" + REPO + "/releases/tags/" + RELEASE_TAG;

    /** Re-check at most once per 6 h while the app stays open. */
    private static final long CHECK_INTERVAL_MS = 6L * 60L * 60L * 1000L;

    private static final String[] ALLOWED_HOSTS = {
            "api.github.com",
            "github.com",
            "objects.githubusercontent.com",
            "github-releases.githubusercontent.com",
            "release-assets.githubusercontent.com"
    };

    private static UpdateManager instance;

    public static synchronized UpdateManager get(Context context) {
        if (instance == null) {
            instance = new UpdateManager(context.getApplicationContext());
        }
        return instance;
    }

    private final Context appContext;
    private final Handler main = new Handler(Looper.getMainLooper());

    private WeakReference<Activity> activityRef;
    private boolean checking;
    private boolean wifiCallbackRegistered;
    private long lastCheckAt;
    private String promptedVersion = "";
    private String stagedVersion = "";

    private UpdateManager(Context appContext) {
        this.appContext = appContext;
    }

    /** Called from {@code MainActivity.onResume()}. */
    public void onResume(Activity activity) {
        activityRef = new WeakReference<>(activity);
        maybeCheck(false);
    }

    // ---------- network policy ----------

    private boolean onWifi() {
        ConnectivityManager cm =
                (ConnectivityManager) appContext.getSystemService(Context.CONNECTIVITY_SERVICE);
        if (cm == null) return false;
        Network network = cm.getActiveNetwork();
        if (network == null) return false;
        NetworkCapabilities caps = cm.getNetworkCapabilities(network);
        if (caps == null) return false;
        return caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)
                && caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)
                && caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED);
    }

    /** No mobile data, no metered Wi-Fi: updates wait for a good Wi-Fi link. */
    private void registerWifiRetry() {
        if (wifiCallbackRegistered) return;
        ConnectivityManager cm =
                (ConnectivityManager) appContext.getSystemService(Context.CONNECTIVITY_SERVICE);
        if (cm == null) return;
        try {
            cm.registerDefaultNetworkCallback(new ConnectivityManager.NetworkCallback() {
                @Override
                public void onAvailable(Network network) {
                    if (onWifi()) {
                        Log.i(TAG, "Wi-Fi available, resuming update check");
                        maybeCheck(true);
                    }
                }
            });
            wifiCallbackRegistered = true;
        } catch (Exception e) {
            Log.w(TAG, "Cannot register network callback: " + e);
        }
    }

    // ---------- check ----------

    private void maybeCheck(final boolean force) {
        Activity activity = activityRef == null ? null : activityRef.get();
        if (activity == null || activity.isFinishing()) return;
        if (checking) return;

        long now = System.currentTimeMillis();
        if (!force && now - lastCheckAt < CHECK_INTERVAL_MS) return;

        if (!onWifi()) {
            registerWifiRetry();
            Log.i(TAG, "Update check skipped: Wi-Fi only policy (not on validated Wi-Fi)");
            return;
        }

        checking = true;
        lastCheckAt = now;

        new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    Release release = fetchRelease();
                    if (release == null) {
                        Log.i(TAG, "No published release yet");
                        return;
                    }
                    int installed = installedVersionCode();
                    if (release.versionCode <= installed) {
                        Log.i(TAG, "Up to date (" + installed + " vs " + release.versionCode + ")");
                        return;
                    }
                    Log.i(TAG, "Update available: " + release.versionName);
                    final Release found = release;
                    main.post(new Runnable() {
                        @Override
                        public void run() {
                            showUpdatePrompt(found);
                        }
                    });
                } catch (Exception e) {
                    Log.w(TAG, "Update check failed: " + e);
                } finally {
                    checking = false;
                }
            }
        }, "darknote-update-check").start();
    }

    private static final class Release {
        int versionCode;
        String versionName = "";
        String sha256 = "";
        String apkUrl = "";
        String notes = "";
    }

    private Release fetchRelease() throws Exception {
        String body = readAll(open(RELEASE_URL), 1 << 20);
        JSONObject json = new JSONObject(body);

        JSONArray assets = json.optJSONArray("assets");
        if (assets == null) return null;

        String manifestUrl = null;
        String apkUrl = null;
        String apkName = "";
        for (int i = 0; i < assets.length(); i++) {
            JSONObject asset = assets.getJSONObject(i);
            String name = asset.optString("name", "");
            String url = asset.optString("browser_download_url", "");
            if (name.equals("update.json")) {
                manifestUrl = url;
            } else if (name.endsWith(".apk")) {
                apkUrl = url;
                apkName = name;
            }
        }
        if (manifestUrl == null) {
            Log.i(TAG, "Release has no update.json manifest, skipping");
            return null;
        }

        JSONObject manifest = new JSONObject(readAll(open(manifestUrl), 64 * 1024));
        Release rel = new Release();
        rel.versionCode = manifest.optInt("versionCode", 0);
        rel.versionName = manifest.optString("versionName", "");
        rel.sha256 = manifest.optString("sha256", "").toLowerCase(Locale.US);
        rel.notes = manifest.optString("notes", "");
        String apkField = manifest.optString("apk", "");

        if (!apkField.isEmpty()) {
            for (int i = 0; i < assets.length(); i++) {
                JSONObject asset = assets.getJSONObject(i);
                if (apkField.equals(asset.optString("name", ""))) {
                    rel.apkUrl = asset.optString("browser_download_url", "");
                    break;
                }
            }
        }
        if (rel.apkUrl.isEmpty()) rel.apkUrl = apkUrl;

        if (rel.versionCode <= 0 || rel.apkUrl == null || rel.apkUrl.isEmpty()
                || rel.sha256.length() != 64) {
            Log.w(TAG, "Release manifest incomplete for " + apkName + ", skipping");
            return null;
        }
        return rel;
    }

    private int installedVersionCode() {
        try {
            PackageInfo info = appContext.getPackageManager()
                    .getPackageInfo(appContext.getPackageName(), 0);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                return (int) info.getLongVersionCode();
            }
            return info.versionCode;
        } catch (PackageManager.NameNotFoundException e) {
            return 0;
        }
    }

    // ---------- UI ----------

    private void showUpdatePrompt(final Release rel) {
        Activity activity = activityRef == null ? null : activityRef.get();
        if (activity == null || activity.isFinishing()) return;
        if (rel.versionName.equals(promptedVersion)) return;
        promptedVersion = rel.versionName;

        String message = "Version " + rel.versionName + " is available.\n\n"
                + "Downloads only over Wi-Fi. The package is checksum-verified before install.";
        if (!rel.notes.isEmpty()) {
            message += "\n\n" + rel.notes;
        }

        new AlertDialog.Builder(activity, android.R.style.Theme_Material_Dialog_Alert)
                .setTitle("DARK NOTE update")
                .setMessage(message)
                .setPositiveButton("Download", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        downloadAndInstall(rel);
                    }
                })
                .setNegativeButton("Later", null)
                .show();
    }

    private void downloadAndInstall(final Release rel) {
        final Activity activity = activityRef == null ? null : activityRef.get();
        if (activity == null || activity.isFinishing()) return;
        if (!onWifi()) {
            toast(activity, "Connect to Wi-Fi to download the update");
            registerWifiRetry();
            return;
        }

        final AlertDialog progress = new AlertDialog.Builder(
                activity, android.R.style.Theme_Material_Dialog_Alert)
                .setTitle("Downloading update")
                .setMessage("0%")
                .setCancelable(false)
                .show();

        new Thread(new Runnable() {
            @Override
            public void run() {
                File target = ApkFileProvider.apkFile(appContext);
                String error = null;
                try {
                    download(rel, target, progress);
                } catch (Exception e) {
                    Log.w(TAG, "Update download failed: " + e);
                    error = e.getMessage();
                    //noinspection ResultOfMethodCallIgnored
                    target.delete();
                }
                final String failure = error;
                main.post(new Runnable() {
                    @Override
                    public void run() {
                        progress.dismiss();
                        if (failure != null) {
                            toast(activity, "Update failed: " + failure);
                            return;
                        }
                        stagedVersion = rel.versionName;
                        requestInstall(rel);
                    }
                });
            }
        }, "darknote-update-download").start();
    }

    private void download(Release rel, File target, final AlertDialog progress) throws Exception {
        HttpURLConnection conn = open(rel.apkUrl);
        long total = conn.getContentLengthLong();
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        File parent = target.getParentFile();
        if (parent != null && !parent.exists()) {
            //noinspection ResultOfMethodCallIgnored
            parent.mkdirs();
        }

        try (InputStream in = conn.getInputStream();
             FileOutputStream out = new FileOutputStream(target)) {
            byte[] buffer = new byte[64 * 1024];
            long read = 0L;
            int postedPct = -5;
            int n;
            while ((n = in.read(buffer)) > 0) {
                out.write(buffer, 0, n);
                digest.update(buffer, 0, n);
                read += n;
                if (total > 0) {
                    final int pct = (int) (read * 100 / total);
                    if (pct >= postedPct + 5) {
                        postedPct = pct;
                        main.post(new Runnable() {
                            @Override
                            public void run() {
                                progress.setMessage(pct + "%");
                            }
                        });
                    }
                }
            }
            out.flush();
        } finally {
            conn.disconnect();
        }

        String actual = toHex(digest.digest());
        if (!actual.equals(rel.sha256)) {
            throw new IOException("checksum mismatch (expected " + rel.sha256
                    + ", got " + actual + ")");
        }
        Log.i(TAG, "Update downloaded and verified: " + target.length() + " bytes");
    }

    private void requestInstall(final Release rel) {
        final Activity activity = activityRef == null ? null : activityRef.get();
        if (activity == null || activity.isFinishing()) return;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                && !appContext.getPackageManager().canRequestPackageInstalls()) {
            new AlertDialog.Builder(activity, android.R.style.Theme_Material_Dialog_Alert)
                    .setTitle("Allow installs")
                    .setMessage("Android needs permission to install the verified DARK NOTE "
                            + rel.versionName + " package. Enable \"Allow from this source\", "
                            + "then reopen the app.")
                    .setPositiveButton("Open settings", new DialogInterface.OnClickListener() {
                        @Override
                        public void onClick(DialogInterface dialog, int which) {
                            Intent intent = new Intent(
                                    Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                                    Uri.parse("package:" + appContext.getPackageName()));
                            try {
                                activity.startActivity(intent);
                            } catch (Exception e) {
                                toast(activity, "Open Settings > Apps > DARK NOTE manually");
                            }
                        }
                    })
                    .setNegativeButton("Cancel", null)
                    .show();
            return;
        }

        Intent install = new Intent(Intent.ACTION_VIEW);
        install.setDataAndType(ApkFileProvider.uriFor(ApkFileProvider.apkFile(appContext)),
                "application/vnd.android.package-archive");
        install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
        try {
            activity.startActivity(install);
        } catch (Exception e) {
            Log.w(TAG, "Install intent failed: " + e);
            toast(activity, "No package installer available");
        }
    }

    private void toast(Activity activity, String text) {
        main.post(new Runnable() {
            @Override
            public void run() {
                Toast.makeText(activity, text, Toast.LENGTH_LONG).show();
            }
        });
    }

    // ---------- transport ----------

    private static boolean allowedHost(String host) {
        if (host == null) return false;
        for (String allowed : ALLOWED_HOSTS) {
            if (host.equals(allowed) || host.endsWith("." + allowed)) return true;
        }
        return false;
    }

    private static HttpURLConnection open(String spec) throws IOException {
        String current = spec;
        for (int hop = 0; hop < 5; hop++) {
            URL url = new URL(current);
            if (!"https".equalsIgnoreCase(url.getProtocol())) {
                throw new IOException("Refusing non-HTTPS URL");
            }
            if (!allowedHost(url.getHost())) {
                throw new IOException("Refusing host " + url.getHost());
            }
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setConnectTimeout(15000);
            conn.setReadTimeout(30000);
            conn.setInstanceFollowRedirects(false);
            conn.setRequestProperty("User-Agent", "DarkNote-Updater");
            conn.setRequestProperty("Accept", "application/vnd.github+json");
            int code = conn.getResponseCode();
            if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308) {
                String location = conn.getHeaderField("Location");
                conn.disconnect();
                if (location == null) throw new IOException("Redirect without Location");
                current = new URL(url, location).toString();
                continue;
            }
            if (code != 200) {
                conn.disconnect();
                throw new IOException("HTTP " + code);
            }
            return conn;
        }
        throw new IOException("Too many redirects");
    }

    private static String readAll(HttpURLConnection conn, int maxBytes) throws IOException {
        try (InputStream in = conn.getInputStream()) {
            ByteArrayOutputStream out = new ByteArrayOutputStream();
            byte[] buffer = new byte[16 * 1024];
            int total = 0;
            int n;
            while ((n = in.read(buffer)) > 0) {
                total += n;
                if (total > maxBytes) throw new IOException("Response too large");
                out.write(buffer, 0, n);
            }
            return new String(out.toByteArray(), "UTF-8");
        } finally {
            conn.disconnect();
        }
    }

    private static String toHex(byte[] bytes) {
        StringBuilder sb = new StringBuilder(bytes.length * 2);
        for (byte b : bytes) {
            sb.append(String.format(Locale.US, "%02x", b));
        }
        return sb.toString();
    }
}
