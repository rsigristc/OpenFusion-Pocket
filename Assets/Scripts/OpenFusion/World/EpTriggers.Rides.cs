// EpTriggers.Rides: lanzador (cnLauncher + CnGuiLauncher + cnAvatarThirdPersonMove.StartLauncher / rama EpLauncher), tobogan (EpSlope) y cuerda (EpRope) del jugador propio.
// SIN COMPILAR NI PROBAR EN UNITY. El estado propio activa BroomFlight.Active (GameClient no mueve ni envia PC_MOVE mientras dura) y mueve el CharacterController aqui.
//
// Original replicado:
//  * Lanzador: Use (tecla de ataque cerca) -> ReceiveLuncherInit: sonido Launcher_ClickOn; la camara pasa a la posicion del trigger + fHeight (2 m) mirando segun CurRotate; el trigger y el avatar se ocultan
//    (renderers). FixedUpdate (50 Hz): CurRotate.x -= eje avance * 0,5 (limite -maxRotate.x .. 0), CurRotate.y += eje lateral * 0,5 (limite +-maxRotate.y). Salto (mantener) = cargar potencia: sube/baja entre
//    minPower y maxPower a (max - min) por segundo con Launcher_PowerPulse en cada extremo; al soltar: Launcher_StopPower, dir = Euler(CurRotate.x, StartRotate.y + CurRotate.y + 180, 0) * forward,
//    pos = camara + dir * 10, StartLauncher(pos, dir * potencia, giro + 180): REQ_PC_LAUNCHER (Size 40), clip "luncher" (sube) o "luncherfly", arma oculta, Launcher_Firing. Esc cancela. Muerte: sale sin disparar.
//  * Vuelo: v horizontal constante = |dir.xz|, vy -= 10 * dt (minimo -10); CharacterController.Move; paquete cada 0,3 s (iV = vector horizontal, iSpeed = vy * 100, iAngle = giro); al tocar suelo o paredes:
//    paquete final con v = 0, iSpeed = min(vy, 0) * 100, arma visible, Jump(0).
//  * Tobogan (EpSlopeTrigger): al tocar su colisionador (punto de contacto bajo la cintura) StartSlope: waypoint mas cercano (o el siguiente si el avatar ya lo ha pasado). Cada frame: deriva
//    slopeMoveVector (rozamiento 1 - 0,065 * dt * 30, empuje hacia el siguiente waypoint cuando el CC toca algo), velocidad = vector * (2 - dot(arriba, normal)) * speed * 0,135 * (2 + eje avance) * speed * 0,3,
//    giro hacia el tramo +- 35 grados segun el eje lateral, empuje lateral right * eje * 3, gravedad 10. Paquete REQ_PC_SLOPE (Size 48) cada 0,3 s si se movio mas de 1 m. Clip "slide" en suelo.
//    Termina al chocar con algo que no es el tobogan (ResetCollisionVariables) o con salto (Jump normal).
//  * Cuerda (EpRopeTrigger): StartRope coloca al avatar en el punto mas cercano de la cuerda (tipo 2 = vertical: delante del avatar); cada frame mueve a lo largo de la cuerda (speed * direccion * dt), tipo 0 / 1
//    horizontal (izquierda / derecha) con el eje lateral, tipo 2 con el eje de avance y giro con el eje lateral (90 grados / s); clips ropeup / ropedrop / ropesturn / ropestand2 / ropeleft / roperight /
//    ropestand1; REQ_PC_ROPE (Size 48) cada 0,3 s o al cambiar de direccion / giro; salto (con avance atras = soltarse sin impulso) termina.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;

namespace OpenFusion.World
{
    public sealed partial class EpTriggers
    {
        enum Ride { None, LaunchAim, LaunchFly, Slope, Rope }
        Ride ride; bool endRequested;
        Transform rSelf; CharacterController rcc; Animation ranim; GameClient rgc; EpNode rNode; Vector3[] rPts; int rIdx; float rLastSend, rMove, rMax, nextRideAllowed; bool slideOn;
        Vector3 slopeVec, rLastPos; float ropeAng, ropeLastDir, ropeLastAng;
        // lanzador
        Camera aimCam; bool aimFollowPrev, aimFollowSet, aimCharging, aimUp, aimByTap, aimCancel; Vector3 aimRot, aimStart, aimMax, aimCamPos; float aimPower, aimMinP, aimMaxP;
        readonly List<Renderer> hidden = new List<Renderer>(); float flyV, flyH, flyAngle; Vector3 flyDirH; string flyClip;
        Texture2D crossTex; bool crossTried; GUIStyle lblStyle;
        static FieldInfo camYawField;

