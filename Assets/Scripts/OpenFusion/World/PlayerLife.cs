using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    [DefaultExecutionOrder(250)]
    public sealed partial class PlayerLife : MonoBehaviour
    {
        static PlayerLife instance;
        readonly LegacyRespawnClock respawnClock = new LegacyRespawnClock();
        WorldEntities entities; PlayerHud hud; bool shown, pending; float groupReviveUntil; float requestedAt;
        public static bool Active => instance != null && instance.entities != null && instance.entities.Self != null && instance.hud != null && instance.hud.Data.Hp <= 0;
        public static PlayerLife Ensure(GameObject host) { var p = host.GetComponent<PlayerLife>(); return p != null ? p : host.AddComponent<PlayerLife>(); }
        public void Init(WorldEntities e, PlayerHud h) { instance = this; entities = e; hud = h; shown = pending = false; respawnClock.Clear(); }
        void OnDestroy() { if (instance == this) instance = null; }
        public static byte[] RespawnRequest(int xcom, int kind = 1)
        {
            if (kind != 1 && kind != 3 && kind != 4) throw new System.ArgumentOutOfRangeException(nameof(kind));
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_REGEN, 12);
            ClientPackets.PutInt(p, 4, kind); // ePCRegenType: Xcom=1, Phoenix=3, group Phoenix=4; never Unstick=5.
            ClientPackets.PutInt(p, 8, 0); ClientPackets.PutInt(p, 12, xcom); return p;
        }
        public void OfferGroupRevive() { if (Active) groupReviveUntil = Time.unscaledTime + 30; }
        public bool RequestRespawn(int kind = 1)
        {
            var client = GameClient.FindSelf;
            if (!Active || pending || client == null || !client.WorldConnected || NpcUI.Sender == null) return false;
            if (kind == 3 && !(GetComponent<NanocomHud>()?.HasPhoenix ?? false) || kind == 4 && Time.unscaledTime >= groupReviveUntil) return false;
            NpcUI.Sender(RespawnRequest(GetComponent<GameMenu>()?.NearestXCom() ?? 0, kind));
            pending = true; respawnClock.MarkRequested(); requestedAt = Time.unscaledTime; Debug.Log("[life] REQ_PC_REGEN type=" + kind); return true;
        }
        void Update()
        {
            if (!Active) { shown = pending = false; respawnClock.Clear(); groupReviveUntil = 0; return; }
            if (!shown) { shown = true; respawnClock.Enter(Time.unscaledTime); GameMenu.CloseWindow(); GameMenu.SetOpen(false, false); GetComponent<TouchControls>()?.CancelForSurfaceChange(); }
            if (pending && Time.unscaledTime - requestedAt > 10) pending = false; // Allow retry; never invent HP or success.
            if (!pending && respawnClock.Due(Time.unscaledTime)) RequestRespawn();
            if (!pending && !GameMenu.Open && GameMenu.Current == GameMenu.Win.None && (Input.GetKeyDown(KeyCode.Return) || ControlMap.Down(ControlMap.K.Jump, false))) RequestRespawn();
        }
        void OnGUI() => DrawLegacyRespawn();
    }
}
