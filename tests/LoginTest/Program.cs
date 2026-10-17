using System; using System.IO; using System.Linq; using System.Text; using System.Collections.Generic; using System.Net; using System.Net.Sockets; using System.Runtime.InteropServices; using System.Threading; using System.Threading.Tasks; using OpenFusion.Net; using OpenFusion.World;

int fail = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK   " : "FALLA ") + m); if (!ok) fail++; }
byte[] Bytes<T>(T v) where T : struct { var b = new byte[Marshal.SizeOf<T>()]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); try { Marshal.StructureToPtr(v, h.AddrOfPinnedObject(), false); } finally { h.Free(); } return b; }
T From<T>(byte[] b) where T : struct { var c = new byte[Math.Max(b.Length, Marshal.SizeOf<T>())]; Buffer.BlockCopy(b, 0, c, 0, b.Length); var h = GCHandle.Alloc(c, GCHandleType.Pinned); try { return Marshal.PtrToStructure<T>(h.AddrOfPinnedObject()); } finally { h.Free(); } }
byte[] Body(byte[] p) => p.Skip(4).ToArray(); uint Type(byte[] p) => BitConverter.ToUInt32(p, 0);
bool Same(byte[] p, uint type, byte[] body) => Type(p) == type && Body(p).SequenceEqual(body);
byte[] ZB(string s, int n) { var b = new byte[n]; Encoding.ASCII.GetBytes(s).CopyTo(b, 0); return b; }

