# iOS single-screen preview

The native iPhone/iPad edition is published as experimental iOS v0.5; it is not device-certified. This document records the initial private builds and remaining validation work. The Windows Xcode export succeeded on 28 October 2026. Compilation uses a private GitHub Actions repository; only generated Xcode input is uploaded, without device logs, pairing records or signing/account credentials. Android remains in the existing checkout; the native export uses an isolated local copy. End-user instructions: [iOS installation](IOS_INSTALL.md).

## Prepared behavior

- iOS build configuration: IL2CPP, ARM64, Metal, gamma color, landscape, iPhone/iPad, minimum iOS 16.0. This minimum is a configurable starting point, not a performance/compatibility guarantee.
- The iOS preview always uses the full single-screen interface, including normal Map, Nanos, Equipment, Missions, chat and E-Mail access. Android DS preferences do not activate a simulated split or reserve a second-screen area.
- Credentials use a native Keychain bridge with an app-specific service, no iCloud synchronization and `WhenUnlockedThisDeviceOnly` protection. There is no plaintext PlayerPrefs fallback. Saving still happens through the existing successful-login flow.
- Diagnostic sharing uses the native share sheet, including the iPad popover anchor.
- The iOS update view does not offer or download Android APK packages. A real iOS distribution/update channel has not been configured.
- Existing touch controls and game logic are reused. The DS bridge, APK installer and Android diagnostic probes are excluded from iOS execution/compilation paths.
- Export postprocessing adds Security.framework, ARC for the native plugin, local-network purpose text and the existing `ofl://` server-link scheme. HTTPS transport policy is not relaxed globally.

## Export an Xcode project

Install Unity **2022.3.62f3** with **iOS Build Support**. Open this project and select iOS in Build Settings. Use **OpenFusion → iOS → Export Xcode preview**, or run Unity with:

```text
-batchmode -buildTarget iOS -projectPath PROJECT_PATH -executeMethod OpenFusion.EditorTools.PocketIosBuild.BatchExport -logFile IOS_EXPORT_LOG
```

Default output: `Builds/iOS/Xcode`. Environment variable `POCKET_IOS_XCODE_OUTPUT` can select another output directory. This exports a development Xcode project. It does not sign, install or upload an IPA. Keep Android and iOS builds sequential, since Unity changes active build targets and imports assets for each platform.

The official Windows iOS Build Support module for Unity 2022.3.62f3 was installed on 28 October 2026 after the owner completed its elevation/installer prompts. Xcode export then succeeded in `C:/Users/RSigrist/AppData/Local/OpenFusionLauncher/PocketIOSNative/Builds/iOS/Xcode`, with 0 errors and 15 warnings. The native preview uses Release without script debugging, ARM64, Metal and iOS 16+. The existing Android project settings and Library remain separate.

## Compile and sign on macOS

1. Install Xcode and its selected command-line tools. Open the exported `Unity-iPhone.xcodeproj`.
2. Select your development team for the application and the signing setup appropriate for your device/distribution method. No team, private keys or provisioning profiles are included in this repository.
3. First build and run on a real device from Xcode. Resolve native compilation, entitlement and signing errors there before preparing distribution.
4. Use Xcode Archive/Distribute to export a signed IPA. Alternatively, provide your own Xcode export-options plist to:

```bash
POCKET_APPLE_TEAM_ID=YOUR_TEAM_ID bash tools/ios-export-ipa.sh Builds/iOS/Xcode /path/outside/repository/ExportOptions.plist Builds/iOS/Signed
```

The script archives and exports locally. It performs no upload, creates no certificates and does not bypass signing. The provisioning profile determines which devices can install the resulting IPA. Do not commit signing credentials or profiles.

## Validation and limits

### Personal testing with an iPhone and Windows, without owning a Mac

The test device reported by the owner is an iPhone SE, part number MMX83LL/A, running iOS 18.5. This is a candidate device; no installation or device test has occurred yet.

A macOS build environment is still required for the native compiler, but the owner does not need a Mac. The private `rsigristc/OpenFusion-Pocket-iOS-Lab` harness downloads the hashed, generated Xcode project and builds it on a GitHub-hosted macOS 15 runner with Xcode 16.4 / the iOS 18.5 SDK. Unity export and licensing run locally on Windows. No Apple account, certificate, pairing record or provisioning profile is sent to Actions. The internal private input upload is not a Pocket app release.

For personal testing without an Apple Developer distribution profile:

