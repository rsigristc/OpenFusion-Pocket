# Mission objective audit - v2.1

The reported blocker is Practice Pranks, mission 504, in Retrobution 3.0.3. This correction is included in v2.1; the published v2.0 release remains unchanged.

## Reference and scope

Compared the current decoded Mission/NPC tables with `cnMissionManager.Update`, `KillMissionNPC`, `CheckMissionItemCount`, `RequestTaskComplete` and the ten-second request checker in the current Legacy client. NPC marker handling was checked against `NpcMoveController` (NPC types >=100 remain invisible and do not perform ground adjustment).

The actual tables contain 2,866 task rows (including sentinel rows): 835 talk, 671 location, 648 combat, 404 collection, 298 delivery and eight escort rows, plus two type-zero rows. Among them are 672 destination/escort rows, 444 rows with kill requirements and no terminating NPC, 212 collection rows without a terminating NPC, and 96 timed rows. These classifications overlap. All 672 destination/escort rows resolve a positive NPC sight radius. This is a structural/data audit, not 2,866 completed gameplay tests.

## Practice Pranks

- Task 462: reach NPC table number 1099, type 111, within a **15m three-dimensional radius**. This is an invisible destination marker, not the outside warp NPC. Success points to task 466.
- Task 466: approach the lair marker 1471 (6m).
- Task 468: enter the lair and reach marker 1470 (15m).
- Task 463: receive four authoritative Shocktangler (type 2513) kill notifications; the table requires instance 12. Success points to task 667.
- Task 667: defeat Fusion Numbuh Two (type 7), one kill. The instance/reward/ownership rules remain server-controlled.

Other rows 461/464/465 exist in the mission's table group, but the client follows the explicit outgoing-task links rather than assuming numeric or table order is the live sequence.

## Findings and corrections

1. Nanocom tracked active tasks and counters, but omitted Legacy's periodic destination/escort completion checks and automatic completion requests for eligible kill/collection objectives. The shared detector now uses live server NPC instances, table sight radii, full 3D distance and all enemy/item conditions. It never completes by map name or waypoint coordinates alone.
2. Automatic completion sends the normal TASK_END request. A task remains active until END_SUCC. Only that acknowledgement requests the table's next task. A ten-second pending guard prevents duplicate requests; repeated END acknowledgements cannot start the next task twice. Failed starts release their retry guard. No online kills, items or rewards are synthesized.
3. NPC metadata previously existed only when its graphic model was instantiated. Metadata is now initialized independently, so a streamed/missing visual model does not remove a server-provided objective marker. Invisible NPC types >=100 retain their server altitude and skip terrain grounding/shadows.
4. Enemy counts consume the server's quest-kill notifications, match NPC species, remain bounded and restore remaining counts by species across reordered saved slots. Sending a start request no longer resets current progress before an acknowledgement arrives.
5. Acknowledged quest-item updates now refresh the shared MissionState prerequisite quantities, avoiding eligibility that remained stale until reconnect. Level acknowledgements similarly refresh eligibility. Missing waypoint data clears the previous marker instead of retaining another task's destination.
6. Escort completion checks the escorted NPC is within 5m of the destination and sends the escort's instance ID, rather than the destination NPC ID.
7. Known expired timers request server resolution. A reconnect packet without remaining-time data does not invent a deadline. Dialogue/delivery/script-only tasks are not automatically completed by proximity.
8. Objectives with a reward open the existing reward chooser without selecting or claiming a reward. Reward mode now reads the current task's reward row, rather than the mission's final task row. Ordinary journal summaries retain final-mission reward presentation.

## Validation and limits

`MissionObjectiveRegressionChecks` exercises the real Practice Pranks records, missing/wrong/outside/vertical destination cases, 15m radius, NPC instance IDs, four required Shocktanglers, mixed enemies and quest items, pending/acknowledgement behavior, saved-counter restoration, manual dialogue, timers, escort distance/IDs, missing streamed marker graphics, live prerequisites and reward-chooser behavior. It runs with the full release regression pipeline.

Logs and fixtures are ignored under `Logs/mission-audit/`. No private account credentials are exported. Limited destination diagnostics report task/marker IDs, distance and radius for device verification.

Live confirmation of Practice Pranks after installation remains required. Complete coverage of timed reconnect behavior, every escort lifecycle/reinvite, every branching mission and server-specific reward/error behavior is not established by these tests. The server still controls kill credit, quest drops/drop probabilities, instance restrictions, completion acceptance and rewards. No GitHub publication is part of this correction.

Additional source-table observation: task 2705 (Schmingerbread Scrooge) has an item quantity of one with item ID zero. Its real enemy requirement is one mob of type 3144, so the invalid item slot is ignored and the valid kill requirement remains mandatory. No source-table data was changed. All current mission IDs fit within the completion-flag range used by the client.

