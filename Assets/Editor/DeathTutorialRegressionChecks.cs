using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DeathTutorialRegressionChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void I(byte[] b, int offset, int value) => BitConverter.GetBytes(value).CopyTo(b, offset);
        public static void Run()
        {
            var oldClient = GameClient.FindSelf; var oldSender = NpcUI.Sender; var oldHint = FirstUse.Instance;
            long flag1 = FirstUse.Flag1, flag2 = FirstUse.Flag2; bool hints = GameOptions.Cur.GameHint;
            int big = GuideWindow.NextBig, small = GuideWindow.NextSmall; bool guideRegistered = GameMenu.Implemented.Contains(GameMenu.Win.Guide);
            var host = new GameObject("Death tutorial regression"); var body = new GameObject("Authoritative respawn position");
            try {
                var client = host.AddComponent<GameClient>(); GameClient.FindSelf = client;
                var hud = host.AddComponent<PlayerHud>(); var entities = host.AddComponent<WorldEntities>();
                typeof(WorldEntities).GetProperty("Self").SetValue(entities, body);
                var menu = host.AddComponent<GameMenu>(); var dispatcher = new PacketDispatcher(); menu.Init(entities, dispatcher, null);
                var regen = ClientPackets.New(PacketIds.P_FE2CL_REP_PC_REGEN_SUCC, 48);
                // Reference revivePlayer writes bMoveLocation=0 even when relocating to a spawn.
                I(regen, 4, 960); I(regen, 12, 370540); I(regen, 16, 444137); I(regen, 20, -5300); regen[44] = 0;
                body.transform.position = new Vector3(3800, -57, 4500);
                dispatcher.Dispatch(regen); // Before either GameMenu.Update or a positive-HP HUD update.
                var apply = typeof(GameClient).GetMethod("ApplyServerRelocation", Private);
                Check((bool)apply.Invoke(client, new object[] { body.transform }), "REGEN with moveFlag=0 must stage the server's spawn at receipt, before menu Update");
                var expected = new Vector3(3705.4f, -53, 4441.37f);
                Check(Vector3.Distance(body.transform.position, expected) < .01f, "Respawn axes or centimetre conversion changed");
                Check((Vector3)typeof(GameClient).GetField("lastPos", Private).GetValue(client) == body.transform.position, "Warp must reset the movement baseline rather than transmit teleport velocity");
                Check(!(bool)apply.Invoke(client, new object[] { body.transform }), "A relocation must be consumed once");
                regen[44] = 0; dispatcher.Dispatch(regen);
                Check((bool)apply.Invoke(client, new object[] { body.transform }) && Vector3.Distance(body.transform.position, expected) < .01f, "In-place revival must honor unchanged server coordinates");
                regen[44] = 1; I(regen, 12, 370640); dispatcher.Dispatch(regen);
                Check((bool)apply.Invoke(client, new object[] { body.transform }) && Mathf.Abs(body.transform.position.x - 3706.4f) < .01f, "Both move flag values must honor confirmed server coordinates");
                dispatcher.Dispatch(ClientPackets.New(PacketIds.P_FE2CL_REP_PC_REGEN_SUCC, 20));
                Check(!(bool)apply.Invoke(client, new object[] { body.transform }), "Truncated revival data must not move the player");
                var life = host.AddComponent<PlayerLife>(); life.Init(entities, hud); hud.Data.Hp = 0;
                Check(PlayerLife.Active && GameMenu.DeathOnly && !DualScreenController.SecondaryBlocked, "Death alone must retain the secondary DS surface");
                Check(DualScreenController.SecondaryCommandAllowed("pan") && DualScreenController.SecondaryCommandAllowed("nanoSelect") &&
                    !DualScreenController.SecondaryCommandAllowed("nanoCard") && !DualScreenController.SecondaryCommandAllowed("nanoTune"), "Dead players may inspect DS without summoning/tuning Nanos");
                hud.Data.Hp = 960;

                var rows = new FirstUseTableElement[73];
                foreach (int id in new[] { 1, 2, 64, 65, 72 }) rows[id] = new FirstUseTableElement { m_iCompDuration = 60, m_iIconDuration = 60, m_iHelpMain = 3, m_iHelpSub = 2 };
                var table = new XdtTableScript { m_pFirstUseTable = new FirstUseTableScript { m_pFirstUseData = rows } };
                var hint = FirstUse.Ensure(host); hint.Init(dispatcher, table, hud, entities); GameOptions.Cur.GameHint = true;
                var enter = typeof(FirstUse).GetMethod("OnSelfEnter", Private);
                enter.Invoke(hint, new object[] { new EnterSucc() }); int sent = 0; NpcUI.Sender = _ => sent++;
                FirstUse.Check(1); FirstUse.Check(1);
                Check(sent == 1 && FirstUse.Has(1), "Repeated first-use actions must send and enqueue the hint once");
                FirstUse.Check(3); Check(!FirstUse.Has(3), "Missing downloaded hint data must not consume its flag");
                foreach (int id in new[] { 64, 65, 72 }) { FirstUse.Check(id); Check(FirstUse.Has(id), "First-use flag boundary lost: " + id); }
                Check(!FirstUse.Has(0) && !FirstUse.Has(129), "Invalid conditions must not alias valid flag bits");
                GameOptions.Cur.GameHint = false; FirstUse.Check(2); Check(!FirstUse.Has(2), "Disabled hints must not consume new flags");
                GameMenu.Implemented.Add(GameMenu.Win.Guide);
                typeof(FirstUse).GetMethod("Press", Private).Invoke(hint, new object[] { 0 });
                Check(GameMenu.Current == GameMenu.Win.Guide && GuideWindow.NextBig == 3 && GuideWindow.NextSmall == 2, "A first-use icon must open the actual Game Guide at the downloaded topic");
                enter.Invoke(hint, new object[] { new EnterSucc { Quest = new QuestSave { FirstUse1 = 2, FirstUse2 = 1 } } });
                Check(!FirstUse.Has(1) && FirstUse.Has(2) && FirstUse.Has(65), "Server hint flags must replace the previous character's flags");
                enter.Invoke(hint, new object[] { new EnterSucc() }); Check(FirstUse.Flag1 == 0 && FirstUse.Flag2 == 0, "New character must not inherit another character's hint state");
            }
            finally {
                UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(body);
                GameClient.FindSelf = oldClient; NpcUI.Sender = oldSender; FirstUse.Instance = oldHint;
                FirstUse.Flag1 = flag1; FirstUse.Flag2 = flag2; GameOptions.Cur.GameHint = hints;
                GuideWindow.NextBig = big; GuideWindow.NextSmall = small;
                if (!guideRegistered) GameMenu.Implemented.Remove(GameMenu.Win.Guide);
            }
            Debug.Log("[death-tutorial] Passed: immediate authoritative relocation, in-place revival, truncated packets, persistent DS on death, read-only death input, real Guide navigation and per-character hint flags.");
        }
    }
}