        bool Dead() { var h = GetComponent<PlayerHud>(); return h != null && h.Data != null && h.Data.Hp <= 0; }

        bool CanStart(bool ui)
        {
            if (ride != Ride.None || selfT == null || Time.unscaledTime < nextRideAllowed) return false;
            if (BroomFlight.Active || VehicleRide.SelfRiding || ZiplineRide.Riding) return false;
            if (ui && (ChatHud.Typing || GameMenu.Blocking || NpcUI.Active)) return false;
            return !Dead();
        }

        void BeginRide(Transform self, Ride r)
        {
            ride = r; endRequested = false; rSelf = self; rcc = self.GetComponent<CharacterController>(); ranim = self.GetComponentInChildren<Animation>(true); rgc = GC();
            if (rcc != null) { ccH = rcc.height; ccR = rcc.radius; }
            BroomFlight.Active = true; slideOn = false; rLastSend = 0f; slopeVec = Vector3.zero;
            env.platform = null; env.belt = null; env.currentSurface = null;
        }

        /// <summary>how: 0 = cancelado (sin salto), 1 = fin natural (aterriza; si esta en el aire cae), 2 = salto normal, 3 = soltarse / caer con velocidad vertical 0 (Jump(0)).</summary>
        void FinishRide(int how)
        {
            var old = ride; if (old == Ride.None) return;
            ride = Ride.None; endRequested = false; BroomFlight.Active = false; nextRideAllowed = Time.unscaledTime + 0.3f;
            RestoreAim();
            var t = rSelf; var g = rgc;
            if (t != null)
            {
                if (old == Ride.Rope || old == Ride.LaunchFly || old == Ride.LaunchAim) { if (!VehicleRide.SelfRiding) Link(t, 3, true); }   // ShowPistol
                if (old == Ride.Slope && how == 1 && rcc != null && rcc.collisionFlags != CollisionFlags.None && (ents == null || ents.Self == null || t == ents.Self.transform))
                {   // el tobogan acaba en suelo: se avisa a los demas con PC_STOP (posicion absoluta)
                    var p = ClientPackets.Stop(NowMs(), Cm(t.position.x), Cm(t.position.z), Cm(t.position.y)); Send(p);
                }
            }
            if (g != null)
            {
                g.ResetAnimClip();
                if (how == 2) g.SkillJump(0f);
                else if (how == 3 || (how == 1 && old == Ride.Slope && rcc != null && rcc.collisionFlags == CollisionFlags.None)) g.SkillJump(-g.JumpVelocity);
            }
            env = new EnvCol(); pendingSlope = null; pendingRope = null; slopeVec = Vector3.zero;
            Debug.Log("[ep] fin de " + old + " (" + how + ")");
            rNode = null; rPts = null; rSelf = null; rcc = null; ranim = null; rgc = null;
        }

        /// <summary>Cancela cualquier lanzador / tobogan / cuerda en curso (warp, cambio de avatar, destruccion).</summary>
        void AbortRide(bool send) { FinishRide(0); }

        void EnsureClip(string name, bool loop, float fade)
        {
            if (ranim == null || string.IsNullOrEmpty(name)) return;
            var st = ranim[name]; if (st == null) return;
            if (!ranim.IsPlaying(name)) { st.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever; ranim.CrossFade(name, fade); }
        }

        static void SetCamYaw(GameClient g, float yaw)
        {   // cam.RotationY = giro + 180 del original; GameClient solo expone CamYaw de lectura: se escribe el campo privado (ver informe)
            if (g == null) return;
            try
            {
                if (camYawField == null) camYawField = typeof(GameClient).GetField("camYaw", BindingFlags.Instance | BindingFlags.NonPublic);
                if (camYawField != null) camYawField.SetValue(g, yaw);
            }
            catch (Exception) { }
        }

