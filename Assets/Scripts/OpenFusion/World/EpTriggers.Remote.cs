// EpTriggers.Remote: paquetes FE2CL de otros jugadores sobre elementos EP (UserMoveController.ReceiveMoveSlope / ReceiveMoveRope / ReceiveMoveLauncher / ReceiveJumpPad / ReceiveMovePlatform / ReceiveMoveBelt)
// y GameFrame REP_IM_CHANGE_SWITCH_STATUS. SIN COMPILAR NI PROBAR EN UNITY.
//
// Estructuras (cuerpo sin los 4 bytes del tipo, offsets = los del struct, Pack 4):
//   PC_JUMPPAD (52):  iCliTime@0, iX@8, iY@12, iZ@16, iVX@20, iVY@24, iVZ@28, iAngle@32, cKeyValue@36, iPC_ID@40, iSvrTime@44
//   PC_LAUNCHER (52): iCliTime@0 (acaba en 1 mod 1000 = dash, no se trata aqui), iX@8, iY@12, iZ@16, iVX@20, iVY@24, iVZ@28, iAngle@32, iSpeed@36, iPC_ID@40, iSvrTime@44
//   PC_MOVEPLATFORM / PC_BELT (76): iLcX@8, iLcY@12, iLcZ@16, iX@20, iY@24, iZ@28, fVX@32, fVY@36, fVZ@40, bDown@44, iPlatformID / iBeltID@48, iAngle@52, cKeyValue@56, iSpeed@60, iPC_ID@64, iSvrTime@68
//   PC_SLOPE (60):    iX@8, iY@12, iZ@16, iAngle@20, iSpeed@24, cKeyValue@28, iPC_ID@32, iSvrTime@36, fVX@44, fVY@48, fVZ@52, iSlopeID@56
//   PC_ROPE (60):     iX@8, iY@12, iZ@16, fVX@20, fVY@24, fVZ@28, iRopeID@32, iAngle@36, cKeyValue@40, iSpeed@44, iPC_ID@48, iSvrTime@52
//   REP_IM_CHANGE_SWITCH_STATUS (16): iMapNum@0, iSwitchLID@4, iSwitchGID@8, iSwitchStatus@12
// Remotos: el original activa un MoveType (EpSlope / EpRope / EpLauncher) hasta el siguiente PC_MOVE / STOP / JUMP de ese jugador (GotoDir / Jump lo ponen a None); plataformas y cintas dejan al jugador
// "pegado" (currentPlatform / currentBelt) hasta un salto; aqui caduca ademas 1,2 s despues del ultimo paquete (el cliente propio de este puerto no envia MOVEPLATFORM salvo SendPlatformPackets).
using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;

namespace OpenFusion.World
{
    public sealed partial class EpTriggers
    {
        sealed class RSpec
        {
            public int Id, Kind, Key; public Transform Tr; public RemoteMover Rm; public NpcAnimator Na; public Animation An; public EpNode Node; public Vector3[] Pts;
            public Vector3 Vec, Hv; public float Speed, Vy, LastPkt, Mt;
        }
        sealed class RPlat
        {
            public int Id, Kind; public Transform Tr; public RemoteMover Rm; public EpNode Node; public float LastPkt, Spd, Ang; public Vector3 V; public bool Moving;
        }
        const int KSlope = 1, KRope = 2, KLauncher = 3;
        readonly Dictionary<int, RSpec> rsp = new Dictionary<int, RSpec>();
        readonly Dictionary<int, RPlat> rpl = new Dictionary<int, RPlat>();
        readonly List<int> tmpIds = new List<int>();
        static float ccH = 1.8f, ccR = 0.4f;

        void OnPacket(uint type, byte[] b)
        {   // hilo de red: solo filtra y encola
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_PC_JUMPPAD || type == PacketIds.P_FE2CL_PC_LAUNCHER || type == PacketIds.P_FE2CL_PC_MOVEPLATFORM || type == PacketIds.P_FE2CL_PC_BELT
                || type == PacketIds.P_FE2CL_PC_SLOPE || type == PacketIds.P_FE2CL_PC_ROPE || type == PacketIds.P_FE2CL_REP_IM_CHANGE_SWITCH_STATUS)
                q.Enqueue(() => HandleRemote(type, b));
        }

