# DARK NOTE — Unity open-world driving sim

Unity 6 (6000.0.85f2) + Android (IL2CPP, arm64-v8a) build of the OpenWorldSim driving
simulator: procedural city with highways and ramps, 60-car roster driven by CSV data,
per-car physics, synthesised engine audio, touch controls, garage and adaptive quality.

Package `com.alsphalt.darknote` · app name **DARK NOTE** · version 1.1 (versionCode 2)

## What changed from the zip you sent

The upload was scripts only — no `ProjectSettings/`, no `Packages/manifest.json`, no scene,
no `.meta` files. Unity could not open it, and six assemblies referenced URP/Addressables
packages that were never declared. Fixed here:

| Problem | Fix |
| --- | --- |
| No `Packages/manifest.json` | Added; declares URP 17.0.3, Addressables 2.2.2, uGUI 2.0.0 and every built-in module the code needs (including `vehicles` for `WheelCollider`) |
| No project settings | `ProjectSettings/ProjectVersion.txt` pins 6000.0.85f2; the rest is applied by code |
| No scene / bootstrap wiring | `Assets/Editor/ProjectSetup.cs` generates `Assets/Scenes/Main.unity` with `GameBootstrap` wired to the roster and city settings |
| Roster had to be imported by hand | Setup runs `VehicleCsvImporter` automatically on the bundled CSV |
| App not called DARK NOTE | Player settings set product name, bundle id, version, arm64, IL2CPP, Vulkan+GLES3, linear colour |
| `RoadLane` crashed on short/empty point lists | Guards in `Bake()` and `GetPoint()` |
| `ChunkStreamer` threw with no city or target | Null guards |
| `TrafficManager` threw before a player existed | Null guards on `player` and `prefabs` |
| `VehicleController.Apply()` threw with no definition or missing wheels | Guarded with clear errors |
| `EngineAudio` / `SynthEngineAudio` threw with no definition | Guarded |
| `AdaptiveQuality` divided by an empty step table | Guarded |
| `GameBootstrap` threw on a null roster array | Guarded |

No gameplay, UI or mechanic was removed; the diffs are guards plus project scaffolding.

## Open it

1. Install **Unity 6000.0.85f2** with **Android Build Support**, **Android SDK & NDK Tools**
   and **OpenJDK** (Unity Hub → Installs → Add modules).
2. Unity Hub → Add → select this folder (`unity/DarkNote`).
3. Menu **Sim → Setup → Configure Project**. This imports the 60 cars, creates the scene
   and applies the Android settings. Takes a few seconds.
4. Press Play in `Assets/Scenes/Main.unity`.

## Build locally

- Menu **Sim → Build → Android APK (debug)** — writes `build/Android/DarkNote-1.1-debug.apk`.
- Or: `Unity -batchmode -quit -projectPath . -executeMethod BuildTools.BuildScript.PerformBuild`

Debug builds use Unity's debug key, so they install straight onto a phone with
"install unknown apps" allowed.

## Render pipeline

The project runs on the **Built-in pipeline** by default and the materials fall back to the
`Standard` shader. That is deliberate: creating a URP asset from code without an editor to
verify it risks an all-pink scene, and a working scene matters more for a test APK.

To switch to URP: **Sim → Setup → Optional: create and assign a URP asset**, then open the
scene and confirm the materials look right. The URP shader path is already used by
`PlaceholderCarFactory` and `CityGenerator` whenever URP is the active pipeline.

## GitHub Actions

Workflow: `.github/workflows/unity-android.yml` (Actions → **Build Android APK** → Run workflow).

Required secret — one of these two, set under **Settings → Secrets and variables → Actions**:

| Secret | Value |
| --- | --- |
| `UNITY_LICENSE` | Full text of your Unity licence file (`.ulf`), from `%ProgramData%/Unity/Unity_lic.ulf` on Windows or `~/Library/Application Support/Unity/Unity_lic.ulf` on macOS |
| `UNITY_EMAIL` + `UNITY_PASSWORD` (+ `UNITY_SERIAL`) | Unity account login; `UNITY_SERIAL` is required for Plus/Pro |

Optional, only for `release` builds:

| Secret | Value |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 your.keystore` |
| `ANDROID_KEYSTORE_PASS` | Keystore password |
| `ANDROID_KEYALIAS` | Key alias |
| `ANDROID_KEYALIAS_PASS` | Key password |

Without the keystore secrets a release build still completes, signed with the debug key.

Getting `UNITY_LICENSE`: run `Unity -batchmode -quit -createManualActivationFile` locally,
upload the `.alf` at license.unity3d.com, download the `.ulf`, and paste its contents into
the secret. Manual activation is free for Personal.

## Known limits (unchanged from the original design)

- Cars are placeholder boxes; the 60 CSV rows carry the physics, not the art.
- No interior meshes, no recorded engine audio (the synthesiser is used instead).
- Junctions use a one-car lock rather than traffic lights; no pedestrians or props.
- Only one highway direction (north-south).
- Real brand names (Audi/BMW/Ford/Toyota) are trademarked — see `Docs/ARCHITECTURE.md`
  before shipping anything publicly.
