using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Unity25; using OpenFusion.World;
int fails=0; void Check(bool ok,string m){ Console.WriteLine((ok?"OK  ":"FAIL ")+m); if(!ok)fails++; }
var root=args[0]; var man=CachingManifest25.FromMainUnity3d(File.ReadAllBytes(Path.Combine(root,"main.unity3d")));
int tut=0,free=0,paid=0,overlap=0; for(int x=0;x<16;x++)for(int y=0;y<16;y++){ bool t=man.IsTutorial(x,y),f=man.IsFree(x,y),p=man.IsPaid(x,y); if(t)tut++; if(f)free++; if(p)paid++; if((t?1:0)+(f?1:0)+(p?1:0)>1)overlap++; }
Check(tut==9&&free==29&&paid==135&&overlap==0,$"dongs tutorial={tut} libre={free} pago={paid} (173 en total) solapes={overlap}");
var names=man.All.Select(e=>e.FileName).ToList(); Check(names.Count==360&&names.Distinct().Count()==360,$"manifiesto: {names.Count} archivos unicos");
// cada Map_XX_YY.unity3d y DongResources_XX_YY existe para su dong
bool allMaps=true; for(int x=0;x<16;x++)for(int y=0;y<16;y++){ bool any=man.IsTutorial(x,y)||man.IsFree(x,y)||man.IsPaid(x,y); if(any&&!names.Contains(DongGrid.MapName(x,y)+".unity3d")) { allMaps=false; Console.WriteLine("  falta "+DongGrid.MapName(x,y)); } }
Check(allMaps,"cada dong tiene su Map_XX_YY.unity3d");
// el cache local del usuario trae archivos con estos nombres
var cache=Directory.GetDirectories(Path.Combine(root,"ffcache")).First(); var local=Directory.GetDirectories(cache).Select(Path.GetFileName).Select(s=>s.Replace("_5f","_").Replace("_2e",".")).ToList();
int match=local.Count(l=>names.Any(n=>string.Equals(n,l,StringComparison.OrdinalIgnoreCase))); Console.WriteLine($"  carpetas del cache local {local.Count}, coinciden con el manifiesto {match}");
Check(match==local.Count,"todas las carpetas del cache local estan en el manifiesto");
Check(man.Version("Map_01_00.unity3d")>0,"version Map_01_00 = "+man.Version("Map_01_00.unity3d"));
Console.WriteLine("  ForZone(Tutorial)="+man.ForZone(ZoneType.Tutorial).Count()+" Free="+man.ForZone(ZoneType.Free).Count()+" Paid="+man.ForZone(ZoneType.Paid).Count());
// rejilla
Check(DongGrid.MapName(1,0)=="Map_01_00"&&DongGrid.MapName(13,12)=="Map_13_12","nombres de mapa");
DongGrid.Cell(6912f,1792f,out int cx,out int cy); Check(cx==13&&cy==3,$"posicion inicial (6912,1792) -> dong {cx},{cy}");
// streaming: jugador en el centro del dong (1,1) con todo cargable y nada cargado -> carga el propio; con 3x3 cargados, descarga a >340 m
var loaded=new HashSet<(int,int)>(); Func<int,int,bool> isL=(a,b)=>loaded.Contains((a,b)); Func<int,int,bool> can=(a,b)=>man.IsTutorial(a,b);
var s=DongGrid.Step(768,768,isL,can); Check(s.load&&s.x==1&&s.y==1,$"carga primero el dong propio ({s.x},{s.y})"); loaded.Add((1,1));
int steps=0; while(true){ s=DongGrid.Step(768,768,isL,can); if(!s.load)break; loaded.Add((s.x,s.y)); steps++; if(steps>20)break; }
Check(loaded.Count>=4,$"carga {loaded.Count} dongs vecinos dentro de 280 m");
var far=DongGrid.Step(768+700,768,isL,can); Check(far.unload,"descarga cuando el jugador se aleja >340 m");
// color: un solo dong con color -> fuera de el se normaliza al propio color
var cols=new Dictionary<(int,int),DongColors>(); cols[(1,0)]=new DongColors{Set=true,Fog=new float[]{0,0,0,1},Sky=new float[]{1,1,1,1},Light=new float[]{1,1,1,1},FogDepth=1};
cols[(1,1)]=new DongColors{Set=true,Fog=new float[]{1,0,0,1},Sky=new float[]{0,0,1,1},Light=new float[]{1,1,0,1},FogDepth=3};
DongGrid.PositionColor(768,256,(a,b)=>cols.TryGetValue((a,b),out var c)?c:default,out var fog,out var sky,out var li,out var fd,out var rim);
Console.WriteLine($"  mezcla en (768,256): fog={fog[0]:F2},{fog[1]:F2} fogDepth={fd:F2} rim={rim[0]:F2}"); Check(Math.Abs(fd-1f)<1e-4,"centro del dong (1,0): fogDepth=1 (solo su color)");
DongGrid.PositionColor(768,512,(a,b)=>cols.TryGetValue((a,b),out var c)?c:default,out fog,out sky,out li,out fd,out rim); Check(fd>1.5f&&fd<2.5f,$"frontera (1,0)/(1,1): fogDepth mezclado = {fd:F2}");
DongGrid.PositionColor(6000,6000,(a,b)=>default,out fog,out sky,out li,out fd,out rim); Check(fd==0&&sky[0]==1&&fog[0]==0,"sin datos: cielo blanco, niebla negra, profundidad 0");
// atributos del terreno
var att=new byte[129*129]; att[5+7*129]=42; byte a1=DongGrid.Attribute(att,129,129,512,512,512,0,512+5*4+1,7*4+1,out bool oob); Check(a1==42&&!oob,"indexacion de atributos (x + z*129, celdas de 4 m)");
// sonido ambiental
var tutF=SerializedFile.Parse(File.ReadAllBytes(Directory.GetFiles(Path.Combine(cache,"tutorial_2eresourceFile")).First())); int nonEmpty=0,totalS=0;
foreach(var o in tutF.Objects.Where(o=>tutF.TypeOf(o)?.Type=="MonoBehaviour")){ var v=tutF.ReadObject(o,out _); if(!v.ContainsKey("EvSoundArray"))continue; var l=EvSound25.From(v); totalS+=l.Count; if(l.Count>0)nonEmpty++; }
Check(totalS==11&&nonEmpty==1,$"sonidos ambientales: {totalS} en {nonEmpty} dong (sound_01_01)");
Console.WriteLine(fails==0?"TODO OK":"FALLOS: "+fails);
// --- AssetStore: resolucion de la escena contra los bundles reales
var store=new AssetStore(); string sceneName="BuildPlayer-Map_01_00";
store.Add(sceneName,File.ReadAllBytes(Directory.GetFiles(Path.Combine(cache,"map_5f01_5f00_2eunity3d"),sceneName).First()));
foreach(var d in Directory.GetDirectories(cache).Where(d=>!d.Contains("map_5f01"))) foreach(var fpath in Directory.GetFiles(d)) { try{ store.Add(Path.GetFileName(fpath),File.ReadAllBytes(fpath)); }catch(Exception e){ Console.WriteLine("  no cargo "+Path.GetFileName(fpath)+": "+e.Message);} }
var sg=SceneGraph25.Build(store.Get(sceneName)); int meshOk=0,meshBad=0,matOk=0,matBad=0,texOk=0,texBad=0,noShader=0; var kinds=new Dictionary<string,int>(); int unassigned=0, emptyMesh=0;
foreach(var n in sg.ByTransform.Values){
  if(n.Mesh.PathId!=0){ var m=store.Mesh(store.Resolve(sceneName,n.Mesh)); if(m!=null&&m.Positions.Length==0){ emptyMesh++; meshOk++; } else if(m!=null)meshOk++; else { meshBad++; var rr=store.Resolve(sceneName,n.Mesh); Console.WriteLine($"  malla mala fid={n.Mesh.FileId} pid={n.Mesh.PathId} ok={rr.Ok} tipo={store.TypeOf(rr)} v={(m==null?-1:m.Positions.Length)}"); } }
  foreach(var mp in n.Materials){ var r=store.Resolve(sceneName,mp); var spec=store.Material(r,out var owner); if(spec==null){matBad++;continue;} matOk++; if(spec.Shader==null){noShader++;continue;} kinds[spec.Shader.Kind.ToString()]=kinds.GetValueOrDefault(spec.Shader.Kind.ToString())+1;
    foreach(var ts in spec.Textures.Values){ if(ts.PathId==0){ unassigned++; continue; } var t=store.Texture(store.Resolve(owner,ts.FileId,ts.PathId)); if(t!=null&&t.ToRGBA32().Length==t.Width*t.Height*4)texOk++; else { texBad++; var rt=store.Resolve(owner,ts.FileId,ts.PathId); Console.WriteLine($"  textura mala {spec.Name} fid={ts.FileId} pid={ts.PathId} ok={rt.Ok} tipo={store.TypeOf(rt)} ext={AssetStore.ExternalName(rt.Ok?rt.File:store.Get(owner),ts.FileId)}"); } } } }
