# OpenFusion:Pocket

**A native Android client for FusionFall, rebuilt in Unity 2022.3 LTS.**

[![Download v2.1](https://img.shields.io/badge/Download-v2.1-00bcd4)](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/v2.1)
[![Support Me on Ko-fi](https://img.shields.io/badge/Support_Me-Ko--fi-ff5e5b)](https://ko-fi.com/rodrigosigrist)

OpenFusion:Pocket is an unofficial community preservation project focused on making FusionFall more convenient to play on Android. Development and device testing currently focus on **FusionFall Retrobution**.

Version **2.1** improves mission progression and teleport placement, restores NPC models after delayed resource loading, and adds Legacy's Nano acquisition presentation and initial-power recovery. See the [bilingual release notes](docs/releases/v2.1.md), including the remaining live Nano-selection check.

The project reconstructs the legacy client's resource loading, world rendering, interface, input and server communication in a newer Unity runtime. It runs as an Android application without running the Windows client through Wine or Winlator. It does not compile the original client or recompile Unity on your phone.

This repository contains the Unity project used for this native client. The earlier [OpenFusion Android project](https://github.com/rsigristc/OpenFusion_Android) used a Winlator-based approach and is maintained separately.

## Download and install

1. Open [Releases](https://github.com/rsigristc/OpenFusion-Pocket/releases).
2. Download **OpenFusion-Pocket-v2.1.apk** from **OpenFusion:Pocket v2.1**.
3. Allow installation from your browser or file manager when Android asks, then install the APK.
4. Open the app, choose a server and sign in with an account for that server. An internet connection is required.

The build targets **Android 6.0 / API 23 or newer**, with ARMv7 and ARM64 included. These are build requirements, not a guarantee of performance or compatibility on every device. Initial game-resource downloads require additional storage and time; Wi-Fi is recommended. Server availability and account registration are managed by each server's operators.

To update an existing installation signed with the same certificate, install over it to retain data. A different signing certificate requires a separate migration; do not delete app data just to troubleshoot a download or loading issue.

## What is included

- Native Android execution through Unity 2022.3 LTS and IL2CPP.
- Server selection, version information and staged game-resource downloads.
- Automatic checks for GitHub releases, release-note changelogs and verified in-app APK downloads with Android installation confirmation.
- Persistent resource caching with integrity checks and a bounded decoded cache to reduce repeated decompression.
- Reconstruction of maps, terrain, characters, equipment, textures and animations from legacy Unity resource formats.
- Adapted materials, particles, lighting and environmental audio.
- Configurable touch buttons for movement, camera, combat and other actions.
- Gamepad support, launcher focus navigation and an in-game UI cursor; touch controls can hide while a controller is connected.
- A touch layout editor, saved profiles and an optional controller-style layout.
- Opt-in password encryption using Android Keystore.
- Experimental Vulkan selection (OpenGL ES is the default), server deep links and shareable diagnostics.
- Retrobution 3.0.3 Halloween ambience/audio and NPC cosmetics.
- Rebindable inputs, clearing bindings, conflict handling and cancellation.
- Automatic stacking of eligible inventory items and inventory sorting, subject to server confirmation.
- Graphics options including render resolution, anti-aliasing and frame-rate controls.

These systems remain under active development. A feature being implemented does not mean every map, weapon, effect or server variation has been fully tested.

## DS Companion

Version **2.1** includes optional companion panels for **Map, Nanos, Equipment, Missions and E-Mail** on compatible physical dual-screen Android devices and unfolded foldables. They use the existing game state, original icons and server-validated actions. E-Mail supports Guide/Player folders, unread indicators and received attachments; composing/replying uses the original interface. Chat opens the original game interface.

Choose **Automatic / Single Screen / Dual Screen** in Dual Screen settings, available in launcher Game Settings and in-game Settings. Ordinary phones and tablets retain the full single-screen interface. An external display alone is insufficient: physical secondary screens need usable dimensions and a touch confirmation in settings. Android fold/window evidence and usable space determine foldable support; folding or losing the display restores the normal interface without restarting the session.

Device testing includes an AynThor and a Galaxy Z Fold. Support depends on Android display/posture/input reporting; this does not guarantee every dual-screen device or foldable. See the [changelog](CHANGELOG.md) and [DS implementation notes](docs/DUAL_SCREEN.md).

## How resource loading works

The APK contains the Android runtime, client code, shaders and embedded interface resources. The much larger game-content bundles are obtained from the selected server's resource endpoints and retained in the app's cache. Subsequent sessions reuse valid cached resources.

The app reads legacy Unity containers, serialized objects and packed game tables, then builds modern Unity meshes, textures, animation clips, terrain and game objects at runtime. Old materials and effects are mapped to implementations in the current rendering pipeline.

**The APK is not a complete offline copy of FusionFall.** Total storage use will grow as game resources are downloaded. Some original interface graphics, portraits, bitmap-font data and launcher artwork are embedded in this repository and the APK; the project does not claim ownership of those resources.

## Development and AI transparency

AI played a substantial role in development, including analyzing legacy formats and behavior, implementing code and troubleshooting issues. I direct the project, integrate changes, test builds on hardware and work through the resulting issues. OpenAI Codex has been used extensively for technical assistance.

This is a combination of human direction, testing and persistence with substantial AI assistance. It is not a claim that the implementation is flawless or independently certified. The source is published so others can inspect it, report problems and contribute improvements.

The motivation is to preserve access to a game many of us care about and offer another comfortable way to revisit it. We respect that people have different views about AI-assisted projects; using or supporting this one is entirely optional.

The opening cinematic and guided local Tech Square practice are adapted from Legacy, with a condensed cinematic presentation. See the [v2.0 notes](docs/releases/v2.0.md) for scope and remaining device checks.

## Current limitations

Retrobution is the primary tested server. OpenFusion Public entries may appear in the launcher, but complete compatibility with every legacy client version or custom server is not established.

Visual fidelity, loading time, memory use and input behavior can vary by device. Full playthrough coverage of every infected zone and Fusion lair is still pending. Recent fixes cover destination-map loading, instance-state cleanup and environmental damage/healing; further device testing is welcome.

The latest asset audit found 20 unresolved target references in the source resources. These do not by themselves establish 20 visible missing objects; identifying which references are actually used remains part of the work. Performance on modest hardware is a goal, not a universal performance guarantee.

## Build and contribute

Use **Unity 2022.3.62f3** with Android Build Support, SDK, NDK and OpenJDK. See [Building from source](docs/BUILDING.md), [Contributing](CONTRIBUTING.md) and [Reporting security issues](SECURITY.md).

The launcher's **About** screen links directly to this repository. **Updates** checks the latest stable GitHub release and shows its changelog. You choose when to download and install; Android may ask you to allow installations from this app. See [Updater and release maintenance](docs/UPDATER.md) for the update manifest and future publishing workflow. The v2.1 APK uses Android build code **5**, following v2.0 build **4**, v1.5 build **3** and the updater-enabled v1.0 build **2**.

- `Assets/Scripts/OpenFusion/`: legacy readers, networking, launcher and gameplay systems.
- `Assets/Shaders/`: rendering adaptations.
- `Assets/Resources/OpenFusion/`: embedded interface resources and supporting data.
- `Assets/Editor/`: project setup, validation, regression checks and Android build tools.
- `Packages/` and `ProjectSettings/`: Unity dependencies and project configuration.
- `tests/` and `tools/`: standalone checks and extraction/development utilities. Some require separately obtained game fixtures.

## Credits

The original FusionFall developers, OpenFusion contributors, Retrobution team and community preservation contributors provided the foundation that makes this project possible. This Android adaptation does not replace or take credit for their work.

See [CREDITS.md](CREDITS.md) and [NOTICE](NOTICE) for attribution and the distinction between original project contributions, upstream material and game resources. Original contributions are offered under the [MIT license](LICENSE); that license does not relicense third-party code, game assets, font data or trademarks.

This is an unofficial project and is not affiliated with or endorsed by Cartoon Network, the OpenFusion team, the Retrobution team or Unity Technologies.

## Support Me

If you would like to support development and device testing, you can contribute through **[Ko-fi: Rodrigo Sigrist](https://ko-fi.com/rodrigosigrist)**.

[![Support Me on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/rodrigosigrist)

Support is optional. Downloads and project features are not tied to donations. Testing on different devices, reporting reproducible bugs, reviewing code and contributing fixes also help enormously.