        // ================================================================ bucle ================================================================
        void StartPending(Transform self)
        {   // OnControllerColliderHit -> StartEP(currentSlope / currentRope)
            var s = pendingSlope; var r = pendingRope; pendingSlope = null; pendingRope = null;
            if (s == null && r == null) return;
            if (ride == Ride.Slope || ride == Ride.Rope)
            {
                if (ride == Ride.Slope && s != null && s != rNode && s.Go != null) { StartSlope(self, s); return; }
                if (ride == Ride.Slope && r != null && r.Go != null) { StartRope(self, r); return; }
                return;
            }
            if (!CanStart(false))
            {
                if (s != null) { env.slope = null; env.currentSurface = null; }
                if (r != null) { env.rope = null; env.currentRopeSurface = null; }
                return;
            }
            if (s != null && s.Go != null) StartSlope(self, s); else if (r != null && r.Go != null) StartRope(self, r);
        }

        void TickRide(Transform self, float dt)
        {
            if (ride == Ride.None) return;
            if (rSelf != self) { FinishRide(0); return; }
            if (Dead()) { FinishRide(0); return; }
            try
            {
                switch (ride)
                {
                    case Ride.LaunchAim: TickAim(self, dt); break;
                    case Ride.LaunchFly: TickFly(self, dt); break;
                    case Ride.Slope: TickSlope(self, dt); break;
                    case Ride.Rope: TickRope(self, dt); break;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[ep] " + ride + ": " + ex.Message); FinishRide(0); }
        }

        // ================================================================ tobogan ================================================================
        void StartSlope(Transform self, EpNode n)
        {
            var pts = n.WorldPoints(); if (pts.Length < 2) return;
            Vector3 pos = self.position; int val = 0; float bd = Vector3.Distance(pts[0], pos);
            for (int i = 1; i < pts.Length; i++) { float d = Vector3.Distance(pts[i], pos); if (d < bd) { bd = d; val = i; } }
            if (val + 1 < pts.Length && Vector3.Dot(pts[val] - pos, pts[val + 1] - pos) < 0f) val++;
            BeginRide(self, Ride.Slope); rNode = n; rPts = pts; rIdx = val; rLastPos = pos + Vector3.up * 5f;   // el primer paquete sale en cuanto hay movimiento
            Debug.Log("[tobogan] inicio en " + n.Go.name + " id " + n.I.ObjectId + " waypoint " + val + "/" + pts.Length + " velocidad " + n.I.Speed.ToString("F1"));
        }

        void TickSlope(Transform self, float dt)
        {
            var n = rNode; if (n == null || n.Go == null || rPts == null || rPts.Length < 2) { FinishRide(0); return; }
            if (endRequested) { FinishRide(1); return; }
            var ax = Axes(); float h = ax.x, f = ax.y; float speed = n.I.Speed; Vector3 pos = self.position;
            slopeVec.y = 0f;
            Vector3 prev = rIdx > 0 ? rPts[rIdx - 1] : pos, cur = rPts[rIdx];
            if ((cur - pos).magnitude < 0.3f * speed)
            {
                if (rIdx + 1 < rPts.Length) rIdx++;
                prev = rIdx > 0 ? rPts[rIdx - 1] : pos; cur = rPts[rIdx];
            }
            float tilt = (2f - Vector3.Dot(Vector3.up, env.contactNormal)) * speed * 0.135f;
            float drift = 0.065f * dt * 30f;
            slopeVec *= 1f - drift;
            bool touching = rcc != null && rcc.collisionFlags != CollisionFlags.None;
            Vector3 seg = cur - prev;
            if (touching && seg.sqrMagnitude > 1e-8f) slopeVec += seg.normalized * drift;
            Vector3 v2 = slopeVec * tilt * (2f + f) * speed * 0.3f;
            float yaw0 = self.eulerAngles.y; float lookY = yaw0;
            if (seg.sqrMagnitude > 1e-8f) lookY = Quaternion.LookRotation(seg, Vector3.up).eulerAngles.y;
            self.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(yaw0, lookY + h * 35f, Mathf.Clamp01(1.8f * dt)), 0f);
            Vector3 v3 = self.right * h * 0.3f * 10f;
            if (Time.time - rLastSend > 0.3f && (rLastPos - self.position).magnitude > 1f)
            {
                rLastSend = Time.time; rLastPos = self.position;
                var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_SLOPE, 48);   // sP_CL2FE_REQ_PC_SLOPE (Size 48): iCliTime@0 (0), iX@8, iAngle@20, iSpeed@24, cKeyValue@28, fV@32, iSlopeID@44
                PutI(p, 12, Cm(self.position.x)); PutI(p, 16, Cm(self.position.z)); PutI(p, 20, Cm(self.position.y));
                PutI(p, 24, (int)(lookY - 180f)); PutI(p, 28, (int)(tilt * 100f)); p[32] = DirNum(h, f);
                PutF(p, 36, slopeVec.x); PutF(p, 40, slopeVec.z); PutF(p, 44, slopeVec.y); PutI(p, 48, n.I.ObjectId);
                Send(p);
            }
            Vector3 mv = v2 + v3; mv.y -= rgc != null ? rgc.Gravity : 10f;
            if (rcc != null && rcc.enabled) rcc.Move(mv * dt);
            bool grounded = rcc == null || rcc.collisionFlags != CollisionFlags.None;
            if (grounded) { slideOn = true; EnsureClip("slide", true, 0.35f); }
            else { if (slideOn) { slideOn = false; if (ranim != null) { ranim.Stop("slide"); } } EnsureClip("jumpstart", false, 0.15f); }   // AvatarSlope(0) + Jump(0)
            if (JumpPressed()) FinishRide(2);   // ResetCollisionVariables + Jump(altura de salto)
        }

