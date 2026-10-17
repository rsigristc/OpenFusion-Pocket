// Disparo "modo 5" (cohete / DH_ROCKET) tal como en el cliente 2.5: cnAvatarAttack.AttackTarget -> REQ_PC_ROCKET_STYLE_FIRE; el servidor responde con
// REP_PC_ROCKET_STYLE_FIRE_SUCC (propio) / PC_ROCKET_STYLE_FIRE (otros) y el cliente crea la ojiva (cnWarHeadContainer.AddWarHead + cnWarHead):
// vuela recta a velocidad = AtkRange*0.01 m/s durante DurationTime*0.1 s, se estrella al tocar un colisionador, un NPC (radio 0,2) o al acabar el tiempo,
// y la propia ojiva envía REQ_PC_ROCKET_STYLE_HIT con los NPC dentro de EffectArea*0.01 m. Hasta 3 ojivas a la vez (ranuras iBulletID 0..2).
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class RocketSystem : MonoBehaviour
    {
        sealed class Head
        {
            public GameObject Go; public int Slot; public bool Other, Crashed; public Vector3 Vel; public float Speed, T0, Dur, Area; public BulletFx.Row Row; public ItemElement Item; public Transform Owner; public bool Grenade; public float VelZ; public Vector3 Flat; public SkillElement Skill;
        }
        // Legacy has one cnWarHeadContainer per avatar, not one for the world.
        readonly List<Head> heads = new List<Head>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        readonly RaycastHit[] worldHits = new RaycastHit[32];
        struct PendingShot { public Vector3 Target; public float Time; }
        readonly Queue<PendingShot> pendingShots = new Queue<PendingShot>();
        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; PlayerHud hud; int logN;
        const int RayMask = ~((1 << 2) | (1 << 30));   // sin agua ni cámara del retrato (el original usa 268434687 = todo menos 2 capas)

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            ents = e; disp = d; tables = t; hud = h; if (disp != null) disp.Unhandled += OnPacket;
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static Vector3 ToClient(int x, int y, int z) { return new Vector3(x * 0.01f, z * 0.01f, y * 0.01f); }
        static int S(float v) { return Mathf.RoundToInt(v * 100f); }

        public static bool SweepBounds(Bounds bounds, Vector3 start, Vector3 step, float radius, out float distance)
        {
            bounds.Expand(radius * 2f); distance = 0;
            if (bounds.Contains(start)) return true;
            float length = step.magnitude;
            return length > .0001f && bounds.IntersectRay(new Ray(start, step / length), out distance) && distance <= length;
        }

        ItemElement ItemById(int id)
        {
            var wt = tables != null ? tables.m_pWeaponItemTable : null; if (wt == null || wt.m_pItemData == null) return null;
            foreach (var e in wt.m_pItemData) if (e != null && e.m_iItemNumber == id) return e; return null;
        }
        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null; if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }

        /// IsShootable(): hay alguna ranura libre.
        public bool CanShoot { get { for (int i = 0; i < 3; i++) if (!heads.Exists(h => !h.Other && h.Slot == i && h.Go != null)) return true; return false; } }

        void ReplaceHead(Head head)
        {
            for (int i = heads.Count - 1; i >= 0; i--)
                if (heads[i].Owner == head.Owner && heads[i].Slot == head.Slot)
                { if (heads[i].Go != null) Destroy(heads[i].Go); heads.RemoveAt(i); }
            heads.Add(head);
        }

        /// AttackTarget (modo 5): origen = posición del jugador + 0,8 m; modo cámara normal: dirección = hacia delante del avatar con y = 0,8 (normalizada) x alcance.
        public bool Fire(Vector3 forward, int range, Vector3? aim = null)
        {
            if (!CanShoot || NpcUI.Sender == null || ents == null || ents.Self == null) return false;
            float r = range * 0.01f; var start = ents.Self.transform.position; start.y += 0.8f;
            var v = aim.HasValue ? aim.Value.normalized : new Vector3(forward.x * r, 0.8f, forward.z * r).normalized; var to = start + v * r;   // modo apuntado: hacia el punto bajo la mira
            NpcUI.Sender(ClientPackets.RocketFire(0, S(start.x), S(start.z), S(start.y), S(to.x), S(to.z), S(to.y)));
            lastTo = to; lastToAt = Time.time;
            while (pendingShots.Count >= 8) pendingShots.Dequeue();
            pendingShots.Enqueue(new PendingShot { Target = to, Time = Time.time });
            watchUntil = Environment.TickCount + 2500;
            if (logN++ < 4) Debug.Log("[cohete] REQ_FIRE de " + start + " a " + to);
            return true;
        }

        /// cnAvatarAttack.FireGrenade: destino = posición del jugador + dirección x AtkRange; el servidor responde con GRENADE_STYLE_FIRE_SUCC.
        public bool FireGrenade(Vector3 dir, int range)
        {
            if (!CanShoot || NpcUI.Sender == null || ents == null || ents.Self == null) return false;
            var to = ents.Self.transform.position + dir.normalized * (range * 0.01f);
            NpcUI.Sender(ClientPackets.GrenadeFire(0, S(to.x), S(to.z), S(to.y)));
            lastTo = to; lastToAt = Time.time; watchUntil = Environment.TickCount + 2500;
            if (logN++ < 6) Debug.Log("[granada] REQ_GRENADE_FIRE hacia " + to);
            return true;
        }

        volatile int watchUntil; int watchLogged; Vector3 lastTo; float lastToAt = -100f;
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (unchecked(Environment.TickCount - watchUntil) < 0 && watchLogged < 40) { watchLogged++; var nm = PacketIds.Name(type); int len = b.Length; q.Enqueue(() => Debug.Log("[cohete] rx " + nm + " (" + len + " B) tras disparar")); }
            if (type == PacketIds.P_FE2CL_REP_PC_ROCKET_STYLE_FIRE_SUCC && b.Length >= 48)
            {   // iSkillID, iX,iY,iZ, iToX,iToY,iToZ, sbyte iBulletID@28, sPCBullet@32 {eAT, iID, bCharged}, iBatteryW@44, bNanoDeactive, iNanoID, iNanoStamina
                var body = b; q.Enqueue(() => OnFire(true, 0, I(body, 4), I(body, 8), I(body, 12), I(body, 16), I(body, 20), I(body, 24), (sbyte)body[28], I(body, 36), I(body, 44), I(body, 0)));
            }
            else if (type == PacketIds.P_FE2CL_REP_PC_GRENADE_STYLE_FIRE_SUCC && b.Length >= 44)
            {   // iSkillID@0, iToX,iToY,iToZ@4..12, sbyte iBulletID@16, sPCBullet@20 {eAT, iID@24, bCharged}, iBatteryW@32, bNanoDeactive@36, iNanoID@40, iNanoStamina@42  (el servidor OpenFusion responde así también al disparo de cohete)
                var body = b; q.Enqueue(() => OnFire(true, 0, int.MinValue, 0, 0, I(body, 4), I(body, 8), I(body, 12), (sbyte)body[16], I(body, 24), I(body, 32), I(body, 0)));
            }
            else if (type == PacketIds.P_FE2CL_PC_GRENADE_STYLE_FIRE && b.Length >= 36)
            {   // iPC_ID@0, iTo@4..12, sbyte iBulletID@16, sPCBullet@20, bNanoDeactive@32
                var body = b; q.Enqueue(() => OnFire(false, I(body, 0), int.MinValue, 0, 0, I(body, 4), I(body, 8), I(body, 12), (sbyte)body[16], I(body, 24), -1, 0));
            }
            else if (type == PacketIds.P_FE2CL_PC_ROCKET_STYLE_FIRE && b.Length >= 44)
            {   // iPC_ID, iX,iY,iZ, iToX,iToY,iToZ, sbyte iBulletID@28, sPCBullet@32, bNanoDeactive@44
                var body = b; q.Enqueue(() => OnFire(false, I(body, 0), I(body, 4), I(body, 8), I(body, 12), I(body, 16), I(body, 20), I(body, 24), (sbyte)body[28], I(body, 36), -1, 0));
            }
        }

        void OnFire(bool own, int pcId, int x, int y, int z, int tx, int ty, int tz, int slot, int itemId, int batteryW, int skillId)
        {
            try
            {
                if (ents == null) return; if (own && ents.SelfId != 0) pcId = ents.SelfId;
                if (!own && pcId == ents.SelfId) return;
                if (logN++ < 10) Debug.Log("[cohete] respuesta own=" + own + " ranura=" + slot + " arma=" + itemId + " skill=" + skillId + " batería=" + batteryW);
                bool boost = false;
                if (own && hud != null && hud.Data != null) { boost = hud.Data.BatteryWpn > 0 && hud.Data.UseWeaponBattery; if (skillId == 0 && batteryW >= 0) hud.Data.BatteryWpn = batteryW; }
                var owner = ents.TransformOfPlayer(pcId); if (owner == null) { Debug.LogWarning("[cohete] sin transform del jugador " + pcId); return; }
                if (skillId != 0) { SpawnSkillHead(slot, skillId, x == int.MinValue ? owner.position + Vector3.up * 0.8f : ToClient(x, y, z), ToClient(tx, ty, tz), !own, owner); return; }
                var item = ItemById(itemId); if (item == null) { Debug.LogWarning("[cohete] arma " + itemId + " sin fila en la tabla"); return; }
                var start = x == int.MinValue ? owner.position + Vector3.up * 0.8f : ToClient(x, y, z); var to = ToClient(tx, ty, tz);
                if (own && (to - start).sqrMagnitude < 0.25f && Time.time - lastToAt < 3f) { Debug.Log("[cohete] destino del servidor = origen; se usa el calculado en el cliente"); to = lastTo; }
                bool gren = item.m_iTargetMode == 6; var aimDirG = to - owner.position;   // granada: dirección = (destino - jugador), con su pendiente
                Vector3? requested = null;
                if (own && !gren)
                {
                    while (pendingShots.Count > 0 && Time.time - pendingShots.Peek().Time > 5f) pendingShots.Dequeue();
                    if (pendingShots.Count > 0) requested = pendingShots.Dequeue().Target;
                }
                var gt = FindDeep(owner, "Gtag01");
                ResolveFlight(x == int.MinValue, own, gren, owner.position, gt != null ? (Vector3?)gt.position : null, requested, ref start, ref to);
                if (gren && x != int.MinValue) to = start + aimDirG;
                if (!own)
                {   // UserMoveController.ReceiveRocketFire: el otro jugador se gira hacia el objetivo y ataca
                    var look = to; look.y = owner.position.y; var d = look - owner.position; if (d.sqrMagnitude > 1e-4f) owner.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
                SpawnHead(slot, itemId, item, boost, start, to, !own, owner);
            }
            catch (Exception e) { Debug.LogWarning("[cohete] " + e.Message); }
        }

        // ReceiveGrenadeFire uses avatar + .8, without the muzzle/flattening in
        // ReceiveRocketFire. OpenFusion uses this abbreviated response for rockets
        // and may substitute a guessed target height; keep our acknowledged shot's
        // original target so the local projectile follows the reticle's aim ray.
        public static void ResolveFlight(bool abbreviated, bool own, bool grenade, Vector3 owner, Vector3? muzzle, Vector3? requested, ref Vector3 start, ref Vector3 target)
        {
            if (abbreviated)
            {
                start = owner + Vector3.up * .8f;
                if (own && !grenade && requested.HasValue) target = requested.Value;
            }
            else if (muzzle.HasValue)
            {
                start = muzzle.Value;
                if (!grenade) target.y = start.y;
            }
        }

        void SpawnHead(int slot, int itemId, ItemElement item, bool boost, Vector3 start, Vector3 to, bool other, Transform owner)
        {
            if (slot < 0 || slot >= 3) { Debug.LogWarning("[cohete] ranura inválida " + slot); return; }
            int eff = boost && item.m_iEffect2 > 0 ? item.m_iEffect2 : item.m_iEffect1; var fx = BulletFx.Instance; var row = fx != null ? fx.RowOf(eff) : null;
            var vel = (to - start).normalized; start += vel * 0.3f;
            var go = new GameObject("cohete " + slot); go.transform.position = start;
            // InitInfo: Euler(atan2(vy, |vxz|), atan2(vx,vz)+π (en grados), 0)
            float yaw = (Mathf.Atan2(vel.x, vel.z) + 3.14f) * 180f / 3.14f, pitch = Mathf.Atan2(vel.y, new Vector2(vel.x, vel.z).magnitude) * 180f / 3.14f;
            go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (fx != null && row != null && row.Part > 0) fx.Spawn(row.Part, start, go.transform.rotation, row.BulletScale, go.transform, 30f);
            var h = new Head { Go = go, Slot = slot, Other = other, Vel = vel, Speed = item.m_iAtkRange * 0.01f, T0 = Time.time, Dur = item.m_iDurationTime * 0.1f, Area = item.m_iEffectArea * 0.01f, Row = row, Item = item, Owner = owner };
            if (item.m_iTargetMode == 6)
            {   // cnWarHead.InitInfo (modo 6): fVelZ = 0,1*DeliverTime*5 + dir.y*vel; la dirección horizontal queda con y = 0 (sin renormalizar)
                h.Grenade = true; h.VelZ = 0.1f * (item.m_iDeliverTime * 5f) + vel.y * h.Speed; h.Flat = new Vector3(vel.x, 0f, vel.z);
            }
            ReplaceHead(h);
            if (logN++ < 8) Debug.Log("[cohete] ojiva " + slot + " arma=" + itemId + " efecto=" + eff + " vel=" + h.Speed.ToString("F1") + " m/s dur=" + h.Dur.ToString("F2") + " s área=" + h.Area.ToString("F1") + " m desde " + start + (row == null ? " (SIN FILA DE BALA)" : ""));
        }

        /// cnWarHead.InitInfo (skill): EffectType 2 -> modo 5 (vel 10, 2 s, recta); EffectType 3 -> modo 6 (vel 8, 3 s, fVelZ = 7 + dir.y*8, parábola). Sólo llegan del servidor (skills 139-142).
        void SpawnSkillHead(int slot, int skillId, Vector3 start, Vector3 to, bool other, Transform owner)
        {
            if (slot < 0 || slot >= 3) { Debug.LogWarning("[cohete] ranura inválida " + slot); return; }
            var st = tables != null ? tables.m_pSkillTable : null; SkillElement se = null; if (st != null && st.m_pSkillData != null && skillId > 0 && skillId < st.m_pSkillData.Length) se = st.m_pSkillData[skillId];
            if (se == null) { Debug.LogWarning("[cohete] skill " + skillId + " sin fila"); return; }
            var vel = (to - start).normalized; var go = new GameObject("skill-ojiva " + slot); go.transform.position = start;
            float yaw = (Mathf.Atan2(vel.x, vel.z) + 3.14f) * 180f / 3.14f, pitch = Mathf.Atan2(vel.y, new Vector2(vel.x, vel.z).magnitude) * 180f / 3.14f; go.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            var fx = BulletFx.Instance; var row = fx != null ? fx.RowOf(0) : null;   // el original no fija iBulletID para skills (queda en 0)
            if (fx != null && row != null && row.Part > 0) fx.Spawn(row.Part, start, go.transform.rotation, row.BulletScale, go.transform, 30f);
            var h = new Head { Go = go, Slot = slot, Other = other, Vel = vel, T0 = Time.time, Row = row, Owner = owner, Skill = se, Area = se.m_iEffectArea * 0.01f };
            if (se.m_iEffectType == 3) { h.Grenade = true; h.Speed = 8f; h.Dur = 3f; h.VelZ = 7f + vel.y * 8f; h.Flat = new Vector3(vel.x, 0f, vel.z); }
            else { h.Speed = 10f; h.Dur = 2f; }
            ReplaceHead(h); if (logN++ < 10) Debug.Log("[cohete] ojiva de skill " + skillId + " tipo=" + se.m_iEffectType + " ranura=" + slot + " desde " + start);
        }

        void Update()
        {
            Action a; while (q.TryDequeue(out a)) a();
            for (int i = heads.Count - 1; i >= 0; i--)
            {
                var h = heads[i]; if (h.Go == null || h.Owner == null) { if (h.Go != null) Destroy(h.Go); heads.RemoveAt(i); continue; }
                if (Time.time - h.T0 > h.Dur) { Crash(h); continue; }
                var pos = h.Go.transform.position;
                if (h.Grenade)
                {   // cnWarHead.Update (modo 6): rebote contra colisionadores (x0,8), gravedad 9,8
                    var dir = new Vector3(h.Flat.x * h.Speed, h.VelZ, h.Flat.z * h.Speed); float mag = dir.magnitude; if (mag > 1e-4f) dir /= mag;
                    RaycastHit gh;
                    if (mag > 1e-4f && Physics.Raycast(pos, dir, out gh, mag * Time.deltaTime + 0.2f, RayMask, QueryTriggerInteraction.Ignore) && !(h.Owner != null && gh.transform.IsChildOf(h.Owner)) && gh.collider.GetComponentInParent<NpcInfo>() == null)
                    {
                        h.Flat.x += gh.normal.x; h.Flat.z += gh.normal.z; h.Flat.Normalize(); h.VelZ = gh.normal.y * Mathf.Abs(h.VelZ) * 0.8f;
                    }
                    bool nearG = false; foreach (var n in Mobs()) if (n.GetBounds().SqrDistance(pos) < 0.04f) { nearG = true; break; }
                    if (nearG) { Crash(h); continue; }
                    h.VelZ -= Time.deltaTime * 9.8f;
                    h.Go.transform.position = pos + new Vector3(h.Flat.x * h.Speed, h.VelZ, h.Flat.z * h.Speed) * Time.deltaTime; continue;
                }
                float step = h.Speed * Time.deltaTime, impact = float.PositiveInfinity;
                if (!h.Other)
                {
                    int count = Physics.RaycastNonAlloc(pos, h.Vel, worldHits, step + .2f, RayMask, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < count; j++)
                    {
                        var hit = worldHits[j];
                        if (hit.transform.IsChildOf(h.Owner) || hit.collider.GetComponentInParent<NpcInfo>() != null) continue;
                        impact = Mathf.Min(impact, hit.distance);
                    }
                }
                foreach (var n in Mobs())
                    if (SweepBounds(n.GetBounds(), pos, h.Vel * step, .2f, out float distance)) impact = Mathf.Min(impact, distance);
                if (!float.IsPositiveInfinity(impact)) { h.Go.transform.position = pos + h.Vel * Mathf.Min(step, impact); Crash(h); continue; }
                h.Go.transform.position = pos + h.Vel * step;
            }
        }

        IEnumerable<NpcInfo> Mobs()
        {
            if (ents == null) yield break;
            foreach (var go in ents.NpcObjects) { if (go == null) continue; var n = go.GetComponent<NpcInfo>(); if (n != null && n.IsMob && n.Hp > 0) yield return n; }
        }

        /// cnWarHead.CrashEffect: efecto de impacto (ES[succ], escala succ) y, si es propia, REQ_PC_ROCKET_STYLE_HIT con los NPC dentro de EffectArea.
        void Crash(Head h)
        {
            if (h.Crashed) return; h.Crashed = true; var pos = h.Go.transform.position; var fx = BulletFx.Instance;
            if (fx != null && h.Row != null && h.Row.SuccScale > 0f) fx.Spawn(h.Row.Succ, pos, Quaternion.identity, h.Row.SuccScale, null, 3f);
            BulletFx.CrashSound(h.Row, pos);
            if (!h.Other && NpcUI.Sender != null && h.Skill != null)
            {   // cnWarHead.CrashEffect (skill): REQ_NANO_SKILL_USE con iBulletID = ranura, iArg1..3 = posición del impacto y los objetivos (mobs si TargetType 1, jugadores si no) a menos de EffectRange
                var se = h.Skill; var ids = new List<int>(); float rng = se.m_iEffectRange * 0.01f;
                if (se.m_iTargetType == 1) { foreach (var n in Mobs()) if (n.GetBounds().SqrDistance(pos) <= rng * rng) ids.Add(n.Id); }
                else foreach (var kv in ents.OtherPlayers) { if (kv.Value != null && Vector3.Distance(kv.Value.transform.position, pos) <= rng) ids.Add(kv.Key); }
                if (ids.Count > 0) { NpcUI.Sender(ClientPackets.NanoSkillUse((sbyte)h.Slot, S(pos.x), S(pos.z), S(pos.y), ids.ToArray())); if (logN++ < 12) Debug.Log("[cohete] skill impacto objetivos=" + ids.Count); }
            }
            else if (!h.Other && NpcUI.Sender != null)
            {
                var ids = new List<int>();
                foreach (var n in Mobs()) if (n.GetBounds().SqrDistance(pos) <= h.Area * h.Area)
                    {
                        ids.Add(n.Id); if (fx != null && h.Row != null && h.Row.SuccScale > 0f) fx.Spawn(h.Row.Succ, n.transform.position + Vector3.up, Quaternion.identity, h.Row.SuccScale * 0.5f, null, 3f);
                    }
                NpcUI.Sender(ClientPackets.RocketHit((sbyte)h.Slot, S(pos.x), S(pos.z), S(pos.y), ids.ToArray()));
                if (logN++ < 12) Debug.Log("[cohete] impacto en " + pos + " objetivos=" + ids.Count);
            }
            Destroy(h.Go); heads.Remove(h);
        }
    }
}
