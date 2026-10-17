// InvenMode: animacion "computadora" del avatar con una ventana abierta (cnAvatarAnimation.SetInvenMode / SetInvenMotion + GameFrame.CreateGameMode/ChangeGameMode + PC_STATE_CHANGE / PC_SPECIAL_STATE_CHANGE). SIN COMPILAR NI PROBAR EN UNITY.
//   Propio: al abrirse una ventana (modos 6 UserEquip, 7 Mision, 8 Vendor, 9 Pc2pc, 11 Bank, 12 Option, 15 WorldMap, 18 Email, 20 Guide = GameMenu.Win salvo Quit) y sin animacion especial (escoba/dash):
//           clip "inven" en bucle (board_inven / scooter_inven con vehiculo), arma (AttachName 3) oculta y REQ_PC_SPECIAL_STATE_SWITCH { iPC_ID, iSpecialStateFlag = 16 } (Size 8, el servidor alterna el bit).
//           Al cerrarla se vuelve a enviar el mismo paquete, vuelve el reposo, se muestra el arma y se borra el holograma (AnimationEventHandler.DeleteCurrentObject).
//   Remotos:  PC_STATE_CHANGE { iPC_ID, sbyte iState } con iState == 6 -> inven; cualquier otro estado lo quita. PC_SPECIAL_STATE_CHANGE { iPC_ID, iReqFlag, iSpecialState }: bit 0x10 -> inven.
//   El holograma (cubos azules) lo crean los eventos particle/tag_p del propio clip "inven" (AnimEventSink); con Persist vive hasta que se sale del modo.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed class InvenMode : MonoBehaviour
    {
        public static InvenMode Instance;
        public static InvenMode Ensure(GameObject host) { var v = host.GetComponent<InvenMode>(); if (v == null) v = host.AddComponent<InvenMode>(); Instance = v; return v; }

        const int RightPistolSlot = 3;
        WorldEntities ents; PacketDispatcher disp; GameClient gc;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly HashSet<int> remote = new HashSet<int>();
        bool selfOn;
        /// <summary>true mientras el propio avatar esta en modo inventario.</summary>
        public static bool SelfActive { get { return Instance != null && Instance.selfOn; } }

        public void Init(WorldEntities e, PacketDispatcher d, GameClient g)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.PcEnter -= OnPcEnter; disp.PcExit -= OnPcExit; }
            if (ents != null) ents.SelfSpawned -= OnSelf;
            ents = e; disp = d; gc = g;
            if (disp != null) { disp.Unhandled += OnPacket; disp.PcEnter += OnPcEnter; disp.PcExit += OnPcExit; }
            if (ents != null) ents.SelfSpawned += OnSelf;
        }
        void OnDestroy()
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.PcEnter -= OnPcEnter; disp.PcExit -= OnPcExit; }
            if (ents != null) ents.SelfSpawned -= OnSelf;
            if (Instance == this) Instance = null;
        }
        void OnSelf(GameObject go, EnterSucc es) { selfOn = false; if (gc != null) { gc.InvenMode = false; } remote.Clear(); }
        void OnPcEnter(PcAppearance a) { if (a == null) return; int id = a.Id; q.Enqueue(() => remote.Remove(id)); }
        void OnPcExit(int id) { q.Enqueue(() => remote.Remove(id)); }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void OnPacket(uint type, byte[] b)   // hilo de red
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_PC_STATE_CHANGE && b.Length >= 5) { int id = I(b, 0); bool on = (sbyte)b[4] == 6; q.Enqueue(() => SetRemote(id, on)); }
            else if (type == PacketIds.P_FE2CL_PC_SPECIAL_STATE_CHANGE && b.Length >= 6) { int id = I(b, 0); bool on = (((sbyte)b[5]) & 0x10) != 0; q.Enqueue(() => SetRemote(id, on)); }
        }

        // ---------------- remotos ----------------
        void SetRemote(int id, bool on)
        {
            if (ents == null || id == ents.SelfId) return;
            var t = ents.TransformOfPlayer(id); if (t == null) return;
            var na = t.GetComponent<NpcAnimator>(); if (na == null) return;
            if (on == na.Inven) return;
            na.SetInven(on);
            if (on) remote.Add(id); else remote.Remove(id);
            if (!VehicleRide.IsRiding(id)) HideWeapon(t, on);   // iVehicle == 0 -> LinkObjectActive(3, !on)
        }
        public bool IsRemoteInven(int id) { return remote.Contains(id); }

        /// <summary>ActorSkinCombiner.LinkObjectActive(AttachName 3 = arma, !hide).</summary>
        public static void HideWeapon(Transform holder, bool hide)
        {
            if (holder == null) return;
            foreach (var tg in holder.GetComponentsInChildren<ActorAttachTag>(true))
                if (tg.Slot == RightPistolSlot) foreach (var rd in tg.GetComponentsInChildren<Renderer>(true)) rd.enabled = !hide;
        }

        // ---------------- propio ----------------
        static bool OwnWindow() { var c = GameMenu.Current; return c != GameMenu.Win.None && c != GameMenu.Win.Quit; }
        bool Special() { return BroomFlight.Active || (gc != null && gc.Dashing); }   // IsSpecialAnimation: escoba, tirolina, cuerda, dash

        void Update()
        {
            Action a; int n = 0; while (n++ < 16 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[inven] " + ex.Message); } }
            if (ents == null || ents.Self == null || gc == null) return;
            bool win = OwnWindow();
            if (!selfOn && win && !Special()) { selfOn = true; SelfApply(true); SendSwitch(); }
            else if (selfOn && !win) { selfOn = false; SelfApply(false); SendSwitch(); }
        }

        void SelfApply(bool on)
        {
            var self = ents.Self.transform; gc.InvenMode = on; gc.ResetAnimClip();
            var sink = self.GetComponentInChildren<AnimEventSink>(true);
            if (sink != null) { sink.Persist = on; if (!on) sink.DeleteCurrentObject(); }
            if (!VehicleRide.SelfRiding) HideWeapon(self, on);
        }

        void SendSwitch()
        {
            if (NpcUI.Sender == null || ents == null || ents.SelfId == 0) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_SPECIAL_STATE_SWITCH, 8); ClientPackets.PutInt(p, 4, ents.SelfId); p[8] = 16;
            try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[inven] envio: " + ex.Message); }
        }
    }
}
