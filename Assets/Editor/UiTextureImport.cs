// Las texturas de interfaz se dibujan con su tamaño real (GUI.DrawTexture + texture.width/height + coordenadas en píxeles del original).
// Con nPOTScale = ToNearest (el valor por defecto) Unity las remuestrea a potencia de 2 (p. ej. shiny_back 178x135 -> 128x128, fm_count 172x63 -> 128x64):
// texture.width cambia y todo lo que se coloca "dentro" de la textura (iconos en el hueco del huevo, dígitos de los contadores...) queda desfasado.
#if UNITY_EDITOR
using UnityEditor;

namespace OpenFusion.EditorTools
{
    public sealed class UiTextureImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 2; }   // subir para forzar reimportar
        static bool IsUi(string p) { return p.StartsWith("Assets/Resources/OpenFusion/UI/") || p.StartsWith("Assets/Resources/OpenFusion/Icons/") || p.StartsWith("Assets/Resources/OpenFusion/Portraits/"); }
        void OnPreprocessTexture()
        {
            if (!IsUi(assetPath)) return;
            var ti = (TextureImporter)assetImporter;
            ti.npotScale = TextureImporterNPOTScale.None; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        }
    }
}
#endif
