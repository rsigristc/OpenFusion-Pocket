using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow
    {
        [Serializable] public sealed class DualItem
        {
            public int slot, type, id, quantity;
            public string key, name, image, label;
            public bool equipped, inspectable;
        }
        [Serializable] public sealed class DualEquipmentState
        {
            public string name, guide, guideImage, oneImage, manyImage, defenseImage, hpFill, fmFill;
            public int level, hp, maxHp, fusion, maxFusion, one, many, defense;
            public bool busy;
            public List<DualItem> equipped = new List<DualItem>(), inventory = new List<DualItem>();
        }
        bool dualItemHost, dualItemPending;
        int dualItemSlot;
        bool dualItemEquipped;
        string dualItemKey;
        public bool DualItemPreview => dualItemHost && GameMenu.Current == GameMenu.Win.MyStuff;
        public static string DualItemKey(ItemBase it) => it.Type + ":" + it.Id + ":" + it.Opt + ":" + it.TimeLimit;
        public bool DualItemMatches(int slot, bool equipped, string key)
        {
            if (slot < 0 || slot >= (equipped ? 9 : Items.Length)) return false;
            var item = equipped ? EquipAt(slot) : Items[slot];
            return item.Id > 0 && item.Type != 8 && DualItemKey(item) == key;
        }
        public bool DualInspectItem(int slot, bool equipped, string key)
        {
            if (GameClient.FindSelf == null || !GameClient.FindSelf.WorldConnected || GameMenu.Blocking || NpcUI.Active ||
                PlayerLife.Active || journal == null || Sorting || !DualItemMatches(slot, equipped, key)) return false;
            if (!GameMenu.OpenWindow(GameMenu.Win.MyStuff)) return false;
            tab = 0; dualItemHost = dualItemPending = true;
            dualItemSlot = slot; dualItemEquipped = equipped; dualItemKey = key;
            return true;
        }
        void DualItemTick()
        {
            if (!dualItemHost) return;
            if (GameMenu.Current != GameMenu.Win.MyStuff) { dualItemHost = dualItemPending = false; return; }
            if (!DualItemMatches(dualItemSlot, dualItemEquipped, dualItemKey))
            { ResetOverlays(); dualItemPending = false; }
            if (dualItemPending)
            {
                dualItemPending = false;
                if (dualItemEquipped || !GeneralPopup(dualItemSlot)) OpenInfo(dualItemEquipped ? -1 : dualItemSlot, dualItemEquipped ? dualItemSlot : -1);
            }
            if (!Overlay) { dualItemHost = false; GameMenu.CloseWindow(); }
        }
        public DualEquipmentState DualEquipment(Func<Texture, string> image)
        {
            var state = new DualEquipmentState { busy = GameMenu.Blocking || NpcUI.Active || PlayerLife.Active };
            if (hud == null) return state;
            var h = hud.Data;
            state.name = h.Name; state.level = h.Level; state.hp = h.Hp; state.maxHp = MaxHpNow();
            state.fusion = h.FusionMatter; state.maxFusion = MaxFmNow();
            ComputeStats(out state.one, out state.many, out state.defense);
            int guide = Mathf.Clamp(Guide - 1, 0, GuideName.Length - 1);
            state.guide = Guide > 0 ? GuideName[guide] : "None";
            state.guideImage = Guide > 0 ? image(UiKit.Tex(GuideIcon[guide])) : "";
            state.oneImage = image(UiKit.Tex("OneVs")); state.manyImage = image(UiKit.Tex("ManyVs"));
            state.defenseImage = image(UiKit.Tex("Defense")); state.hpFill = image(UiKit.Tex("HP_BAR")); state.fmFill = image(UiKit.Tex("FM_BAR"));
            for (int i = 0; i < EqOrder.Length; i++) state.equipped.Add(DualItemData(EquipAt(EqOrder[i]), EqOrder[i], true, EqName[i], image));
            for (int i = 0; i < Items.Length; i++) state.inventory.Add(DualItemData(Items[i], i, false, "", image));
            return state;
        }
        DualItem DualItemData(ItemBase it, int slot, bool equipped, string label, Func<Texture, string> image)
        {
            int type = equipped ? slot == 8 ? 10 : slot == 7 ? 0 : slot : it.Type;
            return new DualItem { slot = slot, type = type, id = it.Id, key = DualItemKey(it), equipped = equipped, label = label,
                inspectable = it.Id > 0 && it.Type != 8, quantity = it.Type == 7 ? Mathf.Max(1, it.Opt) : 1,
                name = it.Id > 0 ? journal?.ItemName(type, it.Id, it.Opt) ?? ("Item " + it.Id) : "Empty",
                image = it.Id > 0 ? image(Icon(type, it.Id, it.Opt)) : "" };
        }
        public RenderTexture DualAvatarTexture()
        {
            if (Preview == null || selfApp == null) return null;
            Preview.Tables = tables; var vehicle = EquipAt(8);
            Preview.VehicleItem = VehicleRide.SelfRiding && vehicle.Id > 0 ? vehicle.Id : 0;
            return Preview.Get(tryApp ?? selfApp, 256, 320) as RenderTexture;
        }
    }
}
