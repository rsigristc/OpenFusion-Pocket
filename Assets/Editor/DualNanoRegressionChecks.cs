using System;
using System.IO;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;
using OpenFusion.World;
using OpenFusion.Screens;
using UnityEngine;
using Unity.Collections;

namespace OpenFusion.EditorTools
{
    public static class DualNanoRegressionChecks
    {
        [Serializable] sealed class PanelState { public bool station; public InventoryWindow.DualNanoCollection collection; }
        static void Check(bool b, string message) { if (!b) throw new InvalidOperationException(message); }
        static void Set(object obj,string name,object value) => obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(obj,value);
        public static void Run()
        {
            GameObject host=null; var sender=NpcUI.Sender; var owned=(short[])InventoryWindow.NanoOwned.Clone(); var skills=(short[])InventoryWindow.NanoSkill.Clone();
            var items=(ItemBase[])InventoryWindow.Items.Clone(); bool machine=InventoryWindow.Machine, registered=GameMenu.Implemented.Contains(GameMenu.Win.MyStuff);
            TouchLayout.Push();
            try
            {
                host=new GameObject("DualNano regression"); var hud=host.AddComponent<PlayerHud>();
                hud.Data.Level=1; hud.Data.FusionMatter=100; hud.Data.NanoSlots[0]=1; hud.Data.NanoSlots[1]=0; hud.Data.ActiveNanoSlot=0; hud.Data.NanoStamina[0]=82;
                var table=new XdtTableScript { m_pNanoTable=new NanoTableScript { m_pNanoData=new NanoElement[4],m_pNanoTuneData=new NanoTuneElement[3] },
                    m_pAvatarTable=new AvatarTableScript { m_pAvatarGrowData=new AvatarGrowElement[2] } };
                for(int i=1;i<=3;i++) table.m_pNanoTable.m_pNanoData[i]=new NanoElement { m_iNanoNumber=i,m_iNanoSet=i,m_iNanoBattery1=150,m_iTune=new[]{1,2} };
                table.m_pNanoTable.m_pNanoTuneData[1]=new NanoTuneElement { m_iSkillID=11,m_iReqItemID=99,m_iReqItemCount=2 };
                table.m_pNanoTable.m_pNanoTuneData[2]=new NanoTuneElement { m_iSkillID=22,m_iReqItemID=99,m_iReqItemCount=2 };
                table.m_pAvatarTable.m_pAvatarGrowData[1]=new AvatarGrowElement { m_iReqBlob_NanoTune=100 };
                var inv=InventoryWindow.Ensure(host); inv.Init(null,null,table,hud,null);
                typeof(InventoryWindow).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inv,null);
                InventoryWindow.Machine=false; InventoryWindow.NanoOwned[1]=1; InventoryWindow.NanoOwned[2]=2; InventoryWindow.NanoOwned[3]=0; InventoryWindow.NanoSkill[1]=11;
                Array.Clear(InventoryWindow.Items,0,InventoryWindow.Items.Length); InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=2 };
                byte[] sent=null; int sends=0; NpcUI.Sender=p=>{sent=p;sends++;};
                var collection=inv.DualNanos(1,140,null);
                Check(collection.entries.Count==3 && collection.entries[0].owned && !collection.entries[2].owned,"Collection must use existing bank ownership, including locked Nanos.");
                Check(collection.selected==1 && collection.scroll==140 && collection.entries[0].energy==82 && collection.entries[0].powers[0].current,"Selection, energy and chosen skill must reflect existing state.");
                Check(collection.entries[0].skill==11&&collection.entries[0].powers.Count==1,"Outside Nano Station, show only the actual assigned power, never alternate tuning choices.");
                Check(collection.entries[2].skill==0&&string.IsNullOrEmpty(collection.entries[2].skillImage),"Locked Nanos must not display an invented assigned power.");
                foreach(string art in new[]{"HP_BAR","nano_st_bar","buff_icon_back","NanoPopupFMBar","NanoPopupItemBar"}) Check(UiKit.Tex(art)!=null,"Legacy Nano presentation asset missing: "+art);
                Check(collection.entries[0].energyKnown&&!collection.entries[1].energyKnown,"Missing bank energy must be shown as unavailable, not invented zero.");
                var wheel=host.AddComponent<NanoWheelHud>();
                Set(wheel,"hud",hud); hud.Data.Hp=0;
                Check(!wheel.CanSummonFromUi,"Native summon bar must retain the legacy death guard.");
                hud.Data.Hp=1;
                Check(wheel.CanSummonFromUi,"A live character with no blocking UI must retain summon controls.");
                int oldFocus=GUIUtility.keyboardControl;
                try { GUIUtility.keyboardControl=12345; Check(wheel.CanSummonFromUi,"Native touch must not inherit an obsolete IMGUI login field focus ID."); }
                finally { GUIUtility.keyboardControl=oldFocus; }
                Check(!inv.DualEquipNano(2,1)&&!inv.DualTuneNano(1,2)&&sends==0,"DS must not bypass the Nano Station requirement.");
                inv.OpenStation(1,"Regression Station");
                var readyPower=inv.DualNanos(1,0,null).entries.Find(n=>n.id==1).powers.Find(p=>!p.current);
                Check(readyPower.resourcesKnown&&readyPower.availableMatter==100&&readyPower.matter==100&&readyPower.items==2&&readyPower.requiredItems==2,"Resource gauges must expose live available and required amounts, including exact balance.");
                hud.Data.FusionMatter=50; InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=1 };
                var partialPower=inv.DualNanos(1,0,null).entries.Find(n=>n.id==1).powers.Find(p=>!p.current);
                Check(partialPower.availableMatter==50&&partialPower.matter==100&&partialPower.items==1&&partialPower.requiredItems==2&&!partialPower.canTune,"Resource updates must expose true partial progress for both gauges and block insufficient tuning.");
                hud.Data.FusionMatter=250; InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=4 };
                var surplusPower=inv.DualNanos(1,0,null).entries.Find(n=>n.id==1).powers.Find(p=>!p.current);
                Check(surplusPower.availableMatter==250&&surplusPower.items==4,"Resource counts must preserve surplus; only the visual fill is capped.");
                hud.Data.FusionMatter=100; InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=2 };
                Check(GameMenu.Blocking&&GameMenu.NanoStationOnly,"A docked station must still block movement/combat like the legacy station.");
                var fold=new DualScreenCapabilities { reliable=true,available=true,fold=true,flat=true,kind=1,density=2,primaryWidth=1800,primaryHeight=1000,secondaryWidth=1800,secondaryHeight=600 };
                Check(DualScreenPolicy.ShouldUse(DualScreenPreference.Automatic,fold,true,DualScreenController.SecondaryBlocked),"An open Nano Station must retain DS and render its original window on the primary surface.");
                Check(DualScreenController.SecondaryCommandAllowed("tab")&&DualScreenController.SecondaryCommandAllowed("nanoSelect")&&DualScreenController.SecondaryCommandAllowed("zoom"),"Station must keep secondary map/collection inspection available.");
                Check(DualScreenController.SecondaryCommandAllowed("nanoEquip")&&DualScreenController.SecondaryCommandAllowed("nanoUnequip")&&DualScreenController.SecondaryCommandAllowed("nanoTune"),"Station edits must be accepted from DS and share the original station validation/busy state.");
                Check(!DualScreenController.SecondaryCommandAllowed("nano")&&!DualScreenController.SecondaryCommandAllowed("window"),"World summon and unrelated windows must still respect station restrictions.");
                Check(!wheel.RequestSlotFromUi(0)&&sends==0,"Native summon must remain blocked inside a real station.");
                wheel.Init(hud,null,table); Set(wheel,"lastSummon",-1000f);
                hud.Data.NanoSlots[1]=2; hud.Data.NanoStamina[1]=150;
                Check(DualScreenController.SecondaryCommandAllowed("nanoCard")&&wheel.RequestSummonFromCard(1,2)&&sent.Length==6&&BitConverter.ToInt16(sent,4)==1,"An equipped card must summon its real slot even at the station, without enabling movement/combat.");
                Check(GameMenu.Blocking,"Card invocation must not unblock world input at the station.");
                typeof(NanoWheelHud).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(wheel,new object[]{PacketIds.P_FE2CL_REP_NANO_ACTIVE_SUCC,new byte[]{1,0,0,0,0,0,0,0}});
                Check(!wheel.RequestSummonFromCard(1,2)&&!wheel.RequestSummonFromCard(1,3)&&sends==1,"Repeated active-card taps must not dismiss the Nano; stale slot IDs must not summon a replacement.");
                hud.Data.ActiveNanoSlot=0; Set(inv,"machineBusy",true); Check(!wheel.RequestSummonFromCard(1,2),"A pending station edit must block concurrent card invocation."); Set(inv,"machineBusy",false);
                hud.Data.NanoSlots[1]=0; sends=0; sent=null; Set(wheel,"lastSummon",-1000f);
                Check(!inv.DualEquipNano(3,1)&&!inv.DualEquipNano(2,0)&&!inv.DualEquipNano(2,7),"Locked Nanos, active replacement slots and invalid slots must be rejected.");
                Check(inv.DualEquipNano(2,1)&&sent.Length==8&&BitConverter.ToInt16(sent,4)==2&&BitConverter.ToInt16(sent,6)==1,"Equip must use the existing packet and slot numbering.");
                Check(hud.Data.NanoSlots[1]==0&&!inv.DualEquipNano(2,1)&&sends==1,"Pending equip must not alter HUD optimistically or send twice.");
                Set(inv,"machineBusy",false); hud.Data.NanoSlots[1]=2;
                Check(inv.DualUnequipNano(2)&&sent.Length==6&&BitConverter.ToInt16(sent,4)==1,"Unequip must target the actual shared slot.");
                Set(inv,"machineBusy",false); hud.Data.FusionMatter=99;
                Check(!inv.DualTuneNano(1,2),"Insufficient Fusion Matter must reject tuning.");
                hud.Data.FusionMatter=100; InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=1 };
                Check(!inv.DualTuneNano(1,2),"Insufficient items must reject tuning.");
                InventoryWindow.Items[49]=new ItemBase { Type=7,Id=99,Opt=2 };
                Check(!inv.DualTuneNano(1,1)&&!inv.DualTuneNano(1,999),"Current and foreign powers must be rejected.");
                Check(inv.DualTuneNano(1,2)&&sent.Length==48&&BitConverter.ToInt16(sent,4)==1&&BitConverter.ToInt16(sent,6)==2&&BitConverter.ToInt32(sent,8)==49,"Tuning must reuse the original packet and real required item slot.");
                Check(InventoryWindow.NanoSkill[1]==11&&hud.Data.FusionMatter==100&&!inv.DualTuneNano(1,2),"Pending power change must await server state and reject duplicates.");
                var ack=new byte[]{1,0,22,0};
                typeof(InventoryWindow).GetMethod("NanoPacket",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inv,new object[]{PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,ack});
                typeof(InventoryWindow).GetMethod("NanoUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(inv,null);
                Check(InventoryWindow.NanoSkill[1]==22&&!inv.DualNanoBusy,"Existing server handler must update the same bank and release pending state.");
                var updated=inv.DualNanos(1,0,null).entries.Find(n=>n.id==1).powers;
                Check(updated.Count==2&&updated[0].current&&updated[0].skill==22&&updated[1].skill==11,"Current Power must move to the first row after a real tune acknowledgement while preserving the remaining powers.");
                inv.DualRestoreSelection(2); Check(inv.DualLegacySelection==2,"Folded station must retain selection in the existing full-screen popup.");
                GameMenu.CloseWindow();
                var outside=inv.DualNanos(1,0,null).entries.Find(n=>n.id==1);
                Check(outside.skill==22&&outside.powers.Count==1&&outside.powers[0].current&&outside.powers[0].skill==22,"Closing the station must hide alternate powers while preserving the acknowledged assignment.");
                Check(DualScreenPolicy.ShouldUse(DualScreenPreference.Automatic,fold,true,DualScreenController.SecondaryBlocked),"Closing the original station must permit DS again without reloading the session.");
                var controller=host.AddComponent<DualScreenController>();
                typeof(DualScreenController).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);
                var caps=new DualScreenCapabilities { active=true };
                typeof(DualScreenController).GetProperty("Capabilities").GetSetMethod(true).Invoke(controller,new object[]{caps});
                inv.OpenStation(1,"Shared upper/DS station");
                var handle=typeof(DualScreenController).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
                handle.Invoke(controller,new object[]{"{\"action\":\"tab\",\"value\":\"Nanos\",\"revision\":0}"});
                handle.Invoke(controller,new object[]{"{\"action\":\"nanoSelect\",\"value\":\"1\",\"revision\":0}"});
                var panel=JsonUtility.FromJson<PanelState>(WorldMapWindow.Ensure(host).DualSnapshot());
                Check(panel.station&&panel.collection!=null&&panel.collection.station&&panel.collection.selected==1,"The live panel snapshot must expose the shared station and selection instead of forcing inspection-only mode.");
                Check(panel.collection.entries.Find(n=>n.id==1).powers.Exists(p=>p.canTune),"Native power changes must receive the actual station resource eligibility.");
                TouchLayout.Enable(7,true); Check(!TouchControls.ActionVisible(7),"DS must hide duplicate Nano touch buttons.");
                caps.active=false; Check(TouchControls.ActionVisible(7),"Single Screen must restore the user's original Nano button preference.");
                using(var rgba=new NativeArray<byte>(new byte[]{255,0,0,255, 0,255,0,128, 0,0,255,255, 255,255,255,0},Allocator.Temp))
                {
                    var pixels=new int[4]; Check(DualNanoPreview.ConvertFrame(rgba,pixels,2,2)&&pixels[0]==unchecked((int)0xff0000ff)&&pixels[1]==0x00ffffff&&pixels[2]==unchecked((int)0xffff0000)&&pixels[3]==unchecked((int)0x8000ff00),"Preview transfer must retain alpha/channels and convert Unity rows to Android orientation.");
                    Check(!DualNanoPreview.ConvertFrame(rgba,pixels,3,1),"Malformed preview buffers must be rejected.");
                }
            }
            finally
            {
                GameMenu.CloseWindow(); if(host!=null) UnityEngine.Object.DestroyImmediate(host);
                Array.Copy(owned,InventoryWindow.NanoOwned,owned.Length);Array.Copy(skills,InventoryWindow.NanoSkill,skills.Length);Array.Copy(items,InventoryWindow.Items,items.Length);
                InventoryWindow.Machine=machine; NpcUI.Sender=sender;TouchLayout.Pop(); if(!registered)GameMenu.Implemented.Remove(GameMenu.Win.MyStuff);
            }
            File.WriteAllText("Logs/dual-nano-regression.txt","PASS: shared collection, locks, energy, powers and selection; station-only equipment/tuning; blocked combat; packet layouts; resources and item slots; no optimistic state or duplicate sends; server acknowledgement; folded selection and Single Screen control restoration.\n");
            Debug.Log("[DS Nanos] Shared-state/station regression checks passed.");
        }
    }
}
