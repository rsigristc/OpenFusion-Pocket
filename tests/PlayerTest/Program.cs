using System; using System.IO; using System.Linq; using System.Collections.Generic; using OpenFusion.Tables; using OpenFusion.Tables.Gen; using OpenFusion.World;
// uso: PlayerTest <xdtdatas.bin>
int fail = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK   " : "FALLA ") + m); if (!ok) fail++; }
var r = new PackedReader(File.ReadAllBytes(args[0])); r.Header(); var x = XdtTableScript.Read(r);
var b = new PlayerLookBuilder(x);
// paletas
Check(ActorPalettes.SkinHex.Length == 48 && ActorPalettes.HairHex.Length == 54 && ActorPalettes.EyeHex.Length == 10, "paletas 48/54/10");
Check(ActorPalettes.Skin(-1) == null && ActorPalettes.Skin(999).Value.R == 1f && Math.Abs(ActorPalettes.Skin(0).Value.R - 0x3A / 255f) < 1e-6, "UserContainer.GetSkinColor: fuera de rango = blanco, índice 0 = 3A2719");
// jugador desnudo, sin equipo
var naked = b.Build(new PlayerStyle { Gender = 1 }, new PlayerEquip());
Check(naked.Cloth[0] == "wear/m_shoes_naked.nif" && naked.Cloth[1] == "wear/m_pants_naked.nif" && naked.Cloth[2] == "wear/m_shirt_naked.nif" && naked.Cloth[3] == null && naked.Cloth[4] == null, "hombre desnudo: 3 piezas naked, sin cara/pelo");
Check(naked.ClothTexture[1] == "texture/m_naked.dds" && naked.ClothTexture2[1] == "texture/m_skin.dds" && naked.Attach[5] == "wear/pistol_trolley.nif", "texturas naked + secundaria = piel + tirolina");
var nf = b.Build(new PlayerStyle { Gender = 2 }, null); Check(nf.Female && nf.Body == "actor/w.kfm" && nf.ClothTexture[0] == "texture/boysrollertee_a.dds" && nf.ClothTexture2[0] == "texture/f_skin.dds", "mujer: actor/w.kfm y rareza de zapatos");
Check(b.Build(new PlayerStyle { Gender = 0 }, null) == null, "género inválido -> null");
// altura/forma
var hs = b.Build(new PlayerStyle { Gender = 1, Height = 2, Body = 1 }, null); Check(Math.Abs(hs.Height - 0.5f) < 1e-6 && Math.Abs(hs.Shape - 0.5f) < 1e-6, "altura = 1 - h/4, forma = b/2");
// objetos reales de las tablas: primero con cada tabla (cara, pelo, hat, glasses, back, weapon, shirt, pants, shoes)
var warns = new List<string>(); int built = 0, withFace = 0, withHair = 0, hatCases = new[] { 0, 1, 2, 3, 4, 5 }.Length;
int Pick(EquipmentItemTableScript t, Func<ItemElement, bool> pred = null) => t?.m_pItemData?.FirstOrDefault(e => e.m_iItemNumber != 0 && (pred == null || pred(e)))?.m_iItemNumber ?? 0;
int face = Pick(x.m_pFaceItemTable), hair = Pick(x.m_pHeadItemTable), shirt = Pick(x.m_pShirtsItemTable), pants = Pick(x.m_pPantsItemTable), shoes = Pick(x.m_pShoesItemTable), glass = Pick(x.m_pGlassItemTable), weapon = Pick(x.m_pWeaponItemTable);
Console.WriteLine($"ids elegidos: cara={face} pelo={hair} camisa={shirt} pantalón={pants} zapatos={shoes} gafas={glass} arma={weapon}");
foreach (var female in new[] { false, true })
{
  var full = b.Build(new PlayerStyle { Gender = female ? 2 : 1, FaceStyle = face, HairStyle = hair, HairColor = 5, SkinColor = 3, EyeColor = 4 }, new PlayerEquip { Shirt = shirt, Pants = pants, Shoes = shoes, Glass = glass, Weapon = weapon }, warns);
  Check(full.Cloth[3] != null && full.Cloth[3].EndsWith("_type01.nif") && full.ClothTexture[3].EndsWith("_d.dds"), $"{(female ? "mujer" : "hombre")}: cara {full.Cloth[3]} / ojos {full.ClothTexture[3]}");
  Check(full.Cloth[4] != null && full.Cloth[4].EndsWith("_type01.nif") && full.ClothTexture[4].EndsWith("_a.dds"), $"{(female ? "mujer" : "hombre")}: pelo {full.Cloth[4]} / {full.ClothTexture[4]}");
  Check(full.SkinColorIndex == 2 && full.HairColorIndex == 4, "índices de color = iSkinColor-1, iHairColor-1");
  Check(full.Cloth[0] != null && !full.Cloth[0].Contains("naked") && full.Cloth[1] != null && !full.Cloth[1].Contains("naked") && full.Cloth[2] != null && !full.Cloth[2].Contains("naked"), "ropa equipada reemplaza naked");
  Console.WriteLine("   rutas: " + string.Join(" | ", full.RequiredPaths().Take(12)));
}
// sombreros: variante de pelo/cara según m_iEquipType real
var hatsByType = x.m_pHatItemTable.m_pItemData.Where(e => e.m_iItemNumber != 0).GroupBy(e => e.m_iEquipType).ToDictionary(g => g.Key, g => g.First().m_iItemNumber);
Console.WriteLine("tipos de sombrero en tablas: " + string.Join(", ", hatsByType.OrderBy(k => k.Key).Select(k => $"{k.Key}:{x.m_pHatItemTable.m_pItemData.Count(e => e.m_iEquipType == k.Key)}")));
foreach (var kv in hatsByType.OrderBy(k => k.Key))
{
  var l = b.Build(new PlayerStyle { Gender = 1, FaceStyle = face, HairStyle = hair, HairColor = 1, SkinColor = 1 }, new PlayerEquip { Hat = kv.Value, Glass = glass }, warns);
  string h = l.Cloth[4] == null ? "(sin pelo)" : l.Cloth[4].Substring(l.Cloth[4].Length - 11);
  bool hairOk = kv.Key == 2 || kv.Key == 3 ? l.Cloth[4] == null : (kv.Key == 1 || kv.Key == 5 ? l.Cloth[4]?.EndsWith("_type02.nif") == true : l.Cloth[4]?.EndsWith("_type01.nif") == true);
  bool faceOk = (kv.Key == 2 || kv.Key == 3) ? l.Cloth[3].EndsWith("_type02.nif") : l.Cloth[3].EndsWith("_type01.nif");
  bool glassOk = (kv.Key >= 3 && kv.Key <= 5) ? l.Attach[1] == null : (glass == 0 || l.Attach[1] != null);
  Check(l.Attach[0] != null && hairOk && faceOk && glassOk, $"sombrero tipo {kv.Key} (id {kv.Value}): pelo {h}, cara {l.Cloth[3]?.Substring(l.Cloth[3].Length - 11)}, gafas {(l.Attach[1] != null)}");
}
// mochilas: equipType 1 -> ropa (slot 7), resto -> acoplada (4)
foreach (var g in x.m_pBackItemTable.m_pItemData.Where(e => e.m_iItemNumber != 0).GroupBy(e => e.m_iEquipType)) { var l = b.Build(new PlayerStyle { Gender = 1 }, new PlayerEquip { Back = g.First().m_iItemNumber }, warns); Check(g.Key == 1 ? (l.Cloth[7] != null && l.Attach[4] == null) : (l.Cloth[7] == null && l.Attach[4] != null), $"mochila tipo {g.Key}: ropa={l.Cloth[7] != null} acoplada={l.Attach[4] != null}"); }
// barrido: TODOS los objetos de cada tabla producen rutas sin null/vacío y con extensión correcta
int items = 0, bad = 0;
void Sweep(EquipmentItemTableScript t, Func<int, PlayerEquip> mk) { foreach (var e in t.m_pItemData) { items++; var l = b.Build(new PlayerStyle { Gender = 1 }, mk(e.m_iItemNumber)); var l2 = b.Build(new PlayerStyle { Gender = 2 }, mk(e.m_iItemNumber)); foreach (var p in l.RequiredPaths().Concat(l2.RequiredPaths())) if (p.Length < 8 || (!p.EndsWith(".nif") && !p.EndsWith(".dds") && !p.EndsWith(".kfm"))) { bad++; if (bad < 6) Console.WriteLine("   ruta rara: " + p + " (item " + e.m_iItemNumber + ")"); } } }
Sweep(x.m_pShirtsItemTable, i => new PlayerEquip { Shirt = i }); Sweep(x.m_pPantsItemTable, i => new PlayerEquip { Pants = i }); Sweep(x.m_pShoesItemTable, i => new PlayerEquip { Shoes = i });
Sweep(x.m_pHatItemTable, i => new PlayerEquip { Hat = i }); Sweep(x.m_pGlassItemTable, i => new PlayerEquip { Glass = i }); Sweep(x.m_pBackItemTable, i => new PlayerEquip { Back = i }); Sweep(x.m_pWeaponItemTable, i => new PlayerEquip { Weapon = i });
Check(bad == 0, $"barrido de {items} objetos reales (x2 géneros): rutas con extensión válida, {bad} raras");
foreach (var f in new[] { x.m_pFaceItemTable, x.m_pHeadItemTable }) foreach (var e in f.m_pItemData) { items++; }
int fc = 0, hc = 0, wf = 0; foreach (var e in x.m_pFaceItemTable.m_pItemData.Where(e => e.m_iItemNumber != 0)) { var l = b.Build(new PlayerStyle { Gender = 1, FaceStyle = e.m_iItemNumber }, null, warns); if (l.Cloth[3] != null) fc++; }
foreach (var e in x.m_pHeadItemTable.m_pItemData.Where(e => e.m_iItemNumber != 0)) { var l = b.Build(new PlayerStyle { Gender = 2, HairStyle = e.m_iItemNumber }, null, warns); if (l.Cloth[4] != null) hc++; }
Check(fc == x.m_pFaceItemTable.m_pItemData.Count(e => e.m_iItemNumber != 0) && hc == x.m_pHeadItemTable.m_pItemData.Count(e => e.m_iItemNumber != 0), $"todas las caras ({fc}) y peinados ({hc}) producen pieza");
Console.WriteLine($"avisos: {warns.Count} " + string.Join("; ", warns.Distinct().Take(5)));
return fail;
