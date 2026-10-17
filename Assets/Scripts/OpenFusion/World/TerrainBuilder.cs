// Construye un Terrain de Unity moderno a partir de Terrain25. Depende de UnityEngine: NO compilado/probado aquí.
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Unity25;

namespace OpenFusion.World
{
    public static class TerrainBuilder
    {
        /// Pruebas visuales: si el pasto o las manchas de textura salen en el sitio equivocado, probar a transponer (los datos 2.5 van por columnas).
        public static bool DetailTranspose = false, AlphaTranspose = false;
        /// resolveTexture: PPtr (relativo al archivo del TerrainData) -> Texture2D ya convertida (TextureDecoder.ToRGBA32 -> SetPixelData).
        public static GameObject Build(Terrain25 t, Func<PPtr25, Texture2D> resolveTexture, Material terrainMaterial = null, Func<PPtr25, Texture25> resolveAlphaData = null)
        {
            var td = new TerrainData { heightmapResolution = t.Width };
            td.size = new Vector3((t.Width - 1) * t.ScaleX, t.ScaleY, (t.Height - 1) * t.ScaleZ);
            var h = new float[t.Height, t.Width];
            for (int z = 0; z < t.Height; z++) for (int x = 0; x < t.Width; x++) h[z, x] = t.Heights[x * t.Height + z] / Terrain25.HeightDiv;   // el mapa de alturas 2.5 está almacenado por columnas (índice x*alto+z): verificado contra props de Map_09_07
            td.SetHeights(0, 0, h);

            var layers = new List<TerrainLayer>(); var usable = new List<bool>();
            foreach (var s in t.Splats)
            {
                var tex = resolveTexture(s.Texture);
                // "SharedPlaceholderSplatMap" (4x4) y texturas no resueltas son capas vacías: no deben aportar peso
                usable.Add(tex != null && tex.width > 4);
                layers.Add(new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(s.TileX, s.TileY), diffuseRemapMin = Vector4.zero, diffuseRemapMax = Vector4.one, smoothness = 0f, metallic = 0f, specular = Color.black });   // sin brillo especular: el terreno 2.5 es sólo difuso (el reflejo que se movía con la cámara)
            }
            td.terrainLayers = layers.ToArray();