        static Vector2 KeyAxes(int k)
        {   // inversa de AvatarUtil.GetDirNum (KeyToDir): (lateral, avance)
            switch (k)
            {
                case 1: return new Vector2(0f, 1f);
                case 2: return new Vector2(1f, 1f);
                case 3: return new Vector2(1f, 0f);
                case 4: return new Vector2(1f, -1f);
                case 5: return new Vector2(0f, -1f);
                case 6: return new Vector2(-1f, -1f);
                case 7: return new Vector2(-1f, 0f);
                case 8: return new Vector2(-1f, 1f);
                default: return Vector2.zero;
            }
        }

        void HandleRemote(uint type, byte[] b)
        {
            if (ents == null) return;
            if (type == PacketIds.P_FE2CL_REP_IM_CHANGE_SWITCH_STATUS)
            {   // GameFrame: SearchEpElement(position, Switch, iSwitchGID).SwitchOn()
                if (b.Length < 16) return;
                Vector3 sp = ents.Self != null ? ents.Self.transform.position : Vector3.zero;
                var sw = Find(EpT.Switch, I(b, 8), sp); if (sw != null) sw.SwitchOn();
                return;
            }
            int id, need;
            if (type == PacketIds.P_FE2CL_PC_JUMPPAD || type == PacketIds.P_FE2CL_PC_LAUNCHER) { need = 52; id = I(b, 40); }
            else if (type == PacketIds.P_FE2CL_PC_MOVEPLATFORM || type == PacketIds.P_FE2CL_PC_BELT) { need = 76; id = I(b, 64); }
            else if (type == PacketIds.P_FE2CL_PC_SLOPE) { need = 60; id = I(b, 32); }
            else { need = 60; id = I(b, 48); }
            if (b.Length < need || id == ents.SelfId) return;
            var tr = ents.TransformOfPlayer(id); if (tr == null) return;
            if (type == PacketIds.P_FE2CL_PC_JUMPPAD) RemoteJumpPad(id, tr, b);
            else if (type == PacketIds.P_FE2CL_PC_LAUNCHER) RemoteLauncher(id, tr, b);
            else if (type == PacketIds.P_FE2CL_PC_MOVEPLATFORM) RemotePlatform(id, tr, b, 0);
            else if (type == PacketIds.P_FE2CL_PC_BELT) RemotePlatform(id, tr, b, 1);
            else if (type == PacketIds.P_FE2CL_PC_SLOPE) RemoteSlope(id, tr, b);
            else RemoteRope(id, tr, b);
        }

        // ---------------------------------------------------------------- jumppad
        void RemoteJumpPad(int id, Transform tr, byte[] b)
        {   // UserMoveController.JumpPad: position = servidor; GotoDir(v, |vh|); Jump(); AvatarJumpPad
            EndRemoteSpecial(id); EndRemotePlat(id);
            Vector3 pos = SToC(I(b, 8), I(b, 12), I(b, 16)); Vector3 vel = new Vector3(I(b, 20) * 0.01f, I(b, 28) * 0.01f, I(b, 24) * 0.01f);
            float hs = new Vector2(vel.x, vel.z).magnitude; var rm = tr.GetComponent<RemoteMover>();
            tr.position = pos;
            if (rm != null) rm.OnJump(pos, vel, hs, I(b, 32));
        }

        // ---------------------------------------------------------------- plataforma / cinta
        void RemotePlatform(int id, Transform tr, byte[] b, int kind)
        {   // MovePlatform / MoveBelt: SetPosition(servidor); si se halla el elemento, position = su posicion + (iLc); GotoDir(v, speed)
            EndRemoteSpecial(id);
            Vector3 pos = SToC(I(b, 20), I(b, 24), I(b, 28)); Vector3 lc = SToC(I(b, 8), I(b, 12), I(b, 16));
            var node = Find(kind == 0 ? EpT.Platform : EpT.Belt, I(b, 48), pos);
            if (node != null && kind == 0) pos = node.T.position + lc; else if (node != null) pos = node.T.position + lc;
            Vector3 vel = new Vector3(F(b, 32), F(b, 40), F(b, 36)); float spd = I(b, 60) * 0.01f; float ang = I(b, 52); int key = b[56];
            tr.position = pos;
            var na = tr.GetComponent<NpcAnimator>(); if (na != null) na.SetKey((byte)key);
            var rm = tr.GetComponent<RemoteMover>(); if (rm != null) rm.OnMove(pos, vel, spd, ang);
            if (node == null) { EndRemotePlat(id); return; }
            RPlat r; if (!rpl.TryGetValue(id, out r) || r.Tr != tr) { r = new RPlat { Id = id, Tr = tr }; rpl[id] = r; }
            r.Kind = kind; r.Rm = rm; r.Node = node; r.LastPkt = Time.time; r.V = vel; r.Spd = spd; r.Ang = ang; r.Moving = new Vector2(vel.x, vel.z).sqrMagnitude > 1e-6f;
        }

