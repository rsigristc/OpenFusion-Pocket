// BuddyListPanel: ventana BUDDY LIST del chat (CnGuiChat.DoBuddyWindow / PreBuddyWarpFunc / BuddyWarp / WarpFail + cnBuddySystemManager BUDDY_WARP_*). SIN COMPILAR NI PROBAR EN UNITY.
// Geometria del prefab (bindings.json de CnGuiChat): BuddyRect (0,15,293,135), BuddyContentRect (-1,32,290,91), Remove_butRect (30,127,80,20), Warp_butRect (115,127,80,20), Add_butRect (200,127,80,20), fBuddyListHeight 16.
//   El grupo se coloca en (ancho de la ventana de chat, Screen.height - 135 - 15, 293, 150), a la derecha del chat y pegado al borde inferior; solo se dibuja con el chat abierto y la pestana BUDDY.
// Estilos de la skin FusionFallChatSkin: Buddy_Window, darkenedChatArea, BlueButton, redbutton2, BuddyItem, BuddySelect.
// Paquetes (cuerpo = Size; total = 4 + Size):
//   REQ_PC_BUDDY_WARP (Size 12): iBuddyPCUID i64 @0, iSlotNum i8 @8.   REP_PC_BUDDY_WARP_FAIL (12): iBuddyPCUID @0, iErrorCode @8 (3/4 -> mensaje 215 con el nombre; 6 -> mensaje 219 con "<cooltime>s").
//   REP_PC_BUDDY_WARP_SAME_SHARD_SUCC (struct vacio): si el warp no era hacia un miembro del grupo, REQ_PC_GROUP_LEAVE (cuerpo 1 B).   REP_PC_BUDDY_WARP_OTHER_SHARD_SUCC (16): ver ChannelWarp.
// Flujo: boton WARP -> si cooltime > 0 WarpFail(6) / si no mensaje 213 (nombre) -> [si grupo > 1 y el buddy no es del grupo: mensaje 220 (nombre)] -> BuddyWarp: envia, cooltime = 60 s, SetFocusOUt(true), sonido Buddy_Warp.
// Gancho (ChatHud.cs, no editable por este agente): dentro del OnGUI del chat, tras DrawWindow(s) y con el mismo GUI.matrix:  if (open && filter == 2 && BuddyListPanel.Instance != null) BuddyListPanel.Instance.Draw(winW);
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed class BuddyListPanel : MonoBehaviour
    {
        public static BuddyListPanel Instance;
        public static BuddyListPanel Ensure(GameObject host) { var b = host.GetComponent<BuddyListPanel>(); if (b == null) b = host.AddComponent<BuddyListPanel>(); Instance = b; return b; }
        /// <summary>cnOwnAvatarStatus/MoveTYPE == None: sin vuelo, tirolina, etc.</summary>
        public static Func<bool> OnFoot = () => !BroomFlight.Active && !VehicleRide.SelfRiding;

        static readonly Rect BuddyRect = new Rect(0f, 15f, 293f, 135f), ContentRect = new Rect(-1f, 32f, 290f, 91f), RemoveR = new Rect(30f, 127f, 80f, 20f), WarpR = new Rect(115f, 127f, 80f, 20f), AddR = new Rect(200f, 127f, 80f, 20f);
        const float ItemH = 16f;

        PacketDispatcher disp;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        int selSlot = -1, lastDraw = -100, stateVer; float refreshT, coolChk, focusAt; Vector2 scroll;
        int cooltime; bool warpToGroup;
        public int Cooltime { get { return cooltime; } }

        public void Init(PacketDispatcher d)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; }
            disp = d; if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnterRaw += OnEnterRaw; }
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnterRaw -= OnEnterRaw; } if (Instance == this) Instance = null; SysMessageBox.FocusOut = false; }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[buddy] envio: " + ex.Message); } } }

        // ---------------- red (hilo de red: solo encolar) ----------------
        void OnEnterRaw(byte[] b)
        {   // GameFrame ENTER_SUCC: m_iBuddyWarpCooltime = PCLoadData2CL.iBuddyWarpTime (@4+2524) - (int)(uiSvrTime (@2556) / 1000), minimo 0
            if (b == null || b.Length < 2564) return;
            int t = I(b, 2528) - (int)(BitConverter.ToUInt64(b, 2556) / 1000UL);
            q.Enqueue(() => { cooltime = t < 0 ? 0 : t; coolChk = Time.realtimeSinceStartup; });
        }
        void OnPacket(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_FAIL)
            {
                long uid = b != null && b.Length >= 8 ? BitConverter.ToInt64(b, 0) : 0L; int err = b != null && b.Length >= 12 ? I(b, 8) : 0;
                q.Enqueue(() => { SysMessageBox.FocusOut = false; Sfx.Ui("Action_Failure01"); warpToGroup = false; WarpFail(err, FindByUid(uid)); });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC)
            {
                q.Enqueue(() =>
                {
                    SysMessageBox.FocusOut = false;
                    if (warpToGroup) { warpToGroup = false; return; }
                    Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_GROUP_LEAVE, 1));   // func_P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC: sale del grupo
                });
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_OTHER_SHARD_SUCC)
            {
                q.Enqueue(() => { SysMessageBox.FocusOut = false; warpToGroup = false; });
            }
        }

        SocialMenu.Buddy FindByUid(long uid)
        {
            var soc = SocialMenu.Instance; if (soc == null) return null;
            if (uid != 0) for (int i = 0; i < SocialMenu.BuddySlots; i++) { var x = soc.BuddyAt(i); if (x != null && x.Uid == uid) return x; }
            return soc.BuddyAt(selSlot);
        }

        // ---------------- logica ----------------
        /// <summary>CnGuiChat.WarpFail(iErrCode): 3/4 -> mensaje 215 (nombre), 6 -> mensaje 219 ("<cooltime>s"); otro codigo: nada.</summary>
        void WarpFail(int code, SocialMenu.Buddy b)
        {
            int num = 0; string text = "";
            if (code == 3 || code == 4) { text = b != null ? b.MsgName : ""; num = 215; }
            if (code == 6) { text = cooltime + "s"; num = 219; }
            if (num > 0) SysMessageBox.Show(num, null, null, text);
        }
        void WarpClicked(SocialMenu.Buddy b)
        {
            if (cooltime > 0) { WarpFail(6, b); return; }
            SysMessageBox.Show(213, () => PreBuddyWarp(b), null, b.MsgName);
        }
        void PreBuddyWarp(SocialMenu.Buddy b)
        {
            var soc = SocialMenu.Instance;
            if (GroupState.Size > 1)
            {
                if (soc != null && soc.IsGroupMemberUid(b.Uid)) { warpToGroup = true; DoWarp(b); }   // IsGroupMember -> evento (14,30): bBuddyWarpToGroup
                else SysMessageBox.Show(220, () => DoWarp(b), null, b.First);
            }
            else DoWarp(b);
        }
        void DoWarp(SocialMenu.Buddy b)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_BUDDY_WARP, 12); BitConverter.GetBytes(b.Uid).CopyTo(p, 4); p[12] = (byte)b.Slot; Send(p);
            cooltime = 60; coolChk = Time.realtimeSinceStartup;
            SysMessageBox.FocusOut = true; focusAt = Time.realtimeSinceStartup; Sfx.Ui("Buddy_Warp");
        }
        void DeleteClicked(SocialMenu.Buddy b)
        {
            var soc = SocialMenu.Instance; int slot = b.Slot;
            string arg = b.NameCheck == 0 ? Loc.T("Player") + " " + b.Uid : b.First;
            SysMessageBox.Show(212, () => { if (soc != null) soc.BlockBuddy(slot); Select(null); }, null, arg);
        }
        void Select(SocialMenu.Buddy b)
        {
            selSlot = b != null ? b.Slot : -1;
            if (ChatHud.Instance != null) ChatHud.Instance.SetBuddyTarget(b != null ? b.Uid : 0L, b != null ? b.Slot : -1);
        }

        void Update()
        {
            Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[buddy] " + ex.Message); } }
            float now = Time.realtimeSinceStartup;
            if (cooltime > 0 && now - coolChk > 1f) { cooltime--; coolChk = now; }
            if (SysMessageBox.FocusOut && now - focusAt > 10f) SysMessageBox.FocusOut = false;   // seguridad: sin respuesta del servidor
            var soc = SocialMenu.Instance;
            if (soc != null && Time.frameCount - lastDraw <= 2)
            {
                if (soc.StateVersion != stateVer) { stateVer = soc.StateVersion; Select(null); }   // GET_BUDDY_STATE_SUCC con cambios: m_iBuddyIndex = -1
                bool any = false; for (int i = 0; i < SocialMenu.BuddySlots && !any; i++) { var x = soc.BuddyAt(i); any = x != null && x.Uid != 0; }
                if (any && now - refreshT > 10f) { refreshT = now; soc.RequestBuddyState(); }   // cnBuddySystemManager.Update: cada 10 s con la pestana BUDDY
            }
        }

        // ---------------- interfaz ----------------
        /// <summary>Llamar desde el OnGUI del chat (mismo GUI.matrix) con el chat abierto y la pestana BUDDY.</summary>
        public void Draw(float chatWidth)
        {
            var soc = SocialMenu.Instance; var e = Event.current; if (soc == null || e == null) return;
            if (Time.frameCount - lastDraw > 2) { selSlot = -1; scroll = Vector2.zero; }   // pestana recien mostrada: m_iBuddyIndex = -1
            lastDraw = Time.frameCount;
            var winSt = Skin.Get(Skin.Chat, "Buddy_Window"); var contSt = Skin.Get(Skin.Chat, "darkenedChatArea"); var blue = Skin.Get(Skin.Chat, "BlueButton"); var red = Skin.Get(Skin.Chat, "redbutton2");
            var item = Skin.Get(Skin.Chat, "BuddyItem"); var selSt = Skin.Get(Skin.Chat, "BuddySelect");
            bool pop = SysMessageBox.Active; var oc = GUI.color;
            var g = new Rect(chatWidth, Screen.height - BuddyRect.height - BuddyRect.y, BuddyRect.width, BuddyRect.height + BuddyRect.y);
            GUI.BeginGroup(g);
            try
            {
                Skin.Box(BuddyRect, winSt, " " + Loc.T("BUDDY LIST"));
                Skin.Box(ContentRect, contSt);
                var cur = soc.BuddyAt(selSlot); if (cur != null && (cur.Uid == 0 || cur.Blocked)) { cur = null; selSlot = -1; }
                bool has = cur != null;
                if (Skin.Button(RemoveR, Loc.T("DELETE"), red, has && !pop)) DeleteClicked(cur);
                bool canWarp = has && cur.State != 0 && OnFoot();
                if (Skin.Button(WarpR, Loc.T("WARP"), blue, canWarp && !pop)) WarpClicked(cur);
                Skin.Button(AddR, Loc.T("ADD"), blue, false);   // ADD (FIND_NAME_MAKE_BUDDY) pendiente de portar
                // lista
                var lr = new Rect(ContentRect.x + 5f, ContentRect.y + 5f, ContentRect.width - 10f, ContentRect.height - 10f);
                int count = 0; for (int i = 0; i < SocialMenu.BuddySlots; i++) { var x = soc.BuddyAt(i); if (x != null && x.Uid != 0 && !x.Blocked) count++; }
                var view = new Rect(0f, 0f, lr.width - 16f, count * ItemH);
                scroll = GUI.BeginScrollView(lr, scroll, view);
                int n = 0;
                for (int i = 0; i < SocialMenu.BuddySlots; i++)
                {
                    var b = soc.BuddyAt(i); if (b == null || b.Uid == 0 || b.Blocked) continue;
                    var r = new Rect(0f, ItemH * n, lr.width - 16f, ItemH - 0.5f);
                    Color col = b.State != 0 ? Color.yellow : Color.gray;
                    if (b.Slot == selSlot)
                    {
                        if (e.type == EventType.Repaint && selSt != null)
                        {
                            Skin.Bg(new Rect(0f, ItemH * n, lr.width, ItemH - 0.5f), selSt.N, selSt);
                            if (ItemH * n >= scroll.y && ItemH * n + ItemH <= scroll.y + lr.height) Skin.Bg(new Rect(-20f, ItemH * n, 38f, ItemH - 0.5f), selSt.N, selSt);
                        }
                        col = new Color(0.1f, 0.1f, 0.1f);
                    }
                    if (Skin.Button(r, "", item, !pop, true)) Select(b);
                    Skin.Label(r, "    " + b.ListName, item, col);
                    n++;
                }
                GUI.EndScrollView();
            }
            finally { GUI.EndGroup(); GUI.color = oc; }
        }
    }
}
