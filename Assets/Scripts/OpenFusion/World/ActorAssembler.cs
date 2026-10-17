// Monta un avatar por piezas en Unity como ActorSkinCombiner.Generate + HNPCLook.WearItems del cliente original.
// SIN COMPILAR NI PROBAR (UnityEngine) y SIN VERIFICAR CON DATOS REALES: el actor (Actor/m.kfm, w.kfm) y las piezas de ropa
// (wear/m_*.nif, f_*.nif con ActorWearSkinTable/ActorWearIndexTable) no están en la caché local. La fusión de mallas y huesos
// (ActorMerge) sí está verificada con mallas reales.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class ActorColors
    {
        public Color Skin = new Color(0.7f, 0.5f, 0.1f, 1f), Hair = new Color(0.3f, 0.1f, 0.05f, 1f);   // valores por defecto del ActorSkinCombiner
        /// Colores de piel y pelo según los índices del ActorLook y las paletas del cliente (índice -1 = se deja el valor por defecto).
        public static ActorColors From(ActorLook look)
        {
            var c = new ActorColors(); var s = ActorPalettes.Skin(look.SkinColorIndex); var h = ActorPalettes.Hair(look.HairColorIndex);
            if (s.HasValue) c.Skin = new Color(s.Value.R, s.Value.G, s.Value.B, 1f); if (h.HasValue) c.Hair = new Color(h.Value.R, h.Value.G, h.Value.B, 1f);
            return c;
        }
    }

    public sealed class ActorAssembler
    {
        readonly ModelFactory models; readonly DongBuilder builder; readonly AssetStore store;
        public Texture MainSpec, SubSpec;     // rampas especulares (UserContainer.GetMainSpec/GetSubSpec); pueden ser null
        public ActorAssembler(ModelFactory m) { models = m; builder = m.Builder; store = m.Store; }

        public GameObject Build(ActorLook look, ActorColors colors = null, Transform parent = null)
        {
            colors = colors ?? ActorColors.From(look);
            var bodyRef = models.Locate(look.Body); if (!bodyRef.Ok) { Debug.LogWarning("Falta " + look.Body); return null; }
            var res = builder.BuildPrefabResultAt(bodyRef, parent); if (res == null || res.Root == null) return null;
            var root = res.Root; var g = builder.GraphOf(bodyRef.FileName);

            // 1) ActorSkinCombiner del actor: huesos y nodo que lleva el SkinnedMeshRenderer combinado
            SceneNode25 combNode = null; ActorCombiner25 comb = null;
            foreach (var n in res.Nodes.Keys.Select(id => g.ByTransform[id]))
                foreach (var c in n.Components.Where(c => c.Key == "MonoBehaviour"))
                { var v = store.Read(store.Resolve(bodyRef.FileName, 0, c.Value)); if (ActorCombiner25.IsMatch(v)) { comb = ActorCombiner25.From(v); combNode = n; break; } }
            if (comb == null) { Debug.LogWarning("El actor no tiene ActorSkinCombiner"); return root; }
            var actorBones = comb.ActorBones.Select(p => res.Nodes.TryGetValue(p.PathId, out var go) ? go.transform : root.transform).ToArray();

            // 2) piezas de ropa -> mallas (con huesos propios) + tablas de índices + materiales
            var parts = new List<WearPart>(); var mats = new List<Material>(); var slots = new List<int>();
            for (int slot = 0; slot < look.Cloth.Length; slot++)
            {
                if (look.Cloth[slot] == null) continue; var wr = models.Locate(look.Cloth[slot]); if (!wr.Ok) { Debug.LogWarning("Falta " + look.Cloth[slot]); continue; }
                var wg = builder.GraphOf(wr.FileName); var wroot = wg.ByTransform.Values.FirstOrDefault(n => n.GameObjectId == wr.Info.PathId); if (wroot == null) continue;
                ActorWearSkins25 skins = null;
                foreach (var c in wroot.Components.Where(c => c.Key == "MonoBehaviour")) { var v = store.Read(store.Resolve(wr.FileName, 0, c.Value)); if (ActorWearSkins25.IsMatch(v)) { skins = ActorWearSkins25.From(v); break; } }
                if (skins == null) { Debug.LogWarning("Pieza sin ActorWearSkinTable: " + look.Cloth[slot]); continue; }
                foreach (var sp in skins.Skins)
                {
                    var node = wg.OwnerOf(sp.PathId); if (node?.Skin == null) continue;
                    var m25 = store.Mesh(store.Resolve(wr.FileName, node.Skin.Mesh)); if (m25 == null) continue;
                    ActorWearIndex25 idx = null;
                    foreach (var c in node.Components.Where(c => c.Key == "MonoBehaviour")) { var v = store.Read(store.Resolve(wr.FileName, 0, c.Value)); if (ActorWearIndex25.IsMatch(v)) { idx = ActorWearIndex25.From(v); break; } }
                    if (idx == null || node.Materials.Count == 0) continue;
                    var shared = builder.MaterialAt(wr.FileName, node.Materials[0]); if (shared == null) continue;
                    parts.Add(new WearPart { Mesh = m25, Index = idx, Slot = slot }); mats.Add(StyleMaterial(shared, slot, look, colors)); slots.Add(slot);
                }
            }
            if (parts.Count > 0)
            {
                var merged = ActorMerge.Merge(parts, look.Female);
                var smr = combNode != null && res.Nodes.TryGetValue(combNode.TransformId, out var cgo) ? (cgo.GetComponent<SkinnedMeshRenderer>() != null ? cgo.GetComponent<SkinnedMeshRenderer>() : cgo.AddComponent<SkinnedMeshRenderer>()) : root.AddComponent<SkinnedMeshRenderer>();
                if (smr.sharedMesh != null) UnityEngine.Object.Destroy(smr.sharedMesh);
                smr.sharedMesh = UnityConverters.ToMesh(merged.Mesh);
                smr.bones = merged.ActorBoneIndex.Select(i => actorBones[Mathf.Clamp(i, 0, actorBones.Length - 1)]).ToArray();
                smr.sharedMaterials = mats.ToArray(); smr.rootBone = root.transform.Find("Bip01") ?? actorBones[0]; smr.updateWhenOffscreen = true;
            }

            // 3) objetos acoplados a huesos (sombrero, gafas, mochila...)
            for (int a = 0; a < look.Attach.Length; a++) AttachOne(root, look, a);

            // 4) altura/forma (animaciones "height"/"shape" muestreadas por normalizedTime)
            var shape = root.AddComponent<ActorShape>(); shape.Height = look.Height; shape.Shape = look.Shape; shape.Apply();
            return root;
        }

        /// Sustituye el objeto acoplado `a` (p. ej. el arma, RightPistol) de un avatar ya montado: borra el anterior (marcado con ActorAttachTag) y acopla el de `look`.
        public void ReplaceAttach(GameObject root, ActorLook look, int a)
        {
            var bone = root.transform.Find(ActorMerge.AttachPath((ActorMerge.Attach)a));
            if (bone != null) for (int c = bone.childCount - 1; c >= 0; c--) { var t = bone.GetChild(c); var tg = t.GetComponent<ActorAttachTag>(); if (tg != null && tg.Slot == a) UnityEngine.Object.Destroy(t.gameObject); }   // solo el mismo hueco: arma (3) y tirolina (5) comparten hueso
            AttachOne(root, look, a);
        }
        void AttachOne(GameObject root, ActorLook look, int a)
        {
            if (look.Attach[a] == null) return; var ar = models.Locate(look.Attach[a]); if (!ar.Ok) { Debug.LogWarning("Falta " + look.Attach[a]); return; }
            var bone = root.transform.Find(ActorMerge.AttachPath((ActorMerge.Attach)a)); if (bone == null) return;
            var go = builder.BuildPrefabAt(ar, bone); if (go == null) return;
            go.AddComponent<ActorAttachTag>().Slot = a;
            WorldEntities.ActivateAll(go);   // los prefabs 2.5 llegan inactivos: sin esto el arma desaparece al cambiarla (TAB)
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); go.transform.localScale = Vector3.one; bone.localScale = Vector3.one;
            var an = go.GetComponent<Animation>(); if (an != null) an.wrapMode = WrapMode.Loop;
            var main = look.AttachTexture[a] != null ? models.Texture(look.AttachTexture[a]) : null; var sub = look.AttachTexture2[a] != null ? models.Texture(look.AttachTexture2[a]) : null;
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true)) foreach (var m in rd.materials)
            {
                bool isMain = m.name.Contains("main"), isSub = m.name.Contains("sub");
                if (isMain && main != null) m.SetTexture("_MainTex", main); else if (isSub && sub != null) m.SetTexture("_MainTex", sub);
                if (m.HasProperty("_SpecMap") && MainSpec != null) { if (isMain) m.SetTexture("_SpecMap", MainSpec); else if (isSub) m.SetTexture("_SpecMap", SubSpec); }
            }
            SetLayer(go.transform, root.layer);
            if (a == (int)ActorMerge.Attach.RightPistol)
            {
                Color color = new Color(1f, .73f, .95f);
                if (!string.IsNullOrEmpty(look.TrailColor) && ColorUtility.TryParseHtmlString("#" + look.TrailColor.TrimStart('#'), out var custom)) color = custom;
                MeleeTrail.Attach(go, root.GetComponent<Animation>(), color);
            }
            // tirolina (pistol_trolley): en el original LinkObjectActive(5,false) salvo mientras se usa la tirolina; sin esto la barra se ve en las manos con las poses de vehiculo
            if (a == (int)ActorMerge.Attach.Zipline) foreach (var zr in go.GetComponentsInChildren<Renderer>(true)) zr.enabled = false;
        }

        /// ActorSkinCombiner.Generate: textura y color del material según su nombre ("sub" = piel, "override" = tal cual, resto = principal; "hair" = color de pelo).
        Material StyleMaterial(Material shared, int slot, ActorLook look, ActorColors colors)
        {
            var m = new Material(shared); var name = shared.name; var primary = look.ClothTexture[slot] != null ? models.Texture(look.ClothTexture[slot]) : null;
            var skinTex = look.ClothTexture2[slot] != null ? models.Texture(look.ClothTexture2[slot]) : null;   // secondaryTextures[slot] del original (piel u otra; null = se deja)
            switch (ActorMerge.RoleOf(name))
            {
                case ActorMerge.Role.Sub:
                    m.color = colors.Skin * 0.5f; if (skinTex != null) m.mainTexture = skinTex;
                    if (m.HasProperty("_Emission")) m.SetColor("_Emission", colors.Skin * m.GetColor("_Emission"));
                    break;
                case ActorMerge.Role.Override: break;
                default: if (primary != null) m.mainTexture = primary; break;
            }
            if (m.HasProperty("_SpecMap") && SubSpec != null) m.SetTexture("_SpecMap", SubSpec);
            if (ActorMerge.IsHair(name)) { m.color = colors.Hair * 0.5f; if (m.HasProperty("_Emission")) m.SetColor("_Emission", colors.Hair * m.GetColor("_Emission")); }
            return m;
        }

        static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) SetLayer(c, layer); }
    }

    /// Altura y forma del actor: port de ActorSkinCombiner.Update/SetAnimationWrapMode (animaciones "height"/"shape" y las aditivas "*_Add").
    public sealed class ActorShape : MonoBehaviour
    {
        [Range(0, 1)] public float Height, Shape; float oldH = -1f, oldS = -1f; Animation anim; AnimationState hAdd, sAdd;

        void Awake() { Setup(); }
        void Setup()
        {
            anim = GetComponentInChildren<Animation>(); if (anim == null) return;
            hAdd = anim["height_Add"]; sAdd = anim["shape_Add"];
            foreach (var st in new[] { hAdd, sAdd }) if (st != null) { st.blendMode = AnimationBlendMode.Additive; st.wrapMode = WrapMode.ClampForever; st.layer = 100; st.weight = 1f; }
            if (anim["shape"] != null) anim["shape"].wrapMode = WrapMode.ClampForever;
            if (anim["height"] != null) anim["height"].wrapMode = WrapMode.ClampForever;
        }
        public void Apply()
        {
            if (anim == null) Setup(); if (anim == null || (oldH == Height && oldS == Shape)) return;
            foreach (var (st, t) in new[] { (anim["height"], Height), (anim["shape"], Shape) })
                if (st != null) { st.enabled = true; st.normalizedTime = t; st.layer = 100; st.weight = 1f; }
            anim.Sample();
            foreach (var st in new[] { anim["height"], anim["shape"] }) if (st != null) st.enabled = false;
            oldH = Height; oldS = Shape;
        }
        void Update()
        {
            if (hAdd != null) { hAdd.enabled = true; hAdd.normalizedTime = Height; }
            if (sAdd != null) { sAdd.enabled = true; sAdd.normalizedTime = Shape; }
            Apply();
        }
    }
}
