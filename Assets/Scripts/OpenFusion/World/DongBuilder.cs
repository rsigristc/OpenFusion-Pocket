// Construye en Unity los GameObjects de una escena Map_XX_YY a partir de SceneGraph25 + AssetStore. SIN COMPILAR NI PROBAR (UnityEngine).
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OpenFusion.Materials;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public sealed class DongBuildResult { public GameObject Root; public Terrain Terrain; public DongColors Colors; public int X, Y; public byte[] Attributes; public List<GameObject> All = new List<GameObject>(); public Dictionary<long, GameObject> Nodes = new Dictionary<long, GameObject>(); }

    public sealed class DongBuilder
    {
        readonly AssetStore store;
        readonly Dictionary<(string, long), Mesh> meshCache = new Dictionary<(string, long), Mesh>();
        readonly Dictionary<(string, long), Material> matCache = new Dictionary<(string, long), Material>();
        readonly Dictionary<(string, long), Texture2D> texCache = new Dictionary<(string, long), Texture2D>();
        public bool CompressTextures;
        /// Los materiales "collision_*" (alfa 0 + alpha test) solo marcan volúmenes de colisión: no se dibujan (ahorra draw calls en móvil).
        public bool HideCollisionVolumes = true;
        /// "indun_roof_*": techo negro de interiores/instancias; sin la lógica del cliente original queda como un parche negro sobre el mapa.
        public bool HideIndoorRoofs = true;
        public DongBuilder(AssetStore s) { store = s; }

        Texture2D Tex(string owner, int fileId, long pathId)
        {
            var r = store.Resolve(owner, fileId, pathId); if (!r.Ok) return null; var k = (r.FileName.ToLowerInvariant(), pathId);
            if (!texCache.TryGetValue(k, out var t)) { var t25 = store.Texture(r); t = texCache[k] = t25 == null ? null : UnityConverters.ToTexture(t25, CompressTextures); }
            return t;
        }
        // MeshCollider exige al menos un triángulo; una malla vacía o con índices descartados registra un error en consola.
        static bool HasTriangles(Mesh m) { for (int i = 0; i < m.subMeshCount; i++) if (m.GetIndexCount(i) >= 3) return true; return false; }
        Mesh MeshOf(string owner, PPtr25 p)
        {
            var r = store.Resolve(owner, p); if (!r.Ok) return null; var k = (r.FileName.ToLowerInvariant(), p.PathId);
            if (!meshCache.TryGetValue(k, out var m)) { var m25 = store.Mesh(r); m = meshCache[k] = (m25 == null || m25.Positions.Length == 0) ? null : UnityConverters.ToMesh(m25); }
            return m;
        }
        Material MaterialOf(string owner, PPtr25 p)
        {
            var r = store.Resolve(owner, p); if (!r.Ok) return null; var k = (r.FileName.ToLowerInvariant(), p.PathId);
            if (matCache.TryGetValue(k, out var m)) return m;
            var spec = store.Material(r, out var mo); m = spec == null ? null : MaterialBinder.Create(spec, ts => Tex(mo, ts.FileId, ts.PathId));
            return matCache[k] = m;
        }

        public DongBuildResult Build(string sceneFileName, Transform parent = null)
        {
            var g = SceneGraph25.Build(store.Get(sceneFileName));
            return BuildNodes(sceneFileName, g, g.ByTransform.Values.ToList(), parent);
        }

        /// Ensambla un prefab (p. ej. NPC, nano, sombrero, vehículo) de un bundle: el subárbol cuyo GameObject raíz se llama `rootName`
        /// (malla con huesos + materiales + componente Animation con sus clips). null si no existe.
        readonly Dictionary<string, SceneGraph25> graphCache = new Dictionary<string, SceneGraph25>();
        public SceneGraph25 GraphOf(string fileName) { if (!graphCache.TryGetValue(fileName, out var g)) g = graphCache[fileName] = SceneGraph25.Build(store.Get(fileName)); return g; }
        /// Libera el grafo cacheado de un bundle (llamar al descargarlo del AssetStore).
        public void Forget(string fileName)
        {
            graphCache.Remove(fileName);
            string owner = fileName.ToLowerInvariant();
            // Quitar referencias permite a UnloadUnusedAssets liberar los objetos
            // que ya no usa ningun renderer, sin destruir recursos aun compartidos.
            foreach (var k in meshCache.Keys.Where(k => k.Item1 == owner).ToList()) meshCache.Remove(k);
            foreach (var k in matCache.Keys.Where(k => k.Item1 == owner).ToList()) matCache.Remove(k);
            foreach (var k in texCache.Keys.Where(k => k.Item1 == owner).ToList()) texCache.Remove(k);
            foreach (var k in clipCache.Keys.Where(k => k.Item1 == owner).ToList()) clipCache.Remove(k);
            foreach (var k in trackCache.Keys.Where(k => k.Item1 == owner).ToList()) trackCache.Remove(k);
        }

        /// Ensambla el prefab cuyo GameObject raíz es `go` (Ref a un GameObject, p. ej. el destino de una ruta del AssetIndex). El objeto
        /// puede estar en cualquier nivel del bundle: se construye su subárbol. null si no es un GameObject del grafo.
        public GameObject BuildPrefabAt(Ref go, Transform parent = null) => BuildPrefabResultAt(go, parent)?.Root;

        /// Igual que BuildPrefabAt pero devuelve también el mapa transformId -> GameObject (necesario para resolver PPtr a huesos).
        public DongBuildResult BuildPrefabResultAt(Ref go, Transform parent = null)
        {
            if (!go.Ok || store.TypeOf(go) != "GameObject") return null;
            var g = GraphOf(go.FileName); var root = g.ByTransform.Values.FirstOrDefault(n => n.GameObjectId == go.Info.PathId); if (root == null) return null;
            var list = new List<SceneNode25>(); void Walk(SceneNode25 n) { list.Add(n); foreach (var c in n.Children) Walk(c); } Walk(root);
            return BuildNodes(go.FileName, g, list, parent);
        }

        /// Material/malla/textura de un PPtr relativo al archivo `owner` (para quien monta cosas a mano, p. ej. el avatar por piezas).
        public Material MaterialAt(string owner, PPtr25 p) => MaterialOf(owner, p);
        public Texture2D TextureAt(Ref r) => r.Ok ? Tex(r.FileName, 0, r.Info.PathId) : null;

        public GameObject BuildPrefab(string fileName, string rootName, Transform parent = null)
        {
            var g = GraphOf(fileName);
            var root = g.Roots.FirstOrDefault(r => r.Name == rootName); if (root == null) return null;
            var list = new List<SceneNode25>(); void Walk(SceneNode25 n) { list.Add(n); foreach (var c in n.Children) Walk(c); } Walk(root);
            return BuildNodes(fileName, g, list, parent).Root;
        }

        /// Nombres de los prefabs (raíces) de un bundle, para listar/buscar.
        public IEnumerable<string> PrefabNames(string fileName) => GraphOf(fileName).Roots.Select(r => r.Name);

        readonly Dictionary<(string, long), AnimationClip> clipCache = new Dictionary<(string, long), AnimationClip>();
        readonly Dictionary<(string, long), ClipCurves.ClipTracks> trackCache = new Dictionary<(string, long), ClipCurves.ClipTracks>();   // clips "nif-default" -> pistas para NifClipDriver
        AnimationClip ClipOf(string owner, PPtr25 p)
        {
            var r = store.Resolve(owner, p); if (!r.Ok || store.TypeOf(r) != "AnimationClip") return null; var k = (r.FileName.ToLowerInvariant(), p.PathId);
            if (!clipCache.TryGetValue(k, out var c))
            {
                var c25 = Clip25.From(store.Read(r)); c = clipCache[k] = ClipBuilder.ToClip(c25);
                if (ClipBuilder.UseNifDriver && c25.Name == ClipBuilder.NifClipName) { var tk = ClipCurves.BuildTracks(c25); if (tk.Any) trackCache[k] = tk; }
            }
            return c;
        }
        /// Pistas del clip (null si no es un "nif-default" con curvas). Llamar despues de ClipOf.
        ClipCurves.ClipTracks TracksOf(string owner, PPtr25 p)
        {
            if (p.PathId == 0) return null; var r = store.Resolve(owner, p); if (!r.Ok) return null; var k = (r.FileName.ToLowerInvariant(), p.PathId);
            return trackCache.TryGetValue(k, out var tk) ? tk : null;
        }

        void ApplySkinAndAnimation(string file, SceneNode25 n, GameObject go, Dictionary<long, GameObject> objs)
        {
            if (n.Skin != null)
            {
                var mesh = MeshOf(file, n.Skin.Mesh);
                if (mesh != null)
                {
                    var smr = go.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = mesh;
                    var bones = n.Skin.Bones.Select(id => objs.TryGetValue(id, out var b) ? b.transform : go.transform).ToArray();
                    smr.bones = bones;
                    var set = new HashSet<Transform>(bones);   // raíz del esqueleto: el hueso cuyo padre no es hueso
                    smr.rootBone = bones.FirstOrDefault(b => b.parent == null || !set.Contains(b.parent)) ?? go.transform;
                    var mats = n.Materials.Select(m => MaterialOf(file, m)).ToArray();
                    for (int i = 0; i < mats.Length; i++) if (mats[i] == null) mats[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    smr.sharedMaterials = mats;
                    smr.updateWhenOffscreen = true;   // el archivo 2.5 no trae AABB; los límites de la malla en pose de bind no sirven animados
                }
            }
            if (n.Anim != null)
            {
                var anim = go.AddComponent<Animation>(); go.AddComponent<AnimEventSink>(); var used = new HashSet<string>();
                foreach (var cp in n.Anim.Clips.Where(c => c.PathId != 0))
                {
                    var clip = ClipOf(file, cp); if (clip == null) continue;
                    var name = clip.name; for (int i = 2; !used.Add(name); i++) name = clip.name + "#" + i;   // nombres duplicados ("nif-default")
                    anim.AddClip(clip, name);
                }
                var def = n.Anim.Default.PathId != 0 ? ClipOf(file, n.Anim.Default) : null; if (def != null) { if (anim.GetClip(def.name) == null) anim.AddClip(def, def.name); anim.clip = def; }
                anim.wrapMode = (WrapMode)n.Anim.WrapMode; anim.playAutomatically = n.Anim.PlayAutomatically;
                // Clip "nif-default" (objetos de mapa): lo reproduce NifClipDriver (curvas con repeticion propia, material por componentes, sin culling); el Animation queda sin autoplay.
                ClipCurves.ClipTracks tracks = n.Anim.Default.PathId != 0 ? TracksOf(file, n.Anim.Default) : null;
                if (tracks == null && n.Anim.Default.PathId == 0)
                {
                    int cnt = 0; PPtr25 only = default(PPtr25);
                    foreach (var cp1 in n.Anim.Clips) if (cp1.PathId != 0) { cnt++; only = cp1; }
                    if (cnt == 1) tracks = TracksOf(file, only);
                }
                if (tracks != null) { anim.playAutomatically = false; var drv = go.AddComponent<NifClipDriver>(); drv.Init(tracks, anim); }
                else if (n.Anim.PlayAutomatically && anim.clip != null && go.activeInHierarchy) anim.Play();
            }
        }

        int primColliders, meshColliders;
        // El conversor sólo leía MeshCollider; el cliente original usa también Box/Sphere/CapsuleCollider (sillas, cajas, troncos...).
        void AddPrimitiveCollider(string file, string type, long pathId, GameObject go)
        {
            var v = store.Read(store.Resolve(file, 0, pathId)); if (v.ContainsKey("m_IsTrigger") && D.I(v["m_IsTrigger"]) != 0) return;
            var c = v.ContainsKey("m_Center") ? D.V3(v["m_Center"]) : new float[3]; var ctr = new Vector3(c[0], c[1], c[2]);
            if (type == "BoxCollider") { var sz = D.V3(v["m_Size"]); var b = go.AddComponent<BoxCollider>(); b.center = ctr; b.size = new Vector3(sz[0], sz[1], sz[2]); }
            else if (type == "SphereCollider") { var sc = go.AddComponent<SphereCollider>(); sc.center = ctr; sc.radius = D.F(v["m_Radius"]); }
            else { var cc = go.AddComponent<CapsuleCollider>(); cc.center = ctr; cc.radius = D.F(v["m_Radius"]); cc.height = D.F(v["m_Height"]); cc.direction = v.ContainsKey("m_Direction") ? D.I(v["m_Direction"]) : 1; }
            primColliders++;
        }

        List<Dictionary<string, object>> combineRaw = new List<Dictionary<string, object>>();   // scripts Combiner de la construccion en curso (se procesan al final de BuildNodes)
        int bnDepth;   // BuildNodes es reentrante (los efectos de partículas construyen sus prefabs dentro de la escena)
        DongBuildResult BuildNodes(string sceneFileName, SceneGraph25 g, List<SceneNode25> nodes, Transform parent)
        {
            var res = new DongBuildResult(); if (bnDepth == 0) { primColliders = 0; meshColliders = 0; } bnDepth++; var savedCombine = combineRaw; combineRaw = new List<Dictionary<string, object>>();
            var objs = new Dictionary<long, GameObject>();
            foreach (var n in nodes)
            {
                var go = new GameObject(string.IsNullOrEmpty(n.Name) ? "node" + n.TransformId : n.Name) { layer = Mathf.Clamp(n.Layer, 0, 31) };
                go.SetActive(n.Active); objs[n.TransformId] = go; res.All.Add(go); res.Nodes[n.TransformId] = go;
            }
            foreach (var n in nodes)
            {
                var go = objs[n.TransformId]; var t = go.transform;
                t.SetParent(n.Parent != null && objs.ContainsKey(n.Parent.TransformId) ? objs[n.Parent.TransformId].transform : parent, false);
                t.localPosition = new Vector3(n.Pos[0], n.Pos[1], n.Pos[2]); t.localRotation = new Quaternion(n.Rot[0], n.Rot[1], n.Rot[2], n.Rot[3]); t.localScale = new Vector3(n.Scale[0], n.Scale[1], n.Scale[2]);
                if (res.Root == null && (n.Parent == null || !objs.ContainsKey(n.Parent.TransformId))) res.Root = go;
                if (n.Mesh.PathId != 0)
                {
                    var mesh = MeshOf(sceneFileName, n.Mesh);
                    if (mesh != null)
                    {
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        if (n.Materials.Count > 0)
                        {
                            var mr = go.AddComponent<MeshRenderer>(); var mats = n.Materials.Select(p => MaterialOf(sceneFileName, p)).ToArray();
                            for (int i = 0; i < mats.Length; i++) if (mats[i] == null) mats[i] = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                            mr.sharedMaterials = mats;
                            if (n.ColliderMesh.PathId == 0 && HasTriangles(mesh) && mats.Length > 0 && mats.All(m => m != null && m.name.StartsWith("collision_", StringComparison.OrdinalIgnoreCase))) { go.AddComponent<MeshCollider>().sharedMesh = mesh; primColliders++; }   // geometría de colisión del original: malla con material collision_*
                            bool allHidden = mats.Length > 0 && mats.All(m => m != null && ((HideCollisionVolumes && m.name.StartsWith("collision_", StringComparison.OrdinalIgnoreCase)) || (HideIndoorRoofs && m.name.StartsWith("indun_roof", StringComparison.OrdinalIgnoreCase))));
                            if (allHidden) mr.enabled = false;
                        }
                    }
                }
                if (n.ColliderMesh.PathId != 0) { var cm = MeshOf(sceneFileName, n.ColliderMesh); if (cm != null && HasTriangles(cm)) { go.AddComponent<MeshCollider>().sharedMesh = cm; meshColliders++; } }
                foreach (var pc in n.Components) { if (pc.Key != "BoxCollider" && pc.Key != "SphereCollider" && pc.Key != "CapsuleCollider") continue; try { AddPrimitiveCollider(sceneFileName, pc.Key, pc.Value, go); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[dong] collider " + pc.Key + ": " + ex.Message); } }
                WaterSurface.TryRegister(go);   // ffWater / OpenFusion/Water: colisionador de agua en la capa 2 (ver WaterSurface.cs); sustituye al MeshCollider propio del nodo
                foreach (var c in n.Components.Where(c => c.Key == "MonoBehaviour")) ApplyScript(sceneFileName, c.Value, n, go, res);
                try { SetupTrail(sceneFileName, n, go); } catch (Exception e) { UnityEngine.Debug.LogWarning("[fx] estela: " + e.Message); }
                if (n.Components.Any(c => c.Key == "EllipsoidParticleEmitter")) { try { BuildLegacyParticles(sceneFileName, n, go); } catch (Exception e) { UnityEngine.Debug.LogWarning("[fx] partículas: " + e.Message); } }
            }
            foreach (var n in nodes) if (n.Skin != null || n.Anim != null) ApplySkinAndAnimation(sceneFileName, n, objs[n.TransformId], objs);   // los huesos ya existen
            try { foreach (var cv in combineRaw) MeshCombiner.Collect(g, objs, cv); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[combiner] " + ex.Message); }   // Combiner: fusion de mallas (World/MeshCombiner.cs)
            combineRaw = savedCombine;
            bnDepth--;
            if (bnDepth == 0 && (fxBuilt > 0 || fxMissing > 0)) { UnityEngine.Debug.Log("[fx] sistemas de partículas: " + fxBuilt + ", prefabs de efecto sin resolver: " + fxMissing); fxBuilt = fxMissing = 0; }
            if (bnDepth == 0 && (primColliders > 0 || meshColliders > 0)) UnityEngine.Debug.Log("[dong] colliders primitivos añadidos: " + primColliders + ", MeshCollider: " + meshColliders);
            return res;
        }

        void ApplyScript(string file, long pathId, SceneNode25 n, GameObject go, DongBuildResult res)
        {
            var r = store.Resolve(file, 0, pathId); var v = store.Read(r);
            if (v.ContainsKey("fogDepth"))   // DongColorSetup
            {
                res.X = D.I(v["x"]); res.Y = D.I(v["y"]);
                res.Colors = new DongColors { Set = true, FogDepth = D.F(v["fogDepth"]), Fog = Col(v["fogColor"]), Sky = Col(v["skyColor"]), Light = Col(v["lightColor"]) };
            }
            else if (v.ContainsKey("attributes")) res.Attributes = (byte[])v["attributes"];   // MapAttributeTable
            else if (v.ContainsKey("m_TerrainData"))                                          // Terrain
            {
                var tdp = PPtr25.From(v["m_TerrainData"]); var tdr = store.Resolve(file, tdp); if (!tdr.Ok) return;
                var t25 = Terrain25.From(store.Read(tdr));
                var tgo = TerrainBuilder.Build(t25, p => Tex(tdr.FileName, p.FileId, p.PathId), resolveAlphaData: p =>
                {
                    var alphaRef = store.Resolve(tdr.FileName, p);
                    return alphaRef.Ok ? store.Texture(alphaRef) : null;
                });
                tgo.transform.SetParent(go.transform, false); tgo.layer = go.layer; res.Terrain = tgo.GetComponent<Terrain>();
            }
            else if (v.ContainsKey("combine") && !v.ContainsKey("triggerType")) combineRaw.Add(v);   // Combiner (692 instancias): se resuelve al final de BuildNodes
            else if (v.ContainsKey("mode") && !v.ContainsKey("effectName") && v.Count <= 6 && go.name.IndexOf("Billboard", System.StringComparison.OrdinalIgnoreCase) >= 0)   // BillboardNode: planos *_BillboardCamera (halos de luz) que miran a la cámara
            { var bb = go.GetComponent<BillboardNode>(); if (bb == null) bb = go.AddComponent<BillboardNode>(); bb.Mode = D.I(v["mode"]); }
            else if (v.ContainsKey("particleElements"))   // EPElementController: efectos ES[n] de objetos EP (flechas de jumppad/launcher, símbolos de carrera/cohete)
            { try { AttachParticleElements(v, go); } catch (Exception e) { UnityEngine.Debug.LogWarning("[fx] particleElements: " + e.Message); } }
            else if (v.ContainsKey("effectName") && (v.ContainsKey("particles") || v.ContainsKey("nifObject")))                // EffectEmitterController: fogatas, barreras, motores...
            { try { AttachEffects(file, v, go); } catch (Exception e) { UnityEngine.Debug.LogWarning("[fx] efecto '" + v["effectName"] + "': " + e.Message); } }
            else if (v.ContainsKey("triggerType") && v.ContainsKey("objectId") && D.I(v["triggerType"]) != 2)   // EpTrigger (plataforma, jumppad, aro, lanzador, tobogan, cuerda, cinta, interruptor): EpParse -> EpTriggers.Register
            { EpParse.Handle(store, file, n, v, go); }
            else if (v.ContainsKey("from") && v.ContainsKey("to") && v.ContainsKey("velocity"))   // EpZiplineTrigger: se registra en ZiplineRide
            {
                try
                {
                    string shape = null; float[] ctr = null, size = null; float rr = 0f, hh = 0f;
                    foreach (var pc in n.Components)
                    {
                        if (pc.Key != "BoxCollider" && pc.Key != "SphereCollider" && pc.Key != "CapsuleCollider") continue;
                        var cv = store.Read(store.Resolve(file, 0, pc.Value)); shape = pc.Key; ctr = cv.ContainsKey("m_Center") ? D.V3(cv["m_Center"]) : null;
                        if (pc.Key == "BoxCollider") size = D.V3(cv["m_Size"]); else { rr = D.F(cv["m_Radius"]); if (cv.ContainsKey("m_Height")) hh = D.F(cv["m_Height"]); }
                        break;
                    }
                    ZiplineRide.Register(go, D.V3(v["from"]), D.V3(v["to"]), D.F(v["velocity"]), v.ContainsKey("radius") ? D.F(v["radius"]) : 0f, shape, ctr, size, rr, hh);
                }
                catch (Exception e) { UnityEngine.Debug.LogWarning("[zipline] registro: " + e.Message); }
            }
        }

        // ---- Partículas de Unity 2.5 (ver LegacyParticles.cs) ----
        int fxBuilt, fxMissing, fxLogged;
        // ParticleTrailController (script con fHeight/trailLength) + ParticleEmitterController del mismo nodo: la malla se genera en ejecución (ParticleTrail.cs)
        void SetupTrail(string file, SceneNode25 n, GameObject go)
        {
            bool trail = false, trailScroll = false; Dictionary<string, object> pec = null;
            foreach (var c in n.Components)
            {
                if (c.Key != "MonoBehaviour") continue; var r = store.Resolve(file, 0, c.Value); if (!r.Ok) continue; var v = store.Read(r);
                if (v.ContainsKey("trailLength") && v.ContainsKey("fHeight")) { trail = true; trailScroll = v.ContainsKey("bUVScroll") && D.I(v["bUVScroll"]) != 0; } else if (v.ContainsKey("m_iGenType")) pec = v;
            }
            if (!trail) return;
            var t = go.AddComponent<ParticleTrail>(); t.bUVScroll = trailScroll;
            if (pec != null) { t.InitialSize = pec.ContainsKey("initialSize") ? D.F(pec["initialSize"]) : 1f; t.Number = pec.ContainsKey("numberPerGeneration") ? D.F(pec["numberPerGeneration"]) : 30f; }
            if (n.Materials != null && n.Materials.Count > 0) t.Mat = MaterialOf(file, n.Materials[0]);
        }
        void BuildLegacyParticles(string file, SceneNode25 n, GameObject go)
        {
            Dictionary<string, object> em = null, an = null, rd = null, pec = null; Material mat = null;
            foreach (var c in n.Components)
            {
                var r = store.Resolve(file, 0, c.Value); if (!r.Ok) continue;
                if (c.Key == "EllipsoidParticleEmitter") em = store.Read(r);
                else if (c.Key == "ParticleAnimator") an = store.Read(r);
                else if (c.Key == "ParticleRenderer")
                {
                    rd = store.Read(r); object mo;
                    if (rd.TryGetValue("m_Materials", out mo) && mo is object[] ma && ma.Length > 0) mat = MaterialOf(file, PPtr25.From(ma[0]));
                }
                else if (c.Key == "MonoBehaviour") { var v = store.Read(r); if (v.ContainsKey("m_iGenType")) pec = v; }
            }
            if (em == null) return;
            LegacyParticles.Build(go, pec, em, an, rd, mat); fxBuilt++;
        }

        /// Gancho para instanciar un efecto ES[n] del bundle Effects (EffectController.InstantiateEffect). Lo asigna quien tenga el ModelFactory/AssetIndex (ver BulletFx.Spawn);
        /// la función debe devolver el prefab ya construido y activado (o null). Si es null, EPElementController no hace nada (estado anterior).
        public Func<int, GameObject> EffectFactory;
        int epElemLogged;
        // EPElementController.Start: por cada elemento, InstantiateEffect(index, host.position + position * host.localScale.x, rotation); si scale != 1 SetParticleScale(scale * host.localScale.x); parent = host.
        void AttachParticleElements(Dictionary<string, object> v, GameObject host)
        {
            if (EffectFactory == null || !(v["particleElements"] is object[] els)) return;
            foreach (var eo in els)
            {
                var e = D.O(eo); int idx = D.I(e["particleIndex"]); var p = D.V3(e["position"]); var q = D.V4(e["rotation"]); float sc = e.ContainsKey("scale") ? D.F(e["scale"]) : 1f;
                var fx = EffectFactory(idx); if (fx == null) continue;
                float hs = host.transform.lossyScale.x;
                fx.transform.SetParent(host.transform, false); fx.transform.position = host.transform.position + new Vector3(p[0], p[1], p[2]) * hs; fx.transform.rotation = new Quaternion(q[0], q[1], q[2], q[3]);
                float s = (sc != 1f ? sc : 1f) * hs; fx.transform.localScale = Vector3.one * (hs > 1e-4f ? s / hs : 1f);
                foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; }
                if (epElemLogged++ < 6) UnityEngine.Debug.Log("[fx] EPElement ES[" + idx + "] en " + host.name + " escala=" + s.ToString("F2"));
            }
        }

        void AttachEffects(string file, Dictionary<string, object> v, GameObject host)
        {
            var parts = v.ContainsKey("particles") ? v["particles"] as object[] : null;
            if (parts == null || parts.Length == 0) { AttachNifObject(file, v, host); return; }   // ES[865]/ES[866] (marcador de mision): solo nifObject (malla holograma), sin particulas
            AttachNifObject(file, v, host);
            var keysComp = host.AddComponent<LegacyEffectKeys>(); keysComp.MaxTimer = v.ContainsKey("maxTimer") ? D.F(v["maxTimer"]) : -1f;
            foreach (var po in parts)
            {
                var p = D.O(po); if (!p.ContainsKey("particlePrefab")) continue;
                var r = store.Resolve(file, PPtr25.From(p["particlePrefab"])); if (!r.Ok) { fxMissing++; continue; }
                var sub = BuildPrefabResultAt(r, host.transform); if (sub == null || sub.Root == null) { fxMissing++; continue; }
                sub.Root.SetActive(true);   // los prefabs PS[n] vienen con m_IsActive=0 en el bundle; el original los muestra igualmente
                var ps = sub.Root.GetComponent<ParticleSystem>(); if (ps == null) ps = sub.Root.GetComponentInChildren<ParticleSystem>();
                if (ps == null)
                {
                    var trail = sub.Root.GetComponentInChildren<ParticleTrail>(true);
                    if (trail != null)   // EffectEmitterController: fHeight = escala*initialSize, trailLength = numberPerGeneration, fTimes = TrailTimes
                    {
                        trail.fHeight = host.transform.localScale.x * trail.InitialSize; trail.trailLength = (int)trail.Number; trail.fTimes = v.ContainsKey("TrailTimes") ? D.F(v["TrailTimes"]) : 0.25f; trail.Init();
                        sub.Root.transform.localPosition = Vector3.zero; if (fxLogged++ < 8) UnityEngine.Debug.Log("[fx] estela " + host.name + " largo=" + trail.trailLength + " alto=" + trail.fHeight + " mat=" + (trail.Mat != null && trail.Mat.shader != null ? trail.Mat.shader.name : "NULL"));
                    }
                    continue;
                }
                var keys = new List<LegacyEffectKeys.Key>();
                if (p.ContainsKey("scriptKeys")) foreach (var ko in D.A(p["scriptKeys"]))
                { var kd = D.O(ko); var t = D.V3(kd["translate"]); keys.Add(new LegacyEffectKeys.Key { Time = D.F(kd["time"]), Translate = new Vector3(t[0], t[1], t[2]), GenType = D.I(kd["genType"]) }); }
                var tr = sub.Root.transform; tr.localRotation = Quaternion.identity; tr.localScale = Vector3.one;
                tr.localPosition = keys.Count > 0 ? keys[0].Translate : Vector3.zero;   // el original: localPosition = scriptKeys[0].translate
                bool on = keys.Count == 0 || keys[0].GenType != 0; var em = ps.emission; em.enabled = on; ps.Play(true); if (fxLogged++ < 8) UnityEngine.Debug.Log("[fx] " + host.name + " -> " + sub.Root.name + " activo=" + sub.Root.activeInHierarchy + " emite=" + on + " render=" + (ps.GetComponent<ParticleSystemRenderer>().enabled) + " mat=" + (ps.GetComponent<ParticleSystemRenderer>().sharedMaterial != null ? ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name : "NULL") + " pos=" + sub.Root.transform.position);
                bool dyn = false; for (int i = 1; i < keys.Count; i++) if (keys[i].GenType != keys[0].GenType || (keys[i].Translate - keys[0].Translate).sqrMagnitude > 1e-6f) dyn = true;
                if (dyn) keysComp.Entries.Add(new LegacyEffectKeys.Entry { Ps = ps, T = tr, Keys = keys.ToArray(), LastEmit = on });
            }
            if (keysComp.Entries.Count == 0) UnityEngine.Object.Destroy(keysComp);
        }
        // EffectEmitterController.nifObject: modelo animado en bucle hijo del host (pantalla/plataforma del respawn, barreras...). El original: Instantiate + animation.wrapMode = Loop + localScale = 1.
        void AttachNifObject(string file, Dictionary<string, object> v, GameObject host)
        {
            object no; if (!v.TryGetValue("nifObject", out no)) return; var np = PPtr25.From(no); if (np.PathId == 0) return;
            var r = store.Resolve(file, np); if (!r.Ok) { fxMissing++; return; }
            var sub = BuildPrefabResultAt(r, host.transform); if (sub == null || sub.Root == null) { fxMissing++; return; }
            var root = sub.Root; foreach (var tr0 in root.GetComponentsInChildren<Transform>(true)) tr0.gameObject.SetActive(true); var t = root.transform;   // en estos prefabs todos los nodos vienen con m_IsActive=0
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;   // original: Instantiate(nifObject, host.position, host.rotation) + parent + localScale = one
            foreach (var tr in root.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = host.layer;
            foreach (var nd in root.GetComponentsInChildren<NifClipDriver>(true)) nd.LoopWhole = true;   // original: animation.wrapMode = Loop (el clip entero repite con su duracion)
            var an = root.GetComponent<Animation>();
            if (an != null && root.GetComponent<NifClipDriver>() == null)   // con NifClipDriver el clip lo reproduce el driver (per-curva); el Animation legacy solo si no hay pistas
            {
                an.wrapMode = WrapMode.Loop; an.playAutomatically = true;
                if (an.clip == null) foreach (AnimationState st in an) { an.clip = st.clip; break; }
                if (an.clip != null) an.Play();
            }
            if (nifNames.Add(root.name))
            {
                var rds = root.GetComponentsInChildren<Renderer>(true); var sb = new System.Text.StringBuilder();
                foreach (var rd in rds) { var m0 = rd.sharedMaterial; sb.Append("\n    " + rd.name + " en=" + rd.enabled + " act=" + rd.gameObject.activeInHierarchy + " mat=" + (m0 != null ? m0.name + "/" + m0.shader.name + " tex=" + (m0.HasProperty("_MainTex") && m0.mainTexture != null ? m0.mainTexture.name : "NULL") : "NULL") + " bounds=" + rd.bounds.size); }
                UnityEngine.Debug.Log("[fx] nifObject " + root.name + " en " + host.name + " pos=" + root.transform.position + " anim=" + (an != null && an.clip != null ? an.clip.name : "-") + " renderers=" + rds.Length + sb);
            }
        }
        readonly HashSet<string> nifNames = new HashSet<string>();
        static float[] Col(object o) { var d = D.O(o); return new[] { D.F(d["r"]), D.F(d["g"]), D.F(d["b"]), D.F(d["a"]) }; }
    }
}
