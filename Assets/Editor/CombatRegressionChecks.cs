using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    public static class CombatRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static int I(byte[] b, int o) => BitConverter.ToInt32(b, o);
        static void Put(byte[] b, int o, int v) => BitConverter.GetBytes(v).CopyTo(b, o);
        static void Hit(byte[] b, int o, int ct, int id, int damage, int hp, int protect = 0)
        { Put(b, o, ct); Put(b, o + 4, id); Put(b, o + 8, protect); Put(b, o + 12, damage); Put(b, o + 16, hp); }
        public static void Run()
        {
            foreach (float height in new[] { 120f, 71.25f, 143.5f })
            {
                var pos = new Vector3(6912f, height, 1792f); var vel = new Vector3(1f, -2.5f, 3f);
                foreach (var pkt in new[] { PlayerMotionPackets.Move(1, pos, vel, 0, 1, 600), PlayerMotionPackets.Stop(1, pos), PlayerMotionPackets.Jump(1, pos, vel, 0, 1, 600) })
                    Check(I(pkt, 12) == 691200 && I(pkt, 16) == 179200 && I(pkt, 20) == Mathf.RoundToInt(height * 100), "Movement retained initial height instead of current height");
                var move = PlayerMotionPackets.Move(1, pos, vel, 0, 1, 600);
                Check(BitConverter.ToSingle(move, 32) == -2.5f && BitConverter.ToSingle(move, 28) == 3f, "MOVE vertical velocity/axis mapping");
                var jump = PlayerMotionPackets.Jump(1, pos, vel, 0, 1, 600);
                Check(I(jump, 32) == -250 && I(jump, 28) == 300, "JUMP vertical velocity/axis mapping");
            }
            GameObject root = null, mob = null;
            var clips = new List<AnimationClip>();
            try
            {
                root = new GameObject("CombatRegression"); mob = new GameObject("MobRegression");
                var info = mob.AddComponent<NpcInfo>(); info.Id = 901; info.Hp = info.MaxHp = 100; info.Name = "Regression mob";
                var model = new GameObject("Model"); model.transform.SetParent(mob.transform);
                var bone = new GameObject("Bone"); bone.transform.SetParent(model.transform);
                var animation = model.AddComponent<Animation>();
                foreach (string name in new[] { "stand1", "wound", "death" })
                {
                    var clip = new AnimationClip { legacy = true, name = name };
                    clip.SetCurve("Bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, .1f));
                    animation.AddClip(clip, name); clips.Add(clip);
                }
                var animator = mob.AddComponent<NpcAnimator>(); animator.Init(model, null, null);
                var entities = root.AddComponent<WorldEntities>();
                var npcs = (Dictionary<int, GameObject>)typeof(WorldEntities).GetField("npcs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(entities);
                npcs[info.Id] = mob;
                var dispatcher = new PacketDispatcher(); var combat = root.AddComponent<CombatSystem>(); combat.Enabled = false; combat.Init(entities, dispatcher, null);
                CheckWeaponSwitch(combat, dispatcher);
                var update = typeof(WorldEntities).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Action<int, int, bool> deliver = (hp, damage, blocked) =>
                {
                    var packet = ClientPackets.New(PacketIds.P_FE2CL_PC_ATTACK_NPCs, 8 + 48);
                    Put(packet, 4, 77); Put(packet, 8, 2);
                    // First result is a PLAYER with the same numeric ID as the mob.
                    Hit(packet, 12, 1, 901, 900, 0);
                    Hit(packet, 36, 0, 901, damage, hp, blocked ? 1 : 0);
                    packet[56] = 2; packet[57] = 255; // signed byte + padding must not alter HP.
                    dispatcher.Dispatch(packet); update.Invoke(entities, null);
                    Check(info.Hp == hp, "Mob HP did not match server result");
                    if (hp > 0) Check(!CombatSystem.Killed.ContainsKey(901), "Player ID collision marked live mob dead");
                };
                deliver(60, 40, false); deliver(60, 0, true); deliver(15, 45, false); deliver(0, 15, false);
                Check(animator.Dying && animator.DeathRemaining() >= .5f, "Legacy eCT=0 did not start death animation");
                Check(CombatSystem.Killed.ContainsKey(901), "Missing server-confirmed mob death"); CombatSystem.Killed.TryRemove(901, out _);
                // Explosion header is 24 bytes, not the rifle header's eight bytes.
                info.Hp = 100;
                var rocket = ClientPackets.New(PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT, 24 + 24);
                Put(rocket, 4, 77); Put(rocket, 24, 1); Hit(rocket, 28, 4, 901, 85, 15);
                dispatcher.Dispatch(rocket); update.Invoke(entities, null); Check(info.Hp == 15, "Rocket HP offset");
                // Reject a truncated list before changing any mob.
                var malformed = ClientPackets.New(PacketIds.P_FE2CL_PC_ATTACK_NPCs, 8 + 24);
                Put(malformed, 4, 77); Put(malformed, 8, 2); Hit(malformed, 12, 4, 901, 15, 0);
                dispatcher.Dispatch(malformed); update.Invoke(entities, null);
                Check(dispatcher.MalformedCount == 1 && info.Hp == 15 && !CombatSystem.Killed.ContainsKey(901), "Truncated combat list applied partial damage/death");
                foreach (int stride in new[] { 20, 32 })
                { var skill = new byte[36 + stride]; Put(skill, 32, 1); Hit(skill, 36, 4, 901, 10, 5); Check(AttackResults.Read(skill, 32, 36, stride)[0].Hp == 5, "Nano result stride"); }
                var legacy = new byte[32]; Put(legacy, 4, 1); Hit(legacy, 8, 0, 901, 15, 0);
                var legacyHit = AttackResults.Read(legacy, 4, 8)[0];
                Check(legacyHit.TargetsNpc(true) && !legacyHit.TargetsNpc(), "Legacy type accepted outside dedicated NPC packet");
                info.Hp = 100; animator.Dying = false;
                var numbers = (System.Collections.IList)typeof(FloatingPoints).GetField("pts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                int numberCount = numbers.Count;
                var compatibility = ClientPackets.New(PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT, 24 + 48);
                Put(compatibility, 4, 77); Put(compatibility, 24, 2);
                Hit(compatibility, 28, 1, 901, 999, 0); // Same ID belongs to a player: never apply it to NPC.
                Hit(compatibility, 52, 0, 901, 40, 60);
                dispatcher.Dispatch(compatibility); update.Invoke(entities, null);
                Check(info.Hp == 60 && !animator.Dying && !CombatSystem.Killed.ContainsKey(901), "Compatibility rocket damage rejected or player collision accepted");
                if (GameOptions.Cur.FloatNumber) Check(numbers.Count == numberCount + 1, "Compatibility explosion did not produce its damage number");
                var mixed = ClientPackets.New(PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT, 48);
                Put(mixed, 4, 77); Put(mixed, 24, 1); Hit(mixed, 28, 0, 901, 60, 0);
                dispatcher.Dispatch(mixed); update.Invoke(entities, null);
                Check(info.Hp == 60 && !CombatSystem.Killed.ContainsKey(901), "Native mixed-target rocket packet accepted unknown entity type");
                var tick = ClientPackets.New(PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_TICK, 32);
                Put(tick, 4, 2); Put(tick, 8, 901); Put(tick, 12, 19); Hit(tick, 16, 2, 901, 15, 45);
                dispatcher.Dispatch(tick); update.Invoke(entities, null);
                Check(info.Hp == 45 && !animator.Dying, "NPC poison tick lost authoritative damage/reaction");
                bool exitSawDeath = false;
                dispatcher.NpcExit += id => entities.Enqueue(() => { exitSawDeath = info.Hp == 0 && CombatSystem.Killed.ContainsKey(id); npcs.Remove(id); });
                var lethal = ClientPackets.New(PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT, 48); Put(lethal, 4, 77); Put(lethal, 24, 1); Hit(lethal, 28, 0, 901, 60, 0);
                dispatcher.Dispatch(lethal);
                var exit = ClientPackets.New(PacketIds.P_FE2CL_NPC_EXIT, 4); Put(exit, 4, 901); dispatcher.Dispatch(exit);
                update.Invoke(entities, null); update.Invoke(entities, null);
                Check(exitSawDeath, "NPC_EXIT overtook lethal damage/death playback");
                Check(animator.Dying && animator.DeathRemaining() >= .5f, "Compatibility explosion did not start mob death animation");
                File.WriteAllText("Logs/combat-regression-results.txt", "PASS: current elevation in MOVE/STOP/JUMP; legacy eCT=0 damage/block/death and death animation started; player/mob ID collision rejected; mixed-target packets require explicit NPC type; explosion header; skill strides; malformed packet atomic rejection; lethal result applied before NPC_EXIT.\n");
            }
            finally { CombatSystem.Killed.TryRemove(901, out _); if (root != null) UnityEngine.Object.DestroyImmediate(root); if (mob != null) UnityEngine.Object.DestroyImmediate(mob); foreach (var clip in clips) UnityEngine.Object.DestroyImmediate(clip); }
        }
        static void CheckWeaponSwitch(CombatSystem combat, PacketDispatcher dispatcher)
        {
            var sender = NpcUI.Sender; const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var appearance = new PcAppearance(); appearance.Equip[0] = new ItemBase { Id = 287 }; appearance.Equip[7] = new ItemBase { Id = 399 };
            var appField = typeof(CombatSystem).GetField("selfApp", flags); appField.SetValue(combat, appearance);
            var change = typeof(CombatSystem).GetMethod("SwitchWeapon", flags); var tick = typeof(CombatSystem).GetMethod("LateUpdate", flags);
            var sent = new List<byte[]>(); NpcUI.Sender = p => sent.Add(p);
            try
            {
                change.Invoke(combat, null); change.Invoke(combat, null);
                Check(sent.Count == 1 && I(sent[0], 4) == 0 && I(sent[0], 8) == 7 && I(sent[0], 12) == 0 && I(sent[0], 16) == 0, "Weapon switch request layout or duplicate guard");
                var unrelated = ClientPackets.New(PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC, 40);
                Put(unrelated, 4, 1); Put(unrelated, 8, 3); Put(unrelated, 24, 1); Put(unrelated, 28, 4);
                dispatcher.Dispatch(unrelated); tick.Invoke(combat, null);
                Check(appearance.Equip[0].Id == 287 && appearance.Equip[7].Id == 399, "Unrelated inventory acknowledgement swapped weapons locally");
                change.Invoke(combat, null); Check(sent.Count == 1, "Unrelated acknowledgement released pending weapon switch");
                var ack = ClientPackets.New(PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC, 40);
                Put(ack, 4, 0); Put(ack, 8, 0); BitConverter.GetBytes((short)399).CopyTo(ack, 14);
                Put(ack, 24, 0); Put(ack, 28, 7); BitConverter.GetBytes((short)287).CopyTo(ack, 34);
                dispatcher.Dispatch(ack); tick.Invoke(combat, null);
                Check(appearance.Equip[0].Id == 399 && appearance.Equip[7].Id == 287, "Server-confirmed reversed slot order not applied");
                change.Invoke(combat, null); Check(sent.Count == 2, "Confirmed switch did not release input guard");
                File.WriteAllText("Logs/weapon-switch-regression-results.txt", "PASS: one request while pending; Legacy slots 7/0; unrelated inventory ACK never swaps weapons; reversed server slot order applied; next switch enabled after confirmation.\n");
            }
            finally { NpcUI.Sender = sender; appField.SetValue(combat, null); }
        }
    }
}
