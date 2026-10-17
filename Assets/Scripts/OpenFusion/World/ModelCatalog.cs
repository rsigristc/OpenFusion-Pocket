// id de la tabla (NPC, nano, shiny, transporte, ítem de equipo) -> rutas de modelo/texturas, igual que el cliente original
// (NpcMoveController, NanoMoveController, GameFrame, cnAvatarStatus...). Puro: sólo depende de las tablas ya leídas.
using System;
using System.Collections.Generic;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public enum ModelKind { Npc, Nano, Shiny, Vehicle, Equip, Player }
    public enum EquipSlot { Back, Face, Glass, Hat, Head, Pants, Shirts, Shoes, Weapon, Vehicle }

    public sealed class ModelRef
    {
        public ModelKind Kind; public int Id;
        public string Model;                    // ruta de asset del modelo, minúsculas ("mob/npc_arturo.kfm"); null si el id no tiene modelo
        public string ModelFemale;              // sólo equipo con variante femenina distinta
        public string Tex1, Tex2, TexF1, TexF2; // "texture/xxx.dds" (null si no hay)
        public float Scale = 1f;                // NPC: escala de la tabla (m_fScale)
        public int HNpcIndex = -1;              // NPC humanoide: fila de all_hnpc (m_iHNpcNum) cuando m_iHNpc != 0; -1 = modelo normal
        public int NpcType;                     // NPC: m_iNpcType
        public bool ApplyTextures;              // NPC: sólo si m_iNpcType < 100 se asignan Tex1 (material "main") y Tex2 (material "sub")
        public override string ToString() => $"{Kind}#{Id} {Model}";
    }

    public sealed class ModelCatalog
    {
        readonly Dictionary<int, ModelRef> npc = new Dictionary<int, ModelRef>(), nano = new Dictionary<int, ModelRef>(), shiny = new Dictionary<int, ModelRef>(), veh = new Dictionary<int, ModelRef>();
        readonly Dictionary<(EquipSlot, int), ModelRef> equip = new Dictionary<(EquipSlot, int), ModelRef>();
        public int Npcs => npc.Count; public int Nanos => nano.Count; public int Shinies => shiny.Count; public int Vehicles => veh.Count; public int Equips => equip.Count;

        static bool Nul(string n) => string.IsNullOrEmpty(n) || n == "null";
        public static string Tex(string n) => Nul(n) ? null : "texture/" + n.ToLowerInvariant() + ".dds";
        static string Path(string dir, string n, string ext) => Nul(n) ? null : dir + n.ToLowerInvariant() + ext;

        static ModelRef Make(ModelKind k, int id, MeshNameElement m, string dir, string ext)
        {
            if (m == null) return new ModelRef { Kind = k, Id = id };
            var r = new ModelRef { Kind = k, Id = id, Model = Path(dir, m.m_pstrMMeshModelString, ext), Tex1 = Tex(m.m_pstrMTextureString), Tex2 = Tex(m.m_pstrMTextureString2), TexF1 = Tex(m.m_pstrFTextureString), TexF2 = Tex(m.m_pstrFTextureString2) };
            var f = Path(dir, m.m_pstrFMeshModelString, ext); if (f != null && f != r.Model) r.ModelFemale = f;
            return r;
        }
        static MeshNameElement At(MeshNameElement[] a, int i) => a != null && i >= 0 && i < a.Length ? a[i] : null;   // el cliente indexa la tabla de mallas con m_iMesh

        public static ModelCatalog From(XdtTableScript x)
        {
            var c = new ModelCatalog();
            var nt = x.m_pNpcTable; if (nt?.m_pNpcData != null) foreach (var e in nt.m_pNpcData) { var m = Make(ModelKind.Npc, e.m_iNpcNumber, At(nt.m_pNpcMeshData, e.m_iMesh), "mob/", ".kfm"); m.Scale = e.m_fScale; m.ApplyTextures = e.m_iNpcType < 100; m.NpcType = e.m_iNpcType; if (e.m_iHNpc != 0) m.HNpcIndex = e.m_iHNpcNum; c.npc[e.m_iNpcNumber] = m; }
            var na = x.m_pNanoTable; if (na?.m_pNanoData != null) foreach (var e in na.m_pNanoData) c.nano[e.m_iNanoNumber] = Make(ModelKind.Nano, e.m_iNanoNumber, At(na.m_pNanoMeshData, e.m_iMesh), "nano/", ".kfm");
            var sh = x.m_pShinyTable; if (sh?.m_pShinyData != null) foreach (var e in sh.m_pShinyData) c.shiny[e.m_iShinyID] = Make(ModelKind.Shiny, e.m_iShinyID, At(sh.m_pShinyMeshData, e.m_iMesh), "mob/", ".kfm");
            var tr = x.m_pTransportationTable; if (tr?.m_pTransportationData != null) foreach (var e in tr.m_pTransportationData) c.veh[e.m_iVehicleID] = Make(ModelKind.Vehicle, e.m_iVehicleID, At(tr.m_pTransportationMeshData, e.m_iMesh), "mob/", ".kfm");
            void Eq(EquipSlot s, EquipmentItemTableScript t) { if (t?.m_pItemData == null) return; foreach (var e in t.m_pItemData) c.equip[(s, e.m_iItemNumber)] = Make(ModelKind.Equip, e.m_iItemNumber, At(t.m_pItemMeshData, e.m_iMesh), "wear/", ".nif"); }
            Eq(EquipSlot.Back, x.m_pBackItemTable); Eq(EquipSlot.Face, x.m_pFaceItemTable); Eq(EquipSlot.Glass, x.m_pGlassItemTable); Eq(EquipSlot.Hat, x.m_pHatItemTable);
            Eq(EquipSlot.Head, x.m_pHeadItemTable); Eq(EquipSlot.Pants, x.m_pPantsItemTable); Eq(EquipSlot.Shirts, x.m_pShirtsItemTable); Eq(EquipSlot.Shoes, x.m_pShoesItemTable);
            Eq(EquipSlot.Weapon, x.m_pWeaponItemTable); Eq(EquipSlot.Vehicle, x.m_pVehicleItemTable);
            return c;
        }

        public ModelRef Npc(int id) => npc.TryGetValue(id, out var r) ? r : null;
        public ModelRef Nano(int id) => nano.TryGetValue(id, out var r) ? r : null;
        public ModelRef Shiny(int id) => shiny.TryGetValue(id, out var r) ? r : null;
        public ModelRef Vehicle(int id) => veh.TryGetValue(id, out var r) ? r : null;
        public ModelRef Item(EquipSlot s, int id) => equip.TryGetValue((s, id), out var r) ? r : null;
        /// Cuerpo del jugador: no está en tablas ("Actor/m.kfm" / "Actor/w.kfm").
        public static ModelRef Player(bool female) => new ModelRef { Kind = ModelKind.Player, Id = female ? 1 : 0, Model = female ? "actor/w.kfm" : "actor/m.kfm" };

        public IEnumerable<ModelRef> All()
        {
            foreach (var r in npc.Values) yield return r; foreach (var r in nano.Values) yield return r; foreach (var r in shiny.Values) yield return r;
            foreach (var r in veh.Values) yield return r; foreach (var r in equip.Values) yield return r;
        }
    }
}