            // Unity 2.5 pintaba las capas EN ORDEN con su alfa encima de la anterior (la capa 0 es la base):
            //   color = L0; para i>=1: color = lerp(color, Li, alfa_i).   Los alfas NO están normalizados (suman >1).
            // El terreno moderno espera pesos que sumen 1, así que se convierten: w_i = a_i * prod(1 - a_j, j>i); w_0 = prod(1 - a_j, j>=1).
            if (t.Splats.Count > 0 && t.AlphaTextures.Count > 0)
            {
                int res = t.AlphamapResolution; td.alphamapResolution = res; int nl = layers.Count;
                var px = new byte[t.AlphaTextures.Count][]; var wd = new int[t.AlphaTextures.Count]; var ht = new int[t.AlphaTextures.Count];
                for (int ti = 0; ti < px.Length; ti++)
                {
                    // Mascaras son datos de CPU. No leer ETC/DXT recompresionado ni
                    // depender de mipmaps/formato soportado por GetPixels32 en el telefono.
                    if (resolveAlphaData != null)
                    {
                        var data = resolveAlphaData(t.AlphaTextures[ti]);
                        if (data == null) throw new InvalidOperationException("Mascara de terreno ausente: " + t.Name + " #" + ti);
                        px[ti] = data.ToRGBA32(); wd[ti] = data.Width; ht[ti] = data.Height;
                    }
                    else
                    {
                        var tex = resolveTexture(t.AlphaTextures[ti]);
                        if (tex == null) throw new InvalidOperationException("Mascara de terreno ausente: " + t.Name + " #" + ti);
                        var colors = tex.GetPixels32(); px[ti] = new byte[colors.Length * 4];
                        for (int p = 0; p < colors.Length; p++) { px[ti][p * 4] = colors[p].r; px[ti][p * 4 + 1] = colors[p].g; px[ti][p * 4 + 2] = colors[p].b; px[ti][p * 4 + 3] = colors[p].a; }
                        wd[ti] = tex.width; ht[ti] = tex.height;
                    }
                }
                var a = new float[res, res, nl]; var al = new float[nl];
                for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
                {
                    for (int l = 0; l < nl; l++)
                    {
                        int ti = l / 4, ch = l % 4; float v = 0f;
                        if (usable[l] && ti < px.Length && px[ti] != null && wd[ti] > 0)
                        {
                            int ax = AlphaTranspose ? y : x, ay = AlphaTranspose ? x : y; int sx = Math.Min(ax * wd[ti] / res, wd[ti] - 1), sy = Math.Min(ay * ht[ti] / res, ht[ti] - 1);
                            v = px[ti][(sy * wd[ti] + sx) * 4 + ch] / 255f;
                        }
                        al[l] = v;
                    }
                    float carry = 1f;
                    for (int l = nl - 1; l >= 1; l--) { a[y, x, l] = al[l] * carry; carry *= 1f - al[l]; }
                    a[y, x, 0] = carry;
                }
                td.SetAlphamaps(0, 0, a);
            }
            var grass = BuildDetails(t, td, resolveTexture);
            var go = Terrain.CreateTerrainGameObject(td); go.name = t.Name;
            var owned = go.AddComponent<OwnedWorldResources>();
            owned.Track(td); foreach (var layer in layers) owned.Track(layer);
            if (terrainMaterial != null) go.GetComponent<Terrain>().materialTemplate = terrainMaterial;
            else
            {
                // El terreno se crea en runtime: activar explicitamente la normal por pixel
                // para los parches instanciados. Sin ella el terreno sale negro en GLES.
                var tsh = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (tsh != null) { var tm = new Material(tsh) { name = "OF_Terrain", enableInstancing = true }; tm.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL"); go.GetComponent<Terrain>().materialTemplate = tm; owned.Track(tm); }
            }
            var tc = go.GetComponent<Terrain>(); tc.detailObjectDistance = GrassField.Custom && grass != null ? 0f : 80f; tc.detailObjectDensity = 1f; tc.heightmapPixelError = 1f; tc.basemapDistance = 1500f; tc.drawInstanced = true;   tc.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;   // sin reflejo del cielo/sol (disco blanco con el Sun direccional)   // gráficos al máximo como el cliente original configurado al máximo
            if (grass != null && GrassField.Custom) go.AddComponent<GrassField>().Init(go, td, grass);
            if (LightmapOverlay && t.Lightmap.PathId != 0) { try { var lm = resolveTexture(t.Lightmap); if (lm != null) AddLightmapOverlay(go, td, lm); else Debug.LogWarning("[lightmap] " + t.Name + ": textura no resuelta"); } catch (Exception ex) { Debug.LogWarning("[lightmap] " + ex.Message); } }
            return go;
        }

