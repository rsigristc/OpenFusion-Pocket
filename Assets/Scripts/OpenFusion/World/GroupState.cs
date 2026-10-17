// Estado del grupo (cnGroupManager): miembros jugadores del grupo propio, a partir de PC_GROUP_JOIN / JOIN_SUCC / LEAVE.
//  Cuerpo: {iID (nuevo miembro / quien sale), iMemberPCCnt, iMemberNPCCnt} (12 B) + iMemberPCCnt x sPCGroupMemberInfo (112 B, iPC_ID @0) + NPCs (32 B).
//  Se usa para: color de nombre de miembros del grupo (PrintName colorindex 2) y la pestaña GROUP del chat (GroupSize).
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed class GroupState : MonoBehaviour
    {
        public static int Size;                                   // m_iGroupSize (incluye al propio jugador)
        static readonly HashSet<int> members = new HashSet<int>();
        public static bool IsMember(int pcId) { lock (members) return members.Contains(pcId); }
        public static int[] Members() { lock (members) { var a = new int[members.Count]; members.CopyTo(a); return a; } }

        PacketDispatcher disp; WorldEntities ents; readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        public static GroupState Ensure(GameObject host) { var g = host.GetComponent<GroupState>(); if (g == null) g = host.AddComponent<GroupState>(); return g; }
        public void Init(WorldEntities e, PacketDispatcher d) { if (disp != null) disp.Unhandled -= OnPacket; ents = e; disp = d; if (disp != null) disp.Unhandled += OnPacket; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; Size = 0; lock (members) members.Clear(); }
        void Update() { while (q.TryDequeue(out var a)) a(); }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void OnPacket(uint type, byte[] b)
        {
            if (b != null && type == PacketIds.P_FE2CL_PC_GROUP_INVITE && b.Length >= 4)
            {   // cnGroupManager.func_FE2CL_GROUP_INVITE: con "ALLOW GROUP REQUESTS" = No se rechaza sin preguntar
                if (!GameOptions.Cur.AllowGroup && NpcUI.Sender != null) { var r = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_GROUP_INVITE_REFUSE, 4); ClientPackets.PutInt(r, 4, I(b, 0)); NpcUI.Sender(r); }
                return;
            }
            if (b != null && type == PacketIds.P_FE2CL_REP_PC_TRADE_OFFER && b.Length >= 12)
            {   // GameFrame (REP_PC_TRADE_OFFER): "ALLOW TRADE REQUESTS" = No -> REQ_PC_TRADE_OFFER_REFUSAL { iID_Request = yo, iID_From, iID_To }
                if (!GameOptions.Cur.AllowTrade && NpcUI.Sender != null && ents != null) { var r = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TRADE_OFFER_REFUSAL, 12); ClientPackets.PutInt(r, 4, ents.SelfId); ClientPackets.PutInt(r, 8, I(b, 4)); ClientPackets.PutInt(r, 12, I(b, 8)); NpcUI.Sender(r); }
                return;
            }
            if (b == null || b.Length < 12) return;
            if (type != PacketIds.P_FE2CL_PC_GROUP_JOIN && type != PacketIds.P_FE2CL_PC_GROUP_JOIN_SUCC && type != PacketIds.P_FE2CL_PC_GROUP_LEAVE) return;
            var body = b; bool leave = type == PacketIds.P_FE2CL_PC_GROUP_LEAVE;
            q.Enqueue(() =>
            {
                int who = I(body, 0), n = Mathf.Clamp(I(body, 4), 0, 4); int self = ents != null ? ents.SelfId : -1;
                var ids = new List<int>(); for (int i = 0; i < n; i++) { int o = 12 + 112 * i; if (o + 4 > body.Length) break; ids.Add(I(body, o)); }
                lock (members)
                {
                    members.Clear(); foreach (int id in ids) if (id != self) members.Add(id);
                    if (leave && who == self) members.Clear();
                }
                Size = (leave && who == self) ? 0 : n; if (n <= 1 && !leave) Size = Mathf.Max(Size, 1);
                if (leave && n <= 1) Size = 0;
            });
        }
    }
}
