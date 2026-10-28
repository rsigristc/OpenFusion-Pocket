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
        static readonly HashSet<long> memberUids = new HashSet<long>();
        public static bool IsMember(int pcId) { lock (members) return members.Contains(pcId); }
        public static bool IsMemberUid(long uid) { lock (members) return uid != 0 && memberUids.Contains(uid); }
        public static int[] Members() { lock (members) { var a = new int[members.Count]; members.CopyTo(a); return a; } }

        PacketDispatcher disp; WorldEntities ents; readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        public static GroupState Ensure(GameObject host) { var g = host.GetComponent<GroupState>(); if (g == null) g = host.AddComponent<GroupState>(); return g; }
        public void Init(WorldEntities e, PacketDispatcher d) { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnEnter; } ents = e; disp = d; Clear(); if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnter += OnEnter; } }
        static void Clear() { lock(members) { Size = 0; members.Clear(); memberUids.Clear(); } }
        void OnEnter(EnterSucc e) { q.Enqueue(Clear); }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnEnter; } Clear(); }
        void Update() { while (q.TryDequeue(out var a)) a(); }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void OnPacket(uint type, byte[] b)
        {
            // Legacy's leave success is an empty struct, not a roster packet.
            if (type == PacketIds.P_FE2CL_PC_GROUP_LEAVE_SUCC) { q.Enqueue(Clear); return; }
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
            if (type != PacketIds.P_FE2CL_PC_GROUP_JOIN && type != PacketIds.P_FE2CL_PC_GROUP_JOIN_SUCC && type != PacketIds.P_FE2CL_PC_GROUP_LEAVE && type != PacketIds.P_FE2CL_PC_GROUP_MEMBER_INFO) return;
            int pcCount=I(b,4), npcCount=I(b,8);
            // Reject malformed snapshots atomically; never replace a valid group
            // with a partial roster or trust an unchecked server count.
            if(pcCount<0 || pcCount>4 || npcCount<0 || npcCount>64 || 12L+112L*pcCount+32L*npcCount>b.Length)return;
            var body = b; bool leave = type == PacketIds.P_FE2CL_PC_GROUP_LEAVE;
            q.Enqueue(() =>
            {
                int who = I(body, 0), n = I(body, 4); int self = ents != null ? ents.SelfId : -1;
                var ids = new HashSet<int>(); var uids = new HashSet<long>();
                for (int i = 0; i < n; i++) { int o = 12 + 112 * i, id=I(body,o); if(id<=0 || !ids.Add(id))return; if(id!=self)uids.Add(BitConverter.ToInt64(body,o+4)); }
                if(self>0 && n>0 && !ids.Contains(self) && !(leave&&who==self))return;
                lock (members)
                {
                    members.Clear(); memberUids.Clear(); foreach (int id in ids) if (id != self) members.Add(id);
                    foreach(long uid in uids)if(uid!=0)memberUids.Add(uid);
                    Size=n;
                    if (leave && (who == self || n <= 1 && npcCount == 0)) { members.Clear();memberUids.Clear();Size=0; }
                }
            });
        }
    }
}
