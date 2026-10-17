// Fija los globales que usan los shaders OpenFusion/* a partir de la luz principal y el ambiente de la escena.
// Unity 2.5 (RenderSettings clase 104) guarda ambient/fog por escena; la luz 0 es la direccional del mapa.
using UnityEngine;

namespace OpenFusion.Materials
{
    public sealed class OpenFusionLighting : MonoBehaviour
    {
        public Light MainLight; public Color Ambient = new Color(0.2f, 0.2f, 0.2f, 1f);   // ambiente por defecto de Unity 2.x
        static readonly int Dir = Shader.PropertyToID("_OF_Light0Dir"), Col = Shader.PropertyToID("_OF_Light0Color"), Amb = Shader.PropertyToID("_OF_Ambient");
        void LateUpdate()
        {
            Vector3 toLight = MainLight != null ? -MainLight.transform.forward : new Vector3(0.3f, 0.8f, 0.5f).normalized;
            Color c = MainLight != null ? MainLight.color * MainLight.intensity : Color.white;
            Shader.SetGlobalVector(Dir, toLight); Shader.SetGlobalColor(Col, c); Shader.SetGlobalColor(Amb, Ambient);
        }
    }
}
