using System;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed partial class EmailWindow
    {
        readonly int[] attachmentSlots={-1,-1,-1,-1};
        readonly ItemBase[] outgoingItems=new ItemBase[4];
        int attachmentPicker=-1;Vector2 attachmentScroll;
        readonly NumPad attachmentAmount=new NumPad();
        void ResetAttachments(){for(int i=0;i<4;i++){attachmentSlots[i]=-1;outgoingItems[i]=default;}attachmentPicker=-1;attachmentAmount.Open=false;}
        bool Mailable(int slot){if(slot<0||slot>=InventoryWindow.Items.Length)return false;var item=InventoryWindow.Items[slot];var data=ItemTbl.Get(tables,item.Type,item.Id);return item.Id>0&&item.TimeLimit==0&&data.Ok&&data.TradeAble!=0;}
        int Postage {get{int count=0;foreach(var item in outgoingItems)if(item.Id>0)count++;return 50+20*count;}}
        bool ValidAttachments(){for(int i=0;i<4;i++)if(outgoingItems[i].Id>0){int slot=attachmentSlots[i];if(!Mailable(slot))return false;var current=InventoryWindow.Items[slot];var sent=outgoingItems[i];if(current.Id!=sent.Id||current.Type!=sent.Type||sent.Opt<=0||sent.Opt>current.Opt||current.TimeLimit!=sent.TimeLimit)return false;for(int j=0;j<i;j++)if(attachmentSlots[j]==slot)return false;}return true;}
        void DrawAttachments(bool enabled)
        {
            var empty=St("slotboxempty");
            for(int i=0;i<4;i++){
                var r=new Rect(ItemNew.x+ItemNew.width*i,ItemNew.y,ItemNew.width,ItemNew.height);var item=outgoingItems[i];
                if(Event.current.type==EventType.Repaint){Skin.Bg(r,empty?.N,empty);if(item.Id>0){var image=ItemIcon(new Att{Type=item.Type,Id=item.Id,Opt=item.Opt});if(image!=null)GUI.DrawTexture(r,image,ScaleMode.ScaleToFit);}}
                if(item.Id>0&&item.Type==7)Txt(r,item.Opt.ToString(),St("label"),Color.white,TextAnchor.UpperLeft);
                if(Hit(r,enabled)){if(item.Id>0){outgoingItems[i]=default;attachmentSlots[i]=-1;}else{attachmentPicker=i;buddyWin=calcWin=false;}}
            }
        }
        void DrawAttachmentPicker(bool enabled)
        {
            if(attachmentPicker<0)return;
            var box=new Rect(580,290,350,330);UiKit.Fill(box,new Color(0,.05f,.1f,.98f));
            GUI.Label(new Rect(box.x+8,box.y+5,280,25),"SELECT ATTACHMENT");
            if(GUI.Button(new Rect(box.xMax-38,box.y+4,32,26),"×")){attachmentPicker=-1;return;}
            bool any=false;for(int i=0;i<InventoryWindow.Items.Length;i++)if(Mailable(i)&&Array.IndexOf(attachmentSlots,i)<0){any=true;break;}
            if(!any){GUI.Label(new Rect(box.x+10,box.y+45,box.width-20,55),"No tradable items available.");return;}
            var view=new Rect(box.x+5,box.y+35,box.width-10,box.height-40);attachmentScroll=GUI.BeginScrollView(view,attachmentScroll,new Rect(0,0,310,Mathf.Ceil(InventoryWindow.Items.Length/4f)*74));
            for(int slot=0;slot<InventoryWindow.Items.Length;slot++){
                int selectedSlot=slot;var item=InventoryWindow.Items[slot];var r=new Rect(slot%4*76,slot/4*74,70,68);bool available=Mailable(slot)&&Array.IndexOf(attachmentSlots,slot)<0;
                if(Event.current.type==EventType.Repaint&&item.Id>0){var icon=ItemIcon(new Att{Type=item.Type,Id=item.Id,Opt=item.Opt});var old=GUI.color;GUI.color=available?Color.white:new Color(1,1,1,.3f);if(icon!=null)GUI.DrawTexture(r,icon,ScaleMode.ScaleToFit);GUI.color=old;}
                if(Hit(r,enabled&&available)){int target=attachmentPicker;attachmentPicker=-1;
                    if(item.Type==7&&item.Opt>1)attachmentAmount.Show("ATTACH ITEM","Amount","ATTACH",item.Opt,1,amount=>{if(amount>0){attachmentSlots[target]=selectedSlot;outgoingItems[target]=item;outgoingItems[target].Opt=amount;}});
                    else{attachmentSlots[target]=slot;outgoingItems[target]=item;}
                }
            }GUI.EndScrollView();
        }
        byte[] BuildOutgoingMail()
        {
            if(!ValidAttachments())throw new InvalidOperationException("An attachment changed. Remove it and select it again.");
            var packet=ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_SEND_EMAIL,1164);BitConverter.GetBytes(toUid).CopyTo(packet,4);
            ClientPackets.PutStr(packet,12,string.IsNullOrEmpty(subject)?"No subject.":subject,32);ClientPackets.PutStr(packet,76,content??"",512);
            for(int i=0;i<4;i++)if(outgoingItems[i].Id>0){int offset=1100+16*i;var item=outgoingItems[i];ClientPackets.PutInt(packet,offset,attachmentSlots[i]);BitConverter.GetBytes(item.Type).CopyTo(packet,offset+4);BitConverter.GetBytes(item.Id).CopyTo(packet,offset+6);ClientPackets.PutInt(packet,offset+8,item.Opt);ClientPackets.PutInt(packet,offset+12,item.TimeLimit);}
            ClientPackets.PutInt(packet,1164,taros);return packet;
        }
    }
}