Console.WriteLine($"  mallas ok={meshOk} mal={meshBad}; materiales ok={matOk} mal={matBad} sinShader={noShader} (mallas vacias={emptyMesh}); texturas ok={texOk} mal={texBad} sin asignar={unassigned}; tipos={string.Join(",",kinds.Select(k=>k.Key+"x"+k.Value))}");
Check(meshBad==0&&matBad==0&&texBad==0&&noShader==0,"toda la escena Map_01_00 se resuelve: mallas, materiales (con shader del catalogo) y texturas decodificadas");
var tdr=store.Resolve(sceneName,1,1899); Check(store.TypeOf(tdr)=="TerrainData","PPtr externo (1,1899) -> TerrainData");
// --- Fuentes de bundles + registro de dongs, simulando la carga real del dong (1,0) desde la cache
var src=new FfCacheBundleSource(cache); var reg=new DongRegistry(man); reg.SetZoneFor(768,256); Check(reg.Zone==ZoneType.Tutorial,"zona en (768,256) = tutorial");
var shared=reg.SharedFiles(ZoneType.Tutorial).ToList(); int have=0; foreach(var n in shared) if(src.Load(n)!=null) have++;
Console.WriteLine($"  compartidos tutorial: {shared.Count} en manifiesto, {have} en la cache local");
var mapFiles=src.Load("Map_01_00.unity3d"); Check(mapFiles!=null&&mapFiles.ContainsKey("BuildPlayer-Map_01_00"),"FfCacheBundleSource.Load(Map_01_00.unity3d) -> "+string.Join(",",mapFiles==null?new string[0]:mapFiles.Keys.ToArray()));
Check(string.Join(",",reg.FilesFor(1,0))=="Map_01_00.unity3d"&&string.Join(",",reg.FilesFor(10,0))=="Map_10_00.unity3d,DongResources_10_00.resourceFile","FilesFor: tutorial solo Map; libre Map+DongResources");
var st=reg.Step(768,256); Check(st.load&&st.x==1&&st.y==0,"Step desde (768,256) elige el dong (1,0)");
reg.At(1,0).Loaded=true; reg.At(1,0).Attributes=new byte[129*129]; reg.At(1,0).OriginX=512; reg.At(1,0).Attributes[3+2*129]=9; Check(reg.AttributeAt(512+3*4+1,2*4+1,out bool kn)==9&&kn,"AttributeAt usa el dong cargado");
Console.WriteLine(fails==0?"TODO OK (2)":"FALLOS: "+fails);
