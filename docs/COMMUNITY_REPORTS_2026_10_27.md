# Community reports: October 27, 2026

Local changes only. Source references below are the installed original Retrobution
client decompilation under `Logs/retrobution-303/decompiled`, its serialized bundles
and the current CDN manifest. This is an audit of concrete client paths, not a
claim that every location/device has been played through.

- **Nano Dash:** `Do3DDashMovement` flattens camera forward and uses the controller
  with gravity disabled. Pocket instead set the transform and chose any support
  up to 20 m above it, including roofs. Dash now uses swept controller movement,
  preserves horizontal travel and bounds the final time step. A near-feet probe
  decides whether to fall when the dash ends.
- **Charging Drain:** Legacy `NpcAnimation.SkillReadyMotion` uses `readyspell`,
  falling back to `ready`; Pocket requested a nonexistent `skillready`. The charge
  pose is retained until a cast/interrupt/movement packet, with normal death taking
  precedence. It does not fabricate an extra drain beam during preparation.
- **Email recipients:** the mail window had a separate static append-only buddy
  list. It now reads the canonical SocialMenu roster, including slots, blocking and
  removals. Reinitialization clears the old composer and scroll. A draggable
  scrollbar handles long lists. Removing a buddy uses its actual server slot,
  rather than its index among unblocked recipients, and matches only the sender's
  UID so two empty/pending names cannot select the wrong buddy.
- **Monkey Skyway:** activate the whole original `wear/item_broom_01.nif` hierarchy,
  explicitly start its embedded animation, protect the rider pose from remote
  locomotion, and allow the original mount1/mount2 poses. New sessions detach old
  handlers and clear mounted models/poses.
- **Combined weapons:** local and remote animation style comes from the appearance
  item encoded in the high half of Opt. Base combat parameters and stat item identity
  are retained. Invalid appearance IDs fall back to the stat item's animation.
- **Building collision:** preserve serialized collider enabled/trigger/convex
  flags. Do not add a second collision-material mesh collider when the object already
  has an explicit collider. Avatar controller dimensions remain the Legacy values.
  The exact PCC Fort cardboard route still needs a physical traversal check.
- **Nano matchup indicator:** use original nano_win/nano_draw/nano_lose textures
  with the same three-way comparison as CnGuiMonster_info.
- **Capsule NPCs:** null AssetBundle entries no longer shadow real assets. Missing
  NPC resources can load separately from their map scene, using a generated route
  index from real bundle contents, preferring the actual asset owner over external
  aliases. Imported resource dependencies remain pinned
  across dong unloading. Recovery is manifest-checked, sequential, capped at 16
  additional bundles per connection and retries transient failures at most three
  times, 30 seconds apart. These are fallback hints, not alternate game data.
  Larry 3000 and Father rebuild from DongResources_10_07. Recall Point is different:
  its original rxcom prefab is an animated, meshless anchor for service FX; it must
  not be replaced by a capsule simply for lacking a renderer.
- **Peach Creek Estates launcher:** preserve Legacy direction/power, sweep the
  initial 10 m placement to avoid crossing world geometry and relocate the
  controller coherently. Flight now applies the terrain correction that ordinary
  walking already had, preventing a missing/misaligned terrain collider from
  sending the avatar below the visible floor. Frame time is bounded during flight.
- **Full inventory:** show the original crate/full_inven artwork when all 50 real
  inventory slots are occupied, with Legacy-style entry bounce. Receiving a crate
  temporarily takes priority over the full-inventory notice.
- **Nano target ring:** render original target_n artwork, pulsing at the original
  rate over valid enemy targets. The same target collector handles both the ring
  and skill request for single-target, cone, self area and selected-target area;
  it respects range, alive/team filters, selected target priority and target count.
  Preview collection no longer mutates the NPC target sort distance.
- **Tower camera:** replace the high-column ground search with bounded support,
  ignore actor colliders in camera obstruction tests and guard zero-length look
  vectors and vertical-pivot singularities. Terrain correction remains available
  without a physical terrain collider. Buffered queries avoid allocating a new hit array every frame, with a
  full-query fallback in unusually dense geometry. Exact City Station tower views
  still need device confirmation.

