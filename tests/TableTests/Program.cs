using System; using System.Diagnostics; using System.IO; using OpenFusion.Tables; using OpenFusion.Tables.Gen;
string dir = args.Length > 0 ? args[0] : "blobs"; int bad = 0;
void Check<T>(string name, Func<PackedReader, T> read, Func<T, string> summary) {
  var raw = File.ReadAllBytes(Path.Combine(dir, name + ".bin")); var r = new PackedReader(raw);
  var sw = Stopwatch.StartNew(); var nm = r.Header(); var o = read(r); sw.Stop();
  bool ok = r.Remaining == 0; if (!ok) bad++;
  Console.WriteLine($"{(ok?"OK ":"FALLO")} {name,-10} script={PackedReader.ScriptId(raw)} nombre={nm} bytes={raw.Length} restante={r.Remaining} {sw.ElapsedMilliseconds} ms | {summary(o)}");
}
Check("xdtdatas", XdtTableScript.Read, o => $"Mission={o.m_pMissionTable?.m_pMissionStringData?.Length}");
Check("all_hnpc", HNpcTableScript.Read, o => "");
Check("worldname", WorldNameScript.Read, o => $"zonas={o.m_pWorldNameData.Length}");
Check("clientnpc", MiniMapNpc.Read, o => $"npcs={o.m_pElements.Length}");
return bad;
