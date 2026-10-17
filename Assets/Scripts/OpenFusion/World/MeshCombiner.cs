// MeshCombiner.cs -- port del script de mapa "Combiner" (Assembly-CSharp-first_pass/Combiner.cs), SIN COMPILAR EN UNITY.
// Original: MonoBehaviour con combine[] = { MeshFilter[] meshes, int[] subMeshIndices }. En Start() desactiva el renderer de todas las mallas del grupo, reparte el trabajo por fotogramas
// (kMaximumCombinerTimePerFrame = 1/150 s) y para cada grupo (>1 malla) hace new Mesh().CombineMeshes(instancias) con transform = meshes[0].worldToLocal * mesh[i].localToWorld,
// se la asigna a meshes[0].sharedMesh y vuelve a activar solo el renderer de meshes[0]. Es una optimizacion de draw calls: sin ella el mapa se ve igual (cada nodo se dibuja por separado).
// Esta version es CONSERVADORA: cada grupo se combina de una vez (no hay parpadeo) y solo si todo es seguro; si no, el grupo se deja tal cual.
// Interruptor: MeshCombiner.Enabled = false (estatico) antes de construir un mapa desactiva todo; con la cola ya llena se vacia en el siguiente fotograma.
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    /// <summary>Destruye la malla combinada cuando se destruye el nodo que la usa (los mapas se descargan).</summary>
    public sealed class CombinedMeshOwner : MonoBehaviour
    {
        public Mesh M;
        void OnDestroy() { if (M != null) Destroy(M); }
    }

    public sealed class MeshCombiner : MonoBehaviour
    {
        /// <summary>Interruptor global. false = no combina nada (igual que antes de portar el Combiner).</summary>
        public static bool Enabled = true;
        /// <summary>kMaximumCombinerTimePerFrame del original.</summary>
        public static float BudgetSeconds = 1f / 150f;
        public static int GroupsSeen, GroupsMerged, GroupsSkipped, RenderersHidden;

        sealed class Job { public GameObject[] Go; public int[] Sub; }
        static readonly Queue<Job> queue = new Queue<Job>();
        static MeshCombiner inst;
        static bool pendingLog;

        static int[] ToInts(object o)
        {
            if (o == null) return null;
            var ia = o as int[]; if (ia != null) return ia;
            var oa = o as object[]; if (oa == null) return null;
            var r = new int[oa.Length]; for (int i = 0; i < oa.Length; i++) r[i] = D.I(oa[i]); return r;
        }

        /// <summary>Lee los grupos del script Combiner (valor `v` del MonoBehaviour) y los pone en cola. `objs`: TransformId -> GameObject de esta construccion; `g`: grafo para pasar de MeshFilter a su nodo.</summary>
        public static void Collect(SceneGraph25 g, Dictionary<long, GameObject> objs, Dictionary<string, object> v)
        {
            if (!Enabled || g == null || objs == null || v == null) return;
            try
            {
                var arr = v["combine"] as object[]; if (arr == null) return;
                foreach (var e in arr)
                {
                    var grp = e as Dictionary<string, object>; GroupsSeen++;
                    if (grp == null || !grp.ContainsKey("meshes") || !grp.ContainsKey("subMeshIndices")) { GroupsSkipped++; continue; }
                    var ms = grp["meshes"] as object[]; var si = ToInts(grp["subMeshIndices"]);
                    if (ms == null || si == null || ms.Length < 2 || si.Length != ms.Length) { if (ms == null || ms.Length >= 2) GroupsSkipped++; continue; }   // el original solo combina grupos de mas de una malla
                    var gos = new GameObject[ms.Length]; bool ok = true;
                    for (int i = 0; i < ms.Length && ok; i++)
                    {
                        var pp = PPtr25.From(ms[i]); if (pp.FileId != 0 || pp.PathId == 0) { ok = false; break; }
                        var own = g.OwnerOf(pp.PathId); GameObject go = null;
                        if (own == null || !objs.TryGetValue(own.TransformId, out go) || go == null) ok = false; else gos[i] = go;
                    }
                    if (!ok) { GroupsSkipped++; continue; }
                    queue.Enqueue(new Job { Go = gos, Sub = si });
                }
                if (queue.Count > 0)
                {
                    pendingLog = true;
                    if (inst == null) { var h = new GameObject("MeshCombiner"); inst = h.AddComponent<MeshCombiner>(); }
                }
            }
            catch (System.Exception ex) { UnityEngine.Debug.LogWarning("[combiner] lectura: " + ex.Message); }
        }

        void Update()
        {
            if (!Enabled) { queue.Clear(); return; }
            var sw = Stopwatch.StartNew();
            while (queue.Count > 0)
            {
                var j = queue.Dequeue();
                try { if (Process(j)) GroupsMerged++; else GroupsSkipped++; }
                catch (System.Exception ex) { GroupsSkipped++; UnityEngine.Debug.LogWarning("[combiner] grupo: " + ex.Message); }
                if (sw.Elapsed.TotalSeconds > BudgetSeconds) break;
            }
            if (queue.Count == 0 && pendingLog)
            {
                pendingLog = false;
                UnityEngine.Debug.Log("[combiner] grupos vistos " + GroupsSeen + ", combinados " + GroupsMerged + ", omitidos " + GroupsSkipped + ", renderers ocultados " + RenderersHidden);
            }
        }

        /// <summary>Primer antecesor (o el propio nodo) inactivo; null si todo esta activo. Sirve para comprobar que todos los miembros se activan/desactivan a la vez.</summary>
        static Transform FirstInactive(Transform t)
        {
            while (t != null) { if (!t.gameObject.activeSelf) return t; t = t.parent; }
            return null;
        }

        /// <summary>Nodos que se mueven o que el juego usa por separado: el original los combinaria igual, el puerto los respeta (un cambio posterior los separaria de la malla fusionada).</summary>
        static bool Dynamic(Transform t)
        {
            if (EpTriggers.OwnerOf(t) != null) return true;
            for (int i = 0; i < 32 && t != null; i++, t = t.parent)
            {
                if (t.GetComponent<Animation>() != null || t.GetComponent<Animator>() != null || t.GetComponent<BillboardNode>() != null || t.GetComponent<Rigidbody>() != null) return true;
            }
            return false;
        }

        static bool Process(Job j)
        {
            int n = j.Go.Length; var mf = new MeshFilter[n]; var mr = new MeshRenderer[n]; Material m0 = null; Transform inactive0 = null; long verts = 0;
            for (int i = 0; i < n; i++)
            {
                var go = j.Go[i]; if (go == null) return false;
                mf[i] = go.GetComponent<MeshFilter>(); mr[i] = go.GetComponent<MeshRenderer>();
                if (mf[i] == null || mr[i] == null || !mr[i].enabled || !go.activeSelf || go.GetComponent<SkinnedMeshRenderer>() != null) return false;
                var mesh = mf[i].sharedMesh; if (mesh == null || !mesh.isReadable) return false;
                int s = j.Sub[i]; var sm = mr[i].sharedMaterials;
                if (s < 0 || s >= sm.Length || s >= mesh.subMeshCount || sm[s] == null) return false;
                if (i == 0) { m0 = sm[s]; inactive0 = FirstInactive(go.transform); }
                else
                {
                    if (sm[s] != m0) return false;                                                       // una sola malla/material por grupo, como los grupos horneados del original
                    if (FirstInactive(go.transform) != inactive0) return false;                          // se activan juntos
                    if (go.layer != j.Go[0].layer || mr[i].shadowCastingMode != mr[0].shadowCastingMode || mr[i].receiveShadows != mr[0].receiveShadows) return false;
                }
                if (Dynamic(go.transform)) return false;
                verts += mesh.vertexCount;
            }
            var inst0 = new CombineInstance[n]; var w2l = j.Go[0].transform.worldToLocalMatrix;
            for (int i = 0; i < n; i++)
            {
                inst0[i].mesh = mf[i].sharedMesh; inst0[i].subMeshIndex = j.Sub[i]; inst0[i].transform = w2l * j.Go[i].transform.localToWorldMatrix;
            }
            var comb = new Mesh(); comb.name = "combined_" + j.Go[0].name;
            if (verts > 65000) comb.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            comb.CombineMeshes(inst0, true, true);   // new Mesh().CombineMeshes(array) del original: mergeSubMeshes = true, useMatrices = true
            if (comb.vertexCount == 0) { Destroy(comb); return false; }
            var owner = j.Go[0].GetComponent<CombinedMeshOwner>(); if (owner == null) owner = j.Go[0].AddComponent<CombinedMeshOwner>();
            if (owner.M != null) Destroy(owner.M);
            owner.M = comb;
            mf[0].sharedMesh = comb; mr[0].sharedMaterials = new[] { m0 };
            for (int i = 1; i < n; i++) if (mr[i] != mr[0]) { mr[i].enabled = false; RenderersHidden++; }
            return true;
        }
    }
}
