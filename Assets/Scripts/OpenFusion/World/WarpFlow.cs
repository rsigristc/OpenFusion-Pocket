// WarpFlow: flujo de warp con NPC (NpcIconMode.NpcWarpOK / GameFrame REP_PC_WARP_USE_NPC_SUCC/FAIL). SIN COMPILAR NI PROBAR EN UNITY.
//  - Blocking: mientras dura la espera de 1,5 s (fWarpWaitTime) y hasta que llega SUCC/FAIL (o 6 s de seguridad) el avatar no manda movimiento
//    (mymove.bEnableSendPacket = false; GameFrame lo vuelve a poner a true en SUCC y FAIL). TransportWindow.Sending lo incluye y GameClient ya lo lee.
//  - SUCC (36 B: x,y,z,eIL,iItemSlotNum,Item(12),iCandy): iTaros = iCandy; si eIL != 4 se actualiza el slot (cnEvent 5,16,2).
//  - FAIL (4 B: iErrorCode): codigos 0 y 1 -> SendSystemMessageBox(173, ..., codigo) modal con OK.
// Hooks (NpcUI.cs): WarpFlow.BeginWait() en BeginWarp y WarpFlow.Sent() justo despues de Sender(ClientPackets.WarpUseNpc(..)) (ya puestos).
// W23: el aviso modal es ahora SysMessageBox (cnSystemMessageManager); WarpFlow crea ademas SysMessageBox, ChannelWarp y BuddyListPanel y expone TimeWarp (REQ_PC_TIME_TO_GO_WARP, Size 32 todo a 0).
//  - Blocking tambien incluye una caja de sistema abierta y una peticion de recall sin contestar (SocialMenu.RecallPending).
//  - TIME_TO_GO_WARP (cnGUINanocom.TimeWarp, boton "time machine" del nanocom en la Future Zone / cnUpsell): no tiene cuenta atras ni aviso propio en el original; solo envia el paquete (32 B, sin datos) y oculta el menu nano;
//    la respuesta del servidor es la del warp normal (WARP_USE_NPC_SUCC / FAIL), que ya trata este archivo.
using System;
using System.Collections.Concurrent;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class WarpFlow : MonoBehaviour
    {
        public static WarpFlow Instance;
        public static WarpFlow Ensure(GameObject host) { var w = host.GetComponent<WarpFlow>(); if (w == null) w = host.AddComponent<WarpFlow>(); Instance = w; return w; }
        /// <summary>El avatar no debe enviar movimiento (espera previa al warp, peticion en vuelo o aviso modal abierto).</summary>
        public static bool Blocking { get { return WorldBootstrap.Travelling || (Instance != null && (Instance.waitUntil > Time.unscaledTime || Instance.awaitUntil > Time.unscaledTime)) || SysMessageBox.Active || SocialMenu.RecallPending; } }
        /// <summary>Hay una caja de sistema (mensaje numerado) abierta.</summary>
        public static bool ModalOpen { get { return SysMessageBox.Active; } }

        /// <summary>cnGUINanocom: el boton de la maquina del tiempo solo aparece si CnCharSelectionMode.iPaymentFlag != 0, el avatar esta en la Future Zone y ((LevelExpansion == 0 y nivel == 4) o (LevelExpansion == 1 y nivel >= 10)); se desactiva en instancia (bInsMap) o sin estar a pie (MoveTYPE != 0).</summary>
        public static bool TimeWarpVisible(bool paid, bool inFutureZone, int level, int levelExpansion)
        {
            return paid && inFutureZone && ((levelExpansion == 0 && level == 4) || (levelExpansion == 1 && level >= 10));
        }
        /// <summary>localized.LevelExpansion del cliente original (constante del cliente localizado: 0 = tope de nivel antiguo, 1 = ampliado). Ajustar si la maquina del tiempo sale al nivel equivocado (verificar en Play).</summary>
        public static int LevelExpansion = 0;
        static float timeWarpAt = -10f;
        /// <summary>cnGUINanocom.TimeWarp: REQ_PC_TIME_TO_GO_WARP (Size 32: iNPC_ID, iWarpID, eIL1, iItemSlot1, eIL2, iItemSlot2, iPC_Level, iPayFlag, todo a 0). Devuelve false si no se pudo (sin conexion, instancia o ya enviado).</summary>
        public static bool TimeWarp()
        {
            if (RaceSystem.InsMap || Time.unscaledTime - timeWarpAt < 1f || NpcUI.Sender == null) return false;   // bTimeWarp: un solo envio por pulsacion
            timeWarpAt = Time.unscaledTime;
            NpcUI.Sender(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_TIME_TO_GO_WARP, 32)); Sent();
            return true;
        }

        public static void BeginWait() { if (Instance != null) Instance.waitUntil = Time.unscaledTime + 1.6f; }
        public static void Sent() { if (Instance != null) { Instance.waitUntil = 0f; Instance.awaitUntil = Time.unscaledTime + 6f; } }
        public static void Cancel() { if (Instance != null) { Instance.waitUntil = 0f; Instance.awaitUntil = 0f; } }

        PacketDispatcher disp; PlayerHud hud;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        float waitUntil, awaitUntil;

        public void Init(PacketDispatcher d, XdtTableScript t, PlayerHud h)
        {
            Bind(d); hud = h;
            SysMessageBox.Ensure(gameObject).Init(t);   // cajas de mensajes numerados
            ChannelWarp.Ensure(gameObject).Init(d);     // canales y cambio de shard
            BuddyListPanel.Ensure(gameObject).Init(d);  // lista de buddies + BUDDY_WARP
            var chat = ChatHud.Instance;   // la pestana BUDDY del chat necesita el numero y los nombres de buddies
            if (chat != null)
            {
                if (chat.BuddyCountProvider == null) chat.BuddyCountProvider = () => { var sm = SocialMenu.Instance; return sm != null ? sm.VisibleBuddyCount() : 0; };
                if (chat.BuddyNameProvider == null) chat.BuddyNameProvider = uid => { var sm = SocialMenu.Instance; return sm != null ? sm.BuddyNameByUid(uid) : null; };
            }
        }
        void Bind(PacketDispatcher d)
        {
            if (disp != null) { disp.WarpUseNpcSuccRaw -= OnSucc; disp.WarpFail -= OnFail; disp.Teleport -= OnRelocated; disp.Unhandled -= OnOther; }
            disp = d;
            if (disp != null) { disp.WarpUseNpcSuccRaw += OnSucc; disp.WarpFail += OnFail; disp.Teleport += OnRelocated; disp.Unhandled += OnOther; }
        }
        void OnOther(uint type, byte[] body) { if (type == PacketIds.P_FE2CL_REP_WARP_USE_RECALL_FAIL) q.Enqueue(Cancel); }
        void OnDestroy() { Bind(null); if (Instance == this) Instance = null; }

        // GOTO and shortened successful warp responses also confirm relocation.
        // Release the network wait without inventing absent inventory/currency data;
        // WorldBootstrap.Travelling continues to block movement while maps load.
        void OnRelocated(int x,int y,int z) { q.Enqueue(() => { Cancel(); Debug.Log("[warpflow] relocation confirmed; network wait cleared"); }); }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        void OnSucc(byte[] b)
        {
            if (b == null || b.Length < 12) return;
            if (b.Length < 36) { q.Enqueue(Cancel); return; }
            int eil = I(b, 12), slot = I(b, 16), candy = I(b, 32);
            var it = new ItemBase { Type = BitConverter.ToInt16(b, 20), Id = BitConverter.ToInt16(b, 22), Opt = I(b, 24), TimeLimit = I(b, 28) };
            q.Enqueue(() =>
            {
                waitUntil = 0f; awaitUntil = 0f;
                if (hud != null) hud.Data.Taros = candy;
                if (eil == 1 && slot >= 0 && slot < InventoryWindow.Items.Length) InventoryWindow.Items[slot] = it;
            });
        }

        void OnFail(int code)
        {
            q.Enqueue(() =>
            {
                waitUntil = 0f; awaitUntil = 0f;
                if (code == 0 || code == 1) SysMessageBox.Show(173, null, null, code.ToString());   // SendSystemMessageBox(173, ..., codigo)
            });
        }

        void Update() { Action a; while (q.TryDequeue(out a)) { try { a(); } catch (Exception e) { Debug.LogError("[warpflow] " + e); } } }
    }
}