        // ================================================================ cuerda ================================================================
        static float RopeTotal(Vector3[] w)
        {
            float s = 0f; for (int i = 0; i + 1 < w.Length; i++) s += (w[i] - w[i + 1]).magnitude; return s;
        }
        /// <summary>EpRopeTrigger.GetLengthPosition: punto a la distancia cur del inicio y direccion del tramo.</summary>
        static bool RopePos(Vector3[] w, float cur, out Vector3 ret, out Vector3 ang)
        {
            ret = Vector3.zero; ang = Vector3.zero;
            for (int i = 0; i < w.Length; i++)
            {
                ret = w[i];
                if (i + 1 < w.Length)
                {
                    float len = (w[i] - w[i + 1]).magnitude;
                    if (cur - len <= 0f) { ang = (w[i + 1] - w[i]).normalized; ret = ang * cur + w[i]; return true; }
                    cur -= len;
                }
            }
            return false;
        }
        /// <summary>EpRopeTrigger.GetNearPosition.</summary>
        static bool RopeNear(Vector3[] w, Vector3 vPos, out Vector3 vDirect, out Vector3 vOut, out float fLength)
        {
            vDirect = Vector3.zero; vOut = Vector3.zero; fLength = 0f;
            float best = float.MaxValue; Vector3 a = Vector3.zero, b = Vector3.zero; bool found = false;
            for (int i = 0; i < w.Length; i++)
            {
                float mag = (w[i] - vPos).magnitude;
                if (best > mag && i + 1 < w.Length)
                {
                    Vector3 p0 = w[i], p1 = w[i + 1]; Vector3 lhs = vPos - p0; Vector3 sg = p1 - p0;
                    float d = Vector3.Dot(lhs, sg), dd = Vector3.Dot(sg, sg);
                    if (d >= 0f && d <= dd) { best = mag; a = p0; b = p1; found = true; break; }
                    fLength += sg.magnitude;
                }
            }
            if (!found) return false;
            vDirect = (b - a).normalized; Vector3 l2 = vPos - a; Vector3 s2 = b - a; float d2 = Vector3.Dot(l2, s2), dd2 = Vector3.Dot(s2, s2);
            if (d2 <= 0f)
            {
                if ((vPos - a).magnitude < 1f) { vOut = a; return true; }
            }
            else
            {
                if (d2 < dd2) { vOut = Mathf.Abs(Vector3.Dot(l2, vDirect)) * vDirect + a; fLength += (vOut - a).magnitude; return true; }
                if ((vPos - b).magnitude < 1f) { vOut = b; fLength += (vOut - a).magnitude; return true; }
            }
            return false;
        }

