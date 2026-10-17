using System; using System.IO; using System.Collections.Generic; using OpenFusion.Tables;
var f = args.Length > 0 ? args[0] : "/mnt/user-data/uploads/OpenFusionLauncher/ffcache/821a1a05-71a0-4348-8d30-7b8f5ab8c5b3/tabledata_2eresourceFile/CustomAssetBundle-1dca92eecee4742d985b799d8226666d";
if (!File.Exists(f)) { Console.WriteLine("sin datos locales: omitido"); return 0; }
var sw = System.Diagnostics.Stopwatch.StartNew();
var t = TableLoader.Load(new Dictionary<string, byte[]> { { "TableData", File.ReadAllBytes(f) } });
Console.WriteLine($"xdtdatas y all_hnpc leídos del bundle en {sw.ElapsedMilliseconds} ms; shirts={t.Xdt.m_pShirtsItemTable?.m_pItemData?.Length}, hnpc={t.HNpc?.TableElement?.Length}");
bool ok = t.Xdt.m_pShirtsItemTable?.m_pItemData?.Length > 100 && t.HNpc.TableElement.Length == 205;
bool threw = false; try { TableLoader.Load(new Dictionary<string, byte[]> { { "x", new byte[10] } }); } catch (InvalidOperationException) { threw = true; }
Console.WriteLine((ok ? "OK   " : "FALLA ") + "TableLoader sobre TableData real"); Console.WriteLine((threw ? "OK   " : "FALLA ") + "bundle sin tablas -> InvalidOperationException");
return ok && threw ? 0 : 1;