        // ---------------------------------------------------------------- tobogan
        void RemoteSlope(int id, Transform tr, byte[] b)
        {   // MoveSlope: SetPosition; fSlopSpeed = iSpeed; MoveDirNum = cKeyValue; slopeMoveVector = (fVX, fVZ, fVY); busca el tobogan por id; SetAngle; AvatarSlope(2)
            EndRemotePlat(id);
            var r = GetSpec(id, tr, KSlope);
            Vector3 pos = SToC(I(b, 8), I(b, 12), I(b, 16)); tr.position = pos;
            r.Speed = I(b, 24) * 0.01f; r.Key = b[28]; r.Vec = new Vector3(F(b, 44), F(b, 52), F(b, 48)); r.Vec.y = 0f; r.LastPkt = Time.time;
            r.Node = Find(EpT.Slope, I(b, 56), pos); r.Pts = r.Node != null ? r.Node.WorldPoints() : null;
            Link(tr, 3, false);
        }

        void TickSlopeRemote(RSpec r, float dt)
        {   // UserMoveController.EpUpdate (EpSlope)
            var tr = r.Tr; Vector3 pos = tr.position; Vector3 a = pos, b2 = pos + tr.rotation * Vector3.forward * 2f; float spd = 2f;
            if (r.Node != null && r.Node.Go != null && r.Pts != null)
            {
                spd = r.Node.I.Speed; float best = float.MaxValue;
                for (int i = 0; i < r.Pts.Length; i++)
                {
                    Vector3 lhs = r.Pts[i] - pos; float mag = lhs.magnitude;
                    if (best > mag && i + 1 < r.Pts.Length)
                    {
                        lhs.Normalize(); Vector3 rhs = (r.Pts[i + 1] - r.Pts[i]).normalized;
                        if (Vector3.Dot(lhs, rhs) < 0f) { best = mag; a = r.Pts[i]; b2 = r.Pts[i + 1]; }
                    }
                }
            }
            Vector2 kd = KeyAxes(r.Key); float drift = 0.065f * dt * 30f;
            r.Vec.y = 0f; r.Vec *= 1f - drift;
            Vector3 sd = b2 - a; if (sd.sqrMagnitude > 1e-8f) r.Vec += sd.normalized * drift;   // el CharacterController del original esta casi siempre en contacto con la rampa
            Vector3 v3 = r.Vec * r.Speed * (2f + kd.y) * spd * 0.3f;
            float y0 = tr.eulerAngles.y; float ty = sd.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(sd, Vector3.up).eulerAngles.y : y0;
            tr.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(y0, ty + kd.x * 35f, Mathf.Clamp01(1.8f * dt)), 0f);
            Vector3 mv = v3 + tr.right * kd.x * 0.3f * 10f;
            Vector3 np = pos + new Vector3(mv.x, 0f, mv.z) * dt; np.y = Ground.Y(new Vector3(np.x, pos.y, np.z), 3f); tr.position = np;
            RClip(r, "slide", true, 0.35f);
        }

        // ---------------------------------------------------------------- cuerda
        void RemoteRope(int id, Transform tr, byte[] b)
        {   // MoveRope: SetPosition; RopeVector = (fVX, fVZ, fVY); RopeSpeed = iSpeed; iRopeDir = cKeyValue; busca la cuerda por id; AvatarRope(moveType, dir)
            EndRemotePlat(id);
            var r = GetSpec(id, tr, KRope);
            Vector3 pos = SToC(I(b, 8), I(b, 12), I(b, 16)); tr.position = pos;
            r.Vec = new Vector3(F(b, 20), F(b, 28), F(b, 24)); r.Speed = I(b, 44) * 0.01f; r.Key = b[40]; r.LastPkt = Time.time;
            r.Node = Find(EpT.Rope, I(b, 32), pos); r.Pts = r.Node != null ? r.Node.WorldPoints() : null;
            Link(tr, 3, false);
        }

        void TickRopeRemote(RSpec r, float dt)
        {
            var n = r.Node; var tr = r.Tr; if (n == null || n.Go == null || r.Pts == null) return;
            int mt = n.I.RopeMoveType; Vector3 pos = tr.position + r.Vec * r.Speed * dt;
            Vector3 dirv, outp; float len; Vector3 probe = mt == 2 ? pos + tr.forward * ccR * 1.1f + Vector3.up * ccH : pos + Vector3.up * ccH * 1.1f;
            if (RopeNear(r.Pts, probe, out dirv, out outp, out len)) pos = mt == 2 ? outp - tr.forward * ccR * 1.1f - Vector3.up * ccH : outp - Vector3.up * ccH * 1.1f;
            else dirv = Vector3.zero;
            if (mt != 2)
            {
                Vector3 v10 = Vector3.Cross(dirv, Vector3.up);
                if (v10.sqrMagnitude > 1e-8f) tr.rotation = Quaternion.LookRotation(mt == 0 ? -v10 : v10, Vector3.up);
            }
            tr.position = pos;
            string clip;
            if (mt == 2) clip = r.Key == 1 ? "ropeup" : (r.Key == 2 ? "ropedrop" : (r.Key == 3 ? "ropesturn" : "ropestand2"));
            else clip = r.Key == 1 ? "ropeleft" : (r.Key == 2 ? "roperight" : "ropestand1");
            RClip(r, clip, true, 0.1f);
        }

        // ---------------------------------------------------------------- lanzador
        void RemoteLauncher(int id, Transform tr, byte[] b)
        {   // ReceiveMoveLauncher: SetPosition; si la velocidad es 0 termina (ShowPistol + salto); si no EpLauncher con fVelocityZ = iSpeed y HidePistol
            if ((U64(b, 0) % 1000UL) == 1UL) return;   // dash (otro sistema)
            EndRemotePlat(id);
            Vector3 pos = SToC(I(b, 8), I(b, 12), I(b, 16)); Vector3 zv = SToC(I(b, 20), I(b, 24), I(b, 28));
            RSpec r; rsp.TryGetValue(id, out r);
            if (zv.magnitude == 0f)
            {
                tr.position = pos;
                if (r != null && r.Kind == KLauncher) EndRemote(r, true, pos);
                return;
            }
            r = GetSpec(id, tr, KLauncher);
            tr.position = pos; r.Hv = new Vector3(zv.x, 0f, zv.z); r.Vy = I(b, 36) * 0.01f; r.LastPkt = Time.time;
            if (r.Hv.sqrMagnitude > 1e-8f) tr.rotation = Quaternion.LookRotation(r.Hv, Vector3.up);
            Link(tr, 3, false);
            if (r.An != null) { var st = r.An[r.Vy > 0f ? "luncher" : "luncherfly"]; if (st != null) { st.wrapMode = WrapMode.ClampForever; r.An.CrossFade(st.name, 0.1f); } }
        }

        void TickLauncherRemote(RSpec r, float dt)
        {
            var tr = r.Tr; Vector3 pos = tr.position;
            r.Vy -= 10f * dt; if (r.Vy < -10f) r.Vy = -10f;
            Vector3 mv = r.Hv; mv.y = r.Vy; Vector3 np = pos + mv * dt;
            if (r.Hv.sqrMagnitude > 0.01f) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(r.Hv, Vector3.up), Mathf.Clamp01(dt * 4f));
            if (r.Vy < 0f)
            {
                float gy = Ground.Y(np, 3f);
                if (np.y < gy) { np.y = gy; tr.position = np; EndRemote(r, false, np); return; }   // CollisionFlags.Below
            }
            tr.position = np;
            if (r.An != null) { var st = r.An[r.Vy > 0f ? "luncher" : "luncherfly"]; if (st != null && !r.An.IsPlaying(st.name)) { st.wrapMode = WrapMode.ClampForever; r.An.CrossFade(st.name, 0.1f); } }
        }

        // ---------------------------------------------------------------- comun
        RSpec GetSpec(int id, Transform tr, int kind)
        {
            RSpec r; rsp.TryGetValue(id, out r);
            if (r != null && r.Tr == tr) { r.Kind = kind; return r; }
            if (r != null) rsp.Remove(id);
            r = new RSpec { Id = id, Kind = kind, Tr = tr, Rm = tr.GetComponent<RemoteMover>(), Na = tr.GetComponent<NpcAnimator>(), An = tr.GetComponentInChildren<Animation>(true) };
            rsp[id] = r;
            if (r.Rm != null) { r.Rm.Flying = true; r.Rm.enabled = false; }
            if (r.Na != null) r.Na.enabled = false;
            return r;
        }

        static void RClip(RSpec r, string name, bool loop, float fade)
        {
            if (r.An == null) return; var st = r.An[name]; if (st == null) return;
            if (!r.An.IsPlaying(name)) { st.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever; r.An.CrossFade(name, fade); }
        }

        void EndRemote(RSpec r, bool setPos, Vector3 pos)
        {
            rsp.Remove(r.Id);
            if (r.Tr == null) return;
            if (setPos) r.Tr.position = pos;
            if (!VehicleRide.IsRiding(r.Id)) Link(r.Tr, 3, true);   // ShowPistol (solo sin vehiculo)
            Vector3 cur = r.Tr.position;
            if (r.Rm != null) { r.Rm.enabled = true; r.Rm.Flying = false; r.Rm.OnStop(cur); }
            if (r.Na != null)
            {
                r.Na.enabled = true;
                bool inv = r.Na.Inven; r.Na.SetInven(!inv); r.Na.SetInven(inv);   // fuerza que el animador vuelva a elegir clip
            }
            else if (r.An != null && r.An["stand1"] != null) r.An.CrossFade("stand1", 0.15f);
        }

        /// <summary>PC_MOVE / PC_STOP / PC_JUMP de ese jugador: GotoDir / Jump ponen MoveType a None.</summary>
        void EndRemoteSpecial(int id)
        {
            RSpec r; if (rsp.TryGetValue(id, out r)) EndRemote(r, false, Vector3.zero);
        }
        void EndRemotePlat(int id) { rpl.Remove(id); }

        void TickRemoteRiders(float dt)
        {
            if (rsp.Count > 0)
            {
                tmpIds.Clear(); foreach (var k in rsp.Keys) tmpIds.Add(k);
                for (int i = 0; i < tmpIds.Count; i++)
                {
                    RSpec r; if (!rsp.TryGetValue(tmpIds[i], out r)) continue;
                    if (r.Tr == null) { rsp.Remove(tmpIds[i]); continue; }
                    if (Time.time - r.LastPkt > 3f) { EndRemote(r, false, Vector3.zero); continue; }   // paquete final perdido / jugador fuera de rango
                    try
                    {
                        if (r.Kind == KSlope) TickSlopeRemote(r, dt);
                        else if (r.Kind == KRope) TickRopeRemote(r, dt);
                        else if (r.Kind == KLauncher) TickLauncherRemote(r, dt);
                    }
                    catch (Exception ex) { Debug.LogWarning("[ep] remoto " + r.Kind + ": " + ex.Message); EndRemote(r, false, Vector3.zero); }
                }
            }
            if (rpl.Count > 0)
            {
                tmpIds.Clear(); foreach (var k in rpl.Keys) tmpIds.Add(k);
                for (int i = 0; i < tmpIds.Count; i++)
                {
                    RPlat p; if (!rpl.TryGetValue(tmpIds[i], out p)) continue;
                    if (p.Tr == null || p.Node == null || p.Node.Go == null || Time.time - p.LastPkt > 1.2f) { rpl.Remove(tmpIds[i]); continue; }
                    Vector3 d = p.Kind == 0 ? p.Node.Vel * dt : BeltDelta(p.Node, p.Tr.position, dt);
                    if (d.sqrMagnitude <= 0f) continue;
                    Vector3 np = p.Tr.position + d; p.Tr.position = np;
                    if (p.Rm != null) { if (p.Moving) p.Rm.OnMove(np, p.V, p.Spd, p.Ang); else p.Rm.OnStop(np); }
                }
            }
        }
    }
}