        void StartRope(Transform self, EpNode n)
        {
            var pts = n.WorldPoints(); if (pts.Length < 2) return;
            var cc = self.GetComponent<CharacterController>(); if (cc == null) return;
            float hgt = cc.height, rad = cc.radius; int mt = n.I.RopeMoveType;
            BeginRide(self, Ride.Rope);
            Vector3 dirv, outPos; float len; Vector3 probe = mt == 2 ? self.position + self.forward * rad * 1.2f + Vector3.up * hgt : self.position + Vector3.up * hgt * 1.1f;
            if (RopeNear(pts, probe, out dirv, out outPos, out len))
            {
                self.position = mt == 2 ? outPos - self.forward * rad * 1.2f - Vector3.up * hgt : outPos - Vector3.up * hgt * 1.1f; Physics.SyncTransforms();
            }
            else len = 0f;
            rNode = n; rPts = pts; rMax = RopeTotal(pts); rMove = len; ropeAng = self.eulerAngles.y; ropeLastAng = ropeAng; ropeLastDir = 0f; rLastSend = 0f;
            Link(self, 3, false);
            Debug.Log("[cuerda] inicio en " + n.Go.name + " id " + n.I.ObjectId + " tipo " + mt + " longitud " + rMax.ToString("F1") + " desde " + len.ToString("F1"));
        }

        void TickRope(Transform self, float dt)
        {
            var n = rNode; if (n == null || n.Go == null || rPts == null || rcc == null) { FinishRide(0); return; }
            var ax = Axes(); float h = ax.x, f = ax.y; int mt = n.I.RopeMoveType; float dirv = 0f; int anim = 0;
            if (mt == 2)
            {
                if (f < 0f) { dirv = -1f; anim = 2; } else if (f > 0f) { dirv = 1f; anim = 1; }
            }
            else if (mt == 0)
            {
                if (h > 0f) { dirv = -1f; anim = 1; } else if (h < 0f) { dirv = 1f; anim = 2; }
            }
            else
            {
                if (h < 0f) { dirv = -1f; anim = 1; } else if (h > 0f) { dirv = 1f; anim = 2; }
            }
            rMove += n.I.Speed * dirv * dt; if (rMove < 0f) rMove = 0f; if (rMove > rMax) rMove = rMax;
            Vector3 target, ang; Vector3 move = Vector3.zero; bool found = RopePos(rPts, rMove, out target, out ang);
            if (found)
            {
                move = mt != 2 ? target - self.position - Vector3.up * rcc.height * 1.1f : target - self.position - self.forward * rcc.radius * 1.2f - Vector3.up * rcc.height;
            }
            bool turning = false;
            if (mt != 2)
            {
                Vector3 v7 = Vector3.Cross(ang, Vector3.up);
                if (found && v7.sqrMagnitude > 1e-8f) self.rotation = Quaternion.LookRotation(mt == 0 ? -v7 : v7, Vector3.up);
                ropeAng = self.eulerAngles.y;
            }
            else
            {
                ropeAng -= h * dt * 90f; self.rotation = Quaternion.Euler(0f, ropeAng, 0f);
                if (h != 0f) turning = true;
            }
            int a2 = turning ? 3 : anim; string clip;
            if (mt == 2) clip = a2 == 1 ? "ropeup" : (a2 == 2 ? "ropedrop" : (a2 == 3 ? "ropesturn" : "ropestand2"));
            else clip = a2 == 1 ? "ropeleft" : (a2 == 2 ? "roperight" : "ropestand1");
            EnsureClip(clip, true, 0.1f);
            if (rcc.enabled) rcc.Move(move - Vector3.one * 1E-06f);
            if (Time.time - rLastSend > 0.3f || ((ropeLastDir != dirv || Mathf.Abs(ropeAng - ropeLastAng) >= 0.2f) && Time.time - rLastSend >= 0.1f))
            {
                rLastSend = Time.time; ropeLastDir = dirv; ropeLastAng = ropeAng;
                var v = move.normalized;
                var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_ROPE, 48);   // sP_CL2FE_REQ_PC_ROPE (Size 48): iCliTime@0, iX@8, fV@20, iRopeID@32, iAngle@36, cKeyValue@40, iSpeed@44
                BitConverter.GetBytes(NowMs()).CopyTo(p, 4);
                PutI(p, 12, Cm(self.position.x)); PutI(p, 16, Cm(self.position.z)); PutI(p, 20, Cm(self.position.y));
                PutF(p, 24, v.x); PutF(p, 28, v.z); PutF(p, 32, v.y);   // ClientToServerF: y <-> z
                PutI(p, 36, n.I.ObjectId); PutI(p, 40, (int)self.rotation.y); p[44] = (byte)anim; PutI(p, 48, (int)(n.I.Speed * Mathf.Abs(dirv) * 100f));
                Send(p);
            }
            if (JumpPressed()) FinishRide(f < 0f ? 3 : 2);
        }

