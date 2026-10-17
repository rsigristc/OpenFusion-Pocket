using System; using System.Runtime.InteropServices; using OpenFusion.Net;
// Verifica la disposición de bytes contra las estructuras REALES del cliente (Marshal) y que ParseAppearance/ParseLoadData leen lo que Marshal escribe.
int fail = 0; void Check(bool ok, string m) { Console.WriteLine((ok ? "OK   " : "FALLA ") + m); if (!ok) fail++; }
Check(Marshal.SizeOf<sPCStyle>() == 76 && Marshal.SizeOf<sPCAppearanceData>() == 232 && Marshal.SizeOf<sPCLoadData2CL>() == 2552, "tamaños Marshal 76/232/2552");
int Off<T>(string f) => (int)Marshal.OffsetOf<T>(f);
Check(Off<sPCStyle>("szFirstName") == 10 && Off<sPCStyle>("szLastName") == 28 && Off<sPCStyle>("iGender") == 62 && Off<sPCStyle>("iBody") == 69 && Off<sPCStyle>("iClass") == 72, "offsets sPCStyle");
Check(Off<sPCAppearanceData>("PCStyle") == 4 && Off<sPCAppearanceData>("iLv") == 86 && Off<sPCAppearanceData>("iAngle") == 108 && Off<sPCAppearanceData>("ItemEquip") == 112 && Off<sPCAppearanceData>("Nano") == 220, "offsets sPCAppearanceData");
Check(Off<sPCLoadData2CL>("PCStyle") == 4 && Off<sPCLoadData2CL>("iLevel") == 84 && Off<sPCLoadData2CL>("iHP") == 92 && Off<sPCLoadData2CL>("iMapNum") == 116 && Off<sPCLoadData2CL>("iX") == 120 && Off<sPCLoadData2CL>("iAngle") == 132 && Off<sPCLoadData2CL>("aEquip") == 136, "offsets sPCLoadData2CL");
byte[] Bytes<T>(T v) where T : struct { var b = new byte[Marshal.SizeOf<T>()]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); try { Marshal.StructureToPtr(v, h.AddrOfPinnedObject(), false); } finally { h.Free(); } return b; }
var st = new sPCStyle { iPC_UID = 0x1122334455667788, iNameCheck = 1, szFirstName = "Ro", szLastName = "Sigrist", iGender = 2, iFaceStyle = 3, iHairStyle = 7, iHairColor = 11, iSkinColor = 5, iEyeColor = 4, iHeight = 2, iBody = 1, iClass = 9 };
var eq = new sItemBase[9]; for (int i = 0; i < 9; i++) eq[i] = new sItemBase { iType = (short)(i + 1), iID = (short)(100 + i), iOpt = i == 2 ? (555 << 16) | 3 : 0, iTimeLimit = i };
var ap = new sPCAppearanceData { iID = 4242, PCStyle = st, iLv = 36, iHP = 1234, iMapNum = 6, iX = 100000, iY = -2000, iZ = 777, iAngle = 90, ItemEquip = eq, Nano = new sNano { iID = 12, iSkillID = 3, iStamina = 150 } };
var a = PcAppearance.ParseAppearance(Bytes(ap));
Check(a.Id == 4242 && a.Uid == 0x1122334455667788 && a.FirstName == "Ro" && a.LastName == "Sigrist" && a.NameChecked && a.Class == 9, "ParseAppearance: id, uid, nombre, clase");
Check(a.Style.Gender == 2 && a.Style.FaceStyle == 3 && a.Style.HairStyle == 7 && a.Style.HairColor == 11 && a.Style.SkinColor == 5 && a.Style.EyeColor == 4 && a.Style.Height == 2 && a.Style.Body == 1, "ParseAppearance: aspecto");
Check(a.Level == 36 && a.Hp == 1234 && a.MapNum == 6 && a.X == 100000 && a.Y == -2000 && a.Z == 777 && a.Angle == 90 && a.NanoId == 12, "ParseAppearance: nivel/hp/posición/nano");
Check(a.Equip[8].Id == 108 && a.Equip[8].TimeLimit == 8 && a.VisibleEquip().Pants == 102 && a.VisibleEquip(true).Pants == 555 && a.VisibleEquip(true).Shirt == 101, "equipo: iID y variante combine (iOpt>>16)");
var ld = new sPCLoadData2CL { PCStyle = st, iLevel = 21, iHP = 999, iMapNum = 3, iX = 5, iY = 6, iZ = 7, iAngle = 180, aEquip = eq };
var l = PcAppearance.ParseLoadData(Bytes(ld));
Check(l.Style.Gender == 2 && l.Style.HairColor == 11 && l.LastName == "Sigrist" && l.Level == 21 && l.Hp == 999 && l.MapNum == 3 && l.X == 5 && l.Z == 7 && l.Angle == 180 && l.Equip[0].Id == 100 && l.Equip[6].Id == 106, "ParseLoadData: aspecto, posición, equipo");
return fail;
