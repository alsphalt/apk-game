# apk-game

A self-contained Android game that builds to a debug APK on GitHub Actions with no
secrets and no Unity licence.

**Genre:** endless highway runner. Three lanes, oncoming traffic, speed ramps up with
score, tap to start, drag left/right to steer. Rendering is plain `android.graphics.Canvas`
on a custom `View` — no third-party dependencies, no AndroidX, no assets.

## Get the APK

1. Open the **Actions** tab.
2. Pick the newest **Build Debug APK** run.
3. Scroll to **Artifacts** and download **app-debug** — a ZIP containing `app-debug.apk`.
4. Install with `adb install app-debug.apk`, or copy it to the phone and open it
   (you will need to allow "install unknown apps").

The workflow also runs on every push to `main` and can be started by hand from
**Actions → Build Debug APK → Run workflow**.

## Build locally

```bash
gradle :app:assembleDebug
# -> app/build/outputs/apk/debug/app-debug.apk
```

Requires JDK 17 and Android SDK platform 34.

## Layout

```
app/src/main/java/com/alsphalt/apkgame/
  MainActivity.java   fullscreen host, screen-on, lifecycle
  GameView.java       game loop, procedural road, traffic, HUD, input
.github/workflows/
  android-debug.yml   CI: JDK 17 + Gradle 8.9 + AGP 8.7.3 -> app-debug.apk
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
