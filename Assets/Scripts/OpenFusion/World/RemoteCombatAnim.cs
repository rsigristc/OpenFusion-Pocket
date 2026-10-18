// RemoteCombatAnim: ataque, dash, herida, muerte y respawn de OTROS jugadores (cnAvatarAnimation.AvatarAttack / AvatarDash / AvatarDead / DamageMotion / ChangeWpn,
// UserMoveController.Attack / ReceiveRegen / AttackResult, GameFrame.ProcessPacket). SIN COMPILAR NI PROBAR EN UNITY.
//
// Paquetes (layout SOLO de sP_FE2CL_*.cs, Pack 4, offsets desde el cuerpo sin el tipo):
//   PC_ATTACK_NPCs {iPC_ID, iNPCCnt}+sAttackResult[24]        -> atacante: LookAt(1er NPC) + AvatarAttack(true) + bala (iBulletNum = m_iEffect1 del arma) a cada NPC
//   PC_ATTACK_CHARs {iPC_ID, iTargetCnt}+sAttackResult[24]    -> igual; los eCT != NPC son jugadores (muerte si iHP <= 0, herida si no)
//   PC_ROCKET_STYLE_FIRE {iPC_ID, iX,iY,iZ, iToX@16,iToY,iToZ, ...} / PC_GRENADE_STYLE_FIRE {iPC_ID, iToX@4,iToY,iToZ, ...} -> LookAt(destino) + AvatarAttack(true)
//   NANO_SKILL_USE (otros) {iPC_ID, sbyte, short iSkillID@6, ..., eST@28, iTargetCnt@32}: skill de tipo 40 -> AvatarDash (no hay PC_DASH en el protocolo legacy); resultados de dano en jugadores
//   PC_SUDDEN_DEAD {iPC_ID, reason, damage, iHP} -> AvatarDead;  PC_REGEN {iPC_ID, iHP, iX,iY,iZ, iAngle, ...} -> posicion + AvatarJump (jumpstart)
//   NPC_ATTACK_PCs / NPC_ATTACK_CHARs / NPC_SKILL_HIT / NPC_SKILL_CORRUPTION_HIT / PC_ROCKET_STYLE_HIT / PC_GRENADE_STYLE_HIT: resultados sobre jugadores remotos (iHP <= 0 -> AvatarDead; si no woundupper)
//   PC_EQUIP_CHANGE (ranura 0, otros): estilo de animacion del arma, arma visible y ChangeWpn ("sheath")
// Todo se recibe por PacketDispatcher.Raw (hilo de red) y se ejecuta en Update.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class RemoteCombatAnim : MonoBehaviour
    {
        public static RemoteCombatAnim Instance;
        static readonly ConcurrentDictionary<int, bool> deadPcs = new ConcurrentDictionary<int, bool>();
        /// <summary>El jugador remoto esta muerto (IsLive() == false): el original no lo incluye en las listas de objetivo (UserContainer.GetConeList / GetNearListAnyTeam). PlayerTarget / NameTags pueden saltarselo.</summary>
        public static bool IsDead(int pcId) { return deadPcs.ContainsKey(pcId); }
        /// <summary>Olvida el estado de un jugador (sale del campo de vision o entra uno nuevo con ese id).</summary>
        public static void Forget(int pcId) { bool d; deadPcs.TryRemove(pcId, out d); }

        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; PlayerAvatarFactory avatars;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        sealed class Pending { public float At; public Action Do; }
        readonly List<Pending> pending = new List<Pending>();
        Dictionary<int, ItemElement> wmap; float nextPoll; int logN;

        sealed class Tgt { public int Ct, Id, Hp; }

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, PlayerAvatarFactory av)
        {
            if (disp != null) disp.Raw -= OnRaw;
            ents = e; disp = d; tables = t; avatars = av; Instance = this; wmap = null;
            if (disp != null) disp.Raw += OnRaw;
        }
        void OnDestroy() { if (disp != null) disp.Raw -= OnRaw; if (Instance == this) Instance = null; deadPcs.Clear(); }

        // ------------------------------------------------------------------ tablas
        ItemElement WeaponById(int id)
        {
            var wt = tables != null ? tables.m_pWeaponItemTable : null; if (id <= 0 || wt == null || wt.m_pItemData == null) return null;
            if (wmap == null) { wmap = new Dictionary<int, ItemElement>(); foreach (var e in wt.m_pItemData) if (e != null) wmap[e.m_iItemNumber] = e; }
            ItemElement r; return wmap.TryGetValue(id, out r) ? r : null;
        }
        /// <summary>Arma (fila de la tabla) del jugador remoto, a partir de Equip[0] con el que entro / el ultimo PC_EQUIP_CHANGE. null = puños.</summary>
        public ItemElement WeaponOf(int pcId)
        {
            var app = ents != null ? ents.AppOf(pcId) : null; if (app == null || app.Equip == null || app.Equip.Length == 0) return null;
            return WeaponById(app.Equip[0].Id);
        }
        /// <summary>Arma del jugador remoto al que pertenece el transform (hueso/objeto del avatar que lanza un evento de animacion). null si no es un jugador remoto.</summary>
        public ItemElement WeaponItemOf(Transform t)
        {
            if (t == null) return null; var rp = t.GetComponentInParent<RemotePc>(); return rp != null ? WeaponOf(rp.Id) : null;
        }
        /// <summary>cnAvatarStatus.iAniStyle segun m_iEquipType: 1,2 stick; 3 pistol; 4 rifle; 10 bomb; 11 rocket (misma correspondencia que CombatSystem.ReadWeapon).</summary>
        static int StyleOfWeapon(ItemElement e)
        {
            if (e == null) return 0;
            switch (e.m_iEquipType) { case 1: case 2: return 1; case 3: return 2; case 4: return 3; case 10: return 6; case 11: return 4; }
            return 0;
        }
        SkillElement SkillOf(int id) { var t = tables != null ? tables.m_pSkillTable : null; return t != null && t.m_pSkillData != null && id > 0 && id < t.m_pSkillData.Length ? t.m_pSkillData[id] : null; }

        // ------------------------------------------------------------------ utilidades (hilo principal)
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        GameObject PlayerGo(int pc) { if (ents == null || pc == ents.SelfId) return null; var t = ents.TransformOfPlayer(pc); return t != null ? t.gameObject : null; }
        NpcAnimator AnimOf(int pc) { var g = PlayerGo(pc); return g != null ? g.GetComponent<NpcAnimator>() : null; }
        void After(float delay, Action a) { if (delay <= 0.01f) { a(); return; } pending.Add(new Pending { At = Time.time + delay, Do = a }); }

        void Update()
        {
            Action a; while (q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[rcombat] " + ex.Message); } }
            if (pending.Count > 0)
            {
                float now = Time.time;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (i >= pending.Count || pending[i].At > now) continue;
                    var act = pending[i].Do; pending.RemoveAt(i);
                    try { act(); } catch (Exception ex) { Debug.LogWarning("[rcombat] " + ex.Message); }
                }
            }
            if (Time.time >= nextPoll) { nextPoll = Time.time + 1f; PollStyles(); }
        }

        /// El estilo de animacion (prefijo del arma) de un remoto puede haberse calculado antes de que la tabla de armas estuviera disponible: se corrige a 1 Hz.
        void PollStyles()
        {
            if (ents == null) return;
            foreach (var kv in ents.OtherPlayers)
            {
                var go = kv.Value; if (go == null) continue;
                var app = ents.AppOf(kv.Key); if (app == null || app.Equip == null || app.Equip.Length == 0) continue;
                var na = go.GetComponent<NpcAnimator>(); if (na == null) continue;
                if (avatars != null && kv.Value.GetComponent<AvatarOutfitStamp>()?.Signature != PlayerAvatarFactory.OutfitSignature(app, avatars.CombineEquip)) avatars.RefreshEquipment(kv.Value, app);
                int st = StyleOfWeapon(WeaponById(app.Equip[0].Id)); if (na.AniStyle != st) na.SetStyle(st);
            }
        }

        // ------------------------------------------------------------------ red (hilo de red -> cola)
        void OnRaw(uint type, byte[] b)
        {
            if (b == null) return;
            try { Route(type, b); }
            catch (Exception ex) { var m = ex.Message; q.Enqueue(() => Debug.LogWarning("[rcombat] paquete " + type + ": " + m)); }
        }

        void Route(uint type, byte[] b)
        {
            switch (type)
            {
                case PacketIds.P_FE2CL_PC_ATTACK_NPCs: if (b.Length >= 8) Attack(I(b, 0), b, false); break;
                case PacketIds.P_FE2CL_PC_ATTACK_CHARs: if (b.Length >= 8) Attack(I(b, 0), b, true); break;
                case PacketIds.P_FE2CL_PC_ROCKET_STYLE_FIRE: if (b.Length >= 28) Fire(I(b, 0), I(b, 16), I(b, 20), I(b, 24)); break;
                case PacketIds.P_FE2CL_PC_GRENADE_STYLE_FIRE: if (b.Length >= 16) Fire(I(b, 0), I(b, 4), I(b, 8), I(b, 12)); break;
                case PacketIds.P_FE2CL_NANO_SKILL_USE: if (b.Length >= 36) NanoSkill(b); break;
                case PacketIds.P_FE2CL_PC_SUDDEN_DEAD: if (b.Length >= 4) { int id = I(b, 0); q.Enqueue(() => Die(id)); } break;
                case PacketIds.P_FE2CL_PC_REGEN:
                    if (b.Length >= 24) { int id = I(b, 0); var pos = PlayerAvatarFactory.ToClient(I(b, 8), I(b, 12), I(b, 16)); q.Enqueue(() => Regen(id, pos)); }
                    break;
                case PacketIds.P_FE2CL_NPC_ATTACK_PCs: if (b.Length >= 8) Victims(b, I(b, 4), 8, 24, false, 0.2f); break;
                case PacketIds.P_FE2CL_NPC_ATTACK_CHARs: if (b.Length >= 8) Victims(b, I(b, 4), 8, 24, true, 0.2f); break;
                case PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT:
                case PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT: if (b.Length >= 24) Victims(b, I(b, 20), 24, 24, true, 0f); break;
                case PacketIds.P_FE2CL_NPC_SKILL_HIT:
                    if (b.Length >= 28) { int est = I(b, 20); Victims(b, I(b, 24), 28, (est == 3 || est == 4 || est == 5 || est == 8) ? 32 : 20, true, 0.3f); }
                    break;
                case PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT: if (b.Length >= 24) Victims(b, I(b, 20), 24, 40, true, 0.3f); break;
                case PacketIds.P_FE2CL_PC_EQUIP_CHANGE:
                    if (b.Length >= 20 && I(b, 4) >= 0 && I(b, 4) < 9)
                    {
                        int id = I(b, 0); var item = new ItemBase { Type = BitConverter.ToInt16(b, 8), Id = BitConverter.ToInt16(b, 10), Opt = I(b, 12), TimeLimit = I(b, 16) };
                        int slot = I(b, 4); q.Enqueue(() => EquipmentChanged(id, slot, item));
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ ataque
        void Attack(int pc, byte[] b, bool chars)
        {
            int n = Math.Min(Math.Max(I(b, 4), 0), 64); var l = new List<Tgt>(n);
            for (int i = 0; i < n; i++) { int o = 8 + 24 * i; if (o + 20 > b.Length) break; l.Add(new Tgt { Ct = I(b, o), Id = I(b, o + 4), Hp = I(b, o + 16) }); }
            q.Enqueue(() => DoAttack(pc, l, chars));
        }

        Transform TargetT(Tgt t, bool chars)
        {
            if (chars && t.Ct != 2 && t.Ct != 4) return ents.TransformOfPlayer(t.Id);
            var g = ents.NpcById(t.Id); return g != null ? g.transform : null;
        }

        void DoAttack(int pc, List<Tgt> l, bool chars)
        {
            if (ents == null) return;
            var go = PlayerGo(pc); var w = go != null ? WeaponOf(pc) : null;
            int bullet = w != null && w.m_iEffect1 > 0 ? w.m_iEffect1 : 1; float delay = 0.2f;
            if (go != null)
            {
                Transform first = l.Count > 0 ? TargetT(l[0], chars) : null;   // GameFrame: solo el primer resultado fija el blanco (Attack -> LookAt) y llama a AvatarAttack una vez
                FaceAndAttack(go, first != null ? first.position : go.transform.position, first != null, w);
                var fx = BulletFx.Instance;
                if (fx != null)
                {
                    var row = fx.RowOf(bullet); if (row != null) delay = row.Hide + row.MaxTimer;
                    for (int i = 0; i < l.Count; i++) { var tt = TargetT(l[i], chars); if (tt != null) fx.Fire(bullet, go.transform, tt, HeightOf(tt), Vector3.zero); }   // AvatarUtil.MakeBullet(atacante, blanco, iBulletNum, tipo)
                }
            }
            if (!chars) return;
            for (int i = 0; i < l.Count; i++) if (l[i].Ct != 2 && l[i].Ct != 4) Hit(l[i].Id, l[i].Hp, delay);   // UserMoveController.AttackResult: iCurHP = iHP; si <= 0 -> AvatarDead
        }

        static float HeightOf(Transform t) { var inf = t != null ? t.GetComponent<NpcInfo>() : null; return inf != null ? inf.Height : 1.6f; }

        void FaceAndAttack(GameObject go, Vector3 at, bool hasTarget, ItemElement w)
        {
            var rm = go.GetComponent<RemoteMover>(); var na = go.GetComponent<NpcAnimator>();
            if (hasTarget && rm != null) { var d = at - go.transform.position; d.y = 0f; if (d.sqrMagnitude > 1e-4f) rm.Face(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg); }
            if (na != null) na.PlayerAttack(w != null && w.m_iDelayTime > 0 ? w.m_iDelayTime * 0.1f : 0.65f);   // ChangeAniSpeed: el clip dura como mucho m_iDelayTime * 0,1 s
        }

        /// ReceiveRocketFire / ReceiveGrenadeFire: el otro jugador mira al destino (y = el suyo) y AvatarAttack(true). La ojiva la crea RocketSystem.
        void Fire(int pc, int tx, int ty, int tz)
        {
            var to = PlayerAvatarFactory.ToClient(tx, ty, tz);
            q.Enqueue(() => { var go = PlayerGo(pc); if (go != null) FaceAndAttack(go, to, true, WeaponOf(pc)); });
        }

        // ------------------------------------------------------------------ skills de nano (dash)
        void NanoSkill(byte[] b)
        {
            int pc = I(b, 0), skill = BitConverter.ToInt16(b, 6), est = I(b, 28), cnt = I(b, 32);
            var se = SkillOf(skill);
            if (se != null && se.m_iSkillType == 40) q.Enqueue(() => { var na = AnimOf(pc); if (na != null) na.PlayerDash(); });   // cnAvatarStatus.NanoSkillUseSucc (no propio): m_iSkillType == 40 -> AvatarDash
            if (est == 1 || est == 3 || est == 4 || est == 5 || est == 8 || est == 30) Victims(b, cnt, est == 30 ? 52 : 36, (est == 3 || est == 4 || est == 5 || est == 8) ? 32 : 20, true, 0.3f);
        }

        // ------------------------------------------------------------------ resultados sobre jugadores
        /// Lee cnt resultados (eCT@0, iID@4, iHP@16) y encola Hit para cada jugador (onlyPc: solo eCT == 1).
        void Victims(byte[] b, int cnt, int first, int stride, bool onlyPc, float delay)
        {
            int n = Math.Min(Math.Max(cnt, 0), 64); List<int> ids = null, hps = null;
            for (int i = 0; i < n; i++)
            {
                int o = first + stride * i; if (o + 20 > b.Length) break;
                if (onlyPc && I(b, o) != 1) continue;
                if (ids == null) { ids = new List<int>(); hps = new List<int>(); }
                ids.Add(I(b, o + 4)); hps.Add(I(b, o + 16));
            }
            if (ids == null) return;
            q.Enqueue(() => { for (int k = 0; k < ids.Count; k++) Hit(ids[k], hps[k], delay); });
        }

        void Hit(int id, int hp, float delay)
        {
            if (ents == null || id == ents.SelfId) return;
            if (hp <= 0) { Die(id); return; }
            if (IsDead(id)) return;
            After(delay, () => { var na = AnimOf(id); if (na != null) na.PlayerWound(); });
        }

        // ------------------------------------------------------------------ muerte / respawn
        void Die(int id)
        {
            if (ents == null || id == ents.SelfId) return;
            deadPcs[id] = true;
            var na = AnimOf(id); if (na != null) na.PlayerDie();
            if (logN++ < 8) Debug.Log("[rcombat] el jugador " + id + " muere");
        }

        void Regen(int pc, Vector3 pos)
        {
            if (ents == null || pc == ents.SelfId) return;
            Forget(pc);
            var go = PlayerGo(pc); if (go == null) return;
            var rm = go.GetComponent<RemoteMover>(); if (rm != null) rm.Warp(pos);
            var na = go.GetComponent<NpcAnimator>(); if (na != null) na.PlayerRevive(true);
            if (logN++ < 8) Debug.Log("[rcombat] el jugador " + pc + " reaparece en " + pos);
        }

        // ------------------------------------------------------------------ cambio de arma
        void EquipmentChanged(int pc, int slot, ItemBase item)
        {
            if (ents == null || pc == ents.SelfId) return;
            var app = ents.AppOf(pc); var go = PlayerGo(pc); if (app == null || app.Equip == null || app.Equip.Length == 0 || go == null) return;
            if (app.Equip[slot].Id == item.Id && app.Equip[slot].Opt == item.Opt) return;   // ReceiveEquipChage: solo si cambia el id
            app.Equip[slot] = item;
            if (slot != 0) { avatars?.RefreshEquipment(go, app); return; }
            var na = go.GetComponent<NpcAnimator>(); if (na != null) { na.SetStyle(StyleOfWeapon(WeaponById(item.Id))); na.PlayerSheath(); }
            if (avatars != null) { try { avatars.RefreshWeapon(go, app); } catch (Exception ex) { Debug.LogWarning("[rcombat] arma visible de " + pc + ": " + ex.Message); } }
        }
    }
}
