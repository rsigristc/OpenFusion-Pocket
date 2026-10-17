// Extrae xdtdatas y all_hnpc de TableData.resourceFile (MonoBehaviours con datos empaquetados). Puro; sin Unity.
using System;
using System.Collections.Generic;
using OpenFusion.Tables.Gen;
using OpenFusion.Unity25;

namespace OpenFusion.Tables
{
    public sealed class GameTables { public XdtTableScript Xdt; public HNpcTableScript HNpc; }

    public static class TableLoader
    {
        /// files = contenido del bundle (IBundleSource.Load("TableData.resourceFile")). Lanza si falta xdtdatas o all_hnpc.
        public static GameTables Load(Dictionary<string, byte[]> files)
        {
            var t = new GameTables();
            foreach (var kv in files)
            {
                SerializedFile sf; try { sf = SerializedFile.Parse(kv.Value); } catch (Exception) { continue; }   // no todos los archivos del bundle son serializados
                foreach (var o in sf.Objects)
                {
                    if (o.ClassId != 114) continue;                           // MonoBehaviour
                    var raw = new byte[o.Size]; Buffer.BlockCopy(sf.Data, (int)o.Offset, raw, 0, (int)o.Size);
                    if (raw.Length < 37) continue;
                    var r = new PackedReader(raw); string name = r.Header();
                    if (name == "xdtdatas") t.Xdt = XdtTableScript.Read(r);
                    else if (name == "all_hnpc") t.HNpc = HNpcTableScript.Read(r);
                }
            }
            if (t.Xdt == null) throw new InvalidOperationException("TableData: no se encontró xdtdatas");
            if (t.HNpc == null) throw new InvalidOperationException("TableData: no se encontró all_hnpc");
            return t;
        }
    }
}
