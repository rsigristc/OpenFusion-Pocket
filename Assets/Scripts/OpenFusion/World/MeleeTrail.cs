using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenFusion.World
{
    // Legacy SwordTrailUtility: odd/even Stag pairs mark both edges of the blade.
    // A bounded ribbon avoids allocating geometry every animation frame on Android.
    public sealed class MeleeTrail : MonoBehaviour
    {
        const int Samples = 30;
        Transform top, bottom; Animation owner; Mesh mesh; Material material;
        readonly Vector3[] vertices = new Vector3[Samples * 2];
        readonly Vector3[] local = new Vector3[Samples * 2];
        bool emitting;
        public static void Attach(GameObject weapon, Animation actor, Color color)
        {
            var tags = new Dictionary<int, Transform>();
            foreach (var t in weapon.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Stag", System.StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(t.name.Substring(4), out int id)) tags[id] = t;
            foreach (var pair in tags)
            {
                if (pair.Key % 2 == 0 || !tags.TryGetValue(pair.Key + 1, out var end)) continue;
                var go = new GameObject("Melee trail"); go.transform.SetParent(weapon.transform, false);
                var trail = go.AddComponent<MeleeTrail>(); trail.top = pair.Value; trail.bottom = end;
                trail.owner = actor; trail.Build(color);
            }
        }
        void Build(Color color)
        {
            mesh = new Mesh { name = "Melee ribbon" }; mesh.MarkDynamic();
            var uv = new Vector2[Samples * 2]; var colors = new Color[Samples * 2]; var triangles = new int[(Samples - 1) * 6];
            for (int i = 0; i < Samples; i++)
            {
                float f = (float)i / (Samples - 1); uv[i * 2] = new Vector2(f, 0); uv[i * 2 + 1] = new Vector2(f, 1);
                colors[i * 2] = colors[i * 2 + 1] = new Color(color.r, color.g, color.b, f * .65f);
                if (i == Samples - 1) continue;
                int k = i * 6, v = i * 2; triangles[k] = v; triangles[k + 1] = v + 1; triangles[k + 2] = v + 2;
                triangles[k + 3] = v + 2; triangles[k + 4] = v + 1; triangles[k + 5] = v + 3;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Shader.Find("Sprites/Default"));
            var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.enabled = false;
        }
        public void SetEmitting(bool value)
        {
            if (mesh == null || top == null || bottom == null) return;
            if (value && !emitting)
            {
                for (int i = 0; i < Samples; i++) { vertices[i * 2] = top.position; vertices[i * 2 + 1] = bottom.position; }
            }
            emitting = value; GetComponent<MeshRenderer>().enabled = value;
        }
        void LateUpdate()
        {
            if (!emitting || mesh == null) return;
            bool attacking = false;
            if (owner != null) foreach (AnimationState state in owner)
                if (state.enabled && state.weight > .05f && state.name.Contains("attack")) { attacking = true; break; }
            if (!attacking || top == null || bottom == null) { SetEmitting(false); return; }
            System.Array.Copy(vertices, 2, vertices, 0, vertices.Length - 2);
            vertices[vertices.Length - 2] = top.position; vertices[vertices.Length - 1] = bottom.position;
            // Store world positions between frames so avatar movement doesn't drag old samples.
            for (int i = 0; i < vertices.Length; i++) local[i] = transform.InverseTransformPoint(vertices[i]);
            mesh.vertices = local; mesh.RecalculateBounds();
        }
        void OnDisable() { SetEmitting(false); }
        void OnDestroy()
        {
            if (Application.isPlaying) { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
            else { if (mesh != null) DestroyImmediate(mesh); if (material != null) DestroyImmediate(material); }
        }
    }
}
