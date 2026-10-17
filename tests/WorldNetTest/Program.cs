using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Runtime.InteropServices; using System.Threading.Tasks; using OpenFusion.Net;
int fail = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK   " : "FALLA ") + m); if (!ok) fail++; }
byte[] Bytes<T>(T v) where T : struct { var b = new byte[Marshal.SizeOf<T>()]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); try { Marshal.StructureToPtr(v, h.AddrOfPinnedObject(), false); } finally { h.Free(); } return b; }
byte[] Pkt(uint type, params byte[][] parts) { var l = new List<byte>(BitConverter.GetBytes(type)); foreach (var p in parts) l.AddRange(p); return l.ToArray(); }
// tamaños declarados
Check(Marshal.SizeOf<sPCAppearanceData>() == PcAppearance.AppearanceSize && Marshal.SizeOf<sNPCAppearanceData>() == NpcAppearance.Size && Marshal.SizeOf<sP_FE2CL_PC_MOVE>() == WorldPackets.PcMoveSize && Marshal.SizeOf<sP_FE2CL_PC_STOP>() == WorldPackets.PcStopSize && Marshal.SizeOf<sP_FE2CL_PC_JUMP>() == WorldPackets.PcJumpSize && Marshal.SizeOf<sP_FE2CL_NPC_MOVE>() == WorldPackets.NpcMoveSize && Marshal.SizeOf<sP_FE2CL_REP_PC_ENTER_SUCC>() == WorldPackets.EnterSuccSize, "tamaños de estructura = constantes del lector");
Check((int)Marshal.OffsetOf<sP_FE2CL_REP_PC_ENTER_SUCC>("uiSvrTime") == 2556 && (int)Marshal.OffsetOf<sPCLoadData2CL>("iFusionMatter") == 108, "ENTER_SUCC: uiSvrTime en 2556, iFusionMatter en 108");
// constantes de paquete: coinciden con las del cliente
Check(PacketIds.P_FE2CL_PC_NEW == 822083587u && PacketIds.P_FE2CL_NPC_AROUND == 822083599u && PacketIds.Name(822083592u) == "P_FE2CL_PC_MOVE" && PacketIds.Name(5u).StartsWith("0x"), "PacketIds (520 constantes) y Name()");

var d = new PacketDispatcher(); var log = new List<string>();
d.SelfEnter += e => log.Add($"self {e.Id} {e.Self.FirstName} lv{e.Self.Level} fm{e.Self.FusionMatter} t{e.SvrTime}");
d.PcEnter += a => log.Add($"pc+ {a.Id} {a.Style.Gender} {a.Equip[4].Id}"); d.PcExit += i => log.Add("pc- " + i);
d.PcMove += m => log.Add($"mv {m.Id} {m.X},{m.Y},{m.Z} v{m.VX},{m.VY},{m.VZ} a{m.Angle} k{m.Key} s{m.Speed} t{m.SvrTime}"); d.PcStop += s => log.Add($"st {s.Id} {s.X} {s.SvrTime}"); d.PcJump += j => log.Add($"jp {j.Id} {j.VZ} k{j.Key}");
d.NpcEnter += n => log.Add($"npc+ {n.Id} type{n.Type} hp{n.Hp} {n.X},{n.Y},{n.Z} a{n.Angle} b{n.BarkerType}"); d.NpcExit += i => log.Add("npc- " + i); d.NpcMove += m => log.Add($"nmv {m.Id} {m.ToX},{m.ToY},{m.ToZ} s{m.Speed} st{m.Style}");
d.Teleport += (x, y, z) => log.Add($"tp {x},{y},{z}"); d.Unhandled += (t, b) => log.Add("??? " + PacketIds.Name(t) + " " + b.Length);
string Run(byte[] p) { log.Clear(); d.Dispatch(p); return string.Join(" | ", log); }

