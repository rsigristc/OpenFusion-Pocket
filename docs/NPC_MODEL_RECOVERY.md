# NPC model recovery — Fusion Numbuh Two

Post-v2.0 correction included in v2.1. See [release notes](releases/v2.1.md).

## Diagnosis

The AynThor log reports `Modelo de NPC no disponible: Npc#7 mob/fusion_numbuhtwo.kfm` on entry to the lair. The magenta capsule is the old fallback primitive, not a successfully loaded boss with a bad texture. The primitive also borrowed Unity's default material, which is incompatible with this URP rendering path.

The current resource tables/router resolve:

- `mob/fusion_numbuhtwo.kfm` in lair resources (`DongResources_14_14.resourceFile`; also present in `DongResources_15_14.resourceFile`).
- `texture/fusion_numbuhtwo.dds` and `texture/spawn11_green.dds` in `NpcTexture.resourceFile`.

NPC appearance packets may be processed before the destination resource registration finishes. Previously a failed model instantiation left its capsule permanently: NPCs had no visual retry, unlike remote player avatars.

## Correction

NPC visual construction is separated from the server-controlled entity holder and metadata. Missing regular NPC models receive a tagged provisional marker using the existing URP-compatible Legacy shader. They are checked for available resources, with a bounded schedule that builds at most one nearby model per quarter second. Successful recovery retires the marker, reconnects the original animation controller and invalidates selection bounds.

Recovery retains the exact NPC holder, network ID, current HP, position, movement controller/destination and mission state. It sends no respawn, combat or reward packet. Dead or removed NPCs are not restored. Invisible location markers remain functional through their metadata without attempting to display a provisional model. Cache-owned meshes/textures/materials are not explicitly destroyed by recovery; only tagged provisional objects and their private marker materials are released.

## Validation

`NpcRecoveryRegressionChecks` downloads hash-verified current bundles and uses the actual table/model, BundleManager/ResourceRouter, AssetStore, DongBuilder and WorldEntities spawn path. It reproduces spawning before resource registration, then loads the resources with the same shared-assets registration rule as DongLoader. It verifies recovery, unchanged holder/HP/position/movement target, no recovery of a dead NPC, original skinned meshes/materials/animation, retired markers and idempotence.

`NpcRecoveryVisualProof` runs in Unity Play Mode and renders the recovered boss with the actual models and textures. GPU capture: 54,272 opaque pixels and zero error-magenta pixels. Image and logs are ignored under `Logs/npc-recovery/`.

Full configured regression suite and Android packaging passed in `Logs/npc-recovery/build.log` (0 errors, 17 warnings). The replacement APK installed successfully on AynThor without clearing app data and PocketActivity was reopened. Local artifact: `Builds/OpenFusion-Pocket-npc-recovery-preview.apk`, 116,946,229 bytes; SHA-256 `c75fa6deb54f9516ec8f27d26cab352129a6ba4aa3fe2c7d1dfba5b95cfa1384`.

Live Android validation on 2026-10-25: `device-after.txt` records the premature spawn at 11:13:58.026, map readiness at 11:13:58.940 and successful recovery at 11:13:59.002 (62 ms after map readiness), for NPC type 7, network ID 2147354362 and HP 1407. The user then confirmed that Fusion Numbuh Two appears complete on AynThor. Animation clips are covered by the automated regression; the user's visual confirmation specifically establishes the complete model appearance. No GitHub publication was performed.
