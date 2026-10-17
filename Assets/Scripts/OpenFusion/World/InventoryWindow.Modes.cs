// InventoryWindow (parcial): ganchos para los modos Banco / Tienda / Intercambio (la ventana de servicio activa define como se comporta el panel de inventario). SIN COMPILAR NI PROBAR EN UNITY.
using System.Reflection;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    /// <summary>Comportamiento del panel de inventario mientras hay una ventana de servicio abierta.</summary>
    public interface IInvMode
    {
        /// <summary>Clic izquierdo sobre una ranura ocupada. true = consumido (no abrir la ficha por defecto).</summary>
        bool OnClick(int slot);
        /// <summary>Clic derecho o doble clic (accion rapida).</summary>
        void OnQuick(int slot);
        /// <summary>Se soltó un objeto arrastrado fuera de la rejilla (wm en coordenadas de ventana). true = consumido.</summary>
        bool OnDrop(int slot, Vector2 wm);
        /// <summary>Botones bajo la ficha del objeto de inventario (rect de la ficha). Se llama cada fotograma mientras esta abierta.</summary>
        void InfoButtons(int slot, Rect ext);
    }

    public sealed partial class InventoryWindow
    {
        /// <summary>Modo activo del panel (null = My Stuff normal).</summary>
        public IInvMode Mode;
        public void CloseInfoPub() { CloseInfo(); }
        /// <summary>Columna de equipo en solo lectura (Panel_Equip con flag draggable = false).</summary>
        public void DrawEquipReadOnly(Event e) { DrawEquipColumn(e, false); }
        public ItemBase EquippedAt(int slot) { return EquipAt(slot); }
        /// <summary>Equipo que el personaje puede llevar (nivel); para marcar en rojo en listas.</summary>
        public PcAppearance SelfApp { get { return selfApp; } }
        public bool CanEquip(int type, int id) { return !IsEquip(type) || MinLevel(type, id) <= (hud != null ? hud.Data.Level : 0); }
        /// <summary>Casilla en rojo: equipo que el personaje no puede llevar (nivel, genero, aspecto combinado).</summary>
        public bool IsRed(ItemBase it) { return it.Id > 0 && IsEquip(it.Type) && !CanEquip(it); }
        public static int FirstFreeSlot() { return FirstEmpty(); }
        public void AskDelete(int slot) { confirmSlot = slot; confirmEq = -1; }
        /// <summary>Ranura de inventario bajo una posicion en coordenadas de ventana (-1 si ninguna).</summary>
        // ---- TRY ON (EquipPopup.TryOn): avatar propio con la prenda puesta, en un panel junto a la ficha de la tienda ----
        PcAppearance tryApp; public bool TryOnOpen { get { return tryApp != null; } }
        static PcAppearance CloneApp(PcAppearance a)
        {
            if (a == null) return null;
            var c = (PcAppearance)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, null);
            c.Equip = (ItemBase[])a.Equip.Clone(); return c;
        }
        /// <summary>EnableTryEquip + tipo distinto de vehiculo: solo equipo de las ranuras 0..6 que el personaje puede llevar.</summary>
        public bool CanTryOn(int type, int id) { return type >= 0 && type <= 6 && selfApp != null && CanEquip(type, id); }
        public void StartTryOn(int type, int id)
        {
            if (!CanTryOn(type, id)) return; var c = CloneApp(selfApp); if (c == null) return;
            c.Equip[EquipSlotFor(type)] = new ItemBase { Type = (short)type, Id = (short)id }; tryApp = c; if (Preview != null) Preview.Yaw = AvatarPreview.FrontYaw;
            Sfx.Ui(type == 0 ? "Weapon_Equipped" : "Clothing_Equipped");
        }
        public void EndTryOn() { tryApp = null; }
        /// <summary>Panel de prueba (rectTryon) a la derecha de la ficha (o a la izquierda si no cabe). Se llama cada fotograma desde los botones de la ficha.</summary>
        public void DrawTryOn(Rect card, Event e)
        {
            if (tryApp == null) return; if (Preview != null) Preview.VehicleItem = 0; const float w = 300f, h = 449f; float x = card.xMax + 10f; if (x + w > 1030f) x = card.x - w - 10f; var r = new Rect(x, card.y, w, h);
            if (e.type == EventType.Repaint)
            {
                var oc = GUI.color; GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.95f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = new Color(0f, 0.8f, 0.9f, 1f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture); GUI.color = oc;
                var t = Preview != null ? Preview.Get(tryApp, 300, 400) : null; if (t != null) GUI.DrawTexture(new Rect(r.x, r.y + 4f, w, 400f), t, ScaleMode.StretchToFill, true);
            }
            else if (Preview != null) Preview.Get(tryApp, 300, 400);
            var b = Skin.Get(Skin.Inven, "button");
            if (Skin.Button(new Rect(r.x + 10f, r.yMax - 36f, 60f, 26f), "<", b) && Preview != null) Preview.Yaw -= 25f;
            if (Skin.Button(new Rect(r.xMax - 70f, r.yMax - 36f, 60f, 26f), ">", b) && Preview != null) Preview.Yaw += 25f;
            if (Skin.Button(new Rect(r.center.x - 50f, r.yMax - 36f, 100f, 26f), Loc.T("CLOSE"), b)) EndTryOn();
        }
        public int InvSlotAt(Vector2 wm) { return SlotAt(wm, new Rect(Panel.x + Content.x, Panel.y + Content.y, Content.width, Content.height)); }
    }
}