var st = new sPCStyle { iPC_UID = 77, iNameCheck = 1, szFirstName = "Ro", szLastName = "S", iGender = 2 };
var eq = new sItemBase[9]; eq[4] = new sItemBase { iID = 321 };
var ap = new sPCAppearanceData { iID = 42, PCStyle = st, ItemEquip = eq };
Check(Run(Pkt(PacketIds.P_FE2CL_PC_NEW, Bytes(new sP_FE2CL_PC_NEW { PCAppearanceData = ap }))) == "pc+ 42 2 321", "PC_NEW");
var ap2 = ap; ap2.iID = 43;
Check(Run(Pkt(PacketIds.P_FE2CL_PC_AROUND, BitConverter.GetBytes(2), Bytes(ap), Bytes(ap2))) == "pc+ 42 2 321 | pc+ 43 2 321", "PC_AROUND: lista de 2 (cabecera 4 B + elementos)");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_AROUND, BitConverter.GetBytes(0))) == "", "PC_AROUND vacío");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_EXIT, Bytes(new sP_FE2CL_PC_EXIT { iID = 9, iExitType = 1 }))) == "pc- 9", "PC_EXIT");
Check(Run(Pkt(PacketIds.P_FE2CL_AROUND_DEL_PC, BitConverter.GetBytes(3), BitConverter.GetBytes(5), BitConverter.GetBytes(6), BitConverter.GetBytes(7))) == "pc- 5 | pc- 6 | pc- 7", "AROUND_DEL_PC");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_MOVE, Bytes(new sP_FE2CL_PC_MOVE { iCliTime = 1, iX = 10, iY = 20, iZ = 30, fVX = 1.5f, fVY = -2f, fVZ = 0.25f, iAngle = 90, cKeyValue = 3, iSpeed = 600, iID = 8, iSvrTime = 99 }))) == "mv 8 10,20,30 v1.5,-2,0.25 a90 k3 s600 t99", "PC_MOVE");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_STOP, Bytes(new sP_FE2CL_PC_STOP { iX = 4, iY = 5, iZ = 6, iID = 8, iSvrTime = 100 }))) == "st 8 4 100", "PC_STOP");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_JUMP, Bytes(new sP_FE2CL_PC_JUMP { iVX = 1, iVY = 2, iVZ = 300, cKeyValue = 51, iID = 8 }))) == "jp 8 300 k51", "PC_JUMP");
var npc = new sNPCAppearanceData { iNPC_ID = 1001, iNPCType = 2, iHP = 555, iX = 1, iY = 2, iZ = 3, iAngle = 270, iBarkerType = 4 };
Check(Run(Pkt(PacketIds.P_FE2CL_NPC_ENTER, Bytes(new sP_FE2CL_NPC_ENTER { NPCAppearanceData = npc }))) == "npc+ 1001 type2 hp555 1,2,3 a270 b4", "NPC_ENTER");
Check(Run(Pkt(PacketIds.P_FE2CL_NPC_NEW, Bytes(new sP_FE2CL_NPC_NEW { NPCAppearanceData = npc }))) == "npc+ 1001 type2 hp555 1,2,3 a270 b4", "NPC_NEW");
var npc2 = npc; npc2.iNPC_ID = 1002;
Check(Run(Pkt(PacketIds.P_FE2CL_NPC_AROUND, BitConverter.GetBytes(2), Bytes(npc), Bytes(npc2))).Split('|').Length == 2, "NPC_AROUND: 2 NPC");
Check(Run(Pkt(PacketIds.P_FE2CL_NPC_EXIT, BitConverter.GetBytes(1001))) == "npc- 1001" && Run(Pkt(PacketIds.P_FE2CL_AROUND_DEL_NPC, BitConverter.GetBytes(2), BitConverter.GetBytes(1), BitConverter.GetBytes(2))) == "npc- 1 | npc- 2", "NPC_EXIT y AROUND_DEL_NPC");
Check(Run(Pkt(PacketIds.P_FE2CL_NPC_MOVE, Bytes(new sP_FE2CL_NPC_MOVE { iNPC_ID = 1001, iToX = 7, iToY = 8, iToZ = 9, iSpeed = 300, iMoveStyle = 1 }))) == "nmv 1001 7,8,9 s300 st1", "NPC_MOVE");
Check(Run(Pkt(PacketIds.P_FE2CL_REP_PC_GOTO_SUCC, Bytes(new sP_FE2CL_REP_PC_GOTO_SUCC { iX = 100, iY = 200, iZ = 300 }))) == "tp 100,200,300", "PC_GOTO_SUCC");
var ld = new sPCLoadData2CL { PCStyle = st, iLevel = 12, iFusionMatter = 345, aEquip = new sItemBase[9] };
Check(Run(Pkt(PacketIds.P_FE2CL_REP_PC_ENTER_SUCC, Bytes(new sP_FE2CL_REP_PC_ENTER_SUCC { iID = 5, PCLoadData2CL = ld, uiSvrTime = 123456789 }))) == "self 5 Ro lv12 fm345 t123456789", "REP_PC_ENTER_SUCC");
Check(Run(Pkt(PacketIds.P_FE2CL_PC_ATTACK_NPCs, new byte[8])).StartsWith("???"), "paquete conocido sin manejador -> Unhandled");
// checksum estampado en el tipo: el despachador lo ignora
uint stamped = PacketCrypto.StampType(PacketIds.P_FE2CL_PC_EXIT, 0xABC);
Check(PacketIds.Unstamp(stamped) == PacketIds.P_FE2CL_PC_EXIT && Run(Pkt(stamped, BitConverter.GetBytes(11), BitConverter.GetBytes(0))) == "pc- 11", "tipo con checksum estampado");
// robustez: contadores falsos, paquetes cortos y basura no lanzan excepciones al llamador
int malformed = 0; d.Malformed += (t, e) => malformed++;
d.Dispatch(Pkt(PacketIds.P_FE2CL_PC_AROUND, BitConverter.GetBytes(1000000), Bytes(ap))); d.Dispatch(Pkt(PacketIds.P_FE2CL_PC_AROUND, BitConverter.GetBytes(-1))); d.Dispatch(Pkt(PacketIds.P_FE2CL_PC_MOVE, new byte[10])); d.Dispatch(new byte[2]); d.Dispatch(null);
Check(malformed == 3, "contador falso/negativo y paquete corto -> Malformed (3), sin excepción");
var rnd = new Random(1); var types = new[] { PacketIds.P_FE2CL_PC_NEW, PacketIds.P_FE2CL_PC_AROUND, PacketIds.P_FE2CL_NPC_AROUND, PacketIds.P_FE2CL_AROUND_DEL_PC, PacketIds.P_FE2CL_AROUND_DEL_NPC, PacketIds.P_FE2CL_PC_MOVE, PacketIds.P_FE2CL_NPC_MOVE, PacketIds.P_FE2CL_REP_PC_ENTER_SUCC, PacketIds.P_FE2CL_PC_EXIT, PacketIds.P_FE2CL_REP_PC_GOTO_SUCC };
bool threw = false; for (int i = 0; i < 20000; i++) { var b = new byte[rnd.Next(0, 3000)]; rnd.NextBytes(b); if (rnd.Next(3) == 0 && b.Length >= 4) for (int k = 4; k < Math.Min(8, b.Length); k++) b[k] = (byte)rnd.Next(0, 4); try { d.Dispatch(Pkt(types[rnd.Next(types.Length)], b)); } catch { threw = true; } }
Check(!threw, "20 000 paquetes aleatorios de tipos conocidos: ninguna excepción");

