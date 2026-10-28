using System;
using System.Reflection;
using OpenFusion.Tables.Gen;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class CombatFeedbackRegressionChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void I(byte[] b, int o, int value) => BitConverter.GetBytes(value).CopyTo(b, o);
        static void S(byte[] b, int o, short value) => BitConverter.GetBytes(value).CopyTo(b, o);
        static byte[] Skill(int type, int stride) { var b = new byte[28 + stride]; S(b, 4, 15); I(b, 20, type); I(b, 24, 1); I(b, 28, 1); I(b, 32, 77); return b; }
        public static void Run()
        {
            var a = new PcAppearance(); a.Equip[0] = new ItemBase { Type = 0, Id = 12, Opt = (42 << 16) | 7 };
            a.Equip[1] = new ItemBase { Type = 1, Id = 8, Opt = 43 << 16 }; a.Equip[4] = new ItemBase { Type = 4, Id = 9, Opt = 44 << 16 };
            Check(a.VisibleEquip().Weapon == 42 && a.VisibleEquip().Shirt == 43 && a.VisibleEquip().Hat == 9, "Croc Pot appearance must be applied only to supported item categories");
            Check(a.VisibleEquip(false).Weapon == 12 && a.Equip[0].Id == 12 && (a.Equip[0].Opt & 65535) == 7, "Cosmetic appearance changed weapon stats/enchantment identity");
            string outfit = PlayerAvatarFactory.OutfitSignature(a);
            a.Equip[1].Opt |= 7; Check(PlayerAvatarFactory.OutfitSignature(a) == outfit, "Enchantment-only changes must not rebuild the outfit");
            a.Equip[1].Opt = 44 << 16; Check(PlayerAvatarFactory.OutfitSignature(a) != outfit, "A Croc Pot style change with the same stat item must refresh the outfit");
            var d = new HudData { Hp = 400, ActiveNanoSlot = 0, NanoBankStamina = new short[40] }; d.NanoSlots[0] = 4;
            var lethal = Skill(1, 20); I(lethal, 40, 400); I(lethal, 44, 0);
            Check(SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, lethal, 77, d, out _) && d.Hp == 0, "Lethal enemy skill failed to enter local death state");
            d.Hp = 400; I(lethal, 24, 2);
            Check(!SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, lethal, 77, d, out _) && d.Hp == 400, "Truncated result list partially changed player health");
            I(lethal, 24, 1); I(lethal, 32, 78);
            Check(!SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, lethal, 77, d, out _) && d.Hp == 400, "Another player's result affected local health");
            var drain = Skill(21, 40); I(drain, 44, 123); I(drain, 52, 234); S(drain, 56, 0); I(drain, 60, 1); I(drain, 64, 64);
            Check(SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, drain, 77, d, out int flags) && d.Hp == 400 && d.BatteryWpn == 123 && d.BatteryNano == 234 && d.ActiveNanoSlot == -1 && d.NanoStamina[0] == 0 && flags == 64, "Drain misread batteries as HP or ignored Nano depletion");
            var debuff = Skill(8, 32); I(debuff, 44, 10); S(debuff, 48, 23); I(debuff, 52, 0); I(debuff, 56, 128); d.ActiveNanoSlot = 0;
            Check(SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, debuff, 77, d, out flags) && d.Hp == 10 && d.NanoBankStamina[4] == 23 && flags == 128, "Damage/debuff result lost health, stamina or condition flags");
            var heal = new byte[52]; I(heal, 0, 88); I(heal, 28, 2); I(heal, 32, 1); I(heal, 36, 1); I(heal, 40, 77); I(heal, 44, 90); I(heal, 48, 100);
            Check(SkillResults.ApplyPacket(PacketIds.P_FE2CL_NANO_SKILL_USE, heal, 77, d, out _) && d.Hp == 100, "Another player's healing skill did not update local HP");
            var stamina = Skill(6, 16); S(stamina, 38, 4); S(stamina, 42, 150);
            Check(SkillResults.ApplyPacket(PacketIds.P_FE2CL_NPC_SKILL_HIT, stamina, 77, d, out _) && d.NanoStamina[0] == 150 && d.Hp == 100, "Nano stamina heal was ignored or misread as HP");
            d.NanoSlots[1] = 1; d.NanoSlots[2] = 5;
            var regen = new byte[48]; I(regen, 0, 500); S(regen, 20, 2); S(regen, 22, 4); S(regen, 26, 75); S(regen, 28, 1); S(regen, 32, 76); S(regen, 34, 5); S(regen, 38, 77); I(regen, 44, 12345);
            Check(HudPackets.ApplyRegenSucc(regen, d, true) && d.Hp == 500 && d.ActiveNanoSlot == 2 && d.NanoSlots[2] == 5 && d.NanoStamina[2] == 77 && d.NanoBankStamina[4] == 75 && d.FusionMatter == 12345, "Respawn failed to restore server HP, Nano identities/energy and Fusion Matter");
            // Retrobution/Legacy revival may supply stamina-only entries. Equipment is
            // retained from ENTER/EQUIP, never reconstructed from these unused IDs.
            for (int i = 0; i < 3; i++) { S(regen,22+i*6,0); S(regen,24+i*6,0); }
            Check(HudPackets.ApplyRegenSucc(regen,d,true) && d.HasActiveNano && d.NanoSlots[0]==4 && d.NanoSlots[1]==1 && d.NanoSlots[2]==5 && d.NanoBankStamina[5]==77,
                "Stamina-only respawn must retain equipped Nano identities and a valid active HUD card.");
            S(regen,20,3); HudPackets.ApplyRegenSucc(regen,d,false);
            Check(!d.HasActiveNano && d.ActiveNanoSlot==-1,"Out-of-range respawn slots must not draw a ghost Nano.");
            d.NanoSlots[2]=0; S(regen,20,2); HudPackets.ApplyRegenSucc(regen,d,false);
            Check(!d.HasActiveNano && d.ActiveNanoSlot==-1,"Respawn active slot pointing to an empty slot must be dismissed.");
            d.NanoSlots[2]=5; S(regen,20,-1); HudPackets.ApplyRegenSucc(regen,d,false);
            Check(!d.HasActiveNano && d.NanoSlots[2]==5,"A server-dismissed Nano must retain equipment without showing an active card.");
            d.ActiveNanoSlot=0; var cut=new byte[47]; I(cut,0,999);
            Check(!HudPackets.ApplyRegenSucc(cut,d,true)&&d.Hp==500&&d.ActiveNanoSlot==0,"A truncated respawn must not partially mutate the HUD.");
            var req = PlayerLife.RespawnRequest(31);
            Check(req.Length == 16 && BitConverter.ToUInt32(req, 0) == PacketIds.P_CL2FE_REQ_PC_REGEN && BitConverter.ToInt32(req, 4) == 1 && BitConverter.ToInt32(req, 12) == 31, "Respawn request must use normal revival rather than Warp Away");
            Check(BitConverter.ToInt32(PlayerLife.RespawnRequest(0, 3), 4) == 3 && BitConverter.ToInt32(PlayerLife.RespawnRequest(0, 4), 4) == 4, "Phoenix revival packet types are incorrect");
            bool rejected = false; try { PlayerLife.RespawnRequest(0, 5); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Death UI must never issue Warp Away as a revival");
            var host = new GameObject("Cooldown regression");
            try {
                var owned = host.AddComponent<AvatarGeneratedAssets>(); owned.Owner = owned.GetInstanceID();
                var borrowed = UnityEngine.Object.Instantiate(host);
                try { Check(owned.OwnsResources && !borrowed.GetComponent<AvatarGeneratedAssets>().OwnsResources, "Portrait clones must not destroy live avatar meshes/materials"); }
                finally { UnityEngine.Object.DestroyImmediate(borrowed); }
                var hud = host.AddComponent<PlayerHud>(); var wheel = host.AddComponent<NanoWheelHud>();
                Check(wheel.DrawNanoInfoCooldown && hud.ShowNanoInfo, "Active Nano cooldown display disabled by default");
                var flagsBinding = BindingFlags.Instance | BindingFlags.NonPublic;
                var tables = new XdtTableScript { m_pNanoTable = new NanoTableScript { m_pNanoData = new NanoElement[5] }, m_pSkillTable = new SkillTableScript { m_pSkillData = new SkillElement[10] } };
                tables.m_pNanoTable.m_pNanoData[4] = new NanoElement { m_iNanoNumber = 4, m_iNanoBattery1 = 150 };
                tables.m_pSkillTable.m_pSkillData[9] = new SkillElement { m_iSkillNumber = 9, m_iBatteryDrainType = 1, m_iCoolTime = 100 };
                wheel.Init(hud, new PacketDispatcher(), tables); hud.Data.NanoSlots[0] = 4;
                typeof(NanoWheelHud).GetField("selfId", flagsBinding).SetValue(wheel, 77);
                ((short[])typeof(NanoWheelHud).GetField("bankSkill", flagsBinding).GetValue(wheel))[4] = 9;
                var ack = new byte[36]; I(ack, 0, 77); S(ack, 6, 9); S(ack, 24, 4); S(ack, 26, 140);
                typeof(NanoWheelHud).GetMethod("Handle", flagsBinding).Invoke(wheel, new object[] { PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC, ack });
                Check(wheel.CooldownRemaining(0) > 9, "Server-triggered skill acknowledgement must start the active-slot cooldown too");
                var revival=new byte[48]; I(revival,0,500); S(revival,20,0); S(revival,26,75);
                HudPackets.ApplyRegenSucc(revival,hud.Data,true);
                typeof(NanoWheelHud).GetMethod("Handle",flagsBinding).Invoke(wheel,new object[]{PacketIds.P_FE2CL_REP_PC_REGEN_SUCC,revival});
                Check(hud.Data.NanoSlots[0]==4 && hud.Data.HasActiveNano && wheel.BankSkill(4)==9 && hud.Data.NanoStamina[0]==75,
                    "Revival must preserve the chosen skill used by the active Nano icon.");
                var nanoTick=new byte[32]; I(nanoTick,0,500); S(nanoTick,8,80);
                HudPackets.ApplyTick(nanoTick,hud.Data);
                typeof(NanoWheelHud).GetMethod("Handle",flagsBinding).Invoke(wheel,new object[]{PacketIds.P_FE2CL_REP_PC_TICK,nanoTick});
                Check(wheel.BankSkill(4)==9 && hud.Data.NanoSlots[0]==4 && hud.Data.NanoStamina[0]==80,"Subsequent stamina-only ticks must not erase the Nano power.");
                var client = host.AddComponent<OpenFusion.GameClient>();
                var body = new GameObject("Life animation fixture"); body.transform.SetParent(host.transform); var anim = body.AddComponent<Animation>();
                var clips = new System.Collections.Generic.List<AnimationClip>();
                try {
                    foreach (string name in new[] { "die", "death", "stand1" }) {
                        var clip = new AnimationClip { legacy = true, name = name }; clip.SetCurve("", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, 0)); anim.AddClip(clip, name); clips.Add(clip);
                    }
                    hud.Data.Hp = 0; var tick = typeof(OpenFusion.GameClient).GetMethod("TickLifeAnimation", flagsBinding);
                    Check((bool)tick.Invoke(client, new object[] { body.transform }) && anim["die"].wrapMode == WrapMode.ClampForever, "Local HP zero must enter the die animation without walking");
                    typeof(OpenFusion.GameClient).GetField("lifeDieEnd", flagsBinding).SetValue(client, -1f);
                    tick.Invoke(client, new object[] { body.transform });
                    Check((bool)typeof(OpenFusion.GameClient).GetField("lifeRest", flagsBinding).GetValue(client) && anim["death"].wrapMode == WrapMode.Loop, "Die animation must transition to the persistent death pose");
                    hud.Data.Hp = 500;
                    Check(!(bool)tick.Invoke(client, new object[] { body.transform }) && !(bool)typeof(OpenFusion.GameClient).GetField("lifeDead", flagsBinding).GetValue(client), "Authoritative positive HP must release the local death state");
                } finally { anim.Stop(); UnityEngine.Object.DestroyImmediate(body); foreach (var clip in clips) UnityEngine.Object.DestroyImmediate(clip); }
            } finally { UnityEngine.Object.DestroyImmediate(host); }
            Debug.Log("[feedback-regression] Passed: Croc appearance/stat identity, lethal skills, malformed/foreign results, drain, status, ally healing, stamina and authoritative respawn.");
        }
    }
}
