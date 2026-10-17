using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25;
var root=args[0]; // carpeta ffcache/<guid>
var scene=SerializedFile.Parse(File.ReadAllBytes(Directory.GetFiles(Path.Combine(root,"map_5f01_5f00_2eunity3d"),"BuildPlayer-Map_01_00").First()));
var shared=SerializedFile.Parse(File.ReadAllBytes(Directory.GetFiles(Path.Combine(root,"tutorial_2eresourceFile")).First()));
var g=SceneGraph25.Build(scene);
Console.WriteLine($"escena: {g.ByTransform.Count} nodos, {g.Roots.Count} raices: {string.Join(",",g.Roots.Select(r=>r.Name+"@"+r.Pos[0]))}");
int fail=0; foreach(var n in g.ByTransform.Values.Where(n=>n.Mesh.PathId!=0)){ var m=n.WorldMatrix(); Console.WriteLine($" mesh fid={n.Mesh.FileId} pid={n.Mesh.PathId} mats={string.Join("/",n.Materials.Select(x=>x.PathId))} world=({m[3]:F1},{m[7]:F1},{m[11]:F1}) layer={n.Layer}"); }
// terrenos
var tds=shared.Objects.Where(o=>shared.TypeOf(o)?.Type=="TerrainData").OrderBy(o=>o.PathId).ToList(); int ok=0;
foreach(var o in tds){ var t=Terrain25.From(shared.ReadObject(o,out int c)); if(c!=o.Size) fail++;
  int mn=t.Heights.Min(h=>(int)h), mx=t.Heights.Max(h=>(int)h);
  Console.WriteLine($"{t.Name} {t.Width}x{t.Height} scale=({t.ScaleX},{t.ScaleY},{t.ScaleZ}) h={mn}..{mx} -> {mn/Terrain25.HeightDiv*t.ScaleY:F1}..{mx/Terrain25.HeightDiv*t.ScaleY:F1} m, splats={t.Splats.Count} alphaTex={t.AlphaTextures.Count} lm={t.Lightmap.PathId} detail={t.DetailPrototypes} shifts={t.Shifts}"); ok++; }
Console.WriteLine($"terrenos {ok}, desajustes de tamano {fail}");
// terreno 01_00: PGM + capas
var td=tds.Select(o=>Terrain25.From(shared.ReadObject(o,out _))).First(t=>t.Name=="TerrainData_01_00");
var by=shared.Objects.ToDictionary(o=>o.PathId);
foreach(var s in td.Splats) { var o=by[s.Texture.PathId]; var tx=Texture25.From(shared.ReadObject(o,out _)); Console.WriteLine($" splat tex {tx.Name} {tx.Width}x{tx.Height} fmt{tx.Format} tile=({s.TileX},{s.TileY})"); }
foreach(var a in td.AlphaTextures){ var tx=Texture25.From(shared.ReadObject(by[a.PathId],out _)); Console.WriteLine($" alpha {tx.Name} {tx.Width}x{tx.Height} fmt{tx.Format}"); }
var lm=Texture25.From(shared.ReadObject(by[td.Lightmap.PathId],out _)); Console.WriteLine($" lightmap {lm.Name} {lm.Width}x{lm.Height} fmt{lm.Format}");
using(var w=new StreamWriter(args[1])){ w.WriteLine($"P2\n{td.Width} {td.Height}\n255"); for(int z=0;z<td.Height;z++){ w.WriteLine(string.Join(" ",Enumerable.Range(0,td.Width).Select(x=>td.Heights[z*td.Width+x]*255/32766))); } }
// resolucion de referencias externas y decodificacion de texturas de terreno
int miss=0, refs=0; foreach(var n in g.ByTransform.Values){ foreach(var p in new[]{n.Mesh,n.ColliderMesh}.Concat(n.Materials)) if(p.FileId==1){ refs++; if(!by.ContainsKey(p.PathId)) miss++; } }
Console.WriteLine($"referencias externas de escena: {refs}, sin resolver: {miss}");
int texOk=0,texBad=0; foreach(var o in tds){ var t=Terrain25.From(shared.ReadObject(o,out _)); var ids=t.Splats.Select(s=>s.Texture.PathId).Concat(t.AlphaTextures.Select(a=>a.PathId)).Append(t.Lightmap.PathId);
  foreach(var id in ids){ if(!by.TryGetValue(id,out var oi)||shared.TypeOf(oi)?.Type!="Texture2D"){ texBad++; Console.WriteLine("  no resuelve "+t.Name+" "+id); continue; } var tx=Texture25.From(shared.ReadObject(oi,out _)); try{ var px=tx.ToRGBA32(); if(px.Length==tx.Width*tx.Height*4) texOk++; else texBad++; }catch(Exception e){ texBad++; Console.WriteLine("  fallo "+tx.Name+" "+e.Message);} } }
Console.WriteLine($"texturas de terreno decodificadas: {texOk}, fallos: {texBad}");
