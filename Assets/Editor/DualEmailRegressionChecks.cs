using System;
using System.IO;
using System.Reflection;
using OpenFusion;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class DualEmailRegressionChecks
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Flags).Invoke(obj,args);
        static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Flags).SetValue(obj,value);
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        public static void Run()
        {
            var host=new GameObject("Mail fixture");var oldClient=GameClient.FindSelf;var sender=NpcUI.Sender;var inventory=(ItemBase[])InventoryWindow.Items.Clone();
            bool registered=GameMenu.Implemented.Contains(GameMenu.Win.EMail);
            try{
                GameMenu.CloseWindow();var client=host.AddComponent<GameClient>();GameClient.FindSelf=client;
                typeof(GameClient).GetProperty("Shard_").SetValue(client,new EnteredShard{Session=new GameSession(Stream.Null,new PacketCrypto())});Set(client,"shardAlive",true);
                var tables=new XdtTableScript{m_pGeneralItemTable=new GeneralItemTableScript{m_pItemData=new GeneralItemElement[3]}};
                tables.m_pGeneralItemTable.m_pItemData[1]=new GeneralItemElement{m_iTradeAble=1,m_iStackNumber=99};
                tables.m_pGeneralItemTable.m_pItemData[2]=new GeneralItemElement{m_iTradeAble=0,m_iStackNumber=1};
                var hud=host.AddComponent<PlayerHud>();var mail=EmailWindow.Ensure(host);mail.Init(null,null,tables,null,hud);Set(mail,"dualPrepared",true);
                byte[] packet=null;int sends=0;NpcUI.Sender=p=>{packet=p;sends++;};
                mail.DualMailCommand("mailMode","player");Check(packet!=null&&PacketIds.Unstamp(BitConverter.ToUInt32(packet,0))==PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_PAGE_LIST&&packet[4]==1,"Player mail must request the real first server page");
                const long id=9007199254741001L;var page=new byte[1024];page[0]=1;BitConverter.GetBytes(id).CopyTo(page,4);ClientPackets.PutStr(page,20,"Sender",9);ClientPackets.PutStr(page,38,"Name",17);ClientPackets.PutStr(page,72,"Fixture subject",32);
                Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_NEW_EMAIL,BitConverter.GetBytes(2));Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_PAGE_LIST_SUCC,page);
                var state=mail.DualMail(t=>"");Check(state.entries.Count==1&&state.entries[0].key=="p:"+id&&state.entries[0].unread&&state.detail==null,"Exact 64-bit mail identity, unread flag and explicit read selection must survive DS serialization");
                mail.DualMailCommand("mailSelect","p:"+id);Check(BitConverter.ToInt64(packet,4)==id,"Selecting DS mail must send its exact 64-bit read request");
                var read=new byte[1084];BitConverter.GetBytes(id+1).CopyTo(read,0);ClientPackets.PutStr(read,8,"Unrelated response",512);Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_READ_EMAIL_SUCC,read);
                Check(!mail.DualMail(t=>"").detail.canDelete,"An unrelated read response must not expose claim/delete actions");
                Array.Clear(read,0,read.Length);BitConverter.GetBytes(id).CopyTo(read,0);ClientPackets.PutStr(read,8,"Actual mail body",512);BitConverter.GetBytes((short)7).CopyTo(read,1032);BitConverter.GetBytes((short)1).CopyTo(read,1034);BitConverter.GetBytes(3).CopyTo(read,1036);BitConverter.GetBytes(125).CopyTo(read,1080);
                Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_READ_EMAIL_SUCC,read);state=mail.DualMail(t=>"");
                Check(state.detail.body=="Actual mail body"&&state.detail.items.Count==1&&state.detail.taros==125&&!state.entries[0].unread,"Acknowledged body/attachments/Taros/read state must use the shared mail model");
                int before=sends;mail.DualMailCommand("mailClaimItems","p:"+(id+1));Check(sends==before,"Stale message commands must be rejected");
                mail.DualMailCommand("mailClaimItems","p:"+id);Check(BitConverter.ToInt64(packet,4)==id&&mail.DualMail(t=>"").detail.items.Count==1,"Claim must retain attachments until server acknowledgment");
                Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_SUCC,BitConverter.GetBytes(id+1));Check(mail.DualMail(t=>"").detail.items.Count==1,"An unrelated claim acknowledgement must not clear this message's attachments");
                Call(mail,"Handle",PacketIds.P_FE2CL_REP_PC_RECV_EMAIL_ITEM_ALL_SUCC,BitConverter.GetBytes(id));Check(mail.DualMail(t=>"").detail.items.Count==0,"Only matching claim acknowledgment clears attachments");
                var mission=new MissionElement{m_iHMissionID=1,m_iHTaskID=101,m_iHMissionType=1,m_iHMissionName=1,m_iSTMessageType=4,m_iSTMessageTextID=2,m_iSTMessageSendNPC=707};
                var unavailable=new MissionElement{m_iHMissionID=2,m_iHTaskID=102,m_iHMissionType=2,m_iMentorEmailID=new[]{2,0,0,0,0},m_iCTRReqLvMin=99};
                tables.m_pMissionTable=new MissionTableScript{m_pMissionData=new[]{mission,unavailable},m_pMissionStringData=new[]{new NameStringElement(),new NameStringElement{m_pstrNameString="Guide subject"},new NameStringElement{m_pstrNameString="Real guide body"}}};
                var world=host.AddComponent<WorldEntities>();var quest=new QuestSave{Mentor=1};quest.Running[0]=101;world.Missions=new MissionState(tables.m_pMissionTable,quest,1);
                var nh=host.AddComponent<NanocomHud>();nh.Init(hud,world,null,tables);Call(nh,"InsertActive",Call(nh,"ResetNode",101,mission));mail.Init(null,world,tables,nh,hud);
                mail.DualMailCommand("mailMode","guide");state=mail.DualMail(t=>"");Check(state.entries.Count==1&&state.entries[0].subject=="Guide subject","Guide mail must reuse current mission data and reject unavailable high-level mission mail");
                mail.DualMailCommand("mailSelect",state.entries[0].key);state=mail.DualMail(t=>"");Check(state.detail.body=="Real guide body"&&!state.detail.canDelete&&!state.detail.canClaimItems,"Guide mail must show real text without inventing player-mail attachments or delete actions");
                Call(mail,"StartNew","Recipient",123L);InventoryWindow.Items[0]=new ItemBase{Type=7,Id=1,Opt=5};InventoryWindow.Items[1]=new ItemBase{Type=7,Id=2,Opt=1};
                Check((bool)Call(mail,"Mailable",0)&&!(bool)Call(mail,"Mailable",1),"Outgoing selector must reject untradeable items");
                var slots=(int[])typeof(EmailWindow).GetField("attachmentSlots",Flags).GetValue(mail);var items=(ItemBase[])typeof(EmailWindow).GetField("outgoingItems",Flags).GetValue(mail);slots[0]=0;items[0]=new ItemBase{Type=7,Id=1,Opt=2};Set(mail,"taros",100);
                var outgoing=(byte[])Call(mail,"BuildOutgoingMail");Check(outgoing.Length==1168&&BitConverter.ToInt64(outgoing,4)==123&&BitConverter.ToInt32(outgoing,1100)==0&&BitConverter.ToInt16(outgoing,1104)==7&&BitConverter.ToInt16(outgoing,1106)==1&&BitConverter.ToInt32(outgoing,1108)==2&&BitConverter.ToInt32(outgoing,1164)==100,"Outgoing attachments must use Legacy's exact 16-byte inventory-slot/item records");
                Check((int)typeof(EmailWindow).GetProperty("Postage",Flags).GetValue(mail)==70&&InventoryWindow.Items[0].Opt==5,"Postage is 50 + 20 per attachment and composing never removes inventory locally");
                InventoryWindow.Items[0]=default;Check(!(bool)Call(mail,"ValidAttachments"),"Changed inventory must invalidate a queued attachment before send");
                Debug.Log("[DS email regression] server paging, exact IDs, selected reads, stale replies, unread state, acknowledged claims and safe outgoing attachment records passed.");
            }finally{UnityEngine.Object.DestroyImmediate(host);GameClient.FindSelf=oldClient;NpcUI.Sender=sender;Array.Copy(inventory,InventoryWindow.Items,inventory.Length);if(!registered)GameMenu.Implemented.Remove(GameMenu.Win.EMail);GameMenu.CloseWindow();}
        }
    }
}
