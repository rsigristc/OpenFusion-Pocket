// ParticleTrail.cs -- port de ParticleTrailController (cliente original, Assembly-CSharp-first_pass): estela en cinta que gira alrededor del eje de avance
// (trailLength segmentos, fHeight de ancho, fTimes = subdivisión por frame). Se usa en ES[321] ("pisto"), disparos de pistola/láser.
// La malla se crea en tiempo de ejecución, por eso el prefab 2.5 no trae malla: el material viene del MeshRenderer del prefab.
using UnityEngine;

namespace OpenFusion.World
{
    public class ParticleTrail : MonoBehaviour
    {
        public float fHeight = 1f, fTimes = 0.25f, InitialSize = 1f, Number = 30f; public int trailLength = 30; public bool bUVScroll; public Material Mat;
        Mesh mesh; Color[] colors; Vector2[] uvs; Vector3[] vertices; int[] triangles; Vector3[] data; float[] uvf; float dTime; bool ready, seeded; MeshFilter mf;

        public void Init()
        {
            trailLength = Mathf.Max(3, trailLength);
            mesh = new Mesh(); mesh.MarkDynamic(); mf = GetComponent<MeshFilter>(); if (mf == null) mf = gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = GetComponent<MeshRenderer>(); if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();
            if (Mat != null) mr.sharedMaterial = Mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            data = new Vector3[trailLength]; uvf = new float[trailLength]; uvs = new Vector2[trailLength * 2]; vertices = new Vector3[trailLength * 2]; colors = new Color[trailLength * 2]; triangles = new int[trailLength * 6];
            for (int i = 0; i < trailLength; i++) data[i] = transform.position;
            for (int j = 0; j < trailLength; j++) uvf[j] = 1f - (j + 1f) / trailLength;
            for (int k = 0; k < trailLength - 1; k++)
            {
                float a = k / (trailLength - 1f); var c = new Color(a, a, a, a);
                colors[k * 2] = c; colors[k * 2 + 1] = c; colors[k * 2 + 2] = c; colors[k * 2 + 3] = c;
                uvs[k * 2] = new Vector2(uvf[k], 0f); uvs[k * 2 + 1] = new Vector2(uvf[k], 1f); uvs[k * 2 + 2] = new Vector2(0f, 0f); uvs[k * 2 + 3] = new Vector2(0f, 1f);
                triangles[k * 6] = k * 2; triangles[k * 6 + 1] = k * 2 + 1; triangles[k * 6 + 2] = k * 2 + 2; triangles[k * 6 + 3] = k * 2 + 2; triangles[k * 6 + 4] = k * 2 + 1; triangles[k * 6 + 5] = k * 2 + 3;
            }
            dTime = 0f; ready = true;
        }

        void Push(Vector3 p)
        {
            for (int i = 0; i < data.Length - 1; i++) data[i] = data[i + 1];
            data[data.Length - 1] = p;
            if (bUVScroll && uvs != null && uvs.Length >= 4)   // SetNewEdgeLine original: las UV avanzan un paso por punto y las primeras pasan al final (la textura se desplaza a lo largo de la cinta)
            {
                var u0 = uvs[0]; var u1 = uvs[1];
                for (int i = 0; i < data.Length - 1; i++) { uvs[i * 2] = uvs[i * 2 + 2]; uvs[i * 2 + 1] = uvs[i * 2 + 3]; }
                uvs[(data.Length - 1) * 2] = u0; uvs[(data.Length - 1) * 2 + 1] = u1;
            }
        }

        void LateUpdate()
        {
            if (!ready) return; var cam = Camera.main; if (cam == null) return;
            transform.rotation = Quaternion.identity;
            if (!seeded) { seeded = true; for (int i = 0; i < data.Length; i++) data[i] = transform.position; }   // Init() corre con el efecto aún en el origen: sembrar en la posición real (si no, haz desde (0,0,0) hasta el arma)
            Vector3 pos = transform.position, last = data[data.Length - 1];
            for (float t = fTimes; t < 1f && fTimes > 0.01f; t += fTimes) Push(Vector3.Lerp(last, pos, t));
            Push(pos);
            Vector3 axis = pos - last; if (axis.sqrMagnitude < 1e-10f) axis = transform.forward; axis.Normalize();
            dTime += Time.deltaTime; float inv = 1f / Mathf.Max(0.0001f, transform.lossyScale.x), hh = fHeight * 0.5f;
            var dir = new Vector3[data.Length];
            for (int i = 0; i < data.Length; i++) dir[i] = Quaternion.AngleAxis(i / (data.Length - 1f) * 360f + dTime * 720f, axis) * cam.transform.right;
            for (int j = 0; j < data.Length - 1; j++)
            {
                vertices[j * 2] = (data[j] + dir[j] * hh - pos) * inv; vertices[j * 2 + 1] = (data[j] - dir[j] * hh - pos) * inv;
                if (j == data.Length - 2) { vertices[j * 2 + 2] = (data[j + 1] + dir[j + 1] * hh - pos) * inv; vertices[j * 2 + 3] = (data[j + 1] - dir[j + 1] * hh - pos) * inv; }
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.triangles = triangles; mesh.colors = colors; mesh.uv = uvs; mesh.RecalculateBounds();
        }
    }
}
