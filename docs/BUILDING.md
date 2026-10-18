# Building OpenFusion:Pocket

## Requirements

- Unity **2022.3.62f3**, matching `ProjectSettings/ProjectVersion.txt`.
- Android Build Support, including the SDK, NDK and OpenJDK installed through Unity Hub.
- Internet access for Unity packages and server-backed validation.
- Enough disk space for Unity's generated `Library/` directory and downloaded game resources.

Unity/URP are not vendored. Package versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`.

## Open the project

Clone the repository, then add its root folder to Unity Hub as an existing project. Let Unity restore packages and import resources. Open `Assets/Scenes/Game.unity`; Game is the supported build scene. MapTest was removed.

Runtime resources are obtained through the launcher and selected server. You do not need to commit or copy a personal launcher cache, account data or game bundles into the source tree.

## Build an APK

Close other Unity instances using this project before running batch builds. From PowerShell at the project root:

```powershell
.\tools\unity-build.ps1 release
```

The helper resolves the exact editor version and writes the APK to `Builds/OpenFusion-release.apk`. For a development APK, use `apk` instead of `release`. To supply an editor path explicitly:

```powershell
.\tools\unity-build.ps1 release -UnityPath 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
```

The `OpenFusion` menu in Unity also offers Android debug, quick iteration and release builds. The setup applies IL2CPP, ARMv7 + ARM64, Gamma color space, the rendering/shader configuration and the Game scene. Build logs are written under `Logs/`.

## Run the release regression checks

The release validation entry point runs the input, controller startup, combat, world, weapon/audio, connection, Android, startup and terrain checks before building:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'
& $unity -batchmode -nographics -projectPath $PWD.Path `
  -executeMethod OpenFusion.EditorTools.OpenFusionBuildRequest.Batch `
  -logFile "$($PWD.Path)\Logs\release-validation.log"
```

Check the process exit code, `Logs/release-result.txt` and the fresh APK timestamp. Several checks contact live server/CDN endpoints; service availability affects those checks. A passing batch run does not replace Android device testing, GPU checks or complete mission playthroughs.

Standalone projects under `tests/` are development aids. Some require local fixture files obtained from the game or extraction utilities and some have historical dependency assumptions. Their presence is not a claim that every standalone project runs without setup. See each project's source and the historical notes for fixture expectations.

## Install and inspect

```powershell
adb devices
adb -s YOUR_DEVICE_SERIAL install -r Builds/OpenFusion-release.apk
adb -s YOUR_DEVICE_SERIAL shell am start -n com.openfusion.android/com.unity3d.player.UnityPlayerActivity
adb -s YOUR_DEVICE_SERIAL logcat -s Unity
```

`install -r` retains existing data when package and signing identity match. Local builds may use your local Android development certificate if no custom signing identity is configured. They are not guaranteed to update a release signed on another machine. No private keystore or signing password is distributed in this repository.

The v1.5 DS Companion asset includes the GitHub updater and uses Android build code 3 (v1.0 used build 2). Its application ID remains `com.openfusion.android`, and its display name is OpenFusion:Pocket. After building, use `tools/prepare-release.ps1` to generate the APK, update manifest and checksums for publication. See [UPDATER.md](UPDATER.md) for the release workflow.

## Useful references

- [Credits and dependencies](../CREDITS.md)
- [Notices and resource attribution](../NOTICE)
- [Historical development notes](DEVELOPMENT_HISTORY.md): early investigation notes, not a current feature checklist.
