using System;
using System.Collections.Generic;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class GuidePastRegressionChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        public static void Run()
        {
            var root = new GameObject("Guide-to-past regression");
            var previousUi = NpcUI.Instance; var previousGuide = GuideChangerWindow.Instance;
            var sender = NpcUI.Sender; int guideId = InventoryWindow.Guide;
            try
            {
                var entities = root.AddComponent<WorldEntities>();
                entities.Instances = new InstanceTableScript { m_pWarpData = new[] {
                    new WarpElement { m_iNpcNumber=1424, m_iWarpNumber=75 },
                    new WarpElement { m_iNpcNumber=1425, m_iWarpNumber=76 }
                }};
                var ui = root.AddComponent<NpcUI>(); ui.Init(entities, null);
                var window = root.AddComponent<GuideChangerWindow>(); window.Init(null, entities, null, null);
                var packets = new List<byte[]>(); NpcUI.Sender = p => packets.Add(p);
                var npcs = Get<Dictionary<int, GameObject>>(entities, "npcs");
                var destination = new GameObject("Server past exit"); destination.transform.SetParent(root.transform);
                var info = destination.AddComponent<NpcInfo>(); info.NpcNumber = 1425; info.Id = 987654; info.Type = 101;
                npcs.Add(info.Id, destination);

                InventoryWindow.Guide = 5; window.Open(123, 23, 18);
                Call(window, "ChoosePast"); Check(Get<bool>(window,"bSelect") && packets.Count==0,"Warp confirmation must first offer guide selection without inventing a warp.");
                Call(window,"SendSelectGuide",2);
                Check(packets.Count==1 && BitConverter.ToUInt32(packets[0],0)==PacketIds.P_CL2FE_REQ_PC_CHANGE_MENTOR,"Choosing a guide must send only the mentor request before acknowledgement.");
                Check(Get<float>(ui,"warpWait")==0,"No past warp may start before server mentor success.");
                Set(window,"sendUntil",-1f);Call(window,"Update");
                Check(!Get<bool>(window,"bSend") && Get<bool>(window,"mentorPending"),"A slow server must not lose an outstanding guide acknowledgement when the UI wait expires.");
                var success = new byte[8];BitConverter.GetBytes((short)2).CopyTo(success,0);BitConverter.GetBytes((short)1).CopyTo(success,2);BitConverter.GetBytes(1500).CopyTo(success,4);
                Call(window,"OnPacket",PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_SUCC,success);Call(window,"Update");
                Check(InventoryWindow.Guide==2 && Get<float>(ui,"warpWait")>0 && !Get<bool>(window,"open") && Get<string>(window,"msg")==null,"First mentor success must continue to the real NPC warp, never show NPC 1425 as an error/message.");
                Call(ui,"AdvanceWarp",2f);
                Check(packets.Count==2 && BitConverter.ToUInt32(packets[1],0)==PacketIds.P_CL2FE_REQ_PC_WARP_USE_NPC && BitConverter.ToInt32(packets[1],4)==info.Id && BitConverter.ToInt32(packets[1],8)==76,"Warp must use the server NPC instance ID and table warp number 76, not a table ID as runtime ID or a direct position mutation.");
                Call(window,"OnPacket",PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_SUCC,success);Call(window,"Update");Call(ui,"AdvanceWarp",2f);
                Check(packets.Count==2 && !Get<bool>(window,"pastWarpPending"),"Duplicate mentor acknowledgements must not queue another one-way warp.");

                Set(ui,"whiteHold",false); packets.Clear();window.Open(123,23,18);Call(window,"ChoosePast");Call(ui,"AdvanceWarp",2f);
                Check(packets.Count==1 && BitConverter.ToUInt32(packets[0],0)==PacketIds.P_CL2FE_REQ_PC_WARP_USE_NPC,"A player with an acknowledged guide must resume without sending/paying for another mentor change.");

                Set(ui,"whiteHold",false);InventoryWindow.Guide=5;window.Open(123,23,18);Call(window,"SendSelectGuide",3);packets.Clear();
                Call(window,"OnPacket",PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_FAIL,new byte[6]);Call(window,"Update");
                Check(Get<float>(ui,"warpWait")==0 && packets.Count==0 && !Get<bool>(window,"pastWarpPending"),"Rejected guide changes must never trigger a warp.");

                Call(window,"Exit",false);InventoryWindow.Guide=2; npcs.Clear();window.Open(123,23,18);Call(window,"ChoosePast");
                Check(Get<bool>(window,"pastWarpPending") && packets.Count==0,"A missing streamed NPC must wait, not synthesize an instance ID or teleport.");
                npcs.Add(info.Id,destination);Call(window,"ContinuePastWarp");Call(ui,"AdvanceWarp",2f);
                Check(packets.Count==1,"The continuation must recover when the actual server NPC arrives.");
                Set(ui,"whiteHold",false);npcs.Clear();window.Open(123,23,18);Call(window,"ChoosePast");Set(window,"pastWarpDeadline",-1f);Call(window,"ContinuePastWarp");
                Check(!Get<bool>(window,"pastWarpPending") && !Get<bool>(window,"bSend") && Get<string>(window,"msg").Contains("Time Machine"),"Missing destinations must time out with readable retry instructions.");
                Call(window,"Exit",false);InventoryWindow.Guide=5;window.Open(123,23,18);Call(window,"SendSelectGuide",2);Call(window,"Exit",false);packets.Clear();
                Call(window,"OnPacket",PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_SUCC,success);Call(window,"Update");
                Check(InventoryWindow.Guide==2 && !Get<bool>(window,"pastWarpPending") && packets.Count==0,"An acknowledgement after cancellation must preserve the server's guide choice without forcing travel.");
                Debug.Log("[guide-past regression] PASS: server acknowledgement, real NPC/warp identity, duplicate suppression, rejected requests, saved-guide recovery and delayed/missing destination handling.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); NpcUI.Instance=previousUi; GuideChangerWindow.Instance=previousGuide; NpcUI.Sender=sender; InventoryWindow.Guide=guideId; WarpFlow.Cancel(); }
        }
    }
}
