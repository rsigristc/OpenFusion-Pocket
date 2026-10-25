# Retrobution 3.0.3 — Halloween 2026 Android compatibility

> Historical implementation/testing notes. The earlier local-only publication restriction was superseded by the explicit authorization to publish v2.0. See [v2.0 release notes](releases/v2.0.md) for current scope.


Reviewed against the live API and the code/data shipped in the current legacy `main.unity3d`. No legacy DLL is loaded or executed by Pocket; the relevant behavior is implemented in the existing Unity 2022 Android client.

## Sources and manifest comparison

- API: `https://api.ffretrobution.net/`
- Current manifest: `https://api.ffretrobution.net/versions/20c02df0-850a-4077-8895-ed5b2500a859.json`
- Version: `Retrobution 3.0.3`; parent `238bd38f-3add-4dc5-a29a-06213461a206` (`retrobution-20261014-2`).
- CDN: `http://cdn.ffretrobution.net/retrobution-20261024`.
- Main SHA256: `cd2450834550f9240271a73c8163f701132b8b3e119aa575cf88473f94828aa2`.
- Of 360 bundles, the changed hashes relative to the parent are `TableData.resourceFile` and `CharacterSelection.resourceFile`. The main file also changed. Existing hash-based cache reuse already selects current files and shares unchanged parent bundles; no full cache deletion or hardcoded version pin is required.
- Client reference: `GameEventManager`, `HalloweenEvent`, `NpcCustomization`, `HatOption` and `VisibleNpc` extracted from this main file. Original shader programs were compared with `tools/shader_sources/Custom_*Rim*.shader`.

## Implemented

`SeasonalEvents` reads the real `themedEvent.php` response, preserving API subpaths and using the CDN directory as fallback. The live response was `halloween,eventsound,`. The device date is not used. The request runs alongside normal loading and does not delay character/map entry. Polling every 60 seconds during an active world session supports server-driven activation/deactivation. A failed refresh preserves the last confirmed state for that session; a fresh server configuration starts with no event.

The Halloween ambience uses the 3.0.3 values: purple fog `(white*0.01 + (120,0,255)*0.00035)*0.8`, light `(0.5,0.5,0.6)`, and green rim `#68BD3E * 0.45`. Fog density is `depth*0.0165` for free/tutorial maps and `depth*0.007` for paid maps. Existing fog controls remain effective. Normal per-dong values return when the event is disabled.

The Toon shader/binder now supports the original Fresnel rim power/intensity, rim emission strength and transparency for the Custom Rim material families. The environment's rim color is global; ordinary materials keep the existing path. No additional runtime camera or rendering surface is introduced. Character selection/creation set their original blue/neutral rim colors so the world event does not leak into those stages. Unrelated Matcap/overlay fidelity is not claimed as newly implemented.

The sky uses the actual `TutorialAssets/sky_night_back.psd`; the Fusion Star/moon uses `TutorialAssets/fusionstar-1.dds`. Original sky textures return when the event ends.

When `eventsound` is enabled, normal world music/ambience route to `Halloween.ogg` and `Ambient Halloween.ogg`, respecting silence, instance tracks, Main Theme and IceSpire exclusions. Seasonal replay delay is capped at 120 seconds, matching GameEventManager. Lobby playback and explicit cinematic/racing overrides are retained. The Fusion Matter fatigue overlay receives the original orange seasonal tint.

NPC cosmetics use the five 3.0.3 options and exact attachment offsets/scales/rotations: Enid, EEnE Viking, Black Hat Top Hat, Elder Flair and Devil Horns. Billy Disguise is absent. Sneaky/Fusion Spawn models choose a cosmetic deterministically by instance ID to avoid rerolling on retries; this is a local cosmetic choice and does not affect server state. Warp Frog models always use Black Hat's Top Hat on `Head03`. Tail hiding is applied per instance without editing shared animation clips. Event changes update already tracked NPCs; hats are removed and original tail scales restored when disabled. Missing streamed assets retry without blocking NPC spawning. Hat materials are private and released with the attachment.

## Assets, rewards and platform limits

The live item tables contain Mystery Mech Torso (type 1/id 675), Leggings (2/575), Boots (3/570), Helmet (4/373) and Wings (6/176). Their male/female model and texture paths resolve through Pocket's real resource router against hash-verified current CDN bundles. Existing equipment/inventory code consumes these item records; no alternate reward system is needed.

Pumpkin C.R.A.T.E. availability, loot selection, drop rates, ownership and rewards remain server-authoritative. The app neither grants items nor changes drop probabilities. Tests do not open C.R.A.T.E.s, spend resources or modify a live character.

The update's Windows Unity 4 compatibility changes do not replace Pocket's native ARM64 Unity 2022/IL2CPP runtime. Only applicable game behavior and asset compatibility are adapted. The seasonal controller currently implements Halloween; it does not claim a complete port of every historical event such as Knishmas.

## Validation

`Retrobution303RegressionChecks` covers endpoint parsing and fallback paths, exact rim/fog values, audio exclusions, returning to ordinary ambience, the five-hat rotation, frog restriction, shader material bindings, current asset resolution, each actual Spawn hat and the Frog attachment, duplicate prevention and disable/re-enable/tail restoration. It also verifies the five Mystery Mech records and their model/texture paths. The build workflow runs this alongside the existing input, DS, tutorial, combat, respawn, camera, terrain, connection, startup and updater regressions.

`Retrobution303VisualProof.Batch` also runs in Unity Play Mode with the real NPC animations, current decoded models/textures and the URP preview renderer. Captures verified all five Spawn hats and the Warp Frog Top Hat. A GPU comparison with the same material and camera found 53,799 pixels with an increased green contribution when enabling the seasonal rim. The offscreen test warms its material/shader bindings before reading pixels; no special texture conversion or cosmetic-specific shader workaround is used in the runtime.

Logs: `Logs/retrobution-303/regression-results.txt`, `Logs/retrobution-303/visual-final.log`, `Logs/retrobution-303/build-final.log`. Captures: `Logs/retrobution-303/model-0.png` through `model-5.png`, `rim-off.png`, `rim-green.png`.

Physical Android verification of the final APK remains pending: especially a live Catacombs visit, seasonal music playback and event deactivation during a session. Editor asset/render checks do not establish device performance or confirm server loot distribution. No GitHub publication is authorized or performed.

Final local APK: `Builds/OpenFusion-Pocket-retrobution-303-preview.apk` (ARM64, IL2CPP). Unity batch build finished with `Result: Success`; the complete configured regression suite passed.
