# DARK NOTE

A self-contained Android endless-runner game that builds to a debug APK on GitHub
Actions with no secrets and no Unity licence.

**Genre:** endless highway runner. Three lanes, oncoming traffic, speed ramps up with
score, tap to start, drag left/right to steer. Rendering is plain `android.graphics.Canvas`
on a custom `View` — no third-party dependencies, no AndroidX, no image assets.

Package: `com.alsphalt.apkgame` · label: **DARK NOTE** · version 1.1 (versionCode 2)

## Get the APK

1. Open the **Actions** tab.
2. Pick the newest **Build Debug APK** run.
3. Download the **Android-Debug-Build** artifact — a ZIP containing `app-debug.apk`.
4. Install with `adb install app-debug.apk`, or copy it to the phone and open it
   (you will need to allow "install unknown apps").

The workflow also runs on every push to `main` and can be started by hand from
**Actions → Build Debug APK → Run workflow**.

## Updates

Pushes to `main` build the APK and publish it to the [`nightly`](../../releases/tag/nightly)
release together with an `update.json` checksum manifest. The app checks that release on
launch and installs newer versions in place.

**Wi-Fi only.** The check and the download run only when the active network is Wi-Fi,
has validated internet access, and Android has not flagged it as metered. On mobile data
the app stays silent and retries automatically as soon as Wi-Fi comes back.

**No credentials in the APK.** Release metadata and assets are fetched from the public
GitHub Releases API with an unauthenticated request, so the app ships no GitHub token,
neither in Java sources, `BuildConfig`, resources, nor `shared_prefs`. Publishing is done
by the workflow, which uses the runner-scoped `GITHUB_TOKEN` (`contents: write`); that
token never leaves the runner and is never an APK input.

**Integrity.** Downloads are HTTPS-only and restricted to `github.com` /
`api.github.com` / GitHub asset hosts (redirects are followed manually and re-validated
per hop). The APK's SHA-256 must match the one published in `update.json`; a mismatch
deletes the file and aborts. The APK is served to the system installer through a
non-exported content provider with a per-intent read grant. Android still shows its own
install confirmation — a sideloaded app cannot install silently.

Updates need a stable signing certificate. `ci/darknote-debug.keystore` (password
`darknotedebug`, alias `darknote`) is committed so every CI build shares one debug
identity; without it, an update would be rejected with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`.
This key is debug-only — never use it for a store release.

## Build locally

```bash
gradle :app:assembleDebug
# -> app/build/outputs/apk/debug/app-debug.apk
```

Requires JDK 17 and Android SDK platform 34.

## Layout

```
app/src/main/java/com/alsphalt/apkgame/
  MainActivity.java     fullscreen host, screen-on, lifecycle
  GameView.java         game loop, procedural road, traffic, HUD, input
  UpdateManager.java    Wi-Fi-only update check, verified download, install hand-off
  ApkFileProvider.java  non-exported provider that serves the staged APK
ci/
  darknote-debug.keystore  stable debug signing identity for updates
.github/workflows/
  android-debug.yml     CI: JDK 17 + Gradle 8.9 + AGP 8.7.3 -> APK, artifact, nightly release
```

## Tuning

Most of the feel lives in `GameView`:

| What | Where |
| --- | --- |
| Lane count | `LANES` |
| Start / max speed | `speed = Math.min(1.55f, 0.5f + score * 0.00055f)` |
| Traffic density | `gap` inside `step()` |
| Road bend | `bend = sin(distance * 0.00035) * w * 0.16f` |
| Traffic colours | `obstacleColors` |

## Note on Unity

A Unity project cannot be built here without a licence. Unity's CI images need
`UNITY_LICENSE` (or `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL`) repository
secrets, plus roughly 10 GB of editor download per run. If the intent is to build a
Unity game from this repo, add those secrets and the workflow has to be switched to
`game-ci/unity-builder`.
