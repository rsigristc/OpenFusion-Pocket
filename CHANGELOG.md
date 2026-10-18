# Changelog

## v1.5 — DS Companion

Changes since the published v1.0 (build 2):

- **DS Companion:** optional dual-screen support for compatible physical dual-screen devices and unfolded foldables, with Automatic / Single Screen / Dual Screen preferences. Conventional phones and tablets keep the full single-screen interface. Supported fold/unfold and display transitions retain the game session.
- **Map:** a companion map with original game icons, local/world views, filters, pan, zoom and live position, plus Nano Boost, Attack Boost and Taros counters.
- **Nanos:** an animated equipped-Nano panel and collection, real energy bars and assigned-power icons, tap-to-summon, and a compatible Gumball shortcut. Nano Station remains visible on the main screen while the companion supports server-validated equipment and power changes, including resource requirements and progress bars.
- **Equipment:** character preview with touch rotation, equipped slots, inventory, live stats and guide portrait. Selecting an item opens the existing Legacy-style details/actions on the main screen.
- **Missions:** categorized active/completed lists with real objectives, progress, NPC information and rewards; tracking and confirmed abandonment use the existing journal and server acknowledgements.
- **Gameplay fixes:** corrected combined-item appearance from Croc Pot, restored the Legacy-style death/respawn flow and server-provided respawn location, and improved Nano conditions, cooldowns, skill results and combat-effect handling.
- **Stability and presentation:** improved DS session/input handling, remote-player model recovery, portrait rendering and icon transparency; corrected My Stuff alignment and an idle-grounding loop that caused camera shake.

Chat and E-Mail still open their existing game interfaces; dedicated native companion panels are not included yet. DS depends on usable Android fold/display/touch capabilities and falls back to single screen when support is unavailable. Retrobution remains the main testing target; full device, server, tutorial and mission-playthrough coverage is ongoing.

Android package: `com.openfusion.android`. Version: **1.5**, build code **3**. Install over a matching v1.0 installation to retain app data and cached resources. The GitHub updater uses the release notes, `update.json` and SHA-256-verified APK.

## v1.0 — Native Android client

Initial public native Unity Android release, followed by build 2 adding the GitHub project link, release-note changelog, verified APK downloads and Android update installation. See the [v1.0 release](https://github.com/rsigristc/OpenFusion-Pocket/releases/tag/v1.0) for its original notes.
