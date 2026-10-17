// ChannelWarp: cambio de canal (WARP_CHANNEL) y cambio de shard por BUDDY_WARP (OTHER_SHARD). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente: CnGuiChat.CheckCheatKey (/chnum /chinfo /chwarp, solo cuentas con iUserLevel <= 50), CnGuiChat.ReceiveChannelInfo, cnMainGame.ReceivePacket (822083834..837),
//   GameFrame (REP_PC_LOADING_COMPLETE_SUCC: reenvio de WARP_CHANNEL tipo 1), cnBuddySystemManager.func_FE2CL_PC_BUDDY_WARP_OTHER_SHARD_SUCC.
// El cliente original NO tiene interfaz de canales: solo estos comandos de chat y el reenvio automatico tras un buddy warp a otro shard.
// Paquetes (cuerpo = Size; total = 4 + Size; un struct vacio mide 1 en Marshal.SizeOf):
//   REQ_PC_CHANNEL_NUM (318767243, 1) | REQ_CHANNEL_INFO (318767242, 1) | REQ_PC_WARP_CHANNEL (318767244, 8) {iChannelNum i32 @0, iWarpType i8 @4: 0 comando, 1 reenvio tras cambiar de shard}
//   REP_PC_CHANNEL_NUM (822083835, 4) {iChannelNum} -> linea de sistema "current channel: N"
//   REP_CHANNEL_INFO (822083834, 8 + 8 n) {iCurrChannelNum, iChannelCnt, sChannelInfo[n] {iChannelNum, iCurrentUserCnt}} -> linea de sistema multilinea
//   REP_PC_WARP_CHANNEL_FAIL (822083836, 4) {iErrorCode}: 1 "this code can only be used in freezone." / 2 "you're already in the channel." / 3 "the channel number is invalid." / 4 "the channel is full." -> mensaje del nanocom
//   REP_PC_WARP_CHANNEL_SUCC (822083837, struct vacio): borra el mundo (eventos 5,7 / 4,9 / 7,3 / 6,3); el servidor reenvia los alrededores.
//   REP_PC_BUDDY_WARP_OTHER_SHARD_SUCC (822083803, 16) {iBuddyPCUID i64 @0, iShardNum i8 @8, iChannelNum i32 @12}: m_iChannelNum = canal; borra el mundo y vuelve a la seleccion de personaje con ese shard;
//     al llegar REP_PC_LOADING_COMPLETE_SUCC del nuevo shard se envia REQ_PC_WARP_CHANNEL (canal, tipo 1).
// Gancho para el cambio de shard (GameClient.cs, no editable por este agente): PendingShard / PendingChannel son estaticos (sobreviven a la recarga de escena). Con ReloadOnShardChange (por defecto true) este componente recarga la escena
//   como GameMenu.SendExit (aproximacion: el flujo de seleccion de personaje vuelve a empezar); GameClient debe usar PendingShard (> 0) como shard en SelectCharacterAsync y ponerlo a 0 al entrar. Con ReloadOnShardChange = false solo se dispara ShardSwitch.
// Gancho para los comandos (ChatHud.Chat, no editable): if (ChannelWarp.TryCommand(text)) { text = ""; caret = 0; return; }   antes de enviar el chat libre.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed class ChannelWarp : MonoBehaviour
    {
        public static ChannelWarp Instance;
        public static ChannelWarp Ensure(GameObject host) { var c = host.GetComponent<ChannelWarp>(); if (c == null) c = host.AddComponent<ChannelWarp>(); Instance = c; return c; }

        /// <summary>cnBuddySystemManager.m_iChannelNum: canal al que volver tras cambiar de shard (0 = ninguno).</summary>
        public static int PendingChannel;
        /// <summary>iShardNum del OTHER_SHARD_SUCC (0 = ninguno). GameClient lo usa al reentrar.</summary>
        public static int PendingShard;
        public static bool ReloadOnShardChange = true;
        /// <summary>El original solo ejecuta /chnum /chinfo /chwarp si iUserLevel &lt;= 50; el port no conoce ese nivel, asi que lo valida el servidor. Poner a false para desactivar los comandos.</summary>
        public static Func<bool> CommandsAllowed = () => true;
        /// <summary>(shard, canal) tras OTHER_SHARD_SUCC, en el hilo principal.</summary>
        public static event Action<int, int> ShardSwitch;

        PacketDispatcher disp;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();

        public void Init(PacketDispatcher d)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (Instance == this) Instance = null; }

        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[canal] envio: " + ex.Message); } } }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        // ---------------- peticiones ----------------
        public static void RequestNum() { Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_CHANNEL_NUM, 1)); }
        public static void RequestInfo() { Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_CHANNEL_INFO, 1)); }
        /// <summary>REQ_PC_WARP_CHANNEL (Size 8). type 0 = comando, 1 = reenvio tras cambiar de shard.</summary>
        public static void Warp(int channel, int type)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_WARP_CHANNEL, 8); ClientPackets.PutInt(p, 4, channel); p[8] = (byte)type; Send(p);
        }

        /// <summary>CheckCheatKey: "/chnum", "/chinfo", "/chwarp N". Devuelve true si el texto era uno de esos comandos (no hay que enviarlo como chat).</summary>
        public static bool TryCommand(string text)
        {
            if (string.IsNullOrEmpty(text) || text[0] != '/') return false;
            string[] a = text.Split(' ');
            if (a[0] != "/chnum" && a[0] != "/chinfo" && a[0] != "/chwarp") return false;
            if (CommandsAllowed != null && !CommandsAllowed()) return false;
            if (a[0] == "/chnum") RequestNum();
            else if (a[0] == "/chinfo") RequestInfo();
            else { int ch; if (a.Length >= 2 && int.TryParse(a[1], out ch)) Warp(ch, 0); }
            return true;
        }

        // ---------------- mundo ----------------
        /// <summary>cnEvent (5,7) / (4,9) / (7,3) / (6,3): vacia jugadores y NPC remotos (PC_EXIT / NPC_EXIT sinteticos por el despachador). El servidor reenvia los alrededores.</summary>
        public static void ClearWorldNow() { if (Instance != null) Instance.ClearWorld(); }
        void ClearWorld()
        {
            var ents = FindObjectOfType<WorldEntities>(); if (ents == null || disp == null) return;
            var pcs = new List<int>(); foreach (var kv in ents.OtherPlayers) if (kv.Key != ents.SelfId) pcs.Add(kv.Key);
            var npcs = new List<int>(); foreach (var go in ents.NpcObjects) { if (go == null) continue; var ni = go.GetComponent<NpcInfo>(); if (ni != null) npcs.Add(ni.Id); }
            foreach (int id in pcs) { var pk = new byte[8]; BitConverter.GetBytes(PacketIds.P_FE2CL_PC_EXIT).CopyTo(pk, 0); BitConverter.GetBytes(id).CopyTo(pk, 4); disp.Dispatch(pk); }
            foreach (int id in npcs) { var pk = new byte[8]; BitConverter.GetBytes(PacketIds.P_FE2CL_NPC_EXIT).CopyTo(pk, 0); BitConverter.GetBytes(id).CopyTo(pk, 4); disp.Dispatch(pk); }
        }

        // ---------------- red (hilo de red: solo encolar) ----------------
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) b = new byte[0];
            switch (type)
            {
                case PacketIds.P_FE2CL_REP_CHANNEL_INFO:
                    if (b.Length < 8) return;
                    {
                        int cur = I(b, 0), cnt = Mathf.Clamp(I(b, 4), 0, Mathf.Max(0, (b.Length - 8) / 8)); var body = b;
                        q.Enqueue(() =>
                        {   // CnGuiChat.ReceiveChannelInfo -> AddChatString(1, texto)
                            string text = Loc.T("current channel") + ": " + cur + "\n";
                            for (int i = 0; i < cnt; i++) text += I(body, 8 + 8 * i) + " " + Loc.T("channel cur user") + ": " + I(body, 12 + 8 * i) + "\n";
                            ChatHud.AddEventString(1, text);
                        });
                    }
                    break;
                case PacketIds.P_FE2CL_REP_PC_CHANNEL_NUM:
                    if (b.Length < 4) return;
                    { int n = I(b, 0); q.Enqueue(() => { if (ChatHud.Instance != null) ChatHud.Instance.AddSystem("current channel: " + n); }); }
                    break;
                case PacketIds.P_FE2CL_REP_PC_WARP_CHANNEL_FAIL:
                    {
                        int code = b.Length >= 4 ? I(b, 0) : 0;
                        q.Enqueue(() =>
                        {
                            SysMessageBox.FocusOut = false;
                            string s = "";
                            if (code == 1) s = "this code can only be used in freezone."; else if (code == 2) s = "you're already in the channel."; else if (code == 3) s = "the channel number is invalid."; else if (code == 4) s = "the channel is full.";
                            if (s.Length > 0) NanoMessages.Post("", Loc.T(s), NanoMessages.Kind.User, 10f);   // cnGUINanocom.SendMessageBox(.., 7, ..): banner del nanocom
                        });
                    }
                    break;
                case PacketIds.P_FE2CL_REP_PC_WARP_CHANNEL_SUCC:
                    q.Enqueue(() => { SysMessageBox.FocusOut = false; ClearWorld(); });
                    break;
                case PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_OTHER_SHARD_SUCC:
                    if (b.Length < 16) return;
                    { int shard = (sbyte)b[8], ch = I(b, 12); q.Enqueue(() => OnOtherShard(shard, ch)); }
                    break;
                case PacketIds.P_FE2CL_REP_PC_LOADING_COMPLETE_SUCC:
                    q.Enqueue(() =>
                    {   // GameFrame: si m_iChannelNum > 0 -> REQ_PC_WARP_CHANNEL {canal, tipo 1}; m_iChannelNum = 0
                        if (PendingChannel > 0) { int ch = PendingChannel; PendingChannel = 0; PendingShard = 0; Warp(ch, 1); }
                    });
                    break;
            }
        }

        void OnOtherShard(int shard, int channel)
        {
            SysMessageBox.FocusOut = false;
            PendingChannel = channel; PendingShard = shard;
            ClearWorld();
            if (ShardSwitch != null) ShardSwitch(shard, channel);
            if (ReloadOnShardChange) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);   // aproximacion de CnCharSelectionMode.Play()
        }

        void Update() { Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[canal] " + ex.Message); } } }
    }
}
