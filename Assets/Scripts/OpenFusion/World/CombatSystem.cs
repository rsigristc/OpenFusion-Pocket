// Combate básico al estilo del original (cnAvatarAttack.Update/GetConeList/AttackTarget + CnGuiMonster_info.DoWindowMob):
//  - Objetivo: NPC hostil (m_iTeam != 1, tipo <= 110 y != 25) vivo dentro del cono de visión (90 deg, 15 m) respecto a la dirección de la cámara; el más cercano.
//  - Lista de ataque: dentro del radio del NPC o a menos de max(radio, alto/2) + AtkRange*0.01 y dentro de AtkAngle (grados); hasta TargetNumber; el arma da rango/ángulo/objetivos.
//  - Click izquierdo (mantenido): P_CL2FE_REQ_PC_ATTACK_NPCs con los ids + animación attackN (o attackNupper en movimiento) con el prefijo del arma.
//  - Resultados: PC_ATTACK_NPCs_SUCC / PC_ATTACK_NPCs -> HP del NPC, números de daño flotantes. HUD: NpcUI.MobTarget (mop_info + HP_BAR).
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    [DefaultExecutionOrder(100)]
    public sealed partial class CombatSystem : MonoBehaviour
    {
        public bool Enabled = true;
        public float AttackInterval = 0.65f;     // cooltime del arma = m_iDelayTime * 0.1 s (GameCondition.SetCoolTimeMax(0, ...)); 0,65 s sólo sin arma
        public float ViewAngleDeg = 90f, ViewDistance = 15f;
        public int DefaultRange = 300, DefaultAngle = 90, DefaultTargets = 1;   // sin arma (puños)

        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; GameClient gc; RocketSystem rockets; RemoteCombatAnim remoteAnim;
        int range, angle, maxTargets; int aniStyle; bool weaponLogged; int bulletType = 1, targetMode; bool inCombat; PcAppearance selfApp; PlayerAvatarFactory avatars;
        float nextAttack; int logN, moveLog; int pendFrom = -1, pendTo = -1;
        float pendingWeaponUntil;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        sealed class Pop { public Vector3 Pos; public string Text; public float T0; public Color Col; }
        readonly List<Pop> pops = new List<Pop>();
        readonly List<NpcInfo> view = new List<NpcInfo>(), atk = new List<NpcInfo>();

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, ModelFactory models = null, PlayerAvatarFactory av = null)
        {
            avatars = av; { rockets = GetComponent<RocketSystem>(); if (rockets == null) rockets = gameObject.AddComponent<RocketSystem>(); rockets.Init(e, d, t, GetComponent<PlayerHud>()); }
            if (models != null) { Sfx.Boot(gameObject, models, null); AnimEventSink.AttackSound = WeaponSound; }
            if (models != null) { var bf = GetComponent<BulletFx>(); if (bf == null) bf = gameObject.AddComponent<BulletFx>(); bf.Init(models); }
            if (disp != null) disp.Unhandled -= OnPacket;
            ents = e; disp = d; tables = t; range = DefaultRange; angle = DefaultAngle; maxTargets = DefaultTargets;
            if (disp != null) disp.Unhandled += OnPacket;
            if (ents != null && ents.WeaponTable == null && tables != null) ents.WeaponTable = tables.m_pWeaponItemTable;   // estilo de arma (stick/pistol/rifle..) de los jugadores remotos
            remoteAnim = GetComponent<RemoteCombatAnim>(); if (remoteAnim == null) remoteAnim = gameObject.AddComponent<RemoteCombatAnim>(); remoteAnim.Init(e, d, t, av);   // ataque / dash / muerte / respawn de otros jugadores
            if (ents != null) ents.SelfSpawned += (go, es) =>
            {
                selfApp = es.Self; ReadWeapon(es.Self);
                if (Sfx.Instance != null) { Sfx.Instance.Listener = go.transform; MusicController.Boot(gameObject, go.transform); }
            };
        }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; NpcUI.MobTarget = null; }

        ItemElement weaponIt;

        /// cnAvatarStatus.AttackSound (jugador propio): con batería del arma, cadena 1 del sonido 2; si no, cadena 1/2/3 al 70/15/15 % del sonido 1. Ruta = "sound/"+cadena.
        string WeaponSound(Transform t)
        {
            if (ents == null || t == null) return null;
            bool own = ents.Self != null && t.IsChildOf(ents.Self.transform);
            ItemElement it = own ? weaponIt : (remoteAnim != null ? remoteAnim.WeaponItemOf(t) : null);   // cnAvatarStatus.AttackSound tambien suena en otros jugadores (con SU arma)
            if (it == null) return null;
            var wt = tables != null ? tables.m_pWeaponItemTable : null; var snd = wt != null ? wt.m_pItemSoundData : null; if (snd == null) return null;
            bool boost = false; if (own) { var hud = GetComponent<PlayerHud>(); boost = hud != null && hud.Data != null && hud.Data.BatteryWpn > 0 && hud.Data.UseWeaponBattery; }
            Func<string, bool> bad = x => string.IsNullOrEmpty(x) || x.IndexOf("null") != -1 || x.IndexOf("...") != -1;
            string r;
            if (boost) { int i2 = it.m_iSound2; var e2 = i2 >= 0 && i2 < snd.Length ? snd[i2] : null; r = e2 != null ? e2.m_pstrSoundString1 : null; }
            else
            {
                int i1 = it.m_iSound1; var e1 = i1 >= 0 && i1 < snd.Length ? snd[i1] : null; if (e1 == null) return null;
                int n = UnityEngine.Random.Range(0, 100); r = n < 70 ? e1.m_pstrSoundString1 : (n >= 85 ? e1.m_pstrSoundString3 : e1.m_pstrSoundString2);
                if (r == "null" || (r != null && r.IndexOf("...") != -1)) r = e1.m_pstrSoundString1;
            }
            return bad(r) ? null : "sound/" + r;
        }

        void ReadWeapon(PcAppearance me)
        {
            weaponIt = null;
            range = DefaultRange; angle = DefaultAngle; maxTargets = DefaultTargets; aniStyle = 0; bulletType = 1; targetMode = 0; AttackInterval = 0.65f;
            try
            {
                var av = tables != null && tables.m_pAvatarTable != null ? tables.m_pAvatarTable.m_pAvatarData : null;
                if (av != null && av.Length > 1 && av[1] != null) { range = av[1].m_iAtkRange; angle = av[1].m_iAtkAngle; maxTargets = Mathf.Max(1, av[1].m_iTargetNumber); }
                var w = me != null && me.Equip != null && me.Equip.Length > 0 ? me.Equip[0] : default(ItemBase);
                ItemElement it = null;
                if (w.Id > 0 && tables != null && tables.m_pWeaponItemTable != null && tables.m_pWeaponItemTable.m_pItemData != null)
                    foreach (var e in tables.m_pWeaponItemTable.m_pItemData) if (e != null && e.m_iItemNumber == w.Id) { it = e; break; }
                weaponIt = it;
                if (it != null)
                {
                    bulletType = it.m_iEffect1 > 0 ? it.m_iEffect1 : 1; targetMode = it.m_iTargetMode; if (it.m_iDelayTime > 0) AttackInterval = it.m_iDelayTime * 0.1f;
                    if (it.m_iAtkRange > 0) range = it.m_iAtkRange; if (it.m_iAtkAngle > 0) angle = it.m_iAtkAngle; if (it.m_iTargetNumber > 0) maxTargets = it.m_iTargetNumber;
                    switch (it.m_iEquipType) { case 1: case 2: aniStyle = 1; break; case 3: aniStyle = 2; break; case 4: aniStyle = 3; break; case 10: aniStyle = 6; break; case 11: aniStyle = 4; break; default: aniStyle = 0; break; }
                    Debug.Log("[atk] arma id=" + w.Id + " equipType=" + it.m_iEquipType + " modo=" + it.m_iTargetMode + " rango=" + range + " ángulo=" + angle + " objetivos=" + maxTargets + " inicial=" + it.m_iInitalTime + " entrega=" + it.m_iDeliverTime + " retardo=" + it.m_iDelayTime + " duración=" + it.m_iDurationTime + " estilo=" + aniStyle);
                }
                else Debug.Log("[atk] sin arma equipada (Equip[0].Id=" + w.Id + "): puños rango=" + range + " ángulo=" + angle);
            }
            catch (Exception ex) { Debug.LogWarning("[atk] arma: " + ex.Message); }
            if (gc == null) gc = FindObjectOfType<GameClient>();
            if (gc != null) { gc.AniStyle = aniStyle; gc.ResetAnimClip(); }
        }

        // ---- red (hilo de red -> cola) ----
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        void QueueNpcResult(Action change) { if (ents != null) ents.Enqueue(change); else q.Enqueue(change); }
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (b.Length >= 4 && (type == PacketIds.P_FE2CL_NPC_SKILL_READY || type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_READY || type == PacketIds.P_FE2CL_NPC_SKILL_HIT || type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT || type == PacketIds.P_FE2CL_NPC_SKILL_CANCEL))
            { int npc = I(b, 0); string phase = type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_READY ? "corruption-ready" : type == PacketIds.P_FE2CL_NPC_SKILL_READY ? "skill-ready" : "hit-or-cancel"; QueueNpcResult(() => CombatAudit.Phase(npc, phase));
                if ((type == PacketIds.P_FE2CL_NPC_SKILL_READY || type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_READY) && b.Length >= 6) {
                    var ready = b; QueueNpcResult(() => NpcSkillPresentation(ready, true));
                } }
            if (type == PacketIds.P_FE2CL_NANO_SKILL_USE && b.Length >= 36 && I(b, 28) == 28)
            {   // W24: eST_Recall_Group (28) de OTRO jugador: GameFrame.SkillResultDistributor case 28 -> Status.ProcessSkillResult evento 28 sobre el avatar propio -> caja "<nombre> is trying to recall you. Accept?"
                //   NANO_SKILL_USE {iPC_ID@0 (lanzador), ..., eST@28, iTargetCnt@32} + sSkillResult_Move[24 B] {eCT@0, iID@4, iMapNum, iMoveX, iMoveY, iMoveZ}
                var rb = b; int caster = I(b, 0), rn = Mathf.Clamp(I(b, 32), 0, 64);
                q.Enqueue(() =>
                {
                    if (ents == null || ents.SelfId == 0 || caster == ents.SelfId || SocialMenu.Instance == null) return;
                    for (int i = 0; i < rn; i++)
                    {
                        int o = 36 + 24 * i; if (o + 8 > rb.Length) break;
                        if (I(rb, o) == 1 && I(rb, o + 4) == ents.SelfId) { SocialMenu.Instance.OfferRecall(caster); break; }   // el bloqueo recallLock evita duplicados
                    }
                });
            }
            if (type == PacketIds.P_FE2CL_PC_ATTACK_NPCs_SUCC || type == PacketIds.P_FE2CL_PC_ATTACK_NPCs)
            {
                var body = b; bool other = type == PacketIds.P_FE2CL_PC_ATTACK_NPCs;
                foreach (var hit in AttackResults.Read(b, 4, 8)) if (hit.TargetsNpc(true) && hit.Hp <= 0) Killed[hit.Id] = other ? I(b, 0) : -1;   // iPC_ID del atacante (-1 = yo)
                QueueNpcResult(() => { if (!other && gc != null) gc.EnterCombat(); ApplyResults(body, other ? I(body, 0) : -1, 4, 8, npcOnly: true); });
            }
            else if ((type == PacketIds.P_FE2CL_PC_ROCKET_STYLE_HIT || type == PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT) && b.Length >= 24)
            {   // sP_FE2CL_PC_ROCKET_STYLE_HIT: iPC_ID, sbyte iBulletID@4, sPCBullet@8, iTargetCnt@20 + sAttackResult[24 B]
                var body = b; int pc = I(b, 0); int nn = I(b, 20); int who = ents != null && pc == ents.SelfId ? -1 : pc;
                // OpenFusion's projectileHit sends the grenade compatibility packet
                // for rockets too; its NPC-only results leave eCT zero.
                bool npcOnly = type == PacketIds.P_FE2CL_PC_GRENADE_STYLE_HIT;
                foreach (var hit in AttackResults.Read(b, 20, 24)) if (hit.TargetsNpc(npcOnly) && hit.Hp <= 0) Killed[hit.Id] = who;
                QueueNpcResult(() => { if (who < 0 && gc != null) gc.EnterCombat(); ApplyResults(body, who, 20, 24, npcOnly: npcOnly); });
            }
            else if (type == PacketIds.P_FE2CL_CHAR_TIME_BUFF_TIME_TICK && b.Length >= 12)
            {
                int ct = I(b, 0), id = I(b, 4), tb = BitConverter.ToInt16(b, 8);
                int size = tb == 17 ? 32 : tb == 19 ? 20 : tb == 24 ? 16 : 0;
                if (size == 0 || b.Length < 12 + size || I(b, 12) != ct || I(b, 16) != id) return;
                if (ct != 2 && ct != 4) return; // Local player ticks are handled by PlayerHud.
                var body = b;
                if (tb != 24 && I(b, 28) <= 0) Killed[id] = 0; // Tick has no attacker ID; do not claim local kill credit.
                QueueNpcResult(() =>
                {
                    if (tb == 24)
                    {
                        var go = ents != null ? ents.NpcById(id) : null; if (go == null) return;
                        var info = go.GetComponent<NpcInfo>(); if (info != null) info.Hp = Mathf.Max(0, I(body, 24));
                        FloatingPoints.AddOn(go, 1, FloatingPoints.Heal, I(body, 20), info != null ? info.Height : 1.8f);
                    }
                    else
                    {
                        var results = new byte[4 + size]; ClientPackets.PutInt(results, 0, 1); Buffer.BlockCopy(body, 12, results, 4, size);
                        ApplyResults(results, 0, 0, 4, size, FloatingPoints.Dot);
                    }
                });
            }
            else if (type == PacketIds.P_FE2CL_NPC_ATTACK_PCs && b.Length >= 8)
            {   // un NPC me golpea: GameFrame.SendCombatMode
                var body = b; q.Enqueue(() =>
                {
                    int nn = I(body, 4); string nname = null; var ng = ents != null ? ents.NpcById(I(body, 0)) : null; if (ng != null) { var ni = ng.GetComponent<NpcInfo>(); if (ni != null) nname = ni.Name; }
                    for (int i = 0; i < nn; i++)
                    {
                        int o = 8 + 24 * i; if (o + 24 > body.Length) break; if (ents == null) break; int tid = I(body, o + 4); bool self = tid == ents.SelfId;
                        if (self && gc != null) gc.EnterCombat();
                        int dmg = I(body, o + 12), hp = I(body, o + 16); int fv = I(body, o + 8) != 0 ? -2 : dmg;
                        var tt = ents.TransformOfPlayer(tid); if (tt != null) FloatingPoints.AddOn(tt.gameObject, 0, FloatingPoints.Damage, fv, 1.8f);
                        if (self && nname != null) ChatHud.AddEventString(6, hp <= 0 ? "You have been defeated by " + nname + "." : "The " + nname + " wounds you for " + dmg + " points of damage.");
                    }
                });
            }
            else if ((type == PacketIds.P_FE2CL_NPC_SKILL_HIT && b.Length >= 28) || (type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT && b.Length >= 24))
            {   // NPC lanza skill / Corruption / Eruption: cnOwnAvatarStatus.Damage -> mensajes tipo 6 cuando el blanco soy yo
                var body = b; bool corr = type == PacketIds.P_FE2CL_NPC_SKILL_CORRUPTION_HIT; q.Enqueue(() => NpcSkillMessages(body, corr));
            }
            else if (type == PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC && b.Length >= 40)
            {   // sP_FE2CL_PC_ITEM_MOVE_SUCC: eFrom, iFromSlotNum, sItemBase(12), eTo, iToSlotNum, sItemBase(12); eFrom/eTo 0 = equipo
                var body = b; if (moveLog++ < 4) Debug.Log("[atk] ITEM_MOVE_SUCC " + b.Length + " B: " + BitConverter.ToString(b));
                q.Enqueue(() =>
                {
                    int ef = I(body, 0), fs = I(body, 4), et = I(body, 20), ts = I(body, 24); if (selfApp == null) return;
                    MoveAck.Fix(ref ef, fs, ref et, ts, Item(body, 8), Item(body, 28));
                    bool match = pendFrom >= 0 && ef == 0 && et == 0 && ((fs == pendFrom && ts == pendTo) || (fs == pendTo && ts == pendFrom));
                    if (ef == 0 && fs >= 0 && fs < 9) selfApp.Equip[fs] = Item(body, 8);
                    if (et == 0 && ts >= 0 && ts < 9) selfApp.Equip[ts] = Item(body, 28);
                    if (match) { pendFrom = pendTo = -1; pendingWeaponUntil = 0f; }
                    if (ef == 0 || et == 0) EquipChanged();
                });
            }
            else if (type == PacketIds.P_FE2CL_PC_EQUIP_CHANGE && b.Length >= 20)
            {   // iPC_ID, iEquipSlotNum, sItemBase
                var body = b; q.Enqueue(() => { if (selfApp == null || ents == null || I(body, 0) != ents.SelfId) return; int sl = I(body, 4); if (sl >= 0 && sl < 9) { selfApp.Equip[sl] = Item(body, 8); EquipChanged(); } });
            }
        }
        static ItemBase Item(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = I(b, o + 4), TimeLimit = I(b, o + 8) }; }
        bool equipmentPending; float nextEquipmentRefresh;
        void EquipChanged() { ReadWeapon(selfApp); equipmentPending = true; RefreshEquipment(); }
        void RefreshEquipment()
        {
            if (!equipmentPending || Time.unscaledTime < nextEquipmentRefresh || avatars == null || ents == null || ents.Self == null) return;
            nextEquipmentRefresh = Time.unscaledTime + .5f;
            if (VehicleRide.SelfRiding || BroomFlight.Active || ZiplineRide.Riding) return;
            try {
                var before = ents.Self.GetComponentInChildren<Animation>(true);
                if (!avatars.RefreshEquipment(ents.Self, selfApp)) return;
                equipmentPending = false;
                if (before != ents.Self.GetComponentInChildren<Animation>(true)) {
                    gc?.InvalidateAvatarAnimation(); GetComponent<PlayerHud>()?.InvalidatePortrait(); InvenMode.Instance?.RefreshAvatar();
                }
            } catch (Exception e) { Debug.LogWarning("[equipment] refresh: " + e.Message); }
        }
        /// cnAvatarAttack.WeaponChange (TAB): intercambia las ranuras de equipo 0 (arma) y 7 (arma secundaria) con REQ_ITEM_MOVE.
        void SwitchWeapon()
        {
            if (selfApp == null || NpcUI.Sender == null) return;
            if (pendFrom >= 0 && Time.unscaledTime < pendingWeaponUntil) return;
            bool main = selfApp.Equip[0].Id > 0, sub = selfApp.Equip[7].Id > 0; if (!main && !sub) { Debug.Log("[atk] TAB: sin armas en las ranuras 0 y 7"); return; }
            int from = sub ? 7 : 0, to = sub ? 0 : 7;
            FirstUse.Check(62); Debug.Log("[atk] TAB: arma " + from + " -> " + to); pendFrom = from; pendTo = to;
            pendingWeaponUntil = Time.unscaledTime + 5f;
            NpcUI.Sender(ClientPackets.ItemMove(0, from, 0, to));
        }
        /// NPC muertos por un ataque (id -> id del PC atacante, -1 = el jugador propio). Se rellena al llegar el paquete (hilo de red) para que NPC_EXIT pueda usarlo.
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> Killed = new System.Collections.Concurrent.ConcurrentDictionary<int, int>();
        public int TargetMode { get { return targetMode; } }
        /// Texto flotante (daño/curación) sobre una posición del mundo.
        public void AddPop(Vector3 pos, string text, Color col) { pops.Add(new Pop { Pos = pos, Text = text, T0 = Time.time, Col = col }); }
        /// Resultados de skill de daño (sSkillResult_Damage 20 B / Damage_N_Debuff 32 B): mismos offsets que sAttackResult (eCT, iID@4, bProtected, iDamage@12, iHP@16).
        public void ApplySkillDamage(byte[] b, int attacker, int cntOff, int firstOff, int stride) { ApplyResults(b, attacker, cntOff, firstOff, stride, FloatingPoints.SkillDamage); }
        void ApplyResults(byte[] b, int attacker, int cntOff, int firstOff, int stride = 24, int fpType = 0, bool npcOnly = false)
        {
            foreach (var hit in AttackResults.Read(b, cntOff, firstOff, stride))
            {
                if (!hit.TargetsNpc(npcOnly)) continue;
                int id = hit.Id, dmg = hit.Damage, hp = hit.Hp;
                var go = ents != null ? ents.NpcById(id) : null; if (go == null) continue;
                var info = go.GetComponent<NpcInfo>();
                if (attacker < 0 && info != null) CombatAudit.Result(info, hit, fpType != 0 ? "nano" : (firstOff == 24 ? "explosion" : "weapon"), ents.Self != null ? ents.Self.transform.position : Vector3.zero);
                if (info != null) { info.Hp = Mathf.Max(0, hp); if (hp > info.MaxHp) info.MaxHp = hp; }
                var anim = go.GetComponent<NpcAnimator>();
                if (anim != null) { if (hp <= 0) anim.Die(attacker < 0 ? (ents != null && ents.Self != null ? ents.Self.transform : null) : ents.TransformOfPlayer(attacker), info != null ? info.Radius : 0.5f, info != null ? info.Height : 1.5f); else anim.Wound(); }
                // FloatingPoint (dueño 1 = monstruo): crítico = iHitFlag & 2; protegido => -2
                int fpt = fpType; if (fpt == 0 && stride == 24 && (hit.HitFlag & 2) == 2) fpt = FloatingPoints.Critical;
                int prot = hit.Protected; int fv = prot != 0 ? -2 : dmg;
                if (fpt == FloatingPoints.Critical && prot == 0 && dmg > 0) Sfx.PlayAt("sound/critical_hit.wav", go.transform.position);
                FloatingPoints.Add(go.transform.position + Vector3.up * (info != null ? info.Height : 1.8f), 1, fpt, fv);
                if (attacker < 0 && info != null)
                    ChatHud.AddEventString(7, hp <= 0 ? "You have defeated " + info.Name + "." : prot != 0 ? "The " + info.Name + " protected itself from your attack." : dmg <= 0 ? "The " + info.Name + " blocked your attack." : "You wound the " + info.Name + " for " + dmg + " points of damage.");
                if (logN++ < 6) Debug.Log("[atk] resultado npc=" + id + " daño=" + dmg + " hp=" + hp);
            }
        }


        /// sP_FE2CL_NPC_SKILL_HIT {iNPC_ID, short iSkillID@4, v1..v3, eST@20, cnt@24}+resultados (20/32 B) y SKILL_CORRUPTION_HIT {iNPC_ID, short skill@4, short style@6, v1..v3, cnt@20}+sCAttackResult(40 B).
        void NpcSkillMessages(byte[] b, bool corr)
        {
            try
            {
                if (ents == null) return;
                NpcSkillPresentation(b, false, corr);
                int npcId = I(b, 0), skill = BitConverter.ToInt16(b, 4), style = corr ? BitConverter.ToInt16(b, 6) : 0;
                int est = corr ? 0 : I(b, 20), cnt = corr ? I(b, 20) : I(b, 24), first = corr ? 24 : 28;
                if (!corr && est == 30) first += 16;
                int stride = corr ? 40 : SkillResults.Stride(est);
                if (stride <= 0 || cnt < 0 || cnt > (b.Length - first) / stride) return;
                string nname = null; var ng = ents.NpcById(npcId); if (ng != null) { var ni = ng.GetComponent<NpcInfo>(); if (ni != null) nname = ni.Name; }
                for (int i = 0; i < cnt; i++)
                {
                    int o = first + stride * i; if (o + stride > b.Length) break;
                    int ct = I(b, o), id = I(b, o + 4); if (ct != 1) continue;
                    if (!corr && !SkillResults.Damage(est)) continue;
                    int dmg = I(b, o + 12), hp = I(b, o + 16); bool prot = I(b, o + 8) != 0; bool self = id == ents.SelfId;
                    var tt = ents.TransformOfPlayer(id);
                    if (tt != null && !(est != 0 && est != 1 && est != 3 && est != 4 && est != 5 && est != 8)) FloatingPoints.AddOn(tt.gameObject, 0, FloatingPoints.SkillDamage, prot ? -2 : dmg, 1.8f);
                    if (!self || nname == null) continue;
                    if (gc != null) gc.EnterCombat();
                    if (corr) ChatHud.AddEventString(6, "The " + nname + " unleashed a mighty <" + (style == 0 ? "Corruption" : "Eruption") + "> attack.");
                    else if (skill > 0) { string sn = SkillName(skill); if (!string.IsNullOrEmpty(sn)) ChatHud.AddEventString(6, "The " + nname + " attack you with " + sn + "."); }
                    if (hp <= 0) ChatHud.AddEventString(6, "You have been defeated by " + nname);
                    else if (dmg > 0) ChatHud.AddEventString(6, "The " + nname + " wounds you for " + dmg + " points of damage.");
                }
            }
            catch (Exception e) { Debug.LogWarning("[atk] NPC skill: " + e.Message); }
        }
        /// TableContainer.GetTableData(12,3,id): nombre de la skill.
        public string SkillName(int id)
        {
            var st = tables != null ? tables.m_pSkillTable : null; if (st == null || st.m_pSkillStringData == null || id < 0 || id >= st.m_pSkillStringData.Length || st.m_pSkillStringData[id] == null) return null;
            return st.m_pSkillStringData[id].m_strName;
        }

        // ---- objetivo y ataque ----
        float nextBoostInput;
        public static bool AttackRequested(bool uiBlocked, bool pointerOverUi, bool mouseHeld, bool actionHeld) =>
            !uiBlocked && (actionHeld || mouseHeld && !pointerOverUi);
        static readonly RaycastHit[] sightHits = new RaycastHit[32];
        public static bool HasLineOfSight(Vector3 origin, Vector3 target, float radius, Transform owner, Transform victim)
        {
            var direction = target - origin; float length = direction.magnitude - radius;
            if (length <= 0) return true;
            // NpcContainer.hitMask excludes avatar/NPC/effect layers in Legacy.
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, sightHits, length, ~18176 & ~(1 << 30), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (owner != null && hit.transform.IsChildOf(owner) || victim != null && hit.transform.IsChildOf(victim)) continue;
                return false;
            }
            return true;
        }
        public static int WeaponBullet(ItemElement item, HudData data, int fallback = 1) =>
            item != null && data != null && data.UseWeaponBattery && data.BatteryWpn > 0 && item.m_iEffect2 > 0 ? item.m_iEffect2 : item != null && item.m_iEffect1 > 0 ? item.m_iEffect1 : fallback;
        // Aim from the camera rendered this frame (GameClient updates it in LateUpdate).
        void LateUpdate()
        {
            while (q.TryDequeue(out var a)) a();
            RefreshEquipment();
            for (int i = pops.Count - 1; i >= 0; i--) if (Time.time - pops[i].T0 > 1.2f) pops.RemoveAt(i);
            if (!Enabled || WorldBootstrap.Travelling || ents == null || ents.Self == null || Camera.main == null) { NpcUI.MobTarget = null; return; }
            if (gc == null) { gc = FindObjectOfType<GameClient>(); if (gc != null) gc.AniStyle = aniStyle; }
            var self = ents.Self.transform; Vector3 pos = self.position + Vector3.up * 0.8f;
            if (gc != null) gc.AimValid = (targetMode == 5 || targetMode == 6) && !VehicleRide.SelfRiding;   // cnOwnAvatarStatus: iVehicle == 0 para los modos de apuntado
            var fw = Camera.main.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 1e-4f) fw = self.forward; fw.Normalize();
            view.Clear(); atk.Clear();
            foreach (var go in ents.NpcObjects)
            {
                if (go == null) continue; var n = go.GetComponent<NpcInfo>(); if (n == null || !n.IsMob || n.Hp <= 0) continue;
                float h = Mathf.Max(0.5f, n.Height), r = Mathf.Max(0.2f, n.Radius); var p2 = go.transform.position;
                if (p2.y < pos.y && pos.y < p2.y + h) p2.y = pos.y; else if (p2.y > pos.y) { } else p2.y += h;
                var d = p2 - pos; float dist = d.magnitude; if (dist < 1e-4f) { atk.Add(n); view.Add(n); continue; }
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d / dist, fw), -1f, 1f)) * Mathf.Rad2Deg;
                n.SortDist = dist;
                if (dist < r) { view.Add(n); atk.Add(n); continue; }
                bool inView = ang <= ViewAngleDeg && dist < Mathf.Max(r, h * 0.5f) + ViewDistance;
                bool inRange = ang <= angle && dist < Mathf.Max(r, h * 0.5f) + range * 0.01f;
                if ((inView || inRange) && HasLineOfSight(pos, p2, r, self, go.transform)) { if (inView) view.Add(n); if (inRange) atk.Add(n); }
            }
            Comparison<NpcInfo> cmp = (x, y) => x.SortDist.CompareTo(y.SortDist); view.Sort(cmp); atk.Sort(cmp);
            if (atk.Count > maxTargets) atk.RemoveRange(maxTargets, atk.Count - maxTargets);
            NpcUI.MobTarget = atk.Count > 0 ? atk[0] : (view.Count > 0 ? view[0] : null);
            bool blocked = PlayerLife.Active || GetComponent<PlayerHud>()?.Data.Hp <= 0 || ChatHud.Typing || NpcUI.Active || WelcomePopup.Active || GameMenu.Blocking;
            bool pointerOverUi = ChatHud.MouseOver || MenuChatPanel.MouseOver || GameMenu.MouseOverHud || FirstUse.MouseOver;
            if (!blocked && NpcUI.Sender != null && Time.unscaledTime >= nextBoostInput)
            {
                var hud = GetComponent<PlayerHud>(); var data = hud != null ? hud.Data : null;
                if (data != null && data.BatteryWpn > 0 && ControlMap.Down(ControlMap.K.WpnCharge, false))
                { data.UseWeaponBattery = !data.UseWeaponBattery; NpcUI.Sender(ClientPackets.ToggleWeaponBoost(ents.SelfId)); nextBoostInput = Time.unscaledTime + .3f; Sfx.Ui("Tab_Click01"); }
                else if (data != null && data.BatteryNano > 0 && data.ActiveNanoSlot >= 0 && ControlMap.Down(ControlMap.K.NanoCharge, false))
                { NpcUI.Sender(ClientPackets.ChargeNano(ents.SelfId)); nextBoostInput = Time.unscaledTime + .3f; }
            }
            if (!blocked && !VehicleRide.SelfRiding && ControlMap.Down(ControlMap.K.WpnChange, false)) SwitchWeapon();   // WeaponChange: iVehicle > 0 -> no
            if (gc != null && NpcUI.Sender != null && ents.SelfId != 0) { bool c = Time.time < gc.CombatUntil; if (c != inCombat) { inCombat = c; NpcUI.Sender(c ? ClientPackets.CombatBegin(ents.SelfId) : ClientPackets.CombatEnd(ents.SelfId)); } }
            bool mouseHeld = Input.touchCount == 0 && !TouchControls.Active && (Input.GetMouseButton(0) || ControlMap.HeldPointer(ControlMap.K.Fire1));
            bool want = AttackRequested(blocked, pointerOverUi, mouseHeld, ControlMap.HeldNonPointer(ControlMap.K.Fire1));
            if (want && VehicleRide.SelfRiding) { VehicleRide.HopOffFromAction(); return; }
            if (ZiplineRide.Intercept(want)) want = false;   // cnAvatarAttack: con un EpZiplineTrigger a mano y sin objetivo, atacar = usar la tirolina
            if (PlayerTarget.Intercept(want)) want = false;   // cnAvatarAttack.Update: con jugador objetivo (y sin combate) atacar = TalkUser (menu de jugador)   // cnAvatarAttack.Update: atacar con iVehicle > 0 = REQ_PC_VEHICLE_OFF (FirstUse 70), no ataca
            if (targetMode == 6) { GrenadeUpdate(want, fw); return; }
            if (grenArc) { grenArc = false; if (arc != null) arc.enabled = false; }
            if (want && Time.time >= nextAttack) { nextAttack = Time.time + AttackInterval; Attack(fw); }
        }

        // ---- granadas (modo 6): cnAvatarAttack: al pulsar ataque se activa la parábola (grenadeArcActive); al soltar, FireGrenade ----
        LineRenderer arc; bool grenArc; Texture2D arcTex;
        static Transform FindDeepT(Transform r, string name)
        {
            if (r == null) return null; if (string.Equals(r.name, name, StringComparison.OrdinalIgnoreCase)) return r;
            for (int i = 0; i < r.childCount; i++) { var f = FindDeepT(r.GetChild(i), name); if (f != null) return f; }
            return null;
        }
        void EnsureArc()
        {
            if (arc != null) return;
            var go = new GameObject("GrenadeArc"); arc = go.AddComponent<LineRenderer>();
            var sh = Shader.Find("Sprites/Default"); if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh); arcTex = OpenFusion.Screens.UiKit.Tex("aim_grenade_line"); if (arcTex != null) { arcTex.wrapMode = TextureWrapMode.Repeat; m.mainTexture = arcTex; }
            arc.material = m; arc.textureMode = LineTextureMode.Tile; arc.widthMultiplier = 0.075f; arc.numCapVertices = 0; arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; arc.receiveShadows = false;
            float aspect = arcTex != null && arcTex.height > 0 ? (float)arcTex.width / arcTex.height : 1f; arc.textureScale = new Vector2(1f / (0.075f * Mathf.Max(0.1f, aspect)), 1f);
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.095f, 0f), new GradientAlphaKey(1f, 1f) }); arc.colorGradient = g;
            arc.enabled = false;
        }
        void GrenadeUpdate(bool want, Vector3 fw)
        {
            var self = ents.Self.transform; var cam = Camera.main;
            if (!grenArc)
            {
                if (!(want && Time.time >= nextAttack && rockets != null && rockets.CanShoot)) return;
                grenArc = true;
            }
            EnsureArc();
            var mz = FindDeepT(self, "Gtag01"); var org = (mz != null ? mz.position : self.position + Vector3.up * 1.2f) + Vector3.up * 0.6f;
            var dir = cam.transform.forward;
            if (gc != null && gc.AimActive)
            {   // en modo apuntado el destino es el punto bajo la mira (si está a más de 2 m)
                var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)); var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore); float best = float.MaxValue; var hp = Vector3.zero;
                foreach (var h in hits) { if (h.transform.IsChildOf(self) || h.distance >= best) continue; best = h.distance; hp = h.point; }
                if (best < float.MaxValue && Vector3.Distance(org, hp) > 2f) dir = hp - org;
            }
            dir.Normalize(); var flat = new Vector3(dir.x, 0f, dir.z); if (flat.sqrMagnitude > 1e-6f) { flat.Normalize(); self.rotation = Quaternion.LookRotation(flat); fw = flat; }
            if (!want)
            {   // soltar: FireGrenade
                grenArc = false; arc.enabled = false; nextAttack = Time.time + AttackInterval;
                if (rockets != null && rockets.FireGrenade(dir, range) && gc != null) gc.PlayAttack(Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg, AttackInterval, false);
                return;
            }
            float rangeM = range * 0.01f, up = (weaponIt != null ? weaponIt.m_iDeliverTime : 0) * 0.1f * 5f, grav = 13f, dur = (weaponIt != null ? weaponIt.m_iDurationTime : 30) * 0.1f; const int N = 50;
            var v0 = dir * rangeM; v0.y += up; var pts = new List<Vector3>(N); var prev = org;
            for (int i = 0; i < N; i++)
            {
                float t = (float)i / (N - 1) * dur; var pt = org + v0 * t; pt.y -= 0.5f * grav * t * t; var d = pt - prev; float mag = d.magnitude;
                RaycastHit hit; if (mag > 0f && Physics.Raycast(prev, d / mag, out hit, mag + 0.2f, ~((1 << 2) | (1 << 30)), QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(self)) { for (int j = i; j < N; j++) pts.Add(hit.point); break; }
                pts.Add(pt); prev = pt;
            }
            arc.positionCount = pts.Count; arc.SetPositions(pts.ToArray()); arc.enabled = true;
        }
        void Attack(Vector3 fw)
        {
            // cnAvatarAttack.AttackTarget: bAttackDelay (= ataque continuado, rota attack1..N) sólo si hay objetivos y no es cohete/granada (modo 5/6)
            bool cont = atk.Count > 0 && targetMode != 5 && targetMode != 6;
            if (targetMode == 5)
            {
                Vector3? aimDir = null;
                if (gc != null && gc.AimActive && ents.Self != null)
                {   // cnAvatarAttack.AttackTarget (modo apuntado): rayo por el centro de pantalla; si impacta a > 2 m del origen (jugador + 0,8) se apunta al impacto, si no, adelante de la cámara
                    var cam = Camera.main; var org = ents.Self.transform.position + Vector3.up * 0.8f; var dir = cam.transform.forward; var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    var hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore); float best = float.MaxValue; var hp = Vector3.zero;
                    foreach (var h in hits) { if (h.transform.IsChildOf(ents.Self.transform) || h.distance >= best) continue; best = h.distance; hp = h.point; }
                    if (best < float.MaxValue && Vector3.Distance(org, hp) > 2f) dir = hp - org;
                    aimDir = dir.normalized; var fl = new Vector3(dir.x, 0f, dir.z); if (fl.sqrMagnitude > 1e-6f) fw = fl.normalized;
                }
                if (rockets != null && rockets.Fire(fw, range, aimDir)) { if (gc != null) gc.PlayAttack(Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg, AttackInterval, false); }
                return;
            }
            if (gc != null) gc.PlayAttack(Mathf.Atan2(fw.x, fw.z) * Mathf.Rad2Deg, AttackInterval, cont);
            var fx = BulletFx.Instance; var self = ents.Self != null ? ents.Self.transform : null;
            var playerHud = GetComponent<PlayerHud>(); int bullet = WeaponBullet(weaponIt, playerHud != null ? playerHud.Data : null, bulletType);
            if (fx != null && self != null)
            {   // AttackTarget: MakeBullet por objetivo; sin objetivo, hacia delante (alcance) y 1 m arriba
                if (atk.Count == 0) fx.Fire(bullet, self, null, 0f, self.position + fw * (range * 0.01f) + Vector3.up * 1f);
                else foreach (var n in atk) fx.Fire(bullet, self, n.transform, n.Height, Vector3.zero);
            }
            if (atk.Count == 0 || NpcUI.Sender == null) return;
            var ids = new int[atk.Count]; for (int i = 0; i < ids.Length; i++) ids[i] = atk[i].Id;
            NpcUI.Sender(ClientPackets.AttackNpcs(ids));
            if (logN < 3) Debug.Log("[atk] ataque a " + string.Join(",", Array.ConvertAll(ids, x => x.ToString())));
        }

        // ---- mira (CnGuiCharacter_info: aim_rocket_point en el centro y, en modo 5, los dos corchetes con separación según la distancia al objetivo bajo la mira) ----
        Texture2D tPoint, tBracket; float lastGap = 24f;
        void DrawCrosshair(float s)
        {
            if (tPoint == null) tPoint = OpenFusion.Screens.UiKit.Tex("aim_rocket_point"); if (tBracket == null) tBracket = OpenFusion.Screens.UiKit.Tex("aim_rocket_leftbracket");
            var old = GUI.matrix; var oc = GUI.color; float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f; const float k = 0.75f;
            GUIUtility.ScaleAroundPivot(new Vector2(s, s), new Vector2(cx, cy));
            if (tPoint != null) GUI.DrawTexture(new Rect(cx - 14f * k, cy - 15f * k, tPoint.width * k, tPoint.height * k), tPoint);
            if (targetMode == 5 && tBracket != null)
            {
                float lockD = 28.1f, td = lockD; var ray = Camera.main.ScreenPointToRay(new Vector3(cx, cy, 0f));
                foreach (var go in ents.NpcObjects)
                {
                    if (go == null) continue; var n = go.GetComponent<NpcInfo>(); if (n == null || !n.IsMob || n.Hp <= 0) continue;
                    var c = go.transform.position + Vector3.up * n.Height * 0.5f; float rr = Mathf.Max(n.Radius, n.Height * 0.5f); var d = c - ray.origin; float t = Vector3.Dot(d, ray.direction);
                    if (t < 0f || t > td) continue; if ((d - ray.direction * t).sqrMagnitude < rr * rr) td = t;
                }
                lastGap = Mathf.Lerp(lastGap, td < lockD ? Mathf.Lerp(24f, 3f, Mathf.Clamp01(td / lockD)) : 24f, 0.35f);
                if (Time.time < nextAttack) GUI.color = new Color(1f, 1f, 1f, 0.5f);   // GameCondition.IsCoolTime(2): reticula semitransparente en enfriamiento
                float bw = tBracket.width * k, bh = tBracket.height * k;
                GUI.DrawTexture(new Rect(cx - bw - lastGap, cy - 32f * k, bw, bh), tBracket);
                GUI.DrawTexture(new Rect(cx + bw + lastGap, cy - 32f * k, -bw, bh), tBracket);
            }
            GUI.color = oc; GUI.matrix = old;
        }

        // ---- HUD: números de daño y botón de ataque táctil ----
        GUIStyle popStyle; Texture2D tIcon0, tIcon1, combatTex;
        void OnGUI()
        {
            if (GameMenu.HudHidden) return;
            if (ents == null || Camera.main == null) return;
            float s = GameOptions.UiScale;
            // GameFrame.OnGUI: en combate, textura 'combat' a pantalla completa con alfa pulsante (1 Hz), GUI.depth = 2
            if (gc != null && Time.time < gc.CombatUntil && Event.current.type == EventType.Repaint)
            {
                if (combatTex == null) combatTex = Resources.Load<Texture2D>("OpenFusion/UI/Textures/combat");
                if (combatTex != null) { var oc0 = GUI.color; int od0 = GUI.depth; GUI.depth = 2; GUI.color = new Color(1f, 1f, 1f, (Mathf.Sin(Time.realtimeSinceStartup * Mathf.PI * 2f) + 1f) * 0.5f); GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), combatTex, ScaleMode.StretchToFill, true); GUI.color = oc0; GUI.depth = od0; }
            }
            if (gc != null && gc.AimActive && Event.current.type == EventType.Repaint && !NpcUI.Active && !ChatHud.Typing) DrawCrosshair(s);
            if (pops.Count > 0)
            {
                if (popStyle == null) { popStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; }
                popStyle.fontSize = Mathf.RoundToInt(20 * s); var old = GUI.color;
                foreach (var p in pops)
                {
                    float t = (Time.time - p.T0) / 1.2f; var sp = Camera.main.WorldToScreenPoint(p.Pos + Vector3.up * (0.2f + t * 1.2f)); if (sp.z <= 0.1f) continue;
                    var r = new Rect(sp.x - 80f, Screen.height - sp.y - 20f, 160f, 40f); float a = 1f - t * t;
                    GUI.color = new Color(0f, 0f, 0f, a); GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), p.Text, popStyle);
                    GUI.color = new Color(p.Col.r, p.Col.g, p.Col.b, a); GUI.Label(r, p.Text, popStyle);
                }
                GUI.color = old;
            }
            // PrintName.SetCombatIcon: 0 (target_icon1a) al primer objetivo de ataque, 1 (target_icon1b) a los demás; no con cohetes/granadas (modo 5/6). Alfa pulsante, a media altura del NPC, escala de UI.
            if (atk.Count > 0 && targetMode != 5 && targetMode != 6)
            {
                if (tIcon0 == null) tIcon0 = Resources.Load<Texture2D>("OpenFusion/UI/Textures/target_icon1a"); if (tIcon1 == null) tIcon1 = Resources.Load<Texture2D>("OpenFusion/UI/Textures/target_icon1b");
                var oc = GUI.color; float pulse = 0.5f + (Mathf.Sin(Time.time * 5f) + 1f) * 0.25f; GUI.color = new Color(pulse, pulse, pulse, pulse); int od = GUI.depth; GUI.depth = 900;
                for (int i = 0; i < atk.Count; i++)
                {
                    var tx = i == 0 ? tIcon0 : tIcon1; if (tx == null || atk[i] == null) continue;
                    var sp = Camera.main.WorldToScreenPoint(atk[i].transform.position + Vector3.up * atk[i].Height * 0.5f); if (sp.z <= 0.1f) continue;
                    float w = tx.width * s, hh = tx.height * s; GUI.DrawTexture(new Rect(sp.x - w / 2f, Screen.height - sp.y - hh / 2f, w, hh), tx, ScaleMode.StretchToFill, true);
                }
                GUI.color = oc; GUI.depth = od;
            }

        }
    }
}
