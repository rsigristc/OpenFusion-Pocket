// Interruptores de depuración visual (Alt+1..4) para aislar artefactos: lightmap del terreno, hierba, niebla, agua. Sólo registran en consola.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class VisToggles : MonoBehaviour
    {
        bool lm = true, grass = true, fog = true, water = true;
        void Update()
        {
            if (!(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) { lm = !lm; foreach (var t in Terrain.activeTerrains) { var o = t.transform.Find("lightmapOverlay"); if (o != null) o.gameObject.SetActive(lm); } Debug.Log("[vis] lightmap del terreno: " + lm); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { grass = !grass; GrassField.Enabled = grass; Shader.SetGlobalFloat("_OF_GrassDist", grass ? GrassField.Distance : 0f); foreach (var t in Terrain.activeTerrains) if (t.GetComponentInChildren<GrassField>() == null) t.detailObjectDistance = grass ? 80f : 0f; Debug.Log("[vis] hierba: " + grass); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { fog = !fog; RenderSettings.fog = fog; Debug.Log("[vis] niebla: " + fog + " densidad=" + RenderSettings.fogDensity); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { water = !water; foreach (var r in FindObjectsOfType<MeshRenderer>()) if (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("ffWater")) r.enabled = water; Debug.Log("[vis] agua: " + water); }
        }
    }
}
