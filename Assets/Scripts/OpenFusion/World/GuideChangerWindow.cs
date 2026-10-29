// GuideChangerWindow: port de cnGuideMode (modo "GUIDE CHANGER" / "WARP TO PAST"). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnGuideMode.cs, eGuideType.cs, NpcIconMode.StartGuideChage (iconos 7/18 de los NPC tipo 18..22 = cambiador de guia, tipo 23 = viaje al pasado),
// cnMissionManager.GetToChargeChangingGuide (coste en FM) / ChangeGuide (evento 12,30), GameFrame (REP_PC_CHANGE_MENTOR_SUCC / _FAIL).
// Paquetes: REQ_PC_CHANGE_MENTOR {short iMentor} (318767188), REP_PC_CHANGE_MENTOR_SUCC {short iMentor, short iMentorCnt, int iFusionMatter} (822083731),
//           REP_PC_CHANGE_MENTOR_FAIL {short iMentor, int iErrorCode} (822083732), y REQ_PC_TASK_STOP {int iTaskNum} para las misiones de guia al cambiar.
// Skin: GUISkin 1393 (FusionFallGuideSkin: box, button, toggle, label, window, BigButton, BigFont16, CancelButton, CloseButton, HelpButton, FrameWindow, compubox, iconbox, Imagewindow).
// Texturas: UiKit.Tex (Ben, dexter, Mojo, Edd, *_confirm, *_icon, member, select_bust, messicon_*, cost_bar, fmfpo, compu_icon, back) + TexLoader (TutorialAssets/panelback, TutorialAssets/select back, CharacterCreationAssets/charcreationBG).
// Pantallas: (1) OnSelect (solo si el jugador no tiene guia real: 0 o Computress=5): caja "READY TO FIGHT FUSE?" + WARP TO THE PAST; (2) OnChange: 4 guias, selector, coste, CHANGE/CHOOSE GUIDE y confirmacion.
// Limitaciones: no hay camara de NPC (EndSubTarget) ni mode de Cash Mall si PaymentFlag == 0; el coste en FM se calcula como cnMissionManager (el original decompilado devuelve 0 pero registra num7: se usa num7).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class GuideChangerWindow : MonoBehaviour
    {
        public static GuideChangerWindow Instance;
        public static GuideChangerWindow Ensure(GameObject host) { var w = host.GetComponent<GuideChangerWindow>(); if (w == null) w = host.AddComponent<GuideChangerWindow>(); return w; }
        /// <summary>true mientras la ventana (o su caja de mensaje) esta abierta: bloquear movimiento, ataques, atajos y ESC del menu.</summary>
        public static bool Active { get { return Instance != null && (Instance.open || Instance.msg != null); } }
        /// <summary>CnCharSelectionMode.iPaymentFlag (0 = cuenta gratuita). Con 0 el icono 18 abre el Cash Mall en el original; aqui se llama a OnNeedPayment.</summary>
        public static int PaymentFlag = 1;
        public static Action OnNeedPayment;
        public Func<string, Texture> TexLoader;

        const int SK = 1393;
        static readonly Rect WindowRect = new Rect(0, 0, 1036, 654), GWRect = new Rect(0, 0, 200, 124), CommentRect = new Rect(10, 35, 180, 50);
        static readonly string[] GuideNameKey = { "BEN TENNYSON", "DEXTER", "MOJO JOJO", "EDD" };
        static readonly int[] GuideNumber = { 4, 2, 3, 1 };
        static readonly Vector2[] GuidePos = { new Vector2(135f, 530f), new Vector2(390f, 523f), new Vector2(652f, 523f), new Vector2(897f, 540f) };
        static readonly string[] SelTex = { "Ben", "dexter", "Mojo", "Edd" }, ConfTex = { "ben_confirm", "dexter_confirm", "mojo_confirm", "Edd_confirm" }, IconTex = { "ben_icon__517", "dexter_icon", "mojo_icon__500", "Edd_icon" };

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud; MessageTableScript msgT;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        bool open, bSelect, bSend, bConfirm; float sendUntil;
        int iGuide, iOwnGuide, ownSelectGuide, guideChangeFM, changeCount, npcId;
        const int PastWarpNpcNumber = 1425;
        bool timeMachine, pastWarpPending, mentorPending; float pastWarpDeadline;
        Texture selectBack, changeBack, guideBack; bool texTried; float texRetry;
        string msg; string[] msgBtns = { "OK" }; Action msgYes, msgAny;

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; }
            disp = d; ents = e; tables = t; hud = h; msgT = t != null ? t.m_pMessageTable : null;
            if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnterRaw += OnEnterRaw; }
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; } if (Instance == this) Instance = null; }

        // ============================================================ apertura (NpcIconMode.StartGuideChage + cnGuideMode.InitMode) ============================================================
        /// <summary>npcType 18..22 -> iGuide = type-18+1 (1..5); npcType 23 -> 0. iconIndex 7 (guia) o 18 (viaje al pasado).</summary>
        public void Open(int npcId, int npcType, int iconIndex)
        {
            if (open) return;
            if (iconIndex == 18 && PaymentFlag == 0) { Debug.Log("[guia] cuenta gratuita: el original abre el Cash Mall"); if (OnNeedPayment != null) OnNeedPayment(); return; }
            timeMachine = npcType == 23; pastWarpPending = mentorPending = false;
            this.npcId = npcId; int arg = npcType == 23 ? 0 : npcType - 18 + 1; if (arg < 0 || arg > 5) arg = 0;   // NPC "Guide Changer" con tipo fuera de 18..22: sin guia preseleccionada
            bSend = false; iGuide = arg; iOwnGuide = InventoryWindow.Guide;
            if (iOwnGuide != 5) iGuide = iOwnGuide;
            if (timeMachine) iGuide = 0; // Resume a previously acknowledged first choice without changing/paying for a guide again.
            ownSelectGuide = 0; guideChangeFM = 0; bConfirm = false; bSelect = false;
            if (iGuide == 0) bSelect = false; else guideChangeFM = ComputeCost(iGuide);
            if (iOwnGuide == 5) { iOwnGuide = 0; ownSelectGuide = 0; }
            else { for (int i = 0; i < 4; i++) if (GuideNumber[i] == iGuide) ownSelectGuide = i + 1; }
            texTried = false; texRetry = 0f; LoadTextures(); msg = null; open = true; Sfx.Ui("Open_Screen");
        }

        void Exit(bool sound = false)
        {
            pastWarpPending = mentorPending = false;
            if (!open) return; open = false; bConfirm = false; bSend = false; selectBack = changeBack = guideBack = null; texTried = false;
            if (sound) Sfx.Ui("Close_Screen");
        }

        void LoadTextures()
        {
            if (TexLoader == null || Time.unscaledTime < texRetry) return; texRetry = Time.unscaledTime + 2f;
            if (changeBack == null) changeBack = Load("TutorialAssets/panelback");
            if (selectBack == null) selectBack = Load("CharacterCreationAssets/charcreationBG");
            if (guideBack == null) guideBack = Load("TutorialAssets/select back");
        }
        Texture Load(string path)
        {
            foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { var t = TexLoader(path + ext); if (t != null) return t; } catch (Exception) { } }
            return null;
        }

        // ============================================================ coste (cnMissionManager.GetToChargeChangingGuide) ============================================================
        static int Lvl(MissionElement m) { return m.m_iCTRReqLvMin > 0 ? m.m_iCTRReqLvMin : m.m_iKorStReqLvlMin; }
        int ComputeCost(int target)
        {
            if (target <= 0 || target >= 6) return -1;
            int cur = InventoryWindow.Guide; if (cur == target) return 0;
            var gt = tables != null ? tables.m_pGuideTable : null; if (gt == null || gt.m_pGuideData == null || target >= gt.m_pGuideData.Length || gt.m_pGuideData[target] == null) return -1;
            var val = gt.m_pGuideData[target];
            var mt = ents != null ? ents.MissionTable : null; var ms = ents != null ? ents.Missions : null; if (mt == null || mt.m_pMissionData == null || ms == null) return 0;
            var firstOf = new Dictionary<int, MissionElement>();
            foreach (var m in mt.m_pMissionData) { if (m == null) continue; if (!firstOf.ContainsKey(m.m_iHMissionID)) firstOf[m.m_iHMissionID] = m; }
            int num3 = 1, num4 = 1; bool flag = false;
            foreach (int id in ms.CompletedIds)
            {
                MissionElement me; if (!firstOf.TryGetValue(id, out me)) continue;
                if (me.m_iCSTReqGuide == cur) { int l = Lvl(me); if (num3 < l) num3 = l; }
                if (val.m_iQuest != null) for (int k = 0; k < val.m_iQuest.Length; k++)
                    {
                        if (val.m_iQuest[k] != id) continue; int l = Lvl(me); flag = true;
                        if (num4 < l) { num4 = l; if (num4 < 7) flag = false; }
                        break;
                    }
            }
            if (num4 == 1) num4 = 4; else if (flag) num4++;
            int num7 = num3 - num4 + 1; if (num7 <= 0) num7 = 1;
            return changeCount * num4 * 100 / num7;
        }

        // ============================================================ red ============================================================
        void OnEnterRaw(byte[] b)
        {   // sPCLoadData2CL.iMentorCount (short) en +88; el cuerpo trae iID (4) delante
            if (b == null || b.Length < 4 + 90) return; int v = BitConverter.ToInt16(b, 4 + 88); q.Enqueue(() => { changeCount = v; });
        }

        void SendSelectGuide(int i)
        {
            if (i == iOwnGuide) { Msg(155); return; }
            bSend = mentorPending = true; sendUntil = Time.unscaledTime + 10f;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_CHANGE_MENTOR, 2); BitConverter.GetBytes((short)i).CopyTo(p, 4); Send(p);
        }
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[guia] envio: " + ex.Message); } } }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_SUCC && b.Length >= 8)
            {
                short mentor = BitConverter.ToInt16(b, 0), cnt = BitConverter.ToInt16(b, 2); int fm = BitConverter.ToInt32(b, 4);
                q.Enqueue(() =>
                {
                    bool requested = mentorPending; mentorPending = false;
                    if (hud != null && hud.Data != null) hud.Data.FusionMatter = fm;
                    changeCount = cnt; InventoryWindow.Guide = mentor; if (ents != null && ents.QuestData != null) ents.QuestData.Mentor = mentor;
                    if (!requested) return; // Synchronize a late acknowledgement, but never initiate travel after cancellation.
                    bSend = false;
                    if (cnt == 1 || timeMachine) { iOwnGuide = mentor; iGuide = 0; bSelect = false; RequestPastWarp(); }
                    else { StopGuideMissions(mentor); Exit(true); }  // evento (12,30) ChangeGuide
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_CHANGE_MENTOR_FAIL)
            {
                q.Enqueue(() => { if (!mentorPending) return; mentorPending = false; bSend = false; MsgThen(154, () => Exit()); });   // SendSystemMessageBox(154, "Exit")
            }
        }

        void RequestPastWarp()
        {
            pastWarpPending = true; pastWarpDeadline = Time.unscaledTime + 10f;
            bSend = true; sendUntil = pastWarpDeadline + 1f;
            ContinuePastWarp();
        }
        void ContinuePastWarp()
        {
            if (!pastWarpPending) return;
            int reason = -1;
            if (NpcUI.Instance != null && NpcUI.Instance.TryWarpNpcNumber(PastWarpNpcNumber, out reason))
            { Debug.Log("[guia] first-choice continuation: original past warp requested"); Exit(); return; }
            if (reason < 0 && Time.unscaledTime < pastWarpDeadline) return; // NPC list may still be streaming.
            pastWarpPending = false; bSend = false;
            if (reason > 0) { Msg(reason); return; }
            msg = "The time-travel destination is not available yet. Stay near the Time Machine and try again.";
            msgBtns = new[] { "OK" }; msgYes = msgAny = null;
        }
        void ChoosePast()
        {
            if (iOwnGuide >= 1 && iOwnGuide <= 4) RequestPastWarp();
            else bSelect = true;
        }

        /// <summary>cnMissionManager.ChangeGuide: detiene las misiones activas de guia (m_iHMissionType == 1) cuyo guia requerido ya no es el actual.</summary>
        void StopGuideMissions(int newGuide)
        {
            try
            {
                var mt = ents != null ? ents.MissionTable : null; if (mt == null || mt.m_pMissionData == null) return;
                var byTask = new Dictionary<int, MissionElement>(); foreach (var m in mt.m_pMissionData) if (m != null) byTask[m.m_iHTaskID] = m;
                var running = new List<int>(); bool got = false;
                if (ents.Missions != null)
                {
                    var f = typeof(MissionState).GetField("activeTasks", BindingFlags.NonPublic | BindingFlags.Instance);
                    var hs = f != null ? f.GetValue(ents.Missions) as HashSet<int> : null; if (hs != null) { running.AddRange(hs); got = true; }
                }
                if (!got && ents.QuestData != null) foreach (var t in ents.QuestData.Running) if (t > 0) running.Add(t);
                foreach (int t in running)
                {
                    MissionElement me; if (!byTask.TryGetValue(t, out me)) continue;
                    if (me.m_iHMissionType == 1 && me.m_iCSTReqGuide != newGuide)
                    { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TASK_STOP, 4); BitConverter.GetBytes(t).CopyTo(p, 4); Send(p); }
                }
            }
            catch (Exception ex) { Debug.LogWarning("[guia] ChangeGuide: " + ex.Message); }
        }

        // ============================================================ mensajes (cnSystemMessageManager) ============================================================
        string Message(int id) { return msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null ? msgT.m_pMessageData[id].m_szString : "(message " + id + ")"; }
        void Msg(int id) { MsgThen(id, null); }
        void MsgThen(int id, Action then)
        {
            int bt = 1; if (msgT != null && msgT.m_pMessageData != null && id >= 0 && id < msgT.m_pMessageData.Length && msgT.m_pMessageData[id] != null) bt = msgT.m_pMessageData[id].m_iButtonType;
            msg = Loc.T(Message(id)); msgBtns = bt == 2 ? new[] { "OK", "CANCEL" } : bt == 3 ? new[] { "YES", "NO" } : new[] { "OK" }; msgYes = null; msgAny = then; Sfx.Ui("Action_Failure01");
        }

        // ============================================================ bucle ============================================================
        bool HelpOpen { get { return GameMenu.Current == GameMenu.Win.Guide; } }
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[guia] " + ex.Message); } }
            if (!open) return;
            ContinuePastWarp();
            if (bSend && Time.unscaledTime > sendUntil) bSend = false;
            if (changeBack == null || selectBack == null || guideBack == null) LoadTextures();
            if (!HelpOpen && !bSend && msg == null && Input.GetKeyDown(KeyCode.Escape)) { Exit(true); }
        }

        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }
        static SkinStyle St(string n) { return Skin.Get(SK, n); }
        static Texture2D Tx(string n) { return UiKit.Tex(n); }
        static void Pic(Rect r, Texture t) { if (t != null && Event.current.type == EventType.Repaint) GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true); }

        void OnGUI()
        {
            if ((!open && msg == null) || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            if (open && HelpOpen && msg == null) return;
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (open)
                {
                    bool select = iGuide == 0 && !bSelect; var bg = select ? selectBack : changeBack;
                    if (e.type == EventType.Repaint)
                    {
                        GUI.color = new Color(0f, 0f, 0f, bg != null ? 1f : 0.75f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                        if (bg != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), bg, ScaleMode.ScaleToFit);
                    }
                    GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height));
                    bool en = !bSend && msg == null;
                    if (select) OnSelect(e, en); else OnChange(e, en);
                    GUI.EndGroup();
                }
                if (msg != null)
                {
                    GUI.BeginGroup(new Rect(-WindowRect.width * 0.5f, -WindowRect.height * 0.5f, WindowRect.width, WindowRect.height)); DrawMsg(e); GUI.EndGroup();
                }
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        // ---- OnSelect: "READY TO FIGHT FUSE?" ----
        void OnSelect(Event e, bool en)
        {
            var rect = new Rect((WindowRect.width - 520f) / 2f, (WindowRect.height - 164f) / 2f, 520f, 164f);
            var mb = Tx("member"); if (mb != null) Pic(new Rect((WindowRect.width - mb.width) / 2f, rect.y - mb.height, mb.width, mb.height), mb);
            GUI.BeginGroup(rect);
            Skin.Box(new Rect(0, 0, 520, 164), St("box"), "");
            Pic(new Rect(60, 27, 62, 62), Tx("messicon_timeMachine"));
            if (Skin.Button(new Rect(280, 124, 200, 25), Loc.T("WARP TO THE PAST"), St("button"), en, true)) { Sfx.Ui("Yes_Button"); ChoosePast(); }
            else if (Skin.Button(new Rect(44, 124, 150, 25), Loc.T("CANCEL"), St("CancelButton"), en, true)) { Sfx.Ui("No_Button"); Exit(); }
            GUI.EndGroup();
            DialogText(rect, "READY TO FIGHT FUSE?", "Once you warp to the past, you will be unable to return to this area. Any active mission you have now will be removed from your journal.");
        }

        /// <summary>GUILayout.BeginArea(rect.x+140, rect.y+20, w-180, h-60): titulo (label), 10 px, cuerpo (imagewindow).</summary>
        void DialogText(Rect rect, string title, string body)
        {
            var area = new Rect(rect.x + 140f, rect.y + 20f, rect.width - 180f, rect.height - 60f);
            GUI.BeginGroup(area);
            Skin.Label(new Rect(0, 0, area.width, 24), Loc.T(title), St("label"));
            Skin.Label(new Rect(0, 34, area.width, area.height - 34), body, St("Imagewindow"));
            GUI.EndGroup();
        }

        // ---- OnChange: selector de guia ----
        void OnChange(Event e, bool en)
        {
            var win = new Rect(0, 0, WindowRect.width, WindowRect.height);
            Pic(win, guideBack);
            Skin.Box(new Rect(12, 12, 365, 110), St("compubox"), Loc.T("COMPUTRESS") + ":");
            Skin.Box(new Rect(22, 22, 64, 64), St("iconbox")); Pic(new Rect(22, 22, 64, 64), Tx("compu_icon"));
            var big = St("BigFont16"); var img = St("Imagewindow");
            if (iGuide == 0)
            {
                Skin.Label(new Rect(0, 45, win.width, 30), Loc.T("CHOOSE A GUIDE") + ":", big, Color.yellow);
                Skin.Label(new Rect(95, 35, 265, 70), Loc.T("Four different guides await your arrival in the past. Each will aid you in your quest to defeat Fuse, as well as offer unique missions and items. So choose wisely."), img);
            }
            else
            {
                Skin.Label(new Rect(0, 45, win.width, 30), Loc.T("CHOOSE A NEW GUIDE") + ":", big, Color.yellow);
                Skin.Label(new Rect(95, 35, 265, 70), Loc.T("If you change your guide, you will start receiving new guide missions. Any active guide missions you have now will be removed from your journal. Any guide items that you own can no longer be equipped."), img);
            }
            var tog = St("toggle"); var gt = tables != null ? tables.m_pGuideTable : null;
            for (int i = 0; i < 4; i++)
            {
                var gw = GWRect; gw.x = GuidePos[i].x - 100f; gw.y = 166f;
                Skin.Box(gw, St("window"), Loc.T(GuideNameKey[i]));
                if (GuideNumber[i] == iOwnGuide && iGuide != 0)
                { var p8 = gw; p8.y -= 15f; p8.height += 15f; Skin.Box(p8, St("FrameWindow"), Loc.T("CURRENT GUIDE")); }
                GUI.BeginGroup(gw);
                bool sel = i + 1 == ownSelectGuide; var tr = new Rect(GWRect.width - 30f, 5f, 22f, 22f);
                if (e.type == EventType.Repaint && tog != null) { var tt = sel ? tog.ON : tog.N; if (tt != null) GUI.DrawTexture(tr, tt, ScaleMode.StretchToFill, true); }
                if (en && !bConfirm && !sel && e.type == EventType.MouseDown && e.button == 0 && tr.Contains(e.mousePosition))
                { e.Use(); Sfx.Button(); ownSelectGuide = i + 1; guideChangeFM = ComputeCost(GuideNumber[i]); }
                if (gt != null && gt.m_pGuideData != null && GuideNumber[i] < gt.m_pGuideData.Length && gt.m_pGuideData[GuideNumber[i]] != null)
                {
                    int si = gt.m_pGuideData[GuideNumber[i]].m_iSelect; string s = gt.m_pGuideStringData != null && si >= 0 && si < gt.m_pGuideStringData.Length && gt.m_pGuideStringData[si] != null ? gt.m_pGuideStringData[si].m_pszString : null;
                    if (s != null) Skin.Label(CommentRect, Loc.T(s), img);
                }
                GUI.EndGroup();
                { var ic = Tx(IconTex[i]); if (ic != null) Pic(new Rect(gw.x - ic.width / 2f, gw.y + 15f - ic.height / 2f, ic.width, ic.height), ic); }   // referencia Retrobution: icono COMPLETO centrado en el borde izquierdo de la cabecera (el original lo dibuja en x=-w/2 del grupo, aqui fuera del recorte)
                if (i + 1 == ownSelectGuide) { var se = Tx("select_bust"); if (se != null) Pic(new Rect(GuidePos[i].x - se.width / 2, GuidePos[i].y - se.height, se.width, se.height), se); }
                var gb = Tx(SelTex[i]); if (gb != null) Pic(new Rect(GuidePos[i].x - gb.width / 2, GuidePos[i].y - gb.height, gb.width, gb.height), gb);
            }
            if (iGuide != 0)
            {
                Pic(new Rect(0, 572, win.width, 20), Tx("cost_bar"));
                string text = Loc.T("cost") + " : " + guideChangeFM; var lab = St("label"); float vx = lab != null && lab.Font != null ? lab.Font.Width(text) : 80f;
                Skin.Label(new Rect(win.width / 2f - vx / 2f - 15f, 572, vx + 10f, 20), text, lab, Color.black);
                Pic(new Rect(win.width / 2f + vx - 15f, 567, 31, 31), Tx("fmfpo"));
                if (Skin.Button(new Rect(840, 605, 180, 35), Loc.T("CHANGE GUIDE"), St("BigButton"), en && !bConfirm, true)) { Sfx.Ui("Yes_Button"); bConfirm = true; }
            }
            else if (Skin.Button(new Rect(840, 605, 180, 35), Loc.T("CHOOSE GUIDE"), St("BigButton"), en && !bConfirm && ownSelectGuide > 0, true)) { Sfx.Ui("Yes_Button"); bConfirm = true; }
            if (Skin.Button(new Rect(10, 617, 150, 25), Loc.T("cancel"), St("CancelButton"), en && !bConfirm, true)) { Sfx.Ui("No_Button"); Exit(); return; }
            if (Skin.Button(new Rect(win.width - 42f, 10, 32, 32), "", St("CloseButton"), en && !bConfirm)) { Exit(); return; }
            if (Skin.Button(new Rect(987, 530, 32, 32), "", St("HelpButton"), en && !bConfirm)) ClickHelp();
            if (ownSelectGuide > 0 && bConfirm) DrawConfirm(e, en);
        }

        /// <summary>Caja "GUIDE CONFIRMATION" / "ALERT" (520x164 centrada, retrato de confirmacion encima).</summary>
        void DrawConfirm(Event e, bool en)
        {
            if (e.type == EventType.Repaint)
            { var bk = Tx("back"); GUI.color = new Color(1f, 1f, 1f, 0.5f); GUI.DrawTexture(new Rect(-1000f, -1000f, 3036f, 2654f), bk != null ? (Texture)bk : Texture2D.blackTexture); GUI.color = Color.white; }
            int ix = ownSelectGuide - 1; string name = Loc.T(GuideNameKey[ix]);
            var rect = new Rect((WindowRect.width - 520f) / 2f, (WindowRect.height - 164f) / 2f, 520f, 164f);
            var ct = Tx(ConfTex[ix]); if (ct != null) Pic(new Rect((WindowRect.width - ct.width) / 2f, rect.y - ct.height + 2f, ct.width, ct.height), ct);
            GUI.BeginGroup(rect);
            Skin.Box(new Rect(0, 0, 520, 164), St("box"), "");
            Pic(new Rect(60, 27, 62, 62), Tx("messicon_guide"));
            if (Skin.Button(new Rect(320, 124, 150, 25), Loc.T("CONFIRM"), St("button"), en, true)) { Sfx.Ui("Yes_Button"); bConfirm = false; SendSelectGuide(GuideNumber[ix]); }
            else if (Skin.Button(new Rect(44, 124, 150, 25), Loc.T("CANCEL"), St("CancelButton"), en, true)) { Sfx.Ui("No_Button"); bConfirm = false; }
            GUI.EndGroup();
            if (iGuide == 0) DialogText(rect, "GUIDE CONFIRMATION", Loc.T("Are you sure that you want to choose %s as your guide?").Replace("%s", name));
            else DialogText(rect, "ALERT", Loc.T("Are you sure you want to change your guide to %s? Remember, once you change guides, you will no longer be able to equip your current guide items, and any guide items that your currently have equipped will no longer be functional.").Replace("%s", name));
        }

        /// <summary>ClickHelp: FirstUse 44 -> pagina de ayuda (m_iHelpMain / m_iHelpSub) en la ventana Game Guide.</summary>
        void ClickHelp()
        {
            var ft = tables != null ? tables.m_pFirstUseTable : null;
            if (ft != null && ft.m_pFirstUseData != null && 44 < ft.m_pFirstUseData.Length && ft.m_pFirstUseData[44] != null) { GuideWindow.NextBig = ft.m_pFirstUseData[44].m_iHelpMain; GuideWindow.NextSmall = ft.m_pFirstUseData[44].m_iHelpSub; }
            GameMenu.OpenWindow(GameMenu.Win.Guide);
        }

        // ---- caja de mensaje (mismo aspecto que ModalWindow.DrawMsg) ----
        void DrawMsg(Event e)
        {
            var oc = GUI.color; if (e.type == EventType.Repaint) { GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(WindowRect, Texture2D.whiteTexture); GUI.color = oc; }
            var r = new Rect(300f, 220f, 436f, 200f); if (e.type == EventType.Repaint) { GUI.color = new Color(0.02f, 0.1f, 0.2f, 0.97f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = oc; }
            var lab = St("label"); if (lab != null) Skin.Label(new Rect(r.x + 15f, r.y + 15f, r.width - 30f, 120f), msg, lab, Color.white);
            float bw = 150f, gap = 20f, tot = msgBtns.Length * bw + (msgBtns.Length - 1) * gap, x = r.x + (r.width - tot) / 2f;
            for (int i = 0; i < msgBtns.Length; i++)
            {
                string key = msgBtns[i]; bool cancel = key == "CANCEL" || key == "NO";
                if (Skin.Button(new Rect(x + i * (bw + gap), r.y + 150f, bw, 28f), Loc.T(key), cancel ? St("CancelButton") : St("button"), true))
                { var y = msgYes; var any = msgAny; msg = null; msgYes = null; msgAny = null; if (!cancel && y != null) y(); if (any != null) any(); return; }
            }
        }
    }
}
