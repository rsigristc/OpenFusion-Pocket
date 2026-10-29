using System;
using System.Collections.Generic;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class NanoLifecycleRegressionChecks
    {
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        static void S(byte[] b,int offset,int value)=>BitConverter.GetBytes((short)value).CopyTo(b,offset);
        static byte[] Tune(int id,int skill){var b=new byte[168];S(b,0,id);S(b,2,skill);return b;}
        public static void Run()
        {
            var host=new GameObject("Nano lifecycle fixtures");var sender=NpcUI.Sender;
            try
            {
                var hud=host.AddComponent<PlayerHud>();var d=hud.Data;d.Hp=100;d.NanoBankStamina=new short[37];
                d.NanoSlots=new short[]{1,3,4};d.ActiveNanoSlot=1;
                var tick=new byte[32];BitConverter.GetBytes(100).CopyTo(tick,0);S(tick,8,120);S(tick,14,-6);S(tick,20,140);
                Check(HudPackets.ApplyTick(tick,d)&&d.ActiveNanoSlot==-1&&d.NanoStamina[1]==0&&d.NanoBankStamina[3]==0,"An authoritative depleted tick must retire the active passive Nano and clamp its slot/bank energy to zero.");
                S(tick,14,12);HudPackets.ApplyTick(tick,d);
                Check(d.ActiveNanoSlot==-1&&d.NanoStamina[1]==12&&d.NanoSlots[1]==3,"Recharge must not automatically resummon a depleted Nano or overwrite equipped IDs.");
                d.ActiveNanoSlot=1;S(tick,8,-20);S(tick,14,100);HudPackets.ApplyTick(tick,d);
                Check(d.ActiveNanoSlot==1&&d.NanoStamina[0]==0,"Exhausting an inactive Nano must not dismiss a different active Nano.");
                Check(!HudPackets.ApplyTick(new byte[20],d)&&d.ActiveNanoSlot==1,"Truncated updates must not fabricate exhaustion.");
                var wheel=host.AddComponent<NanoWheelHud>();wheel.Init(hud,null,null);
                typeof(NanoWheelHud).GetField("selfId",Private).SetValue(wheel,77);
                var skill=new byte[36];BitConverter.GetBytes(77).CopyTo(skill,0);BitConverter.GetBytes(1).CopyTo(skill,20);S(skill,24,1);S(skill,26,-3);
                Call(wheel,"Handle",PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC,skill);
                Check(d.ActiveNanoSlot==1&&d.NanoStamina[0]==0,"A late depleted result for a previously active Nano must not dismiss its replacement.");
                S(skill,24,3);Call(wheel,"Handle",PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC,skill);
                Check(d.ActiveNanoSlot==-1&&d.NanoStamina[1]==0,"Active skill depletion must use the same nonnegative exhaustion state.");
                var packets=new List<byte[]>();NpcUI.Sender=p=>packets.Add(p);
                d.NanoSlots=new short[]{1,0,4};
                var award=new byte[28];S(award,20,2);S(award,24,150);Call(wheel,"Handle",PacketIds.P_FE2CL_REP_PC_NANO_CREATE_SUCC,award);
                Check(packets.Count==0,"Legacy does not auto-equip an untuned reward before its first power is confirmed.");
                Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(2,30));
                Check(packets.Count==1&&BitConverter.ToUInt32(packets[0],0)==PacketIds.P_CL2FE_REQ_NANO_EQUIP&&BitConverter.ToInt16(packets[0],4)==2&&BitConverter.ToInt16(packets[0],6)==1&&d.NanoSlots[1]==0,"First confirmed power must request the first empty slot without optimistic equipment changes.");
                Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(2,30));
                Check(packets.Count==1,"Duplicate first-power acknowledgements must not send repeated equipment requests.");
                var equipped=new byte[8];S(equipped,0,2);S(equipped,2,1);Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_EQUIP_SUCC,equipped);
                Check(d.NanoSlots[1]==2&&d.NanoStamina[1]==150,"Only equipment acknowledgement may fill the slot using the received bank energy.");
                Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(3,40));
                Check(packets.Count==1,"A full wheel must retain the new Nano in storage without replacing another Nano.");
                d.NanoSlots[1]=0;Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(2,31));
                Check(packets.Count==1,"Retuning an existing power must not auto-equip a stored Nano.");
                d.NanoSlots[2]=0;Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(5,50));Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,Tune(6,60));
                Check(packets.Count==3&&BitConverter.ToInt16(packets[1],6)==1&&BitConverter.ToInt16(packets[2],6)==2&&d.NanoSlots[1]==0&&d.NanoSlots[2]==0,"Concurrent initial acknowledgements must reserve distinct empty slots until the server confirms them.");
                wheel.Init(hud,null,null);var pending=(int[])typeof(NanoWheelHud).GetField("pendingInitialEquip",Private).GetValue(wheel);
                Check(Array.TrueForAll(pending,id=>id==0)&&wheel.BankSkill(5)==0,"Changing sessions must clear pending equipment and previous skill-bank state.");
                Call(wheel,"Handle",PacketIds.P_FE2CL_REP_NANO_TUNE_SUCC,new byte[3]);
                Check(packets.Count==3,"Malformed tuning data cannot trigger auto-equipment.");
                Debug.Log("[nano lifecycle regression] PASS: passive/active depletion, nonnegative slot/bank energy, inactive recharge, stale skill responses, Legacy first-power auto-equipment, full slots, duplicate and concurrent acknowledgements, session reset.");
            }
            finally{UnityEngine.Object.DestroyImmediate(host);NpcUI.Sender=sender;}
        }
    }
}
