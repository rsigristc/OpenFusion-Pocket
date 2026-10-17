// Apariencia del jugador -> ActorLook. Puro; sin Unity. Port de cnAvatarStatus.SetAppearanceData/SetLook/SetAvatarLook del cliente original
// (SetFace, SetHead, SetBody, SetHat, SetFaceHand, SetBackFack). Entrada: sPCStyle + los 7 ids de equipo visibles de sPCAppearanceData.ItemEquip.
using System;
using System.Collections.Generic;
using OpenFusion.Tables.Gen;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    /// Campos de sPCStyle que afectan al aspecto (iGender: 1 = hombre, 2 = mujer).
    public struct PlayerStyle
    {
        public int Gender, FaceStyle, HairStyle, HairColor, SkinColor, EyeColor, Height, Body;
    }

    /// Ids de los objetos equipados, en el orden de sPCAppearanceData.ItemEquip[0..6] (el cliente usa los 7 primeros; 9 en total).
    public sealed class PlayerEquip
    {
        public int Weapon, Shirt, Pants, Shoes, Hat, Glass, Back;
        public static PlayerEquip FromItemEquip(IList<int> ids)
        {
            int G(int i) => ids != null && i < ids.Count ? ids[i] : 0;
            return new PlayerEquip { Weapon = G(0), Shirt = G(1), Pants = G(2), Shoes = G(3), Hat = G(4), Glass = G(5), Back = G(6) };
        }
    }

    public sealed class PlayerLookBuilder
    {
        // Tablas de objetos por id de tabla del cliente (TableContainer.GetTableData(<id>, 0, itemId) y (<id>, 3, m_iMesh))
        public const int TBack = 17, TFace = 18, TGlass = 19, THat = 20, THair = 21, TPants = 22, TShirt = 23, TShoes = 24, TWeapon = 25;
        const string EyeSuffix = "aabcdefghijklmnopqrstuvwxyz";   // cnAvatarStatus.szTextureSuffix (el índice 0 y 1 dan 'a')

        readonly Dictionary<int, Dictionary<int, (ItemElement Item, MeshNameElement Mesh)>> tables = new Dictionary<int, Dictionary<int, (ItemElement, MeshNameElement)>>();
        readonly XdtTableScript source;

        public PlayerLookBuilder(XdtTableScript x)
        {
            source = x;
            void Add(int id, EquipmentItemTableScript t)
            {
                var d = new Dictionary<int, (ItemElement, MeshNameElement)>(); tables[id] = d; if (t?.m_pItemData == null) return;
                foreach (var e in t.m_pItemData) d[e.m_iItemNumber] = (e, t.m_pItemMeshData != null && e.m_iMesh >= 0 && e.m_iMesh < t.m_pItemMeshData.Length ? t.m_pItemMeshData[e.m_iMesh] : null);
            }
            Add(TBack, x.m_pBackItemTable); Add(TFace, x.m_pFaceItemTable); Add(TGlass, x.m_pGlassItemTable); Add(THat, x.m_pHatItemTable); Add(THair, x.m_pHeadItemTable);
            Add(TPants, x.m_pPantsItemTable); Add(TShirt, x.m_pShirtsItemTable); Add(TShoes, x.m_pShoesItemTable); Add(TWeapon, x.m_pWeaponItemTable);
        }

        public bool TryItem(int table, int id, out ItemElement item, out MeshNameElement mesh)
        {
            item = null; mesh = null; if (!tables.TryGetValue(table, out var d) || !d.TryGetValue(id, out var v)) return false; item = v.Item; mesh = v.Mesh; return true;
        }

        static string Lc(string s) => s.ToLowerInvariant();
        static bool Empty(string s) => ActorLook.None(s);
        static string M(bool female, MeshNameElement e) => female ? e.m_pstrFMeshModelString : e.m_pstrMMeshModelString;
        static string T1(bool female, MeshNameElement e) => female ? e.m_pstrFTextureString : e.m_pstrMTextureString;
        static string T2(bool female, MeshNameElement e) => female ? e.m_pstrFTextureString2 : e.m_pstrMTextureString2;

        /// Equivale a SetAvatarLook() del cliente. Devuelve null sólo si el género no es 1 ni 2.
        public ActorLook Build(PlayerStyle st, PlayerEquip eq, List<string> warnings = null)
        {
            if (st.Gender != 1 && st.Gender != 2) return null;
            eq = eq ?? new PlayerEquip();
            var look = new ActorLook { Female = st.Gender == 2 };
            bool f = look.Female; string skinTex = look.SkinTexture;   // "texture/m_skin.dds" | "texture/f_skin.dds"
            void Warn(string w) { warnings?.Add(w); }

            // sombrero: decide la variante de pelo/cara y si hay gafas (SetHat)
            TryItem(THat, eq.Hat, out var hatItem, out var hatMesh);
            int hatType = eq.Hat != 0 && hatItem != null ? hatItem.m_iEquipType : -1;
            if (eq.Hat != 0 && hatItem == null) Warn("sombrero desconocido " + eq.Hat);
            bool hairHidden = hatType == 2 || hatType == 3;                    // RemoveParts(4)
            int hairType = (hatType == 1 || hatType == 5) ? 2 : 1;            // _type02 = pelo bajo el sombrero
            int faceType = (hatType == 2 || hatType == 3) ? 2 : 1;            // _type02 = cara con sombrero que la cubre
            bool glassesHidden = hatType == 3 || hatType == 4 || hatType == 5; // AttachObject[1] = null

            // cara (slot 3)
            const int Face = (int)ActorLook.PlayerSlot.Face, Hair = (int)ActorLook.PlayerSlot.Hair;
            if (st.FaceStyle != 0)
            {
                if (TryItem(TFace, st.FaceStyle, out _, out var fm) && fm != null)
                {
                    look.Cloth[Face] = "wear/" + Lc(M(f, fm)) + "_type0" + faceType + ".nif";
                    int eye = Math.Max(0, Math.Min(st.EyeColor, EyeSuffix.Length - 1));
                    look.ClothTexture[Face] = "texture/" + Lc(T1(f, fm)) + "_" + EyeSuffix[eye] + ".dds"; look.ClothTexture2[Face] = skinTex;
                    look.SkinColorIndex = st.SkinColor - 1;      // sólo se asigna cuando hay cara (SetFace)
                }
                else Warn("cara desconocida " + st.FaceStyle);
            }
            // pelo (slot 4)
            if (st.HairStyle != 0 && !hairHidden)
            {
                if (TryItem(THair, st.HairStyle, out _, out var hm) && hm != null)
                {
                    look.Cloth[Hair] = "wear/" + Lc(M(f, hm)) + "_type0" + hairType + ".nif";
                    look.ClothTexture[Hair] = "texture/" + Lc(T1(f, hm)) + "_a.dds";
                    look.HairColorIndex = st.HairColor - 1;
                }
                else Warn("pelo desconocido " + st.HairStyle);
            }

            // pantalón (1), camisa (2), zapatos (0): si no hay objeto, ropa "naked" del género
            void Body(int table, int id, PlayerSlotNaming n)
            {
                int slot = n.Slot; string mesh, tex;
                if (id == 0) { mesh = n.Naked(f); tex = n.NakedTex(f); }
                else if (TryItem(table, id, out _, out var m) && m != null) { mesh = Lc(M(f, m)) + ".nif"; tex = Lc(T1(f, m)) + ".dds"; }
                else { Warn("objeto desconocido " + table + "/" + id); return; }
                look.Cloth[slot] = "wear/" + mesh; look.ClothTexture[slot] = "texture/" + tex; look.ClothTexture2[slot] = skinTex;
            }
            Body(TPants, eq.Pants, PlayerSlotNaming.Pants); Body(TShirt, eq.Shirt, PlayerSlotNaming.Shirt); Body(TShoes, eq.Shoes, PlayerSlotNaming.Shoes);

            // arma (objeto acoplado 3 = pistola derecha) y gafas (1)
            void Attach(int slotIdx, int table, int id)
            {
                if (id == 0) return;
                if (!TryItem(table, id, out _, out var m) || m == null) { Warn("objeto desconocido " + table + "/" + id); return; }
                look.Attach[slotIdx] = "wear/" + Lc(M(f, m)) + ".nif"; look.AttachTexture[slotIdx] = "texture/" + Lc(T1(f, m)) + ".dds";
                var t2 = T2(f, m); look.AttachTexture2[slotIdx] = !string.IsNullOrEmpty(t2) && t2.Length > 0 && t2 != "null" ? "texture/" + Lc(t2) + ".dds" : null;
            }
            Attach((int)ActorMerge.Attach.RightPistol, TWeapon, eq.Weapon);
            if (TryItem(TWeapon, eq.Weapon, out var weapon, out _) && source.m_pWeaponItemTable?.m_pItemStringData != null)
            {
                var names = source.m_pWeaponItemTable.m_pItemStringData;
                if (weapon.m_iItemName >= 0 && weapon.m_iItemName < names.Length) look.TrailColor = names[weapon.m_iItemName]?.m_strComment2;
            }
            if (!glassesHidden) Attach((int)ActorMerge.Attach.Glasses, TGlass, eq.Glass);

            // sombrero acoplado (0)
            if (eq.Hat != 0 && hatItem != null && hatMesh != null)
            {
                look.Attach[0] = "wear/" + Lc(M(f, hatMesh)) + ".nif"; look.AttachTexture[0] = "texture/" + Lc(T1(f, hatMesh)) + ".dds";
                var t2 = T2(f, hatMesh); look.AttachTexture2[0] = t2 != null && t2.Length > 1 && t2 != "null" ? "texture/" + Lc(t2) + ".dds" : null;
            }

            // mochila: equipType 1 = ropa fusionada (slot 7), otro = objeto acoplado a la espalda (4)
            if (eq.Back != 0)
            {
                if (TryItem(TBack, eq.Back, out var bi, out var bm) && bm != null)
                {
                    var t2 = T2(f, bm); string tex2 = !string.IsNullOrEmpty(t2) && t2 != "null" ? "texture/" + Lc(t2) + ".dds" : null;
                    if (bi.m_iEquipType == 1)
                    { int s = (int)ActorLook.PlayerSlot.BackCloth; look.Cloth[s] = "wear/" + Lc(M(f, bm)) + ".nif"; look.ClothTexture[s] = "texture/" + Lc(T1(f, bm)) + ".dds"; look.ClothTexture2[s] = tex2; }
                    else
                    { int s = (int)ActorMerge.Attach.Back; look.Attach[s] = "wear/" + Lc(M(f, bm)) + ".nif"; look.AttachTexture[s] = "texture/" + Lc(T1(f, bm)) + ".dds"; look.AttachTexture2[s] = tex2; }
                }
                else Warn("mochila desconocida " + eq.Back);
            }

            // tirolina: siempre cargada (inactiva hasta usarla)
            look.Attach[(int)ActorMerge.Attach.Zipline] = "wear/pistol_trolley.nif";

            look.Height = 1f - st.Height / 4f; look.Shape = st.Body / 2f;
            return look;
        }

        sealed class PlayerSlotNaming
        {
            public int Slot; public Func<bool, string> Naked, NakedTex;
            public static readonly PlayerSlotNaming Pants = new PlayerSlotNaming { Slot = (int)ActorLook.PlayerSlot.Pants, Naked = f => f ? "f_pants_naked.nif" : "m_pants_naked.nif", NakedTex = f => f ? "f_naked.dds" : "m_naked.dds" };
            public static readonly PlayerSlotNaming Shirt = new PlayerSlotNaming { Slot = (int)ActorLook.PlayerSlot.Shirt, Naked = f => f ? "f_shirt_naked.nif" : "m_shirt_naked.nif", NakedTex = f => f ? "f_naked.dds" : "m_naked.dds" };
            // el original usa "boysrollertee_a.dds" para los zapatos desnudos femeninos (rareza del cliente, replicada)
            public static readonly PlayerSlotNaming Shoes = new PlayerSlotNaming { Slot = (int)ActorLook.PlayerSlot.Shoes, Naked = f => f ? "f_shoes_naked.nif" : "m_shoes_naked.nif", NakedTex = f => f ? "boysrollertee_a.dds" : "m_naked.dds" };
        }
    }
}
