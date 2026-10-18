using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class InventoryWindow
    {
        float nextDualGumball = -1;
        // Prefer the Nano's own style; use an all-style gumball only as fallback.
        public int DualGumballSlot(int nanoId)
        {
            if (hud == null || hud.Data.ActiveNanoSlot < 0 || hud.Data.ActiveNanoSlot >= 3 ||
                hud.Data.NanoSlots[hud.Data.ActiveNanoSlot] != nanoId) return -1;
            var nano = NanoRow(nanoId); var items = tables?.m_pGeneralItemTable?.m_pItemData;
            if (nano == null || items == null) return -1;
            int universal = -1;
            for (int i = 0; i < Items.Length; i++)
            {
                var item = Items[i];
                if (item.Type != 7 || item.Id <= 0 || item.Id >= items.Length || item.Opt <= 0) continue;
                var row = items[item.Id]; if (row == null || row.m_iItemType != 3) continue;
                if (row.m_iStimPackAttri == nano.m_iStyle + 1) return i;
                if (row.m_iStimPackAttri == 4 && universal < 0) universal = i;
            }
            return universal;
        }
        bool CanUseDualGumball(int id) => NpcUI.Sender != null && !Sorting && !Machine &&
            Wheel != null && Wheel.CanSummonFromUi && hud != null && Time.unscaledTime >= nextDualGumball &&
            DualGumballSlot(id) >= 0 && Wheel.GumballRemaining(hud.Data.ActiveNanoSlot) <= 0;
        public bool DualUseGumball(int nanoId, int expectedItemId)
        {
            int slot = DualGumballSlot(nanoId);
            if (!CanUseDualGumball(nanoId) || slot < 0 || Items[slot].Id != expectedItemId) return false;
            nextDualGumball = Time.unscaledTime + 3; // Prevent duplicate sends while waiting for the existing acknowledgement path.
            UseNano(slot, hud.Data.ActiveNanoSlot); return true;
        }

        // The bitmap includes transparent padding: digits belong inside its nine cells,
        // not the full texture bounds. Coordinates are normalized to taros_count.png (170x64).
        public static Rect TarosDigitsRect => new Rect(TarosBox.x + TarosBox.width * 2 / 170f,
            TarosBox.y + TarosBox.height * 31 / 64f, TarosBox.width * 110 / 170f, TarosBox.height * 15 / 64f);
        static void DrawFittedLegacyText(Rect rect, string value, SkinStyle style, Color color)
        {
            var font = style?.Font; if (font == null || string.IsNullOrEmpty(value)) return;
            float scale = Mathf.Min(1, rect.width / Mathf.Max(1, font.Width(value)), rect.height / Mathf.Max(1, font.LineHeight));
            var center = rect.center;
            var design = new Rect(center.x - rect.width / scale / 2, center.y - rect.height / scale / 2, rect.width / scale, rect.height / scale);
            font.Draw(design, value, TextAnchor.MiddleCenter, false, color, null, center, scale);
        }
    }
}