// ================= 1. disposición de bytes: cada paquete que enviamos == la estructura real del cliente, byte a byte =================
Check(Same(ClientPackets.LoginPassword("ro", "pw"), 301989889u, Bytes(new sP_CL2LS_REQ_LOGIN { szID = "ro", szPassword = "pw", iClientVerA = 1, iClientVerB = 0, iClientVerC = 44, iLoginType = 1, szCookie_TEGid = new byte[64], szCookie_authid = new byte[255] })), "REQ_LOGIN usuario/clave (468 B, tipo 301989889)");
Check(Same(ClientPackets.LoginCookie("teg", "authcookie", 1, 0, 45), 301989889u, Bytes(new sP_CL2LS_REQ_LOGIN { szID = " ", szPassword = " ", iClientVerA = 1, iClientVerB = 0, iClientVerC = 45, iLoginType = 2, szCookie_TEGid = ZB("teg", 64), szCookie_authid = ZB("authcookie", 255) })), "REQ_LOGIN con cookie (tipo 2)");
Check(Same(ClientPackets.CharSelect(123456789012L), 301989890u + 0, Bytes(new sP_CL2LS_REQ_CHAR_SELECT { iPC_UID = 123456789012L })) || Type(ClientPackets.CharSelect(1)) == PacketIds.P_CL2LS_REQ_CHAR_SELECT && Body(ClientPackets.CharSelect(123456789012L)).SequenceEqual(Bytes(new sP_CL2LS_REQ_CHAR_SELECT { iPC_UID = 123456789012L })), "REQ_CHAR_SELECT");
Check(Body(ClientPackets.ShardSelect(5)).SequenceEqual(Bytes(new sP_CL2LS_REQ_SHARD_SELECT { ShardNum = 5 })) && Type(ClientPackets.ShardSelect(5)) == 301989895u, "REQ_SHARD_SELECT (1 B, tipo 301989895)");
Check(Body(ClientPackets.LiveCheckLs(9)).SequenceEqual(Bytes(new sP_CL2LS_REP_LIVE_CHECK { iTempValue = 9 })) && Type(ClientPackets.LiveCheckLs()) == 301989900u && Body(ClientPackets.LiveCheckFe(9)).SequenceEqual(Bytes(new sP_CL2FE_REP_LIVE_CHECK { iTempValue = 9 })), "REP_LIVE_CHECK (LS y FE)");
Check(Body(ClientPackets.ExitDuplicate("ro", "")).SequenceEqual(Bytes(new sP_CL2LS_REQ_PC_EXIT_DUPLICATE { szID = "ro" })) && Type(ClientPackets.ExitDuplicate("ro", "")) == 301989899u, "REQ_PC_EXIT_DUPLICATE (132 B, Pack 2, tipo 301989899)");
Check(Body(ClientPackets.CheckCharName(1, 2, 3, "Ab", "Cd")).SequenceEqual(Bytes(new sP_CL2LS_REQ_CHECK_CHAR_NAME { iFNCode = 1, iLNCode = 2, iMNCode = 3, szFirstName = "Ab", szLastName = "Cd" })), "REQ_CHECK_CHAR_NAME");
Check(Body(ClientPackets.SaveCharName(2, 1, 4, 5, 6, "Ab", "Cd")).SequenceEqual(Bytes(new sP_CL2LS_REQ_SAVE_CHAR_NAME { iSlotNum = 2, iGender = 1, iFNCode = 4, iLNCode = 5, iMNCode = 6, szFirstName = "Ab", szLastName = "Cd" })), "REQ_SAVE_CHAR_NAME");
Check(Body(ClientPackets.SaveCharTutor(77, 7)).Take(12).SequenceEqual(Bytes(new sP_CL2LS_REQ_SAVE_CHAR_TUTOR { iPC_UID = 77, iTutorialFlag = 7 })), "REQ_SAVE_CHAR_TUTOR (12 B)");
var cst = new PlayerStyle { Gender = 2, FaceStyle = 3, HairStyle = 4, HairColor = 5, SkinColor = 6, EyeColor = 7, Height = 1, Body = 2 };
Check(Body(ClientPackets.CharCreate(99, cst, 11, 22, 33, 1, 2, 3)).SequenceEqual(Bytes(new sP_CL2LS_REQ_CHAR_CREATE
{
    PCStyle = new sPCStyle { iPC_UID = 99, iGender = 2, iFaceStyle = 3, iHairStyle = 4, iHairColor = 5, iSkinColor = 6, iEyeColor = 7, iHeight = 1, iBody = 2 },
    sOn_Item = new sOnItem { iEquipUBID = 11, iEquipLBID = 22, iEquipFootID = 33 }, sOn_Item_Index = new sOnItem_Index { iEquipUBID_index = 1, iEquipLBID_index = 2, iEquipFootID_index = 3, iFaceStyle = 3, iHairStyle = 4 }
})), "REQ_CHAR_CREATE (100 B: sPCStyle + sOnItem + sOnItem_Index)");
Check(Body(ClientPackets.PcEnter("ro", 0x1122334455667788L)).SequenceEqual(Bytes(new sP_CL2FE_REQ_PC_ENTER { szID = "ro", iEnterSerialKey = 0x1122334455667788L })) && Type(ClientPackets.PcEnter("ro", 1)) == 318767105u, "REQ_PC_ENTER (80 B, tipo 318767105)");
Check(Body(ClientPackets.LoadingComplete(42)).SequenceEqual(Bytes(new sP_CL2FE_REQ_PC_LOADING_COMPLETE { iPC_ID = 42 })), "REQ_PC_LOADING_COMPLETE");
Check(Body(ClientPackets.Move(5, 1, 2, 3, 1.5f, -2f, 0.25f, 90, 3, 600)).SequenceEqual(Bytes(new sP_CL2FE_REQ_PC_MOVE { iCliTime = 5, iX = 1, iY = 2, iZ = 3, fVX = 1.5f, fVY = -2f, fVZ = 0.25f, iAngle = 90, cKeyValue = 3, iSpeed = 600 })), "REQ_PC_MOVE (44 B)");
Check(Body(ClientPackets.Stop(5, 1, 2, 3)).SequenceEqual(Bytes(new sP_CL2FE_REQ_PC_STOP { iCliTime = 5, iX = 1, iY = 2, iZ = 3 })), "REQ_PC_STOP (20 B)");
Check(Body(ClientPackets.Jump(5, 1, 2, 3, 4, 5, 6, 90, 3, 600)).SequenceEqual(Bytes(new sP_CL2FE_REQ_PC_JUMP { iCliTime = 5, iX = 1, iY = 2, iZ = 3, iVX = 4, iVY = 5, iVZ = 6, iAngle = 90, cKeyValue = 3, iSpeed = 600 })), "REQ_PC_JUMP (44 B)");

