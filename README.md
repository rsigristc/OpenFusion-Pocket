# OpenFusion:Pocket

**A native Android client for FusionFall, rebuilt in Unity 2022.3 LTS.**

[![Download V1.0](https://img.shields.io/badge/Download-V1.0-00bcd4)](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/v1.0)
[![Support Me on Ko-fi](https://img.shields.io/badge/Support_Me-Ko--fi-ff5e5b)](https://ko-fi.com/rodrigosigrist)

OpenFusion:Pocket is an unofficial community preservation project focused on making FusionFall more convenient to play on Android. Development and device testing currently focus on **FusionFall Retrobution**.

The project reconstructs the legacy client's resource loading, world rendering, interface, input and server communication in a newer Unity runtime. It runs as an Android application without running the Windows client through Wine or Winlator. It does not compile the original client or recompile Unity on your phone.

This repository contains the Unity project used for this native client. The earlier [OpenFusion Android project](https://github.com/rsigristc/OpenFusion_Android) used a Winlator-based approach and is maintained separately.

## Download and install

1. Open [Releases](https://github.com/rsigristc/OpenFusion-Pocket/releases).
2. Download **OpenFusion-Pocket-v1.0.apk** from **OpenFusion:Pocket V1.0**.
3. Allow installation from your browser or file manager when Android asks, then install the APK.
4. Open the app, choose a server and sign in with an account for that server. An internet connection is required.

The build targets **Android 6.0 / API 23 or newer**, with ARMv7 and ARM64 included. These are build requirements, not a guarantee of performance or compatibility on every device. Initial game-resource downloads require additional storage and time; Wi-Fi is recommended. Server availability and account registration are managed by each server's operators.

To update an existing installation signed with the same certificate, install over it to retain data. A different signing certificate requires a separate migration; do not delete app data just to troubleshoot a download or loading issue.

## What is included

- Native Android execution through Unity 2022.3 LTS and IL2CPP.
- Server selection, version information and staged game-resource downloads.
- Persistent resource caching with integrity checks and a bounded decoded cache to reduce repeated decompression.
- Reconstruction of maps, terrain, characters, equipment, textures and animations from legacy Unity resource formats.
- Adapted materials, particles, lighting and environmental audio.
- Configurable touch buttons for movement, camera, combat and other actions.
- Gamepad support, with an option to hide touch controls while a controller is connected.
- Rebindable inputs, clearing bindings, conflict handling and cancellation.
- Automatic stacking of eligible inventory items and inventory sorting, subject to server confirmation.
- Graphics options including render resolution, anti-aliasing and frame-rate controls.

These systems remain under active development. A feature being implemented does not mean every map, weapon, effect or server variation has been fully tested.

## How resource loading works

The APK contains the Android runtime, client code, shaders and embedded interface resources. The much larger game-content bundles are obtained from the selected server's resource endpoints and retained in the app's cache. Subsequent sessions reuse valid cached resources.

The app reads legacy Unity containers, serialized objects and packed game tables, then builds modern Unity meshes, textures, animation clips, terrain and game objects at runtime. Old materials and effects are mapped to implementations in the current rendering pipeline.

**The APK is not a complete offline copy of FusionFall.** Total storage use will grow as game resources are downloaded. Some original interface graphics, portraits, bitmap-font data and launcher artwork are embedded in this repository and the APK; the project does not claim ownership of those resources.

## Development and AI transparency

AI played a substantial role in development, including analyzing legacy formats and behavior, implementing code and troubleshooting issues. I directed the project, integrate changes, tests builds on hardware and works through the resulting issues. OpenAI Codex has been used extensively for technical assistance.

This is a combination of human direction, testing and persistence with substantial AI assistance. It is not a claim that the implementation is flawless or independently certified. The source is published so others can inspect it, report problems and contribute improvements.

The motivation is to preserve access to a game many of us care about and offer another comfortable way to revisit it. We respect that people have different views about AI-assisted projects; using or supporting this one is entirely optional.

## Current limitations

Retrobution is the primary tested server. OpenFusion Public entries may appear in the launcher, but complete compatibility with every legacy client version or custom server is not established.

Visual fidelity, loading time, memory use and input behavior can vary by device. Full playthrough coverage of every infected zone and Fusion lair is still pending. Recent fixes cover destination-map loading, instance-state cleanup and environmental damage/healing; further device testing is welcome.

The latest asset audit found 20 unresolved target references in the source resources. These do not by themselves establish 20 visible missing objects; identifying which references are actually used remains part of the work. Performance on modest hardware is a goal, not a universal performance guarantee.

## Build and contribute

Use **Unity 2022.3.62f3** with Android Build Support, SDK, NDK and OpenJDK. See [Building from source](docs/BUILDING.md), [Contributing](CONTRIBUTING.md) and [Reporting security issues](SECURITY.md).

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
