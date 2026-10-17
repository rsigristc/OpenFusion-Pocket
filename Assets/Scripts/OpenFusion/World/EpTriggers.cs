// EpTriggers: port de los EpTrigger del mapa (Assembly-CSharp-first_pass/EpTrigger.cs, EpPlatformTrigger, EpJumppadTrigger, EpLauncherTrigger, EpRingTrigger, EpSwitchTrigger, EpBeltTrigger,
// EpSlopeTrigger, EpRopeTrigger, EpSynchronizer, GameFrameEpContainer) + la parte del jugador de cnAvatarThirdPersonMove / EnvironmentCollision / cnAvatarAttack / cnOwnAvatarStatus.
// SIN COMPILAR NI PROBAR EN UNITY. Archivos: EpTriggers.cs (registro, reloj del servidor, plataformas, zonas: jumppad / aros / interruptores, cintas, colisiones),
// EpTriggers.Rides.cs (lanzador, tobogan, cuerda), EpTriggers.Remote.cs (paquetes de otros jugadores), EpParse.cs (script del mapa -> EpInfo), EpHitRelay (abajo).
//
// Datos del mapa (volcado de los 173 BuildPlayer-Map_*): cada EP es un nodo con un MonoBehaviour {triggerType, objectId, serverId, cneId, radius, startPosition, ...} bajo el contenedor "_EpElements".
//   triggerType: 1 Slope (34) | 2 Zipline (293, ZiplineRide) | 3 Launcher (88) | 4 Platform (1569) | 5 Jumppad (1135) | 8 Switch (15) | 11 Ring (3086) | 12 Rope (15) | 13 Belt (5).
//   NO hay Door (EpDoorTrigger), Trap, Ecom ni Xcom en ningun mapa (0 scripts con openType/attackInterval): no se portan.
//   Zonas (jumppad, aro, lanzador, interruptor): SphereCollider isTrigger en el propio nodo (DongBuilder no construye los triggers: se leen los datos en EpParse). Plataforma: Rigidbody cinematico en el
//   nodo y colisionadores (capa 12) en los hijos; jumppad/tobogan/cinta/cuerda: colisionadores no-trigger en los hijos (el original los distingue por la etiqueta, aqui por el nodo propietario).
//   Waypoints: scripts {point, prev, next} en hijos del nodo; EpParse los lee en orden head -> tail (point es local al nodo).
//
// Original replicado (por tipo):
//  * Plataforma: Start(): initialPosition/Rotation = transform, worldTo = rot * (to - from), center = (to - from) / 2, moveLength = |to - from|; Line con moveLength < 0,01 -> activated = false.
//    Update: timer += dt * velocity (ida timer 0 -> 1, vuelta 1 -> 0; Line: k = clamp01(timer - sen(2 pi timer) * 0,15); xCurve/yCurve/zCurve: circulo de radio moveLength / 2 centrado en center;
//    xRotate/zRotate(_Move): giro k * 180 * spinvelocity grados (negativo en la vuelta); *_Path: waypoints). Solo se mueve si distancia < min(cull, 80) + moveLength (cada 2 s).
//    Sincronia: InitTime(uiAccumTime): periodo = (ulong)(2000 / velocity + 0,05) ms; timer = velocity * (t % periodo) / 1000; < 1 ida, si no timer = 2 - timer (vuelta). Aqui el movimiento es analitico
//    (posicion = f(reloj del servidor)), equivalente al original con resincronizacion continua. Reloj: ENTER_SUCC.iSvrTime y el eco del servidor de PC_MOVE/STOP/JUMP/JUMPPAD/LAUNCHER propios.
//    Jugador sobre la plataforma (EnvironmentCollision.HandlePlatformCollision / CalculatePlatformVectors): al chocar el CharacterController con un hijo de la plataforma queda "currentPlatform";
//    cada frame position += platform.position - platformPosition (salvo si la plataforma esta por encima y se aleja); xRotate / zRotate (tipos 4 y 5) en cambio empujan 0,25 m x normal horizontal.
//  * Jumppad: OnTriggerEnter -> InsideJumppadRange(addPower) (flag 3 s); al tocar el CC un hijo del jumppad con el flag -> Jump(addPower) + REQ_PC_JUMPPAD + sonido sfx_bouncetech.
//  * Aro (Ring): OnTriggerEnter -> UseRing: con carrera (bRingRace) REQ_EP_GET_RING {iRingLID = serverId}; luego Deactive() (oculta el nodo). Active()/Deactive() muestran/ocultan (SetActiveRecursively).
//    InitRings(area, switches) (INSTANCE_MAP_INFO): aros dentro del area del dong del centro del area: Deactive() y serverId = 1, 2, ... en el orden de los hijos de _EpElements; interruptores igual
//    (SwitchOff, serverId aparte; los de la lista pasan a SwitchOn). ActivateRings/DeactivateRings con la carrera; ResetRings al warp/teleport (serverId = -1, ocultos).
//  * Interruptor (Switch): Start -> CheckSwitch (SwitchOn/SwitchOff del objetivo targetElementId); al usarlo (tecla de ataque cerca, sin mob) REQ_IM_CHANGE_SWITCH_STATUS {iSwitchLID} + SwitchOn local
//    (el nodo baja 0,5 m y el objetivo se activa); REP_IM_CHANGE_SWITCH_STATUS {iSwitchGID} -> SwitchOn del interruptor con ese objectId.
//  * Cinta (Belt): beltMoveDelta = direccion del segmento de waypoints mas cercano * speed * dt mientras el CC la toca.
//  * Lanzador, tobogan y cuerda: ver EpTriggers.Rides.cs.
//
// Integracion: DongBuilder.ApplyScript -> EpParse.Handle (rama triggerType != 2); WorldBootstrap -> EpTriggers.Ensure(gameObject).Init(Entities, dispatcher); ZiplineRide.Intercept -> EpTriggers.Intercept
// (CombatSystem ya llama a ZiplineRide.Intercept: no hace falta tocarlo). Paquetes: PacketDispatcher.Unhandled. Raceoptions: RaceSystem.InsMapInfo / RacingChanged.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    /// <summary>EpTrigger.TriggerType.</summary>
    public static class EpT
    {
        public const int None = 0, Slope = 1, Zipline = 2, Launcher = 3, Platform = 4, Jumppad = 5, Door = 6, Trap = 7, Switch = 8, Ecom = 9, Xcom = 10, Ring = 11, Rope = 12, Belt = 13;
    }

    /// <summary>Datos de un EpTrigger del mapa (campos del MonoBehaviour + colisionador trigger + waypoints). Lo rellena EpParse.</summary>
    public sealed class EpInfo
    {
        public int Type; public GameObject Go; public int ObjectId = -1, CneId, Order; public float Radius = 1f;
        public string Shape; public Vector3 Center, Size; public float R, H;                       // colisionador trigger del nodo (BoxCollider / SphereCollider / CapsuleCollider), local
        public Vector3 From, To; public float Velocity, SpinVelocity; public int MoveType, TargetElementId, WaypointCount;   // plataforma
        public Vector3[] Points;                                                                   // Waypoint.point de head a tail (local al nodo): plataforma *_Path, cinta, tobogan, cuerda
        public float Speed; public int RopeMoveType;                                               // cinta / tobogan / cuerda (0 izquierda, 1 derecha, 2 arriba-abajo)
        public float AddPower, MinPower, MaxPower; public Vector3 InitRotate, MaxRotate; public int FceType;   // jumppad / lanzador / aro
    }

    /// <summary>Estado en ejecucion de un EpTrigger.</summary>
    public sealed class EpNode
    {
        public EpInfo I; public GameObject Go; public Transform T; public int Type;
        public int ServerId = -1; public bool Activated = true, SwitchedOn; public EpNode Target; public bool PendingCheck; public int RegFrame;
        public bool Init, Near = true; public Vector3 InitPos, WorldTo, Center, Vel; public Quaternion InitRot = Quaternion.identity; public float MoveLength;   // plataforma
        public bool Inside; public float EnterTime;                                                                                                              // zonas

        /// <summary>EpTrigger.Active(): los aros se muestran; true si estaba desactivado.</summary>
        public bool Active()
        {
            if (Type == EpT.Ring && Go != null) Go.SetActive(true);
            if (!Activated) { Activated = true; return true; }
            return false;
        }
        /// <summary>EpTrigger.Deactive(): los aros se ocultan; true si estaba activo.</summary>
        public bool Deactive()
        {
            if (Type == EpT.Ring && Go != null) Go.SetActive(false);
            if (Activated) { Activated = false; return true; }
            return false;
        }
        /// <summary>EpTrigger.SwitchOn(): el interruptor baja 0,5 m y su objetivo se activa.</summary>
        public bool SwitchOn()
        {
            if (Type != EpT.Switch || T == null) return false;
            if (!SwitchedOn) { var p = T.position; p.y -= 0.5f; T.position = p; }
            SwitchedOn = true;
            if (Target != null && Target.Go != null) return Target.Active();
            return false;
        }
        /// <summary>EpTrigger.SwitchOff(): el interruptor sube 0,5 m y su objetivo se desactiva.</summary>
        public bool SwitchOff()
        {
            if (Type != EpT.Switch || T == null) return false;
            if (SwitchedOn) { var p = T.position; p.y += 0.5f; T.position = p; }
            SwitchedOn = false;
            if (Target != null && Target.Go != null) return Target.Deactive();
            return false;
        }

        /// <summary>Posiciones de mundo de los waypoints (T.TransformPoint(point)).</summary>
        public Vector3[] WorldPoints()
        {
            var p = I.Points; if (p == null) return new Vector3[0];
            var r = new Vector3[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = T.TransformPoint(p[i]); return r;
        }
    }

    /// <summary>OnControllerColliderHit del original (cnAvatarThirdPersonMove) llega a todos los scripts del objeto del CharacterController: esta pieza lo reenvia a EpTriggers.</summary>
    public sealed class EpHitRelay : MonoBehaviour
    {
        public Action<ControllerColliderHit> Hit;
        void OnControllerColliderHit(ControllerColliderHit h) { if (Hit != null) Hit(h); }
    }

    /// <summary>EnvironmentCollision (la parte de plataformas / cintas / toboganes / cuerdas).</summary>
    sealed class EnvCol
    {
        public Transform currentSurface, currentBeltSurface, currentRopeSurface;
        public EpNode platform, slope, belt, rope;
        public Vector3 platformPosition, contactNormal = Vector3.zero, moveDelta, rotOffset, beltDelta; public int platformMoveType;
        public float lastPriorityHit = -1f, platformSeen, beltSeen; public bool nonPriority;
    }

    [DefaultExecutionOrder(500)]
    public sealed partial class EpTriggers : MonoBehaviour
    {
        public static EpTriggers Instance;
        static readonly List<EpNode> nodes = new List<EpNode>();
        static readonly Dictionary<Transform, EpNode> byRoot = new Dictionary<Transform, EpNode>();
        static int regCount, regLogged;
        /// <summary>Como el original (cnAvatarThirdPersonMove.MovePacket, bEnableSendPacket): sobre una plataforma / cinta se manda REQ_PC_MOVEPLATFORM / REQ_PC_BELT (Size 64) EN LUGAR de PC_MOVE / PC_STOP, con la cadencia de GameClient. false = comportamiento anterior (PC_MOVE absoluto).</summary>
        public static bool SendPlatformPackets = true;
        /// <summary>min(GetDefaultObjectCull(), 80) del original (EpPlatformTrigger.updateTransformDistance).</summary>
        public static float ActiveDistance = 80f;
        public static int Count { get { return nodes.Count; } }

        public static EpTriggers Ensure(GameObject host) { var v = host.GetComponent<EpTriggers>(); if (v == null) v = host.AddComponent<EpTriggers>(); Instance = v; return v; }

        /// <summary>Registro desde DongBuilder.ApplyScript (hilo principal) a traves de EpParse.</summary>
        public static EpNode Register(EpInfo info)
        {
            if (info == null || info.Go == null) return null;
            var n = new EpNode { I = info, Go = info.Go, T = info.Go.transform, Type = info.Type, RegFrame = Time.frameCount };
            n.MoveLength = (info.To - info.From).magnitude;
            if (info.Type == EpT.Platform && info.Go.GetComponent<Rigidbody>() == null)
            {   // el original lleva un Rigidbody cinematico (rigidbody.MovePosition); los colisionadores de los hijos pasan a ser parte de un cuerpo cinematico movil
                var rb = info.Go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            }
            if (info.Type == EpT.Switch) n.PendingCheck = true;
            if (info.Type == EpT.Ring) { n.Activated = false; info.Go.SetActive(false); }   // el original los oculta con ResetRings/InitRings al entrar; los aros solo existen durante una carrera
            nodes.Add(n); byRoot[n.T] = n; regCount++;
            if ((regCount & 255) == 0) Prune();
            if (regLogged < 12) { regLogged++; Debug.Log("[ep] registrado tipo " + info.Type + " id " + info.ObjectId + " en " + info.Go.name + (info.Points != null ? " waypoints=" + info.Points.Length : "")); }
            return n;
        }

        static void Prune()
        {
            nodes.RemoveAll(x => x == null || x.Go == null);
            List<Transform> dead = null;
            foreach (var kv in byRoot) if (kv.Key == null) { if (dead == null) dead = new List<Transform>(); dead.Add(kv.Key); }
            if (dead != null) foreach (var d in dead) byRoot.Remove(d);
        }

        /// <summary>Nodo EP propietario de un transform (el propio nodo o un antecesor: los colisionadores estan en los hijos).</summary>
        public static EpNode OwnerOf(Transform t)
        {
            for (int i = 0; i < 24 && t != null; i++) { EpNode n; if (byRoot.TryGetValue(t, out n)) return n; t = t.parent; }
            return null;
        }

        /// <summary>SearchEpElement: el nodo del tipo e id (objectId) mas cercano a pos.</summary>
        public static EpNode Find(int type, int objectId, Vector3 pos)
        {
            EpNode best = null; float bd = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i]; if (n == null || n.Go == null || n.Type != type || n.I.ObjectId != objectId) continue;
                float d = (n.T.position - pos).sqrMagnitude; if (d < bd) { bd = d; best = n; }
            }
            return best;
        }

        // ================================================================ estado de la instancia ================================================================
        WorldEntities ents; PacketDispatcher disp; GameClient gc;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        Transform selfT; EpHitRelay relay; EnvCol env = new EnvCol(); Vector3 prevSelfPos; Vector3 selfVel; bool prevSelfInit;
        float nextNear; EpNode nearUse; bool prevWant;

        public void Init(WorldEntities e, PacketDispatcher d)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnSelfEnter; disp.PcMove -= OnPcMove; disp.PcStop -= OnPcStop; disp.PcJump -= OnPcJump; }
            if (ents != null) ents.Teleported -= OnTeleport;
            ents = e; disp = d; Instance = this;
            if (disp != null) { disp.Unhandled += OnPacket; disp.SelfEnter += OnSelfEnter; disp.PcMove += OnPcMove; disp.PcStop += OnPcStop; disp.PcJump += OnPcJump; }
            if (ents != null) ents.Teleported += OnTeleport;
            RaceSystem.InsMapInfo -= OnInsMapInfo; RaceSystem.InsMapInfo += OnInsMapInfo;
            RaceSystem.RacingChanged -= OnRacingChanged; RaceSystem.RacingChanged += OnRacingChanged;
        }
        void OnDestroy()
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.SelfEnter -= OnSelfEnter; disp.PcMove -= OnPcMove; disp.PcStop -= OnPcStop; disp.PcJump -= OnPcJump; }
            if (ents != null) ents.Teleported -= OnTeleport;
            RaceSystem.InsMapInfo -= OnInsMapInfo; RaceSystem.RacingChanged -= OnRacingChanged;
            AbortRide(false); if (Instance == this) Instance = null;
        }

        GameClient GC() { if (gc == null) gc = FindObjectOfType<GameClient>(); return gc; }
        static void Send(byte[] p) { var s = NpcUI.Sender; if (s != null) s(p); }
        static void PutI(byte[] b, int o, int v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static void PutF(byte[] b, int o, float v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static int I(byte[] b, int o) { return b != null && b.Length >= o + 4 ? BitConverter.ToInt32(b, o) : 0; }
        static float F(byte[] b, int o) { return b != null && b.Length >= o + 4 ? BitConverter.ToSingle(b, o) : 0f; }
        static ulong U64(byte[] b, int o) { return b != null && b.Length >= o + 8 ? BitConverter.ToUInt64(b, o) : 0UL; }
        static int Cm(float v) { return (int)(v * 100f); }                                               // CoordUtil.ClientToServer (truncado)
        static Vector3 SToC(int x, int y, int z) { return new Vector3(x * 0.01f, z * 0.01f, y * 0.01f); }   // CoordUtil.ServerToClient
        static ulong NowMs() { return (ulong)(Time.realtimeSinceStartup * 1000f); }                     // GetTimestampMs()
        static readonly int[,] KeyToDir = { { 6, 5, 4 }, { 7, 0, 3 }, { 8, 1, 2 } };                  // AvatarUtil.GetDirNum
        static byte DirNum(float h, float v)
        {
            int ih = Mathf.Abs(h) < 0.01f ? 0 : (h > 0f ? 1 : -1), iv = Mathf.Abs(v) < 0.01f ? 0 : (v > 0f ? 1 : -1); return (byte)KeyToDir[iv + 1, ih + 1];
        }
        /// <summary>(strafe, avance) en -1..1: el stick tactil si se usa, si no el teclado / mando de ControlMap. 0 con chat, menu o NPC abiertos.</summary>
        static Vector2 Axes()
        {
            if (ChatHud.Typing || GameMenu.Blocking || NpcUI.Active) return Vector2.zero;
            if (TouchControls.Active && TouchControls.Move.sqrMagnitude > 0f) return TouchControls.Move;
            return new Vector2(ControlMap.Strafe, ControlMap.Forward);
        }
        static bool JumpPressed() { if (ChatHud.Typing || GameMenu.Blocking || NpcUI.Active) return false; return ControlMap.Down(ControlMap.K.Jump, false) || TouchControls.ConsumeJump(); }
        /// <summary>ActorSkinCombiner.LinkObjectActive(slot, on): 3 = arma, 5 = polea.</summary>
        static void Link(Transform holder, int slot, bool on)
        {
            if (holder == null) return;
            foreach (var tg in holder.GetComponentsInChildren<ActorAttachTag>(true))
                if (tg.Slot == slot) foreach (var rd in tg.GetComponentsInChildren<Renderer>(true)) rd.enabled = on;
        }

        // ================================================================ reloj del servidor ================================================================
        static double svrOffset; static bool svrKnown;
        /// <summary>Hora del servidor en ms (aprox.). Antes de recibir ninguna se usa el reloj local (las plataformas se mueven igual, solo sin sincronia con otros jugadores).</summary>
        public static ulong ServerMs { get { double v = Time.realtimeSinceStartupAsDouble * 1000.0 + (svrKnown ? svrOffset : 0.0); return v < 0.0 ? 0UL : (ulong)v; } }
        static void SyncClock(ulong svr)
        {
            if (svr == 0UL) return;
            double off = (double)svr - Time.realtimeSinceStartupAsDouble * 1000.0;
            if (!svrKnown || Math.Abs(off - svrOffset) > 400.0) { svrOffset = off; svrKnown = true; } else svrOffset += (off - svrOffset) * 0.1;
        }
        void OnSelfEnter(EnterSucc e) { ulong t = e.SvrTime; q.Enqueue(() => SyncClock(t)); }
        void OnPcMove(PcMove m) { if (ents != null && m.Id == ents.SelfId) { ulong t = m.SvrTime; q.Enqueue(() => SyncClock(t)); } else { int id = m.Id; q.Enqueue(() => EndRemoteSpecial(id)); } }
        void OnPcStop(PcStop m) { if (ents != null && m.Id == ents.SelfId) { ulong t = m.SvrTime; q.Enqueue(() => SyncClock(t)); } else { int id = m.Id; q.Enqueue(() => EndRemoteSpecial(id)); } }
        void OnPcJump(PcJump m) { if (ents != null && m.Id == ents.SelfId) { ulong t = m.SvrTime; q.Enqueue(() => SyncClock(t)); } else { int id = m.Id; q.Enqueue(() => { EndRemoteSpecial(id); EndRemotePlat(id); }); } }

        // ================================================================ bucle ================================================================
        void LateUpdate()
        {
            while (q.TryDequeue(out var a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[ep] " + ex.Message); } }
            if (WorldBootstrap.Travelling) return;
            float dt = Time.deltaTime;
            Transform self = ents != null && ents.Self != null ? ents.Self.transform : null;
            if (self != selfT) OnSelfChanged(self);
            if (self != null)
            {   // velocidad horizontal propia (para JUMPPAD) sin el arrastre de la plataforma
                if (prevSelfInit && dt > 1e-5f) { var d = (self.position - prevSelfPos) / dt; selfVel = Vector3.Lerp(selfVel, d, 0.5f); }
                prevSelfPos = self.position; prevSelfInit = true;
            }
            ulong svr = ServerMs;
            bool rescan = Time.time >= nextNear; if (rescan) nextNear = Time.time + 2f;   // LocatePlayerCharacter: cada 2 s
            bool anyMoved = false;
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var n = nodes[i]; if (n == null || n.Go == null) { nodes.RemoveAt(i); continue; }
                if (n.PendingCheck && Time.frameCount > n.RegFrame + 1) { n.PendingCheck = false; try { SwitchStart(n); } catch (Exception ex) { Debug.LogWarning("[ep] switch: " + ex.Message); } }
                if (n.Type != EpT.Platform) continue;
                if (rescan) n.Near = self != null && Vector3.Distance(n.T.position, self.position) < ActiveDistance + n.MoveLength;
                if (!n.Near || !n.Activated || !n.Go.activeInHierarchy) { n.Vel = Vector3.zero; continue; }
                try { MovePlatform(n, svr, dt); if (n.Vel.sqrMagnitude > 0f) anyMoved = true; } catch (Exception ex) { Debug.LogWarning("[ep] plataforma " + n.Go.name + ": " + ex.Message); n.Activated = false; }
            }
            if (anyMoved) Physics.SyncTransforms();
            if (self != null)
            {
                ScanZones(self);
                StartPending(self);
                TickRide(self, dt);
                RideTick(self, dt);
            }
            TickRemoteRiders(dt);
            TickInstance();
        }

        void OnSelfChanged(Transform self)
        {
            AbortRide(false); selfT = self; env = new EnvCol(); prevSelfInit = false; selfVel = Vector3.zero; insideJumppad = standingJumppad = false; nearUse = null;
            if (self != null)
            {
                relay = self.GetComponent<EpHitRelay>(); if (relay == null) relay = self.gameObject.AddComponent<EpHitRelay>();
                relay.Hit = OnHit;
            }
        }
        void OnTeleport(int x, int y, int z)
        {
            q.Enqueue(() =>
            {
                AbortRide(false); env = new EnvCol(); insideJumppad = standingJumppad = false;
                ResetRings(SToC(x, y, z));
                if (lastInfoValid && Time.unscaledTime - lastInfoTime < 15f) { pendingInit = true; initSince = Time.unscaledTime; }   // INSTANCE_MAP_INFO llega justo despues del warp
            });
        }

        // ================================================================ plataformas ================================================================
        static Vector3 PathPoint(EpInfo I, float t)
        {   // EpPlatformTrigger.GetLerpPosition(fTime)
            var p = I.Points; if (p == null || p.Length == 0) return Vector3.zero;
            int cnt = I.WaypointCount > 0 ? Mathf.Min(I.WaypointCount, p.Length) : p.Length; float seg = 1f / cnt;
            int idx = (int)(t / seg); if (idx >= cnt) idx = cnt - 1; if (idx < 0) idx = 0;
            float f = (t - seg * idx) / seg; Vector3 v = p[idx]; if (idx + 1 < p.Length) v = v * (1f - f) + p[idx + 1] * f; return v;
        }

        void InitPlatform(EpNode n)
        {
            n.Init = true; n.InitPos = n.T.position; n.InitRot = n.T.rotation;
            Vector3 d = n.I.To - n.I.From; n.MoveLength = d.magnitude; n.Center = d * 0.5f; n.WorldTo = n.InitRot * d;
            if (n.MoveLength < 0.01f && n.I.MoveType == 0) n.Activated = false;
            if (n.I.TargetElementId != 0) n.Target = FindOther(n, n.I.TargetElementId);
        }
        EpNode FindOther(EpNode self, int objectId)
        {
            EpNode best = null; float bd = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                var o = nodes[i]; if (o == null || o.Go == null || o == self || o.I.ObjectId != objectId) continue;
                float dd = (o.T.position - self.T.position).sqrMagnitude; if (dd < bd) { bd = dd; best = o; }
            }
            return best;
        }

        /// <summary>EpPlatformTrigger.MoveToTo / MoveToFrom / MovePath con timer = f(hora del servidor).</summary>
        void MovePlatform(EpNode n, ulong svr, float dt)
        {
            var I0 = n.I; if (I0.Velocity <= 0.0001f) return;
            if (!n.Init) { InitPlatform(n); if (!n.Activated) return; }
            ulong period = (ulong)(2000f / I0.Velocity + 0.05f); if (period == 0UL) period = 1UL;
            float u = I0.Velocity * (float)(svr % period) / 1000f; int mt = I0.MoveType; bool pathType = mt == 8 || mt == 9 || mt == 10; float timer; bool fwd = true;
            if (pathType) { timer = u > 1f ? u - 1f : u; if (timer > 1f) timer = 1f; }
            else if (u < 1f) timer = u;
            else { timer = 2f - u; fwd = false; }
            if (timer < 0f) timer = 0f;
            const float PI = Mathf.PI; Vector3 pos = n.T.position; Quaternion rot = n.T.rotation; bool setRot = false; float sc = n.T.localScale.x;
            if (pathType)
            {
                if (mt == 9) { rot = n.InitRot * Quaternion.Euler(timer * 2f * PI * 57.29578f * I0.SpinVelocity, 0f, 0f); setRot = true; }
                else if (mt == 10) { rot = n.InitRot * Quaternion.Euler(0f, 0f, timer * 2f * PI * 57.29578f * I0.SpinVelocity); setRot = true; }
                n.WorldTo = n.InitRot * (PathPoint(I0, timer) * sc); pos = n.InitPos + n.WorldTo;
            }
            else if (mt == 1 || mt == 2 || mt == 3)
            {   // xCurve (1): x = 0; zCurve (2): z = 0; yCurve (3): y = 0
                float th = fwd ? timer * PI + PI / 2f : 2f * PI - timer * PI + PI / 2f; float s = Mathf.Sin(th), c = Mathf.Cos(th);
                Vector3 v = mt == 1 ? new Vector3(0f, s, c) : (mt == 3 ? new Vector3(s, 0f, c) : new Vector3(s, c, 0f));
                v *= n.MoveLength * 0.5f; v += n.Center; pos = n.InitRot * (v * sc) + n.InitPos;
            }
            else if (mt >= 4 && mt <= 7)
            {   // xRotate (4), zRotate (5), xRotate_Move (6), zRotate_Move (7)
                float k = Mathf.Clamp01(timer - Mathf.Sin(timer * 2f * PI) * 0.15f); float ang = (fwd ? 1f : -1f) * k * PI * 57.29578f * I0.SpinVelocity;
                rot = n.InitRot * ((mt == 4 || mt == 6) ? Quaternion.Euler(ang, 0f, 0f) : Quaternion.Euler(0f, 0f, ang)); setRot = true;
                if (mt >= 6) pos = n.InitPos + n.WorldTo * k;
            }
            else
            {   // Line
                float k = Mathf.Clamp01(timer - Mathf.Sin(timer * 2f * PI) * 0.15f); pos = n.InitPos + n.WorldTo * k;
            }
            Vector3 old = n.T.position;
            if ((pos - old).sqrMagnitude > 1e-10f) n.T.position = pos;
            if (setRot) n.T.rotation = rot;
            n.Vel = dt > 1e-5f ? (pos - old) / dt : Vector3.zero;
            if (n.Target != null && n.Target.Go != null) { n.Target.T.position = n.T.position; n.Target.T.rotation = n.T.rotation; }   // targetElementTrigger acompana a la plataforma
        }

        // ================================================================ colisiones del jugador (EnvironmentCollision) ================================================================
        bool insideJumppad, standingJumppad; float jumpRangeTime, jumpPower; EpNode pendingSlope, pendingRope;

        void OnHit(ControllerColliderHit hit)
        {
            if (hit == null || hit.collider == null || hit.collider.isTrigger || selfT == null) return;
            env.contactNormal = hit.normal;
            var t = hit.collider.transform; var owner = OwnerOf(t); int ty = owner != null ? owner.Type : 0; Vector3 pos = selfT.position;
            if (ty == EpT.Platform) HitPlatform(owner, t);
            else if (ty == EpT.Slope && hit.point.y < pos.y + 0.5f) { if (HitSlope(owner, t)) pendingSlope = owner; }
            else if (ty == EpT.Belt && hit.point.y < pos.y + 0.5f) HitBelt(owner, t);
            else if (ty == EpT.Rope && hit.point.y > pos.y + 0.3f) { if (HitRope(owner, t)) pendingRope = owner; }
            else if (ty == EpT.Jumppad) { if (insideJumppad) standingJumppad = true; }
            else if (env.lastPriorityHit < Time.time) ResetCollision();
        }

        void HitPlatform(EpNode owner, Transform t)
        {   // EnvironmentCollision.HandlePlatformCollision
            env.nonPriority = false;
            if ((env.currentSurface != null && !(env.currentSurface.position.y >= t.position.y)) || (env.lastPriorityHit < Time.time && env.currentSurface != t) || env.platform == null)
            {
                env.lastPriorityHit = Time.time; env.currentSurface = t;
                env.platform = owner; env.platformPosition = owner.T.position; env.platformMoveType = owner.I.MoveType;
            }
            else if (env.lastPriorityHit < Time.time && env.currentSurface == t) env.lastPriorityHit = Time.time;
            env.platformSeen = Time.time;
        }
        bool HitSlope(EpNode owner, Transform t)
        {   // HandleSlopeCollision: true al cambiar de tobogan
            env.nonPriority = false;
            if (env.currentSurface != t)
            {
                env.currentSurface = t;
                if (env.slope == null || env.slope != owner) { env.slope = owner; return true; }
            }
            return false;
        }
        void HitBelt(EpNode owner, Transform t)
        {   // HandleBeltCollision
            env.nonPriority = false; env.beltSeen = Time.time;
            if (env.currentBeltSurface != t) { env.lastPriorityHit = Time.time; env.currentBeltSurface = t; env.belt = owner; }
        }
        bool HitRope(EpNode owner, Transform t)
        {   // HandleRopeCollision: true al cambiar de cuerda
            env.nonPriority = false; env.lastPriorityHit = Time.time;
            if (env.currentRopeSurface != t)
            {
                env.currentRopeSurface = t;
                if (env.rope == null || env.rope != owner) { env.rope = owner; return true; }
            }
            return false;
        }
        /// <summary>cnAvatarThirdPersonMove.ResetCollisionVariables (el tobogan se acaba al tocar algo que no es suyo; la cuerda no).</summary>
        void ResetCollision()
        {
            if (ride == Ride.Rope) return;
            if (ride == Ride.Slope) endRequested = true;
            env.currentSurface = null; env.platform = null; env.slope = null; env.belt = null; env.currentBeltSurface = null; env.rope = null; env.currentRopeSurface = null; env.nonPriority = true;
        }

        /// <summary>EnvironmentCollision.CalculatePlatformVectors + position += deltas (UpdateCollisionAndPlatforms).</summary>
        void RideTick(Transform self, float dt)
        {
            if (env.platform != null && (env.platform.Go == null || Time.time - env.platformSeen > 0.25f)) { env.platform = null; env.currentSurface = null; }
            if (env.belt != null && (env.belt.Go == null || Time.time - env.beltSeen > 0.25f)) { env.belt = null; env.currentBeltSurface = null; }
            env.moveDelta = Vector3.zero; env.rotOffset = Vector3.zero; env.beltDelta = Vector3.zero;
            if (ride != Ride.None) { env.platform = null; env.belt = null; return; }   // en tobogan / cuerda / lanzador no hay arrastre
            if (env.platform != null)
            {
                var pt = env.platform.T;
                if (env.platformMoveType == 4 || env.platformMoveType == 5)
                {   // xRotate / zRotate: el avatar resbala empujado por la normal (0,25 m por frame del original a ~30 fps)
                    var cn = env.contactNormal; cn.y = 0f; env.rotOffset = cn * 0.25f * Mathf.Clamp(dt * 30f, 0f, 2f); env.contactNormal = cn * 0.9f;
                }
                else
                {
                    Vector3 lhs = pt.position - env.platformPosition;
                    if (!(env.platformPosition.y >= self.position.y) || Vector3.Dot(lhs, self.position - pt.position) > 0f) env.moveDelta = pt.position - env.platformPosition;
                    env.platformPosition = pt.position;
                }
            }
            if (env.belt != null) env.beltDelta = BeltDelta(env.belt, self.position, dt);
            // salto de jumppad (UpdateCollisionAndPlatforms)
            if (standingJumppad && insideJumppad && Time.time - jumpRangeTime <= 3f) { DoJumpPad(self); insideJumppad = false; standingJumppad = false; }
            Vector3 total = env.moveDelta + env.beltDelta + env.rotOffset;
            if (total.sqrMagnitude > 0f) { self.position += total; Physics.SyncTransforms(); }
        }

        static Vector3 BeltDelta(EpNode belt, Vector3 avatar, float dt)
        {
            var pts = belt.WorldPoints(); Vector3 a0 = Vector3.zero, a1 = Vector3.zero; float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 lhs = pts[i] - avatar; float mag = lhs.magnitude;
                if (best > mag && i + 1 < pts.Length)
                {
                    lhs.Normalize(); Vector3 rhs = (pts[i + 1] - pts[i]).normalized;
                    if (Vector3.Dot(lhs, rhs) < 0f) { best = mag; a0 = pts[i]; a1 = pts[i + 1]; }
                }
            }
            return (a1 - a0).normalized * belt.I.Speed * dt;
        }

        /// <summary>sP_CL2FE_REQ_PC_MOVEPLATFORM / sP_CL2FE_REQ_PC_BELT (ambos Size 64 = tamano natural con Pack 4: u64 + 3 int + 3 int + 3 float + 3 int + byte + int = 64):
        /// iCliTime@0, iLc@8 (posicion local al elemento, ClientToServer x,z,y), iPos@20, fV@32 (kMovement x,z,y), bDown@44, iPlatformID / iBeltID@48, iAngle@52, cKeyValue@56, iSpeed@60.
        /// Cuerpo = 64, total = 68. Lo que cnAvatarThirdPersonMove.MovePacket manda en lugar de PC_MOVE mientras enCol.currentPlatform / currentBelt != null.</summary>
        static byte[] BuildSurface(Transform self, EpNode el, bool belt, float vx, float vy, float vz, int angle, byte key, int speed)
        {
            var p = ClientPackets.New(belt ? PacketIds.P_CL2FE_REQ_PC_BELT : PacketIds.P_CL2FE_REQ_PC_MOVEPLATFORM, 64); var loc = self.position - el.T.position;
            BitConverter.GetBytes(NowMs()).CopyTo(p, 4);
            PutI(p, 12, Cm(loc.x)); PutI(p, 16, Cm(loc.z)); PutI(p, 20, Cm(loc.y));
            PutI(p, 24, Cm(self.position.x)); PutI(p, 28, Cm(self.position.z)); PutI(p, 32, Cm(self.position.y));
            if (key != 0) { PutF(p, 36, vx); PutF(p, 40, vz); PutF(p, 44, vy); }   // parado: el original no toca fV (sin movimiento)
            PutI(p, 52, el.I.ObjectId); PutI(p, 56, angle); p[60] = key;
            PutI(p, 64, belt ? (int)(el.I.Speed * 100f) : (key != 0 ? speed : 0));
            return p;
        }

        /// <summary>true si el avatar esta sobre una plataforma o cinta (enCol.currentPlatform / currentBelt != null) y el envio de esos paquetes esta activo: GameClient NO debe mandar PC_MOVE / PC_STOP.</summary>
        public static bool OnSurface
        {
            get { var i = Instance; return SendPlatformPackets && i != null && i.ride == Ride.None && (i.env.platform != null || i.env.belt != null); }
        }

        /// <summary>Sustituye a PC_MOVE / PC_STOP sobre plataforma o cinta (orden del original: plataforma, luego cinta). vx, vy(vertical), vz en unidades de cliente (x, y, z de Unity); angle = (int)eulerY - 180; key = DirNum (0 = parado); speed = RunSpeed.
        /// Devuelve false si no se envio nada (no esta sobre ninguno).</summary>
        public static bool TrySendSurface(Transform self, float vx, float vy, float vz, int angle, byte key, int speed)
        {
            var i = Instance; if (!OnSurface || self == null) return false;
            if (i.env.platform != null) Send(BuildSurface(self, i.env.platform, false, vx, vy, vz, angle, key, speed));
            else Send(BuildSurface(self, i.env.belt, true, vx, vy, vz, angle, key, speed));
            return true;
        }

        // ================================================================ zonas: jumppad, aros, lanzador, interruptor ================================================================
        /// <summary>El punto esta dentro del colisionador trigger del nodo (BoxCollider / SphereCollider / CapsuleCollider local), con margen.</summary>
        static bool InsideShape(EpNode n, Vector3 world, float margin)
        {
            var I0 = n.I; var t = n.T; var l = t.InverseTransformPoint(world);
            if (I0.Shape == "BoxCollider")
            {
                var d = l - I0.Center; var h = I0.Size * 0.5f; var s = t.lossyScale;
                float mx = margin / Mathf.Max(0.0001f, Mathf.Abs(s.x)), my = margin / Mathf.Max(0.0001f, Mathf.Abs(s.y)), mz = margin / Mathf.Max(0.0001f, Mathf.Abs(s.z));
                return Mathf.Abs(d.x) <= h.x + mx && Mathf.Abs(d.y) <= h.y + my && Mathf.Abs(d.z) <= h.z + mz;
            }
            if (I0.Shape == "SphereCollider" || I0.Shape == "CapsuleCollider")
            {
                float sc = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Max(Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));
                float r = I0.R * sc + margin; float hh = I0.Shape == "CapsuleCollider" ? Mathf.Max(0f, I0.H * 0.5f - I0.R) * sc : 0f;
                var c = t.TransformPoint(I0.Center); var dd = world - c; if (hh > 0f) dd.y = Mathf.Sign(dd.y) * Mathf.Max(0f, Mathf.Abs(dd.y) - hh);
                return dd.sqrMagnitude <= r * r;
            }
            float rr = Mathf.Max(I0.Radius, 1f); return (world - t.TransformPoint(I0.Center)).sqrMagnitude <= rr * rr;
        }
        /// <summary>Capsula del CharacterController (0,3 .. 1,3 m): tres muestras.</summary>
        static bool PlayerInside(EpNode n, Vector3 feet)
        {
            const float margin = 0.3f;
            return InsideShape(n, feet + Vector3.up * 0.3f, margin) || InsideShape(n, feet + Vector3.up * 0.8f, margin) || InsideShape(n, feet + Vector3.up * 1.3f, margin);
        }

        void ScanZones(Transform self)
        {
            Vector3 feet = self.position; EpNode use = null; float useT = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i]; if (n == null || n.Go == null) continue;
                int ty = n.Type; if (ty != EpT.Jumppad && ty != EpT.Ring && ty != EpT.Launcher && ty != EpT.Switch) continue;
                bool inside = false;
                if ((n.T.position - feet).sqrMagnitude < 900f && n.Go.activeInHierarchy) inside = PlayerInside(n, feet);
                if (inside && !n.Inside) { n.Inside = true; n.EnterTime = Time.time; OnZoneEnter(n); }
                else if (!inside && n.Inside) n.Inside = false;
                if ((ty == EpT.Launcher || ty == EpT.Switch) && n.Inside && n.Activated && n.EnterTime < useT) { use = n; useT = n.EnterTime; }
            }
            nearUse = ride == Ride.None ? use : null;
        }

        void OnZoneEnter(EpNode n)
        {
            if (!n.Activated) return;
            if (n.Type == EpT.Jumppad) { insideJumppad = true; jumpPower = n.I.AddPower; jumpRangeTime = Time.time; }   // InsideJumppadRange(addPower)
            else if (n.Type == EpT.Ring) UseRing(n);
        }

        void UseRing(EpNode n)
        {   // cnOwnAvatarStatus.UseRing + EpTrigger.CrashUse: solo con carrera; el aro se oculta siempre
            if (RaceSystem.Racing && n.ServerId > 0) RaceSystem.PickRing(n.ServerId);
            n.Deactive();
        }

        void DoJumpPad(Transform self)
        {   // Jump(currentJumppadPower) + JumpPadPacket()
            var g = GC(); if (g == null) return;
            g.SkillJump(jumpPower - g.JumpVelocity);   // vy = JumpVelocity + extra = addPower
            if (!g.IsAirborne) return;                  // nadando / dash: SkillJump no hizo nada
            Sfx.PlayAt("ui sound/sfx_bouncetech.wav", self.position, 0.7f);
            var ax = Axes(); var h = selfVel;
            // sP_CL2FE_REQ_PC_JUMPPAD (Size 40): iCliTime@0, iX@8, iY@12, iZ@16, iVX@20, iVY@24, iVZ@28 (cm/s), iAngle@32, cKeyValue@36
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_JUMPPAD, 40);
            BitConverter.GetBytes(NowMs()).CopyTo(p, 4);
            PutI(p, 12, Cm(self.position.x)); PutI(p, 16, Cm(self.position.z)); PutI(p, 20, Cm(self.position.y));
            PutI(p, 24, Cm(h.x)); PutI(p, 28, Cm(h.z)); PutI(p, 32, Cm(jumpPower));
            PutI(p, 36, (int)self.eulerAngles.y - 180); p[40] = DirNum(ax.x, ax.y);
            Send(p);
            Debug.Log("[jumppad] salto con potencia " + jumpPower.ToString("F1"));
        }

        // ================================================================ activacion / interruptores (EpTrigger.Active / Deactive / SwitchOn / SwitchOff) ================================================================
        void SwitchStart(EpNode n)
        {   // EpSwitchTrigger.Start: busca el objetivo por objectId y CheckSwitch
            if (n.I.TargetElementId != 0) n.Target = FindOther(n, n.I.TargetElementId);
            if (n.SwitchedOn) n.SwitchOn(); else n.SwitchOff();
        }

        void UseNode(EpNode n)
        {   // EpTrigger.Use(go) para Switch y Launcher (la tirolina la usa ZiplineRide)
            if (n == null || n.Go == null || !n.Activated) return;
            if (n.Type == EpT.Switch)
            {
                if (!n.SwitchedOn && n.Target != null && n.ServerId > 0)
                {
                    var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_IM_CHANGE_SWITCH_STATUS, 4); PutI(p, 4, n.ServerId); Send(p);   // cnOwnAvatarStatus.SwitchOn
                    n.SwitchOn();
                }
            }
            else if (n.Type == EpT.Launcher) StartAim(n);
        }

        /// <summary>CombatSystem (via ZiplineRide.Intercept): true = el ataque se consumio (usar interruptor / lanzador, o lanzador / tobogan / cuerda en curso).</summary>
        public static bool Intercept(bool want)
        {
            var e = Instance; if (e == null) return false;
            if (e.ride != Ride.None) { e.prevWant = want; return true; }
            if (e.nearUse == null || NpcUI.MobTarget != null) { e.prevWant = want; return false; }
            if (want && !e.prevWant) e.UseNode(e.nearUse);
            e.prevWant = want; return true;
        }
        /// <summary>El jugador esta en un lanzador / tobogan / cuerda (GameClient no mueve, BroomFlight.Active = true).</summary>
        public static bool Riding { get { return Instance != null && Instance.ride != Ride.None; } }

        Texture2D hint;
        void OnGUI()
        {
            if (ride == Ride.LaunchAim) { DrawLauncherGui(); return; }
            if (WelcomePopup.Active || Event.current.type != EventType.Repaint) return;
            if (nearUse == null || ride != Ride.None || ZiplineRide.NearTrigger || NpcUI.MobTarget != null || ChatHud.Typing || GameMenu.Blocking || NpcUI.Active || VehicleRide.SelfRiding) return;
            if (hint == null) hint = UiKit.Tex("target_icon3"); if (hint == null) return;
            float s = GameOptions.UiScale; float a = 0.5f + (Mathf.Sin(Time.time * 5f) + 1f) * 0.25f;   // PrintName.CombatIcon[4] pulsante
            var oc = GUI.color; int od = GUI.depth; GUI.color = new Color(a, a, a, a); GUI.depth = 900;
            float w = hint.width * s, h = hint.height * s; GUI.DrawTexture(new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h), hint, ScaleMode.StretchToFill, true);
            GUI.color = oc; GUI.depth = od;
        }

        // ================================================================ aros / interruptores de instancia (EpSynchronizer.InitRings / ResetRings / Activate / Deactivate) ================================================================
        bool pendingInit, lastInfoValid; Rect initArea; int[] initSwitches = new int[0]; float lastInfoTime, initSince, nextInitTry;

        void OnInsMapInfo(Rect area, int[] sw)
        {   // GameFrame INSTANCE_MAP_INFO -> InitRings(area, switches)
            if (!RaceSystem.InsMap)
            {
                pendingInit = lastInfoValid = false; initArea = default; initSwitches = new int[0];
                foreach (var n in nodes) if (n != null && n.Go != null && (n.Type == EpT.Ring || n.Type == EpT.Switch))
                { n.ServerId = -1; if (n.Type == EpT.Ring) n.Deactive(); else n.SwitchOff(); }
                return;
            }
            initArea = area; initSwitches = sw ?? new int[0]; pendingInit = true; initSince = Time.unscaledTime; lastInfoValid = true; lastInfoTime = Time.unscaledTime;
        }
        void OnRacingChanged(bool racing)
        {   // REP_EP_RACE_START_SUCC -> ActivateRings; fin / cancelacion / fallo -> DeactivateRings (del dong del jugador)
            q.Enqueue(() =>
            {
                Vector3 p = ents != null && ents.Self != null ? ents.Self.transform.position : Vector3.zero;
                int cx = (int)p.x / 512, cz = (int)p.z / 512;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i]; if (n == null || n.Go == null || n.Type != EpT.Ring || n.ServerId <= 0) continue;
                    if (CellX(n) != cx || CellZ(n) != cz) continue;
                    if (racing) n.Active(); else n.Deactive();
                }
            });
        }
        static int CellX(EpNode n) { return (int)n.T.position.x / 512; }
        static int CellZ(EpNode n) { return (int)n.T.position.z / 512; }

        void ResetRings(Vector3 p)
        {   // ResetRings(vPos): aros y interruptores del dong del jugador -> serverId = -1, aros ocultos, interruptores en SwitchOff
            int cx = (int)p.x / 512, cz = (int)p.z / 512;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i]; if (n == null || n.Go == null) continue;
                if (n.Type != EpT.Ring && n.Type != EpT.Switch) continue;
                if (CellX(n) != cx || CellZ(n) != cz) continue;
                n.ServerId = -1;
                if (n.Type == EpT.Ring) n.Deactive(); else n.SwitchOff();
            }
        }

        void TickInstance()
        {
            if (!pendingInit || Time.unscaledTime < nextInitTry) return;
            nextInitTry = Time.unscaledTime + 0.5f;
            if (Time.unscaledTime - initSince > 60f) { pendingInit = false; return; }
            // el dong del centro del area; sus aros / interruptores solo estan cuando ya se ha construido
            int cx = (int)((initArea.xMin + initArea.xMax) / 1024f), cz = (int)((initArea.yMin + initArea.yMax) / 1024f);
            var rings = new List<EpNode>(); var switches = new List<EpNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i]; if (n == null || n.Go == null) continue;
                if (n.Type != EpT.Ring && n.Type != EpT.Switch) continue;
                if (CellX(n) != cx || CellZ(n) != cz) continue;
                var pt = new Vector2(n.T.position.x, n.T.position.z); if (!initArea.Contains(pt)) continue;
                if (n.Type == EpT.Ring) rings.Add(n); else switches.Add(n);
            }
            if (rings.Count == 0 && switches.Count == 0) return;   // aun no esta cargado: se reintenta
            rings.Sort((a, b) => a.I.Order.CompareTo(b.I.Order)); switches.Sort((a, b) => a.I.Order.CompareTo(b.I.Order));
            int num = 1; foreach (var r in rings) { r.Deactive(); r.ServerId = num++; }
            int snum = 1;
            foreach (var s in switches)
            {
                s.SwitchOff(); s.ServerId = snum++;
                for (int k = 0; k < initSwitches.Length; k++) if (initSwitches[k] == s.ServerId) { s.SwitchOn(); break; }
            }
            pendingInit = false;
            Debug.Log("[ep] instancia: " + rings.Count + " aros, " + switches.Count + " interruptores (area " + initArea + ")");
            if (RaceSystem.Racing) OnRacingChanged(true);
        }
    }

}