// respuestas del servidor: nuestro lector == lo que escribe la estructura real
var succ = ClientPackets.ParseLoginSucc(Bytes(new sP_LS2CL_REP_LOGIN_SUCC { iCharCount = 2, iSlotNum = 3, iPaymentFlag = 1, uiSvrTime = 0x0102030405060708UL, szID = "ro", iOpenBetaFlag = 1 }));
Check(succ.CharCount == 2 && succ.SlotNum == 3 && succ.PaymentFlag == 1 && succ.SvrTime == 0x0102030405060708UL && succ.Id == "ro" && succ.OpenBetaFlag == 1, "LOGIN_SUCC");
Check(ClientPackets.ParseErrorCode(Bytes(new sP_LS2CL_REP_LOGIN_FAIL { iErrorCode = 3, szID = "ro" }), "x") == 3, "LOGIN_FAIL: código");
sItemBase[] MkEq(short hat) { var e = new sItemBase[9]; e[4] = new sItemBase { iType = 4, iID = hat, iOpt = 9, iTimeLimit = 11 }; e[0] = new sItemBase { iType = 0, iID = 5 }; return e; }
var ci = ClientPackets.ParseCharInfo(Bytes(new sP_LS2CL_REP_CHAR_INFO { iSlot = 2, iLevel = 33, sPC_Style = new sPCStyle { iPC_UID = 4242, szFirstName = "Ro", szLastName = "S", iGender = 2, iFaceStyle = 3, iHairStyle = 4, iHairColor = 5, iSkinColor = 6, iEyeColor = 7, iHeight = 1, iBody = 2 }, iX = 10, iY = 20, iZ = 30, aEquip = MkEq(321) }));
Check(ci.Slot == 2 && ci.Level == 33 && ci.Appearance.Uid == 4242 && ci.Appearance.FirstName == "Ro" && ci.Appearance.Style.Gender == 2 && ci.Appearance.Style.HairColor == 5 && ci.Appearance.Style.Body == 2 && ci.X == 10 && ci.Z == 30 && ci.Appearance.Equip[4].Id == 321 && ci.Appearance.Equip[4].Opt == 9 && ci.Appearance.Equip[4].TimeLimit == 11 && ci.Appearance.Equip[0].Id == 5, "CHAR_INFO (204 B): slot, nivel, estilo, posición, equipo[9]");
var tk = ClientPackets.ParseShardSelectSucc(Bytes(new sP_LS2CL_REP_SHARD_SELECT_SUCC { g_FE_ServerIP = ZB("10.1.2.3", 16), g_FE_ServerPort = 23000, iEnterSerialKey = 0x1122334455667788L }));
Check(tk.Host == "10.1.2.3" && tk.Port == 23000 && tk.EnterSerialKey == 0x1122334455667788L, "SHARD_SELECT_SUCC: IP, puerto, clave");
var cn = ClientPackets.ParseCheckNameSucc(Bytes(new sP_LS2CL_REP_CHECK_CHAR_NAME_SUCC { szFirstName = "Ab", szLastName = "Cd" }));
Check(cn[0] == "Ab" && cn[1] == "Cd", "CHECK_CHAR_NAME_SUCC");
var ns = ClientPackets.ParseSaveNameSucc(Bytes(new sP_LS2CL_REP_SAVE_CHAR_NAME_SUCC { iPC_UID = 5555, iSlotNum = 2, iGender = 1, szFirstName = "Ab", szLastName = "Cd" }));
Check(ns.Uid == 5555 && ns.Slot == 2 && ns.Gender == 1 && ns.FirstName == "Ab" && ns.LastName == "Cd", "SAVE_CHAR_NAME_SUCC");
var cc = ClientPackets.ParseCharCreateSucc(Bytes(new sP_LS2CL_REP_CHAR_CREATE_SUCC { iLevel = 1, sPC_Style = new sPCStyle { iPC_UID = 5555, szFirstName = "Ab", iGender = 2, iFaceStyle = 3 }, sOn_Item = new sOnItem { iEquipUBID = 11, iEquipLBID = 22, iEquipFootID = 33, iEquipBackID = 44 } }));
Check(cc.Level == 1 && cc.Appearance.Uid == 5555 && cc.Appearance.Style.FaceStyle == 3 && cc.Appearance.Equip[1].Id == 11 && cc.Appearance.Equip[2].Id == 22 && cc.Appearance.Equip[3].Id == 33 && cc.Appearance.Equip[6].Id == 44, "CHAR_CREATE_SUCC: sOnItem -> Equip[1..3,6]");
bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }
Check(Throws(() => ClientPackets.ParseLoginSucc(new byte[10])) && Throws(() => ClientPackets.ParseCharInfo(new byte[50])) && Throws(() => ClientPackets.ParseShardSelectSucc(new byte[3])), "paquetes cortos del servidor -> ArgumentException (no se cae el cliente)");

