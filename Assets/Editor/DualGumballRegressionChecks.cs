using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DualGumballRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        public static void Run()
        {
            var items = (ItemBase[])InventoryWindow.Items.Clone(); var sender = NpcUI.Sender;
            bool machine = InventoryWindow.Machine, registered = GameMenu.Implemented.Contains(GameMenu.Win.MyStuff);
            var host = new GameObject("DS gumball fixture");
            try
            {
                GameMenu.CloseWindow(); InventoryWindow.Machine = false;
                var hud = host.AddComponent<PlayerHud>(); hud.Data.Hp = 100; hud.Data.ActiveNanoSlot = 0; hud.Data.NanoSlots[0] = 1; hud.Data.NanoSlots[1] = 2;
                var table = new XdtTableScript {
                    m_pNanoTable = new NanoTableScript { m_pNanoData = new NanoElement[3] },
                    m_pGeneralItemTable = new GeneralItemTableScript { m_pItemData = new GeneralItemElement[4] }
                };
                table.m_pNanoTable.m_pNanoData[1] = new NanoElement { m_iStyle = 1 };
                table.m_pNanoTable.m_pNanoData[2] = new NanoElement { m_iStyle = 0 };
                for (int i = 1; i <= 3; i++) table.m_pGeneralItemTable.m_pItemData[i] = new GeneralItemElement { m_iItemType = 3, m_iStimPackAttri = i == 3 ? 4 : i };
                var inv = InventoryWindow.Ensure(host); inv.Init(null, null, table, hud, null);
                var wheel = host.AddComponent<NanoWheelHud>(); wheel.Init(hud, null, table); inv.Wheel = wheel;
                Array.Clear(InventoryWindow.Items, 0, InventoryWindow.Items.Length);
                InventoryWindow.Items[0] = new ItemBase { Type = 7, Id = 1, Opt = 9 };
                InventoryWindow.Items[48] = new ItemBase { Type = 7, Id = 3, Opt = 3 };
                InventoryWindow.Items[49] = new ItemBase { Type = 7, Id = 2, Opt = 2 };
                byte[] sent = null; int sends = 0; NpcUI.Sender = p => { sent = p; sends++; };
                Check(inv.DualGumballSlot(1) == 49 && inv.DualGumballSlot(2) == -1, "Only the active Nano can use this shortcut; prefer its matching style over a universal gumball.");
                InventoryWindow.Items[49] = default;
                Check(inv.DualGumballSlot(1) == 48, "Universal gumballs are a valid fallback; wrong-style items are excluded.");
                InventoryWindow.Items[49] = new ItemBase { Type = 7, Id = 2, Opt = 2 };
                Check(!inv.DualUseGumball(1, 1) && sends == 0, "Reject stale or wrong-style item commands.");
                Check(inv.DualUseGumball(1, 2) && sends == 1 && sent.Length == 16 && BitConverter.ToInt32(sent, 8) == 49 && BitConverter.ToInt16(sent, 12) == 0, "Use the original item packet with the actual inventory and active Nano slots.");
                Check(InventoryWindow.Items[49].Opt == 2 && !inv.DualUseGumball(1, 2) && sends == 1, "Do not decrement inventory optimistically or send duplicate consumption requests.");
                typeof(InventoryWindow).GetField("nextDualGumball", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(inv, -1f);
                hud.Data.Hp = 0; Check(!inv.DualUseGumball(1, 2), "Dead players cannot use the shortcut.");
                hud.Data.Hp = 100; InventoryWindow.Machine = true; Check(!inv.DualUseGumball(1, 2), "Station inspection must not consume items.");
                InventoryWindow.Machine = false; hud.Data.ActiveNanoSlot = 1; Check(!inv.DualUseGumball(1, 2), "Changing the active Nano invalidates the previous shortcut.");
                var digits = InventoryWindow.TarosDigitsRect;
                Check(digits.height > 0 && digits.y >= 574 && digits.yMax < 592, "Taros numbers must remain inside the source texture's cells, above Redeem Code.");
                Debug.Log("[DS polish regression] style/universal gumballs, active-slot/stale/death/station/duplicate guards, authoritative quantity and Taros bounds passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host); Array.Copy(items, InventoryWindow.Items, items.Length);
                NpcUI.Sender = sender; InventoryWindow.Machine = machine;
                if (!registered) GameMenu.Implemented.Remove(GameMenu.Win.MyStuff);
            }
        }
    }
}