1. Export the Xcode project using the Unity command above on an environment with iOS Build Support.
2. On the Mac, run `bash tools/ios-build-unsigned.sh Builds/iOS/Xcode`. This builds for an ARM64 physical device and packages a fresh `Payload` directory into an **unsigned** IPA. It does not run `-exportArchive`, use an Apple account or publish anything.
3. Transfer the resulting IPA to Windows. It cannot run on the iPhone while unsigned.
4. Install and configure **AltStore Classic/AltServer** using its official Windows instructions; use your own Apple account locally with that tool. Do not put its password or authentication codes in this repository or chat. Let the tool sign/install the IPA, then validate the app on the device.
5. With a free account, plan for periodic renewal: AltStore documents seven-day expiry for its free-account sideloading route. This is personal installation, not TestFlight or an App Store release.

Only Bash syntax of the unsigned-build script has been checked on Windows. The actual native compilation, packaging, re-signing, embedded Unity framework handling and installation still require end-to-end validation. Reference: [AltStore Classic for Windows](https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows), [AltStore renewal](https://faq.altstore.io/altstore-classic/your-altstore).

`IosPreparationChecks` checks the native/managed entry-point agreement and source boundaries during the normal Android regression build. `tools/check-ios-managed.py` compiles iOS C# branches using Unity Roslyn, excluding the Android JNI reference. It does **not** replace an actual iOS SDK, Xcode or IL2CPP build; editor postprocessing and native code remain to be validated on macOS.

Windows validation on 26 October 2026:

- Preparation source checks: passed (`Logs/ios/preparation-checks.txt`).
- iOS C# conditional compilation without the Android JNI reference: passed (`Logs/ios/managed-check/compiler.log`), with 8 existing unused-field warnings. This compiled managed source, not an iOS binary.
- Full Android regression suite and APK build after the platform separation: passed, **0 errors and 17 warnings** (`Logs/ios/android-regression-build.log`).
- Export script Bash syntax and managed-check script Python syntax: passed. The IPA export script has not executed Xcode on macOS.
- Native plugin, Keychain, share sheet, iOS editor postprocessing, Metal rendering, Xcode export, IPA signing and physical-device behavior: **not yet validated**. No IPA has been generated or published.

On a real iPhone and iPad verify:

- First launch, launcher safe area, keyboard, touch profiles and supported gamepads.
- Login, character creation/selection, intro, tutorial and world loading.
- Cache reuse, asset downloads, TCP connection, cancellation and reconnect.
- Metal shaders, terrain, translucent icons, particles, models, animations, shadows and preview render targets.
- Audio, memory pressure, background/resume, orientation and safe areas around notches/home indicators.
- Explicit credential save after login, relaunch/load, overwrite, forget one account, forget all, locked-device behavior and no plaintext persistence. Keychain entries can outlive removal of the app; use Forget during tests.
- Diagnostics share sheet on both device families.
- Single-screen access to all game panels, and no APK update download/install controls.

External-screen DS on iPad, an iOS release channel, TestFlight/App Store review and complete gameplay/device fidelity are outside this first preview. They are not certified by a successful C# compilation.

Official references: [Unity iOS setup](https://docs.unity3d.com/6000.0/Documentation/Manual/ios-environment-setup.html), [Unity iOS plug-ins](https://docs.unity3d.com/2022.3/Documentation/Manual/PluginsForIOS.html), [Apple Keychain protection](https://developer.apple.com/documentation/security/ksecattraccessiblewhenunlockedthisdeviceonly), [Apple device distribution](https://developer.apple.com/documentation/xcode/distributing-your-app-to-registered-devices).


### Native pipeline validation on 28 October 2026

- Official iOS module: cached download MD5 `df95fa014f6026f2a1ff1f46a458438d`, valid Unity Technologies signature; installed.
- Editor target separation: Android-only Gradle regression fixture now compiles only under `UNITY_ANDROID`.
- Windows native Xcode export: succeeded, 0 errors / 15 warnings. Build GUID `ae913d4fde2044b194c15c8ad868116f`.
- Prepared iOS source checks: passed during the export. Native Keychain/sharing and runtime behavior still need device tests.
- Xcode input ZIP: 289055966 bytes, SHA-256 `35ad5170f41ec3f97c1e655971b9620c172e872b0e519c6ef98753e1d937a9a8`.
- Private remote build: `https://github.com/rsigristc/OpenFusion-Pocket-iOS-Lab/actions/runs/40784038424`; succeeded with Xcode 16.4 / iOS 18.5 SDK.
- Host build uses Release ARM64 without Apple signing credentials. The package is verified/ad-hoc signed for transfer, then must be re-signed locally with AltStore.
- An Apple free-account installation may require deactivating the experimental Husk app to free one of its app slots. App removal/deactivation is left to the owner.
- Husk experiments established launcher/character-selection compatibility, but world-load ENOMEM near 1.9 GiB and 5-7 FPS remained unresolved. These do not certify performance or memory usage of the native port; both must be measured on the iPhone.


### First native IPA produced

The private build completed successfully. The application and UnityFramework are
ARM64, bundle ID `com.openfusion.pocket.ios`, version 3.0 / build 6, minimum iOS
16.0. The native Keychain/share entry points are present in the framework. The
IPA contains no Husk/QEMU/ANGLE libraries. Ad-hoc bundle signature verification
passed on the runner; AltStore must still sign it locally for installation.

- IPA: `Logs/ios/native-build-40784038424/OpenFusion-Pocket-iOS-Lab.ipa`.
- Size: 61969669 bytes.
- SHA-256: `fa178d6cee8946b84472574e5ef089f0eb69d392a0242b8d1527827a62a3a2e2`.
- Copied to the iPhone's StikDebug Documents folder and verified after transfer.
- Owner confirmed native launch, login and character selection. The first world
  load closed the app; its cause remains unconfirmed (no native crash or jetsam
  report was available). A repeat attempt successfully entered the world;
  live USB logs showed player movement, camera updates and a memory warning
  during loading. This does not establish that the intermittent close is fixed.
- Stage samples on the successful attempt: 720 MiB managed / 232 MiB Unity at
  `LOADING WORLD`. These are not peak physical footprint measurements and must
  not be added together as independent memory totals.
- Sound, touch completeness, sustained FPS and repeated cold/warm loading remain
  device validation work.

### Loading-memory preview (private, build 7)

- The legacy serialized reader now reads numeric scalars directly from its input
  buffer instead of allocating a temporary array for each scalar. 4,096 bit-exact
  LE/BE fixtures, packed offsets, alignment and truncated input pass. 100,000
  direct integer reads allocate zero bytes versus 3,200,000 in the baseline
  .NET experiment. Generic `Read(TypeNode)` still boxes scalar results.
- iOS waits for preview destruction, unloads unused Unity objects and collects
  managed garbage before world loading. During the initial shared-bundle pass,
  collection boundaries reclaim decode/index temporaries after 32 MiB of managed
  growth or a memory warning. No bundle cache, model or texture quality is removed.
  These explicit collections are not run in the gameplay update loop.
- Fixed-stage numeric diagnostics (`ios-loading-memory.log`, bounded to about
  32 KiB) record managed/Unity allocations, reclaimed managed bytes and memory
  warnings. They contain no account, chat, packet or location information. They
  do not measure peak process footprint or guarantee avoidance of iOS jetsam.
- Compare at least three separate app launches/world entries after signing and
  installing the preview. Keep the same character, renderer and resolution; do
  not clear bundle caches between warm-load tests. Record failures as well as
  successes. Export/build success alone does not validate these runtime changes.
- Build 7 succeeded privately with Xcode 16.4:
  `https://github.com/rsigristc/OpenFusion-Pocket-iOS-Lab/actions/runs/40802769942`.
  The 2,931-file generated project was reconstructed from a verified base and
  1,304,328-byte delta; every target file was checked before compiling.
- IPA: `Logs/ios/native-build-40802769942/OpenFusion-Pocket-iOS-Lab.ipa`,
  61,972,412 bytes, SHA-256
  `9987017f8b4335914d129f400fa881699131d27a9c418f345537eb762b78af5a`.
  ARM64 app/framework, native bridge and loading-memory instrumentation verified.
- Copied to StikDebug Documents on the iPhone with matching SHA-256. AltStore
  re-signing/installation and repeated world-load validation remain pending.

### Public experimental iOS v0.5 / build 8

The version series is independent of Android: `IosAppVersion=0.5`,
`IosBuildCode=8`, retaining `com.openfusion.pocket.ios`. Private previews labeled
3.0 require a manual first update to this new series. The native build succeeded
in run `40805704297`; IPA SHA-256:
`20937f2d539720abb34b295759eaad3639192d062dd5d378603453a5f024f398`.

The GitHub iOS release parser and cross-platform updater regressions pass,
including channel isolation, bundle/version/asset/digest/minimum-OS checks and
downgrade rejection. The compiled IPA contains native installed-build and cached
file-sharing entry points. AltStore permissions are generated from the actual
IPA: `get-task-allow` and the local-network usage description. Release downloads
remain ad-hoc signed inputs for a user's signing tool, not Apple-distributed apps.

Repeated on-device loading, native file-share installation flow and broader
sound/input/performance coverage remain pending. The initial unexplained close
must not be presented as a verified memory-crash fix.