// ================= 2. servidor falso (login + shard) por TCP real con cifrado y claves reales =================
async Task<T> Timed<T>(Task<T> t, int ms = 8000) { if (await Task.WhenAny(t, Task.Delay(ms)) != t) throw new TimeoutException("la prueba se colgó"); return await t; }
async Task<LoginException> Catch(Func<Task> f) { try { await f(); return null; } catch (LoginException e) { return e; } }
var fast = new Func<LoginOptions>(() => new LoginOptions { Timeout = TimeSpan.FromSeconds(5), DuplicateWait = TimeSpan.FromMilliseconds(50) });

try
{
    // --- flujo completo: login -> personajes -> selección -> shard -> entrada -> mover ---
    using (var ls = new FakeLs { LiveCheckFirst = true }) using (var fe = new FakeFe(ls))
    {
        using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
        {
            var info = await Timed(s.LoginAsync("ro", "pw"));
            Check(info.CharCount == 2 && info.Id == "ro" && info.SvrTime == 123456789UL, "login: LOGIN_SUCC leído");
            Check(s.Characters.Count == 2 && s.Characters[0].Appearance.Uid == 1000 && s.Characters[1].Appearance.FirstName == "Ro1" && s.Characters[1].Appearance.Equip[4].Id == 301 && s.Characters[1].Level == 31, "login: 2 REP_CHAR_INFO llegan cifrados con la clave NUEVA y se leen bien");
            Check(s.Crypto.EKey == PacketCrypto.Create64BitKey(123456789UL, 3, 4) && s.FEKey == PacketCrypto.Create64BitKey(PacketCrypto.DefaultKey, 44, 1) && s.FEKey == ls.FEKey, "claves: EKey = f(svrTime, chars+1, slot+1); FEKey = f(DefaultKey, verC, 1)");
            Check(ls.LastLogin.szID == "ro" && ls.LastLogin.iClientVerC == 44 && ls.LastLogin.iLoginType == 1, "el servidor ve REQ_LOGIN con ID, versión C y tipo 1");
            for (int i = 0; i < 40 && !ls.Log.Contains("live:P_CL2LS_REP_LIVE_CHECK"); i++) await Task.Delay(50);
            Check(ls.Log.Contains("live:P_CL2LS_REP_LIVE_CHECK"), "live-check del login server contestado automáticamente");
            var ticket = await Timed(s.SelectCharacterAsync(1001, 0));
            Check(ticket.Host == "127.0.0.1" && ticket.Port == fe.Port && ticket.EnterSerialKey == 0x1122334455667788L && ls.LastShard == 0, "CHAR_SELECT -> SHARD_SELECT encadenados; ticket del shard");
            var seen = new List<string>(); var around = new TaskCompletionSource<bool>();
            var ent = await Timed(s.EnterShardAsync(ticket, "ro", g => g.Dispatcher.PcEnter += a => { seen.Add(a.FirstName); around.TrySetResult(true); }));
            Check(ent.Enter.Id == 42 && ent.Enter.Self.FirstName == "Ro0" && ent.Enter.Self.FusionMatter == 7 && ent.Enter.SvrTime == 999, "REP_PC_ENTER_SUCC recibido (cifrado con FEKey del login)");
            Check(fe.Log.Contains("enter:ro:1122334455667788"), "el shard recibe REQ_PC_ENTER con usuario y clave de serie");
            Check(await Timed(around.Task) && seen.SequenceEqual(new[] { "Otro" }), "PC_AROUND pegado a ENTER_SUCC no se pierde (manejadores enganchados antes de leer)");
            Check(ent.Session.Crypto.EKey == PacketCrypto.Create64BitKey(999, 43, 8), "EKey de sesión tras ENTER_SUCC");
            Check(fe.LiveReplied.Wait(4000) && fe.Log.Contains("live:77"), "live-check del game server contestado automáticamente");
            await ent.Session.SendAsync(ClientPackets.Move(1, 100, 200, 300, 1f, 0f, -1f, 90, 1, 600));
            Check(fe.MoveSeen.Wait(4000) && From<sP_CL2FE_REQ_PC_MOVE>(fe.LastMove).iX == 100 && From<sP_CL2FE_REQ_PC_MOVE>(fe.LastMove).iZ == 300 && From<sP_CL2FE_REQ_PC_MOVE>(fe.LastMove).iSpeed == 600, "REQ_PC_MOVE descifrado por el servidor con la clave de sesión");
            ent.Session.Dispose();
        }
    }
    // --- contraseña mala ---
    using (var ls = new FakeLs())
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        var e = await Timed(Catch(() => s.LoginAsync("ro", "mal")));
        Check(e != null && e.Stage == "login" && e.Code == 2 && e.LoginCode == LoginError.WrongPassword, "login con clave mala -> LoginException(WrongPassword)");
    }
    // --- sesión duplicada: 3 -> EXIT_DUPLICATE -> relogin ---
    using (var ls = new FakeLs())
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        await Timed(s.LoginAsync("dup", "pw"));
        using (var s2 = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
        {
            var e = await Timed(Catch(() => s2.LoginAsync("dup", "pw")));
            Check(e != null && e.LoginCode == LoginError.AlreadyLoggedIn, "segundo login igual -> AlreadyLoggedIn (3)");
            await s2.ExitDuplicateAsync("dup");
            var ok = await Timed(s2.LoginAsync("dup", "pw"));
            Check(ok.CharCount == 2 && ls.Log.Contains("exitdup:dup"), "REQ_PC_EXIT_DUPLICATE + nuevo login -> entra");
        }
    }
    // --- login con cookie ---
    using (var ls = new FakeLs())
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        var ok = await Timed(s.LoginWithCookieAsync("teg", "auth123"));
        Check(ok.Id == "cookie_teg" && ls.LastLogin.iLoginType == 2 && ls.LastLogin.szID == " " && Encoding.ASCII.GetString(ls.LastLogin.szCookie_authid).TrimEnd('\0') == "auth123", "login con cookie (tipo 2) llega con TEGid/authid");
    }
    // --- personaje inexistente y shard rechazado ---
    using (var ls = new FakeLs())
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        await Timed(s.LoginAsync("ro", "pw"));
        var e = await Timed(Catch(() => s.SelectCharacterAsync(31337)));
        Check(e != null && e.Stage == "char_select" && e.Code == 1, "CHAR_SELECT_FAIL -> LoginException(char_select, 1)");
        ls.Mode = "shardfail"; e = await Timed(Catch(() => s.SelectCharacterAsync(1000)));
        Check(e != null && e.Stage == "shard_select" && e.Code == 2, "SHARD_SELECT_FAIL -> LoginException(shard_select, 2)");
    }
    // --- creación de personaje (cuenta sin personajes) ---
    using (var ls = new FakeLs())
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        var ok = await Timed(s.LoginAsync("newbie", "pw"));
        Check(ok.CharCount == 0 && s.Characters.Count == 0, "cuenta nueva: 0 personajes, no se queda esperando CHAR_INFO");
        var bad = await Timed(Catch(() => s.CheckNameAsync(0, 0, 0, "BadName", "X")));
        Check(bad != null && bad.Stage == "check_name" && bad.Code == 4, "nombre rechazado -> LoginException(check_name)");
        var nm = await Timed(s.CheckNameAsync(1, 2, 3, "Ab", "Cd")); Check(nm[0] == "Ab" && nm[1] == "Cd", "nombre aceptado");
        var sv = await Timed(s.SaveNameAsync(0, 2, 1, 2, 3, "Ab", "Cd")); Check(sv.Uid == 5555 && sv.Gender == 2, "nombre reservado -> uid 5555");
        var made = await Timed(s.CreateCharacterAsync(sv.Uid, cst, 11, 22, 33, 1, 2, 3)); Check(made.Appearance.Uid == 5555 && made.Appearance.Style.HairStyle == 4 && made.Appearance.Equip[1].Id == 11 && made.Appearance.Equip[3].Id == 33, "personaje creado: el servidor ve estilo y ropa inicial");
        var tk2 = await Timed(s.SelectCharacterAsync(sv.Uid)); Check(tk2.EnterSerialKey != 0, "el personaje nuevo se puede seleccionar");
    }
    // --- ENTER_FAIL, timeouts, caída ---
    using (var ls = new FakeLs()) using (var fe = new FakeFe(ls) { Mode = "enterfail" })
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        await Timed(s.LoginAsync("ro", "pw")); var tk3 = await Timed(s.SelectCharacterAsync(1000));
        var e = await Timed(Catch(() => s.EnterShardAsync(tk3, "ro")));
        Check(e != null && e.Stage == "enter" && e.Code == 3, "REP_PC_ENTER_FAIL -> LoginException(enter, 3)");
    }
    using (var ls = new FakeLs { Mode = "silent" })
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, new LoginOptions { Timeout = TimeSpan.FromMilliseconds(400) })))
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); var e = await Timed(Catch(() => s.LoginAsync("ro", "pw")));
        Check(e != null && e.Code == -2 && sw.ElapsedMilliseconds < 3000, "servidor mudo -> tiempo de espera (código -2), sin colgarse");
    }
    using (var ls = new FakeLs { Mode = "drop" })
    using (var s = await Timed(LoginSession.ConnectAsync("127.0.0.1", ls.Port, fast())))
    {
        Exception ex = null; try { await Timed(s.LoginAsync("ro", "pw")); } catch (Exception e) { ex = e; }
        Check(ex is IOException, "el servidor cierra la conexión a mitad -> IOException (no espera para siempre)");
    }
    {
        var l = new TcpListener(IPAddress.Loopback, 0); l.Start(); int dead = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
        var e = await Timed(Catch(async () => { await LoginSession.ConnectAsync("127.0.0.1", dead); }));
        Check(e != null && e.Stage == "connect", "no se puede conectar -> LoginException(connect)");
    }
}
catch (Exception e) { Check(false, "excepción inesperada: " + e); }
Console.WriteLine(fail == 0 ? "\nTODO OK" : "\n" + fail + " FALLOS"); return fail;
