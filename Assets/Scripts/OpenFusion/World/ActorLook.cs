// Descripción de un avatar por piezas (qué ropa, texturas, colores, objetos acoplados). Puro; sin Unity.
// Fuente: HNPCLook.WearItems del cliente original (NPC humanoides de all_hnpc). El jugador rellena lo mismo desde sPCAppearanceData.
using System;
using OpenFusion.Tables.Gen;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class ActorLook
    {
        public bool Female;
        public string TrailColor;
        public string Body => Female ? "actor/w.kfm" : "actor/m.kfm";
        public string SkinTexture => Female ? "texture/f_skin.dds" : "texture/m_skin.dds";
        public const int ClothSlots = 11;   // ActorSkinCombiner.clothes: el jugador usa 11 huecos; el NPC humanoide sólo 0..4 (otro orden)
        // Huecos del jugador (cnAvatarStatus): 0 zapatos, 1 pantalón, 2 camisa, 3 cara, 4 pelo, 7 mochila-ropa. Huecos del NPC (HNPCLook): ver ActorMerge.Cloth.
        public enum PlayerSlot { Shoes = 0, Pants = 1, Shirt = 2, Face = 3, Hair = 4, BackCloth = 7 }
        // ropa por hueco: ruta "wear/..." (null = sin pieza), textura principal ("texture/...") y secundaria (la de "sub": piel u otra)
        public string[] Cloth = new string[ClothSlots], ClothTexture = new string[ClothSlots], ClothTexture2 = new string[ClothSlots];
        // AttachObject[0..5] = Hat, Glasses, LeftPistol, RightPistol, Back, Zipline: ruta, textura principal y secundaria (Mains/Subs)
        public string[] Attach = new string[6], AttachTexture = new string[6], AttachTexture2 = new string[6];
        public int SkinColorIndex = -1, HairColorIndex = -1;      // índices en ActorPalettes (ya restado el -1 del jugador); -1 = por defecto del actor
        public float Height = 0f, Shape = 0f;                     // 0..1, normalizedTime de las animaciones "height"/"shape"

        /// El cliente sólo exige Length > 0 y != "null"; "0" aparece en la tabla como relleno y se trata también como vacío.
        public static bool None(string s) => string.IsNullOrEmpty(s) || s == "null" || s == "0";
        public static string Wear(string s) => None(s) ? null : "wear/" + s.ToLowerInvariant();
        public static string Tex(string s) => None(s) ? null : "texture/" + s.ToLowerInvariant();

        /// HNPCLook.WearItems: iType par = mujer ("Actor/w.kfm"), impar = hombre. Altura/forma: 1 - iHeight/4 y iShape/2.
        public static ActorLook FromHNpc(HNpcTableElement h)
        {
            var l = new ActorLook { Female = h.iType % 2 == 0 };
            l.Cloth[(int)ActorMerge.Cloth.Hair] = Wear(h.strHairMesh); l.ClothTexture[0] = Tex(h.strHairTexture);
            l.Cloth[(int)ActorMerge.Cloth.Face] = Wear(h.strFaceMesh); l.ClothTexture[1] = Tex(h.strFaceTexture);
            l.Cloth[(int)ActorMerge.Cloth.Shirt] = Wear(h.strShirtMesh); l.ClothTexture[2] = Tex(h.strShirtTexture);
            l.Cloth[(int)ActorMerge.Cloth.Pants] = Wear(h.strPantsMesh); l.ClothTexture[3] = Tex(h.strPantsTexture);
            l.Cloth[(int)ActorMerge.Cloth.Shoes] = Wear(h.strShoesMesh); l.ClothTexture[4] = Tex(h.strShoesTexture);
            // secundarias del NPC: en el original son la textura de piel (strHatTextureS != "null" decide si se asigna, para hat, glass y back)
            l.ClothTexture2[2] = l.ClothTexture2[3] = l.ClothTexture2[4] = l.SkinTexture;
            string sub = !None(h.strHatTextureS) ? l.SkinTexture : null;
            l.Attach[(int)ActorMerge.Attach.Hat] = Wear(h.strHatMesh); l.AttachTexture[0] = Tex(h.strHatTextureM); l.AttachTexture2[0] = sub;
            l.Attach[(int)ActorMerge.Attach.Glasses] = Wear(h.strGlassMesh); l.AttachTexture[1] = Tex(h.strGlassTextureM); l.AttachTexture2[1] = sub;
            l.Attach[(int)ActorMerge.Attach.Back] = Wear(h.strBackMesh); l.AttachTexture[4] = Tex(h.strBackTextureM); l.AttachTexture2[4] = sub;
            l.SkinColorIndex = h.iSkinColor >= 0 ? h.iSkinColor : -1; l.HairColorIndex = h.iHairColor >= 0 ? h.iHairColor : -1;
            l.Height = 1f - h.iHeight / 4f; l.Shape = h.iShape / 2f;
            return l;
        }

        /// Todas las rutas de asset que hay que tener cargadas para montar este avatar.
        public System.Collections.Generic.IEnumerable<string> RequiredPaths()
        {
            yield return Body; yield return SkinTexture;
            foreach (var s in Cloth) if (s != null) yield return s;
            foreach (var s in ClothTexture) if (s != null) yield return s;
            foreach (var s in ClothTexture2) if (s != null) yield return s;
            foreach (var s in Attach) if (s != null) yield return s;
            foreach (var s in AttachTexture) if (s != null) yield return s;
            foreach (var s in AttachTexture2) if (s != null) yield return s;
        }
    }
}
