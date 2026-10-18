# DS Companion — v1.5

DS Companion is included in v1.5 following explicit release authorization. Map, Nanos, Equipment and Missions use live game data; Chat and E-Mail use the original windows. The following sections retain the incremental implementation and validation history. Earlier notes saying local-only or pending describe those stages, rather than the current release status.

## Architecture and constraints

The app is a native Unity 2022.3 client, with URP 14, GLES3 and IL2CPP. It downloads the selected server's client assets, decodes legacy Unity data and reconstructs the world. It does not run the original executable or recompile arbitrary downloaded scripts into Android code.

Most game UI is immediate-mode IMGUI, with coordinates derived from `Screen.width/height`. It cannot simply be moved between Android displays like a Canvas. The game camera, projected world labels, touch controls and HUD must continue to agree about the size of the Unity surface.

The DS bridge therefore resizes the **actual UnityPlayer view** on a compatible fold and uses an Android view for the secondary map. Separate physical displays use Android `Presentation`; they do not add another 3D camera or activate Unity multi-display input. The same native map presenter is used in both configurations. Its commands are delivered once to Unity's main thread. Native secondary touches are consumed by Android and do not become attack/look input.

Client-side: layout, capability selection, map presentation, filters, zoom, local waypoints, input routing. Server-side: authoritative combat, inventory changes, Nano activation and energy, mission progression, chat delivery and player email/attachments. External: Android window/fold information, display availability and touch routing, IME, downloaded map/icon assets, server protocol support. DS does not extend server capabilities.

## Capability rules

- Default preference: **Automatic**. **Single Screen** always wins. **Dual Screen** never overrides an unsupported configuration.
- Screen size, brand/model, aspect ratio and the existence of an external monitor are insufficient evidence.
- Current Jetpack WindowManager `FoldingFeature` evidence is required for the fold path. A folded cover display or missing/uncertain feature falls back to Single.
- A flat, non-occluding fold permits a wide game above the secondary pane. A separating/occluding hinge is avoided; useful horizontal half-fold postures may work. Narrow vertical panes are rejected.
- Both panes must meet usable size thresholds: primary at least 480×270 dp and width/height ≥ 1.2; secondary at least 320×300 dp. Insets/cutouts are excluded from a fold layout. Portrait autorotation is allowed only while a compatible fold is present; the previous rotation permissions return on fallback. Launcher, login, loading, modal windows and active chat retain the ordinary full-screen interface.
- Presentation displays must pass a touch confirmation **on the actual second display** for the current connection. The test is initiated in DS settings, expires after 15 seconds and rejects mouse-only confirmation. Disconnecting removes confirmation. Mirrored/undersized/non-touch/unverified displays are not automatically enabled.
- The native view hierarchy must support safe resizing. Bridge/platform failures restore Single Screen.

Settings are accessible from launcher Game Settings and the in-game Settings footer. Preferences persist separately from legacy graphics options.

## First integration

- Live map textures, player position/heading, mission and NPC markers, existing filters, local/world view, pan and zoom use `WorldMapWindow` state. A texture missing from the downloaded client is reported; no mock map is substituted.
- Boost counters, the three equipped Nano names, current energy and activation use `PlayerHud.Data` and `NanoWheelHud.RequestSlot`. Server validation/cooldowns still apply.
- Map and Nanos have secondary tabs. Equipment, journal, chat and email shortcuts open the existing full-screen panels; the arrow indicates this behavior. Those remaining panels are not docked yet.
- A fold/display transition does not reload a scene, reconnect, close a modal, clear a chat draft or select another mission/mail. Map zoom/pan/filter state survives hiding/recreating the native view. Touch state and cached dp geometry are reset when the surface changes.
- Map snapshots are limited to four updates per second. Texture readbacks occur only on first use of each texture, capped at 1024 pixels; Android decodes images off its UI thread and bounds its bitmap cache. There is no recurring world screenshot/readback.

## Incremental work

1. Validate surface sizing, posture transitions, safe areas, simultaneous primary/secondary touches and hot-unplug on actual fold and independent-touch hardware. Confirm Unity does not override its native view size on those players.
2. Nano collection is now docked as described below; validate live station acknowledgements and controller behavior on hardware before proceeding to more panels.
3. Extract presentation interfaces from inventory and journal before docking them. Preserve selected slot/task and all pending server operations.
4. Adapt chat and email last, including per-display keyboard focus, drafts, channels, guide mail and actual protocol-supported attachments. Do not assume that a non-focusable `Presentation` can own an IME.

Native DS styling is a functional prototype, not the mockup artwork. The existing UI remains the complete fallback throughout.

## Validation

`DualScreenRegressionChecks` covers conservative capability selection, forced Single, unknown/cover/conventional/external-unverified rejection, useful fold and verified physical acceptance, insufficient space, modal fallback, repeated disconnect transitions, real shared HUD counters and map pan/zoom state, and clearing touch movement.

These checks do not prove OEM posture reporting, native surface sizing or independent physical touch routing. Compatible hardware and a real logged-in session remain necessary for those tests. Run the existing input, controller startup, combat, world, sound, connection, Android, startup, terrain and updater checks as well. Development APKs remain local previews unless a release is explicitly authorized.

The real native pane geometry is independently exercised by `tools/DualScreenLayoutChecks.java`: flat and separating folds, conventional and narrow screens, missing evidence, invalid metrics, off-window/partial hinges, insets, multiwindow and 100 successive feature-loss/restoration cycles. Compile it together with `DualScreenLayout.java` using a host JDK; this does not emulate Android rendering or touch.