The original Construction Site Map_05_03 contains 7,351 nodes and 1,021 objects
with mesh colliders; these are enabled, non-trigger and non-convex, and no objects
in that file have multiple serialized colliders. Preserving flags is consequently
not proof of a fix for the reported PCC Fort cardboard route. Do not widen the
avatar or inflate every world collider without reproducing that specific route.

`CommunityRegressionChecks` exercises physics, targeting, roster transitions,
combined-weapon stat/appearance separation, charge cancellation, HUD assets and
inventory capacity. `CommunityAssetAudit` uses real tables and models, generates
NpcAssetRoutes.json, verifies Larry/Father/Recall/Monkey and runs the actual
asynchronous missing-bundle importer and Monkey attachment lifecycle. Reports
remain under Logs/community-audit. Existing full regression checks and Android
packaging must pass before distributing the local APK.

Device playtests are still needed for the exact launcher, both camera reports,
PCC climbing, the duration of a full Skyway trip and Drain under different mobs.
An offline physics/asset test does not certify those complete gameplay scenarios.

## Follow-up: time-machine warp and steep slopes

`NpcUI` now ends dialogue on every authoritative relocation and restores its own
hidden renderers even if the streamed-out NPC already compares equal to null.
Starting another dialogue also releases the previous renderer lease. Intentionally
hidden equipment is untouched. This fixes a concrete visibility leak; the user's
exact time-machine sequence was subsequently replayed on the AynThor: the user
confirmed the avatar remains visible after using "Go to time machine now".

Legacy `EnvironmentCollision.HandleSurfaceSliding` replaces horizontal movement
on slopes beyond `controller.slopeLimit` with the downhill normal at 2 m/s and
doubles downward velocity (`cnAvatarThirdPersonMove.slideSpeedModifier = 2`).
Walking now applies that missing step using near-foot physical support and terrain
normals, including terrain height correction without a physical collider. Rising
jumps are left to the CharacterController; distant surfaces do not alter flight.
The existing slope limit is unchanged. Regression fixtures verify steep/gentle
slopes, airborne separation, and renderer restoration without exposing hidden gear.
The user also confirmed steep slopes now cause the expected downhill slide on the
AynThor. ADB showed completed destination loads and dialogue cleanup, with no
matching fatal/null-reference/missing-reference/index exceptions in the inspected
log buffer. Local preview APK SHA256:
`09CA36D05C313C1C7730E81BD3ECD11624F715C0DB6537C496843231F98823F0`.

## Follow-up: Monkey attachment size

The user still reported an invisible Monkey during Skyway travel, while ADB
confirmed the mount object had been created. Testing the actual male/female actor
prefabs revealed the `Bip01 Broomstick` attachment bone has a scale of about .36.
The port parented with `worldPositionStays=false` and reset local scale to one,
shrinking the mount into the rider. Legacy instantiates in world space before
parenting. BroomFlight now reuses VehicleWorldScale to preserve world scale one
after animated bone transforms. Both riding clips retain their existing playback.

`CommunityAssetAudit.BatchMonkey` exercises the actual runtime mount construction
with both real actor skeletons, mount1/mount2 sampled at four phases, enabled
geometry and embedded animation playback. Before correction the largest mesh's
bounds were approximately .55 x .78 x .76 m; after correction 1.51 x 2.14 x 2.07 m.
See Logs/monkey-skeleton-audit.log and Logs/monkey-skeleton-fixed.log. The standard
regressions additionally verify uniform and nonuniform animated attachment scales.
The user confirmed the model and animation now appear correctly during a live
Skyway trip on the AynThor. ADB confirmed world scale (1,1,1) with five renderers;
no matching Unity null/missing-reference exceptions appeared in the inspected
buffer. Installed preview SHA256:
`EFEE7B9E4A8FAE33ECB4259C52E7B098FB63838C738A865A72BB35EBC3145AE9`.

## Follow-up: initial guide selection and travel to the past

