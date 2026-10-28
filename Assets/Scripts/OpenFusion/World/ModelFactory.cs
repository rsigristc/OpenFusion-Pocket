// Instancia modelos (NPC, nano, mob, equipo, vehículo, jugador) a partir de ModelRef + AssetIndex + AssetStore. SIN COMPILAR NI PROBAR (UnityEngine).
using System.Linq;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class ModelFactory
    {
        public MainGameData GameData;
        public System.Action<string> MissingAsset;
        readonly AssetStore store; readonly IAssetLocator index; readonly DongBuilder builder;
        public ModelFactory(AssetStore s, IAssetLocator i, DongBuilder b) { store = s; index = i; builder = b; }

        /// Ref al objeto de una ruta de asset ("mob/npc_arturo.kfm") o default si ningún bundle cargado la tiene (descargar su bundle primero).
        public Ref Locate(string assetPath)
        {
            if (assetPath == null) return default;
            if (!index.TryLocate(assetPath, out var e)) { OpenFusion.Screens.PocketDiagnostics.MissingResource(assetPath); MissingAsset?.Invoke(assetPath); return default; }
            var result = store.Resolve(e.File, e.Target);
            if (!result.Ok) { OpenFusion.Screens.PocketDiagnostics.MissingResource(assetPath); MissingAsset?.Invoke(assetPath); }
            return result;
        }
        public bool IsAvailable(ModelRef m) => m?.Model != null && Locate(m.Model).Ok;

        public DongBuilder Builder => builder; public AssetStore Store => store;
        public Texture2D Texture(string assetPath) => builder.TextureAt(Locate(assetPath));

        /// Instancia el modelo como lo hace NpcMoveController: escala de la tabla, animación sin autoplay y texturas "main"/"sub".
        /// `female` elige la variante femenina de equipo si existe. null si el modelo no está disponible (bundle sin cargar).
        public GameObject Instantiate(ModelRef m, Transform parent = null, bool female = false)
        {
            var path = female && m.ModelFemale != null ? m.ModelFemale : m.Model; var r = Locate(path); if (!r.Ok) return null;
            var go = builder.BuildPrefabAt(r, parent); if (go == null) return null;
            go.SetActive(true);   // la raíz del prefab 2.5 llega inactiva; el original instancia y la deja activa
            go.transform.localScale = Vector3.one * (m.Kind == ModelKind.Npc ? m.Scale : 1f);
            var anim = go.GetComponent<Animation>(); if (anim != null) anim.playAutomatically = false;
            if (m.Kind == ModelKind.Npc && m.ApplyTextures)
            {
                var main = Texture(female ? m.TexF1 ?? m.Tex1 : m.Tex1); var sub = Texture(female ? m.TexF2 ?? m.Tex2 : m.Tex2);
                foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in rd.materials)   // instancia, como renderer.material en el original
                    {
                        if (mat.name.Contains("sub") && sub != null) mat.SetTexture("_MainTex", sub);
                        else if (mat.name.Contains("main") && main != null) mat.SetTexture("_MainTex", main);
                    }
            }
            return go;
        }
    }
}
