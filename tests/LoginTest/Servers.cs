using System; using System.IO; using System.Linq; using System.Text; using System.Collections.Generic; using System.Net; using System.Net.Sockets; using System.Runtime.InteropServices; using System.Threading; using System.Threading.Tasks; using OpenFusion.Net; using OpenFusion.World;
public sealed class Peer
{
    public readonly PacketCrypto Rx = new PacketCrypto(), Tx = new PacketCrypto(); readonly FramedStream rd, wr; public readonly TcpClient C;
    public Peer(TcpClient c) { C = c; rd = new FramedStream(c.GetStream(), Rx); wr = new FramedStream(c.GetStream(), Tx); }
    public async Task<(uint t, byte[] b)> Read() { var p = await rd.ReceiveAsync(); return (PacketIds.Unstamp(BitConverter.ToUInt32(p, 0)), p.Skip(4).ToArray()); }
    public Task Send(uint type, byte[] body) => wr.SendAsync(BitConverter.GetBytes(type).Concat(body).ToArray());
}
public sealed class FakeLs : IDisposable
{
    public string Mode = "normal"; public bool DupLoggedIn; public bool LiveCheckFirst; public int FePort; public ulong FEKey; public readonly List<string> Log = new List<string>();
    public sP_CL2LS_REQ_LOGIN LastLogin; public sbyte LastShard = -1;
    readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0); public int Port => ((IPEndPoint)l.LocalEndpoint).Port;
    public FakeLs() { l.Start(); var _ = Task.Run(async () => { while (true) { TcpClient c; try { c = await l.AcceptTcpClientAsync(); } catch { return; } var __ = Task.Run(() => Serve(c)); } }); }
    static T From<T>(byte[] b) where T : struct { var c = new byte[Math.Max(b.Length, Marshal.SizeOf<T>())]; Buffer.BlockCopy(b, 0, c, 0, b.Length); var h = GCHandle.Alloc(c, GCHandleType.Pinned); try { return Marshal.PtrToStructure<T>(h.AddrOfPinnedObject()); } finally { h.Free(); } }
    static byte[] Bytes<T>(T v) where T : struct { var b = new byte[Marshal.SizeOf<T>()]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); try { Marshal.StructureToPtr(v, h.AddrOfPinnedObject(), false); } finally { h.Free(); } return b; }
    async Task Serve(TcpClient c)
    {
        var p = new Peer(c);
        try
        {
                        while (true)
            {
                var (t, b) = await p.Read();
                switch (t)
                {
                    case PacketIds.P_CL2LS_REQ_LOGIN:
                    {
                        var rq = From<sP_CL2LS_REQ_LOGIN>(b); LastLogin = rq; Log.Add("login:" + rq.iLoginType);
                        if (Mode == "silent") break; if (Mode == "drop") { c.Close(); return; }
                        string id = rq.iLoginType == 2 ? "cookie_" + Encoding.ASCII.GetString(rq.szCookie_TEGid).TrimEnd('\0') : rq.szID;
                        if (rq.iLoginType == 1 && rq.szPassword != "pw") { await p.Send(PacketIds.P_LS2CL_REP_LOGIN_FAIL, Bytes(new sP_LS2CL_REP_LOGIN_FAIL { iErrorCode = 2, szID = id })); break; }
                        if (id == "dup" && DupLoggedIn) { await p.Send(PacketIds.P_LS2CL_REP_LOGIN_FAIL, Bytes(new sP_LS2CL_REP_LOGIN_FAIL { iErrorCode = 3, szID = id })); break; }
                        if (id == "dup") DupLoggedIn = true;
                        ulong svrTime = 123456789UL; int chars = id == "newbie" ? 0 : 2; int slots = 3;
                        await p.Send(PacketIds.P_LS2CL_REP_LOGIN_SUCC, Bytes(new sP_LS2CL_REP_LOGIN_SUCC { iCharCount = (sbyte)chars, iSlotNum = (sbyte)slots, uiSvrTime = svrTime, szID = id }));
                        // el servidor real cambia las claves JUSTO después de enviar LOGIN_SUCC: lo que sigue ya va con la clave nueva
                        var k = PacketCrypto.Create64BitKey(svrTime, chars + 1, slots + 1); p.Rx.EKey = k; p.Tx.EKey = k; FEKey = PacketCrypto.Create64BitKey(PacketCrypto.DefaultKey, rq.iClientVerC, 1);
                        for (int i = 0; i < chars; i++)
                        {
                            var eq = new sItemBase[9]; eq[4] = new sItemBase { iID = (short)(300 + i) };
                            await p.Send(PacketIds.P_LS2CL_REP_CHAR_INFO, Bytes(new sP_LS2CL_REP_CHAR_INFO { iSlot = (sbyte)(i + 1), iLevel = (short)(30 + i), sPC_Style = new sPCStyle { iPC_UID = 1000 + i, szFirstName = "Ro" + i, szLastName = "S", iGender = (sbyte)(1 + i), iFaceStyle = 2, iHairStyle = 3 }, iX = 100 * (i + 1), aEquip = eq }));
                        }
                        if (LiveCheckFirst) await p.Send(PacketIds.P_LS2CL_REQ_LIVE_CHECK, BitConverter.GetBytes(1));   // con la clave nueva, ya autenticados
                        break;
                    }
                    case PacketIds.P_CL2LS_REP_LIVE_CHECK: Log.Add("live:" + PacketIds.Name(t)); break;
                    case PacketIds.P_CL2LS_REQ_PC_EXIT_DUPLICATE: DupLoggedIn = false; Log.Add("exitdup:" + From<sP_CL2LS_REQ_PC_EXIT_DUPLICATE>(b).szID); break;
                    case PacketIds.P_CL2LS_REQ_CHAR_SELECT:
                    {
                        long uid = From<sP_CL2LS_REQ_CHAR_SELECT>(b).iPC_UID;
                        if (uid == 1000 || uid == 1001 || uid == 5555) await p.Send(PacketIds.P_LS2CL_REP_CHAR_SELECT_SUCC, new byte[0]);
                        else await p.Send(PacketIds.P_LS2CL_REP_CHAR_SELECT_FAIL, BitConverter.GetBytes(1));
                        break;
                    }
                    case PacketIds.P_CL2LS_REQ_SHARD_SELECT:
                        LastShard = (sbyte)b[0];
                        if (Mode == "shardfail") await p.Send(PacketIds.P_LS2CL_REP_SHARD_SELECT_FAIL, BitConverter.GetBytes(2));
                        else await p.Send(PacketIds.P_LS2CL_REP_SHARD_SELECT_SUCC, Bytes(new sP_LS2CL_REP_SHARD_SELECT_SUCC { g_FE_ServerIP = Encoding.ASCII.GetBytes("127.0.0.1").Concat(new byte[7]).ToArray(), g_FE_ServerPort = FePort, iEnterSerialKey = 0x1122334455667788L }));
                        break;
                    case PacketIds.P_CL2LS_REQ_CHECK_CHAR_NAME:
                    {
                        var q = From<sP_CL2LS_REQ_CHECK_CHAR_NAME>(b);
                        if (q.szFirstName.StartsWith("Bad")) await p.Send(PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_FAIL, BitConverter.GetBytes(4));
                        else await p.Send(PacketIds.P_LS2CL_REP_CHECK_CHAR_NAME_SUCC, Bytes(new sP_LS2CL_REP_CHECK_CHAR_NAME_SUCC { szFirstName = q.szFirstName, szLastName = q.szLastName }));
                        break;
                    }
                    case PacketIds.P_CL2LS_REQ_SAVE_CHAR_NAME:
                    { var q = From<sP_CL2LS_REQ_SAVE_CHAR_NAME>(b); await p.Send(PacketIds.P_LS2CL_REP_SAVE_CHAR_NAME_SUCC, Bytes(new sP_LS2CL_REP_SAVE_CHAR_NAME_SUCC { iPC_UID = 5555, iSlotNum = q.iSlotNum, iGender = q.iGender, szFirstName = q.szFirstName, szLastName = q.szLastName })); break; }
                    case PacketIds.P_CL2LS_REQ_CHAR_CREATE:
                    {
                        var q = From<sP_CL2LS_REQ_CHAR_CREATE>(b);
                        if (q.PCStyle.iPC_UID != 5555) await p.Send(PacketIds.P_LS2CL_REP_CHAR_CREATE_FAIL, BitConverter.GetBytes(1));
                        else await p.Send(PacketIds.P_LS2CL_REP_CHAR_CREATE_SUCC, Bytes(new sP_LS2CL_REP_CHAR_CREATE_SUCC { iLevel = 1, sPC_Style = q.PCStyle, sOn_Item = q.sOn_Item }));
                        break;
                    }
                    default: Log.Add("?" + PacketIds.Name(t)); break;
                }
            }
        }
        catch { }
    }
    public void Dispose() { try { l.Stop(); } catch { } }
}
public sealed class FakeFe : IDisposable
{
    public FakeLs Ls; public string Mode = "normal"; public readonly List<string> Log = new List<string>(); public byte[] LastMove; public ManualResetEventSlim MoveSeen = new ManualResetEventSlim(); public ManualResetEventSlim LiveReplied = new ManualResetEventSlim();
    readonly TcpListener l = new TcpListener(IPAddress.Loopback, 0); public int Port => ((IPEndPoint)l.LocalEndpoint).Port;
    public FakeFe(FakeLs ls) { Ls = ls; l.Start(); ls.FePort = Port; var _ = Task.Run(async () => { while (true) { TcpClient c; try { c = await l.AcceptTcpClientAsync(); } catch { return; } var __ = Task.Run(() => Serve(c)); } }); }
    static byte[] Bytes<T>(T v) where T : struct { var b = new byte[Marshal.SizeOf<T>()]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); try { Marshal.StructureToPtr(v, h.AddrOfPinnedObject(), false); } finally { h.Free(); } return b; }
    static T From<T>(byte[] b) where T : struct { var c = new byte[Math.Max(b.Length, Marshal.SizeOf<T>())]; Buffer.BlockCopy(b, 0, c, 0, b.Length); var h = GCHandle.Alloc(c, GCHandleType.Pinned); try { return Marshal.PtrToStructure<T>(h.AddrOfPinnedObject()); } finally { h.Free(); } }
    async Task Serve(TcpClient c)
    {
        var p = new Peer(c);
        try
        {
            p.Tx.EKey = Ls.FEKey;   // el shard cifra hacia el cliente con la FEKey de la sesión de login; el cliente envía con la clave por defecto hasta ENTER_SUCC
            var (t, b) = await p.Read(); var q = From<sP_CL2FE_REQ_PC_ENTER>(b); Log.Add("enter:" + q.szID + ":" + q.iEnterSerialKey.ToString("X"));
            if (Mode == "silent") { await Task.Delay(5000); return; }
            if (t != PacketIds.P_CL2FE_REQ_PC_ENTER || q.szID != "ro" || q.iEnterSerialKey != 0x1122334455667788L || Mode == "enterfail") { await p.Send(PacketIds.P_FE2CL_REP_PC_ENTER_FAIL, BitConverter.GetBytes(3)); return; }
            ulong svrTime = 999; var ld = new sPCLoadData2CL { PCStyle = new sPCStyle { iPC_UID = 1000, szFirstName = "Ro0", iGender = 1 }, iLevel = 30, iFusionMatter = 7, aEquip = new sItemBase[9] };
            p.Rx.EKey = PacketCrypto.Create64BitKey(svrTime, 42 + 1, 7 + 1);   // a partir de aquí el cliente envía con la clave de sesión
            await p.Send(PacketIds.P_FE2CL_REP_PC_ENTER_SUCC, Bytes(new sP_FE2CL_REP_PC_ENTER_SUCC { iID = 42, PCLoadData2CL = ld, uiSvrTime = svrTime }));
            // PC_AROUND pegado a ENTER_SUCC: no debe perderse
            var other = new sPCAppearanceData { iID = 43, PCStyle = new sPCStyle { iPC_UID = 2000, szFirstName = "Otro", iGender = 2 }, ItemEquip = new sItemBase[9] };
            await p.Send(PacketIds.P_FE2CL_PC_AROUND, BitConverter.GetBytes(1).Concat(Bytes(other)).ToArray());
            await p.Send(PacketIds.P_FE2CL_REQ_LIVE_CHECK, BitConverter.GetBytes(77));
            while (true)
            {
                var (t2, b2) = await p.Read();
                if (t2 == PacketIds.P_CL2FE_REP_LIVE_CHECK) { Log.Add("live:" + From<sP_CL2FE_REP_LIVE_CHECK>(b2).iTempValue); LiveReplied.Set(); }
                else if (t2 == PacketIds.P_CL2FE_REQ_PC_MOVE) { LastMove = b2; MoveSeen.Set(); }
                else Log.Add("?" + PacketIds.Name(t2));
            }
        }
        catch { }
    }
    public void Dispose() { try { l.Stop(); } catch { } }
}

