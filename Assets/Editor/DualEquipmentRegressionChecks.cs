using System;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DualEquipmentRegressionChecks
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        public static void Run()
        {
            var saved = (ItemBase[])InventoryWindow.Items.Clone();
            bool registered = GameMenu.Implemented.Contains(GameMenu.Win.MyStuff);
            var oldWindow = GameMenu.Current;
            var host = new GameObject("DS Equipment regression");
            try
            {
                GameMenu.CloseWindow();
                var inv = InventoryWindow.Ensure(host);
                typeof(InventoryWindow).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(inv, null);
                var hud = host.AddComponent<PlayerHud>();
                inv.Init(null, null, null, hud, null);
                hud.Data.Name = "Equipment fixture"; hud.Data.Hp = 37; hud.Data.MaxHp = 100;
                hud.Data.FusionMatter = 45; hud.Data.MaxFusionMatter = 200;
                Array.Clear(InventoryWindow.Items, 0, InventoryWindow.Items.Length);
                var item = new ItemBase { Type = 1, Id = 17, Opt = (25 << 16) | 4, TimeLimit = 123 };
                InventoryWindow.Items[49] = item;
                Set(inv, "selfApp", new PcAppearance { Equip = new ItemBase[9] });
                var state = inv.DualEquipment(texture => "");
                Check(state.inventory.Count == 50 && state.equipped.Count == 9, "DS must expose the full existing inventory and all equipment slots, including legs.");
                Check(state.name == "Equipment fixture" && state.hp == 37 && state.maxHp == 100 && state.fusion == 45 && state.maxFusion == 200, "DS must expose live HUD values rather than mockup numbers.");
                string key = state.inventory[49].key;
                Check(inv.DualItemMatches(49, false, key) && state.inventory[49].id == 17, "Combined item identity must retain real stats ID and appearance Opt.");
                item.Opt++; InventoryWindow.Items[49] = item;
                Check(!inv.DualItemMatches(49, false, key), "Reject a stale touch after the item's options/appearance change.");
                Check(!inv.DualItemMatches(-1, false, key) && !inv.DualItemMatches(50, false, key) && !inv.DualItemMatches(9, true, key), "Reject out-of-bounds slots.");
                InventoryWindow.Items[0] = new ItemBase { Type = 8, Id = 1 };
                Check(!inv.DualItemMatches(0, false, InventoryWindow.DualItemKey(InventoryWindow.Items[0])), "Do not invent an unsupported quest-item action dialog.");
                Check(!inv.DualInspectItem(49, false, InventoryWindow.DualItemKey(item)), "Disconnected fixtures must not open actionable item dialogs.");
                GameMenu.Implemented.Add(GameMenu.Win.MyStuff); Set(inv, "dualItemHost", true);
                GameMenu.OpenWindow(GameMenu.Win.MyStuff);
                Check(GameMenu.EquipmentInfoOnly && !DualScreenController.SecondaryBlocked && GameMenu.Blocking, "An original item preview must retain DS while still blocking world combat/movement.");
                Check(!DualScreenController.SecondaryCommandAllowed("itemInspect") && !DualScreenController.SecondaryCommandAllowed("equipmentRotate"), "The lower panel must not trigger a second item action while an original dialog is open.");
                Set(inv, "dualItemHost", false);
                Check(DualScreenController.SecondaryBlocked, "Ordinary My Stuff must retain its original full-screen modal behavior.");
                Debug.Log("[DS Equipment regression] live inventory/stats, stale identities, modal routing and single-screen fallback passed.");
            }
            finally
            {
                GameMenu.CloseWindow(); UnityEngine.Object.DestroyImmediate(host);
                Array.Copy(saved, InventoryWindow.Items, saved.Length);
                if (!registered) GameMenu.Implemented.Remove(GameMenu.Win.MyStuff);
                if (oldWindow != GameMenu.Win.None) GameMenu.OpenWindow(oldWindow);
            }
        }
    }
}
