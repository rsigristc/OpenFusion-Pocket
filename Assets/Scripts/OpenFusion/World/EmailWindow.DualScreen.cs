using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed partial class EmailWindow
    {
        public static EmailWindow Instance;
        [Serializable] public sealed class DualMailEntry
        { public string key,from,subject,date,image; public bool unread,selected,attachments; }
        [Serializable] public sealed class DualMailItem
        { public int count;public string image; }
        [Serializable] public sealed class DualMailDetail
        { public string key,from,subject,date,image,body,note;public int taros;public bool canClaimItems,canClaimTaros,canDelete,canReply;public List<DualMailItem> items=new List<DualMailItem>(); }
        [Serializable] public sealed class DualMailState
        { public bool guide,busy,previous,next;public int page,unread;public string message;public List<DualMailEntry> entries=new List<DualMailEntry>();public DualMailDetail detail; }
        bool dualPrepared,pendingCompose;string pendingRecipient="";long pendingRecipientUid,requestedRead;
        int requestedPage=1,playerUnread,lastAnnounced;float nextMailRefresh;string guideScope="";
        public int UnreadCount { get;private set; }
        void OnMailEnter(EnterSucc snapshot)
        {
            q.Enqueue(()=>{guideScope="pocket.mail.read."+(GameClient.FindSelf?.Bootstrap?.ServerUrl??"")+":"+snapshot.Self.Uid+":";
                playerUnread=lastAnnounced=UnreadCount=0;dualPrepared=false;nextMailRefresh=0;requestedRead=0;Array.Clear(user,0,user.Length);userCount=0;composing=false;});
        }
        string GuideReadKey(Node node)=>guideScope+GuideNum+":"+node.Mode+":"+node.Mission.m_iHTaskID;
        bool GuideUnread(Node node)=>!string.IsNullOrEmpty(guideScope)&&PlayerPrefs.GetInt(GuideReadKey(node),0)==0;
        void UpdateMailNotice()
        {
            if(TutorialTraining.Active||nh==null||ents?.Self==null)return;
            if(Time.unscaledTime>=nextMailRefresh){nextMailRefresh=Time.unscaledTime+1;
                string selected=comMail&&click>=0&&click+guidePage*5<nodes.Count?EntryKey(click):null;
                BuildGuideMail();if(comMail&&selected!=null){int found=nodes.FindIndex(n=>"g:"+n.Mode+":"+n.Mission.m_iHTaskID==selected);
                    if(found<0){click=-1;bodyText="";}else{guidePage=found/5;click=found%5;}}
                guidePage=Mathf.Clamp(guidePage,0,Mathf.Max(0,(nodes.Count-1)/5));
            }
            int unread=playerUnread;foreach(var node in nodes)if(GuideUnread(node))unread++;
            UnreadCount=unread;GameMenu.NewMail=unread>0;
            if(GameClient.FindSelf!=null&&GameClient.FindSelf.WorldConnected && !(OpenFusion.Screens.LoadingScreen.I?.Visible??false)){
                if(unread>lastAnnounced)NanoMessages.Post("E-MAIL",unread+" unread message"+(unread==1?"":"s")+". Open E-Mail to read.",NanoMessages.Kind.Etc,8);
                lastAnnounced=unread;
            }
        }
        bool MailActions => !TutorialTraining.Active&&!GameMenu.Blocking&&!NpcUI.Active&&!PlayerLife.Active&&!busy&&!sending&&msg==null&&
            GameClient.FindSelf!=null&&GameClient.FindSelf.WorldConnected&&NpcUI.Sender!=null;
        void PrepareDualMail(){if(dualPrepared)return;dualPrepared=true;comMail=true;guidePage=0;click=-1;BuildGuideMail();}
        string EntryKey(int row)=>comMail?"g:"+nodes[row+guidePage*5].Mode+":"+nodes[row+guidePage*5].Mission.m_iHTaskID:"p:"+user[row].Index;
        bool SelectedMail(string key)=>click>=0&&(comMail?click+guidePage*5<nodes.Count:click<5&&user[click].Index>0)&&EntryKey(click)==key;
        public void DualMailCommand(string action,string key)
        {
            PrepareDualMail();if(action=="mailDismiss"&&msg!=null&&msgYes==null){msg=null;return;}if(!MailActions)return;
            if(action=="mailMode"){if(key=="guide"){comMail=true;guidePage=0;click=-1;requestedRead=0;BuildGuideMail();}else if(key=="player"){comMail=false;click=-1;RequestPage(1);}return;}
            if(action=="mailPage"){if(key!="next"&&key!="previous")return;int delta=key=="next"?1:-1;
                if(comMail){guidePage=Mathf.Clamp(guidePage+delta,0,Mathf.Max(0,(nodes.Count-1)/5));click=-1;}else RequestPage(Mathf.Clamp(userPage+delta,1,4));return;}
            if(action=="mailSelect"){
                int count=comMail?Mathf.Min(5,nodes.Count-guidePage*5):5;
                for(int i=0;i<count;i++)if((comMail||user[i].Index>0)&&EntryKey(i)==key){ClickRow(i);return;}return;
            }
            if(action=="mailCompose"){QueueCompose("",0);return;}
            if(!SelectedMail(key)||comMail||!readMail||readIndex<=0||readIndex!=user[click].Index)return;
            if(action=="mailReply"){QueueCompose(user[click].First+" "+user[click].Last,user[click].FromUid);return;}
            if(action=="mailDelete"){DeleteMail();return;}
            uint packet=action=="mailClaimItems"?PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_ITEM_ALL:action=="mailClaimTaros"?PacketIds.P_CL2FE_REQ_PC_RECV_EMAIL_CANDY:0;
            if(packet==0||action=="mailClaimTaros"&&readCash<=0||action=="mailClaimItems"&&!Array.Exists(readItems,a=>a.Id>0))return;
            var request=ClientPackets.New(packet,8);BitConverter.GetBytes(readIndex).CopyTo(request,4);Busy();Send(request);
        }
        void QueueCompose(string name,long uid){if(!GameMenu.OpenWindow(GameMenu.Win.EMail))return;pendingRecipient=name;pendingRecipientUid=uid;pendingCompose=true;}
        public DualMailState DualMail(Func<Texture,string> image)
        {
            PrepareDualMail();var state=new DualMailState{guide=comMail,busy=!MailActions,page=comMail?guidePage+1:userPage,unread=UnreadCount,
                previous=comMail?guidePage>0:prevB,next=comMail?(guidePage+1)*5<nodes.Count:nextB,message=msg??(busy?"Loading mail…":"")};
            int count=comMail?Mathf.Min(5,Mathf.Max(0,nodes.Count-guidePage*5)):5;
            for(int row=0;row<count;row++){
                if(!comMail&&user[row].Index<=0)continue;
                var entry=new DualMailEntry{key=EntryKey(row),selected=row==click};
                if(comMail){var node=nodes[row+guidePage*5];int npc=node.Mode==1?node.Mission.m_iSTMessageSendNPC:GuideNum>=1&&GuideNum<=5?GuideNpc[GuideNum-1]:0;
                    entry.from=NpcNm(npc);entry.subject=MStr(node.Mission.m_iHMissionName);entry.image=image(Portrait(npc));entry.unread=GuideUnread(node);entry.date="Guide mail";}
                else{var info=user[row];entry.from=(BuddyName(info)+" "+info.Last).Trim();entry.subject=info.Subject;entry.date=Date(info);entry.unread=info.Read==0;entry.attachments=info.ItemCandy!=0;entry.image="";}
                state.entries.Add(entry);
                if(!entry.selected)continue;
                state.detail=new DualMailDetail{key=entry.key,from=entry.from,subject=entry.subject,date=entry.date,image=entry.image,
                    body=comMail||readMail&&readIndex==user[row].Index?bodyText:"Select this message to load it.",
                    note=comMail?"Guide mail is generated from your missions and is removed when its related mission is completed.":""};
                if(!comMail&&readMail&&readIndex==user[row].Index){var detail=state.detail;detail.taros=readCash;detail.canDelete=detail.canReply=MailActions;
                    detail.canClaimTaros=MailActions&&readCash>0;foreach(var item in readItems)if(item.Id>0)detail.items.Add(new DualMailItem{image=image(ItemIcon(item)),count=item.Type==7?Mathf.Max(1,item.Opt):1});
                    detail.canClaimItems=MailActions&&detail.items.Count>0;}
            }
            return state;
        }
    }
}
