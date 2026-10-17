using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    // Bounded combat-only diagnostics: no account details or raw login packets.
    public static class CombatAudit
    {
        static readonly Queue<string> lines = new Queue<string>();
        static readonly Dictionary<int, string> phases = new Dictionary<int, string>();
        static Vector3 sentPosition; static bool havePosition; static float nextSave;
        public static void Phase(int npc, string phase)
        {
            if (phases.Count >= 256) phases.Clear();
            phases[npc] = phase + "@" + Time.unscaledTime.ToString("F1");
        }
        public static void Sent(byte[] packet)
        {
            if (packet == null || packet.Length < 24) return;
            uint type = BitConverter.ToUInt32(packet, 0);
            if (type != PacketIds.P_CL2FE_REQ_PC_MOVE && type != PacketIds.P_CL2FE_REQ_PC_STOP && type != PacketIds.P_CL2FE_REQ_PC_JUMP) return;
            sentPosition = PlayerAvatarFactory.ToClient(BitConverter.ToInt32(packet, 12), BitConverter.ToInt32(packet, 16), BitConverter.ToInt32(packet, 20));
            havePosition = true;
        }
        public static void Result(NpcInfo npc, AttackResult hit, string source, Vector3 self)
        {
            string phase = phases.TryGetValue(npc.Id, out var value) ? value : "none";
            string progress = hit.Hp <= 0 ? "DEAD" : hit.Hp < npc.Hp ? "HP_DOWN" : hit.Hp > npc.Hp ? "HP_UP" : hit.Protected != 0 ? "BLOCKED" : "HP_UNCHANGED";
            var line = "[combat-audit] " + DateTime.UtcNow.ToString("O") + " source=" + source + " npc=" + npc.Id + " name=" + npc.Name +
                " ct=" + hit.CharacterType + " hp=" + npc.Hp + "->" + hit.Hp + " damage=" + hit.Damage + " protected=" + hit.Protected + " flags=" + hit.HitFlag + " " + progress +
                " phase=" + phase + " self=" + self.ToString("F2") + " sent=" + (havePosition ? sentPosition.ToString("F2") : "unknown") +
                " mob=" + npc.transform.position.ToString("F2") + " distance=" + Vector3.Distance(self, npc.transform.position).ToString("F2");
            Debug.Log(line);
            lines.Enqueue(line); while (lines.Count > 128) lines.Dequeue();
            if (Time.unscaledTime < nextSave && hit.Hp > 0) return;
            nextSave = Time.unscaledTime + 5f;
            try { File.WriteAllLines(Path.Combine(Application.persistentDataPath, "combat-audit.log"), lines); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