        public static bool LightmapOverlay = true, LightmapTranspose = false, LightmapFlipV = false;
        /// El cliente original multiplica el color del terreno por un lightmap horneado de 1024x1024 (sombras de árboles y edificios, AO). Aquí: malla del mismo relieve con blend multiplicativo.
        static void AddLightmapOverlay(GameObject terrainGo, TerrainData td, Texture2D lm)
        {
            int N = Mathf.Clamp(td.heightmapResolution - 1, 128, Application.isMobilePlatform ? 256 : 512); var verts = new Vector3[(N + 1) * (N + 1)]; var uvs = new Vector2[verts.Length]; var tris = new int[N * N * 6]; var sz = td.size;
            for (int j = 0; j <= N; j++) for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, v = j / (float)N; int k = j * (N + 1) + i;
                verts[k] = new Vector3(u * sz.x, td.GetInterpolatedHeight(u, v) + 0.05f, v * sz.z);
                float uu = LightmapTranspose ? v : u, vv = LightmapTranspose ? u : v; if (LightmapFlipV) vv = 1f - vv; uvs[k] = new Vector2(uu, vv);
            }
            int n = 0;
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++)
            {
                int a = j * (N + 1) + i, b = a + 1, c = a + N + 1, d = c + 1;
                // diagonal como la del terreno real: GetInterpolatedHeight en el centro de la celda revela cuál de las dos diagonales usa (a-d o b-c)
                float hc = td.GetInterpolatedHeight((i + 0.5f) / N, (j + 0.5f) / N) + 0.05f;
                float ad = (verts[a].y + verts[d].y) * 0.5f, bc = (verts[b].y + verts[c].y) * 0.5f;
                if (Mathf.Abs(hc - ad) <= Mathf.Abs(hc - bc)) { tris[n++] = a; tris[n++] = c; tris[n++] = d; tris[n++] = a; tris[n++] = d; tris[n++] = b; }   // diagonal a-d
                else { tris[n++] = a; tris[n++] = c; tris[n++] = b; tris[n++] = b; tris[n++] = c; tris[n++] = d; }                                              // diagonal b-c
            }
            var mesh = new Mesh { name = "lightmapOverlay", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32, vertices = verts, uv = uvs, triangles = tris }; mesh.RecalculateBounds();
            var sh = Shader.Find("OpenFusion/TerrainLightmap"); if (sh == null) { UnityEngine.Object.Destroy(mesh); Debug.LogWarning("[lightmap] shader OpenFusion/TerrainLightmap no encontrado"); return; }
            lm.wrapMode = TextureWrapMode.Clamp; lm.filterMode = FilterMode.Bilinear;
            var go = new GameObject("lightmapOverlay"); go.transform.SetParent(terrainGo.transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = new Material(sh) { mainTexture = lm }; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var owned = go.AddComponent<OwnedWorldResources>(); owned.Track(mesh); owned.Track(mr.sharedMaterial);
            mesh.UploadMeshData(true);
            Debug.Log("[lightmap] " + terrainGo.name + ": " + lm.width + "x" + lm.height + " aplicado");
        }

