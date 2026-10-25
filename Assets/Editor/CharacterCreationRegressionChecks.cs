using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class CharacterCreationRegressionChecks
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static byte[] Fail(uint type, int code) { var p=ClientPackets.New(type,4); ClientPackets.PutInt(p,4,code); return p; }
        static async Task Reject(Task pending, string stage, int code)
        {
            try { await pending; throw new Exception("Creation rejection was accepted"); }
            catch (LoginException e) { Check(e.Stage==stage && e.Code==code,"Creation rejection became a timeout or lost its stage/code"); }
        }
        public static async Task Run()
        {
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            try {
                var accepted=listener.AcceptTcpClientAsync();
                using(var login=await LoginSession.ConnectAsync("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port,new LoginOptions{Timeout=TimeSpan.FromSeconds(2)}))
                using(var peer=await accepted)
                using(var limit=new CancellationTokenSource(TimeSpan.FromSeconds(12))) {
                    var server=new FramedStream(peer.GetStream(),new PacketCrypto());
                    var writer=new FramedStream(peer.GetStream(),new PacketCrypto());
                    var partial = new CharInfo { Slot=2, Appearance=new PcAppearance { Uid=456, FirstName="Saved", LastName="Name" } };
                    Check(partial.NeedsCustomization, "A name-only character must resume customization instead of entering the shard");
                    var chars = (System.Collections.Generic.List<CharInfo>)typeof(LoginSession).GetField("chars",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(login);
                    chars.Add(partial);
                    Check(login.ReservedName(2)?.Uid==456,"A reconnect must reuse the server's existing unfinished character");
                    chars.Clear();
                    // The server can reject SAVE_NAME with CHECK_NAME_FAIL or SHARD_SELECT_FAIL.
                    // Each rejection must leave the connection usable for the following attempt.
                    var attempt=login.SaveNameAsync(4,1,2,3,4,"Test","Character",limit.Token);
                    await server.ReceiveAsync(limit.Token);
                    await writer.SendAsync(Fail(PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL,1),limit.Token);
                    await Reject(attempt,"check_name",1);
                    attempt=login.SaveNameAsync(4,1,2,3,4,"Test","Character",limit.Token);
                    await server.ReceiveAsync(limit.Token);
                    await writer.SendAsync(Fail(PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL,2),limit.Token);
                    await Reject(attempt,"save_slot",2);
                    attempt=login.SaveNameAsync(4,1,2,3,4,"Test","Character",limit.Token);
                    await server.ReceiveAsync(limit.Token);
                    var reserved=ClientPackets.New(PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_SUCC,64);
                    BitConverter.GetBytes(987L).CopyTo(reserved,4);reserved[12]=4;reserved[13]=1;
                    ClientPackets.PutStr(reserved,14,"Test",9);ClientPackets.PutStr(reserved,32,"Character",17);
                    await writer.SendAsync(reserved,limit.Token);
                    var name=await attempt;
                    Check(name.Slot==4 && login.ReservedName(4)?.Uid==987,"A cancelled customization must retain the reserved slot/UID for retry");
                    var style=new PlayerStyle{Gender=1,FaceStyle=2,HairStyle=4,HairColor=1,SkinColor=1,EyeColor=1,Height=2,Body=1};
                    var create=login.CreateCharacterAsync(name.Uid,style,11,22,33,2,3,4,ct:limit.Token);
                    var request=await server.ReceiveAsync(limit.Token);
                    var look=new PcAppearance();PcAppearance.ReadStyle(request,4,look);
                    Check(look.Uid==987 && look.FirstName=="Test" && look.LastName=="Character","CHAR_CREATE must retain the synthetic reserved identity; saved="+name.FirstName+"/"+name.LastName+" received="+look.Uid+"/"+look.FirstName+"/"+look.LastName);
                    await writer.SendAsync(Fail(PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL,2),limit.Token);
                    await Reject(create,"char_create",2);
                    Check(login.ReservedName(4)?.Uid==987 && login.Characters.Count==0,"Rejected creation must not consume the reservation or add a fake character");
                    create=login.CreateCharacterAsync(name.Uid,style,11,22,33,2,3,4,ct:limit.Token);
                    request=await server.ReceiveAsync(limit.Token);
                    var result=ClientPackets.New(PacketIds.P_LS2CL_REP_CHAR_CREATE_SUCC,100);
                    BitConverter.GetBytes((short)1).CopyTo(result,4);
                    Array.Copy(request,4,result,8,76); // response level + padding precede sPCStyle
                    BitConverter.GetBytes((short)11).CopyTo(result,90);
                    BitConverter.GetBytes((short)22).CopyTo(result,92);
                    BitConverter.GetBytes((short)33).CopyTo(result,94);
                    await writer.SendAsync(result,limit.Token);
                    var made=await create;
                    Check(!made.WorldPositionKnown && !made.NeedsCustomization && made.Slot==4 && made.Appearance.Uid==987 && login.Characters.Count==1 && login.ReservedName(4)==null,"Successful creation must update the correct slot exactly once");
                    Check(made.Appearance.Equip[1].Type==1 && made.Appearance.Equip[1].Id==11 && made.Appearance.Equip[3].Type==3 && made.Appearance.Equip[3].Id==33,"Created-character previews require typed clothing IDs");
                }
            } finally {listener.Stop();}
            Debug.Log("[character creation regression] alternate name/slot/create rejections, reusable connection, reserved identity and completed slot/equipment passed.");
        }
    }
}
