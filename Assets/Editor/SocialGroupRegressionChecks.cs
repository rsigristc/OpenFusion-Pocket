using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.World;

namespace OpenFusion.EditorTools
{
    public static class SocialGroupRegressionChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        static void I(byte[] b,int o,int value)=>BitConverter.GetBytes(value).CopyTo(b,o);
        static void L(byte[] b,int o,long value)=>BitConverter.GetBytes(value).CopyTo(b,o);
        static void Check(bool value,string why){if(!value)throw new Exception(why);}
        static byte[] Roster(int other=20,long uid=2000){var b=new byte[236];I(b,0,other);I(b,4,2);I(b,12,10);L(b,16,1000);I(b,124,other);L(b,128,uid);return b;}
        static void Dispatch(PacketDispatcher d,uint type,byte[] body){var p=ClientPackets.New(type,body.Length);body.CopyTo(p,4);d.Dispatch(p);}
        public static void Batch(){try{Run();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Run()
        {
            Directory.CreateDirectory("Logs/legacy-audit");var host=new GameObject("Social and group audit");var sender=NpcUI.Sender;var oldClient=GameClient.FindSelf;
            try {
                var d=new PacketDispatcher();var world=host.AddComponent<WorldEntities>();
                typeof(WorldEntities).GetProperty("SelfId").GetSetMethod(true).Invoke(world,new object[]{10});
                var groups=GroupState.Ensure(host);groups.Init(world,d);
                var chat=host.AddComponent<ChatHud>();chat.Init(null,world,d);
                var social=SocialMenu.Ensure(host);social.Init(d,world,null);
                int sends=0,bubbles=0;NpcUI.Sender=p=>sends++;chat.ChatBubble+=(id,text,emote,kind)=>bubbles++;
                Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_JOIN_SUCC,Roster());Call(groups,"Update");
                Check(GroupState.Size==2&&GroupState.IsMember(20)&&!GroupState.IsMember(10)&&social.IsGroupMemberUid(2000),"Group UIDs must remain available without a spawned/nearby remote avatar.");
                var shortRoster=new byte[130];Array.Copy(Roster(),shortRoster,130);
                Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_MEMBER_INFO,shortRoster);Call(groups,"Update");
                Check(GroupState.Size==2&&GroupState.IsMember(20),"A truncated roster must not partially replace a valid group.");
                var duplicate=Roster();I(duplicate,124,10);Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_JOIN,duplicate);Call(groups,"Update");
                Check(GroupState.IsMember(20),"Duplicate roster IDs must not replace valid membership.");
                Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_MEMBER_INFO,Roster(30,3000));Call(groups,"Update");
                Check(GroupState.IsMember(30)&&!GroupState.IsMember(20)&&social.IsGroupMemberUid(3000)&&!social.IsGroupMemberUid(2000),"Periodic MEMBER_INFO must refresh both transient IDs and stable UIDs.");
                Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_LEAVE_SUCC,Array.Empty<byte>());Call(groups,"Update");
                Check(GroupState.Size==0&&GroupState.Members().Length==0&&!social.IsGroupMemberUid(3000),"Empty leave-success packet must clear membership, including UID-based buddy warp checks.");
                Dispatch(d,PacketIds.P_FE2CL_PC_GROUP_JOIN,Roster());Call(groups,"Update");Call(groups,"OnEnter",new EnterSucc());Call(groups,"Update");
                Check(GroupState.Size==0,"Re-entering a session must clear obsolete group state.");
                var buddy=new byte[88];buddy[13]=1;I(buddy,16,101);L(buddy,20,9001);buddy[28]=1;buddy[29]=1;buddy[30]=1;buddy[85]=1;
                Dispatch(d,PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC,buddy);Call(social,"Update");
                Check(chat.IsBlockedPc!=null&&chat.IsBlockedUid!=null&&chat.IsBlockedPc(101)&&chat.IsBlockedUid(9001),"Server-confirmed block state must be wired into both public and private chat filters, without requiring a streamed avatar.");
                var msg=new byte[264];I(msg,0,101);System.Text.Encoding.Unicode.GetBytes("Blocked message").CopyTo(msg,4);
                Call(chat,"Handle",PacketIds.P_FE2CL_REP_SEND_FREECHAT_MESSAGE_SUCC,msg);
                Check(bubbles==0,"Ignored public messages must not create world speech bubbles.");
                buddy[28]=0;Dispatch(d,PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC,buddy);Call(social,"Update");
                Check(!chat.IsBlockedPc(101)&&!chat.IsBlockedUid(9001),"Server-confirmed unblocking must take effect without reconnecting.");
                Call(chat,"Handle",PacketIds.P_FE2CL_REP_SEND_FREECHAT_MESSAGE_SUCC,msg);Check(bubbles==1,"Unblocked messages must still reach the existing chat/bubble path.");
                var client=host.AddComponent<GameClient>();GameClient.FindSelf=client;client.RunSpeed=800;client.JumpVelocity=8;
                GameClient.ApplyBuff(1,1,200);GameClient.ApplyBuff(3,1,250);GameClient.ApplyBuff(8,1,100);
                var nc=host.AddComponent<NanocomHud>();typeof(NanocomHud).GetField("ents",Private).SetValue(nc,world);
                var expired=new byte[12];I(expired,0,1);I(expired,4,10);I(expired,8,4);
                Call(nc,"Handle",PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_OUT,expired);
                Check(client.RunSpeed==800&&Mathf.Approximately(client.JumpVelocity,10.5f),"Expired slow/speed modifiers must clear while an active jump bit (4) remains; swim bit 2 is unrelated.");
                I(expired,8,0);Call(nc,"Handle",PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_OUT,expired);
                Check(client.RunSpeed==800&&Mathf.Approximately(client.JumpVelocity,8),"Expired jump must restore the base jump height.");
                GameClient.ApplyBuff(8,1,100);I(expired,4,11);Call(nc,"Handle",PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_OUT,expired);
                Check(client.RunSpeed==700,"Another player's expiry must not clear the local slow condition.");
                Check(sends==0,"Audit must not send real invitations, chat, trading or buddy modification requests.");
            } finally {UnityEngine.Object.DestroyImmediate(host);NpcUI.Sender=sender;GameClient.FindSelf=oldClient;}
            File.WriteAllText("Logs/legacy-audit/social-group-checks.txt","PASS: real dispatcher JOIN/MEMBER_INFO/empty LEAVE_SUCCESS, UID membership outside streaming range, atomic malformed/duplicate rejection, session reset, live blocked/unblocked chat predicates and bubble filtering, speed/slow expiry and jump/swim bit separation, foreign-player isolation; zero outgoing social packets.\n");
            Debug.Log("[social-group audit] regression passed");
        }
    }
}