        /// Pasto: 24 prototipos (billboards) y recuentos por muestra de 1 m (parches de 8x8). Los datos van por columnas como el mapa de alturas.
        static GrassInfo BuildDetails(Terrain25 t, TerrainData td, Func<PPtr25, Texture2D> resolveTexture)
        {
            if (t.DetailProtos.Count == 0 || t.DetailCounts == null || t.DetailPatchCount <= 0) return null;
            int pc = t.DetailPatchCount, ps = t.DetailPatchSamples, res = pc * ps;
            td.SetDetailResolution(res, ps);
            td.wavingGrassStrength = 0.5f; td.wavingGrassAmount = 0.5f; td.wavingGrassSpeed = 0.5f; td.wavingGrassTint = new Color(1f, 0.9f, 0.8f, 1f);   // m_DetailDatabase del original
            var protos = new List<DetailPrototype>(); var keep = new List<int>();
            for (int i = 0; i < t.DetailProtos.Count; i++)
            {
                var p = t.DetailProtos[i]; var tex = resolveTexture(p.Texture); if (tex == null) { protos.Add(null); continue; }
                protos.Add(new DetailPrototype { prototypeTexture = tex, usePrototypeMesh = false, renderMode = DetailRenderMode.GrassBillboard,
                    minWidth = Math.Min(p.MinW, p.MaxW), maxWidth = Math.Max(p.MinW, p.MaxW), minHeight = Math.Min(p.MinH, p.MaxH), maxHeight = Math.Max(p.MinH, p.MaxH),
                    noiseSpread = p.Noise, healthyColor = new Color(p.Healthy[0], p.Healthy[1], p.Healthy[2], 1f), dryColor = new Color(p.Dry[0], p.Dry[1], p.Dry[2], 1f) });
            }
            var final = new List<DetailPrototype>(); var remap = new int[protos.Count];
            for (int i = 0; i < protos.Count; i++) { if (protos[i] == null) { remap[i] = -1; continue; } remap[i] = final.Count; final.Add(protos[i]); }
            if (final.Count == 0) return null; td.detailPrototypes = final.ToArray();
            var maps = new int[final.Count][,];
            for (int q = 0; q < t.DetailCounts.Length; q++)
            {
                int a = q / pc, b = q % pc; var li = t.DetailLayerIndices[q]; var cn = t.DetailCounts[q];
                for (int k = 0; k < li.Length; k++)
                {
                    int pi = li[k]; if (pi >= remap.Length || remap[pi] < 0) continue; int m = remap[pi];
                    for (int s = 0; s < ps * ps; s++)
                    {
                        int idx = k * ps * ps + s; if (idx >= cn.Length) break; int c = cn[idx]; if (c == 0) continue;
                        int gx = a * ps + s / ps, gz = b * ps + s % ps; if (DetailTranspose) { int tmp = gx; gx = gz; gz = tmp; }
                        if (maps[m] == null) maps[m] = new int[res, res]; maps[m][gz, gx] = c;
                    }
                }
            }
            long cells = 0, objs = 0; int layersUsed = 0;
            for (int m = 0; m < maps.Length; m++) if (maps[m] != null) { td.SetDetailLayer(0, 0, m, maps[m]); layersUsed++; foreach (var v in maps[m]) if (v > 0) { cells++; objs += v; } }
            for (int i = 0; i < protos.Count; i++)
            {
                int m = remap[i]; long cc = 0; if (m >= 0 && maps[m] != null) foreach (var v in maps[m]) if (v > 0) cc++;
                if (cc == 0) continue; var tx = protos[i] != null ? protos[i].prototypeTexture : null;
                string al = "?"; try { if (tx != null && tx.isReadable) { var px = tx.GetPixels32(); long sa = 0; int st = Mathf.Max(1, px.Length / 4096), cn2 = 0, opaque = 0; for (int z = 0; z < px.Length; z += st) { sa += px[z].a; cn2++; if (px[z].a > 128) opaque++; } al = (sa / (cn2 * 255f)).ToString("F2") + "/opaco>0.5=" + (opaque / (float)cn2).ToString("F2"); } else al = "no legible"; } catch (Exception) { }
                var pp = protos[i]; Debug.Log("[grass] " + t.Name + " #" + i + " " + (tx != null ? tx.name + " " + tx.width + "x" + tx.height + " " + tx.format : "NULL") + " celdas=" + cc + " ancho=" + (pp != null ? pp.minWidth.ToString("F2") + "-" + pp.maxWidth.ToString("F2") : "-") + " alto=" + (pp != null ? pp.minHeight.ToString("F2") + "-" + pp.maxHeight.ToString("F2") : "-") + " sano=" + (pp != null ? pp.healthyColor.ToString() : "-") + " seco=" + (pp != null ? pp.dryColor.ToString() : "-") + " ruido=" + (pp != null ? pp.noiseSpread.ToString("F2") : "-") + " alfa=" + al);
            }
            Debug.Log("[grass] " + t.Name + ": prototipos=" + final.Count + "/" + protos.Count + " capas con datos=" + layersUsed + " celdas=" + cells + " objetos=" + objs + " transpuesto=" + DetailTranspose);
            var gi = new GrassInfo { Res = res, Maps = maps };
            for (int i = 0; i < protos.Count; i++) { if (remap[i] < 0) continue; var pp2 = protos[i]; gi.Protos.Add(new GrassInfo.Proto { Tex = pp2.prototypeTexture, MinW = pp2.minWidth, MaxW = pp2.maxWidth, MinH = pp2.minHeight, MaxH = pp2.maxHeight, Noise = pp2.noiseSpread, Healthy = pp2.healthyColor, Dry = pp2.dryColor }); }
            return gi;
        }
    }
}
