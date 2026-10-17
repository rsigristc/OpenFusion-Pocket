// Nano en el mundo + skills de nano (NanoContainer / NanoMoveController / NanoAnimation / cnAvatarAttack.NanoSkillUse / GameFrame.SkillResultDistributor / MakeSkillBullet).
//  - Nano activo de cada jugador: modelo "nano/<malla>.kfm" que flota a 0,7 m del jugador (y +1,12), efecto de invocación ES[estilo+527], de retirada ES[10], animaciones
//    stand1..3 / discharge / skill1..3 / withdraw (NanoAnimation) y movimiento de ForceUpdate (círculo, pulso vertical, giro).
//  - Uso de skill (tecla X = Key.Fire2, o UseSkill() desde un botón): sólo skills activas (BatteryDrainType 1) de tipo objetivo (EffectType 1). Construye la lista de objetivos como el
//    original (1 = objetivo, 2 = uno mismo, 3 = cono, 5 = área propia, 6 = área alrededor del objetivo), respeta el cooldown del slot y envía REQ_NANO_SKILL_USE.
//  - NANO_SKILL_USE_SUCC (propio) / NANO_SKILL_USE (otros): anima al nano, lanza la bala del skill (SkillElement.m_iTargetEffect) hacia cada objetivo y aplica los resultados
//    (daño en NPC, curación propia). Skills de cohete (EffectType 2/3) y movimiento (tipos 27/28/29/40) no se portan todavía: sólo se envía la petición / se registra.
// SIN COMPILAR EN UNITY.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class NanoSystem : MonoBehaviour
    {
        sealed class Nn
        {
            public int PcId, NanoId, Skill, SkillAni, Style, Mode; public GameObject Root, Model; public Animation Anim; public Transform User; public bool Own, Leaving, Dead;
            public float Angle, ModeEnd, PatternAt, RotSpeed, TurnSpeed, PulseH, PulseHz, SpawnAt; public int Pattern; public bool Circle = true, Vertical = true, ForceSound; public int Tries; public NanoElement Row;
        }
        const int Stand = 1, Skill1 = 11, Skill2 = 12, Skill3 = 13, Discharge = 3, Withdraw = 10;
        readonly Dictionary<int, Nn> byPc = new Dictionary<int, Nn>();
        readonly Dictionary<Transform, Nn> byModel = new Dictionary<Transform, Nn>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; PlayerHud hud; NanoWheelHud wheel; CombatSystem combat; ModelFactory models; ModelCatalog catalog;
        float nextSync, lastUse; int logN; sealed class Pend { public int Nano, Skill; public bool Passive; public float At; }
        readonly Dictionary<int, Pend> pending = new Dictionary<int, Pend>();
        readonly List<NpcInfo> tmp = new List<NpcInfo>();

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, PlayerHud h, NanoWheelHud w, CombatSystem c, ModelFactory m, ModelCatalog cat)
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.PcEnter -= OnPcEnter; }
            ents = e; disp = d; tables = t; hud = h; wheel = w; combat = c; models = m; catalog = cat;
            if (disp != null) { disp.Unhandled += OnPacket; disp.PcEnter += OnPcEnter; }
            if (AnimEventSink.Muted == null) AnimEventSink.Muted = IsMuted;
        }
        void OnDestroy() { if (disp != null) { disp.Unhandled -= OnPacket; disp.PcEnter -= OnPcEnter; } }

        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static short S(byte[] b, int o) { return BitConverter.ToInt16(b, o); }

        // ------------------------------------------------------------------ red (hilo de red -> cola)

        void OnPcEnter(PcAppearance a) { if (a == null) return; int id = a.Id, nano = a.NanoId; q.Enqueue(() => { if (id != ents.SelfId && nano > 0) SetNano(id, nano, 0, false); }); }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_NANO_ACTIVE && b.Length >= 20)
            {   // iPC_ID@0, sNano{iID@4,iSkillID@6,iStamina@8}, iConditionBitFlag@12, eCSTB___Add@16
                var body = b; q.Enqueue(() =>
                {
                    int pc = I(body, 0); if (pc == ents.SelfId) return; int id = S(body, 4), sk = S(body, 6);
                    if (id == -1 || id == 0) Remove(pc, id == 0); else SetNano(pc, id, sk, I(body, 16) != 0);
                });
            }
            else if ((type == PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC || type == PacketIds.P_FE2CL_NANO_SKILL_USE) && b.Length >= 36)
            {
                bool own = type == PacketIds.P_FE2CL_NANO_SKILL_USE_SUCC; var body = b;
                int est = I(b, 28), cnt = I(b, 32);
                if (cnt > 0) PrefillKills(b, est, cnt, own ? -1 : I(b, 0));
                if (ents != null) ents.Enqueue(() => OnSkill(body, own)); else q.Enqueue(() => OnSkill(body, own));
            }
        }

        /// NPC muertos por skill: se anotan antes de que llegue NPC_EXIT (igual que con los ataques).
        static void PrefillKills(byte[] b, int est, int cnt, int attacker)
        {
            int stride = Stride(est); if (stride != 20 && stride != 32) return; int first = 36 + (est == 30 ? 16 : 0);
            for (int i = 0; i < cnt; i++) { int o = first + stride * i; if (o + 20 > b.Length) break; int ct = I(b, o); if ((ct == 2 || ct == 4) && I(b, o + 16) <= 0) CombatSystem.Killed[I(b, o + 4)] = attacker; }
        }

        /// Tamaño de cada resultado según eST (GameFrame.SkillResultDistributor).
        static int Stride(int est)
        {
            switch (est)
            {
                case 1: case 30: return 20;
                case 3: case 4: case 5: case 8: return 32;
                case 2: case 34: case 6: return 16;
                case 10: case 11: case 12: case 14: case 15: case 16: case 17: case 18: case 19: case 20: case 23: case 25: case 31: case 32: case 33: case 35: return 16;
                case 27: return 24;
            }
            return 0;
        }

        // ------------------------------------------------------------------ nano en el mundo

        NanoElement RowOf(int id)
        {
            var nt = tables != null ? tables.m_pNanoTable : null; var arr = nt != null ? nt.m_pNanoData : null; if (arr == null) return null;
            if (id > 0 && id < arr.Length && arr[id] != null && arr[id].m_iNanoNumber == id) return arr[id];
            foreach (var e in arr) if (e != null && e.m_iNanoNumber == id) return e; return null;
        }

        /// NanoContainer.AddNano(id>0): crea el nano (destruye el anterior).
        void SetNano(int pc, int nanoId, int skillId, bool passive)
        {
            Nn cur; if (byPc.TryGetValue(pc, out cur) && !cur.Leaving && cur.NanoId == nanoId) { cur.Skill = skillId; PickSkillAni(cur); return; }
            if (cur != null) { Kill(cur, false); Dispose(cur); }   // sin esto el Nn anterior dejaba de estar en byPc y su modelo se quedaba en el mundo
            var user = ents != null ? ents.TransformOfPlayer(pc) : null;
            if (user == null) { pending[pc] = new Pend { Nano = nanoId, Skill = skillId, Passive = passive, At = Time.time }; return; }
            pending.Remove(pc);
            var n = new Nn { PcId = pc, NanoId = nanoId, Skill = skillId, Own = pc == ents.SelfId, User = user, Row = RowOf(nanoId), SpawnAt = Time.time };
            n.Style = n.Row != null ? n.Row.m_iStyle : 0; n.Angle = user.rotation.eulerAngles.y; NewPattern(n);
            byPc[pc] = n; PickSkillAni(n);
            if (passive) n.SkillAni = Skill1;
            Place(n, true);
            if (logN++ < 8) Debug.Log("[nano] pc " + pc + " invoca nano " + nanoId + " estilo=" + n.Style + " skill=" + skillId + " animación de skill=" + n.SkillAni);
        }

        /// NanoContainer.SetPassiveSkill: la animación de skill es skill1/2/3 según la "tune" cuyo m_iSkillID coincide con la skill del nano.
        void PickSkillAni(Nn n)
        {
            n.SkillAni = 0; var nt = tables != null ? tables.m_pNanoTable : null; if (n.Row == null || nt == null || nt.m_pNanoTuneData == null || n.Row.m_iTune == null) return;
            for (int i = 0; i < 3 && i < n.Row.m_iTune.Length; i++)
            {
                int t = n.Row.m_iTune[i]; if (t <= 0 || t >= nt.m_pNanoTuneData.Length || nt.m_pNanoTuneData[t] == null) continue;
                if (nt.m_pNanoTuneData[t].m_iSkillID == n.Skill) n.SkillAni = Skill1 + i;
            }
        }

        void NewPattern(Nn n)
        {
            n.PatternAt = Time.time; int a = 1; switch (UnityEngine.Random.Range(0, 10)) { case 0: a = 2; break; case 1: a = 4; break; }
            int b = 8; if (n.Vertical) switch (UnityEngine.Random.Range(0, 3)) { case 0: b = 16; break; case 1: b = 32; break; }
            int c = 64; switch (UnityEngine.Random.Range(0, 10)) { case 0: c = 256; break; case 1: c = 128; break; }
            n.Pattern = a | b | c; n.RotSpeed = UnityEngine.Random.Range(1f, 2f) * 57.29578f; n.TurnSpeed = UnityEngine.Random.Range(0.5f, 2f) * 57.29578f;
            n.PulseH = UnityEngine.Random.Range(0.1f, 0.3f); n.PulseHz = UnityEngine.Random.Range(0.5f, 1.5f);
        }

        static Vector3 Offset(Nn n, bool rocket)
        {
            Vector3 v = rocket ? new Vector3(0.45f, 0f, -0.45f) : new Vector3(0.7f, 0f, 0f);
            return Quaternion.Euler(0f, n.Angle, 0f) * v;
        }

        void Place(Nn n, bool first)
        {
            if (n.Root == null) { n.Root = new GameObject("nano#" + n.NanoId + " pc" + n.PcId); }
            var p = n.User.position + Offset(n, false); p.y += 1.12f; n.Root.transform.position = p; n.Root.transform.rotation = n.User.rotation;
            if (first) { var fx = BulletFx.Instance; if (fx != null) fx.Spawn(n.Style + 527, p, n.Root.transform.rotation, 1f, null, 3f); }
        }

        bool BuildModel(Nn n)
        {
            if (n.Model != null) return true; if (models == null || catalog == null) return false;
            var m = catalog.Nano(n.NanoId); if (m == null || m.Model == null) { n.Tries = 99; return false; }
            var go = models.Instantiate(m, n.Root.transform); if (go == null) return false;   // el bundle del nano puede llegar tarde
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            var main = m.Tex1 != null ? models.Texture(m.Tex1) : null; var sub = m.Tex2 != null ? models.Texture(m.Tex2) : null;
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
            {
                rd.enabled = true;
                foreach (var mat in rd.materials) { if (mat.name.Contains("sub") && sub != null) mat.SetTexture("_MainTex", sub); else if (mat.name.Contains("main") && main != null) mat.SetTexture("_MainTex", main); }
            }
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; n.Model = go;
            n.Anim = go.GetComponent<Animation>(); if (n.Anim == null) n.Anim = go.GetComponentInChildren<Animation>(true);
            if (n.Anim != null)
            {
                n.Anim.enabled = true; n.Anim.cullingType = AnimationCullingType.AlwaysAnimate; foreach (AnimationState st in n.Anim) st.wrapMode = WrapMode.Loop;   // NanoAnimation.SetModel
                var sink = n.Anim.GetComponent<AnimEventSink>(); if (sink != null) sink.IsNano = true; byModel[n.Anim.transform] = n;
            }
            if (Has(n, "call")) Play(n, "call", 2, 0.1f); else SetStand(n);   // NanoAnimation.Call: la voz de invocación va en el clip "call"
            return true;
        }

        /// <summary>NanoContainer.SetEmote: el nano acompana el emote del avatar con el clip de AnimationTable.m_pNanoData[m_iNanoAnimation] (al acabar vuelve a reposo).</summary>
        public void PlayEmote(int pcId, string clip)
        {
            if (string.IsNullOrEmpty(clip)) return; Nn n; if (!byPc.TryGetValue(pcId, out n) || n == null || n.Leaving || n.Model == null || n.Anim == null) return;
            string c = clip; if (n.Anim[c] == null) { c = null; foreach (AnimationState st in n.Anim) if (string.Equals(st.name, clip, StringComparison.OrdinalIgnoreCase)) { c = st.name; break; } }
            if (c != null) Play(n, c, 2, 0.1f);
        }
        bool Has(Nn n, string c) { return n.Anim != null && n.Anim[c] != null; }
        void Play(Nn n, string clip, int mode, float fade)
        {
            n.Mode = mode; if (!Has(n, clip)) { n.ModeEnd = Time.time + 1f; return; }
            n.Anim.CrossFade(clip, fade); n.ModeEnd = Time.time + Mathf.Max(0.2f, n.Anim[clip].length);
        }

        /// NanoAnimation.SetStandMotion: discharge con < 20 % de estamina (propio), si no stand1..3.
        void SetStand(Nn n)
        {
            n.ForceSound = false; string c;
            if (n.Own && StaminaLow(n)) { c = "discharge"; n.ForceSound = true; if (!Has(n, c)) c = "stand1"; }
            else { c = "stand" + UnityEngine.Random.Range(1, 4); if (!Has(n, c)) c = "stand1"; }
            Play(n, c, Stand, 0.1f);
        }
        bool StaminaLow(Nn n)
        {
            if (hud == null || hud.Data == null || n.Row == null || n.Row.m_iNanoBattery1 <= 0) return false;
            int slot = hud.Data.ActiveNanoSlot; if (slot < 0 || slot > 2) return false;
            return (float)hud.Data.NanoStamina[slot] / n.Row.m_iNanoBattery1 < 0.2f;
        }

        /// NanoContainer.SkillAnimation -> NanoAnimation.Skill()
        void SkillAnim(Nn n)
        {
            if (n == null || n.Leaving || n.Model == null) return;
            if (n.SkillAni == Skill1) Play(n, "skill1", Skill1, 0.1f); else if (n.SkillAni == Skill2) Play(n, "skill2", Skill2, 0.1f); else if (n.SkillAni == Skill3) Play(n, "skill3", Skill3, 0.1f);
        }

        bool IsMuted(Transform t, string data)
        {
            Nn n; if (t == null || !byModel.TryGetValue(t, out n)) return false;
            if (n.Mode != Stand) return false; if (n.ForceSound) return false; return UnityEngine.Random.Range(0, 20) != 1;   // NanoAnimation.IsStand
        }

        /// AddNano(0): retirada con animación + efecto ES[10]; AddNano(-1): se oculta (también con ES[10]).
        void Remove(int pc, bool withdraw)
        {
            Nn n; pending.Remove(pc); if (!byPc.TryGetValue(pc, out n)) return;
            Kill(n, withdraw);
        }

        void Kill(Nn n, bool anim)
        {
            if (n.Leaving) return; n.Leaving = true;
            if (!anim && n.Root != null && tables != null) { try { var sx = Sfx.Instance; var row = RowOf(n.NanoId); if (sx != null && row != null) { var nm = tables.m_pNanoTable.m_pNanoStringData[row.m_iNanoName].m_strName; sx.At("sound/" + nm + "_NanDismiss0" + UnityEngine.Random.Range(1, 4) + ".wav", n.Root.transform.position, 1f, false, 0f, null, true); } } catch { } }   // AddNano(-1): voz de despedida
            var fx = BulletFx.Instance; if (fx != null && n.Root != null) fx.Spawn(10, n.Root.transform.position, n.Root.transform.rotation, 1f, null, 3f);
            if (anim && n.Model != null && Has(n, "withdraw")) { Play(n, "withdraw", Withdraw, 0.1f); n.ModeEnd = Time.time + Mathf.Min(2f, n.Anim["withdraw"].length); }
            else n.ModeEnd = Time.time;
            n.Dead = true;
        }

        void Dispose(Nn n)
        {
            if (n.Anim != null) byModel.Remove(n.Anim.transform);
            if (n.Root != null) UnityEngine.Object.Destroy(n.Root);
            Nn cur; if (byPc.TryGetValue(n.PcId, out cur) && cur == n) byPc.Remove(n.PcId);
        }

        // ------------------------------------------------------------------ actualización

        void Update()
        {
            Action a; while (q.TryDequeue(out a)) a();
            if (ents == null) return;
            // nano propio = slot activo del HUD (REP_NANO_ACTIVE_SUCC / REGEN / ENTER lo actualizan)
            if (Time.time >= nextSync && hud != null && hud.Data != null && ents.Self != null)
            {
                nextSync = Time.time + 0.2f; int slot = hud.Data.ActiveNanoSlot; int id = slot >= 0 && slot < 3 ? hud.Data.NanoSlots[slot] : 0;
                Nn cur; byPc.TryGetValue(ents.SelfId, out cur);
                if (id > 0) { int sk = wheel != null ? wheel.BankSkill(id) : 0; if (cur == null || cur.Leaving || cur.NanoId != id) SetNano(ents.SelfId, id, sk, false); else if (cur.Skill != sk) { cur.Skill = sk; PickSkillAni(cur); } }
                else if (cur != null && !cur.Leaving) Kill(cur, true);
            }
            var done = (List<Nn>)null;
            foreach (var kv in byPc)
            {
                var n = kv.Value; var u = ents.TransformOfPlayer(n.PcId); if (u != null) n.User = u;
                if (n.User == null && !n.Own) { (done ?? (done = new List<Nn>())).Add(n); continue; }
                if (n.Dead) { if (Time.time >= n.ModeEnd) (done ?? (done = new List<Nn>())).Add(n); else Follow(n); continue; }
                if (n.Model == null && Time.time - n.SpawnAt < 15f) { if (!BuildModel(n)) { Follow(n); continue; } }
                if (n.Model != null && Time.time >= n.ModeEnd) SetStand(n);   // "end" del clip
                Follow(n);
            }
            if (done != null) foreach (var n in done) Dispose(n);
            if (pending.Count > 0)
            {
                List<int> ready = null; foreach (var kv in pending) if (ents.TransformOfPlayer(kv.Key) != null || Time.time - kv.Value.At > 10f) (ready ?? (ready = new List<int>())).Add(kv.Key);
                if (ready != null) foreach (var pc in ready) { var p = pending[pc]; pending.Remove(pc); if (ents.TransformOfPlayer(pc) != null) SetNano(pc, p.Nano, p.Skill, p.Passive); }
            }
            HandleInput();
        }

        void HandleInput()
        {
            if (ChatHud.Typing || NpcUI.Active || WelcomePopup.Active || GameMenu.Blocking) return;
            if (ControlMap.Down(ControlMap.K.Fire2, false)) UseSkill();
        }

        /// NanoMoveController.ForceUpdate
        void Follow(Nn n)
        {
            if (n.User == null || n.Root == null) return; float dt = Time.deltaTime; var tr = n.Root.transform;
            bool rocket = n.Own && combat != null && (combat.TargetMode == 5 || combat.TargetMode == 6);
            bool still = n.Mode == Skill1 || n.Mode == Skill2 || n.Mode == Skill3 || n.Mode == 2 || n.Mode == Discharge || n.Mode == 4;
            if (!still && Time.time - n.PatternAt > 10f) { if (UnityEngine.Random.Range(0, 3) == 0) NewPattern(n); else n.PatternAt = Time.time; }
            if (still) { if (n.Mode >= Skill1 && n.Mode <= Skill3) tr.rotation = n.User.rotation; dt = 0f; }
            if ((n.Pattern & 0x80) == 128) tr.Rotate(0f, dt * n.TurnSpeed, 0f); else if ((n.Pattern & 0x100) == 256) tr.Rotate(0f, -dt * n.TurnSpeed, 0f);
            if ((n.Pattern & 2) == 2 && n.Circle) n.Angle += dt * n.RotSpeed;
            if ((n.Pattern & 4) == 4 && n.Circle) n.Angle -= dt * n.RotSpeed;
            if (!n.Circle) n.Angle = Mathf.LerpAngle(n.Angle, n.User.eulerAngles.y, Time.deltaTime * 5f);
            var v = Offset(n, rocket) + n.User.position; float h = 1.12f;
            if ((n.Pattern & 0x10) == 16) h += Mathf.Sin(n.PulseHz * n.PatternAt * Mathf.PI) * n.PulseH;
            if ((n.Pattern & 0x20) == 32) h += Mathf.Cos(n.PulseHz * n.PatternAt * Mathf.PI) * n.PulseH;
            v.y += h; var pos = tr.position;
            if (rocket)
            {
                n.Circle = false; n.Vertical = false; float mag = new Vector2(pos.x - v.x, pos.z - v.z).magnitude; float t = Mathf.Clamp01(1f - mag / 3f); float sp = Mathf.Lerp(3f, 18f, t);
                tr.position = new Vector3(Mathf.Lerp(pos.x, v.x, Time.deltaTime * sp), Mathf.Lerp(pos.y, v.y, Time.deltaTime * 6f), Mathf.Lerp(pos.z, v.z, Time.deltaTime * sp));
                if (!still) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(n.User.forward), Time.deltaTime * 4f);
            }
            else { n.Circle = true; n.Vertical = true; tr.position = (1f - Time.deltaTime * 4.25f) * pos + Time.deltaTime * 4.25f * v; }
            if (!rocket && !still && (n.Pattern & 0x180) == 0) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.Euler(0f, n.User.eulerAngles.y, 0f), Time.deltaTime * 3f);   // desvío deliberado del original (sólo gira con el patrón 128/256): el nano mira hacia donde mira el jugador
        }

        // ------------------------------------------------------------------ uso de skill (cnAvatarAttack.NanoSkillUse)

        /// Llamable desde un botón táctil.
        public void UseSkill()
        {
            if (hud == null || hud.Data == null || ents == null || ents.Self == null || wheel == null || NpcUI.Sender == null) return;
            var d = hud.Data; int slot = d.ActiveNanoSlot; if (slot < 0 || slot > 2) return;
            int nanoId = d.NanoSlots[slot]; int skillId = wheel.BankSkill(nanoId); if (skillId <= 0) return;
            var se = wheel.SkillOf(skillId); if (se == null || d.NanoStamina[slot] <= 0 || se.m_iBatteryDrainType != 1) return;
            if (se.m_iEffectType != 1) { if (logN++ < 8) Debug.Log("[skill] skill " + skillId + ": EffectType " + se.m_iEffectType + " no se usa desde el cliente (el original sólo envía NANO_SKILL_USE para tipo 1; los de cohete/área llegan como ojivas del servidor)"); return; }
            var self = ents.Self.transform; var targets = new List<NpcInfo>(); bool selfTarget = false; int arg1 = 0; var allies = new List<int>(); System.Action local = null;
            var tg = NpcUI.MobTarget; var fw = self.forward; var cam = Camera.main; if (cam != null) { fw = cam.transform.forward; fw.y = 0f; fw.Normalize(); }
            float range = se.m_iEffectRange * 0.01f, area = se.m_iEffectArea * 0.01f; var sp = self.position;
            switch (se.m_iEffectTarget)
            {
                case 1:
                    if (se.m_iTargetType == 1 && tg != null && tg.Hp > 0 && tg.GetBounds().SqrDistance(sp) <= range * range) { targets.Add(tg); arg1 = tg.Id; }
                    else if (se.m_iTargetType == 2) { int al = AllyTarget(sp, fw, range, 360f); if (al != 0) { allies.Add(al); arg1 = al; } else { selfTarget = true; arg1 = ents.SelfId; } }
                    break;
                case 2:
                    selfTarget = true; arg1 = ents.SelfId;
                    if (se.m_iSkillType == 29 || se.m_iSkillType == 40)
                    {   // la habilidad no se envía (ni se mueve el avatar) mientras la ranura está en enfriamiento: GameCondition.IsCoolTime(6 + slot)
                        if (wheel.IsCooling(slot)) return;
                        float extra = se.m_iValueA != null && se.m_iValueA.Length > 0 ? se.m_iValueA[0] * 0.01f : 0f; bool dash = se.m_iSkillType == 40;
                        local = () => { if (gcl == null) gcl = FindObjectOfType<GameClient>(); if (gcl == null) return; if (dash) gcl.SkillDash(35f); else gcl.SkillJump(extra); };
                    }
                    else if (se.m_iSkillType == 27 || se.m_iSkillType == 28)
                    {   // instancia: sólo dentro de una instancia con iRXCom != 0 (mensajes 217/218 en caso contrario); no hay estado de instancia en este cliente
                        if (logN++ < 8) Debug.Log("[skill] tipo " + se.m_iSkillType + " (instancia): sólo disponible dentro de una instancia (mensaje 217); no se envía");
                        return;
                    }
                    break;
                case 3:
                    if (se.m_iTargetType == 1 && tg != null) { Cone(targets, fw, se.m_iEffectAngle, range, self); if (targets.Contains(tg)) { targets.Remove(tg); targets.Insert(0, tg); arg1 = tg.Id; } else targets.Clear(); }
                    else if (se.m_iTargetType == 2) { int al = AllyTarget(sp, fw, range, se.m_iEffectAngle); if (al != 0) { arg1 = al; allies.Add(al); foreach (var kv in ents.OtherPlayers) if (kv.Key != al && kv.Value != null && InCone(sp, fw, kv.Value.transform.position, range, se.m_iEffectAngle)) allies.Add(kv.Key); } }
                    break;
                case 5:
                    arg1 = ents.SelfId; if (se.m_iTargetType == 1) Near(targets, sp, area); else { selfTarget = true; if (se.m_iTargetType == 2) AddPcsNear(allies, sp, area); }
                    break;
                case 6:
                    if (se.m_iTargetType == 1 && tg != null && tg.Hp > 0) { Near(targets, tg.transform.position, area); targets.Remove(tg); targets.Insert(0, tg); arg1 = tg.Id; }
                    else if (se.m_iTargetType == 2) { int al = AllyTarget(sp, fw, range, 360f); Transform ct = al != 0 ? ents.TransformOfPlayer(al) : self; if (al == 0) { selfTarget = true; arg1 = ents.SelfId; } else { arg1 = al; allies.Add(al); } AddPcsNear(allies, ct.position, area, al); }
                    break;
            }
            int max = Mathf.Max(1, se.m_iTargetNumber); var ids = new List<int>();
            if (selfTarget) ids.Add(ents.SelfId);
            foreach (var a in allies) if (!ids.Contains(a)) ids.Add(a);
            foreach (var n in targets) ids.Add(n.Id);
            if (ids.Count == 0) { if (Time.time - lastUse > 1f && logN++ < 8) Debug.Log("[skill] skill " + skillId + ": sin objetivos válidos (target=" + (tg != null ? tg.Id.ToString() : "-") + ")"); lastUse = Time.time; return; }
            if (ids.Count > max) ids.RemoveRange(max, ids.Count - max);
            if (wheel.CallCool(slot)) return;               // GameCondition.IsCallCoolTime(6+slot)
            wheel.NotifyNanoSkillUsed(se.m_iCoolType);      // CoolTimeCheck
            if (local != null) local();                     // movimiento local (salto / dash)
            NpcUI.Sender(ClientPackets.NanoSkillUse(-1, arg1, 0, 0, ids.ToArray()));
            Nn nn; if (byPc.TryGetValue(ents.SelfId, out nn)) SkillAnim(nn);
            lastUse = Time.time;
            if (logN++ < 10) Debug.Log("[skill] REQ_NANO_SKILL_USE skill=" + skillId + " objetivos=" + string.Join(",", ids.ConvertAll(x => x.ToString()).ToArray()));
        }

        GameClient gcl;
        /// Aliado objetivo (en el original, el avatar seleccionado con equipo 1): el jugador más cercano dentro del alcance y del cono frontal.
        int AllyTarget(Vector3 sp, Vector3 fw, float range, float angle)
        {
            int best = 0; float bd = float.MaxValue;
            foreach (var kv in ents.OtherPlayers)
            {
                if (kv.Value == null) continue; var p = kv.Value.transform.position; float d = Vector3.Distance(p, sp); if (d > range) continue;
                if (angle < 359f && !InCone(sp, fw, p, range, angle)) continue; if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }
        static bool InCone(Vector3 sp, Vector3 fw, Vector3 p, float range, float angle)
        {
            var d = p - sp; if (d.magnitude > range) return false; var h = new Vector3(d.x, 0f, d.z); return h.sqrMagnitude < 1e-4f || Vector3.Angle(h, fw) <= angle;
        }
        /// UserContainer.GetNearListAnyTeam: jugadores a menos de r (más cercanos primero).
        void AddPcsNear(List<int> res, Vector3 pos, float r, int except = 0)
        {
            var l = new List<KeyValuePair<float, int>>();
            foreach (var kv in ents.OtherPlayers) { if (kv.Value == null || kv.Key == except) continue; float d = Vector3.Distance(kv.Value.transform.position, pos); if (d <= r) l.Add(new KeyValuePair<float, int>(d, kv.Key)); }
            l.Sort((x, y) => x.Key.CompareTo(y.Key)); foreach (var e in l) if (!res.Contains(e.Value)) res.Add(e.Value);
        }

        IEnumerable<NpcInfo> Mobs()
        {
            foreach (var go in ents.NpcObjects) { if (go == null) continue; var n = go.GetComponent<NpcInfo>(); if (n != null && n.IsMob && n.Hp > 0) yield return n; }
        }
        void Near(List<NpcInfo> res, Vector3 pos, float r)
        {   // NpcContainer.GetNearListMobTeam: mobs a < r, de más cerca a más lejos
            tmp.Clear(); foreach (var n in Mobs()) { float d = Mathf.Sqrt(n.GetBounds().SqrDistance(pos)); if (d <= r) { n.SortDist = d; tmp.Add(n); } }
            tmp.Sort((x, y) => x.SortDist.CompareTo(y.SortDist)); res.AddRange(tmp);
        }
        void Cone(List<NpcInfo> res, Vector3 fw, float angle, float range, Transform self)
        {   // NpcContainer.GetSkillConeList
            tmp.Clear(); var pos = self.position + Vector3.up * 0.8f;
            foreach (var n in Mobs())
            {
                var p = n.transform.position; var d = p - pos; float dist = d.magnitude; float h = Mathf.Max(0.5f, n.Height);
                float dd = Mathf.Sqrt(n.GetBounds().SqrDistance(pos)); if (dd > range) continue;
                if (dist > 1e-3f && Vector3.Angle(new Vector3(d.x, 0f, d.z), fw) > angle) continue;
                n.SortDist = dd; tmp.Add(n);
            }
            tmp.Sort((x, y) => x.SortDist.CompareTo(y.SortDist)); res.AddRange(tmp);
        }

        // ------------------------------------------------------------------ resultado (GameFrame.SkillResultDistributor + MakeSkillBullet)

        void OnSkill(byte[] b, bool own)
        {
            try
            {
                int pc = I(b, 0), skillId = S(b, 6), est = I(b, 28), cnt = I(b, 32), nanoId = S(b, 24); short stamina = S(b, 26);
                if (own && ents.SelfId != 0) pc = ents.SelfId;
                var user = ents.TransformOfPlayer(pc); Nn nn; byPc.TryGetValue(pc, out nn);
                if (nn != null && !(own && Time.time - lastUse < 0.3f)) SkillAnim(nn);   // el propio ya animó al enviar
                var se = wheel != null ? wheel.SkillOf(skillId) : null; if (se == null) return;
                if (logN++ < 14) Debug.Log("[skill] " + (own ? "SUCC" : "otro") + " pc=" + pc + " skill=" + skillId + " eST=" + est + " objetivos=" + cnt + " estamina=" + stamina);
                if (cnt <= 0) return; int stride = Stride(est); if (stride == 0) return;
                int first = 36; Transform src = nn != null && nn.Root != null && !nn.Dead ? nn.Root.transform : user;
                if (est == 30 && b.Length >= 52)
                {   // sSkillResult_Heal_HP propio + daños
                    if (own && user != null) SelfHeal(b, 36); first = 52; stride = 20;
                }
                var fx = BulletFx.Instance;
                for (int i = 0; i < cnt; i++)
                {
                    int o = first + stride * i; if (o + stride > b.Length) break; int ct = I(b, o), id = I(b, o + 4);
                    Transform tgt = null; float h = 1.5f;
                    if (ct == 1) tgt = ents.TransformOfPlayer(id); else if (ct == 2 || ct == 4) { var go = ents.NpcById(id); if (go != null) { tgt = go.transform; var inf = go.GetComponent<NpcInfo>(); if (inf != null) h = inf.Height; } }
                    if (fx != null && src != null && tgt != null && se.m_iTargetEffect > 0) fx.Fire(se.m_iTargetEffect, src, tgt, h, tgt.position + Vector3.up);
                    if ((est == 2 || est == 34) && ct == 1 && id == ents.SelfId && own) SelfHeal(b, o);
                    else if ((est == 2 || est == 34) && ct == 1 && o + 12 <= b.Length && tgt != null) FloatingPoints.AddOn(tgt.gameObject, 0, FloatingPoints.Heal, I(b, o + 8), 1.8f);
                }
                if (own && est != 2 && est != 34)
                {   // GameFrame.MakeSkillBullet: "You have attacked MOB with <skill>."
                    bool mob = false; for (int i = 0; i < cnt; i++) { int o2 = first + stride * i; if (o2 + 8 <= b.Length && I(b, o2) != 1) { mob = true; break; } }
                    string sn = combat != null ? combat.SkillName(skillId) : null; if (mob && !string.IsNullOrEmpty(sn)) ChatHud.AddEventString(7, "You have attacked MOB with <" + sn + ">.");
                }
                if (est == 1 || est == 3 || est == 4 || est == 5 || est == 8 || est == 30) combat.ApplySkillDamage(b, pc == ents.SelfId ? -1 : pc, 32, first, stride);
            }
            catch (Exception e) { Debug.LogWarning("[skill] " + e.Message); }
        }

        void SelfHeal(byte[] b, int o)
        {   // sSkillResult_Heal_HP { eCT, iID, iHealHP, iHP }
            if (o + 16 > b.Length) return; int heal = I(b, o + 8), hp = I(b, o + 12);
            if (hud != null && hud.Data != null) hud.Data.Hp = hp;
            if (ents.Self != null) FloatingPoints.AddOn(ents.Self, 0, FloatingPoints.Heal, heal, 1.8f);
        }
    }
}
