# OpenFusionLauncher 2.3.2 alignment (local preview)

> Historical implementation/testing notes. The earlier local-only publication restriction was superseded by the explicit authorization to publish v2.0. See [v2.0 release notes](releases/v2.0.md) for current scope.


Upstream inspected at tag `2.3.2`, commit `ea5ce585071be842a2c6934cdf70c6f39f805ad5` on 2026-10-24. Pocket remains its own Android application/version; this is functional alignment, not a replacement with the desktop executable.

Sources:

- [2.3.2 release](https://github.com/OpenFusionProject/OpenFusionLauncher/releases/tag/2.3.2)
- [2.3.0 release](https://github.com/OpenFusionProject/OpenFusionLauncher/releases/tag/2.3.0)
- [2.3.1 release](https://github.com/OpenFusionProject/OpenFusionLauncher/releases/tag/2.3.1)
- [Protocol parsing](https://github.com/OpenFusionProject/OpenFusionLauncher/blob/2.3.2/src-tauri/src/util.rs)
- [About UI](https://github.com/OpenFusionProject/OpenFusionLauncher/blob/2.3.2/app/components/AboutModal.tsx)

## Adapted

- Android registers `ofl://server` links with the existing single-task Unity activity. Cold starts and deliveries to the running activity use Unity's deep-link event. The launcher decodes the upstream form-encoded `endpoint` and `description` fields, including API subpaths, spaces and explicit HTTP/ports.
- A new endpoint opens the existing Add Server form for review. Existing endpoints are selected using canonical endpoint identity. Links do not automatically connect or save servers. Pending links wait while a game session, settings page, modal or authentication request is active; they are processed on return to the launcher. Only the latest pending link is retained.
- Unknown commands, duplicate parameters, embedded credentials, control characters and non-HTTP(S) endpoints are rejected. Simple Server links report the existing limitation: Pocket requires an API endpoint for asset downloads. They do not start an unusable session.
- About shows the actual Unity runtime, process bitness, renderer and upstream feature reference separately from Pocket's release version.
- Collect diagnostics opens Android's share chooser with a bounded text report. It includes runtime/GPU/window/memory information and up to 20 recent managed error stack summaries. Log messages, paths, server addresses, credentials, account files and packet logs are excluded. It does not send anything until the user chooses a destination. Desktop/editor writes a report in the application's persistent data directory.
- Endpoint validation/storage now preserves explicit HTTP and ports. Previously the Add Server form stripped the HTTP scheme and subsequently connected over HTTPS.

## Already present or platform-specific

- Pocket already uses an ARM64 Unity 2022 Android runtime and direct HTTP(S) asset fetching with temporary files, length/hash verification and bounded parallelism. The upstream Unity 4 Windows 64-bit Web Player, DLL packaging fix in 2.3.2, Discord IPC leak and Windows shutdown/texture fixes cannot be applied as Android DLL updates. No desktop FPS increase is claimed for Pocket.
- The upstream 26 built-in version names and the two public API defaults match the existing Pocket catalog/defaults. Registration's email verification notice is already implemented.
- Wine/Proton profiles, Windows signing, desktop process launching, cache proxy and desktop Discord Rich Presence do not have corresponding Android components.
- Pocket keeps its existing shared/decoded Android asset cache and manual cache management. Upstream's automatic removal of obsolete desktop engine caches is not copied onto the different Android cache layout. No user caches are deleted by this update.
- Pocket updater continues to track OpenFusion-Pocket APK releases, not upstream Windows executables. DS, touch profiles, encrypted password storage, gamepad navigation and Vulkan/OpenGL selection remain part of Pocket.
- Diagnostics are intentionally narrower than upstream's full logs/crash-dump archive: no native tombstone export, previous-process crash history or raw runtime logs are included.

## Validation

`Launcher232RegressionChecks` passed with the existing build checks: protocol decoding, malformed links, unsupported Simple links, endpoint canonicalization, session/modal deferral, Add Server confirmation, diagnostic redaction and repeated Gradle manifest processing. All configured regressions and the Android build passed in `Logs/launcher-232-final.log`.

Installed successfully on AynThor over ADB. Android resolved a cold `ofl://server?endpoint=api.ffretrobution.net` launch to `PocketActivity`; a second VIEW intent was delivered to the existing activity. The final manifest contains the BROWSABLE intent filter and singleTask activity. The device subsequently displayed character selection; final visual checks of Add Server/About/share chooser await the user returning to the launcher. The last pending test link points to the existing Retrobution server; no test server was saved. Active-session deferral is verified by the automated state test; a complete live return-to-launcher cycle is still pending.

Local APK: `Builds/OpenFusion-Pocket-controls-preview.apk`, 116529815 bytes, SHA256 `00870C23A9FEF03FEFDB53C2673A8C636CE89751CEB2EF72B3D1245DBE955129`. Nothing is published to GitHub.

## Follow-up: 2.3.0 engine/FPS review

The upstream 2.3.0 performance announcement compares its former 32-bit Web Player with a desktop Unity 4 64-bit port. The reported 50–100% FPS increase belongs to upstream's test configuration, not to Pocket or to all 64-bit applications. Changing bitness alone does not establish a particular FPS gain.

Pocket's project uses Unity `2022.3.62f3`; its release build configures IL2CPP Release and includes ARM64 alongside ARMv7 for older devices. ADB inspection of the installed AynThor package confirmed `primaryCpuAbi=arm64-v8a`, `secondaryCpuAbi=null`. It already uses the native 64-bit path on this device and directly downloads HTTP(S) assets without the old desktop proxy. The extra address-space and native 64-bit runtime prerequisites are already covered. No desktop engine DLL, architecture selector or speculative FPS setting was added. This review does not claim that all mobile CPU/GPU bottlenecks are eliminated; further FPS improvements require measurement on the actual maps and device.

The remaining 2.3.0 features were checked against the previous alignment: endpoint deep links, single-task delivery, engine information and bounded diagnostics are adapted; Wine/Proton, desktop Discord IPC, Windows signing and separate desktop runner exit codes have no equivalent component here. The different Android cache layout keeps manual cleanup to preserve reusable bundles. No cache deletion or performance-default change was made for this follow-up.
