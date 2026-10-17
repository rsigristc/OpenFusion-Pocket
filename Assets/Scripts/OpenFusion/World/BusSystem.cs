// BusSystem: port de cnBusContainer + cnBusMoveController (autobuses del mundo, eTT 3) y de su relacion con el avatar (enCol.currentBus, busPosition, busMoveDelta). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/cnBusContainer.cs, cnBusMoveController.cs, EnvironmentCollision (HandleBusCollision, CalculatePlatformVectors), cnAvatarThirdPersonMove (MovePacket / OnControllerColliderHit),
// UserMoveController.MoveTransportation, UserContainer.LateUpdate (ForceAllUpdate), GameFrame (822083739..744, 822083764, 822083829), AppearEffect (Delete).
// Paquetes (cuerpos sin los 4 bytes del tipo; solo se atienden los de eTT == 3, como GameFrame):
//   TRANSPORTATION_ENTER (822083739) / TRANSPORTATION_NEW (822083742), 24 B: sTransportationAppearanceData {eTT, iT_ID, iT_Type, iX, iY, iZ} -> AddBus.
//   TRANSPORTATION_EXIT (822083740), 8 B {eTT, iT_ID} -> DelayDeleteBus.
//   TRANSPORTATION_MOVE (822083741), 28 B {eTT, iT_ID, iToX, iToY, iToZ, iSpeed, short iMoveStyle} -> bus.Goto(ServerToClient(iTo*), iSpeed * 0,01).
//   TRANSPORTATION_AROUND (822083743) {iCnt} + n x sTransportationAppearanceData. El original lee la lista en SizeOf(TAD) * (i + 1) = 24 * (i + 1) (no en 4 + 24 * i): aqui se detecta por la longitud del paquete.
//   AROUND_DEL_TRANSPORTATION (822083744) {eTT, iCnt} + n x int iT_ID -> DelayDeleteBus.
//   PC_MOVETRANSPORTATION (822083764, 72 B): jugador remoto sobre un autobus: posicion = bus + iLc (si el bus existe; si no iX,iY,iZ), velocidad (fVX, fVZ, fVY), angulo, tecla, iSpeed * 0,01; queda "montado" (se mueve con el bus).
//   REQ_PC_MOVETRANSPORTATION (318767202, cuerpo 60 = tamano natural: u64 + 3 int Lc + 3 int pos + 3 float V + int T_ID + int angulo + byte tecla (+3 relleno) + int velocidad): lo manda BusSystem.TrySend mientras el avatar va sobre un autobus.
//   REQ_PC_TRANSPORT_WARP (318767239, 16) / REP_PC_TRANSPORT_WARP_SUCC (822083829, 36): ver TransportWindow (RequestTransportWarp / evento TransportWarpSucc).
// Tablas: TransportationTable.m_pTransportationData[iT_Type] (indice de la matriz, GetTableData(14, 0, iT_Type)) -> m_iMesh -> m_pTransportationMeshData[m_iMesh] (GetTableData(14, 1, ...)); modelo "mob/<m_pstrMMeshModelString>.kfm",
//   textura "main" = m_pstrMTextureString y "sub" = m_pstrMTextureString2 (materiales cuyo nombre contiene "main" / "sub"), animacion "stand1" en bucle.
// Diferencias asumidas: el original NO gira el autobus (Quaternion.identity y sin LookAt): tampoco aqui (BusSystem.ModelYaw = giro opcional del modelo); sin aparicion / desaparicion con alfa (AppearEffect: 2 s) -> solo la espera de 2 s antes de borrar.
// Gancho pendiente en GameClient.Update (ver informe): junto a EpTriggers.OnSurface / TrySendSurface usar BusSystem.OnBus / BusSystem.TrySend para mandar REQ_PC_MOVETRANSPORTATION en lugar de PC_MOVE / PC_STOP.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    /// <summary>cnBusMoveController: un autobus del mundo. Se mueve hacia TargetPosition a la velocidad indicada (ForceUpdate).</summary>
    public sealed class BusMover : MonoBehaviour
    {
        public int Id, BusType;
        public TransportationElement Table; public MeshNameElement Mesh;
        public Vector3 TargetPosition, Movement, Vel;
        public float DeleteAt = -1f, NextModelTry; public int ModelTries; public GameObject Model;
        float moveSpeed = 6f;
        public float Speed { get { return moveSpeed; } }

        /// <summary>cnBusMoveController.Goto: si el autobus va mas de 1 m por detras de su destino anterior salta a el (y arrastra al avatar que va encima).</summary>
        public void Goto(Vector3 target, float speed)
        {
            if ((target - TargetPosition).magnitude < 0.1f && Mathf.Abs(speed - moveSpeed) < 0.01f) return;
            Vector3 v = TargetPosition - transform.position;
            if (v.magnitude > 1f)
            {
                transform.position = TargetPosition;
                BusSystem.OnBusSnapped(this, v);
            }
            moveSpeed = speed; TargetPosition = target;
        }

        /// <summary>cnBusMoveController.ForceUpdate (UserContainer.LateUpdate -> cnBusContainer.ForceAllUpdate).</summary>
        public void Step(float dt)
        {
            Movement = TargetPosition - transform.position;
            if (Movement.magnitude >= 0.1f)
            {
                float mag = Movement.magnitude; Movement.Normalize(); Movement *= Mathf.Min(moveSpeed * dt, mag);
            }
            else Movement = Vector3.zero;
            transform.position += Movement;
            Vel = dt > 1e-5f ? Movement / dt : Vector3.zero;
        }
    }

    /// <summary>OnControllerColliderHit del CharacterController del avatar propio -> BusSystem (como EpHitRelay, pero propio para no pisar el de EpTriggers).</summary>
    public sealed class BusHitRelay : MonoBehaviour
    {
        public Action<ControllerColliderHit> Hit;
        void OnControllerColliderHit(ControllerColliderHit h) { if (Hit != null) Hit(h); }
    }

    [DefaultExecutionOrder(450)]
    public sealed class BusSystem : MonoBehaviour
    {
        public static BusSystem Instance;
        public static BusSystem Ensure(GameObject host) { var b = host.GetComponent<BusSystem>(); if (b == null) b = host.AddComponent<BusSystem>(); Instance = b; return b; }
        public Func<ModelFactory> GetModels;
        /// <summary>Giro (grados) del modelo respecto al contenedor. El original no gira el autobus (identidad): 0.</summary>
        public static float ModelYaw = 0f;
        /// <summary>true = mandar REQ_PC_MOVETRANSPORTATION sobre un autobus (requiere el gancho de GameClient). false = PC_MOVE absoluto.</summary>
        public static bool SendBusPackets = true;
        /// <summary>Segundos sin tocar el autobus tras los que el avatar deja de considerarse encima (el original lo anula al chocar con otro suelo).</summary>
        public static float ContactTimeout = 2.5f;

        struct Tad { public int Tt, Id, Type, X, Y, Z; }
        sealed class Rider { public BusMover Bus; public Vector3 Last; }

        WorldEntities ents; PacketDispatcher disp; TransportationTableScript ts;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly Dictionary<int, BusMover> buses = new Dictionary<int, BusMover>();
        readonly Dictionary<int, Rider> riders = new Dictionary<int, Rider>();
        Transform root;
        // enCol del avatar propio
        Transform selfT; BusHitRelay relay; BusMover curBus; Vector3 busPos; Transform curSurface; float lastHit = -100f;
        bool warpPending; float warpAt; int unblockFrame; float lastWarpReq = -100f; int diagBus;

        // ============================================================ API estatica ============================================================
        public static int Count { get { return Instance != null ? Instance.buses.Count : 0; } }
        /// <summary>enCol.currentBus != null: el avatar propio va sobre un autobus.</summary>
        public static bool OnBus { get { var i = Instance; return SendBusPackets && i != null && i.curBus != null && i.selfT != null; } }
        public static int CurrentBusId { get { var i = Instance; return i != null && i.curBus != null ? i.curBus.Id : -1; } }
        /// <summary>cnBusContainer.SearchBus.</summary>
        public static BusMover Search(int id) { var i = Instance; if (i == null) return null; BusMover b; return i.buses.TryGetValue(id, out b) && b != null ? b : null; }

        /// <summary>Lo que cnAvatarThirdPersonMove.MovePacket manda sobre un autobus en lugar de PC_MOVE / PC_STOP. vx, vy(vertical), vz en unidades de cliente (x, y, z de Unity, m/s, incluyen el arrastre del autobus: se descuenta);
        /// angle = (int)eulerY - 180; key = DirNum (0 = parado); speed = RunSpeed. false si el avatar no va sobre un autobus.</summary>
        public static bool TrySend(Transform self, float vx, float vy, float vz, int angle, byte key, int speed)
        {
            var i = Instance; if (!OnBus || self == null || NpcUI.Sender == null) return false;
            var bus = i.curBus; var pos = self.position; var lc = pos - bus.transform.position;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_MOVETRANSPORTATION, 60);
            BitConverter.GetBytes((ulong)(Time.realtimeSinceStartup * 1000f)).CopyTo(p, 4);                      // iCliTime
            PutI(p, 12, Cm(lc.x)); PutI(p, 16, Cm(lc.z)); PutI(p, 20, Cm(lc.y));                                  // iLcX, iLcY, iLcZ (CoordUtil.ClientToServer: x, z, y)
            PutI(p, 24, Cm(pos.x)); PutI(p, 28, Cm(pos.z)); PutI(p, 32, Cm(pos.y));                                // iX, iY, iZ
            if (key != 0) { var v = new Vector3(vx, vy, vz) - bus.Vel; PutF(p, 36, v.x); PutF(p, 40, v.z); PutF(p, 44, v.y); }   // fVX, fVY, fVZ = kMovement.x, z, y (parado: 0)
            PutI(p, 48, bus.Id); PutI(p, 52, angle); p[56] = key; PutI(p, 60, key != 0 ? speed : 0);              // iT_ID, iAngle, cKeyValue, iSpeed
            try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[bus] envio: " + ex.Message); return false; }
            return true;
        }
        static void PutI(byte[] b, int o, int v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static void PutF(byte[] b, int o, float v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static int Cm(float v) { return (int)(v * 100f); }   // CoordUtil.ClientToServer (truncado)
        static int I(byte[] b, int o) { return b.Length >= o + 4 ? BitConverter.ToInt32(b, o) : 0; }
        static float F(byte[] b, int o) { return b.Length >= o + 4 ? BitConverter.ToSingle(b, o) : 0f; }
        static Vector3 S2C(int x, int y, int z) { return new Vector3(x * 0.01f, z * 0.01f, y * 0.01f); }   // CoordUtil.ServerToClient
        static Tad ReadTad(byte[] b, int o) { return new Tad { Tt = I(b, o), Id = I(b, o + 4), Type = I(b, o + 8), X = I(b, o + 12), Y = I(b, o + 16), Z = I(b, o + 20) }; }

        // ============================================================ ciclo de vida ============================================================
        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t)
        {
            Detach();
            ents = e; disp = d; ts = t != null ? t.m_pTransportationTable : null; Instance = this;
            if (root == null) { var rg = new GameObject("Buses"); root = rg.transform; }
            if (disp != null) { disp.Unhandled += OnPacket; disp.PcMove += OnPcMove; disp.PcStop += OnPcStop; disp.PcJump += OnPcJump; disp.PcExit += OnPcExit; }
            if (ents != null) ents.Teleported += OnTeleported;
            TransportWindow.TransportWarpSucc += OnTransportWarpSucc;
        }
        void Detach()
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.PcMove -= OnPcMove; disp.PcStop -= OnPcStop; disp.PcJump -= OnPcJump; disp.PcExit -= OnPcExit; }
            if (ents != null) ents.Teleported -= OnTeleported;
            TransportWindow.TransportWarpSucc -= OnTransportWarpSucc;
        }
        void OnDestroy() { Detach(); if (Instance == this) Instance = null; if (root != null) Destroy(root.gameObject); }

        // ============================================================ red (hilo de red: solo encolar) ============================================================
        void Post(Action a) { q.Enqueue(a); }
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            switch (type)
            {
                case PacketIds.P_FE2CL_TRANSPORTATION_ENTER:
                case PacketIds.P_FE2CL_TRANSPORTATION_NEW:
                    if (b.Length >= 24) { var tad = ReadTad(b, 0); if (tad.Tt == 3) Post(() => AddBus(tad)); }
                    break;
                case PacketIds.P_FE2CL_TRANSPORTATION_EXIT:
                    if (b.Length >= 8 && I(b, 0) == 3) { int id = I(b, 4); Post(() => DelayDeleteBus(id)); }
                    break;
                case PacketIds.P_FE2CL_TRANSPORTATION_MOVE:
                    if (b.Length >= 24 && I(b, 0) == 3)
                    {
                        int id = I(b, 4), x = I(b, 8), y = I(b, 12), z = I(b, 16), spd = I(b, 20);
                        Post(() => MoveBus(id, x, y, z, spd));
                    }
                    break;
                case PacketIds.P_FE2CL_TRANSPORTATION_AROUND:
                    if (b.Length >= 4)
                    {
                        int cnt = Math.Min(I(b, 0), 256); if (cnt <= 0) break;
                        int off = b.Length >= 24 * (cnt + 1) ? 24 : 4;   // original: SizeOf(TAD) * (i + 1); formato limpio: 4 + 24 * i
                        cnt = Math.Min(cnt, (b.Length - off) / 24);
                        var list = new List<Tad>(); for (int i = 0; i < cnt; i++) { var t = ReadTad(b, off + 24 * i); if (t.Tt == 3) list.Add(t); }
                        if (list.Count > 0) Post(() => { foreach (var t in list) AddBus(t); });
                    }
                    break;
                case PacketIds.P_FE2CL_AROUND_DEL_TRANSPORTATION:
                    if (b.Length >= 8 && I(b, 0) == 3 && I(b, 4) > 0)
                    {
                        int cnt = Math.Min(I(b, 4), Math.Max(0, (b.Length - 8) / 4)); var ids = new int[cnt]; for (int i = 0; i < cnt; i++) ids[i] = I(b, 8 + 4 * i);
                        if (cnt > 0) Post(() => { foreach (int id in ids) DelayDeleteBus(id); });
                    }
                    break;
                case PacketIds.P_FE2CL_PC_MOVETRANSPORTATION:
                    if (b.Length >= 64)
                    {
                        int pc = I(b, 60), busId = I(b, 44), angle = I(b, 48), speed = I(b, 56); byte key = b[52];
                        Vector3 lc = S2C(I(b, 8), I(b, 12), I(b, 16)), abs = S2C(I(b, 20), I(b, 24), I(b, 28)), vel = new Vector3(F(b, 32), F(b, 40), F(b, 36));   // zero.x = fVX, zero.y = fVZ, zero.z = fVY
                        Post(() => RemoteBusMove(pc, busId, lc, abs, vel, speed * 0.01f, angle, key));
                    }
                    break;
            }
        }
        void OnPcMove(PcMove m) { int id = m.Id; Post(() => riders.Remove(id)); }       // un PC_MOVE normal = ya no esta sobre el autobus
        void OnPcStop(PcStop s) { int id = s.Id; Post(() => riders.Remove(id)); }
        void OnPcJump(PcJump j) { int id = j.Id; Post(() => riders.Remove(id)); }       // UserMoveController.Jump: currentBus = null
        void OnPcExit(int id) { Post(() => riders.Remove(id)); }

        // ============================================================ cnBusContainer ============================================================
        /// <summary>cnBusContainer.Add (ENTER / NEW / AROUND).</summary>
        void AddBus(Tad d)
        {
            if (curBus != null && curBus.Id == d.Id)
            {   // el avatar va en este autobus: solo se actualiza su destino
                var kv = S2C(d.X, d.Y, d.Z); curBus.TargetPosition = kv; curBus.Goto(kv, 12f); return;
            }
            DeleteBus(d.Id);
            TransportationElement tel = null; MeshNameElement mesh = null;
            if (ts != null && ts.m_pTransportationData != null && d.Type >= 0 && d.Type < ts.m_pTransportationData.Length) tel = ts.m_pTransportationData[d.Type];
            if (tel == null) { Debug.LogWarning("[bus] tipo de transporte desconocido " + d.Type + " (id " + d.Id + ")"); return; }
            if (ts.m_pTransportationMeshData != null && tel.m_iMesh >= 0 && tel.m_iMesh < ts.m_pTransportationMeshData.Length) mesh = ts.m_pTransportationMeshData[tel.m_iMesh];
            var pos = S2C(d.X, d.Y, d.Z);
            if (root == null) { var rg = new GameObject("Buses"); root = rg.transform; }
            var go = new GameObject("Bus#" + d.Id + " t" + d.Type); go.transform.SetParent(root, false); go.transform.position = pos; go.transform.rotation = Quaternion.identity;
            var bm = go.AddComponent<BusMover>(); bm.Id = d.Id; bm.BusType = d.Type; bm.Table = tel; bm.Mesh = mesh; bm.TargetPosition = pos; bm.Goto(pos, 12f);
            buses[d.Id] = bm;
            Debug.Log("[bus] alta id=" + d.Id + " tipo=" + d.Type + " malla=" + (mesh != null ? mesh.m_pstrMMeshModelString : "(sin malla)") + " pos=" + pos + " buses=" + buses.Count);
            TryBuildModel(bm);
        }

        /// <summary>TRANSPORTATION_MOVE: bus.Goto(destino, velocidad).</summary>
        void MoveBus(int id, int x, int y, int z, int speed)
        {
            BusMover bm; if (!buses.TryGetValue(id, out bm) || bm == null) return;
            bm.Goto(S2C(x, y, z), speed * 0.01f);
        }

        /// <summary>cnBusContainer.DelayDelete: si el avatar va en el autobus, REQ_PC_TRANSPORT_WARP; si no, AppearEffect.Delete (2 s y luego Delete).</summary>
        void DelayDeleteBus(int id)
        {
            BusMover bm; if (!buses.TryGetValue(id, out bm) || bm == null) return;
            if (RidingBus(id)) { RequestWarp(id); return; }
            bm.DeleteAt = Time.time + 2f;
        }

        /// <summary>cnBusContainer.Delete: si el avatar va en el autobus se pide el TRANSPORT_WARP y el autobus se conserva; si no, se destruye.</summary>
        void DeleteBus(int id)
        {
            BusMover bm; if (!buses.TryGetValue(id, out bm)) return;
            if (bm != null && RidingBus(id)) { RequestWarp(id); return; }
            buses.Remove(id); if (bm != null) Destroy(bm.gameObject);
            var drop = new List<int>(); foreach (var kv in riders) if (kv.Value.Bus == bm) drop.Add(kv.Key); foreach (int pid in drop) riders.Remove(pid);
        }

        bool RidingBus(int id) { return selfT != null && curBus != null && curBus.Id == id; }

        /// <summary>REQ_PC_TRANSPORT_WARP {iT_ID, posicion del avatar - posicion del autobus} + bEnableSendPacket = false (WarpFlow.Sent).</summary>
        void RequestWarp(int id)
        {
            if (selfT == null || curBus == null) return;
            if (warpPending && Time.unscaledTime - lastWarpReq < 1f) return;   // el original repite el REQ por cada aviso; aqui como mucho 1 por segundo
            Vector3 off = selfT.position - curBus.transform.position;
            TransportWindow.RequestTransportWarp(id, off);
            warpPending = true; warpAt = Time.unscaledTime + 8f; lastWarpReq = Time.unscaledTime;
            Debug.Log("[bus] el autobus " + id + " desaparece con el avatar encima: REQ_PC_TRANSPORT_WARP offset=" + off);
        }

        /// <summary>cnBusContainer.RemoveAdd (REP_PC_TRANSPORT_WARP_SUCC): conserva solo el autobus del avatar (y lo recoloca en el TAD); destruye el resto.</summary>
        void RemoveAddBus(Tad d)
        {
            BusMover keep = null;
            var all = new List<BusMover>(buses.Values);
            foreach (var bm in all)
            {
                if (bm == null) continue;
                if (selfT != null && curBus != null && bm.Id == d.Id)
                {
                    var kv = S2C(d.X, d.Y, d.Z); keep = curBus; keep.TargetPosition = kv; keep.Goto(kv, 12f);
                }
                else Destroy(bm.gameObject);
            }
            buses.Clear(); riders.Clear();
            if (keep != null) buses[d.Id] = keep;
        }

        // ============================================================ TRANSPORT_WARP_SUCC (GameFrame 822083829) ============================================================
        void OnTransportWarpSucc(byte[] body)
        {
            if (body == null || body.Length < 36) return;
            var tad = ReadTad(body, 0); Vector3 lc = S2C(I(body, 24), I(body, 28), I(body, 32));
            warpPending = false;
            RemoveAddBus(tad);   // (los eventos 5,7 / 4,9 / 7,3 ya los hizo TransportWindow)
            var bus = Search(tad.Id);
            if (bus == null && tad.Tt == 3) { AddBus(tad); bus = Search(tad.Id); }   // el original no lo recrea si el avatar no iba encima; aqui si (el servidor lo habria reenviado igualmente)
            if (bus != null && selfT != null)
            {
                selfT.position = bus.transform.position + lc; Physics.SyncTransforms();
                curBus = bus; busPos = bus.transform.position; lastHit = Time.time; curSurface = null;
                Debug.Log("[bus] TRANSPORT_WARP_SUCC: bus " + tad.Id + " pos=" + bus.transform.position + " avatar=" + selfT.position);
            }
            // bEnableSendPacket = true: dos fotogramas despues, para que GameClient fije lastPos en la nueva posicion sin mandar un salto enorme
            WarpFlow.Sent(); unblockFrame = Time.frameCount + 2;
        }

        // ============================================================ avatar propio (EnvironmentCollision) ============================================================
        void OnSelfChanged(Transform s)
        {
            selfT = s; ClearBus(); riders.Clear(); warpPending = false;
            if (s != null) { relay = s.GetComponent<BusHitRelay>(); if (relay == null) relay = s.gameObject.AddComponent<BusHitRelay>(); relay.Hit = OnHit; }
        }
        void ClearBus() { curBus = null; curSurface = null; }
        void OnTeleported(int x, int y, int z) { Post(() => { if (!warpPending) ClearBus(); }); }

        void OnHit(ControllerColliderHit h)
        {
            if (h == null || h.collider == null || h.collider.isTrigger || selfT == null) return;
            var bm = h.collider.GetComponentInParent<BusMover>();
            if (bm != null) { HandleBusCollision(h.collider.transform, bm); return; }
            // otro suelo (ResetCollisionVariables): solo si se pisa algo por debajo y hace rato que no se toca el autobus
            if (curBus != null && !warpPending && h.normal.y > 0.5f && h.point.y <= selfT.position.y + 0.3f && Time.time - lastHit > 0.15f) ClearBus();
        }
        /// <summary>EnvironmentCollision.HandleBusCollision.</summary>
        void HandleBusCollision(Transform t, BusMover bm)
        {
            if ((curSurface != null && !(curSurface.position.y >= t.position.y)) || (lastHit < Time.time && curSurface != t) || curBus == null)
            {
                lastHit = Time.time; curSurface = t; busPos = bm.transform.position;
                if (curBus != bm) { curBus = bm; if (diagBus++ < 12) Debug.Log("[bus] el avatar sube al autobus " + bm.Id); }
            }
            else if (lastHit < Time.time && curSurface == t) lastHit = Time.time;
        }
        /// <summary>cnBusMoveController.Goto: salto del autobus > 1 m: el avatar que va encima se desplaza el mismo vector y busPosition = nueva posicion.</summary>
        public static void OnBusSnapped(BusMover bus, Vector3 v)
        {
            var i = Instance; if (i == null || i.selfT == null || i.curBus != bus) return;
            i.selfT.position += v; Physics.SyncTransforms(); i.busPos = bus.TargetPosition;
        }

        // ============================================================ jugadores remotos ============================================================
        /// <summary>UserMoveController.MoveTransportation.</summary>
        void RemoteBusMove(int pc, int busId, Vector3 lc, Vector3 abs, Vector3 vel, float speed, int angle, byte key)
        {
            if (ents == null || pc == ents.SelfId) return;   // propio: GameFrame solo sincroniza el reloj (SyncEpElement)
            var tr = ents.TransformOfPlayer(pc); if (tr == null) return;
            BusMover bus; buses.TryGetValue(busId, out bus); if (bus != null && !bus) bus = null;
            Vector3 pos = bus != null ? bus.transform.position + lc : abs;
            var an = tr.GetComponent<NpcAnimator>(); if (an != null) an.SetKey(key);
            var mv = tr.GetComponent<RemoteMover>(); if (mv != null) mv.OnMove(pos, vel, speed, angle);
            tr.position = pos;
            if (bus != null) { Rider r; if (!riders.TryGetValue(pc, out r)) { r = new Rider(); riders[pc] = r; } r.Bus = bus; r.Last = bus.transform.position; }
            else riders.Remove(pc);
        }

        // ============================================================ bucle ============================================================
        void TryBuildModel(BusMover bm)
        {
            if (bm == null || bm.Model != null) return;
            bm.NextModelTry = Time.unscaledTime + 3f; bm.ModelTries++;
            var mf = GetModels != null ? GetModels() : null; var m = bm.Mesh;
            if (mf == null || m == null || string.IsNullOrEmpty(m.m_pstrMMeshModelString) || m.m_pstrMMeshModelString == "null") { if (m == null || string.IsNullOrEmpty(m.m_pstrMMeshModelString)) bm.NextModelTry = float.MaxValue; return; }
            try
            {
                var mr = new ModelRef { Kind = ModelKind.Npc, Id = bm.BusType, Model = "mob/" + m.m_pstrMMeshModelString.ToLowerInvariant() + ".kfm", Tex1 = ModelCatalog.Tex(m.m_pstrMTextureString), Tex2 = ModelCatalog.Tex(m.m_pstrMTextureString2), Scale = 1f, ApplyTextures = true };
                var go = mf.Instantiate(mr, bm.transform); if (go == null) return;   // bundle sin cargar: se reintenta
                WorldEntities.ActivateAll(go);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
                bm.Model = go;
                int cols = go.GetComponentsInChildren<Collider>(true).Length, mfs = 0, skins = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                if (cols == 0)
                {   // sin colisionadores en el modelo: el avatar no podria subirse; se anaden MeshCollider (no convexos) a las mallas visibles
                    foreach (var f in go.GetComponentsInChildren<MeshFilter>(true)) { if (f.sharedMesh == null) continue; var mc = f.gameObject.GetComponent<MeshCollider>(); if (mc == null) mc = f.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = f.sharedMesh; mfs++; }
                }
                var an = go.GetComponentInChildren<Animation>(true);
                if (an != null)
                {
                    an.cullingType = AnimationCullingType.AlwaysAnimate;
                    var st = an["stand1"]; if (st != null) { st.wrapMode = WrapMode.Loop; an.Play("stand1"); }
                }
                Debug.Log("[bus] modelo " + mr.Model + " listo (id " + bm.Id + "): colisionadores=" + cols + (cols == 0 ? " -> MeshCollider anadidos a " + mfs + " mallas" : "") + " skinned=" + skins + " animacion=" + (an != null ? "si" : "no"));
            }
            catch (Exception ex) { Debug.LogWarning("[bus] modelo del autobus " + bm.Id + ": " + ex.Message); }
        }

        void Update()
        {
            Action a; int guard = 0; while (guard++ < 128 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[bus] " + ex.Message); } }
            var s = ents != null && ents.Self != null ? ents.Self.transform : null;
            if (s != selfT) OnSelfChanged(s);
            if (curBus != null && !curBus) ClearBus();   // autobus destruido
            if (unblockFrame > 0 && Time.frameCount >= unblockFrame) { unblockFrame = 0; WarpFlow.Cancel(); }
            if (warpPending && Time.unscaledTime > warpAt) { warpPending = false; Debug.LogWarning("[bus] el servidor no contesto al REQ_PC_TRANSPORT_WARP en 8 s"); }
            if (curBus != null && !warpPending && selfT != null && Time.time - lastHit > ContactTimeout) ClearBus();
            List<BusMover> due = null;
            foreach (var kv in buses)
            {
                var bm = kv.Value; if (bm == null) continue;
                if (bm.Model == null && bm.ModelTries < 600 && Time.unscaledTime >= bm.NextModelTry) TryBuildModel(bm);
                if (bm.DeleteAt >= 0f && Time.time >= bm.DeleteAt) { if (due == null) due = new List<BusMover>(); due.Add(bm); }
            }
            if (due != null) foreach (var bm in due) { bm.DeleteAt = -1f; DeleteBus(bm.Id); }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (var kv in buses) if (kv.Value != null) kv.Value.Step(dt);   // cnBusContainer.ForceAllUpdate
            // avatar propio: EnvironmentCollision.CalculatePlatformVectors (busMoveDelta) + position += busMoveDelta
            if (curBus != null && selfT != null)
            {
                Vector3 cur = curBus.transform.position, lhs = cur - busPos, d = Vector3.zero;
                if (!(busPos.y >= selfT.position.y) || Vector3.Dot(lhs, selfT.position - cur) > 0f) d = lhs;
                busPos = cur;
                if (d.sqrMagnitude > 0f) { selfT.position += d; Physics.SyncTransforms(); }
            }
            // jugadores remotos montados
            if (riders.Count > 0)
            {
                List<int> gone = null;
                foreach (var kv in riders)
                {
                    var r = kv.Value; var tr = ents != null ? ents.TransformOfPlayer(kv.Key) : null;
                    if (r.Bus == null || !r.Bus || tr == null) { if (gone == null) gone = new List<int>(); gone.Add(kv.Key); continue; }
                    Vector3 delta = r.Bus.transform.position - r.Last; r.Last = r.Bus.transform.position;
                    if (delta.sqrMagnitude > 0f) { var mv = tr.GetComponent<RemoteMover>(); if (mv != null) mv.Carry(delta); else tr.position += delta; }
                }
                if (gone != null) foreach (int id in gone) riders.Remove(id);
            }
        }
    }
}