        // ================================================================ lanzador ================================================================
        Vector3 AimDir() { return Quaternion.Euler(aimRot.x, aimRot.y + aimStart.y + 180f, 0f) * Vector3.forward; }

        void StartAim(EpNode n)
        {
            if (!CanStart(true) || n == null || n.Go == null) return;
            var self = selfT; var cam = Camera.main; if (cam == null || self == null) return;
            BeginRide(self, Ride.LaunchAim); rNode = n; aimCam = cam;
            Sfx.Ui("Launcher_ClickOn");
            aimRot = Vector3.zero; aimMinP = n.I.MinPower; aimMaxP = n.I.MaxPower; aimPower = aimMinP; aimCharging = false; aimUp = true; aimByTap = false; aimCancel = false;
            aimStart = n.T.rotation.eulerAngles + n.I.InitRotate;
            while (aimStart.y > 360f) aimStart.y -= 360f;
            while (aimStart.y < 0f) aimStart.y += 360f;
            aimMax = n.I.MaxRotate; aimCamPos = n.T.position + Vector3.up * 2f;   // cnPlayerCamera.fHeight = 2
            if (rgc != null) { aimFollowPrev = rgc.FollowCamera; aimFollowSet = true; rgc.FollowCamera = false; }
            hidden.Clear();
            foreach (var r in n.Go.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            foreach (var r in self.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; hidden.Add(r); }
            Debug.Log("[lanzador] apuntando en " + n.Go.name + " potencia " + aimMinP.ToString("F1") + " - " + aimMaxP.ToString("F1") + " giro max " + aimMax);
        }

        /// <summary>Vuelve a mostrar el trigger y el avatar y devuelve la camara a GameClient (idempotente).</summary>
        void RestoreAim()
        {
            for (int i = 0; i < hidden.Count; i++) if (hidden[i] != null) hidden[i].enabled = true;
            hidden.Clear();
            if (aimFollowSet) { if (rgc != null) rgc.FollowCamera = aimFollowPrev; aimFollowSet = false; }
            aimCam = null; aimCancel = false;
        }

        void TickAim(Transform self, float dt)
        {
            var n = rNode; if (n == null || n.Go == null || aimCam == null) { FinishRide(0); return; }
            var ax = Axes(); float k = dt * 50f;   // FixedUpdate a 50 Hz, 0,5 por paso
            aimRot.x -= ax.y * 0.5f * k; aimRot.x = Mathf.Clamp(aimRot.x, -Mathf.Abs(aimMax.x), 0f);
            aimRot.y += ax.x * 0.5f * k;
            float my = Mathf.Abs(aimMax.y); if (Mathf.Abs(aimRot.y) > my) aimRot.y = aimRot.y < 0f ? -my : my;
            bool blocked = ChatHud.Typing || GameMenu.Blocking;
            bool down = !blocked && ControlMap.Down(ControlMap.K.Jump, false), up = ControlMap.Up(ControlMap.K.Jump, false), tap = TouchControls.Active && TouchControls.ConsumeJump();
            bool fire = false;
            if (!aimCharging)
            {
                if (down || tap) { Sfx.Ui("Launcher_StartPower"); Sfx.Ui("Launcher_PowerPulse"); aimCharging = true; aimByTap = tap && !down; }
            }
            else if (aimByTap ? tap : up) fire = true;
            if (aimCharging)
            {
                float rng = aimMaxP - aimMinP;
                if (aimUp)
                {
                    aimPower += rng * dt;
                    if (aimPower > aimMaxP) { aimPower = aimMaxP; aimUp = false; Sfx.Ui("Launcher_PowerPulse"); }
                }
                else
                {
                    aimPower -= rng * dt;
                    if (aimPower < aimMinP) { aimPower = aimMinP; aimUp = true; Sfx.Ui("Launcher_PowerPulse"); }
                }
            }
            aimCam.transform.position = aimCamPos;
            aimCam.transform.rotation = Quaternion.LookRotation(AimDir(), Vector3.up);   // CameraUpdate: LookAt(pos + Euler(CurRotate + StartRotate.y + 180) * forward)
            if (fire) { FireLauncher(self); return; }
            if (aimCancel || (!blocked && ControlMap.Down(ControlMap.K.Esc, false))) FinishRide(0);
        }

        void FireLauncher(Transform self)
        {
            Vector3 dir = AimDir(); Vector3 pos = FlightSafety.LaunchPosition(aimCamPos, dir, 10f, self, rNode?.T, rcc); Vector3 v = dir * aimPower; float rotate = aimStart.y + aimRot.y + 180f;
            Sfx.Ui("Launcher_StopPower");
            var g = rgc; RestoreAim(); SetCamYaw(g, rotate);
            FlightSafety.Relocate(rcc, self, pos); self.rotation = Quaternion.Euler(0f, rotate, 0f);
            flyV = v.y; Vector3 hv = v; hv.y = 0f; flyDirH = hv; flyH = hv.magnitude; flyAngle = rotate;
            ride = Ride.LaunchFly; rLastSend = Time.time;
            Send(BuildLauncher(pos, v, (int)rotate - 180, (int)(v.y * 100f)));
            flyClip = flyV > 0f ? "luncher" : "luncherfly"; EnsureClip(flyClip, false, 0.1f);
            Link(self, 3, false);
            Sfx.Ui("Launcher_Firing");
            Debug.Log("[lanzador] disparo con potencia " + aimPower.ToString("F1") + " dir " + dir);
        }

        /// <summary>sP_CL2FE_REQ_PC_LAUNCHER (Size 40): iCliTime@0 (0), iX@8, iY@12, iZ@16, iVX@20, iVY@24, iVZ@28 (cm/s, servidor y<->z), iAngle@32, iSpeed@36 (vy * 100).</summary>
        static byte[] BuildLauncher(Vector3 pos, Vector3 vel, int angle, int speed)
        {
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_LAUNCHER, 40);
            PutI(p, 12, Cm(pos.x)); PutI(p, 16, Cm(pos.z)); PutI(p, 20, Cm(pos.y));
            PutI(p, 24, Cm(vel.x)); PutI(p, 28, Cm(vel.z)); PutI(p, 32, Cm(vel.y));
            PutI(p, 36, angle); PutI(p, 40, speed);
            return p;
        }

