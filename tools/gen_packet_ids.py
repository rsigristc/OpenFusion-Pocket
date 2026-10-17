#!/usr/bin/env python3
"""Genera Assets/Scripts/OpenFusion/Net/PacketIds.cs a partir de csDefines.cs (constantes P_* del cliente original).
uso: gen_packet_ids.py <csDefines.cs> <salida.cs>"""
import re, sys

src = open(sys.argv[1]).read()
items = re.findall(r'public const uint (P_\w+) = (\d+)u;', src)
total_lines = len(re.findall(r'public const uint P_', src))
if len(items) != total_lines:
    sys.exit(f"el patrón sólo reconoce {len(items)} de {total_lines} constantes")
out = ["// GENERADO por tools/gen_packet_ids.py desde csDefines.cs del cliente original. No editar a mano.",
       "using System.Collections.Generic;", "", "namespace OpenFusion.Net", "{", "    public static class PacketIds", "    {"]
for n, v in items:
    out.append(f"        public const uint {n} = {v}u;")
out += ["", "        static Dictionary<uint, string> names;",
        "        /// Nombre legible de un tipo de paquete (para registros); \"0x........\" si no existe.",
        "        public static string Name(uint type)", "        {", "            if (names == null)", "            {",
        "                var d = new Dictionary<uint, string>();"]
seen = set()
for n, v in items:
    if v in seen:
        continue
    seen.add(v)
    out.append(f"                d[{v}u] = \"{n}\";")
out += ["                names = d;", "            }",
        "            return names.TryGetValue(type, out var s) ? s : \"0x\" + type.ToString(\"X8\");", "        }",
        "        /// El checksum va estampado en los bits 12..23 del tipo; esto lo quita (igual que csSocketData.GetPacketType).",
        "        public static uint Unstamp(uint type) => type & 0xFF000FFFu;", "    }", "}", ""]
open(sys.argv[2], "w").write("\n".join(out))
print(len(items), "constantes,", len(seen), "valores distintos")
