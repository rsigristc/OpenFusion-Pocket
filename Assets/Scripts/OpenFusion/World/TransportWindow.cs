// TransportWindow: port de cnTrans (NPC tipo 15 = Skyway/Scamper "WARP", tipo 16 = Monkey "WARP"; NpcIconMode.StartTransport + CheckTransport). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnTrans.cs, NpcIconMode.CheckTransport/StartTransport, GameFrame (822083775 REGIST_..._SUCC, 822083776 WARP_USE_TRANSPORTATION_FAIL, 822083777 _SUCC).
// Paquetes (cuerpos con +4 de cabecera para los que se envian):
//   REQ_REGIST_TRANSPORTATION_LOCATION (318767208, 12 B) {eTT (1 = tipo 15, 2 = tipo 16), iNPC_ID, iLocationID} -> al hablar con el NPC si su ubicacion aun no esta registrada (el original la registra siempre que el evento 5,9,18 devuelve > 0).
//   REQ_PC_WARP_USE_TRANSPORTATION (318767209, 20 B) {iNPC_ID, iTransporationID (= m_iVehicleID), eIL (4), iSlotNum (0), iTurbo}.
//   REP_PC_WARP_USE_TRANSPORTATION_SUCC (822083777, 20 B) {eTT, iX, iY, iZ, iCandy}: cierra la ventana, taros = iCandy, teletransporte si eTT == 1. FAIL (822083776) {iErrorCode}: 5 -> msg 161, 7 -> 116, 8 -> 162, otro -> 163.
//   REP_PC_REGIST_TRANSPORTATION_LOCATION_SUCC lo procesa WorldMapWindow (QuestSave.Warp / Wyvern).
//   W23 TRANSPORT_WARP (cnBusContainer.Delete/DelayDelete + GameFrame 822083829): REQ_PC_TRANSPORT_WARP (318767239, Size 16) {iTransport_ID, iLcX, iLcY, iLcZ = posicion del avatar relativa al autobus x100 (servidor: x, z, y)} se envia cuando
//     el autobus en el que va el avatar (enCol.currentBus.iD == ID) es eliminado (TRANSPORTATION_EXIT / AROUND_DEL_TRANSPORTATION) y el avatar deja de enviar movimiento hasta la respuesta.
//     REP_PC_TRANSPORT_WARP_SUCC (36 B) {sTransportationAppearanceData TAD (24: eTT, iT_ID, iT_Type, iX, iY, iZ), iLcX, iLcY, iLcZ}: borra el mundo (eventos 5,7 / 4,9 / 7,3), vuelve a crear el autobus (TAD), coloca al avatar en
//     (posicion del autobus + offset) y reactiva el envio de movimiento. W24: los autobuses (TRANSPORTATION_*, PC_MOVETRANSPORTATION, enCol.currentBus) viven en World/BusSystem.cs: llama a RequestTransportWarp al borrarse el autobus del avatar y se suscribe a TransportWarpSucc (RemoveAddBus + colocar al avatar). Sin suscriptor queda la aproximacion de teletransporte.
//   W24 WARP_USE_TRANSPORTATION_SUCC (GameFrame 822083777): eTT == 1 -> DongLoader.LoadPosition + teletransporte + borra el mundo (eventos 5,7 / 4,9 / 7,3 / 6,3) + ResetRings; SIEMPRE (con o sin ventana abierta) bEnableSendPacket = true y taros = iCandy. El original NO crea ningun autobus aqui
//     (el Skyway/Scamper es un teletransporte; los autobuses solo llegan por TRANSPORTATION_*). Los avisos de error (161/116/162/163/148) son cajas de sistema (cnSystemMessageManager.SendSystemMessageBox) -> SysMessageBox.
// Skin: GUISkin 1384 (FusionFallTransportSkin: bigfont14/16, rightText, Closebutton, left_back, right_back, left_box, sel_box, sel_frame, icon_box, button...).
// Texturas (UiKit): line_main, line_main00/01, reg_warp, sel_warp, unregi_warp, mypoint, selected_monkey, register_monkey, unregister_monkey, monkey_pilot, bubble, buddy, tarosfpo; TexLoader: TutorialAssets/panelback, freezone_002, darkland_002, payzone_004.
// Limitaciones: sin camara del NPC (EndSubTarget / NPC_RTCamera), sin el efecto de teletransporte (EffectController 394) ni el vuelo del Monkey (eTT 2: el servidor mueve al jugador con TRANSPORTATION_*, no portado); modo objeto (iTransType 3) via OpenItem(slot).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class TransportWindow : MonoBehaviour
    {
        public static TransportWindow Instance;
        public static TransportWindow Ensure(GameObject host) { var w = host.GetComponent<TransportWindow>(); if (w == null) w = host.AddComponent<TransportWindow>(); Instance = w; return w; }
        public static bool Active { get { return Instance != null && Instance.open; } }
        /// <summary>Viaje en curso (cuenta atras de 1,5 s o peticion enviada): GameClient no manda movimiento (cnAvatarThirdPersonMove.bEnableSendPacket = false en SendTrans).</summary>
        public static bool Sending { get { return WarpFlow.Blocking || (Instance != null && Instance.open && (Instance.bWarp || Instance.bSend)); } }
        public Func<string, Texture> TexLoader;
        /// <summary>REP_PC_TRANSPORT_WARP_SUCC: cuerpo completo (36 B) para el sistema de autobuses (RemoveAddBus + colocacion sobre el autobus), en el hilo principal.</summary>
        public static event Action<byte[]> TransportWarpSucc;
        /// <summary>cnBusContainer.Delete: el autobus ID en el que va el avatar desaparece. offset = posicion del avatar - posicion del autobus (cliente). Envia REQ_PC_TRANSPORT_WARP y bloquea el movimiento hasta la respuesta.</summary>
        public static void RequestTransportWarp(int busId, Vector3 offset)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TRANSPORT_WARP, 16);
            ClientPackets.PutInt(p, 4, busId); ClientPackets.PutInt(p, 8, (int)(offset.x * 100f)); ClientPackets.PutInt(p, 12, (int)(offset.z * 100f)); ClientPackets.PutInt(p, 16, (int)(offset.y * 100f));   // CoordUtil.ClientToServer: (x, z, y) * 100
            Send(p); WarpFlow.Sent();
        }

        const int SK = 1384;
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), CloseR = new Rect(995, 8, 32, 32), MapPic = new Rect(390, 10, 590, 630), GoR = new Rect(213, 604, 132, 26);
        static readonly Rect SelectR = new Rect(20, 148, 340, 445), TurboToggleR = new Rect(33, 607, 20, 20), TurboBgR = new Rect(30, 604, 26, 26), TurboHitR = new Rect(30, 604, 170, 26), TurboLabelR = new Rect(60, 610, 200, 26);
        static readonly Rect FutureZone = new Rect(5632f, 512f, 2150.4f, 2150.4f), DarkZone = new Rect(1024f, 5120f, 2560f, 2048f), TutorialZone = new Rect(0f, 0f, 1536f, 1536f);

        sealed class El { public TransportationElement E; public string Name, Region, Taros; public bool Enable, IconOk; public Texture Icon, MapIcon; public Vector2 Pos; public int IconIdx; }

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; MessageTableScript msgT; NanocomHud nh;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        bool open, bWarp, bSend, bFuture, bDark, turbo, itemUse; float startT, sendT; int npcInst, npcType, sel = -1, transType, itemSlot;
        readonly List<El> list = new List<El>();
        TransportationTableScript ts;
        Texture back, worldMap; bool texTried; float texRetry;
        Rect mapRect = new Rect(0, 0, 1, 1), targetRect = new Rect(0, 0, 1, 1); Vector2 startLoc, scroll; float fx1, fx2;
        bool dragging; Vector2 dragFrom; float dragScroll;

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; ents = e; tables = t; hud = h; msgT = t != null ? t.m_pMessageTable : null; ts = t != null ? t.m_pTransportationTable : null;
            if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }

        // ============================================================ registro de ubicacion (NpcIconMode.CheckTransport) ============================================================
        bool Registered(int tt, int loc)
        {
            var qd = ents != null ? ents.QuestData : null; if (qd == null || loc <= 0) return false;
            if (tt == 1) return (qd.Warp & (1 << (loc - 1))) != 0;
            if (loc > 63) return (qd.Wyvern[1] & (1L << (loc - 64))) != 0;
            return (qd.Wyvern[0] & (1L << (loc - 1))) != 0;
        }
        /// <summary>Al hablar con un NPC tipo 15 / 16: si su ubicacion de transporte no esta registrada, REQ_REGIST_TRANSPORTATION_LOCATION.</summary>
        public void RegisterAt(int npcInstId, int npcNumber, int type)
        {
            if (ts == null || (type != 15 && type != 16)) return;
            var arr = type == 15 ? ts.m_pTransportationWarpLocation : ts.m_pBroomstickLocation; if (arr == null) return;
            int tt = type == 15 ? 1 : 2, loc = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i] != null && arr[i].m_iNPCID == npcNumber && arr[i].m_iLocationID > 0) { loc = arr[i].m_iLocationID; break; }
            if (loc <= 0 || Registered(tt, loc)) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_REGIST_TRANSPORTATION_LOCATION, 12); ClientPackets.PutInt(p, 4, tt); ClientPackets.PutInt(p, 8, npcInstId); ClientPackets.PutInt(p, 12, loc); Send(p);
        }

        // ============================================================ apertura (cnTrans.InitMode + GetWarpList) ============================================================
        public static System.Func<int, Texture> NpcPortrait;   // retrato 3D del NPC (cnTrans: pCamera sobre el NPC)
        int npcNum;
        public void Open(int npcInstId, int npcNumber, int type)
        {
            npcNum = npcNumber;
            if (open || ts == null || ents == null || ents.Self == null) return;
            itemUse = false; npcInst = npcInstId; npcType = type; turbo = false; bWarp = bSend = false; sel = -1; scroll = Vector2.zero; list.Clear(); dragging = false;
            if (nh == null) nh = FindObjectOfType<NanocomHud>();
            var pos = ents.Self.transform.position; var pt = new Vector2(pos.x, pos.z);
            bFuture = FutureZone.Contains(pt) || TutorialZone.Contains(pt); bDark = !bFuture && DarkZone.Contains(pt);
            LoadTextures(true);
            BuildList(npcNumber);
            SelectTrans(-1);
            mapRect = targetRect; open = true; Sfx.Ui("Open_Screen");
        }

        /// <summary>Modo objeto (cnTrans con isItemUse, iTransType 3): se usa un objeto de transporte (GeneralItem.m_iItemType == 9) desde el inventario; sin NPC, coste 0, destinos de zona 2 y 3.</summary>
        public void OpenItem(int invSlot)
        {
            if (open || ts == null || ents == null || ents.Self == null) return;
            itemUse = true; itemSlot = invSlot; npcInst = 0; npcType = 0; turbo = false; bWarp = bSend = false; sel = -1; scroll = Vector2.zero; list.Clear(); dragging = false;
            if (nh == null) nh = FindObjectOfType<NanocomHud>();
            var pos = ents.Self.transform.position; var pt = new Vector2(pos.x, pos.z);
            bFuture = FutureZone.Contains(pt) || TutorialZone.Contains(pt); bDark = !bFuture && DarkZone.Contains(pt);
            LoadTextures(true);
            BuildItemList(pt);
            SelectTrans(-1);
            mapRect = targetRect; open = true; Sfx.Ui("Open_Screen");
        }
        void BuildItemList(Vector2 playerPos)
        {
            transType = 3; var loc = ts.m_pBroomstickLocation; var str = ts.m_pBroomstickString; if (loc == null || str == null) return;
            startLoc = playerPos;   // el original usa position.x / position.y (altura); aqui x / z para que el icono de inicio quede donde esta el jugador
            foreach (var l in loc)
            {
                if (l == null || (l.m_iZone != 2 && l.m_iZone != 3) || l.m_iLocationID < 0 || l.m_iLocationID >= str.Length) continue;
                var d = new TransportationElement { m_iCost = 0, m_iVehicleID = 1, m_iEndLocation = l.m_iLocationID };
                var el = new El { E = d, Taros = "0", Pos = new Vector2(l.m_iXpos * 0.01f, l.m_iYpos * 0.01f) };
                el.Name = str[l.m_iLocationID] != null ? str[l.m_iLocationID].m_pstrLocationName : "";
                el.IconIdx = l.m_iIcon; el.Icon = IconOf(l.m_iIcon); el.IconOk = el.Icon != null; if (el.Icon == null) el.Icon = UiKit.Tex("buddy");
                string dong, zone; el.Region = nh != null && nh.DongAt(el.Pos, out dong, out zone) ? zone : "";
                el.Enable = Registered(2, l.m_iLocationID);
                el.MapIcon = UiKit.Tex(el.Enable ? "reg_warp" : "unregi_warp");
                list.Add(el);
            }
        }

        void BuildList(int npcNumber)
        {
            bool monkey = npcType == 16; transType = monkey ? 2 : 1;
            var data = ts.m_pTransportationData; if (data == null) return;
            var loc = monkey ? ts.m_pBroomstickLocation : ts.m_pTransportationWarpLocation; var str = monkey ? ts.m_pBroomstickString : ts.m_pTransportationWarpString;
            if (loc == null || str == null) return;
            foreach (var d in data)
            {
                if (d == null || d.m_iNPCID != npcNumber || d.m_iEndLocation < 0 || d.m_iEndLocation >= loc.Length || d.m_iStartLocation < 0 || d.m_iStartLocation >= loc.Length) continue;
                var end = loc[d.m_iEndLocation]; var st = loc[d.m_iStartLocation]; if (end == null || st == null) continue;
                startLoc = new Vector2(st.m_iXpos * 0.01f, st.m_iYpos * 0.01f);
                var el = new El { E = d, Taros = d.m_iCost.ToString(), Pos = new Vector2(end.m_iXpos * 0.01f, end.m_iYpos * 0.01f) };
                el.Name = d.m_iEndLocation < str.Length && str[d.m_iEndLocation] != null ? str[d.m_iEndLocation].m_pstrLocationName : "";
                el.IconIdx = end.m_iIcon; el.Icon = IconOf(end.m_iIcon); el.IconOk = el.Icon != null; if (el.Icon == null) el.Icon = UiKit.Tex("buddy");
                string dong, zone; el.Region = nh != null && nh.DongAt(el.Pos, out dong, out zone) ? zone : "";
                el.Enable = Registered(monkey ? 2 : 1, d.m_iEndLocation);
                el.MapIcon = UiKit.Tex(monkey ? (el.Enable ? "register_monkey" : "unregister_monkey") : (el.Enable ? "reg_warp" : "unregi_warp"));
                list.Add(el);
            }
        }
        Texture IconOf(int icon)
        {
            var j = GetComponent<JournalWindow>(); if (j == null) j = FindObjectOfType<JournalWindow>();
            if (j == null || ts.m_pTransIcon == null || icon < 0 || icon >= ts.m_pTransIcon.Length || ts.m_pTransIcon[icon] == null) return null;
            return j.IconTexture(ts.m_pTransIcon[icon]);
        }

        // ============================================================ texturas ============================================================
        Texture Load(string name)
        {
            if (TexLoader == null) return null;
            foreach (var ext in new[] { ".psd", ".png", ".dds", ".tga", "" }) { try { var t = TexLoader("TutorialAssets/" + name + ext); if (t != null) return t; } catch (Exception) { } }
            return null;
        }
        void LoadTextures(bool force)
        {
            if (!force && (Time.unscaledTime < texRetry || (back != null && worldMap != null))) return; texRetry = Time.unscaledTime + 2f;
            if (back == null) back = Load("panelback");
            if (worldMap == null) worldMap = Load(bFuture ? "freezone_002" : bDark ? "darkland_002" : "payzone_004");
        }

        // ============================================================ mensajes ============================================================
        string Message(int id) { return msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null ? msgT.m_pMessageData[id].m_szString : "(message " + id + ")"; }
        void Msg(int id) { if (SysMessageBox.Instance != null) { SysMessageBox.Show(id, null, null); return; } var sm = SocialMenu.Instance; if (sm != null) sm.Notice(Loc.T(Message(id))); else Debug.Log("[transporte] " + Message(id)); }   // SendSystemMessageBox(id)
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[transporte] envio: " + ex.Message); } } }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_WARP_USE_TRANSPORTATION_SUCC && b.Length >= 20)
            {
                var body = b; q.Enqueue(() =>
                {
                    int tt = BitConverter.ToInt32(body, 0), x = BitConverter.ToInt32(body, 4), y = BitConverter.ToInt32(body, 8), z = BitConverter.ToInt32(body, 12), candy = BitConverter.ToInt32(body, 16);
                    if (hud != null && hud.Data != null) hud.Data.Taros = candy;
                    Exit(); Sfx.Ui("Action_Sucess"); WarpFlow.Cancel();   // bEnableSendPacket = true
                    if (tt == 1 && ents != null) { ChannelWarp.ClearWorldNow(); ents.RaiseTeleport(x, y, z); }   // LoadPosition + (5,9,8) + (5,7) (4,9) (7,3) (6,3) + ResetRings (EpTriggers lo hace en Teleported)
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_TRANSPORT_WARP_SUCC && b.Length >= 36)
            {
                var body = b; q.Enqueue(() =>
                {
                    WarpFlow.Cancel();   // bEnableSendPacket = true
                    ChannelWarp.ClearWorldNow();   // cnEvent(5,7) / (4,9) / (7,3)
                    int x = BitConverter.ToInt32(body, 12) + BitConverter.ToInt32(body, 24), y = BitConverter.ToInt32(body, 16) + BitConverter.ToInt32(body, 28), z = BitConverter.ToInt32(body, 20) + BitConverter.ToInt32(body, 32);
                    if (TransportWarpSucc != null) TransportWarpSucc(body);
                    else if (ents != null) ents.RaiseTeleport(x, y, z);   // sin autobuses: posicion del TAD + offset (aproximacion)
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_WARP_USE_TRANSPORTATION_FAIL)
            {
                int code = b.Length >= 4 ? BitConverter.ToInt32(b, 0) : 0;
                q.Enqueue(() => { bSend = bWarp = false; Msg(code == 5 ? 161 : code == 7 ? 116 : code == 8 ? 162 : 163); });
            }
        }

        // ============================================================ bucle ============================================================
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[transporte] " + ex.Message); } }
            if (!open) return;
            LoadTextures(false);
            if (bWarp && !bSend && Time.unscaledTime - startT > 1.5f && sel >= 0 && sel < list.Count)
            {   // SendTrans (NPC: iNPC_ID = id de instancia, iTransporationID = m_iVehicleID)
                var el = list[sel]; var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_WARP_USE_TRANSPORTATION, 20);   // 5 ints (Size=16 del atributo es menor que los campos: Mono usa el tamano natural 20); con 16 el servidor cierra la conexion

                // objeto: iNPC_ID = 0, iTransporationID = m_iEndLocation, eIL = 1 (inventario), iSlotNum = hueco del objeto
                ClientPackets.PutInt(p, 4, itemUse ? 0 : npcInst); ClientPackets.PutInt(p, 8, itemUse ? el.E.m_iEndLocation : el.E.m_iVehicleID); ClientPackets.PutInt(p, 12, itemUse ? 1 : 4); ClientPackets.PutInt(p, 16, itemUse ? itemSlot : 0); ClientPackets.PutInt(p, 20, turbo ? 1 : 0);
                Send(p); bSend = true; sendT = Time.unscaledTime; bWarp = false;
                Debug.Log("[transporte] REQ_PC_WARP_USE_TRANSPORTATION npc=" + (itemUse ? 0 : npcInst) + " id=" + (itemUse ? el.E.m_iEndLocation : el.E.m_iVehicleID) + " eIL=" + (itemUse ? 1 : 4) + " slot=" + (itemUse ? itemSlot : 0) + " turbo=" + (turbo ? 1 : 0) + " sender=" + (NpcUI.Sender != null));
            }
            if (bSend && Time.unscaledTime - sendT > 10f) { bSend = false; Debug.LogWarning("[transporte] el servidor no respondio al viaje en 10 s"); }   // sin respuesta: se vuelve a mostrar la ventana
            if (!bWarp && !bSend && Input.GetKeyDown(KeyCode.Escape)) Exit();
        }
        void Exit() { open = false; bWarp = bSend = false; dragging = false; }

        int Taros { get { return hud != null && hud.Data != null ? hud.Data.Taros : 0; } }

        void Go()
        {
            if (sel < 0 || sel >= list.Count) { Msg(148); Sfx.Ui("Action_Failure01"); return; }
            var el = list[sel];
            if (!el.Enable) { Msg(161); return; }
            if (Taros < el.E.m_iCost || (turbo && Taros < el.E.m_iCost * 3)) { Msg(116); return; }
            Sfx.Ui("Transportation_Warp"); bWarp = true; startT = Time.unscaledTime;
        }

        // ============================================================ seleccion / mapa ============================================================
        void SelectTrans(int index)
        {
            if (list.Count <= index) return;
            float cx, cy, w, h;
            if (index >= 0)
            {
                sel = index; var el = list[index]; cx = el.Pos.x; cy = el.Pos.y;
                if (itemUse)
                {   // SelectTrans en modo objeto: el mapa de fondo depende de la zona del destino
                    bool d = DarkZone.Contains(el.Pos); if (d != bDark || worldMap == null) { bDark = d; worldMap = Load(d ? "darkland_002" : "payzone_004"); }
                }
            }
            else { cx = list.Count > 0 ? startLoc.x : ents.Self.transform.position.x; cy = list.Count > 0 ? startLoc.y : ents.Self.transform.position.z; }
            if (transType == 2 || bFuture) { w = 0.2880543f; h = 0.3075834f; } else { w = 0.5761086f; h = 0.6151668f; }
            var r = new Rect(cx / 8192f - w / 2f, cy / 8192f - h / 2f, w, h);
            if (bFuture)
            {
                if (r.x < FutureZone.x / 8192f) r.x = FutureZone.x / 8192f; if (r.y < FutureZone.y / 8192f) r.y = FutureZone.y / 8192f;
                if (r.x + w > FutureZone.xMax / 8192f) r.x = FutureZone.xMax / 8192f - w; if (r.y + h > FutureZone.yMax / 8192f) r.y = FutureZone.yMax / 8192f - h;
            }
            else
            {
                if (r.x < 0f) r.x = 0f; if (r.y < 0f) r.y = 0f; if (r.x + w > 1f) r.x = 1f - w; if (r.y + h > 1f) r.y = 1f - h;
            }
            targetRect = r; if (sel < 0) mapRect = r;
        }

        // ============================================================ dibujo ============================================================
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static SkinStyle St(string n) { return Skin.Get(SK, n); }
        static void Pic(Rect r, Texture t) { if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }
        static void Box(Rect r, string style) { var s = St(style); if (s != null && Event.current.type == EventType.Repaint) Skin.Bg(r, s.N, s); }

        void OnGUI()
        {
            if (!open) return; var e = Event.current; if (e == null || Screen.height <= 0) return;
            if (bWarp || bSend) return;   // el original no dibuja nada mientras viaja
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                DrawWindow(e);
                GUI.EndGroup();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        void DrawWindow(Event e)
        {
            Box(new Rect(379, 0, 658, 652), "right_back"); Box(new Rect(0, 0, 380, 652), "left_back");
            Skin.Label(new Rect(10, 10, 300, 30), Loc.T("TRANSPORTATION"), St("bigfont16"));
            if (sel >= 0 && sel < list.Count) Skin.Label(new Rect(10, 25, 300, 20), list[sel].Name + " - " + list[sel].Region, St("bigfont14"));
            if (Skin.Button(CloseR, "", St("Closebutton"))) { Exit(); return; }
            if (e.type == EventType.Repaint)
            {
                mapRect.x = targetRect.x * 0.2f + mapRect.x * 0.8f; mapRect.y = targetRect.y * 0.2f + mapRect.y * 0.8f; mapRect.width = targetRect.width * 0.2f + mapRect.width * 0.8f; mapRect.height = targetRect.height * 0.2f + mapRect.height * 0.8f;
            }
            DrawMap(e);
            DrawLeft(e);
        }

        bool InZone(Vector2 p)
        {
            if (bFuture) return true; return bDark ? DarkZone.Contains(p) : !DarkZone.Contains(p);
        }

        void DrawMap(Event e)
        {
            GUI.BeginGroup(MapPic);
            var local = new Rect(0, 0, MapPic.width, MapPic.height);
            if (e.type == EventType.Repaint)
            {
                if (worldMap != null) { GUI.color = Color.gray; GUI.DrawTextureWithTexCoords(local, worldMap, mapRect); GUI.color = Color.white; }
                foreach (var el in list) if (InZone(el.Pos)) Icon(el.Pos / 8192f, el.MapIcon, false);
                if (transType == 2)
                {
                    Icon(startLoc / 8192f, UiKit.Tex("register_monkey"), true);
                    if (sel >= 0 && sel < list.Count) Icon(list[sel].Pos / 8192f, UiKit.Tex("selected_monkey"), false);
                }
                else
                {
                    Icon(startLoc / 8192f, UiKit.Tex("reg_warp"), true);
                    if (sel >= 0 && sel < list.Count && InZone(list[sel].Pos)) Icon(list[sel].Pos / 8192f, UiKit.Tex("sel_warp"), false);
                }
                var lm = UiKit.Tex("line_main"); if (lm != null) GUI.DrawTexture(local, lm, ScaleMode.StretchToFill, true);
                Scan(local, UiKit.Tex("line_main00"), ref fx1, 0.15f, true); Scan(local, UiKit.Tex("line_main01"), ref fx2, 0.5f, false);
            }
            GUI.EndGroup();
        }

        /// <summary>Barrido vertical de las texturas line_main00 (entra y sale del marco) y line_main01 (cae hasta el fondo).</summary>
        void Scan(Rect map, Texture t, ref float f, float speed, bool clip)
        {
            if (t == null) return; f += Time.unscaledDeltaTime * speed; float th = t.height;
            if (clip)
            {
                if (f > 1f + th / map.height) f = 0f;
                float y = f * map.height - th; var sr = new Rect(0, 0, 1, 1); float hh = th;
                if (y < 0f) { sr.y -= y / th; sr.height += y / th; hh += y; y = 0f; }
                if (y > map.height - hh) { float r = map.height - y; sr.height = r / hh; hh = r; }
                GUI.DrawTextureWithTexCoords(new Rect(map.x, map.y + y, map.width, hh), t, sr);
            }
            else
            {
                if (f > 1f) f = 0f; float y = Mathf.Min(map.height - th, f * map.height);
                GUI.DrawTexture(new Rect(map.x, map.y + y, map.width, th), t, ScaleMode.StretchToFill, true);
            }
        }

        void Icon(Vector2 p, Texture t, bool start)
        {
            if (t == null || !mapRect.Contains(p)) return;
            var r = new Rect(0, 0, 36, 36); p.x -= mapRect.x; p.y -= mapRect.y;
            r.x += p.x / mapRect.width * MapPic.width - r.width / 2f; r.y += MapPic.height - p.y / mapRect.height * MapPic.height - r.height / 2f;
            if (r.xMax > MapPic.width || r.yMax > MapPic.height || r.xMin < 0 || r.yMin < 0) return;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            if (start) { r.x += 26f; if (r.xMax <= MapPic.width) { var s = UiKit.Tex("mypoint"); if (s != null) GUI.DrawTexture(r, s, ScaleMode.StretchToFill, true); } }
        }

        void DrawLeft(Event e)
        {
            Box(new Rect(17, 65, 343, 577), "left_box");
            if (transType == 1 && NpcPortrait != null) { var pt0 = NpcPortrait(npcNum); if (pt0 != null) Pic(new Rect(20, 34, 90, 90), pt0); }
            if (transType == 2) { var m = UiKit.Tex("monkey_pilot"); if (m != null) Pic(new Rect(20, 124 - m.height, m.width, m.height), m); }
            Pic(new Rect(127, 53, 200, 44), UiKit.Tex("bubble"));
            Skin.Label(new Rect(182, 65, 140, 20), Loc.T("WHERE TO?"), St("bigfont16"), Color.black);

            // ---- lista (BeginScrollView sin barras; rueda y arrastre) ----
            float contentH = 82 * list.Count + 10, maxS = Mathf.Max(0f, contentH - SelectR.height);
            bool inList = SelectR.Contains(e.mousePosition);
            if (e.type == EventType.ScrollWheel && inList) { scroll.y = Mathf.Clamp(scroll.y + e.delta.y * 25f, 0f, maxS); e.Use(); }
            if (e.type == EventType.MouseDown && e.button == 0 && inList) { dragFrom = e.mousePosition; dragScroll = scroll.y; dragging = false; }
            else if (e.type == EventType.MouseDrag && e.button == 0 && dragFrom != Vector2.zero && Mathf.Abs(e.mousePosition.y - dragFrom.y) > 8f && inList) { dragging = true; scroll.y = Mathf.Clamp(dragScroll - (e.mousePosition.y - dragFrom.y), 0f, maxS); }
            bool up = e.type == EventType.MouseUp && e.button == 0;
            GUI.BeginGroup(SelectR);
            var selBox = St("sel_box"); var frame = St("sel_frame"); var iconBox = St("icon_box"); var nm = St("bigfont14"); var rt = St("rightText");
            for (int i = 0; i < list.Count; i++)
            {
                var el = list[i]; float y = i * 82 + 5 - scroll.y; if (y + 80 < 0 || y > SelectR.height) continue;
                if (!el.IconOk) { var ti = IconOf(el.IconIdx); if (ti != null) { el.Icon = ti; el.IconOk = true; } }   // el bundle de iconos puede cargar despues de abrir la ventana
                var rc = new Rect(10, y, 295, 75); bool hov = rc.Contains(e.mousePosition) && inList;
                if (e.type == EventType.Repaint)
                {
                    if (!el.Enable) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    if (selBox != null) Skin.Bg(rc, hov && selBox.H != null ? selBox.H : selBox.N, selBox);
                    if (el.Enable && i == sel && frame != null) Skin.Bg(new Rect(9, y - 1, 297, 77), frame.N, frame);
                    GUI.color = Color.white;
                }
                Color nc = el.Enable && i == sel ? Color.yellow : Color.white; if (!el.Enable) nc.a = 0.5f;
                Skin.Label(new Rect(93, y + 14, 200, 20), el.Name, nm, nc); Skin.Label(new Rect(93, y + 34, 200, 20), el.Region, nm, nc);
                if (el.Enable)
                {
                    int cost = el.E.m_iCost; Color pc = Taros < cost || (turbo && Taros < cost * 3) ? Color.red : turbo ? Color.yellow : Color.white;
                    Skin.Label(new Rect(143, y + 50, 130, 25), turbo ? (cost * 3).ToString() : el.Taros, rt, pc);
                    Pic(new Rect(273, y + 45, 31, 31), UiKit.Tex("tarosfpo"));
                }
                else Skin.Label(new Rect(143, y + 50, 130, 25), Loc.T("Unregistered"), rt, new Color(1f, 1f, 1f, 0.5f));
                if (e.type == EventType.Repaint)
                {
                    var ir = new Rect(18, y + 5, 64, 64); if (!el.Enable) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                    if (iconBox != null) Skin.Bg(ir, iconBox.N, iconBox); if (el.Icon != null) GUI.DrawTexture(ir, el.Icon, ScaleMode.ScaleToFit, true); GUI.color = Color.white;
                }
                if (up && hov && !dragging && el.Enable && i != sel) { Sfx.Button(); SelectTrans(i); }
            }
            GUI.EndGroup();
            if (up) { dragging = false; dragFrom = Vector2.zero; }

            if (transType == 2)
            {   // TURBO TRAVEL
                var bt = St("button"); if (bt != null && e.type == EventType.Repaint) Skin.Bg(TurboBgR, bt.N, bt);
                if (turbo && e.type == EventType.Repaint) { var tg = St("toggle"); var tt = tg != null ? (tg.ON ?? tg.N) : null; if (tt != null) Skin.Bg(TurboToggleR, tt, tg); else { GUI.color = new Color(0.6f, 1f, 1f); GUI.DrawTexture(TurboToggleR, Texture2D.whiteTexture); GUI.color = Color.white; } }
                if (up && TurboHitR.Contains(e.mousePosition)) { turbo = !turbo; Sfx.Button(); }
                Skin.Label(TurboLabelR, Loc.T("TURBO TRAVEL"), St("bigfont14"));
            }
            if (Skin.Button(GoR, Loc.T("GO NOW!"), St("button"))) Go();
        }
    }
}