Legacy cnGuideMode.ReceivePacket handles the first mentor success by switching
to NpcIconMode and dispatching NpcWarpOK with NPC table number 1425. It is not a
system-message ID: the actual MessageTable contains only 265 rows. The port
incorrectly called MsgThen(1425), displaying a numeric fallback and omitting
the teleport request. Actual Retrobution tables identify NPC 1425 as "To the
Past!", warp 76, with no item, task, level or currency restriction in that row.

The continuation now finds the real server NPC by table number and follows the
ordinary checked NPC warp sequence using its runtime ID, original effect/sound,
1.5-second delay and server-authoritative relocation. It waits briefly for a
streamed destination and provides readable retry instructions on timeout.
Reopening the Time Machine after a previously acknowledged choice resumes the
warp without purchasing/changing the guide again. No position, quest completion
or payment is invented locally. Duplicate acknowledgements cannot repeat travel;
rejected guide changes cannot start it. Live past travel is left to the user
because it can permanently leave the Future and remove active Future missions.

GuidePastRegressionChecks covers the handshake, actual runtime-vs-table IDs,
request rejection, duplicate acknowledgement, saved-guide recovery and delayed
or absent NPCs with an intercepted sender; it sends no real account operations.

## Follow-up: Nano exhaustion and initial equipment

Legacy cnOwnAvatarStatus.PCTick withdraws the current Nano and sets its active
slot to -1 when the server reports stamina <= 0. Pocket updated the stamina but
left that slot active, so passive Nanos could remain visible through negative
energy and their subsequent recharge. Server stamina now shares a nonnegative
slot/bank update path that retires only the matching active Nano at exhaustion.
Regeneration does not resummon it. Tick/respawn, skill results, charge and timed
drain updates use this state consistently. Late skill results for another Nano
cannot dismiss the replacement. Buff values remain server-authoritative; this
does not indiscriminately remove group or external buffs.

Legacy GameFrame's NANO_TUNE_SUCC handler equips the first empty slot when the
Nano previously had skill 0. It does not equip an untuned Nano at CREATE_SUCC.
Pocket now sends that equipment request after initial power acknowledgement,
without updating the equipped slot until EQUIP_SUCC. Full slots retain the Nano
in storage; retuning does not auto-equip. Pending initial equipment reserves
separate slots for simultaneous acknowledgements and resets on a new session.

NanoLifecycleRegressionChecks covers exhaustion, negative-energy clamping,
inactive recharge, stale responses, no pre-power equip, real equipment ACKs,
full slots, duplicate/concurrent tuning replies and session cleanup. The user confirmed both passive-Nano exhaustion/recharge and first-power
auto-equipment into an empty slot on the AynThor. Warp to the Past was also
confirmed working after guide selection.


## Follow-up: boost shortcuts and touch camera zoom

The DS resource strip and the single-screen HUD route Nano Boost / Attack Boost
taps through CombatSystem.ActivateBoost, shared with the existing mapped inputs.
Nano Boost requests the existing server charge for the active Nano; Attack Boost
toggles the existing weapon-battery state and packet. The 300 ms input cooldown,
live-session/death/modal checks and available-resource checks apply to all paths.
The attack counter gains a yellow outline while boost use is enabled and stock
is positive. Taros remains informational.

World-camera pinch now tracks a stable pair and a baseline when the second
finger arrives. Pinch consumes neither an attack nor a joystick finger, suppresses
rotation during zoom and suppresses tap selection when the gesture ends. Modal,
surface and controller transitions clear its state. It uses the existing camera
distance limits and world-camera availability checks. Optional virtual Zoom In /
Out buttons remain available in the touch profile editor.

Validation: input regressions cover spread/close, finger ordering, attack held
through pinch, no baseline jump, no tap/rotation on release, and modal cancellation.
Android instrumentation on the AynThor passed at two panel sizes: Nano/Attack
taps enqueue exactly one matching command, Taros does nothing, drag/cancel
suppresses activation, disconnected taps are rejected, and the active Attack
Boost draws yellow pixels at the outline. No gameplay resources were consumed.
Build Logs/boost-pinch-build.log completed with 0 errors; the installed local APK
SHA-256 is 625AD819B7ACF1E613E4935DB7A440FD6330ED53EA2FAC67FC32511B25113E08.
The user confirmed the boost shortcuts, active outline and pinch zoom work correctly on AynThor.