Full configured regression suite and Android packaging passed in `Logs/mission-audit/build.log` (0 errors, 17 warnings). Local APK: `Builds/OpenFusion-Pocket-mission-fix-preview.apk`. Physical mission progression remains pending installation/login testing.

## AynThor verification

Replacement installation succeeded without clearing app data; the activity was reopened. APK SHA-256: `98e6dd62353d5b66f77cfd3185f39faca299ea5208bc176d11305ad48b902bb3` (116,932,679 bytes). The user re-entered KND Training Area. Device log `device-after.txt` records restored task 462 at 10:06:24, the real destination marker at distance 0.42m within its 15m radius, TASK_END at 10:06:26, server END_SUCC and the next START_SUCC for task 466 at 10:06:27. This confirms the original blocked transition on hardware; it is not merely a local UI change.

The new task initially reports marker 1471 outside the delivered NPC set; it is not auto-completed using cached map coordinates. Continuing toward the actual lair is required for its own proximity check. Full lair/combat continuation is pending the user's next live test.

## Infected-zone platform landing follow-up

The user paused the lair test to report shallow foot penetration on the infected training area's wooden arrival platform. `GameClient.ApplyServerRelocation` applied raw server coordinates without resetting the controller or resolving destination platform support; the ongoing correction covered terrain height but not platform colliders/skin penetration.

The follow-up resets the CharacterController during authoritative relocation, waits for destination map readiness, and queries nearby upward-facing physical support. The query excludes the avatar, CharacterControllers, triggers, water and portrait geometry, and selects the closest qualifying surface rather than the highest overhead surface. It only raises a below-surface pivot within a bounded range and retains the server X/Z. Normal walking corrects shallow skin penetration on physical platforms without snapping an airborne avatar down.

`PlatformLandingRegressionChecks` exercises a warp starting 0.2m inside a box platform, map-loading deferral, self/trigger/roof exclusions, original X/Z and controller state, 180 stable idle probes and free airborne height. The existing visible-terrain/no-collider stability test also passes. Actual AynThor appearance after re-entering the warp remains to be checked; these tests do not prove every platform's rendered mesh matches its collision mesh.

Platform follow-up delivery: full configured regressions and Android packaging passed in `Logs/mission-audit/platform-build.log` (zero errors, 17 warnings). Replacement installation succeeded on the AynThor and the app was reopened. Final local APK: `Builds/OpenFusion-Pocket-mission-platform-fix-preview.apk`, 116,934,889 bytes; SHA-256 `73eb5212a3881af035dc503f3cbfe5b515cc81ed01fea6d58b8aa98796794d79`. Re-entering the warp and physical appearance remain pending user feedback. No GitHub publication.

The user then reported that the feet initially remained embedded and popped upward seconds later. Device timestamps isolated a scheduling defect: authoritative relocation at 10:24:35.871, map ready at 10:24:35.925, but support correction (+0.596m) at 10:24:41.727. The support call was behind `TransportWindow.Sending`, which includes the six-second warp fallback wait. It now runs immediately when destination geometry is ready, independently of that movement guard.

`WarpFlow` now also clears its pending network wait on the dispatcher's validated relocation event (GOTO and successful warp), including short coordinate-only acknowledgements, without inventing absent item/currency fields. Map loading remains a separate movement gate. Regression checks dispatch both actual packet IDs through PacketDispatcher, drain the real WarpFlow queue, and verify immediate network acknowledgement while retaining destination-loading protection. `platform-wait-checks.log` passes alongside the terrain/platform stability tests. The earlier platform APK was insufficient; the next build supersedes it.

Final scheduling-fix build: all configured regressions and packaging passed in `platform-final-build.log` (0 errors, 17 warnings). Replacement installation succeeded and the app was reopened on AynThor. APK: `Builds/OpenFusion-Pocket-mission-platform-final-preview.apk`, 116,940,758 bytes; SHA-256 `37caf25e5ce4e554e276c8a419bad21eef70ed014274b09249c178383207ea05`. Actual warp latency/appearance is being rechecked; this supersedes the preceding delayed-adjustment APK.

Final AynThor landing result: the user confirmed the feet now appear correctly. Device timestamps show the acknowledgement clearing the network wait at 10:38:06.589, authoritative relocation at 10:38:06.617, map readiness at 10:38:06.673 and the +0.596m support correction at 10:38:06.678: approximately 5ms after map readiness, replacing the prior ~5.8s delay. The original mission's arrival stage is confirmed server-completed; continuation through the lair and four-kill objective is the remaining live test.

## Confirmed mission continuation

The user confirmed that the four-Shocktangler counter completes and changes to the Fusion Numbuh Two objective. Device logs independently confirm END_SUCC/START_SUCC transitions 466→468 at 10:41:50, 468→463 at 10:41:56, and 463→667 at 10:42:58. Thus infected-area arrival, finding/entering the lair and the four-kill stage all progressed through real server acknowledgements. The boss's final defeat/reward was not exercised in this verification; neither was every live mission.
