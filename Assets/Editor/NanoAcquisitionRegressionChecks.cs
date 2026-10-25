using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    public static class NanoAcquisitionRegressionChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
        static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
        static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        public static void Batch() { try { Run(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
        public static void BatchPreview()
        {
            try {
                Run();
                bool ok = BuildScript.Build(false, "Builds/OpenFusion-Pocket-nano-acquisition-preview.apk", new[] { "POCKET_INPUT_PREVIEW" });
                EditorApplication.Exit(ok ? 0 : 1);
            } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void Run()
        {
            Directory.CreateDirectory("Logs/nano-acquisition");
            Check(UiKit.Tex("NanoPopUpNextNano_1") != null, "Initial selection must use CnGuiNanoFreeTuning's actual transparent popup binding, not My Stuff's portrait variant.");
            foreach (string style in new[] { "bigblue", "bigyellow", "TransparentLightBlue", "TransparentYellowSmall", "TransparentBlue", "button" })
                Check(Skin.Get(Skin.Nano, style)?.Font != null, "Missing Legacy acquisition bitmap font/style: " + style);
            var owned = (short[])InventoryWindow.NanoOwned.Clone(); var skills = (short[])InventoryWindow.NanoSkill.Clone();
            var sender = NpcUI.Sender; var previous = InventoryWindow.Instance; bool registered = GameMenu.Implemented.Contains(GameMenu.Win.MyStuff);
            GameObject host = null, player = null;
            try {
                host = new GameObject("Nano acquisition regression"); player = new GameObject("Player");
                var entities = host.AddComponent<WorldEntities>();
                typeof(WorldEntities).GetProperty("Self").GetSetMethod(true).Invoke(entities, new object[] { player });
                var hud = host.AddComponent<PlayerHud>(); hud.Data.Hp = 100; hud.Data.Level = 1; hud.Data.FusionMatter = 1234;
                var table = new XdtTableScript { m_pNanoTable = new NanoTableScript { m_pNanoData = new NanoElement[4], m_pNanoTuneData = new NanoTuneElement[4] } };
                for (int i = 1; i < 4; i++) {
                    table.m_pNanoTable.m_pNanoData[i] = new NanoElement { m_iNanoNumber = i, m_iTune = new[] { 1, 2, 3 } };
                    table.m_pNanoTable.m_pNanoTuneData[i] = new NanoTuneElement { m_iSkillID = i * 10, m_iReqItemCount = 5, m_iReqItemID = 99 };
                }
                var inv = InventoryWindow.Ensure(host); Call(inv, "Awake"); inv.Init(null, entities, table, hud, null);
                Array.Clear(InventoryWindow.NanoOwned, 0, owned.Length); Array.Clear(InventoryWindow.NanoSkill, 0, skills.Length);
                int sends = 0; byte[] sent = null; NpcUI.Sender = p => { sends++; sent = p; };
                var created = new byte[28]; created[20] = 2; created[24] = 150; created[26] = 2;
                Call(inv, "NanoPacket", PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC, created); Call(inv, "NanoUpdate");
                Check(InventoryWindow.NanoOwned[2] == 2 && InventoryWindow.NanoAcquisitionActive && GameMenu.Blocking && GameMenu.HudHidden, "Real award must open acquisition while retaining authoritative ownership and blocking combat/HUD.");
                Check(!GameMenu.OpenWindow(GameMenu.Win.MyStuff) && !DualScreenController.SecondaryCommandAllowed("nanoTune"), "Hotkeys and DS must not bypass initial power selection.");
                Check(!(bool)Call(inv, "SelectAcquisitionPower", 0) && sends == 0, "A power cannot be sent before the presentation finishes.");
                Set(inv, "acquisitionPhase", 3);
                Check(!(bool)Call(inv, "SelectAcquisitionPower", 99) && sends == 0, "Foreign choices must be rejected.");
                Check((bool)Call(inv, "SelectAcquisitionPower", 1) && sends == 1 && sent.Length == 48 && BitConverter.ToUInt32(sent, 0) == PacketIds.P_CL2FE_REQ_NANO_TUNE && BitConverter.ToInt16(sent, 4) == 2 && BitConverter.ToInt16(sent, 6) == 2, "Initial choice must submit the real Legacy nano/tune IDs and full 44-byte request body.");
                for (int i = 8; i < 48; i += 4) Check(BitConverter.ToInt32(sent, i) == -1, "Free initial tuning must explicitly mark every inventory slot unused; zero is a real paid-tuning slot.");
                Check(InventoryWindow.NanoSkill[2] == 0 && hud.Data.FusionMatter == 1234 && !(bool)Call(inv, "SelectAcquisitionPower", 0) && sends == 1, "No optimistic power/currency changes or duplicate pending requests.");
                Call(inv, "NanoPacket", PacketIds.P_FE2CL_REP_NANO_TUNE_FAIL, new byte[4]); Call(inv, "NanoUpdate");
                Check((bool)Call(inv, "SelectAcquisitionPower", 1) && sends == 2, "Server rejection must permit a new explicit selection.");
                var ack = new byte[] { 2, 0, 20, 0 };
                Call(inv, "NanoPacket", PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC, ack); Call(inv, "NanoUpdate");
                Check(InventoryWindow.NanoSkill[2] == 20 && (int)typeof(InventoryWindow).GetField("acquisitionPhase", Private).GetValue(inv) == 4, "Matching server ACK must update the shared bank and play the chosen skill before exit.");
                Set(inv, "acquisitionAt", -100f); Call(inv, "NanoUpdate");
                Check(!InventoryWindow.NanoAcquisitionActive && !GameMenu.Blocking, "Completion must restore normal world/DS controls.");
                Call(inv, "NanoPacket", PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC, new byte[2]); Call(inv, "NanoUpdate");
                Check(!InventoryWindow.NanoAcquisitionActive, "Truncated awards cannot start an acquisition.");
                // Resume an untuned owned Nano from an actual ENTER_SUCC bank, without issuing a grant.
                var enter = new byte[1800]; int bank = 4 + 1444;
                enter[bank + 6] = 1; enter[bank + 12] = 2; enter[bank + 14] = 20;
                Call(inv, "OnEnterRaw", enter); Call(inv, "NanoUpdate");
                Check(InventoryWindow.NanoAcquisitionActive && (int)typeof(InventoryWindow).GetField("acquiredNano", Private).GetValue(inv) == 1 && sends == 2, "Reconnect must recover an untuned owned Nano, skip tuned ones and send nothing automatically.");
                Call(inv, "ResetAcquisition"); InventoryWindow.NanoOwned[1] = 1; InventoryWindow.NanoSkill[1] = 10;
                Call(inv, "QueueAcquiredNano", 1, false); Call(inv, "NanoUpdate");
                Check(!InventoryWindow.NanoAcquisitionActive, "Queued but already tuned Nanos must not reopen selection.");
            } finally {
                if (host != null) UnityEngine.Object.DestroyImmediate(host); if (player != null) UnityEngine.Object.DestroyImmediate(player);
                Array.Copy(owned, InventoryWindow.NanoOwned, owned.Length); Array.Copy(skills, InventoryWindow.NanoSkill, skills.Length);
                InventoryWindow.Instance = previous; NpcUI.Sender = sender; if (!registered) GameMenu.Implemented.Remove(GameMenu.Win.MyStuff);
            }
            File.WriteAllText("Logs/nano-acquisition/results.txt", "PASS: award presentation, modal/DS input gates, real tune IDs and packet layout, no optimistic power/currency/items, duplicate guard, rejection/retry, server ACK, completion, malformed award, reconnect recovery and already-tuned exclusion.\n");
            Debug.Log("[nano-acquire] regression passed");
        }
    }
}