Platform references: [Android fold information](https://developer.android.com/develop/adaptive-apps/guides/foldables/make-your-app-fold-aware), [WindowManager](https://developer.android.com/jetpack/androidx/releases/window), [Presentation](https://developer.android.com/reference/android/app/Presentation).

### Hardware session, 2026-10-17

- Local preview installed with `adb install -r` on the user's Samsung Z Fold (SM-F971B, API 37); no data clear or uninstall.
- Android reported `CLOSED` with no current fold feature and a 1972×1248 game surface: DS remained off. `OPENED` produced a reliable flat, nonseparating feature and a 1848×2448 window. The Unity surface actually resized to 1848×1039 while the native live map occupied the secondary area.
- The user confirmed fold/unfold with the session preserved and the map returning. ADB confirmed the same process through fallback/restoration. A captured frame shows real Sector V textures and NPC/mission/player icons; Nano boost 4858, attack boost 4324 and equipped Nano energies match the original HUD.
- All existing build regression suites and the DS policy/shared-state suite passed. Native Java layout tests additionally cover the device's fractional density (2.625): minimum dp sizes must round **up** to pixels, otherwise a valid landscape pane can be rejected by less than one pixel. The native module was rebuilt after that correction; this did not change the Unity runtime assembly.
- The corrected APK was reinstalled and opened. On real hardware the landscape window 2448×1848 now reports `available=true`, primary 2448×916 and secondary 2448×788; portrait remains usable. The brief rotation test restored the device's original `free` rotation preference. Landscape capability was verified after the correction; a prolonged landscape gameplay session was not.
- A separate independent physical touch display has not been tested. Half-fold OEM behavior, secondary keyboards, prolonged sessions and the remaining docked panels still need validation/work. This is a bounded functional prototype, not completion of every panel in the mockups.

Local build entry point: `OpenFusion.EditorTools.OpenFusionBuildRequest.BatchDS`; output `Builds/OpenFusion-Pocket-DS-preview.apk`. The native module can also be recompiled with `:launcher:assembleRelease` in Unity's generated Gradle project after copying the changed Java sources. The public release and update manifest are unchanged.

## Nano panel increment

The secondary **Nanos** tab now uses `InventoryWindow.DualNanos` over the same bank ownership, chosen skills, tables, inventory items and HUD energy as the original inventory. It lists owned/locked Nanos using the existing 2D icons, shows the selected Nano's descriptions and powers, marks its equipped slot/current power and reports real equipped-slot cooldowns. Missing bank energy is labelled unavailable rather than presented as an invented zero. No extra character/Nano preview camera is created.

Summoning remains available from the persistent three-slot bar during ordinary gameplay and reuses `NanoWheelHud.RequestSlot`. Equipment and power changes remain **Nano Station operations**, as in the original UI. Opening a station keeps `GameMenu.Current=MyStuff` and `Machine=true`: movement, combat and ordinary shortcuts stay blocked. On compatible DS layouts, the original Nano Station window renders on the primary Unity surface while the secondary map/collection remains visible. On Single Screen or after a fold/disconnect, the same station renders on the full Unity surface. It is never suppressed by the secondary collection.

Equipment/unequipment call the existing packet methods and reject locked Nanos, active replacement slots, invalid slots and concurrent requests. Tuning uses the same requirements helper in both the original and DS UI, validates the actual inventory item slots, and shows an inline resource confirmation before sending. The existing packet handlers remain authoritative: no optimistic equipment, power or currency changes. Inline confirmations cancel when the native view is destroyed; they never spend resources automatically after a fold.

The controller retains the tab, selected Nano and collection scroll outside the native view. The original station popup receives the selection on fallback. DS hides the primary Nano wheel, its duplicate counters and optional Nano slot touch buttons; their normal rendering/preferences return in Single Screen. Primary HP, combat/movement, active Nano info, boost/power controls and gamepad bindings are retained. Native secondary widgets cannot take keyboard/gamepad focus away from Unity.

`DualNanoRegressionChecks` covers shared ownership/locks, energy availability, power selection, station gating and blocked gameplay, equip/unequip/tune packets, required resources/item slots, rejecting duplicate sends, waiting for authoritative acknowledgements, selection transfer and restoring Single Screen touch preferences. Live station equipment/tuning and gamepad behavior must be distinguished from these fixture tests in any validation report. Power changes that consume resources are not performed automatically during device QA.

### Local input follow-up (2026-10-17)

The Fold feedback reported unresponsive station and summon controls. A live capture of the earlier APK subsequently showed the station state and real equipment actions in the secondary panel; this alone does not validate sending those actions. The station open path now explicitly selects the Nanos tab, independent of the next status poll. Native summon eligibility checks actual gameplay/modal/text state rather than a retained IMGUI keyboard control ID. It shares `RequestSlot` for stamina, vehicle and rate limits and stays disabled while the station is open. Close the station before summoning; selection in the collection is separate from summoning an equipped slot.

Command rejection logs include surface revision and modal state; summon logs distinguish missing wheel, blocked UI and a sent request. Native buttons/cells now use navy/cyan borders and lime selection instead of Android's stock gray backgrounds. This is lightweight native presentation, retaining original asset icons.

Local build `CD94C5ACCBDCA4E4D30E3E171F25A3F97D5E6A42F1CC5B3F598F986D5DA587DC` passed the configured regression suites and was installed with `adb install --user 0 -r` on the Fold. End-to-end device verification of this follow-up is pending login. No DS release or repository publication was performed.

Device evidence for the input follow-up is saved in `Logs/ds-nanos-device-qa.log`: process 2846 sent slot 0 and slot 2 summon requests and received the corresponding Nano spawns (Eduardo/Blossom) at 01:47; the station opened as MyStuff/Machine at 01:48:32; unequip/equip requests for Buttercup were followed by the live slot changing to Empty and back to Buttercup. A subsequent hierarchy dump showed all three summon buttons enabled after station close. The styled panel was visually inspected on the Fold. No power tuning was performed.

At 01:49:03 the shard reported `Network subsystem is down` after the app was backgrounded. Further requests were only queued locally: the pre-existing sender silently discarded them while the old DS UI remained enabled. The follow-up adds a shared read-only connection property, disables native summon/equipment/tuning while disconnected, displays a reconnect message, and rejects network actions again on the Unity side. Map inspection and closing the station remain local actions. This does not implement automatic shard reconnection; reconnect through the launcher after losing the session. Do not count requests after this disconnection as successful server actions.

Final local APK SHA-256: `32A1295171D87B1CE683A590C52224B8CA5D2D0BFADC4031E2C0941F929E3799` (93,329,645 bytes). All configured checks passed again. Installation succeeded on the same Fold after discovering its changed wireless connection port through ADB mDNS. The app was launched after installation. Connection-loss guard is compiled into this final APK; the earlier connected gameplay/station evidence above applies to the same input and presentation implementation. Resource-consuming tuning, physical secondary displays and a connected gamepad remain unverified on hardware. Automatic reconnect is not included.

### Original Nano Station window (2026-10-17)

User feedback clarified that the station itself must open normally, rather than be replaced by the secondary collection. Earlier task-switcher captures were misleading: losing window focus restored the original station, while returning to the foreground enabled DS and suppressed it again. The controller no longer exempts Nano Stations from modal fallback, and InventoryWindow no longer skips its original OnGUI window when DS was active. Queued secondary commands are rejected while any modal is open. The Nanos collection and summon bar remain secondary during ordinary gameplay; station operations use the existing full-screen UI. No station/network session is recreated by the layout change.

A regression opens the real InventoryWindow station, checks that it blocks DS even with compatible Fold capabilities, then closes it and checks that DS is eligible again. Existing station packet/resource checks remain for the shared operations; they do not imply the station is still presented natively in DS. Historical device observations above refer to the previous station presentation.

The station-modal fix compiled successfully with all configured regressions passing (`Logs/unity-ds-station-modal.log`); APK SHA-256 `937D9C6FB214CFB96F3A994CFC20DB950370B62B8E5C2BD313F5ACB7596B47F4`, 93,328,354 bytes. Installed on the Fold with user-0 replacement preserving data, then launched. Foreground station appearance and close-to-DS return still need live verification after login. No GitHub publication.

Live validation completed: the user confirmed both station/DS transitions now look correct. Process 25956 logged the station opening as MyStuff at 02:14:00 while DS stayed inactive with Unity 1848x2448; DS resumed at 02:14:05 with Unity 1848x1039 after closing. The same process/session was retained. Evidence: `Logs/ds-station-modal-qa.log`. This supersedes the pending live-verification note above.

### Nano Station on the upper screen (previous inspector-only revision)

At the user's request, the previous station-modal fallback is superseded: a Nano Station dialog (NPC types 7/8/9) and its original station window now remain on the primary Unity surface while DS stays active. InventoryWindow continues to draw the original window; it already fits/scales to the current Unity surface dimensions. No extra camera, scene reload or station/gameplay data is created. Ordinary incompatible/folded configurations still fall back to the full original window.

The secondary map/collection can be inspected while the station is open. An explicit command allowlist admits only local tab/selection/scroll/map operations in that state. Summoning, unrelated windows and native equip/unequip/tune/close requests are blocked; equipment/power edits belong to the original upper station UI. The collection snapshot marks itself as an inspector and disables all power changes, with a notice directing the user above. The controller preserves the previous secondary tab instead of switching automatically to Nanos. Other modal screens retain their existing fallback behavior.

The updated regression checks require DS eligibility with an open station, allow secondary inspection, reject interfering network/window commands, retain legacy movement/combat/summon guards, and allow ordinary DS again after closing.

Upper-station build passed the configured checks and was installed on the Fold preserving data, then launched. APK SHA-256 `7150C9DA3D6B79A4A7A6D481AC7E4C94C345DCC2A0DD220DBBC81FFBCC8AFC36` (93,328,355 bytes), build log `Logs/unity-ds-upper-station.log`. Live verification after login is pending; no resource-consuming operations will be used for QA. No publication.

Live Fold capture `Logs/ds-upper-station.png` confirms the original Nano Station, including Buttercup's power popup and its unequip button, fits on the upper Unity surface while the secondary collection remains visible. The lower inspector shows the upper-station notice and disables conflicting edits. Process 2584 kept DS revision 3 active at 1848x1039 across NPC dialog open at 02:26:11 and station open at 02:26:13; lower collection selections continued at 02:26:17-22 without changing that revision. Evidence: `Logs/ds-upper-station-qa.log`. No tuning/resource consumption was performed by the agent.

### Shared station editing and Nano card UI (current behavior)

The user requested editing from DS as well as the upper station. The controller now accepts native equip/unequip/tune and close commands only in an unblocked Nano Station, with no NPC dialog active. The inspection-only snapshot override has been removed. Both presentations use InventoryWindow's same resource validation, machineBusy flag, packets and authoritative handlers. A second request from either presentation is rejected until the server responds; no equipment, skill or currency is updated optimistically. Native tuning retains inline spending confirmation. World summon/unrelated-window restrictions and disconnected/stale-surface guards remain in place.

The native Nanos presentation is rebuilt toward the supplied mockup using code-native vector chrome and existing client icons: left equipped-slot cards with real style and energy bars, selected Nano details and station actions; right owned/locked collection with retained touch scroll; angled cyan frames and lime selection; vector boost counters and icon navigation. Narrow panes stack the two columns. The redundant text-only summon row is hidden on the Nanos tab, where the equipped cards are visible; Map retains its existing summon shortcuts. No mockup names, levels, currencies, abilities or ownership are invented. Locked icons use the client's assets; skill changes still require the actual materials and Fusion Matter shown in the requirements.

Native Java preflight (`Logs/ds-native-cards-build.log`) compiled successfully. Updated regression checks cover allowing DS station edits while retaining summon/window restrictions, shared duplicate-request prevention, packet/resource layouts, and the serialized live station/selection/power eligibility snapshot. Device visual and interaction QA follows the full Unity build.

Final native build (`Logs/ds-native-cards-final.log`) also preserves Summon/Dismiss from an equipped Nano's details outside the station; equipped cards select the details, with a separate active marker and lime selection. Full Unity checks passed before the final Java-only build. Installed on the Fold preserving data, then launched: SHA-256 `9922B8D8FA846D2977582799D5802F13DBADE863CCDEB4B2C7C9E0DF2D8F3E51`, 93,335,300 bytes. Live station/action/visual verification after login is pending. No publication.

Device process 18779 rendered the new two-column card UI with the three real equipped Nanos, style labels, energy bars, collection/locked assets, selected details and icon navigation. The observed game capture was visually checked. Station opening at 02:47:21 was followed by native equip requests for Wilt (id 6) to slot 0 at 02:47:25 and Eduardo (id 4) to slot 0 at 02:47:29, both accepted by the client shared validation (`sent=True`). Evidence: `Logs/ds-nano-cards-qa.log`. These logs distinguish accepted requests from explicit server acknowledgements; follow-up user feedback remains useful. Power changes were not performed by the agent because they consume resources; insufficient-material cases remain disabled as intended. No native panel exception/fatal was observed in this trace. Summon/Dismiss on the new detail panel still needs explicit live confirmation. No GitHub publication.

### Animated Nano models, card summon and Current Power ordering

DS reuses NanoModelRt's legacy model loading, stand/idle animation clips, framing and isolated preview camera. Only the three equipped models and an owned selected Nano are requested, never the full collection. A 512x128 atlas of up to four 128px previews is copied to Android as ARGB integer pixels: async GPU readback where supported, capped synchronous readback otherwise; no PNG/disk frame loop or second world camera. Rendering/capture is capped (up to 12 fps, 6 fps on the fallback/lower-memory path). Android holds two atlas bitmaps and coalesces pending frame deliveries, rejecting stale surface revisions. The existing Animated Nanos option is respected; client icons remain the fallback. Inactive DS-only models are deactivated, their cache is limited to eight retained previews, and transfer buffers are allocated lazily/released after inactivity.

Equipped cards now select and directly request summoning, including inside the station through an explicit guarded card path. Movement/combat remain blocked there. Card commands include the expected Nano ID and slot, reject stale equipment, obey HP/stamina/vehicle/rate limits and pending station edits, and do not dismiss a Nano already active. The normal legacy toggle and explicit Dismiss behavior are retained separately. No optimistic active-Nano state is introduced. The selected Nano's current power is stably moved to the first list row; other powers retain their order.

Tests cover station card invocation and the real active packet layout, acknowledgement/no-dismiss on repeat taps, stale IDs, pending station edits, Current Power moving after tune acknowledgement, and RGBA-to-Android alpha/channel/row conversion. Native Java build passed (`Logs/ds-native-nano-animation.log`). Full Unity build and hardware animation validation are in progress. No publication.

Full Unity build and configured regressions passed (`Logs/unity-ds-nano-animation.log`), followed by a successful Java-only preview invalidation refinement (`Logs/ds-native-nano-animation-final.log`). Animated frames invalidate only the three equipped cards and selected model, not the entire static collection. The explicit card path also supports station summoning while preserving movement/combat blocking. Final APK installed on the Fold preserving data and launched: SHA-256 `A0396262C5E36FFA3CAB0663F12D551E15C4B0D42D10006F5A8B73DB3555FCE8`, 93,348,395 bytes. Live model motion/color/orientation, direct invocation and fold behavior still require verification after login. No publication.

Fold runtime evidence (process 5269): async preview delivery reached 600 frames in both Unity and Android logs, including the three equipped IDs 4/1/5 and an additional selected owned Nano (id 6). Frames 1→600 spanned roughly 62 seconds (about 9.7 delivered preview fps under the 12 fps cap). Direct card invocation of Eduardo at 03:12:15 was followed by the actual server Nano spawn; the repeated active-card tap at 03:12:18 returned sent=False without dismissal, followed by successful outgoing card requests for Blossom and Buttercup. No preview-fallback, async-readback-error, native panel exception or fatal was observed in the collected trace. Evidence: `Logs/ds-nano-animation-qa.log`.

The current foreground app is the chat app, so no private-screen capture was taken. Visual model motion/orientation and post-fold confirmation remain pending user feedback; the synchronous/lower-memory fallback is not hardware-tested here. Current Power reordering after tune acknowledgement passed the fixture; no materials were spent in live QA. The stale/inactive preview cache targets eight entries after its grace period; active/recent entries can temporarily exceed that target. No GitHub publication.


## Map presentation and shared Taros counter (2026-10-17)

The secondary Map uses Local/World tabs with authoritative selection, a horizontal strip of the original map filter sprites, a left-side zoom rail, a cyan cut-corner map border, and a location footer. Twelve filters mirror the legacy UI: grouped mission symbols, grouped shops, and the ten original individual categories. The filter strip scrolls horizontally when necessary, retaining 44dp touch targets. Legacy Local view continues to show all categories; its filters are disabled with an accessibility explanation. Zoom, pan, markers, objective and player position still use WorldMapWindow's existing state. Unknown filter commands are rejected and valid filters persist through the original preference mask. The map's old text-only Nano shortcut row is hidden to maximize map space; equipped Nano cards remain available on the Nanos tab.

The persistent header now includes Taros beside Nano and Attack boosts, with the original tarosicon sprite. The amount comes directly from PlayerHud.Data.Taros, including normal server-driven reward, trade and inventory updates; no extra currency cache or packet implementation was introduced. Native drawing and the existing bounded texture cache are reused. Changes remain local and unpublished.

Regression coverage now verifies live Taros updates, isolated individual filters, grouped mission filters, rejected unknown filters, and Local-view filter behavior, alongside the existing single-screen/capability/transitions checks. Full APK build and hardware validation are in progress.

Full Unity/Android build succeeded with zero errors and all configured regression suites passed (`Logs/unity-ds-map-taros.log`). Local APK SHA-256: `2950B3C50E320F18C1F0449D2710E939CABE15A12F96C03F6BA5A6B2FFE46D33`, 93,347,565 bytes. Fold installation and live visual validation are pending. No GitHub publication.

The APK was installed successfully on the Fold with `adb install --user 0 -r --no-streaming` preserving data and launched. Live in-world visual validation is pending login.


### Map reference refinement

The follow-up uses the user's close-up map reference: angular Local/World tabs with a lime filled selection, circular plus/minus buttons and three live zoom-level ticks, an angular location plate with the real location highlighted, a continuous cyan outer frame, and a subtle grid beneath the original marker sprites. Existing map input coordinates are unchanged; the zoom controls remain separate native touch targets. Tapped marker names remain readable for five seconds instead of being immediately overwritten by the next 250ms snapshot. All twelve original filter categories and the shared Taros counter from the preceding build are retained.

Only native presentation files changed in this refinement. Native Java compilation, release packaging and lint succeeded (`Logs/ds-map-reference-native.log`); the previously completed full Unity regression suite remains applicable to the unchanged C# map/filter/currency logic. Local APK SHA-256: `7D586126432F9FAFDC54ECFC47E20E024E0DA8AA6A709A7EE81ECFC42C211C6C`. Installation and in-game visual validation are pending. No GitHub publication.

The refined APK installed successfully on the Fold preserving app data and was launched. Visual confirmation of the map requires login; no static mockup was substituted for live game content.

## Nanos catalogue on the right (2026-10-17)

The Nanos page now keeps two columns throughout supported DS layouts, including surfaces narrower than the old 560dp breakpoint. Equipped cards stay at the top left, selected Nano details scroll independently below them, and the collection scrolls on the right. The equipped row adapts to available height, leaving room for details; the collection uses its measured width to choose two to five columns. Resize reflows preserve the existing selected Nano and scroll state. Animated previews, summon-on-equipped-card actions and server-validated station actions continue through the existing data and command paths. No C# gameplay or device-capability policy changed.

Native Java compilation and release APK packaging succeeded (`Logs/ds-nanos-columns-native.log`), and `git diff --check` passed. Module lint ran but is not clean: its standalone manifest reports `MissingClass` for UnityPlayerActivity, provided by the containing Unity player rather than this Android library, plus existing warnings. This was not treated as proof of live layout correctness. The local APK is `Builds/OpenFusion-Pocket-DS-preview.apk`, 93,392,562 bytes, SHA-256 `9C37A6984C3B0B96019961F586FEEFFEAF2B39255A25207CB0E27AC015812B42`. ADB discovery/devices returned no device, so installation and visual/touch validation on the AynThor remain pending. No GitHub publication.

## Remember physical touch verification when reopening the app (2026-10-17)

ADB confirmed that the AynThor exposes a usable presentation display (display 4, Screen-2, 1240x1080 landscape) and a separate touchscreen. PocketDS logs showed the actual cause of the startup fallback: the touch probe had to be repeated because verifiedDisplay existed only in process memory. The same logs showed kind=2, touchVerified=true and active=true after the user's probe. Display discovery itself was working.

The actual touchscreen probe now saves a private profile containing the Android boot count, display connection ID, name/physical mode/density/flags and the real input-device descriptor. App restarts can restore that proof only while the same display connection and exactly one matching physical touchscreen remain present. Display removal deletes the profile, input-device events revalidate it, and a reboot or unknown boot identity rejects persistence. Rotation and refresh-rate changes retain the proof; changed modes/density fail conservatively. Mouse and synthetic/virtual-device taps cannot establish a persistent proof. Automatic/Single Screen preferences and existing minimum-size policy remain unchanged; no screen-size or model-name shortcut was added.

This intentionally does not promise permanent trust across device reboots. The AynThor runs API 33; the public touchscreen-to-display association method is documented only from API 37, and its product-info connection type is UNKNOWN. On this firmware, retesting after a device reboot is the reliable fallback. References: [InputDevice association and descriptor](https://developer.android.com/reference/android/view/InputDevice), [DeviceProductInfo connection types](https://developer.android.com/reference/android/hardware/display/DeviceProductInfo). Hidden APIs and dumpsys parsing are not used by the app.

The host JVM checks cover app reopening, changed boot/unknown boot, replacement display, changed surface, absent touchscreen, ambiguous descriptors and empty profiles (`tools/PhysicalTouchProofChecks.java`). All passed. Native Java compilation and release packaging succeeded (`Logs/ds-physical-auto-native.log`). Local APK SHA-256: `BD65A43B3EE09C6F9042B30B4A2589D601A8B8C57B796DEF9ACDADB883AB9319`. Live persistence verification requires one new real touch probe after installing this version, followed by an app restart. No GitHub publication.

Hardware follow-up: installation on the AynThor succeeded preserving data. After the user's real touch confirmation, PID 10581 logged physical=true, touchVerified=true and active=true in the world. ADB then force-stopped and reopened the app. The new process (PID 11570) immediately reported reliable=true, available=true, kind=2 and touchVerified=true without another probe, confirming persisted recognition across process termination. Active=false at the launcher is expected because the secondary gameplay panel requires entering the world. No Unity/AndroidRuntime errors appeared in the new-process capture (`Logs/aynthor-ds-reopen.txt`). A second in-world activation after this restart and persistence across reboot were not claimed as verified.

## Legacy Nano stamina and assigned-power presentation (2026-10-17)

The AynThor's follow-up log (PID 18286, `Logs/aynthor-revive-confirmation.txt`) confirmed normal revival: REGEN_SUCC at 22:58:08.483 supplied HP 960 and coordinates 370540,444137,-5300 with moveFlag=0; the client applied (3705.40,-53.00,4441.37) before resumed movement. Subsequent locomotion remained logged through 22:59:02.267 without another server-close report. DS stayed available; a short destination-loading surface transition at 22:58:08.727 restored active DS at 22:58:08.890. This is a bounded successful hardware observation, not a guarantee against future network/backend closures.

The Nanos panel now exports the original HP_BAR fill, nano_st_bar frame and buff_icon_back art once per collection snapshot through the existing cached texture exporter. Equipped cards and selected details use those stamina textures and authoritative energy/max values; no value is substituted for unknown bank energy. Each equipped card shows the assigned skill's original icon from the shared inventory/Nano bank, with the original icon background, rather than a mock skill or a fixed tune index. No additional model cameras were added.

Selected details use a white Nano name and the same group color as equipped cards. Outside Nano Station, the C# snapshot contains only the current power and the native view also filters alternatives defensively. Opening the station restores the real tune choices/costs and existing server-validated change actions; closing it removes alternatives. Regression checks cover current-only world browsing, assigned skill changes after the existing acknowledgement handler, alternative choices at the station, hiding them again on close, unknown/locked skill state and presence of all three original UI textures. APK compilation and installation/visual verification are in progress (`Logs/unity-ds-nano-legacy-details.log`). No publication.

All configured regressions and full Unity/Android APK packaging passed (zero errors, 12 build warnings) in that log. Local APK: 93,393,696 bytes; SHA-256 `BA92C9508A9412EBFF15D588AB266C19E77A23D90A44846D7F45E5BFE5D6815C`. Data-preserving AynThor installation is in progress. Hardware visual inspection of the new bars, assigned icons and station-only alternatives remains pending login. The separate revival/session hardware check above passed before this presentation update.

The replacement installation completed successfully with data preserved; the app is open on the AynThor (PID 21928). Visual/touch confirmation of the new Nanos presentation is pending user login. No GitHub publication.

## Power cards and complete map-filter tiles (2026-10-17)

Nano powers now use a My Stuff-style card: CURRENT POWER caption, original skill icon at the left, white power name, yellow type and a separate blue description area. Native layout wraps the name/type and description instead of centering a separate icon between text lines. Existing current-only world browsing and station-only alternatives/tuning commands are unchanged; this presentation uses the same snapshot data.

The map screenshot showed a partial next tile in the horizontal filter viewport, rather than a missing filter sprite. The viewport now fits a whole number of the existing 44dp touch tiles. A native scroll subclass settles to a tile boundary after touch/fling completion; the scrollbar stays visible to expose the remaining categories. All original categories remain horizontally accessible and keep their shared filter state; dragging can temporarily show partial tiles while the finger is moving. There is no size/model shortcut in device capability detection.

Only native presentation files changed. Java compilation and release packaging passed (`Logs/ds-power-card-map-filters-native.log`); the unchanged C# logic retains its preceding full-suite validation. Local APK SHA-256: `473FE15530FEE08FAE5FF1B53515C3A62082AF21AC80F88AC8F3BB81C43ED163`. Hardware installation is in progress, and in-world visual/touch validation is pending login. No GitHub publication.

Installation on the AynThor completed successfully preserving app data and the activity was launched. New power-card styling and complete-tile filter scrolling still require in-world visual/touch confirmation.

### Station-only equipment and grouped alternative power cards

The selected Nano's EQUIP IN SLOT title and slot buttons are now omitted unless Nano Station is open. The existing station/connection/busy/active-slot checks still govern the buttons when visible. Alternative powers at the station now contain their icon, name, type, description and a blue resource section in one card, matching the My Stuff information hierarchy. Required Fusion Matter and owned/required item counts use framed fields; insufficient item counts are red. The numeric Fusion Matter field is a required amount, not a fabricated available balance/progress gauge. Change/confirmation/cancel controls remain inside that same card and continue to use the existing server-validated tuning command.

Only NanoPane's native presentation changed. Java compilation and release APK packaging succeeded (`Logs/ds-station-power-requirements-native.log`), and `git diff --check` passed. Local APK SHA-256: `98CCE0C74A117B7FF4569385A2CB08709A79EF76C675E57108A9D139643FEA90`. Installation is in progress; station/world visibility and card styling await in-game visual confirmation. No resources were spent and nothing was published.

The APK installed successfully on the AynThor preserving data and the activity was launched. Visual confirmation remains pending login.

### Keep the summoned-Nano indicator current on retained cards

The collection grid is intentionally retained while only the active Nano changes. Its cells previously continued drawing the `active` value from their creation snapshot, leaving the yellow dot on an earlier Nano. Cards now derive that dot from the current authoritative `activeSlot` plus their equipped slot. Every snapshot refreshes this state for both equipped cards and retained catalogue cells, including activeSlot=-1 when dismissed. Only cells whose summoned status changes are invalidated by the new update, and the grid is not rebuilt for this purpose. Selection highlighting remains independent, preserving the browsed Nano and collection scroll. Accessibility descriptions also reflect the current summoned state.

Native Java compilation and release packaging passed (`Logs/ds-nano-active-indicator-native.log`) and `git diff --check` passed. Local APK SHA-256: `18583D0DCFE03F53706AC2CF7336B7E574B8B5797295CA5E4AAECB5C5CF7DB54`. Installation is in progress; hardware switching/dismissal visual confirmation remains pending. No gameplay/protocol change or GitHub publication.

Replacement installation succeeded on the AynThor with data preserved and the activity was launched. Switching/dismissal visual verification still requires entering the world.

### Real station resource progress gauges

The station resource fields now draw the original NanoPopupFMBar and NanoPopupItemBar fills behind available/required counts, matching the existing My Stuff gauge data and texture paths. DualNanoPower now carries the real available Fusion Matter, its required cost, real inventory item count/requirement and a resource-availability flag. Missing source data displays an unknown amount and no fill. Progress is capped visually at 100%, while numeric amounts retain surplus resources; zero-cost requirements use a full gauge like the original. Shortages keep red text and the existing tuning guard/confirmation. Neither gauge consumes resources or infers a fake balance from whether the Change Power button is enabled.

Resource changes participate in the existing power snapshot key, so visible gauges refresh when the HUD balance or inventory count changes. Regressions verify exact balance, half-progress/insufficient resources and surplus amounts, alongside station-only visibility and tuning validation. All five original Nano art resources, including both progress textures, are resolved by the regression. Full build/installation are in progress in `Logs/unity-ds-station-resource-gauges.log`; live visual verification remains pending. No GitHub publication.

All configured regressions and Android packaging succeeded in that log (zero errors, 12 build warnings); `git diff --check` also passed. Local preview: 93,396,775 bytes, SHA-256 `AD3B6FB8E465BBFFBF4AE8691D64F4DFC1006DE99830906793FF1504BA006173`. Replacement installation on the AynThor is in progress. No real tuning/resources were used during validation; native visual confirmation of both gauges remains pending login.

Installation succeeded preserving data; the activity was launched on the AynThor. The user can verify the two progress gauges at Nano Station without changing a power or spending resources.


### Equipment DS integration and centered navigation

Navigation labels are fitted and centered under their icons, including the remaining full-window shortcuts. Equipment is now a real secondary tab, not a shortcut to an unimplemented mockup. It presents the same InventoryWindow/HudData player name, level, One/Area/Defense computation, health/Fusion Matter, guide, all nine equipped slots (including legs) and the existing 50 inventory slots. Both item columns scroll independently. Persistent boost/Taros counters remain shared with the other tabs. Art uses the downloaded original icons and HP/FM textures; the chrome/layout follows the reference rather than copying its sample values.

The character uses the existing AvatarPreview model/camera, including combined-item appearance and mounted-vehicle selection. A bounded 256x320 RGBA stream crosses JNI at up to 6 fps (3 fps synchronous fallback); no new world camera or frame PNG files are created. Only a visible Equipment tab requests frames; inactive readback buffers are released. Missing previews display an explicit loading/unavailable placeholder rather than a fabricated character.

Tapping a nonempty supported item opens InventoryWindow's actual OpenInfo/GeneralPopup and DrawOverlays on the primary Unity surface, while keeping DS visible/read-only. Equip/weapon slot selection/unequip/use/chest/gumball/delete dialogs continue through their existing validation and packet handlers; opening an item never automatically uses/equips it. Commands verify the surface revision, connection, modal/death state, slot bounds and exact Type/Id/Opt/TimeLimit before opening. If a server update replaces the selected item, its dialog is dismissed. Closing the dialog returns to the world with Equipment retained; losing DS restores the complete existing My Stuff interface. The More Options shortcut exposes the original window for sorting, redeem and drag operations. Original quest items (type 8) do not have a supported item dialog and remain non-actionable here, as in Legacy's current port.

DualEquipmentRegressionChecks runs in BatchDS and verifies full slot coverage, real HUD values, combined-item identity, stale/bounds/quest rejection, offline restrictions, DS retention with blocked world input and ordinary My Stuff fallback. Validation log: Logs/unity-ds-equipment.log. Hardware visual/item-dialog validation is pending the new APK installation/login. No real item use, deletion, equipment change or resource spending is performed by these fixtures. Nothing is published to GitHub.

All configured regressions and full Android packaging passed (zero build errors, 12 warnings). `git diff --check` passed. Local APK: 93,420,618 bytes; SHA-256 `5851C2B41CAF590B7CC23932AFED57CD9E00198F42A8DA1F0361A1B33B299BEA`. Replacement installation on AynThor 192.168.68.100:33155 succeeded, preserving data, and the activity was launched. User visual confirmation of Equipment/avatar/original item dialogs remains pending login; no item-changing actions were performed.


### Station gauges, Equipment gestures and active-Nano gumballs

The resource fields now use an explicit Canvas View rather than inherited TextView drawing. The original tiny, dark NanoPopupFMBar/NanoPopupItemBar textures are composited over a brighter green/amber fill, with the border and centered counts drawn last. Real available/required ratios are unchanged: surplus fills to 100%; 0/5 has no fill; unknown amounts remain empty. ResourceProgressChecks exercises surplus, empty, partial, zero-cost, unknown and negative bounds. Hardware contrast/appearance still requires visual confirmation.

Equipment no longer prints Rotate or uses the name area as rotation buttons. Horizontal drags beginning over the model generate accumulated, bounded yaw commands; inventory/equipment scrolling stays independent. DS commands drain each Unity frame, refreshing native capability status before processing a nonempty batch to retain surface-revision checks, while heavier layout/status work remains on its existing interval. The existing avatar preview targets up to 24 fps on >=6 GB devices, 18 fps on >=3 GB, 12 fps below that, or 6 fps synchronous-readback fallback. Actual throughput depends on game frame timing/GPU; this is a cap, not a measured frame-rate guarantee. No additional world camera or frame PNG files are used. The previously exported original guide icon is now drawn beside the guide name.

My Stuff Sort now draws its label separately from the inherited skin content offset. Taros digits fit inside the nine cells of the original taros_count texture (normalized coordinates), rather than using its transparent top padding as the text area. Existing sorting actions, inventory and currency data remain unchanged.

For a selected, summoned Nano outside Nano Station, Gumball appears beside Dismiss Nano. The shortcut selects an in-stock general item of subtype 3 with matching StimPackAttri (Nano style + 1), preferring that over a universal (4) item. It revalidates active Nano identity, expected item, inventory quantity, gameplay/modal/death restrictions and existing buff timer; a three-second pending guard prevents repeated sends while awaiting the existing item-use acknowledgement. It calls the original UseNano inventory/Nano-slot packet path; inventory quantities and buff timer remain acknowledgement-driven. No free items, optimistic consumption or alternate protocol were introduced. The action is unavailable at Nano Station, during an active known gumball buff, or without compatible stock. DualGumballRegressionChecks uses a stub sender to verify packet slots, style/universal choice, stale/death/station/duplicate guards, unchanged pre-ack quantities and Taros text bounds. No real gumballs were used during validation.

Build/test log: Logs/unity-ds-ui-polish.log. Compilation/installation and hardware visual confirmation are in progress. No GitHub publication.

Final build passed all configured regressions and Android packaging (zero errors, 12 warnings); Java ResourceProgressChecks and git diff --check passed. Local preview: 93,441,924 bytes, SHA-256 3C9A80530EA358F135FB4C4C076B7ACABE1B7B536634A89DECF9C7E71F857309. Replacement installation on AynThor 192.168.68.100:33155 succeeded preserving data; the activity was launched. Visual contrast/gesture/Sort/Taros/guide confirmation remains pending login. No real gumballs were consumed by validation. No GitHub publication.

### DS Missions journal

The Missions navigation tab now opens a native DS page with Active/Completed tabs and Nano/Guide/World sections. The selected mission appears on the right, with the original journal NPC portrait/banner, level/difficulty, descriptions, live objectives and counters, actual item/Nano rewards, Fusion Matter and Taros. Both columns scroll independently to fit the AynThor and unfolded layouts. All content comes from JournalWindow, NanocomHud and the existing tables/completion flags; there is no separate quest inventory or mission protocol.

Inspection and tracking are distinct: selecting a row only opens its details. Track calls the existing JournalSelect path and updates the HUD/waypoint. Abandon opens a confirmation, then revalidates the exact active mission/task, connection and modal restrictions before using the original task-stop packet. The mission remains visible until the server acknowledges removal. Completed missions have no Track/Abandon actions. Offers, reward selection and turn-in continue through the original NPC interface. No mission is automatically accepted or completed.

The shared journal selection and active/completed mode survive opening the original Journal; DS list/detail offsets are retained separately by tab through surface recreation. Existing capability checks and single-screen fallback remain in control of DS; this page does not split conventional devices or introduce another world camera. The visual treatment follows the mockup with the established cyan/lime panel style; narrower detail/reward sections scroll rather than fabricate or truncate content.

DualMissionRegressionChecks uses fixture tables and a stub sender to check live categories, enemy counts, reward values, inspection versus tracking, stale task rejection, duplicate stop requests, acknowledgement-driven removal, completion flags and retained selection/scroll. No real mission abandonment or reward claim occurs in these tests. Build log: Logs/unity-ds-missions.log. Hardware visual confirmation remains pending. No GitHub publication.

Missions validation: all configured C# regressions and full Android packaging passed (0 errors, 12 warnings), log Logs/unity-ds-missions.log. APK 93,462,334 bytes; SHA-256 257D365B5778CC288C89032988A5C9D0169E3268B6649DFBF077155DF4CFBDF2. Replacement installation on AynThor 192.168.68.100:33155 succeeded, preserving app data. Hardware panel verification requires login; no real mission actions were performed. No GitHub publication.


## v1.5 release validation

The user confirmed Missions works and reported that the camera-grounding correction made the experience much better on AynThor. Fold/unfold session and map restoration were previously confirmed on Galaxy Z Fold. These reports supplement the regression fixtures; they do not establish full device or mission coverage. Dedicated native Chat/E-Mail panels and complete tutorial/playthrough validation remain pending. The v1.5 release uses Android build code 3 and the existing package/signing identity.

Release v1.5 validation: full Unity regressions and APK build passed with 0 errors / 12 warnings; standalone Java layout/touch/progress checks passed. Package/version/architectures were checked from the actual APK, and its signing certificate was compared with the downloaded v1.0 release. APK SHA-256: 27ecc729c0500543650dfd87c9c21276b98f651ad75ed8460f21c2898ca694a4 (93,463,255 bytes).
