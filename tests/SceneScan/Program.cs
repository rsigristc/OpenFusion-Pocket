using System; using System.IO; using System.Collections; using System.Collections.Generic; using System.Linq; using OpenFusion.Unity25;
var f=SerializedFile.Parse(File.ReadAllBytes(args[0]));
foreach(var o in f.Objects){ if(f.TypeOf(o)?.Type!="MonoBehaviour")continue; var v=f.ReadObject(o,out int c); if(!v.ContainsKey("EvSoundArray"))continue; var a=((IEnumerable)v["EvSoundArray"]).Cast<Dictionary<string,object>>().ToList();
  Console.WriteLine($"{v["m_Name"]} n={a.Count} size={o.Size} used={c}"); foreach(var e in a.Take(3)){ var p=(Dictionary<string,object>)e["Position"]; Console.WriteLine($"   '{e["FileName"]}' ({p["x"]},{p["y"]},{p["z"]})"); } }
