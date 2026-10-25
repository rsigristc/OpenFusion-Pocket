// ChatHud: reconstruccion del chat del cliente original (FusionFall / Retrobution, Unity Web Player 2.5): ventana con pestanas ALL / GROUP / BUDDY,
// historial de lineas con barra de desplazamiento, linea de entrada, boton SEND, esquina de redimensionado, envio y recepcion de mensajes.
//
// ORIGINAL (decompilado, Assembly-CSharp/CnGuiChat.cs, GameObject "GameHUD", skin "FusionFallChatSkin" = GUISkin path_id 1368):
//   OnOwnGUI (l.427)            -> escala FFGUIUtility.ScaleAroundPivot(BottomLeft) = Screen.height/681, WindowRect.y = Screen.height - alto, arrastre de la esquina
//   DoChatWindow (l.1053)       -> pestanas (TabRect), caja darkenedChatArea, chatEntryArea, botones menuChat/emote/SEND, TextField(127), lista + VerticalScrollbar a la IZQUIERDA
//   AddChatString/AddList       -> l.917 / l.961 (prefijos "[Local] ", "[Group] ", "[Buddy] ", limite iMaxChatList = 50)
//   Receive*                    -> l.609..866 (ReceiveChat, ReceiveMenuChat, ReceiveGroupChat, ReceiveGroupMenuChat, ReceiveBuddyChat, ReceiveBuddyMenuChat, ReceiveMOTD, ReceiveChannelInfo)
//   Chat()                      -> l.1751 (envio segun pestana), CheckSpam l.3056 (1 s entre mensajes; >15 mensajes "acumulados" => bloqueo 15 s), historial iMaxHistoryList = 20
//   Update()                    -> l.1812 (seleccion de lista, tecla Enter = ConfigurableInput Key.Menu (Return/KeypadEnter), flechas arriba/abajo = historial)
//   Colores por tipo            -> l.1296: 0 general = cnOption.arrayColor[1] (1,0.6,0), 2 buddy = [2] (0.5,0.66,0), 3 group = [3] (0,0.39,0), 4 blanco, 5 (1,0.5,0), 6 rojo, 7 azul, resto (incl. 1 sistema) rojo; se multiplica por el color del label (e1e3e5)
//   Paquetes                    -> sP_CL2FE_REQ_SEND_(FREECHAT|MENUCHAT|BUDDY_FREECHAT|ALL_GROUP_FREECHAT)_MESSAGE, sP_FE2CL_REP_SEND_*_SUCC, sP_FE2CL_PC_MOTD_LOGIN, sP_FE2CL_ANNOUNCE_MSG (ver ChatPackets)
//
// Diferencias (ver REPORT.md): no hay GUILayout (se calcula la maquetacion que produce), entrada de texto propia con fuente de mapa de bits (TouchScreenKeyboard en moviles),
// sin sonidos (hook Sound), sin MenuChatScript (frases/emotes: solo ganchos), sin sistema de amigos/grupo (proveedores opcionales), sin comandos GM del cliente (CheckCheatKey).
// SIN COMPILAR EN UNITY (revisado a mano contra la API 2022.3 LTS, C# 9).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    /// <summary>Paquetes de chat. Disposicion = Marshal Pack 4 del original (cadenas UTF-16 de longitud fija). Cada Build devuelve tipo (4 B) + cuerpo.</summary>
    public static class ChatPackets
    {
        public const int MsgBody = 260;        // sP_CL2FE_REQ_SEND_FREECHAT_MESSAGE / MENUCHAT / ALL_GROUP_*: szFreeChat[128] (0) + iEmoteCode (256)
        public const int BuddyBody = 272;      // sP_CL2FE_REQ_SEND_BUDDY_*: szFreeChat[128] (0) + iEmoteCode (256) + iBuddyPCUID i64 (260, Pack 4) + iBuddySlot i8 (268)
        public const int MsgSuccBody = 264;    // sP_FE2CL_REP_SEND_FREECHAT_MESSAGE_SUCC (= MENUCHAT, ALL_GROUP_*): iPC_ID / iSendPCID (0) + szFreeChat[128] (4) + iEmoteCode (260)
        public const int BuddySuccBody = 276;  // sP_FE2CL_REP_SEND_BUDDY_*_SUCC: iFromPCUID i64 (0) + iToPCUID i64 (8) + szFreeChat[128] (16) + iEmoteCode (272)

        static byte[] Msg(uint type, string text, int emote)
        {
            var p = ClientPackets.New(type, MsgBody); ClientPackets.PutStr(p, 4, text, 128); BitConverter.GetBytes(emote).CopyTo(p, 4 + 256); return p;
        }
        static byte[] Buddy(uint type, string text, long uid, int slot, int emote)
        {
            var p = ClientPackets.New(type, BuddyBody); ClientPackets.PutStr(p, 4, text, 128); BitConverter.GetBytes(emote).CopyTo(p, 4 + 256); BitConverter.GetBytes(uid).CopyTo(p, 4 + 260); p[4 + 268] = (byte)(sbyte)slot; return p;
        }
        public static byte[] FreeChat(string text, int emote = 0) { return Msg(PacketIds.P_CL2FE_REQ_SEND_FREECHAT_MESSAGE, text, emote); }
        public static byte[] MenuChat(string text, int emote = 0) { return Msg(PacketIds.P_CL2FE_REQ_SEND_MENUCHAT_MESSAGE, text, emote); }
        public static byte[] GroupFreeChat(string text, int emote = 0) { return Msg(PacketIds.P_CL2FE_REQ_SEND_ALL_GROUP_FREECHAT_MESSAGE, text, emote); }
        public static byte[] GroupMenuChat(string text, int emote = 0) { return Msg(PacketIds.P_CL2FE_REQ_SEND_ALL_GROUP_MENUCHAT_MESSAGE, text, emote); }
        public static byte[] BuddyFreeChat(string text, long buddyUid, int slot, int emote = 0) { return Buddy(PacketIds.P_CL2FE_REQ_SEND_BUDDY_FREECHAT_MESSAGE, text, buddyUid, slot, emote); }
        public static byte[] BuddyMenuChat(string text, long buddyUid, int slot, int emote = 0) { return Buddy(PacketIds.P_CL2FE_REQ_SEND_BUDDY_MENUCHAT_MESSAGE, text, buddyUid, slot, emote); }

        /// <summary>Cadena UTF-16 de longitud fija (hasta el primer 0). Tolera cuerpos cortos.</summary>
        public static string Str(byte[] b, int o, int chars)
        {
            if (b == null || o < 0 || o >= b.Length) return "";
            int n = Math.Min(chars, (b.Length - o) / 2); if (n <= 0) return "";
            var s = Encoding.Unicode.GetString(b, o, n * 2); int z = s.IndexOf('\0'); return z >= 0 ? s.Substring(0, z) : s;
        }
        public static bool ParseMsg(byte[] b, out int pcId, out string text, out int emote)
        {
            pcId = 0; text = ""; emote = 0; if (b == null || b.Length < MsgSuccBody) return false;
            pcId = BitConverter.ToInt32(b, 0); text = Str(b, 4, 128); emote = BitConverter.ToInt32(b, 260); return true;
        }
        public static bool ParseBuddy(byte[] b, out long from, out long to, out string text, out int emote)
        {
            from = 0; to = 0; text = ""; emote = 0; if (b == null || b.Length < BuddySuccBody) return false;
            from = BitConverter.ToInt64(b, 0); to = BitConverter.ToInt64(b, 8); text = Str(b, 16, 128); emote = BitConverter.ToInt32(b, 272); return true;
        }
    }

    public sealed class ChatHud : MonoBehaviour
    {
        // ---------------- API publica ----------------
        /// <summary>true mientras el chat esta abierto para escribir (bChatEnable). GameClient debe ignorar el movimiento/salto (ver PATCH.txt).</summary>
        public static bool Typing { get; private set; }
        /// <summary>true si el raton esta sobre la ventana de chat (para no hacer zoom de camara / clic en el mundo).</summary>
        public static bool MouseOver { get; private set; }
        public static ChatHud Instance { get; private set; }

        public bool Visible = true;
        public bool TimeStamp = false;              // cnOption.displayOption.bChatTimeStamp (por defecto false)
        public bool EventMessage1 = true, EventMessage2 = true;   // bEventMessage1 (tipos 4/5), bEventMessage2 (tipos 6/7)
        public bool AnnounceToChat = false;          // el original manda ANNOUNCE_MSG a un banner (cnEvent 10,1), no al chat. Sin banner en el port => linea de sistema
        public bool ClickToOpen = true;             // (extension) clic en la linea de entrada cerrada abre el chat (moviles no tienen Enter)
        public bool EscapeCloses = true;            // (extension) Escape cierra el chat sin enviar
        public bool UseNativeKeyboardOnMobile = true;
        public float WheelStep = 36f;               // px por muesca de rueda (original: scrollVel 200 * eje; aqui ~3 lineas)
        public string LabelFontJson = "ChaletBook-Regular_Small__1018", TabFontJson = "JEFFE___14__903";
        public int MaxChatList = 50, MaxHistory = 20;           // iMaxChatList, iMaxHistoryList

        public Action<string> Sound;                // nombres originales: "Outgoing_chat", "Incoming_Tell", "Tab_Click01", "Action_Failure01"
        public Action<byte[]> SenderOverride;       // por defecto OpenFusion.World.NpcUI.Sender
        public Action OnMenuChatButton, OnEmoteButton;          // botones de MenuChatScript (frases / emotes): sin implementar, solo ganchos
        public Func<int> GroupSizeProvider;         // cnGroupManager.m_iGroupSize (incluye al propio jugador). null => 0
        public Func<int> BuddyCountProvider;        // numero de amigos no bloqueados. null => 0
        public Func<long, string> BuddyNameProvider;            // uid -> "Nombre Apellido"; null/"" => "Player <uid>"
        public Func<int, bool> IsBlockedPc; public Func<long, bool> IsBlockedUid;
        /// <summary>(pcId o npcId, texto, emote, tipo: 0 libre, 1 menu, 2 NPC (emote 420)). Para globos de dialogo/animaciones de emote.</summary>
        public event Action<int, string, int, int> ChatBubble;

        public void AddSystem(string s) { Post(() => AddChatString(1, s, 0)); }
        /// <summary>Equivale a CnGuiChat.AddEventString (tipos: 1 sistema, 2 buddy, 3 grupo, 4 NPC, 5 recibido, 6 dano recibido, 7 ataque).</summary>
        public static void AddEventString(int type, string str)
        {
            var c = Instance; if (c == null) return;
            if (((type != 4 && type != 5) || c.EventMessage1) && ((type != 6 && type != 7) || c.EventMessage2)) c.Post(() => c.AddChatString(type, str, 0));
        }
        /// <summary>Destino de la pestana BUDDY (m_iBuddyIndex / iSendIndex). uid 0 = ninguno.</summary>
        public void SetBuddyTarget(long uid, int slot) { buddyUid = uid; buddySlot = slot; pendingRecalc = true; }
        /// <summary>Envio de una frase de menu (CnGuiChat.MenuChat l.1515); lo usaria MenuChatScript.</summary>
        public void SendMenuChat(string text, int emote)
        {
            if (!CheckSpam()) return;
            if (filter == 0) Send(ChatPackets.MenuChat(text, emote));
            else if (filter == 2 && buddyUid != 0) Send(ChatPackets.BuddyMenuChat(text, buddyUid, buddySlot, emote));
            else if (filter == 1) Send(ChatPackets.GroupMenuChat(text, emote));
        }
        /// <summary>bChatEnable (chat abierto para escribir).</summary>
        public bool IsOpen { get { return open; } }
        public void Open() { if (open || !TutorialTraining.ChatAllowed) return; open = true; Typing = true; iCurHistory = history.Count; ScrollToBottomOnOpen(); StartKeyboard(); blinkT0 = Time.unscaledTime; }
        public void Close() { if (!open) return; open = false; Typing = false; StopKeyboard(); }

        // ---------------- constantes de maquetacion (CnGuiChat; Start() sobreescribe los valores serializados) ----------------
        public const float UiRefH = 681f;
        const float TabH = 14f, TabW = 135f, EntryH = 23f;
        const float MinW = 325f, MinH = 135f, MaxW = 550f, MaxH = 250f;     // windowRectMin/Max
        const int MaxLen = 127;
        static readonly Color LabelCol = new Color(0xe1 / 255f, 0xe3 / 255f, 0xe5 / 255f, 1f);   // label.normal.textColor
        static readonly Color TabCol = new Color(0.8f, 1f, 1f, 1f), TabHoverCol = new Color(0f, 0.2f, 0.4f, 1f), TabSelCol = new Color(0f, 1f, 1f, 1f);
        static readonly Color BlueBtnCol = Color.white, BlueBtnHoverCol = new Color(0f, 0.3411765f, 0.5294118f, 1f), NullCol = new Color(0.8f, 1f, 1f, 1f);
        static readonly Color CursorCol = new Color(1f, 1f, 0.0846774f, 1f);
        static readonly string[] TabName = { "ALL", "GROUP", "BUDDY" };   // serializado en bindings.json (la clase trae "FRIEND" pero el prefab dice BUDDY)
        // cnOption.arrayColor[1..3] (iGeneral = 1, iBuddy = 2, iGroup = 3)
        static readonly Color GeneralCol = new Color(1f, 0.6f, 0f), BuddyCol = new Color(0.5f, 0.66f, 0f), GroupCol = new Color(0f, 0.39f, 0f);

        sealed class ChatItem { public string Text; public int Height, Offset, Type; public ChatItem(string t, int h, int o, int ty) { Text = t; Height = h; Offset = o; Type = ty; } }

        // ---------------- estado ----------------
        PlayerHud hud; WorldEntities ents; PacketDispatcher dispatcher;
        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        readonly Dictionary<int, string> names = new Dictionary<int, string>();
        int selfId; long selfUid; string selfName = "";

        float winW = 440f, winH = 150f;             // cnOption.displayOption.fChatBoxWidth/Height
        bool open;                                   // bChatEnable
        string text = ""; int caret; float blinkT0;
        float scrollY; bool snapBottom; bool pendingRecalc = true;
        int selectTab, filter;                       // SelectTab (pestana dibujada) / SendFilter (destino); pueden diferir (ver Update)
        readonly bool[] alert = new bool[3];
        List<ChatItem> allList = new List<ChatItem>(), groupList = new List<ChatItem>(), allBuddyList = new List<ChatItem>(), blank = new List<ChatItem>(), cur;
        readonly Dictionary<long, List<ChatItem>> buddyLists = new Dictionary<long, List<ChatItem>>();
        long buddyUid; int buddySlot = -1;
        readonly List<string> history = new List<string>(); int iCurHistory;
        float chatTime = -100f, fSpamTime = -100f, fMessageCount;   // el original arranca con 0 (Time.time ya era grande al entrar al juego)
        bool dragging; Vector2 dragStart; float dragW, dragH;
        TouchScreenKeyboard kb;

        BitmapFont chalet, jeffe; bool fontsTried;
        readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>(); readonly HashSet<string> missing = new HashSet<string>();
        const string TexPath = "OpenFusion/UI/Textures/";
        GUIStyle sTab, sTabSel, sBox, sEntry, sMenuBtn, sEmoteBtn, sBlue, sFieldClosed, sField, sNull, sCorner, sCornerSel, fbMeasure;
        GUISkin barSkin; bool stylesBuilt;

        // ---------------- ciclo de vida ----------------
        void Awake()
        {
            Instance = this; cur = allList;
            try { winW = Mathf.Clamp(PlayerPrefs.GetFloat("Chatbox Width", 440f), MinW, MaxW); winH = Mathf.Clamp(PlayerPrefs.GetFloat("Chatbox Height", 150f), MinH, MaxH); } catch (Exception) { }
        }
        void OnDestroy() { Detach(); if (Instance == this) Instance = null; Typing = false; MouseOver = false; if (barSkin != null) Destroy(barSkin); }
        void OnDisable() { Typing = false; MouseOver = false; }

        public void Init(PlayerHud hud, WorldEntities ents, PacketDispatcher dispatcher)
        {
            Detach(); this.hud = hud; this.ents = ents; this.dispatcher = dispatcher;
            if (dispatcher != null) { dispatcher.Unhandled += OnPacket; dispatcher.PcEnter += OnPcEnter; dispatcher.SelfEnter += OnSelfEnter; }
            Fonts();
        }
        void Detach()
        {
            if (dispatcher != null) { dispatcher.Unhandled -= OnPacket; dispatcher.PcEnter -= OnPcEnter; dispatcher.SelfEnter -= OnSelfEnter; }
            dispatcher = null;
        }
        void Fonts() { if (fontsTried) return; fontsTried = true; chalet = BitmapFontLoader.Load(LabelFontJson); jeffe = BitmapFontLoader.Load(TabFontJson); }
        void Post(Action a) { queue.Enqueue(a); }

        // ---------------- red (hilo de red: solo encolar) ----------------
        void OnPcEnter(PcAppearance a) { if (a == null) return; int id = a.Id; string n = ((a.FirstName ?? "") + " " + (a.LastName ?? "")).Trim(); Post(() => { names[id] = n; }); }
        void OnSelfEnter(EnterSucc e)
        {
            if (e.Self == null) return; int id = e.Id; long uid = e.Self.Uid; string n = ((e.Self.FirstName ?? "") + " " + (e.Self.LastName ?? "")).Trim();
            // CnGuiChat.SetAvatar (l.362): vacia las listas al (re)crear el avatar propio
            Post(() => { selfId = id; selfUid = uid; selfName = n; names[id] = n; allList.Clear(); groupList.Clear(); allBuddyList.Clear(); buddyLists.Clear(); history.Clear(); iCurHistory = 0; filter = 0; selectTab = 0; buddyUid = 0; buddySlot = -1; pendingRecalc = true; });
        }
        void OnPacket(uint type, byte[] body) { Post(() => Handle(type, body)); }

        void Handle(uint type, byte[] b)
        {
            try
            {
                switch (type)
                {
                    case PacketIds.P_FE2CL_REP_SEND_FREECHAT_MESSAGE_SUCC: RecvFree(b, false); break;
                    case PacketIds.P_FE2CL_REP_SEND_MENUCHAT_MESSAGE_SUCC: RecvFree(b, true); break;
                    case PacketIds.P_FE2CL_REP_SEND_ALL_GROUP_FREECHAT_MESSAGE_SUCC: RecvGroup(b, false); break;
                    case PacketIds.P_FE2CL_REP_SEND_ALL_GROUP_MENUCHAT_MESSAGE_SUCC: RecvGroup(b, true); break;
                    case PacketIds.P_FE2CL_REP_SEND_BUDDY_FREECHAT_MESSAGE_SUCC: RecvBuddy(b, false); break;
                    case PacketIds.P_FE2CL_REP_SEND_BUDDY_MENUCHAT_MESSAGE_SUCC: RecvBuddy(b, true); break;
                    case PacketIds.P_FE2CL_PC_MOTD_LOGIN:   // sP_FE2CL_PC_MOTD_LOGIN (Pack 2): iType i8 (0) + szSystemMsg[512] (2)
                        if (b != null && b.Length >= 4) AddChatString(1, "Gamemaster: " + ChatPackets.Str(b, 2, 512), 0); break;
                    case PacketIds.P_FE2CL_ANNOUNCE_MSG:
                    case PacketIds.P_FE2CL_GM_REP_PC_ANNOUNCE:   // sP_FE2CL_ANNOUNCE_MSG (Pack 4): iAnnounceType i8 (0) + iDuringTime (4) + szAnnounceMsg[512] (8)
                        if (b != null && b.Length >= 10) { string am = ChatPackets.Str(b, 8, 512); if (NanoMessages.Instance != null) { int dt = BitConverter.ToInt32(b, 4); NanoMessages.Announce(am, dt > 1000 ? dt / 1000f : dt); } if (AnnounceToChat) AddChatString(1, am, 0); } break;
                    case PacketIds.P_FE2CL_REP_PC_CHANNEL_NUM:   // ReceiveSystem("current channel: N") (cnMainGame l.557)
                        if (b != null && b.Length >= 4) AddChatString(1, "current channel: " + BitConverter.ToInt32(b, 0), 0); break;
                    case PacketIds.P_FE2CL_REP_CHANNEL_INFO:     // ReceiveChannelInfo (l.873): sP_FE2CL_REP_CHANNEL_INFO {iCurrChannelNum, iChannelCnt} + sChannelInfo[] {iChannelNum, iCurrentUserCnt}
                        if (b != null && b.Length >= 8)
                        {
                            var sb = new StringBuilder("current channel: " + BitConverter.ToInt32(b, 0) + "\n"); int n = BitConverter.ToInt32(b, 4);
                            for (int i = 0; i < n && 16 + i * 8 <= b.Length; i++) sb.Append(BitConverter.ToInt32(b, 8 + i * 8) + " channel cur user: " + BitConverter.ToInt32(b, 12 + i * 8) + "\n");
                            AddChatString(1, sb.ToString(), 0);
                        }
                        break;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[chat] paquete " + type + ": " + ex.Message); }
        }

        bool FreeChatEnabled() { return hud == null || hud.Data == null || hud.Data.FreeChat; }   // bFreeChat = !(iSpecialState & 64)
        string NameOf(int pcId)
        {
            string n; if (names.TryGetValue(pcId, out n) && n.Length > 0) return n;
            if (pcId == selfId && selfName.Length > 0) return selfName; if (pcId == selfId && hud != null && hud.Data != null && hud.Data.Name.Length > 0) return hud.Data.Name;
            return "Player " + pcId;
        }
        string BuddyName(long uid) { string n = BuddyNameProvider != null ? BuddyNameProvider(uid) : null; return string.IsNullOrEmpty(n) ? "Player " + uid : n; }
        void Play(string s) { if (Sound != null) Sound(s); }

        // CnGuiChat.ReceiveChat (l.766) / ReceiveMenuChat (l.808)
        void RecvFree(byte[] b, bool menu)
        {
            int id, emote; string msg; if (!ChatPackets.ParseMsg(b, out id, out msg, out emote)) return;
            if (menu)
            {
                if (emote == 420) { if (ChatBubble != null) ChatBubble(id, msg, emote, 2); return; }   // burbuja de mision de un NPC (PrintName.QuestChat); no va al historial
                if (emote == 666) { MusicController.TrueOverride = msg ?? ""; MusicController.TrueOverrideLoop = id == 1; return; }
            }
            else if (!FreeChatEnabled()) return;
            if (id == selfId) Play("Outgoing_chat");
            if (IsBlockedPc != null && IsBlockedPc(id)) return;
            AddChatString(0, NameOf(id) + ": " + msg, 0);
            if (ChatBubble != null) ChatBubble(id, msg, emote, menu ? 1 : 0);
        }
        // CnGuiChat.ReceiveGroupChat (l.638) / ReceiveGroupMenuChat (l.609)
        void RecvGroup(byte[] b, bool menu)
        {
            int id, emote; string msg; if (!ChatPackets.ParseMsg(b, out id, out msg, out emote)) return;
            if (!menu && !FreeChatEnabled()) return; if (IsBlockedPc != null && IsBlockedPc(id)) return;
            AddChatString(3, NameOf(id) + ": " + msg, 0);
            if (filter != 1) alert[1] = true;
            if (id == selfId) Play("Outgoing_chat");
        }
        // CnGuiChat.ReceiveBuddyChat (l.687) / ReceiveBuddyMenuChat (l.730)
        void RecvBuddy(byte[] b, bool menu)
        {
            long from, to; string msg; int emote; if (!ChatPackets.ParseBuddy(b, out from, out to, out msg, out emote)) return;
            if (!menu && !FreeChatEnabled()) return; if (IsBlockedUid != null && IsBlockedUid(from)) return;
            bool incoming = selfUid != 0 ? to == selfUid : from != buddyUid;
            if (incoming)
            {
                AddChatString(2, BuddyName(from) + ": " + msg, from);
                if (filter != 2) alert[2] = true; Play("Incoming_Tell");
            }
            else { AddChatString(2, (selfName.Length > 0 ? selfName : NameOf(selfId)) + ": " + msg, to); Play("Outgoing_chat"); }
        }

        // ---------------- lineas (AddChatString l.917 / AddList l.961) ----------------
        List<ChatItem> BuddyListFor(long uid) { List<ChatItem> l; if (!buddyLists.TryGetValue(uid, out l)) { l = new List<ChatItem>(); buddyLists[uid] = l; } return l; }
        void AddChatString(int type, string str, long buddy)
        {
            if (string.IsNullOrEmpty(str)) return;
            string ts = TimeStamp ? "[" + DateTime.Now.ToString("HH:mm:ss") + "] " : "";
            if (type == 2) Play("Incoming_Tell");
            switch (type)
            {
                case 1: AddList(allList, 1, ts + str); AddList(groupList, 1, ts + str); break;
                case 2: AddList(allBuddyList, 2, ts + str); AddList(BuddyListFor(buddy), 2, ts + str); AddList(allList, 2, ts + "[Buddy] " + str); break;
                case 3: AddList(groupList, 3, ts + str); AddList(allList, 3, ts + "[Group] " + str); break;
                case 0: AddList(allList, 0, ts + "[Local] " + str); break;
                default: AddList(allList, type, ts + str); break;
            }
            if (cur != null && cur.Count > 0) snapBottom = true;   // original: scrollpos.y += ultimo.Height + ultimo.Offset (el ScrollView lo recorta al final)
        }
        int Measure(string s)
        {
            float w = winW - 24f - 24f;   // chat_listRect.width - 24, con chat_listRect.width = ancho de la caja - 24
            if (chalet != null) return Mathf.FloorToInt(chalet.Height(s, w));
            if (fbMeasure != null) return Mathf.FloorToInt(fbMeasure.CalcHeight(new GUIContent(s), w));
            return 14;
        }
        void AddList(List<ChatItem> list, int type, string s)
        {
            if (list == null) return;
            list.Add(new ChatItem(s, Measure(s), 0, type));
            if (list.Count > MaxChatList) list.RemoveAt(0);
            Reoffset(list); pendingRecalc = true;
        }
        static void Reoffset(List<ChatItem> list) { int o = 4; for (int i = 0; i < list.Count; i++) { list[i].Offset = o; o += list[i].Height; } }
        void RecalcAll()
        {
            Recalc(allList); Recalc(groupList); Recalc(allBuddyList); foreach (var kv in buddyLists) Recalc(kv.Value);
        }
        void Recalc(List<ChatItem> l) { for (int i = 0; i < l.Count; i++) l[i].Height = Measure(l[i].Text); Reoffset(l); }

        // ---------------- envio (Chat l.1751) ----------------
        void Send(byte[] pkt) { var s = SenderOverride != null ? SenderOverride : NpcUI.Sender; if (s != null) s(pkt); else Debug.LogWarning("[chat] sin Sender: paquete no enviado"); }
        // CheckSpam (l.3056)
        bool CheckSpam()
        {
            float num = Time.time - chatTime; fMessageCount = Mathf.Max(0f, fMessageCount - num);
            if ((fMessageCount += 1f) > 15f) fSpamTime = Time.time;
            if (Time.time - chatTime < 1f || Time.time - fSpamTime < 15f) { Play("Action_Failure01"); return false; }
            chatTime = Time.time; return true;
        }
        void Chat()
        {
            if (text.Length == 0 || !Ready()) return;
            if (!FreeChatEnabled()) { AddChatString(1, "Free chat is currently disabled.", 0); text = ""; caret = 0; return; }   // original: cuadro de mensaje 136
            if (ChannelWarp.TryCommand(text)) { text = ""; caret = 0; return; }
            if (!CheckSpam()) return;
            // (CheckCheatKey: comandos GM del cliente no portados; "/..." se envia como chat libre y lo interpreta el servidor)
            if (filter == 0) Send(ChatPackets.FreeChat(text));
            else if (filter == 2 && buddyUid != 0) Send(ChatPackets.BuddyFreeChat(text, buddyUid, buddySlot));
            else if (filter == 1) Send(ChatPackets.GroupFreeChat(text));
            history.Add(text); if (history.Count > MaxHistory) history.RemoveAt(0); iCurHistory = history.Count;
            text = ""; caret = 0;
        }

        // ---------------- Update (l.1812) ----------------
        bool Ready() { if (ents != null && ents.Self != null) return true; return hud != null && hud.Data != null && !string.IsNullOrEmpty(hud.Data.Name); }
        void ScrollToBottomOnOpen()
        {   // SetViewMenu (l.886)
            if (cur != null && cur.Count > 0) { var l = cur[cur.Count - 1]; scrollY = (l.Offset + l.Height) - (ListRect().height - 20f) + 22f; }
        }
        void StartKeyboard()
        {
            if (kb != null || !Application.isMobilePlatform || !UseNativeKeyboardOnMobile) return;
            try { kb = TouchScreenKeyboard.Open(text, TouchScreenKeyboardType.Default, true, false, false, false, "", MaxLen); } catch (Exception ex) { Debug.LogWarning("[chat] teclado: " + ex.Message); kb = null; }
        }
        void StopKeyboard() { if (kb != null) { try { kb.active = false; } catch (Exception) { } kb = null; } }

        void Update()
        {
            int n = 0; Action a; while (n++ < 64 && queue.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[chat] " + ex.Message); } }
            if (!Ready()) { MouseOver = false; return; }
            // seleccion de lista
            if (filter == 0) cur = allList;
            else if (filter == 1) { cur = groupList; if (GroupSize() <= 1) { filter = 0; cur = allList; } }   // l.1830: sin grupo vuelve a ALL (la pestana dibujada sigue siendo GROUP)
            else if (filter == 2) cur = buddyUid != 0 ? BuddyListFor(buddyUid) : blank;
            float s = GameOptions.UiScale; var m = Input.mousePosition;
            MouseOver = m.x >= 0f && m.x <= winW * s && m.y >= 0f && m.y <= winH * s;
            if (open)
            {
                if (kb != null)
                {
                    string t = kb.text ?? ""; if (t.Length > MaxLen) t = t.Substring(0, MaxLen); if (t != text) { text = t; caret = text.Length; }
                    var st = kb.status;
                    if (st == TouchScreenKeyboard.Status.Done) { Chat(); Close(); }
                    else if (st == TouchScreenKeyboard.Status.Canceled || st == TouchScreenKeyboard.Status.LostFocus) Close();
                }
                else if (EscapeCloses && Input.GetKeyDown(KeyCode.Escape)) { Close(); }
                if (open && kb == null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                {   // localized.ChatModify == eNone: enviar si hay texto y cerrar (ShowNanoMenu(false))
                    if (text.Length > 0) Chat(); Close();
                }
                if (open)
                {
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { iCurHistory--; if (iCurHistory <= 0) iCurHistory = 0; if (iCurHistory < history.Count) { text = history[iCurHistory]; caret = text.Length; } }
                    else if (Input.GetKeyDown(KeyCode.DownArrow)) { iCurHistory++; if (iCurHistory >= history.Count) iCurHistory = history.Count; if (iCurHistory < history.Count) { text = history[iCurHistory]; caret = text.Length; } }
                    if (MouseOver) { float wheel = Input.mouseScrollDelta.y; if (Mathf.Abs(wheel) > 0.001f) scrollY -= wheel * WheelStep; }
                }
            }
            else if (ControlMap.Down(ControlMap.K.Menu, false)) Open();   // ShowNanoMenu(true)
        }
        int GroupSize() { return GroupSizeProvider != null ? GroupSizeProvider() : 0; }
        int BuddyCount() { return BuddyCountProvider != null ? BuddyCountProvider() : 0; }

        // ---------------- recursos / estilos ----------------
        Texture2D T(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Texture2D t; if (tex.TryGetValue(names[i], out t)) { if (t != null) return t; continue; }
                t = Resources.Load<Texture2D>(TexPath + names[i]); tex[names[i]] = t;
                if (t != null) { t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp; return t; }
            }
            if (missing.Add(names[0])) Debug.LogWarning("[chat] falta textura " + TexPath + names[0]);
            return null;
        }
        static GUIStyle St(Texture2D n, Texture2D h, Texture2D a, RectOffset border)
        {
            var s = new GUIStyle(); s.normal.background = n; s.hover.background = h != null ? h : n; s.active.background = a != null ? a : n; s.focused.background = n;
            s.onNormal.background = n; s.onHover.background = h != null ? h : n; s.onActive.background = a != null ? a : n; s.onFocused.background = n;
            s.border = border; s.margin = new RectOffset(0, 0, 0, 0); s.padding = new RectOffset(0, 0, 0, 0); s.alignment = TextAnchor.MiddleCenter; return s;
        }
        void BuildStyles()
        {
            stylesBuilt = true; var none = new RectOffset(0, 0, 0, 0);
            sTab = St(T("chatTab_normal"), T("chatTab_over"), null, none);
            sTabSel = St(T("chatTab_selected"), null, null, none);
            sBox = St(T("darkenedChatArea"), null, null, new RectOffset(8, 8, 4, 4));
            sEntry = St(T("chatEntryArea"), null, null, new RectOffset(0, 20, 0, 0));
            sMenuBtn = St(T("menuChatButton"), T("menuChatButton_Over"), null, none);
            sEmoteBtn = St(T("emoteButton"), T("emoteButton_Over"), null, none);
            sBlue = St(T("blue_button_normal"), T("blue_button_over"), null, new RectOffset(6, 6, 4, 4));
            sFieldClosed = St(T("largeChatTextfield-normal"), null, null, new RectOffset(2, 2, 2, 2));
            sField = St(T("ff-textfield-normal"), null, null, new RectOffset(2, 2, 2, 2));
            sNull = St(T("chatNullBack"), null, null, none);
            var off = T("chatCornerDrag_off.png", "chatCornerDrag_off"); var on = T("chatCornerDrag_on.png", "chatCornerDrag_on");   // el fichero del proyecto se llama chatCornerDrag_off.png.png
            sCorner = St(off, on, on, none); sCornerSel = St(on, on, on, none);
            fbMeasure = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            // GUISkin del chat: barra vertical (skin FusionFallChatSkin: verticalScrollbar / Thumb / UpButton / DownButton)
            barSkin = Instantiate(GUI.skin); barSkin.hideFlags = HideFlags.HideAndDontSave;
            var bar = St(T("scroll_bar"), null, null, new RectOffset(2, 2, 4, 4)); bar.overflow = new RectOffset(0, 1, 0, 0); bar.fixedWidth = 16f; bar.stretchWidth = true;
            var th = St(T("scroll_Thumb"), null, null, new RectOffset(2, 2, 4, 4)); th.padding = new RectOffset(3, 3, 6, 6); th.overflow = new RectOffset(-2, -1, -2, -2); th.fixedWidth = 15f; th.stretchWidth = false; th.stretchHeight = true;
            var up = St(T("scroll_up"), null, null, none); up.fixedHeight = 12f; var dn = St(T("scroll_dn"), null, null, none); dn.fixedHeight = 12f;
            barSkin.verticalScrollbar = bar; barSkin.verticalScrollbarThumb = th; barSkin.verticalScrollbarUpButton = up; barSkin.verticalScrollbarDownButton = dn;
        }

        // ---------------- dibujo ----------------
        // Geometria de DoChatWindow (coordenadas locales de GUILayout.BeginArea(WindowRect)); el area parte de la esquina inferior izquierda.
        Rect ListRect() { return new Rect(24f, TabH + 4f, winW - 24f, (winH - TabH - EntryH) - 16f); }   // lastRect: x += 24, w -= 24, y += 4, h -= 16

        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            if (!Visible || !Ready()) return;
            if (NpcUI.Active) return;   // el original oculta el chat mientras se habla con un NPC
            if (!stylesBuilt) BuildStyles();
            Fonts();
            GUI.depth = 9;
            var oldM = GUI.matrix; var oldC = GUI.color; bool oldE = GUI.enabled; var oldSkin = GUI.skin;
            float s = GameOptions.UiScale;
            GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(0f, Screen.height));    // FFGUIUtility.ScaleAroundPivot(BottomLeft)
            GUI.color = Color.white; GUI.enabled = true;
            try { DrawWindow(s); if (open && BuddyListPanel.Instance != null && filter == 2) BuddyListPanel.Instance.Draw(winW); }
            finally { GUI.matrix = oldM; GUI.color = oldC; GUI.enabled = oldE; GUI.skin = oldSkin; }
        }

        void Txt(BitmapFont f, Rect r, string t, TextAnchor anchor, Color c, bool wrap = false, RectOffset pad = null)
        {
            if (string.IsNullOrEmpty(t)) return;
            if (f != null) { f.Draw(r, t, anchor, wrap, c, pad); return; }
            if (Event.current.type != EventType.Repaint) return;
            var st = new GUIStyle(GUI.skin.label) { alignment = anchor, fontSize = 12, wordWrap = wrap, clipping = TextClipping.Clip }; st.normal.textColor = c;
            if (pad != null) st.padding = pad; GUI.Label(r, t, st);
        }
        static bool Rep { get { return Event.current.type == EventType.Repaint; } }
        void Box(GUIStyle st, Rect r) { if (st != null && Rep) st.Draw(r, GUIContent.none, false, false, false, false); }

        void DrawWindow(float s)
        {
            var e = Event.current; float H = Screen.height;
            // --- arrastre de la esquina (OnOwnGUI l.431): usa Input.mousePosition con FixMousePositionForGUI(BottomLeft); aqui con la Y volteada correctamente ---
            Vector2 mouse = new Vector2(Input.mousePosition.x / s, -Input.mousePosition.y / s + H);
            if (dragging)
            {
                winW = Mathf.Clamp(dragW + (mouse.x - dragStart.x), MinW, MaxW); winH = Mathf.Clamp(dragH + (dragStart.y - mouse.y), MinH, MaxH); pendingRecalc = true;
            }
            if (pendingRecalc && Rep) { RecalcAll(); pendingRecalc = false; }
            float W = winW, Hh = winH;
            var area = new Rect(0f, H - Hh, W, Hh);      // WindowRect.y = Screen.height - alto
            GUI.BeginGroup(area);
            // --- pestanas: TabRect[0]=(-40,1) ALL, [2]=(95,1) BUDDY, [1]=(230,1) GROUP (si W < 390: -24 / -48) ---
            var tabs = new Rect[3];
            tabs[0] = new Rect(-40f, 1f, TabW, TabH); tabs[2] = new Rect(TabW - 40f, 1f, TabW, TabH); tabs[1] = new Rect(2f * TabW - 40f, 1f, TabW, TabH);
            if (W < 390f) { tabs[2].x -= 24f; tabs[1].x -= 48f; }
            for (int i = 2; i >= 0; i--)
            {
                if (i == selectTab) continue;
                bool hover = tabs[i].Contains(e.mousePosition) && !dragging;
                if (GUI.Button(tabs[i], GUIContent.none, sTab)) { SelectTabClick(i); }
                Txt(jeffe, tabs[i], TabName[i], TextAnchor.MiddleCenter, hover ? TabHoverCol : TabCol);
                if (alert[i]) { var at = T("chatAlert"); if (at != null) GUI.DrawTexture(new Rect(tabs[i].x + tabs[i].width - 35f, tabs[i].y, 14f, 14f), at); }
            }
            GUI.Button(tabs[selectTab], GUIContent.none, sTabSel);   // Toggle(LargeChatTabSelect): ya seleccionada, el clic no cambia nada
            Txt(jeffe, tabs[selectTab], TabName[selectTab], TextAnchor.MiddleCenter, TabSelCol);
            // --- caja darkenedChatArea (GUILayout.Box expandido; con el chat cerrado se dibuja "deshabilitada" = medio transparente) ---
            var box = new Rect(0f, TabH, W, Hh - TabH - EntryH);
            bool pe = GUI.enabled; GUI.enabled = open; Box(sBox, box); GUI.enabled = pe;
            // --- esquina de redimensionado (RepeatButton en (xMax-29, 18, 25, 25)) ---
            bool mouseDown = Input.GetMouseButton(0);
            if (dragging && (!mouseDown || !open)) { dragging = false; try { PlayerPrefs.SetFloat("Chatbox Width", winW); PlayerPrefs.SetFloat("Chatbox Height", winH); } catch (Exception) { } }
            if (open)
            {
                bool held = GUI.RepeatButton(new Rect(box.xMax - 29f, 18f, 25f, 25f), GUIContent.none, dragging ? sCornerSel : sCorner);
                if (!dragging && held) { dragStart = mouse; dragW = winW; dragH = winH; dragging = true; }
            }
            // --- chatEntryArea (-8, yMax - 10, ancho + 22, 40) ---
            Box(sEntry, new Rect(-8f, box.yMax - 10f, box.width + 22f, 40f));
            // --- fila de entrada: [6] menuChat 31x19 [4] emote 31x19 [3] campo 228x23; todo a y = alto - 26 (margen superior -3 del boton) ---
            float rowY = Hh - EntryH - 3f;
            if (GUI.Button(new Rect(6f, rowY, 31f, 19f), GUIContent.none, sMenuBtn)) { if (OnMenuChatButton != null) OnMenuChatButton(); }
            if (open && GUI.Button(new Rect(6f + 31f + 4f, rowY, 31f, 19f), GUIContent.none, sEmoteBtn)) { if (OnEmoteButton != null) OnEmoteButton(); }
            var field = new Rect(open ? 6f + 31f + 4f + 31f + 3f : 6f + 31f + 3f, rowY, 228f, EntryH);
            if (open)
            {   // GUI.Button(Rect(chat_bar_editRect.width, y, 69, 23), "SEND", BlueButton)  (x = 228 en coordenadas del area, como en el original)
                var sb = new Rect(228f, rowY, 69f, EntryH); bool hov = sb.Contains(e.mousePosition);
                if (GUI.Button(sb, GUIContent.none, sBlue)) { Play("Click"); Chat(); }
                Txt(jeffe, sb, "SEND", TextAnchor.MiddleCenter, hov && mouseDown ? new Color(0f, 0f, 0f, 0f) : (hov ? BlueBtnHoverCol : BlueBtnCol));
            }
            DrawField(field, e);
            // --- lista de mensajes ---
            DrawList(box, e);
            GUI.EndGroup();
        }

        void SelectTabClick(int i)
        {
            alert[i] = false; selectTab = i; filter = i; Play("Tab_Click01"); snapBottom = true; pendingRecalc = true;
            if (i != 2) { /* original: m_iBuddyIndex = -1 si SelectTab != 2 */ buddyUid = 0; buddySlot = -1; }
        }

        // Campo de texto. Abierto: GUI.TextField(position2, ChatString, 127) con el estilo textfield de la skin (fondo ff-textfield-normal, texto blanco con foco, cursor amarillo).
        void DrawField(Rect field, Event e)
        {
            GUI.BeginGroup(field);
            var pos2 = new Rect(0f, 0f, field.width - 15f, EntryH);
            if (open) pos2.width -= 55f;
            if (open)
            {
                var pad = new RectOffset(5, 5, 5, 5);
                if (chalet == null)
                {   // sin fuente de mapa de bits: TextField estandar de IMGUI
                    GUI.SetNextControlName("ChatString"); string t = GUI.TextField(pos2, text, MaxLen); if (t != text) { text = t; caret = text.Length; } GUI.FocusControl("ChatString");
                }
                else
                {
                    float tw = chalet.MaxLineWidth(text) + pad.left + pad.right;      // CalcMinMaxWidth(textField).max
                    if (tw > pos2.width) { pos2.x = pos2.width - tw; pos2.width = tw; }   // el campo se desplaza a la izquierda para mostrar el final
                    HandleKeys(e);
                    if (e.type == EventType.MouseDown && e.button == 0 && new Rect(0f, 0f, field.width, EntryH).Contains(e.mousePosition))
                    {
                        float px = e.mousePosition.x - pos2.x - pad.left; int best = 0; float bd = float.MaxValue;
                        for (int i = 0; i <= text.Length; i++) { float d = Mathf.Abs(chalet.Width(text.Substring(0, i)) - px); if (d < bd) { bd = d; best = i; } }
                        caret = best; blinkT0 = Time.unscaledTime; e.Use();
                    }
                    Box(sField, pos2);
                    Txt(chalet, pos2, text, TextAnchor.UpperLeft, Color.white, false, pad);
                    float blink = (Time.unscaledTime - blinkT0) % 1.06f;
                    if (Rep && blink < 0.53f)
                    {
                        float cx = pos2.x + pad.left + chalet.Width(text.Substring(0, Mathf.Clamp(caret, 0, text.Length)));
                        var oc = GUI.color; GUI.color = CursorCol; GUI.DrawTexture(new Rect(Mathf.Floor(cx), pos2.y + pad.top, 1f, chalet.LineHeight), Texture2D.whiteTexture); GUI.color = oc;
                    }
                }
            }
            else
            {   // GUI.Label(position2, "", "largeChatTextField") + texto gris en (10, 0)
                Box(sFieldClosed, pos2);
                Txt(chalet, new Rect(10f, 0f, pos2.width, pos2.height), "Press ENTER to access chat and menus.", TextAnchor.UpperLeft, new Color(LabelCol.r * 0.5f, LabelCol.g * 0.5f, LabelCol.b * 0.5f, 1f));
                if (ClickToOpen && GUI.Button(new Rect(0f, 0f, field.width, EntryH), GUIContent.none, GUIStyle.none)) Open();
            }
            GUI.EndGroup();
        }

        void HandleKeys(Event e)
        {
            if (e.type != EventType.KeyDown || kb != null) return;
            KeyCode k = e.keyCode; char c = e.character; bool ctrl = e.control || e.command;
            if (k == KeyCode.Return || k == KeyCode.KeypadEnter || c == '\n' || c == '\r') { e.Use(); return; }   // Enter se trata en Update
            blinkT0 = Time.unscaledTime;
            switch (k)
            {
                case KeyCode.Backspace: if (caret > 0 && caret <= text.Length) { text = text.Remove(caret - 1, 1); caret--; } e.Use(); return;
                case KeyCode.Delete: if (caret < text.Length) text = text.Remove(caret, 1); e.Use(); return;
                case KeyCode.LeftArrow: caret = Mathf.Max(0, caret - 1); e.Use(); return;
                case KeyCode.RightArrow: caret = Mathf.Min(text.Length, caret + 1); e.Use(); return;
                case KeyCode.Home: caret = 0; e.Use(); return;
                case KeyCode.End: caret = text.Length; e.Use(); return;
                case KeyCode.UpArrow: case KeyCode.DownArrow: case KeyCode.Escape: case KeyCode.Tab: e.Use(); return;   // historial / cierre: en Update
            }
            if (ctrl)
            {
                if (k == KeyCode.V) { string clip = GUIUtility.systemCopyBuffer; if (!string.IsNullOrEmpty(clip)) Insert(clip.Replace("\r", "").Replace("\n", " ")); }
                else if (k == KeyCode.X) { GUIUtility.systemCopyBuffer = text; text = ""; caret = 0; }
                else if (k == KeyCode.C) GUIUtility.systemCopyBuffer = text;
                e.Use(); return;
            }
            if (c >= ' ' && c != (char)127 && !(c >= 0xE000 && c <= 0xF8FF)) { Insert(c.ToString()); e.Use(); }
        }
        void Insert(string s)
        {
            caret = Mathf.Clamp(caret, 0, text.Length); int room = MaxLen - text.Length; if (room <= 0) return; if (s.Length > room) s = s.Substring(0, room);
            text = text.Insert(caret, s); caret += s.Length;
        }

        Color TypeColor(int t)
        {
            switch (t)
            {
                case 0: return GameOptions.Palette[GameOptions.Cur.ChatGeneral]; case 2: return GameOptions.Palette[GameOptions.Cur.ChatBuddy]; case 3: return GameOptions.Palette[GameOptions.Cur.ChatGroup]; case 4: return Color.white;
                case 5: return (Color.red + Color.yellow) / 2f; case 6: return Color.red; case 7: return Color.blue; default: return Color.red;
            }
        }

        void DrawList(Rect box, Event e)
        {
            var list = ListRect(); bool pe = GUI.enabled;
            // texto de ayuda cuando no hay amigos / grupo (l.1255): Rect(x + w/16, rect.height/2 - 15, w - w/8, 60)
            bool hint = false; var p3 = new Rect(box.x + box.width / 16f, list.height / 2f - 15f, box.width - box.width / 8f, 60f);
            if (filter == 2 && BuddyCount() < 1)
            {
                GUI.enabled = open; DrawHint(p3, "Click on a player and select MAKE A BUDDY from the menu, or select ADD in your BUDDY LIST.", "ChatBubbleIcon_buddy"); GUI.enabled = pe; hint = true;
            }
            else if (filter == 1 && GroupSize() < 1)
            {
                GUI.enabled = open; DrawHint(p3, "Click on a player and select INVITE TO GROUP from the menu.", "ChatBubbleIcon_group"); GUI.enabled = pe; hint = true;
            }
            if (hint) return;
            var l = cur ?? blank; float num3 = 0f; if (l.Count > 0) num3 = l[l.Count - 1].Offset + l[l.Count - 1].Height;
            float viewH = num3 + 5f, maxScroll = Mathf.Max(0f, viewH - list.height);
            if (snapBottom) { scrollY = float.MaxValue; snapBottom = false; }
            scrollY = Mathf.Clamp(scrollY, 0f, maxScroll);
            // BeginScrollView(rect, scroll, view): los items se recortan al rect y se desplazan -scrollY
            GUI.BeginGroup(list);
            if (Rep)
            {
                int i = 0; while (i < l.Count && l[i].Offset + l[i].Height <= scrollY) i++;
                for (; i < l.Count; i++)
                {
                    var it = l[i]; if (it.Offset >= scrollY + list.height) break;
                    var tc = TypeColor(it.Type); var col = new Color(LabelCol.r * tc.r, LabelCol.g * tc.g, LabelCol.b * tc.b, 1f);
                    Txt(chalet, new Rect(0f, it.Offset - scrollY, list.width - 16f, it.Height), it.Text, TextAnchor.UpperLeft, col, true);
                }
            }
            GUI.EndGroup();
            // VerticalScrollbar a la IZQUIERDA de la lista: Rect(rect.x - 21, rect.y, 16, rect.height)
            if (viewH > list.height)
            {
                GUI.enabled = open; var sk = GUI.skin; if (barSkin != null) GUI.skin = barSkin;
                float size = Mathf.Min(list.height, viewH);
                scrollY = GUI.VerticalScrollbar(new Rect(list.x - 21f, list.y, 16f, list.height), scrollY, size, 0f, viewH);
                GUI.skin = sk; GUI.enabled = pe;
            }
        }

        void DrawHint(Rect r, string msg, string icon)
        {
            Box(sNull, r);
            Txt(chalet, r, msg, TextAnchor.UpperLeft, NullCol, true, new RectOffset(50, 5, 7, 0));
            var ic = T(icon); if (ic != null && Rep) GUI.DrawTexture(new Rect(r.x + 10f, r.y - 15f + r.height / 2f, 29f, 29f), ic);
        }
    }
}
