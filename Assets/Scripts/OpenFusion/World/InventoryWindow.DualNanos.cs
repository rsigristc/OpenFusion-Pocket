using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Tables.Gen;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow
    {
        [Serializable] public sealed class DualNanoPower
        {
            public int tune, skill, matter, availableMatter, items, requiredItems;
            public string name, description, type, itemName, image;
            public bool current, canTune, resourcesKnown;
        }
        [Serializable] public sealed class DualNanoEntry
        {
            public int id, slot = -1, energy, max, styleIndex, skill;
            public int gumballId, gumballCount; public string gumballImage; public bool canGumball;
            public string name, description, style, image, skillImage;
            public bool owned, active, energyKnown;
            public float cooldown;
            public List<DualNanoPower> powers = new List<DualNanoPower>();
        }
        [Serializable] public sealed class DualNanoCollection
        {
            public bool station, busy;
            public string notice;
            public string energyFill, energyFrame, skillBack, matterFill, itemFill;
            public int selected, scroll, activeSlot = -1;
            public List<DualNanoEntry> entries = new List<DualNanoEntry>();
        }
        public bool DualNanoExists(int id) { if (nanoList == null) BuildNanoList(); return nanoList.Contains(id); }
        public bool DualStation => Machine && GameMenu.Current == GameMenu.Win.MyStuff;
        public bool DualNanoBusy => machineBusy;
        int EquippedSlot(int id)
        {
            if (hud != null) for (int k = 0; k < 3; k++) if (id > 0 && hud.Data.NanoSlots[k] == id) return k;
            return -1;
        }
        bool CanEquipDual(int id, int slot) => NpcUI.Sender != null && DualStation && !machineBusy && OwnsNano(id) && NanoRow(id) != null && hud != null &&
            slot >= 0 && slot < 3 && hud.Data.ActiveNanoSlot != slot && EquippedSlot(id) < 0;
        public bool DualEquipNano(int id, int slot)
        {
            if (!CanEquipDual(id, slot)) return false;
            EquipNano(slot + 1, id); return true;
        }
        public bool DualUnequipNano(int id)
        {
            int slot = EquippedSlot(id);
            if (NpcUI.Sender == null || !DualStation || machineBusy || !OwnsNano(id) || slot < 0) return false;
            EquipNano(0, id, slot); return true;
        }
        public bool CanTuneNano(int id, int tune, out int matter, out int have, out NanoTuneElement td)
        {
            td = null; matter = have = 0;
            var n = NanoRow(id); var nt = NT; var g = Grow(hud != null ? hud.Data.Level : 0);
            if (n?.m_iTune == null || Array.IndexOf(n.m_iTune, tune) < 0 || nt?.m_pNanoTuneData == null || tune <= 0 || tune >= nt.m_pNanoTuneData.Length) return false;
            td = nt.m_pNanoTuneData[tune]; if (td == null || g == null) return false;
            matter = g.m_iReqBlob_NanoTune;
            have = CountItem(td.m_iReqItemID);
            return NpcUI.Sender != null && DualStation && !machineBusy && OwnsNano(id) && SkillOfNano(id) != td.m_iSkillID && hud != null &&
                hud.Data.FusionMatter >= matter && have >= Mathf.Max(1, td.m_iReqItemCount);
        }
        public bool DualTuneNano(int id, int tune)
        {
            if (!CanTuneNano(id, tune, out _, out _, out var td)) return false;
            nanoPop = id; nanoPopOwned = true; TuneNano(tune, td); return true;
        }
        public void DualRestoreSelection(int id)
        {
            if (!DualStation || !DualNanoExists(id)) return;
            nanoPop = id; nanoPopOwned = OwnsNano(id);
        }
        public int DualLegacySelection => DualStation && nanoPop > 0 ? nanoPop : 0;
        public DualNanoCollection DualNanos(int selected, int scroll, Func<Texture, string> export)
        {
            if (nanoList == null) BuildNanoList();
            var result = new DualNanoCollection { station = DualStation, busy = machineBusy, selected = selected, scroll = scroll, activeSlot = hud != null ? hud.Data.ActiveNanoSlot : -1,
                energyFill = export?.Invoke(UiKit.Tex("HP_BAR")) ?? "", energyFrame = export?.Invoke(UiKit.Tex("nano_st_bar")) ?? "",
                skillBack = export?.Invoke(UiKit.Tex("buff_icon_back")) ?? "",
                matterFill = export?.Invoke(UiKit.Tex("NanoPopupFMBar")) ?? "", itemFill = export?.Invoke(UiKit.Tex("NanoPopupItemBar")) ?? "",
                notice = DualStation ? "Nano Station: changes are validated by the server. Movement and combat are paused." :
                "Visit a Nano Station to change equipped Nanos or powers. Tap an equipped Nano to summon it." };
            foreach (int id in nanoList)
            {
                var n = NanoRow(id); if (n == null) continue;
                int slot = EquippedSlot(id);
                var entry = new DualNanoEntry { id = id, slot = slot, name = NanoStr(n, 0), description = NanoStr(n, 1), style = NanoStr(n, 2),
                    owned = OwnsNano(id), active = hud != null && slot >= 0 && hud.Data.ActiveNanoSlot == slot, max = n.m_iNanoBattery1, styleIndex = n.m_iStyle };
                entry.energy = hud == null ? 0 : slot >= 0 ? hud.Data.NanoStamina[slot] :
                    hud.Data.NanoBankStamina != null && id < hud.Data.NanoBankStamina.Length ? hud.Data.NanoBankStamina[id] : 0;
                entry.energyKnown = hud != null && (slot >= 0 || hud.Data.NanoBankStamina != null && id < hud.Data.NanoBankStamina.Length);
                entry.cooldown = Wheel != null && slot >= 0 ? Wheel.CooldownRemaining(slot) : 0;
                if (entry.active) {
                    int gum = DualGumballSlot(id);
                    if (gum >= 0) { entry.gumballId = Items[gum].Id; entry.gumballCount = Items[gum].Opt; entry.gumballImage = export?.Invoke(Icon(7, Items[gum].Id, Items[gum].Opt)) ?? ""; }
                    entry.canGumball = gum >= 0 && CanUseDualGumball(id);
                }
                entry.image = export?.Invoke(NanoIconTex(n, !entry.owned) ?? NanoIconTex(n, false)) ?? "";
                entry.skill = entry.owned ? SkillOfNano(id) : 0;
                entry.skillImage = entry.skill > 0 ? export?.Invoke(SkillIconTex(entry.skill) ?? hud?.NanoSkillIconProvider?.Invoke(id)) ?? "" : "";
                if (id == selected && n.m_iTune != null) foreach (int tune in n.m_iTune)
                {
                    bool can = CanTuneNano(id, tune, out int matter, out int have, out var td);
                    if (td == null) continue;
                    bool current = entry.skill == td.m_iSkillID;
                    if (!current && !result.station) continue;
                    var strings = NT.m_pNanoTuneStringData;
                    var ts = strings != null && td.m_iTuneName >= 0 && td.m_iTuneName < strings.Length ? strings[td.m_iTuneName] : null;
                    entry.powers.Add(new DualNanoPower { tune = tune, skill = td.m_iSkillID, current = current, canTune = can,
                        matter = matter, availableMatter = hud != null ? Mathf.Max(0, hud.Data.FusionMatter) : 0, resourcesKnown = hud != null,
                        items = have, requiredItems = Mathf.Max(1, td.m_iReqItemCount), itemName = ItemName(td.m_iReqItemID),
                        name = ts != null ? ts.m_strName : "Power " + td.m_iSkillID, description = ts != null ? ts.m_strComment : "",
                        type = ts != null ? ts.m_strComment1 : "", image = export?.Invoke(SkillIconTex(td.m_iSkillID)) ?? "" });
                }
                int currentPower = entry.powers.FindIndex(p => p.current);
                if (currentPower > 0) { var power = entry.powers[currentPower]; entry.powers.RemoveAt(currentPower); entry.powers.Insert(0, power); }
                result.entries.Add(entry);
            }
            return result;
        }
    }
}
