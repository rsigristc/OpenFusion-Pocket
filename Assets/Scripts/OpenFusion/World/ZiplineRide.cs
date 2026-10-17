// Tirolina (zipline) = EpZiplineTrigger (first_pass) + cnAvatarThirdPersonMove.StartZipline / rama MoveType.EpZipline + cnAvatarAnimation.AvatarZipline +
// UserMoveController.ReceiveMoveZipline (jugadores remotos) + cnAvatarAttack (usar el trigger con la tecla de ataque). SIN COMPILAR NI PROBAR EN UNITY.
//
// Original replicado:
//  * El mapa tiene un MonoBehaviour EpZiplineTrigger (triggerType 2; campos from/to en local del trigger, velocity en m/s) con un colisionador trigger. Al entrar el jugador se registra
//    (RegisterForUserInteraction) y cnAvatarAttack, si no hay NPC/jugador/mob a la vista, convierte el clic/tecla de ataque en trigger.Use -> UseEp -> StartZipline.
//    Con el trigger a mano se dibuja PrintName.CombatIcon[4] (target_n) en el centro de la pantalla con alfa pulsante.
//  * StartZipline: posicion = TransformPoint(from); direccion = (to-from).normalized; yaw = rumbo de la linea; AvatarZipline(1) (oculta el arma, clip "ropedown" en bucle);
//    REQ_PC_ZIPLINE (318767167, cuerpo 76 B, total 80) con mov 0; arma oculta (slot 3), polea wear/pistol_trolley.nif visible (slot 5); si hay nano activo REQ_NANO_ACTIVE(-1).
//  * Cada frame: fZipHeight = (Bip01 L Hand - Bip01 L Toe0).y; mov += velocity*dt; posicion = from + (to-from)*mov/max, y -= fZipHeight (las manos van en la linea); yaw hacia la linea (slerp x4);
//    paquete cada 0,3 s (fVX/Y/Z = direccion con y<->z, iSpeed = velocidad*100, iRollMax 30, iRoll 0, iAngle = (int)rotation.y como el original).
//  * Fin: mov > max -> mov = max, ShowPistol, AvatarZipline(0), Jump(0), polea oculta. Salto (tecla Jump) en medio -> igual pero bDown = 1.
//  * Remotos: PC_ZIPLINE (822083703, cuerpo 88: iPC_ID@76) -> posicion del servidor, extrapolada con la direccion x iSpeed/100; clip ropedown, polea visible, arma oculta; si fMax == fMov o bDown == 1
//    el jugador termina (posicion del servidor, salto, polea oculta).
//  * No hay sonidos, camara especial ni curva: la linea es un objeto del mapa.
// Integracion (ver informe w22): DongBuilder.ApplyScript llama a ZiplineRide.Register(...), WorldBootstrap a ZiplineRide.Ensure(gameObject).Init(Entities, dispatcher) y CombatSystem a
// ZiplineRide.Intercept(want). El estado propio activa BroomFlight.Active (GameClient no mueve ni envia MOVE y InvenMode/VehicleRide lo cuentan como animacion especial).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed class ZiplineRide : MonoBehaviour
    {
        public static ZiplineRide Instance;
        /// <summary>El jugador propio esta en la tirolina (MoveType.EpZipline).</summary>
        public static volatile bool Riding;
        /// <summary>Hay un EpZiplineTrigger al alcance (movecontroller.triggers.Count &gt; 0).</summary>
        public static bool NearTrigger { get { return Instance != null && Instance.near != null; } }

        sealed class Zip
        {
            public GameObject Go; public Vector3 From, To; public float Velocity, Radius;
            public string Shape; public Vector3 Center, Size; public float R, H;   // colisionador trigger (BoxCollider / SphereCollider / CapsuleCollider) en local del objeto
        }
        static readonly List<Zip> zips = new List<Zip>();

        /// <summary>Registro desde DongBuilder.ApplyScript (hilo principal): from/to/velocity/radius del EpZiplineTrigger y, si lo hay, el colisionador trigger del mismo nodo (shape = tipo de componente,
        /// center, size (box) o radius/height (sphere/capsule)). Sin colisionador se usa una esfera de max(radius, 2) m alrededor de from.</summary>
        public static void Register(GameObject go, float[] from, float[] to, float velocity, float radius, string shape, float[] center, float[] size, float shapeRadius, float shapeHeight)
        {
            if (go == null || from == null || to == null || from.Length < 3 || to.Length < 3) return;
            var z = new Zip { Go = go, From = new Vector3(from[0], from[1], from[2]), To = new Vector3(to[0], to[1], to[2]), Velocity = velocity, Radius = radius, Shape = shape };
            if (center != null && center.Length >= 3) z.Center = new Vector3(center[0], center[1], center[2]);
            if (size != null && size.Length >= 3) z.Size = new Vector3(size[0], size[1], size[2]);
            z.R = shapeRadius; z.H = shapeHeight;
            for (int i = zips.Count - 1; i >= 0; i--) if (zips[i].Go == null) zips.RemoveAt(i);
            zips.Add(z);
            Debug.Log("[tirolina] registrada en " + go.name + " desde " + z.From + " hasta " + z.To + " v=" + velocity + " trigger=" + (shape ?? "esfera"));
        }

        /// <summary>Punto (mundo) dentro del volumen trigger del Zip, con un margen (radio del CharacterController).</summary>
        static bool Inside(Zip z, Vector3 world, float margin)
        {
            var t = z.Go.transform; var l = t.InverseTransformPoint(world);
            if (z.Shape == "BoxCollider")
            {
                var d = l - z.Center; var h = z.Size * 0.5f; var s = t.lossyScale;
                float mx = margin / Mathf.Max(0.0001f, Mathf.Abs(s.x)), my = margin / Mathf.Max(0.0001f, Mathf.Abs(s.y)), mz = margin / Mathf.Max(0.0001f, Mathf.Abs(s.z));
                return Mathf.Abs(d.x) <= h.x + mx && Mathf.Abs(d.y) <= h.y + my && Mathf.Abs(d.z) <= h.z + mz;
            }
            if (z.Shape == "SphereCollider" || z.Shape == "CapsuleCollider")
            {
                float sc = Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Max(Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));
                float r = z.R * sc + margin; float hh = z.Shape == "CapsuleCollider" ? Mathf.Max(0f, z.H * 0.5f - z.R) * sc : 0f;
                var c = t.TransformPoint(z.Center); var dd = world - c; if (hh > 0f) dd.y = Mathf.Sign(dd.y) * Mathf.Max(0f, Mathf.Abs(dd.y) - hh);
                return dd.sqrMagnitude <= r * r;
            }
            float rr = Mathf.Max(z.Radius, 2f); return (world - t.TransformPoint(z.From)).sqrMagnitude <= rr * rr;
        }

        // ------------------------------------------------------------------------------------------------------------------------------
        WorldEntities ents; PacketDispatcher disp; GameClient gc;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        Zip near; bool prevWant; float nearScan; float nextStart;

        public static ZiplineRide Ensure(GameObject host) { var v = host.GetComponent<ZiplineRide>(); if (v == null) v = host.AddComponent<ZiplineRide>(); Instance = v; return v; }
        public void Init(WorldEntities e, PacketDispatcher d)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            if (ents != null) ents.Teleported -= OnTeleport;
            ents = e; disp = d; Instance = this;
            if (disp != null) disp.Unhandled += OnPacket;
            if (ents != null) ents.Teleported += OnTeleport;
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; if (ents != null) ents.Teleported -= OnTeleport; if (Riding) { Riding = false; BroomFlight.Active = false; } if (Instance == this) Instance = null; }
        void OnTeleport(int x, int y, int z) { q.Enqueue(() => { if (Riding) EndSelf(false, false); }); }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null; if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }
        /// <summary>ActorSkinCombiner.LinkObjectActive(slot, on): activa/desactiva los renderers de los objetos acoplados del hueco (3 = arma, 5 = polea).</summary>
        static void Link(Transform holder, int slot, bool on)
        {
            if (holder == null) return;
            foreach (var tg in holder.GetComponentsInChildren<ActorAttachTag>(true))
                if (tg.Slot == slot) foreach (var rd in tg.GetComponentsInChildren<Renderer>(true)) rd.enabled = on;
        }
        static int I(byte[] b, int o) { return b.Length >= o + 4 ? BitConverter.ToInt32(b, o) : 0; }
        static float F(byte[] b, int o) { return b.Length >= o + 4 ? BitConverter.ToSingle(b, o) : 0f; }
        static void PutI(byte[] b, int o, int v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static void PutF(byte[] b, int o, float v) { BitConverter.GetBytes(v).CopyTo(b, o); }
        static Vector3 SToC(int x, int y, int z) { return new Vector3(x * 0.01f, z * 0.01f, y * 0.01f); }   // CoordUtil.ServerToClient

        // ================================================================ propio ================================================================
        Zip cur; Vector3 zFrom, zTo, zDir; float zLen, zMove, zSpeed, zHeight, lastSend; Transform selfT, hand, toe; Animation selfAnim; CharacterController selfCc; float linkAt;

        /// <summary>CombatSystem: llamar con 'want' (ataque pulsado). true = la tirolina consumio el ataque (no atacar). Cerca de un trigger y sin objetivo, atacar = usar la tirolina.</summary>
        public static bool Intercept(bool want)
        {
            var z = Instance; if (z == null) return EpTriggers.Intercept(want);
            if (Riding) { z.prevWant = want; return true; }
            if (z.near == null || NpcUI.MobTarget != null) { z.prevWant = want; return EpTriggers.Intercept(want); }   // sin tirolina a mano: interruptores / lanzadores (EpTriggers)
            if (want && !z.prevWant) z.TryStart(z.near);
            z.prevWant = want; return true;
        }

        void TryStart(Zip z)
        {
            if (z == null || z.Go == null || ents == null || ents.Self == null || Riding) return;
            if (Time.unscaledTime < nextStart || BroomFlight.Active || VehicleRide.SelfRiding || ChatHud.Typing || NpcUI.Active || GameMenu.Blocking) return;
            var t = ents.Self.transform; var tr = z.Go.transform;
            zFrom = tr.TransformPoint(z.From); zTo = tr.TransformPoint(z.To); zDir = zTo - zFrom; zLen = zDir.magnitude;
            if (zLen < 0.01f) { Debug.LogWarning("[tirolina] linea de longitud 0 en " + z.Go.name); return; }
            cur = z; selfT = t; zSpeed = z.Velocity; zMove = 0f; zDir.Normalize();
            selfCc = t.GetComponent<CharacterController>(); if (selfCc != null) selfCc.enabled = false;
            selfAnim = t.GetComponentInChildren<Animation>(true); hand = FindDeep(t, "Bip01 L Hand"); toe = FindDeep(t, "Bip01 L Toe0");
            t.position = zFrom; Physics.SyncTransforms();
            // yaw como StartZipline: num = atan2(x, z) + pi normalizado a [0, 2pi]; fCurAngle = num en grados + 180
            float num = Mathf.Atan2(zDir.x, zDir.z) + Mathf.PI; while (num > Mathf.PI * 2f) num -= Mathf.PI * 2f; while (num < 0f) num += Mathf.PI * 2f;
            float fCurAngle = num * Mathf.Rad2Deg + 180f; t.rotation = Quaternion.Euler(0f, fCurAngle, 0f);
            Riding = true; BroomFlight.Active = true;
            PlayRope();
            Link(t, 3, false); Link(t, 5, true); linkAt = Time.unscaledTime + 0.5f;
            Send(BuildReq(t.position, 0f, 0, (int)(fCurAngle - 180f), (int)(zSpeed * 100f)));
            lastSend = Time.time;
            var hud = GetComponent<PlayerHud>(); if (hud != null && hud.Data.ActiveNanoSlot >= 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_ACTIVE, 2); p[4] = 0xFF; p[5] = 0xFF; Send(p); }
            Debug.Log("[tirolina] inicio en " + z.Go.name + ": longitud " + zLen.ToString("F1") + " m, velocidad " + zSpeed.ToString("F1") + " m/s");
        }

        void PlayRope()
        {
            if (selfAnim == null) return;
            var st = selfAnim["ropedown"]; if (st == null) return;
            if (!selfAnim.IsPlaying("ropedown")) { st.wrapMode = WrapMode.Loop; selfAnim.CrossFade("ropedown", 0.1f); }
        }

        byte[] BuildReq(Vector3 pos, float mov, int down, int angle, int speed)
        {   // sP_CL2FE_REQ_PC_ZIPLINE (Size 76): iCliTime@0 (el original no lo rellena = 0), iSt@8, fMov@20, fMax@24, fDummy@28, iPos@32, fV@44, bDown@56, iRollMax@60, iRoll@64 (1 B), iAngle@68, iSpeed@72
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_ZIPLINE, 76);   // indices del array = offset + 4 (tipo)
            PutI(p, 12, (int)(zFrom.x * 100f)); PutI(p, 16, (int)(zFrom.z * 100f)); PutI(p, 20, (int)(zFrom.y * 100f));
            PutF(p, 24, mov); PutF(p, 28, zLen);
            PutI(p, 36, (int)(pos.x * 100f)); PutI(p, 40, (int)(pos.z * 100f)); PutI(p, 44, (int)(pos.y * 100f));
            PutF(p, 48, zDir.x); PutF(p, 52, zDir.z); PutF(p, 56, zDir.y);   // ClientToServerF: y <-> z
            PutI(p, 60, down); PutI(p, 64, 30); p[68] = 0; PutI(p, 72, angle); PutI(p, 76, speed);
            return p;
        }
        static void Send(byte[] p) { var s = NpcUI.Sender; if (s != null) s(p); }

        /// <summary>Fin de la tirolina propia. natural = llego al final (ShowPistol + Jump(0)); down = salto manual (bDown = 1). send = enviar el ultimo paquete.</summary>
        void EndSelf(bool send, bool down)
        {
            if (!Riding) return;
            var t = selfT != null ? selfT : (ents != null && ents.Self != null ? ents.Self.transform : null);
            if (send && t != null) Send(BuildReq(t.position, down ? zMove : zLen, down ? 1 : 0, (int)t.rotation.y, (int)(zSpeed * 100f)));
            Riding = false; BroomFlight.Active = false; nextStart = Time.unscaledTime + 0.5f;
            if (t != null)
            {
                Link(t, 5, false); if (!VehicleRide.SelfRiding) Link(t, 3, true);   // ShowPistol (solo sin vehiculo) + LinkObjectActive(5, false)
                if (selfCc != null) selfCc.enabled = true;
            }
            if (gc == null) gc = FindObjectOfType<GameClient>();
            if (gc != null && send) { gc.ResetAnimClip(); gc.SkillJump(-gc.JumpVelocity); }   // Jump(0f): cae desde el punto final con velocidad vertical 0
            else if (gc != null) gc.ResetAnimClip();
            cur = null; selfT = null; hand = null; toe = null; selfAnim = null; selfCc = null;
            Debug.Log("[tirolina] fin (" + (down ? "salto" : (send ? "llegada" : "cancelada")) + ")");
        }

        void TickSelf()
        {
            if (ents == null || ents.Self == null || selfT == null || ents.Self.transform != selfT) { EndSelf(false, false); return; }
            var t = selfT; float dt = Time.deltaTime;
            if (Time.unscaledTime >= linkAt) { linkAt = Time.unscaledTime + 0.5f; Link(t, 5, true); Link(t, 3, false); }   // LinkObjectActive(5, true) cada frame en el original
            if (hand == null || toe == null) { hand = FindDeep(t, "Bip01 L Hand"); toe = FindDeep(t, "Bip01 L Toe0"); }
            zHeight = hand != null && toe != null ? (hand.position - toe.position).y : 1.8f;
            zMove += zSpeed * dt;
            var line = (zTo - zFrom) * zMove / zLen + zFrom;
            var pos = line; pos.y -= zHeight; t.position = pos;
            PlayRope();
            bool ended = false, jumpDown = false;
            if (zLen < zMove) { zMove = zLen; ended = true; }
            bool inputOk = !ChatHud.Typing && !GameMenu.Blocking && !NpcUI.Active;
            if (inputOk && (ControlMap.Down(ControlMap.K.Jump, false) || TouchControls.ConsumeJump())) jumpDown = true;
            if (ended || jumpDown)
            {
                if (jumpDown && !ended) { EndSelf(true, true); return; }
                EndSelf(true, false); return;
            }
            if (Time.time - lastSend > 0.3f) { lastSend = Time.time; Send(BuildReq(t.position, zMove, 0, (int)t.rotation.y, (int)(zSpeed * 100f))); }
            var mv = zDir * zSpeed; if (mv.magnitude > 0.1f) { mv.y = 0f; if (mv.sqrMagnitude > 1e-8f) t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(mv, Vector3.up), dt * 4f); }
        }

        // ================================================================ remotos ================================================================
        sealed class Remote
        {
            public Transform Tr; public RemoteMover Rm; public NpcAnimator Na; public Animation An; public Vector3 Target, Dir; public float Speed, LastPkt; public bool Init;
        }
        readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
        readonly List<int> tmpIds = new List<int>();

        void OnPacket(uint type, byte[] b)
        {
            if (type != PacketIds.P_FE2CL_PC_ZIPLINE) return;
            q.Enqueue(() => HandleRemote(b));
        }
        void HandleRemote(byte[] b)
        {   // sP_FE2CL_PC_ZIPLINE (Size 88): iSt@8, fMov@20, fMax@24, iPos@32, fV@44, bDown@56, iSpeed@72, iPC_ID@76, iSvrTime@80
            if (ents == null || b == null || b.Length < 80) return;
            int id = I(b, 76); if (id == ents.SelfId) return;
            var tr = ents.TransformOfPlayer(id); if (tr == null) return;
            var pos = SToC(I(b, 32), I(b, 36), I(b, 40)); var dir = new Vector3(F(b, 44), F(b, 52), F(b, 48));   // ServerToClientF: y <-> z
            float mov = F(b, 20), max = F(b, 24); int down = I(b, 56); float speed = I(b, 72) * 0.01f;
            Remote r; remotes.TryGetValue(id, out r);
            if (max == mov || down == 1) { if (r != null) EndRemote(id, r, pos); else { tr.position = pos; } return; }
            if (r == null || r.Tr != tr)
            {
                r = new Remote { Tr = tr, Rm = tr.GetComponent<RemoteMover>(), Na = tr.GetComponent<NpcAnimator>(), An = tr.GetComponentInChildren<Animation>(true) };
                remotes[id] = r;
                if (r.Rm != null) { r.Rm.Flying = true; r.Rm.enabled = false; }
                if (r.Na != null) r.Na.enabled = false;
            }
            tr.position = pos;   // GotoDir/ReceiveMoveZipline: transform.position = posicion del servidor en cada paquete
            r.Target = pos; r.Dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.zero; r.Speed = speed; r.LastPkt = Time.time; r.Init = true;
            Link(tr, 3, false); Link(tr, 5, true);
            PlayRope(r);
        }
        static void PlayRope(Remote r)
        {
            if (r.An == null) return; var st = r.An["ropedown"]; if (st == null) return;
            if (!r.An.IsPlaying("ropedown")) { st.wrapMode = WrapMode.Loop; r.An.CrossFade("ropedown", 0.1f); }
        }
        void EndRemote(int id, Remote r, Vector3 pos)
        {
            remotes.Remove(id);
            if (r.Tr == null) return;
            r.Tr.position = pos;
            Link(r.Tr, 5, false); if (!VehicleRide.IsRiding(id)) Link(r.Tr, 3, true);   // ShowPistol (solo sin vehiculo)
            if (r.Rm != null) { r.Rm.enabled = true; r.Rm.Flying = false; r.Rm.OnStop(pos); }
            if (r.Na != null)
            {
                r.Na.enabled = true;
                bool inv = r.Na.Inven; r.Na.SetInven(!inv); r.Na.SetInven(inv);   // fuerza que el animador vuelva a elegir clip (cur = null)
            }
            else if (r.An != null && r.An["stand1"] != null) r.An.CrossFade("stand1", 0.15f);
        }
        void TickRemotes()
        {
            if (remotes.Count == 0) return;
            float dt = Time.deltaTime; tmpIds.Clear(); foreach (var kv in remotes) tmpIds.Add(kv.Key);
            foreach (var id in tmpIds)
            {
                Remote r; if (!remotes.TryGetValue(id, out r)) continue;
                if (r.Tr == null) { remotes.Remove(id); continue; }
                if (Time.time - r.LastPkt > 3f) { EndRemote(id, r, r.Tr.position); continue; }   // paquete final perdido / jugador fuera de rango
                r.Target += r.Dir * r.Speed * dt;
                r.Tr.position = Vector3.Lerp(r.Tr.position, r.Target, Mathf.Clamp01(12f * dt));
                var mv = r.Dir * r.Speed; mv.y = 0f;
                if (mv.magnitude > 0.1f) r.Tr.rotation = Quaternion.Slerp(r.Tr.rotation, Quaternion.LookRotation(mv, Vector3.up), dt * 4f);
                PlayRope(r);
            }
        }

        // ================================================================ bucle ================================================================
        void Update()
        {
            while (q.TryDequeue(out var a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[tirolina] " + ex.Message); } }
            if (Riding) { try { TickSelf(); } catch (Exception ex) { Debug.LogWarning("[tirolina] " + ex.Message); EndSelf(false, false); } }
            TickRemotes();
            // trigger a mano: se busca 5 veces por segundo (colisionador trigger del original: OnTriggerEnter/Exit con la etiqueta Player)
            if (Time.unscaledTime >= nearScan)
            {
                nearScan = Time.unscaledTime + 0.2f; near = null;
                if (!Riding && zips.Count > 0 && ents != null && ents.Self != null)
                {
                    var c = ents.Self.transform.position + Vector3.up * 0.8f; Zip best = null; float bd = float.MaxValue;
                    for (int i = zips.Count - 1; i >= 0; i--)
                    {
                        var z = zips[i]; if (z.Go == null) { zips.RemoveAt(i); continue; }
                        if (!z.Go.activeInHierarchy) continue;
                        if (!Inside(z, c, 0.3f)) continue;
                        float d = (z.Go.transform.TransformPoint(z.From) - c).sqrMagnitude; if (d < bd) { bd = d; best = z; }
                    }
                    near = best;   // triggers[0]
                }
            }
        }

        Texture2D hint;
        void OnGUI()
        {
            if (near == null || Riding || NpcUI.MobTarget != null || ChatHud.Typing || GameMenu.Blocking || NpcUI.Active || VehicleRide.SelfRiding) return;
            if (WelcomePopup.Active || Event.current.type != EventType.Repaint) return;
            if (hint == null) hint = UiKit.Tex("target_icon3"); if (hint == null) return;
            float s = GameOptions.UiScale; float a = 0.5f + (Mathf.Sin(Time.time * 5f) + 1f) * 0.25f;   // RenderIconColor = blanco x (0,5 + (sen(5t) + 1) x 0,25)
            var oc = GUI.color; int od = GUI.depth; GUI.color = new Color(a, a, a, a); GUI.depth = 900;
            float w = hint.width * s, h = hint.height * s; GUI.DrawTexture(new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - h * 0.5f, w, h), hint, ScaleMode.StretchToFill, true);
            GUI.color = oc; GUI.depth = od;
        }
    }
}