        void TickFly(Transform self, float dt)
        {
            if (rcc == null) { FinishRide(0); return; }
            dt = Mathf.Clamp(dt, 0f, .1f);
            float g = rgc != null ? rgc.Gravity : 10f;
            Vector3 fwd = self.forward; fwd.y = 0f; if (fwd.sqrMagnitude > 1e-8f) fwd.Normalize();
            flyV -= g * dt; if (flyV < -g) flyV = -g;
            Vector3 mv = fwd * flyH; mv.y = flyV;
            var fl = rcc.enabled ? rcc.Move(mv * dt - Vector3.one * 1E-06f) : CollisionFlags.None;
            if (FlightSafety.CorrectTerrain(rcc, self)) fl |= CollisionFlags.Below;
            EnsureClip(flyClip, false, 0.1f);
            if ((fl & CollisionFlags.Below) != 0 || (fl & CollisionFlags.Sides) != 0)
            {
                float vz = flyV > 0f ? 0f : flyV;
                Send(BuildLauncher(self.position, Vector3.zero, (int)flyAngle, (int)(vz * 100f)));
                FinishRide(3);   // ShowPistol + Jump(0)
            }
            else if (Time.time - rLastSend > 0.3f)
            {
                Send(BuildLauncher(self.position, flyDirH, (int)flyAngle, (int)(flyV * 100f)));
                Link(self, 3, false); rLastSend = Time.time;
            }
        }

