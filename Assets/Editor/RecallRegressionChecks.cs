using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class RecallRegressionChecks
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        static void I(byte[] b, int o, int n) => BitConverter.GetBytes(n).CopyTo(b, o);
        static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static IList List(object obj, string name) => (IList)obj.GetType().GetField(name, Private).GetValue(obj);
        static void Property(object obj, string name, object value) => obj.GetType().GetProperty(name).GetSetMethod(true).Invoke(obj, new[] { value });
        static void Dispatch(PacketDispatcher d, uint type, byte[] body) { var p = ClientPackets.New(type, body.Length); body.CopyTo(p, 4); d.Dispatch(p); }
        public static void Batch() { try { Run(); EditorApplication.Exit(0); } catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); } }
        public static void Run()
        {
            var host = new GameObject("Recall regression"); var sender = NpcUI.Sender;
            try {
                RaceSystem.ResetState();
                var d = new PacketDispatcher(); var world = host.AddComponent<WorldEntities>();
                var self = new GameObject("Self"); self.transform.SetParent(host.transform); self.transform.position = new Vector3(1, 2, 3);
                Property(world, "SelfId", 77); Property(world, "Self", self);
                var hud = host.AddComponent<PlayerHud>(); hud.Data.Hp = 100; hud.Data.ActiveNanoSlot = 0;
                hud.Data.NanoSlots[0] = 4; hud.Data.NanoStamina[0] = 150;
                var tables = new XdtTableScript {
                    m_pNanoTable = new NanoTableScript { m_pNanoData = new NanoElement[5] },
                    m_pSkillTable = new SkillTableScript { m_pSkillData = new SkillElement[10] }
                };
                tables.m_pNanoTable.m_pNanoData[4] = new NanoElement { m_iNanoNumber = 4, m_iNanoBattery1 = 150 };
                var skill = new SkillElement { m_iSkillNumber = 9, m_iSkillType = 27, m_iBatteryDrainType = 1, m_iEffectType = 1, m_iEffectTarget = 2, m_iTargetNumber = 1, m_iCoolTime = 100 };
                tables.m_pSkillTable.m_pSkillData[9] = skill;
                var wheel = host.AddComponent<NanoWheelHud>(); wheel.Init(hud, d, tables);
                ((short[])typeof(NanoWheelHud).GetField("bankSkill", Private).GetValue(wheel))[4] = 9;
                Call(wheel, "Refresh");
                // Let the initial default cooldown elapse without depending on editor uptime.
                var slots = (Array)typeof(NanoWheelHud).GetField("slots", Private).GetValue(wheel);
                void Ready() => slots.GetValue(0).GetType().GetField("CoolCur").SetValue(slots.GetValue(0), Time.time - 100);
                Ready();
                var social = SocialMenu.Ensure(host); social.Init(d, world, tables);
                var boxes = SysMessageBox.Ensure(host); boxes.Init(tables);
                var warp = WarpFlow.Ensure(host); Call(warp, "Bind", d);
                var race = RaceSystem.Ensure(host); race.Init(d, world, tables, hud);
                var nano = host.AddComponent<NanoSystem>(); nano.Init(world, d, tables, hud, wheel, null, null, null);
                var sent = new List<byte[]>(); NpcUI.Sender = p => sent.Add(p);
                void ClearNotices() { Call(social, "Update"); List(social, "pops").Clear(); Call(boxes, "Update"); List(boxes, "list").Clear(); }
                nano.UseSkill(); Call(boxes, "Update");
                Check(sent.Count == 0 && SysMessageBox.Active && !wheel.IsCooling(0), "Recall outside instances must explain the restriction without sending or consuming cooldown.");
                ClearNotices();
                var instance = new byte[60]; I(instance, 0, 8); I(instance, 16, 100); I(instance, 24, 100);
                Dispatch(d, PacketIds.P_FE2CL_INSTANCE_MAP_INFO, instance); Call(race, "Update");
                nano.UseSkill(); Call(boxes, "Update");
                Check(sent.Count == 0 && SysMessageBox.Active && RaceSystem.RecallMapId == 0, "Instance entry alone must not invent a registered point."); ClearNotices();
                RaceSystem.RegisterRecall(123); RaceSystem.RegisterRecall(123);
                Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_REGIST_RXCOM && RaceSystem.RecallMapId == 0, "Registration must wait for server confirmation and suppress duplicate in-flight requests."); sent.Clear();
                Dispatch(d, PacketIds.P_FE2CL_REP_REGIST_RXCOM, new byte[4]); Call(race, "Update");
                Check(RaceSystem.RecallMapId == 0, "A truncated registration response must not enable Recall.");
                var point = new byte[16]; I(point, 0, 8); I(point, 4, 10000); I(point, 8, 20000); I(point, 12, 30000);
                Dispatch(d, PacketIds.P_FE2CL_REP_REGIST_RXCOM, point); Call(race, "Update"); ClearNotices();
                Check(RaceSystem.RecallMapId == 8, "RXCOM response stores the map number, not an NPC ID.");
                RaceSystem.RegisterRecall(123); Check(sent.Count == 0, "A confirmed registration at the same NPC must not resend.");
                foreach (int type in new[] { 27, 28 }) {
                    skill.m_iSkillType = type; Ready(); nano.UseSkill();
                    Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_NANO_SKILL_USE && sent[0].Length == 28 && BitConverter.ToInt32(sent[0], 8) == 77 && BitConverter.ToInt32(sent[0], 20) == 1 && BitConverter.ToInt32(sent[0], 24) == 77, "Recall/Group Recall must send Legacy's own-PC target and argument, not a predicted destination or nearby-player list.");
                    Check(WarpFlow.Blocking && wheel.IsCooling(0) && self.transform.position == new Vector3(1, 2, 3) && hud.Data.NanoStamina[0] == 150, "Request must block motion and start cooldown while leaving position/energy authoritative.");
                    nano.UseSkill(); Check(sent.Count == 1, "Repeated skill input during flight/cooldown must not duplicate requests."); sent.Clear();
                    Dispatch(d, PacketIds.P_FE2CL_REP_WARP_USE_RECALL_FAIL, new byte[4]); Call(warp, "Update"); Call(social, "Update");
                    Check(!WarpFlow.Blocking, "Recall rejection must release movement.");
                }
                Ready(); nano.UseSkill(); sent.Clear();
                var gotoBody = new byte[12]; I(gotoBody, 0, 1000);
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_GOTO_SUCC, gotoBody); Call(warp, "Update"); Call(social, "Update");
                Check(!WarpFlow.Blocking && RaceSystem.InsMap && RaceSystem.RecallMapId == 8, "GOTO confirms relocation without clearing same-instance registration.");
                WarpFlow.Sent(); Set(warp, "awaitUntil", Time.unscaledTime - 1);
                Check(!WarpFlow.Blocking, "A silent server must not lock movement indefinitely.");
                var combat = host.AddComponent<CombatSystem>(); Set(combat, "ents", world);
                var offer = new byte[60]; I(offer, 0, 88); I(offer, 28, 28); I(offer, 32, 1); I(offer, 36, 1); I(offer, 40, 77);
                void DeliverOffer(byte[] body) {
                    Call(combat, "OnPacket", PacketIds.P_FE2CL_NANO_SKILL_USE, body);
                    var queued = (System.Collections.Concurrent.ConcurrentQueue<Action>)typeof(CombatSystem).GetField("q", Private).GetValue(combat);
                    while (queued.TryDequeue(out var action)) action();
                    Call(social, "Update");
                }
                var partial = new byte[44]; Array.Copy(offer, partial, 44); DeliverOffer(partial);
                Check(!SocialMenu.RecallPending, "A partial Move result must not open a group invitation.");
                DeliverOffer(offer); DeliverOffer(offer);
                Check(SocialMenu.RecallPending && List(social, "pops").Count == 1, "Repeated group result must create one prompt.");
                Call(social, "Resolve", List(social, "pops")[0], true);
                Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_WARP_USE_RECALL && BitConverter.ToInt32(sent[0], 4) == 88 && !SocialMenu.RecallPending && WarpFlow.Blocking, "Accept must send caster ID once and await authoritative relocation."); sent.Clear();
                Dispatch(d, PacketIds.P_FE2CL_REP_WARP_USE_RECALL_FAIL, new byte[4]); Call(warp, "Update"); Call(social, "Update");
                social.Notice("Older notice"); social.OfferRecall(88); Call(social, "Update");
                var prompt = List(social, "pops")[1]; prompt.GetType().GetField("Deadline").SetValue(prompt, Time.unscaledTime - 1);
                Call(social, "Update");
                Check(!SocialMenu.RecallPending && sent.Count == 1 && BitConverter.ToInt32(sent[0], 4) == 0, "A group invitation must time out and decline even behind an older modal."); sent.Clear(); ClearNotices();
                social.OfferRecall(88); Call(social, "Update");
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_GOTO_SUCC, gotoBody); Call(social, "Update");
                Check(!SocialMenu.RecallPending && List(social, "pops").Count == 0 && sent.Count == 0, "Relocation must discard stale invitation without replying to a previous instance.");
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_WARP_USE_NPC_SUCC, gotoBody); Call(race, "Update");
                Check(!RaceSystem.InsMap && RaceSystem.RecallMapId == 0, "Leaving an instance must clear registration.");
            } finally { UnityEngine.Object.DestroyImmediate(host); NpcUI.Sender = sender; RaceSystem.ResetState(); WarpFlow.Cancel(); }
            Directory.CreateDirectory("Logs/legacy-audit");
            File.WriteAllText("Logs/legacy-audit/recall-checks.txt", "PASS: instance/point gates, server-only registration, malformed response, duplicate suppression, both skill request layouts, cooldown, no predicted movement/energy, rejection/GOTO/timeout unlock, group accept/expiry/stale prompt cleanup. Synthetic packets only.\n");
            Debug.Log("[recall] regression passed");
        }
    }
}