// extremo a extremo por la capa de tramas con cifrado: servidor -> FramedStream -> despachador
var ms = new MemoryStream(); var tx = new FramedStream(ms, new PacketCrypto());
tx.SendAsync(Pkt(PacketIds.P_FE2CL_PC_MOVE, Bytes(new sP_FE2CL_PC_MOVE { iX = 1, iY = 2, iZ = 3, iID = 66 }))).Wait(); tx.SendAsync(Pkt(PacketIds.P_FE2CL_NPC_EXIT, BitConverter.GetBytes(77))).Wait();
ms.Position = 0; var rx = new FramedStream(ms, new PacketCrypto()); log.Clear();
d.Dispatch(rx.ReceiveAsync().Result); d.Dispatch(rx.ReceiveAsync().Result);
Check(log.Count == 2 && log[0].StartsWith("mv 66 1,2,3") && log[1] == "npc- 77", "FramedStream cifrado/estampado -> despachador");

// --- GameSession por TCP real (loopback): clave S2C = FEKey del cliente = EKey del servidor; ENTER_SUCC cambia la EKey de envío del cliente
{
    ulong K = PacketCrypto.Create64BitKey(0x1234, 5, 6);
    var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0); l.Start(); int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
    var cc = new PacketCrypto { FEKey = K, GameServer = true }; var sess = GameSession.ConnectAsync("127.0.0.1", port, cc).Result; var srvSock = l.AcceptTcpClient();
    var srv = new FramedStream(srvSock.GetStream(), new PacketCrypto { EKey = K });
    var got = new List<string>(); sess.Dispatcher.PcExit += i => got.Add("pc-" + i); sess.Dispatcher.NpcExit += i => got.Add("npc-" + i); Exception disc = new Exception("sin cierre"); bool closed = false; sess.Disconnected += e => { disc = e; closed = true; };
    var run = sess.RunAsync();
    var ld2 = new sPCLoadData2CL { PCStyle = st, iFusionMatter = 345, aEquip = new sItemBase[9] };
    srv.SendAsync(Pkt(PacketIds.P_FE2CL_REP_PC_ENTER_SUCC, Bytes(new sP_FE2CL_REP_PC_ENTER_SUCC { iID = 5, PCLoadData2CL = ld2, uiSvrTime = 777 }))).Wait();
    srv.SendAsync(Pkt(PacketIds.P_FE2CL_PC_EXIT, Bytes(new sP_FE2CL_PC_EXIT { iID = 1 }))).Wait(); srv.SendAsync(Pkt(PacketIds.P_FE2CL_NPC_EXIT, BitConverter.GetBytes(2))).Wait();
    for (int i = 0; i < 100 && got.Count < 2; i++) System.Threading.Thread.Sleep(20);
    Check(got.Count == 2 && got[0] == "pc-1" && got[1] == "npc-2", "GameSession: 3 paquetes cifrados por TCP real llegan al despachador");
    Check(cc.EKey == PacketCrypto.Create64BitKey(777, 5 + 1, 345 + 1) && cc.EKey != PacketCrypto.DefaultKey, "ENTER_SUCC fija EKey = Create64BitKey(uiSvrTime, iID+1, iFusionMatter+1)");
    srvSock.Close(); run.Wait(3000); Check(closed && disc == null || closed && disc is System.IO.IOException, "cierre del servidor -> Disconnected (sin excepción propagada)");
    l.Stop(); sess.Dispose();
}
return fail;