        /// <summary>CnGuiLauncher.OnGUI: franjas oscuras alrededor de una cruz de 660 px, barra de potencia a la izquierda y el consejo de uso.</summary>
        void DrawLauncherGui()
        {
            var ev = Event.current; float s = Mathf.Max(0.5f, GameOptions.UiScale);
            int od = GUI.depth; GUI.depth = 9;
            float cs = Mathf.Min(660f * s, Mathf.Min(Screen.width, Screen.height) * 0.9f); float sg = cs / 660f;
            var cross = new Rect((Screen.width - cs) * 0.5f, (Screen.height - cs) * 0.5f, cs, cs);
            if (ev.type == EventType.Repaint)
            {
                var dark = new Color(0f, 0f, 0f, 0.55f);
                UiKit.Fill(new Rect(0f, 0f, Screen.width, cross.y), dark);
                UiKit.Fill(new Rect(0f, cross.y, cross.x, cs), dark);
                UiKit.Fill(new Rect(cross.xMax, cross.y, Screen.width - cross.xMax, cs), dark);
                UiKit.Fill(new Rect(0f, cross.yMax, Screen.width, Screen.height - cross.yMax), dark);
                if (!crossTried) { crossTried = true; crossTex = UiKit.Tex("launchcross"); }
                if (crossTex != null) UiKit.Img(cross, crossTex);
                else
                {
                    var lc = new Color(1f, 1f, 1f, 0.7f);
                    UiKit.Fill(new Rect(cross.center.x - 1f, cross.y, 2f, cs), lc); UiKit.Fill(new Rect(cross.x, cross.center.y - 1f, cs, 2f), lc);
                }
                float gw = 169f * sg, gh = 595f * sg; var gage = new Rect(cross.x - (gw + 10f * sg), cross.y + 30f * sg, gw, gh);
                UiKit.Fill(gage, new Color(0f, 0f, 0.1f, 0.75f)); UiKit.Outline(gage, new Color(1f, 1f, 1f, 0.5f), 1f);
                var area = new Rect(gage.x + 42f * sg, gage.y + 30f * sg, 87f * sg, 500f * sg); UiKit.Fill(area, new Color(0.15f, 0.15f, 0.2f, 0.9f));
                float pw = aimMaxP > aimMinP ? Mathf.Clamp01((aimPower - aimMinP) / (aimMaxP - aimMinP)) : 0f;
                float bh = 31f * sg; var bar = new Rect(area.x, area.y + (area.height - bh) * (1f - pw), area.width, bh); UiKit.Fill(bar, new Color(1f, 0.75f, 0.1f, 0.95f));
                if (lblStyle == null) { lblStyle = new GUIStyle(GUI.skin.label); lblStyle.alignment = TextAnchor.MiddleCenter; lblStyle.normal.textColor = Color.white; lblStyle.wordWrap = true; }
                lblStyle.fontSize = Mathf.Max(10, (int)(16f * s));
                GUI.Label(new Rect(gage.x, gage.y - 28f * sg, gw, 28f * sg), Loc.T("POWER"), lblStyle);
                lblStyle.fontSize = Mathf.Max(9, (int)(12f * s));
                GUI.Label(new Rect(gage.x + 10f * sg, gage.y + 560f * sg, 150f * sg, 34f * sg), Loc.T("HOLD SPACE BAR TO SET POWER AND RELEASE TO FIRE"), lblStyle);
            }
            float bw = 120f * s, bhh = 44f * s;
            if (GUI.Button(new Rect(Screen.width - bw - 20f * s, 20f * s, bw, bhh), "X")) aimCancel = true;
            GUI.depth = od;
        }
    }
}
