// Jugadores y NPC visibles en el mundo, alimentados por el PacketDispatcher. SIN COMPILAR NI PROBAR EN UNITY (sólo TypeCheck contra el stub).
// Los paquetes llegan en el hilo de red: se encolan y se procesan en Update con un presupuesto de tiempo, porque montar un avatar es caro.
// Diferencias con el cliente original (documentadas): sin física (se coloca en el suelo del servidor), borrado inmediato en vez del fundido AppearEffect,
// salto aproximado, sin nombres/barras de vida, y sin reintento de modelos que aún no estén descargados (se muestra un marcador).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed partial class WorldEntities : MonoBehaviour
    {
        public float FrameBudgetMs = 4f;        // tiempo máximo por frame dedicado a procesar paquetes
        public int MaxSpawnsPerFrame = 3;

        readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        readonly Dictionary<int, GameObject> players = new Dictionary<int, GameObject>(), npcs = new Dictionary<int, GameObject>();
        ModelCatalog catalog; ModelFactory models; PlayerAvatarFactory avatars; HNpcTableScript hnpcs; Transform root; PacketDispatcher dispatcher;

        public MissionTableScript MissionTable; public InstanceTableScript Instances; public MissionState Missions; public QuestSave QuestData; public int LastWarpFail;
        public NpcTableScript NpcTable;   // para velocidades de animación, gestos de reposo y 'barkers'
        Dictionary<int, NpcTableElement> npcRows;
        NpcTableElement NpcRow(int type) { if (npcRows == null) { npcRows = new Dictionary<int, NpcTableElement>(); if (NpcTable?.m_pNpcData != null) foreach (var e in NpcTable.m_pNpcData) npcRows[e.m_iNpcNumber] = e; } return npcRows.TryGetValue(type, out var r) ? r : null; }
        public GameObject Self { get; private set; }
        public IEnumerable<GameObject> NpcObjects => npcs.Values;
        public GameObject NpcById(int id) { return npcs.TryGetValue(id, out var g) ? g : null; }
        public int SelfId { get; private set; }
        public IEnumerable<KeyValuePair<int, GameObject>> OtherPlayers => players;
        readonly Dictionary<int, PcAppearance> apps = new Dictionary<int, PcAppearance>();
        /// <summary>Apariencia (equipo incluido) con la que entro un jugador remoto; RemoteCombatAnim actualiza Equip[0] con PC_EQUIP_CHANGE. null = desconocido o ya salio.</summary>
        public PcAppearance AppOf(int id) { PcAppearance a; return apps.TryGetValue(id, out a) ? a : null; }
        public Transform TransformOfPlayer(int id) { if (id == SelfId && Self != null) return Self.transform; return players.TryGetValue(id, out var g) && g != null ? g.transform : null; }
        public event Action<GameObject, EnterSucc> SelfSpawned;
        public event Action<int, int, int> Teleported;   // posición del servidor tras REP_PC_GOTO_SUCC (el controlador del jugador propio la aplica)
        public int PlayerCount => players.Count; public int NpcCount => npcs.Count;

        public void Init(PacketDispatcher d, ModelCatalog catalog, ModelFactory models, PlayerAvatarFactory avatars, HNpcTableScript hnpcs, Transform root = null)
        {
            if (GetComponent<NpcUI>() == null) gameObject.AddComponent<NpcUI>().Init(this, models); this.catalog = catalog; this.models = models; this.avatars = avatars; this.hnpcs = hnpcs; this.root = root != null ? root : transform;
            if (dispatcher != null) Detach();
            dispatcher = d;
            d.Raw += PlayerPacket; d.Malformed += PlayerMalformed;
            d.SelfEnter += OnSelf; d.PcEnter += OnPcEnter; d.PcExit += OnPcExit; d.PcMove += OnPcMove; d.PcStop += OnPcStop; d.PcJump += OnPcJump;
            d.NpcEnter += OnNpcEnter; d.NpcExit += OnNpcExit; d.NpcMove += OnNpcMove; d.Teleport += OnTeleport; d.WarpFail += OnWarpFail;
        }
        void Detach()
        {
            var d = dispatcher; d.Raw -= PlayerPacket; d.Malformed -= PlayerMalformed; d.SelfEnter -= OnSelf; d.PcEnter -= OnPcEnter; d.PcExit -= OnPcExit; d.PcMove -= OnPcMove; d.PcStop -= OnPcStop; d.PcJump -= OnPcJump;
            d.NpcEnter -= OnNpcEnter; d.NpcExit -= OnNpcExit; d.NpcMove -= OnNpcMove; d.Teleport -= OnTeleport; d.WarpFail -= OnWarpFail;
        }
        void OnDestroy() { if (dispatcher != null) Detach(); }

        // --- hilo de red: sólo encolar ---
        // Combat and lifecycle changes share FIFO order: a lethal hit must be
        // applied before the following NPC_EXIT removes the object.
        public void Enqueue(Action change) => queue.Enqueue(change);
        void OnSelf(EnterSucc e) => queue.Enqueue(() => SpawnSelf(e));
        void OnPcEnter(PcAppearance a) => queue.Enqueue(() => SpawnPlayer(a));
        void OnPcExit(int id) => queue.Enqueue(() => { Remove(players, id); apps.Remove(id); missingPlayers.Remove(id); playerExits++; RemoteCombatAnim.Forget(id); });
        void OnPcMove(PcMove m) => queue.Enqueue(() => { if (m.Id != SelfId && players.TryGetValue(m.Id, out var go)) { go.GetComponent<NpcAnimator>()?.SetKey(m.Key); go.GetComponent<RemoteMover>()?.OnMove(PlayerAvatarFactory.ToClient(m.X, m.Y, m.Z), new Vector3(m.VX, m.VZ, m.VY), m.Speed * 0.01f, m.Angle); } });
        void OnPcStop(PcStop s) => queue.Enqueue(() => { if (s.Id != SelfId && players.TryGetValue(s.Id, out var go)) go.GetComponent<RemoteMover>()?.OnStop(PlayerAvatarFactory.ToClient(s.X, s.Y, s.Z)); });
        void OnPcJump(PcJump j) => queue.Enqueue(() => { if (j.Id != SelfId && players.TryGetValue(j.Id, out var go)) go.GetComponent<RemoteMover>()?.OnJump(PlayerAvatarFactory.ToClient(j.X, j.Y, j.Z), new Vector3(j.VX * 0.01f, j.VZ * 0.01f, j.VY * 0.01f), j.Speed * 0.01f, j.Angle); });
        void OnNpcEnter(NpcAppearance n) => queue.Enqueue(() => SpawnNpc(n));
        void OnNpcExit(int id) => queue.Enqueue(() => ExitNpc(id));
        /// NPC_EXIT: un mob muerto por ataque se deja terminar su animacion de muerte (AppearEffect.Delete: 2 s de margen) antes de borrarlo.
        void ExitNpc(int id)
        {
            GameObject go; int killer;
            if (npcs.TryGetValue(id, out go) && go != null)
            {
                var an = go.GetComponent<NpcAnimator>(); bool k = CombatSystem.Killed.TryRemove(id, out killer);
                if (an != null && (an.Dying || k))
                {
                    if (!an.Dying) { var inf = go.GetComponent<NpcInfo>(); an.Die(killer < 0 ? (Self != null ? Self.transform : null) : TransformOfPlayer(killer), inf != null ? inf.Radius : 0.5f, inf != null ? inf.Height : 1.5f); }
                    npcs.Remove(id); Destroy(go, Mathf.Max(2f, an.DeathRemaining() + 1.5f)); return;
                }
            }
            Remove(npcs, id);
        }
        void OnNpcMove(NpcMove m) => queue.Enqueue(() => { if (npcs.TryGetValue(m.Id, out var go)) go.GetComponent<RemoteMover>()?.OnNpcMove(PlayerAvatarFactory.ToClient(m.ToX, m.ToY, m.ToZ), m.Speed * 0.01f); });
        void OnWarpFail(int code) => queue.Enqueue(() => { LastWarpFail = code; UnityEngine.Debug.LogWarning("[warp] el servidor rechazó el warp, código " + code); });
        /// <summary>Teletransporte del jugador propio pedido por la UI (Warp Away: REP_PC_REGEN_SUCC); mismo camino que REP_PC_GOTO_SUCC.</summary>
        public void RaiseTeleport(int x, int y, int z) { OnTeleport(x, y, z); }
        void OnTeleport(int x, int y, int z) => queue.Enqueue(() => Teleported?.Invoke(x, y, z));

        void Update()
        {
            UpdatePlayers();
            var sw = Stopwatch.StartNew(); int spawns = 0;
            while (queue.TryDequeue(out var a))
            {
                a();
                if (++spawns >= MaxSpawnsPerFrame * 8 || sw.Elapsed.TotalMilliseconds > FrameBudgetMs) break;   // los movimientos son baratos; los spawns pesan más y se frenan en SpawnPlayer/SpawnNpc
            }
        }

        // --- hilo principal ---
        void Remove(Dictionary<int, GameObject> map, int id) { if (map.TryGetValue(id, out var go)) { map.Remove(id); if (go != null) Destroy(go); } }

        static int diagNpc, diagAnim, diagBones;
        /// Diagnóstico de esqueleto: compara la pose real de cada hueso con la que implica su bindpose (si difieren, la malla se deforma o se colapsa).
        System.Collections.IEnumerator BoneDiag(GameObject go, string who)
        {
            yield return new WaitForSeconds(1.5f);
            if (go == null || diagBones++ >= 4) yield break;
            var sb = new System.Text.StringBuilder("[huesos] " + who + ": ");
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh; if (mesh == null) { sb.Append("sin malla; "); continue; }
                var bp = mesh.bindposes; var bs = smr.bones; int n = Mathf.Min(bp.Length, bs.Length);
                sb.Append("SMR '" + smr.name + "' huesos=" + bs.Length + " bindposes=" + bp.Length + " bounds=" + smr.bounds.size + " | ");
                float sum = 0f, max = 0f; int nulls = 0, zero = 0;
                for (int i = 0; i < n; i++)
                {
                    if (bs[i] == null) { nulls++; continue; }
                    var exp = smr.transform.localToWorldMatrix * bp[i].inverse; var act = bs[i].localToWorldMatrix;
                    float d = (new Vector3(exp.m03, exp.m13, exp.m23) - new Vector3(act.m03, act.m13, act.m23)).magnitude; sum += d; if (d > max) max = d;
                    if (bs[i].lossyScale.sqrMagnitude < 1e-8f) zero++;
                    if (i < 4) sb.Append(bs[i].name + " d=" + d.ToString("F2") + " esc=" + bs[i].lossyScale + "; ");
                }
                sb.Append("distMedia=" + (n > 0 ? sum / n : 0f).ToString("F2") + " distMax=" + max.ToString("F2") + " nulos=" + nulls + " escalaCero=" + zero + " | ");
            }
            var an = go.GetComponentInChildren<Animation>(); if (an != null) sb.Append("anim.isPlaying=" + an.isPlaying);
            UnityEngine.Debug.Log(sb.ToString());
        }
        /// El original reproduce una animación de reposo al crear el modelo; sin ella los huesos quedan en la pose del prefab (modelos deformados).
        /// Los prefabs 2.5 llegan con nodos desactivados (el original usaba SetActiveRecursively(true)).
        public static void ActivateAll(GameObject go)
        {
            if (go == null) return; foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        }
        /// Los NPC "objectnpc" (clip npc_location) son marcadores de lugar: en el original no se dibujan.
        static void HideIfLocation(GameObject model)
        {
            var an = model.GetComponentInChildren<Animation>(true); if (an == null || an["npc_location"] == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
        public static void PlayIdle(GameObject model, string who)
        {
            if (model == null) return; var an = model.GetComponentInChildren<Animation>(); if (an == null) { if (diagNpc < 6) UnityEngine.Debug.Log("[anim] " + who + ": sin componente Animation"); return; }
            var names = new System.Collections.Generic.List<string>(); foreach (AnimationState st in an) names.Add(st.name);
            string pick = null;
            foreach (var exact in new[] { "stand1", "stand", "idle", "wait", "staying", "stay", "normal", "default" })
            { foreach (var n in names) if (string.Equals(n, exact, StringComparison.OrdinalIgnoreCase)) { pick = n; break; } if (pick != null) break; }
            if (pick == null) foreach (var n in names) { var l = n.ToLowerInvariant(); if ((l.Contains("stand") || l.Contains("idle")) && !l.Contains("add") && !l.Contains("swim") && !l.Contains("stick") && !l.Contains("rifle") && !l.Contains("pistol") && !l.Contains("rocket") && !l.Contains("bomb") && !l.Contains("rope") && !l.Contains("scooter") && !l.Contains("board")) { pick = n; break; } }
            if (pick == null && an.clip != null) pick = an.clip.name;
            if (pick == null) foreach (var n in names) if (n != "height" && n != "shape" && n != "height_Add" && n != "shape_Add") { pick = n; break; }
            if (diagAnim++ < 8) UnityEngine.Debug.Log("[anim] " + who + ": clips=[" + string.Join(", ", names.ToArray()) + "] -> " + (pick ?? "ninguno"));
            if (pick != null) { var s = an[pick]; if (s != null) s.wrapMode = WrapMode.Loop; an.cullingType = AnimationCullingType.AlwaysAnimate; an.Play(pick); }
        }
        static string AvgAlpha(Texture2D t)
        {
            if (t == null) return "-";
            try { var px = t.GetPixels32(); long s = 0; int n = Mathf.Min(px.Length, 4096), step = Mathf.Max(1, px.Length / n); int c = 0; for (int i = 0; i < px.Length; i += step) { s += px[i].a; c++; } return (s / (float)(c * 255)).ToString("F2"); }
            catch (System.Exception) { return "?"; }
        }
        static string DescribeDeep(GameObject go)
        {
            if (go == null) return "null"; var sb = new System.Text.StringBuilder("capa=" + go.layer + " ");
            var cam = Camera.main; if (cam != null) sb.Append("cullingMask&capa=" + ((cam.cullingMask & (1 << go.layer)) != 0) + " ");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                sb.Append("\n   · " + r.GetType().Name + " '" + r.name + "' capa=" + r.gameObject.layer + " on=" + r.enabled + " mats=" + r.sharedMaterials.Length);
                foreach (var m in r.sharedMaterials) sb.Append(" [" + (m == null ? "NULL" : m.name + " alfaTex=" + AvgAlpha((m.HasProperty("_MainTex") ? m.mainTexture : null) as Texture2D) + " sh=" + (m.shader != null ? m.shader.name : "-") + " tex=" + (m.HasProperty("_MainTex") && m.mainTexture != null ? m.mainTexture.name : "NINGUNA") + " cola=" + m.renderQueue) + "]");
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) sb.Append(" malla=" + (smr.sharedMesh != null ? smr.sharedMesh.vertexCount + "v" : "NULL") + " huesos=" + (smr.bones != null ? smr.bones.Length : -1) + " raíz=" + (smr.rootBone != null ? smr.rootBone.name : "null") + " localBounds=" + smr.localBounds.size + " escalaMundo=" + r.transform.lossyScale);
                else { var mf = r.GetComponent<MeshFilter>(); sb.Append(" malla=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount + "v" : "NULL") + " escalaMundo=" + r.transform.lossyScale); }
            }
            return sb.ToString();
        }
        /// Diagnóstico: posición mundial, nº de renderers (activos), tamaño de sus límites y shader del primero.
        static string Describe(GameObject go)
        {
            if (go == null) return "null";
            var rs = go.GetComponentsInChildren<Renderer>(true); int on = 0; Bounds b = default; bool any = false; string sh = "-";
            foreach (var r in rs) { if (r.enabled) on++; if (!any) { b = r.bounds; any = true; sh = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "sin material"; } else b.Encapsulate(r.bounds); }
            return go.name + " pos=" + go.transform.position + " activo=" + go.activeInHierarchy + " renderers=" + rs.Length + "(" + on + " on) tamaño=" + (any ? b.size.ToString() : "-") + " centro=" + (any ? b.center.ToString() : "-") + " shader=" + sh;
        }

        void SpawnSelf(EnterSucc e)
        {
            if (Self != null) Destroy(Self);
            SelfId = e.Id; Self = avatars?.Create(e.Self, root, own: true);
            { var sa = Self != null ? Self.GetComponentInChildren<Animation>(true) : null; if (sa != null && sa.GetComponent<AvatarEmoteGuard>() == null) sa.gameObject.AddComponent<AvatarEmoteGuard>().Own = true; }   // emotes sostenidos (sentado/tumbado/baile) sin levantarse solo
            UnityEngine.Debug.Log("[ent] yo(detalle): " + DescribeDeep(Self)); StartCoroutine(BoneDiag(Self, "yo"));
            UnityEngine.Debug.Log("[ent] yo: " + Describe(Self) + (avatars == null ? " (avatars == null: tablas no cargadas)" : ""));
            QuestData = e.Quest; try { if (MissionTable != null && e.Quest != null) { Missions = new MissionState(MissionTable, e.Quest, e.Self.Level); UnityEngine.Debug.Log("[mision] completadas=" + Missions.CompletedCount + " activas=" + Missions.ActiveCount + " nivel=" + e.Self.Level); } } catch (Exception ex) { UnityEngine.Debug.LogWarning("[mision] " + ex.Message); }
            if (Self != null) SelfSpawned?.Invoke(Self, e);
        }

        /// <summary>Tabla de armas (WorldBootstrap: Entities.WeaponTable = tables.m_pWeaponItemTable). Sin ella los jugadores remotos usan siempre las animaciones de puños (sin prefijo stick/pistol/rifle...).</summary>
        public EquipmentItemTableScript WeaponTable;
        /// <summary>cnAvatarStatus.iAniStyle según m_iEquipType del arma (Equip[0]): 1,2 stick; 3 pistol; 4 rifle; 10 bomb; 11 rocket (CombatSystem usa la misma correspondencia).</summary>
        int StyleOf(PcAppearance a)
        {
            try
            {
                var w = a != null && a.Equip != null && a.Equip.Length > 0 ? a.Equip[0] : default(ItemBase);
                if (w.Id <= 0 || WeaponTable == null || WeaponTable.m_pItemData == null) return 0;
                foreach (var e in WeaponTable.m_pItemData)
                {
                    if (e == null || e.m_iItemNumber != w.Id) continue;
                    switch (e.m_iEquipType) { case 1: case 2: return 1; case 3: return 2; case 4: return 3; case 10: return 6; case 11: return 4; }
                    return 0;
                }
            }
            catch (Exception) { }
            return 0;
        }
        /// <summary>Cambia el estilo de animacion (arma) de un jugador remoto, p. ej. al recibir su cambio de equipo.</summary>
        public void SetPlayerStyle(int pcId, int style) { if (players.TryGetValue(pcId, out var g) && g != null) { var na = g.GetComponent<NpcAnimator>(); if (na != null) na.SetStyle(style); } }

        void SpawnPlayer(PcAppearance a)
        {
            if (a.Id == SelfId) return;
            Remove(players, a.Id);
            var go = avatars?.Create(a, root, own: false) ?? new GameObject("Player#" + a.Id);
            if (go == null) return;
            missingPlayers.Remove(a.Id);
            go.transform.SetParent(root, true);
            var position = PlayerAvatarFactory.ToClient(a.X, a.Y, a.Z); position.y = Ground.Y(position); go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, a.Angle, 0);
            var mv = go.AddComponent<RemoteMover>(); mv.Init(go.transform.position);
            PrepareRemotePlayer(go, a);
            go.AddComponent<RemotePc>().Id = a.Id; apps[a.Id] = a; RemoteCombatAnim.Forget(a.Id);
            players[a.Id] = go; playerSpawns++;
            UnityEngine.Debug.Log("[players] spawn id=" + a.Id + " position=" + go.transform.position + " model=" + HasPlayerModel(go) + " tracked=" + players.Count);
        }

        /// Como NpcContainer.Add: hp <= 0 no se crea; tipo desconocido se registra; humanoide (m_iHNpc) por piezas; el resto, modelo de mob/ con escala y texturas.
        void SpawnNpc(NpcAppearance n)
        {
            Remove(npcs, n.Id);
            if (n.Hp <= 0) return;
            var m = catalog?.Npc(n.Type); if (m == null) { UnityEngine.Debug.LogWarning("NPC de tipo desconocido " + n.Type); return; }
            var holder = new GameObject("NPC#" + n.Id + " t" + n.Type); holder.transform.SetParent(root, false);
            { var np = PlayerAvatarFactory.ToClient(n.X, n.Y, n.Z); np.y = Ground.Y(np); holder.transform.position = np; } holder.transform.rotation = Quaternion.Euler(0f, n.Angle + 180, 0f);
            GameObject model = null;
            if (m.HNpcIndex >= 0 && hnpcs?.TableElement != null && m.HNpcIndex < hnpcs.TableElement.Length && avatars != null)
            { var look = ActorLook.FromHNpc(hnpcs.TableElement[m.HNpcIndex]); model = avatars.Assembler.Build(look, ActorColors.From(look), holder.transform); }
            else if (models != null) model = models.Instantiate(m, holder.transform);
            if (diagNpc++ < 6) UnityEngine.Debug.Log("[ent] npc " + n.Type + " " + m + ": " + Describe(holder) + (model == null ? " SIN MODELO" : ""));
            if (model == null) { UnityEngine.Debug.LogWarning("Modelo de NPC no disponible: " + m); Marker(holder, Color.yellow); }
            else { ActivateAll(model); HideIfLocation(model); model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; { var row = NpcRow(n.Type); string[] hs = null; if (row != null && row.m_iHNpc != 0 && row.m_iMesh > 0 && NpcTable?.m_pNpcMeshData != null && row.m_iMesh < NpcTable.m_pNpcMeshData.Length) { var me = NpcTable.m_pNpcMeshData[row.m_iMesh]; if (me != null) hs = new[] { me.m_pstrMMeshModelString, me.m_pstrMTextureString, me.m_pstrMTextureString2 }; } holder.AddComponent<NpcAnimator>().Init(model, row, hs); if (row != null) { var inf = holder.AddComponent<NpcInfo>(); inf.Id = n.Id; inf.NpcNumber = row.m_iNpcNumber; inf.Team = row.m_iTeam; inf.Level = row.m_iNpcLevel; inf.Style = row.m_iNpcStyle; inf.MaxHp = Mathf.Max(row.m_iHP, n.Hp, 1); inf.Hp = n.Hp; inf.Effect = row.m_iEffect; inf.TableHeight = row.m_iHeight * 0.01f; inf.Type = row.m_iNpcType; inf.MapIcon = row.m_iMapIcon; inf.Service = row.m_iServiceNumber; inf.Radius = row.m_iRadius * 0.01f; var sd = NpcTable?.m_pNpcStringData; if (sd != null && row.m_iNpcName >= 0 && row.m_iNpcName < sd.Length && sd[row.m_iNpcName] != null) { inf.Name = sd[row.m_iNpcName].m_strName ?? ""; inf.Greeting = sd[row.m_iNpcName].m_strComment ?? ""; } var idt = NpcTable?.m_pNpcIconData; if (idt != null && row.m_iIcon1 >= 0 && row.m_iIcon1 < idt.Length && idt[row.m_iIcon1] != null) { inf.IconNumber = idt[row.m_iIcon1].m_iIconNumber; inf.IconType = idt[row.m_iIcon1].m_iIconType; } } if (row != null && row.m_iBarkerType != 0 && NpcTable?.m_pNpcBarkerData != null && row.m_iBarkerNumber > 0 && row.m_iBarkerNumber < NpcTable.m_pNpcBarkerData.Length) { var be = NpcTable.m_pNpcBarkerData[row.m_iBarkerNumber]; if (be != null) holder.AddComponent<NpcBarker>().Init(new[] { be.m_strName, be.m_strComment, be.m_strComment1, be.m_strComment2 }, () => Self != null ? Self.transform : null); } } if (diagBones < 3) StartCoroutine(BoneDiag(model, "npc " + n.Type)); }
            SeasonalEvents.Instance?.TrackNpc(model, m, models, n.Id);
            holder.AddComponent<RemoteMover>().Init(holder.transform.position);
            { var ni = holder.GetComponent<NpcInfo>(); BlobShadow.Attach(holder, Mathf.Clamp((ni != null ? ni.Radius : 0.5f) * 2.6f, 1.0f, 8f)); }
            { var ni2 = holder.GetComponent<NpcInfo>(); if (ni2 != null) AttachIconFx(holder, ni2); }
            npcs[n.Id] = holder;
        }

        Material iconMat;
        // Icono de servicio sobre la cabeza = partícula aditiva del original (ver NpcIconFx): quad en el mundo, 1 s de ciclo, tamaño 0.35/0.30/0.35 m.
        void AttachIconFx(GameObject holder, NpcInfo ni)
        {
            try
            {
                int e = NpcIconFx.ChooseEffect(ni.Type, ni.Effect, false, false); if (e < 0 || !NpcIconFx.TryGetSpec(e, out var sp)) return;
                var tex = Resources.Load<Texture2D>("OpenFusion/Icons/" + sp.Texture); if (tex == null) return;
                if (iconMat == null) { var sh = Shader.Find("OpenFusion/NpcIconParticle"); if (sh == null) { UnityEngine.Debug.LogWarning("[npcfx] shader OpenFusion/NpcIconParticle no encontrado"); return; } iconMat = new Material(sh); }
                Transform head = null; foreach (var t in holder.GetComponentsInChildren<Transform>(true)) if (t.name == "Bip01 Head") { head = t; break; }
                holder.AddComponent<NpcIconBillboard>().Init(e, tex, holder.transform, head, ni.TableHeight, iconMat); ni.HasFx = true;
            }
            catch (Exception ex) { UnityEngine.Debug.LogWarning("[npcfx] " + ex.Message); }
        }
        static GameObject Marker(string name, Color c) { var go = new GameObject(name); Marker(go, c); return go; }
        static void Marker(GameObject host, Color c)
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule); var col = cap.GetComponent<Collider>(); if (col != null) Destroy(col);
            cap.transform.SetParent(host.transform, false); cap.transform.localPosition = new Vector3(0f, 1f, 0f);
            var r = cap.GetComponent<Renderer>(); if (r != null) r.material.color = c;
        }
    }

    /// <summary>Marca el contenedor de un jugador remoto con su id (RemoteCombatAnim.WeaponItemOf lo busca subiendo desde el hueso que lanza un evento de animacion).</summary>
    public sealed class RemotePc : MonoBehaviour { public int Id; }

    /// Burbuja automática de NPC ('barker'): cada 15 s (con desfase aleatorio) 10 % de probabilidad, si el jugador está a menos de 10 m; texto al azar de 4; dura max(7 s, largo/5).
    public sealed class NpcBarker : MonoBehaviour
    {
        string[] lines; System.Func<Transform> player; float next; GameObject bubble; float hideAt; TextMesh tm; Transform bg; bool sized;
        public void Init(string[] l, System.Func<Transform> p)
        {
            var ok = new List<string>(); foreach (var x in l) if (!string.IsNullOrEmpty(x) && x.Trim().Length > 0) ok.Add(x);
            lines = ok.ToArray(); player = p; next = Time.time + UnityEngine.Random.Range(0f, 15f);
        }
        static string Wrap(string t, int w) { var sb = new System.Text.StringBuilder(); int c = 0; foreach (var word in t.Split(' ')) { if (c + word.Length > w && c > 0) { sb.Append('\n'); c = 0; } else if (c > 0) { sb.Append(' '); c++; } sb.Append(word); c += word.Length; } return sb.ToString(); }
        void Update()
        {
            if (lines == null || lines.Length == 0) return; Watchdog.At("barker.Update");
            if (bubble != null && Time.time >= hideAt) { Destroy(bubble); bubble = null; }
            if (Time.time < next) return;
            next = Time.time + 15f;
            var pl = player?.Invoke(); if (pl == null || bubble != null) return;
            if ((pl.position - transform.position).sqrMagnitude > 100f) return;
            if (UnityEngine.Random.value > 0.1f) return;
            Show(lines[UnityEngine.Random.Range(0, lines.Length)]);
        }
        void Show(string text)
        {
            float top = 2f; var rs = GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); top = b.max.y - transform.position.y + 0.4f; }
            bubble = new GameObject("barker"); bubble.transform.SetParent(transform, false); bubble.transform.position = transform.position + Vector3.up * top;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var tg = new GameObject("txt"); tg.transform.SetParent(bubble.transform, false); tg.transform.localPosition = new Vector3(0, 0, -0.01f);
            tm = tg.AddComponent<TextMesh>(); tm.font = font; tm.text = Wrap(text, 28); tm.fontSize = 48; tm.characterSize = 0.025f; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.black;
            tg.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.transform.SetParent(bubble.transform, false); bg = q.transform;
            var m = new Material(Shader.Find("Sprites/Default")); m.color = new Color(1, 1, 1, 0.9f); q.GetComponent<Renderer>().sharedMaterial = m;
            sized = false; hideAt = Time.time + Mathf.Max(7f, text.Length / 5f);
        }
        void LateUpdate()
        {
            if (bubble == null) return;
            var cam = Camera.main; if (cam != null) bubble.transform.rotation = cam.transform.rotation;
            if (!sized && tm != null) { var b = tm.GetComponent<MeshRenderer>().bounds; if (b.size.x > 0.01f) { bg.localScale = new Vector3(b.size.x + 0.3f, b.size.y + 0.2f, 1f); bg.localPosition = new Vector3(0, 0, 0); sized = true; } }
        }
    }

    /// Movimiento remoto: corrige hacia la posición del servidor y extrapola con la velocidad recibida (como GotoDir/SetPosition del cliente).
    public sealed class RemoteMover : MonoBehaviour
    {
        float nextGround; Vector3 target, dir; float speed, vy, yaw, npcSpeed; bool hasYaw, npcMoving; public bool Flying; const float Gravity = 10f, Catchup = 12f, TurnRate = 540f;

        public void Init(Vector3 p) { target = p; }
        float jumpAt = -10f;
        /// <summary>Salto reciente (PC_JUMP hace &lt; 1,5 s y con velocidad vertical): animacion board_jump / scooter_jump de los jinetes (VehicleRide).</summary>
        public bool Airborne { get { return vy != 0f && Time.time - jumpAt < 1.5f; } }
        public void OnMove(Vector3 pos, Vector3 vel, float spd, float angle)
        {
            target = pos; vy = vel.y; dir = new Vector3(vel.x, 0f, vel.z);
            if (dir.sqrMagnitude > 0f) { dir.Normalize(); yaw = angle; hasYaw = true; } else dir = Vector3.zero; speed = spd;
        }
        public void OnStop(Vector3 pos) { target = pos; dir = Vector3.zero; speed = 0f; vy = 0f; }
        /// <summary>Orienta al avatar hacia donde MIRA el modelo (yaw visible, grados). El contenedor lleva el modelo girado 180 grados: UserMoveController.Attack hace LookAt(objetivo) + Rotate(0,180,0). Persiste hasta el siguiente PC_MOVE.</summary>
        public void Face(float visibleYaw) { yaw = visibleYaw + 180f; hasYaw = true; transform.rotation = Quaternion.Euler(0f, yaw, 0f); }
        /// <summary>PC_REGEN (UserMoveController.ReceiveRegen): transform.position = posicion del servidor y GotoDir(0): parado y sin salto.</summary>
        public void Warp(Vector3 pos) { var p = pos; p.y = Ground.Y(p, 40f); transform.position = p; target = p; dir = Vector3.zero; speed = 0f; vy = 0f; npcMoving = false; }
        public void OnJump(Vector3 pos, Vector3 vel, float spd, float angle) { jumpAt = Time.time; target = pos; dir = new Vector3(vel.x, 0f, vel.z); if (dir.sqrMagnitude > 0f) dir.Normalize(); speed = spd; vy = vel.y; yaw = angle; hasYaw = true; }
        public void OnNpcMove(Vector3 to, float spd) { target = to; npcSpeed = spd; npcMoving = true; dir = Vector3.zero; speed = 0f; }
        /// <summary>BusSystem: el autobus sobre el que va este jugador se ha movido delta; se desplaza posicion y objetivo (y la referencia de animacion, para que no corra parado sobre el autobus).</summary>
        public void Carry(Vector3 delta) { target += delta; lastP += delta; transform.position += delta; }

        Vector3 lastP; bool lastInit; NpcAnimator anim; bool animTried;
        void LateUpdate()
        {
            if (!animTried) { animTried = true; anim = GetComponent<NpcAnimator>(); }
            if (anim != null) { var cp = transform.position; if (lastInit && Time.deltaTime > 0f) { var hv = new Vector3(cp.x - lastP.x, 0f, cp.z - lastP.z) / Time.deltaTime; anim.SetMotion(hv.magnitude); } lastP = cp; lastInit = true; }
            // reajuste periódico al suelo: el terreno puede cargar después del spawn y el Y del servidor es aproximado
            if (Flying || vy != 0f || Time.time < nextGround) return; nextGround = Time.time + 0.25f + (GetInstanceID() & 7) * 0.02f;
            var q = transform.position; float gy = Ground.Y(q, 40f); if (Mathf.Abs(gy - q.y) > 0.01f) { q.y = Mathf.Lerp(q.y, gy, 0.6f); transform.position = q; target.y = q.y; }
        }

        void Update()
        {
            float dt = Time.deltaTime; var p = transform.position;
            if (npcMoving)
            {
                var d = target - p; float step = npcSpeed * dt;
                if (d.magnitude <= step) { p = target; npcMoving = false; }
                else
                {
                    p += d.normalized * step; var h = new Vector3(d.x, 0f, d.z);
                    if (h.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(h), TurnRate * dt);
                }
                transform.position = p; return;
            }
            target += dir * speed * dt;                                                   // el objetivo avanza a la velocidad del jugador
            if (vy != 0f) { target.y += vy * dt; vy -= Gravity * dt; if (vy < 0f && target.y <= p.y) vy = 0f; }   // salto aproximado (sin raycast de suelo)
            p = Vector3.Lerp(p, target, Mathf.Clamp01(Catchup * dt));
            transform.position = p;
            if (hasYaw) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.Euler(0f, yaw, 0f), TurnRate * dt);
        }
    }

    /// Como NpcAnimation del original: reposo = clip "stand1..4" aleatorio al terminar cada uno (40/30/20/10 %), humanoides con los 3 clips de la tabla (34/33/33 %),
    /// "walk"/"run" al desplazarse con las velocidades de animación de la tabla, y los clips "upper/melee/wound/death" con sus capas.
    public sealed class NpcAnimator : MonoBehaviour
    {
        /// <summary>iVehicle del jugador remoto (VehicleRide): 1 tabla ("board_"), 2/3 scooter ("scooter_"); 0 = a pie.</summary>
        public int Vehicle; RemoteMover rmv;
        public void SetVehicle(int t) { if (t == Vehicle) return; Vehicle = t; cur = null; }
        /// <summary>Modo inventario del jugador remoto (cnAvatarAnimation.SetInvenMode): clip "inven" (board_/scooter_ con vehiculo) en bucle mientras esta quieto.</summary>
        public bool Inven;
        public void SetInven(bool v)
        {
            if (v == Inven) return; Inven = v; cur = null; var sink = an != null ? an.GetComponent<AnimEventSink>() : null;
            if (sink != null) { sink.Persist = v; if (!v) sink.DeleteCurrentObject(); }
        }
        void VehicleTick()
        {   // StrModifyForVehicle: stand1 / run / jump con prefijo; el reposo siempre es stand1 (sin gestos)
            string pre = Vehicle == 1 ? "board_" : (Vehicle == 2 || Vehicle == 3) ? "scooter_" : null; if (pre == null) return;
            if (rmv == null) rmv = GetComponent<RemoteMover>();
            string want = rmv != null && rmv.Airborne ? pre + "jump" : moving ? pre + "run" : (Inven ? pre + "inven" : pre + "stand1");
            if (!Has(want)) want = pre + "stand1"; if (!Has(want)) return;
            if (cur == want && an.IsPlaying(want)) return;
            an[want].wrapMode = WrapMode.Loop; an.CrossFade(want, 0.3f); cur = want;
        }
        Animation an; string cur; bool moving, run; float walkSpeedAnim = 1f, runSpeedAnim = 1f, walkThreshold = 3.5f, motion; string[] hnpc;

        public void Init(GameObject model, NpcTableElement row, string[] hnpcStands)
        {
            an = model != null ? model.GetComponentInChildren<Animation>(true) : null; if (an == null) return;
            hnpc = hnpcStands; an.cullingType = AnimationCullingType.AlwaysAnimate;
            if (row != null) { if (row.m_fWalkAnimationSpeed > 0f) walkSpeedAnim = row.m_fWalkAnimationSpeed; if (row.m_fRunAnimationSpeed > 0f) runSpeedAnim = row.m_fRunAnimationSpeed; if (row.m_iWalkSpeed > 0 && row.m_iRunSpeed > row.m_iWalkSpeed) walkThreshold = (row.m_iWalkSpeed + row.m_iRunSpeed) * 0.005f; }
            foreach (AnimationState st in an)
            {
                var n = st.name;
                if (Player && n.IndexOf("sheath", System.StringComparison.Ordinal) >= 0) st.layer = 101;   // cnAvatarAnimation.MakeUperLayer
                else if (Player && n.IndexOf("wound", System.StringComparison.Ordinal) >= 0) { st.blendMode = AnimationBlendMode.Additive; st.layer = 104; st.weight = 1f; st.wrapMode = WrapMode.Once; }   // wound / woundupper aditivos (MakeUperLayer lo comprueba antes que 'upper')
                else if (n.IndexOf("upper", System.StringComparison.Ordinal) >= 0) st.layer = Player ? 101 : 100;
                else if (n.IndexOf("melee", System.StringComparison.Ordinal) >= 0) { st.blendMode = AnimationBlendMode.Additive; st.layer = 102; st.weight = 1f; st.wrapMode = WrapMode.Once; }
                else if (n.IndexOf("wound", System.StringComparison.Ordinal) >= 0) { st.blendMode = AnimationBlendMode.Additive; st.layer = 104; st.weight = 1f; st.wrapMode = WrapMode.Once; }
                else if (n.IndexOf("death", System.StringComparison.Ordinal) >= 0 || n.IndexOf("height", System.StringComparison.Ordinal) >= 0 || n.IndexOf("shape", System.StringComparison.Ordinal) >= 0 || n.IndexOf("in_make_out", System.StringComparison.Ordinal) >= 0) st.wrapMode = WrapMode.Once;
                else st.wrapMode = WrapMode.Loop;
                if (n == "walk") st.speed = walkSpeedAnim; else if (n == "run") st.speed = runSpeedAnim;
            }
            if (Player) BuildEmoteList(); else SetStand();
        }

        // ---- herida / muerte (NpcAnimation.DamageMotion / DeadMotion) ----
        public bool Dying; float dyingStart, deathLen = 1.5f;
        public float DeathRemaining() { return Dying ? Mathf.Max(0f, dyingStart + deathLen - Time.time) : 0f; }
        float actionUntil;
        public bool PlayAction(string clip)
        {
            if (an == null || Dying || string.IsNullOrEmpty(clip) || !Has(clip)) return false;
            var st = an[clip]; st.time = 0; st.speed = 1; st.wrapMode = WrapMode.Once;
            actionUntil = Time.time + Mathf.Max(.1f, st.length); an.CrossFade(clip, .1f); cur = clip; return true;
        }
        public void Wound() { if (an == null || Dying || !Has("wound")) return; an.Rewind("wound"); an.Play("wound"); }
        public void Die(Transform killer, float radius, float height)
        {
            if (an == null || Dying) return; Dying = true; dyingStart = Time.time; cur = "death";
            if (Has("death")) { an["death"].wrapMode = WrapMode.ClampForever; an["death"].layer = 101; an.CrossFade("death", 0.1f); deathLen = Mathf.Max(0.5f, an["death"].length); }
            if (Has("wound")) { an.Rewind("wound"); an.Play("wound"); }
            StartCoroutine(DeathSeq(killer, radius, height));
        }
        System.Collections.IEnumerator DeathSeq(Transform killer, float radius, float height)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, deathLen - 1f));
            // DeadMotion.Update: efecto 372 en el mob y balas 76 (taros) / 77 (blob) hacia quien lo mato; despues el modelo se hunde 1 m/s hasta los 10 s
            if (killer != null && BulletFx.Instance != null)
            {
                BulletFx.Instance.Spawn(372, transform.position, transform.rotation, Mathf.Max(0.5f, radius * 2f), null, 3f);
                BulletFx.Instance.Fire(76, transform, killer, 1.6f, killer.position + Vector3.up);
                BulletFx.Instance.Fire(77, transform, killer, 1.6f, killer.position + Vector3.up);
            }
            float t = 0f; var m = an != null ? an.transform : null;
            while (t < 9f && m != null) { m.position += Vector3.down * Time.deltaTime; t += Time.deltaTime; yield return null; }
        }

        // ---- jugador remoto (cnAvatarAnimation: AvatarMove / AvatarStand / EndAnimation / AvatarJump / Landing) ----
        /// <summary>true = avatar de otro jugador: reposo = stand1 en bucle con gestos stand2..4 (75 % al acabar cada vuelta), siempre 'run' (nunca 'walk'), 'runback' con las teclas 4..6, salto, prefijo de arma y respeto a los emotes.</summary>
        public bool Player; public int AniStyle;
        /// <summary>Otro sistema (tirolina, etc.) controla las animaciones de este avatar: el locomotor no toca nada mientras sea true.</summary>
        public bool External;
        byte moveKey; float landUntil, airSince, lastLoopN; bool wasAir; AnimationState[] emoteStates;
        public bool IsMoving { get { return moving; } }
        public bool IsAirborne { get { if (rmv == null) rmv = GetComponent<RemoteMover>(); return rmv != null && rmv.Airborne; } }
        public void SetKey(byte k) { moveKey = k; }
        public void SetStyle(int s) { if (s == AniStyle) return; AniStyle = s; cur = null; }
        static readonly string[] StylePre = { "", "stick", "pistol", "rifle", "rocket", "", "bomb" };
        static readonly string[] EmoteClips = { "agree", "angry", "applaud", "cheer", "cry", "flex", "goodbye", "hello", "kiss", "laugh", "no", "ok", "tease", "thank", "shocked", "scratch", "enough", "coin", "report", "observe", "foldedarms", "usestanding", "talk", "talkquestion", "talkexclamation" };
        string Sty(string c) { if (AniStyle <= 0 || AniStyle >= StylePre.Length || StylePre[AniStyle].Length == 0) return c; string n = StylePre[AniStyle] + c; return Has(n) ? n : c; }
        void BuildEmoteList() { var l = new List<AnimationState>(); foreach (AnimationState st in an) if (System.Array.IndexOf(EmoteClips, st.name) >= 0 || AvatarEmoteGuard.IsHeldName(st.name)) l.Add(st); emoteStates = l.ToArray(); }
        /// <summary>Hay un emote (EmoteSystem.PlayFor / sentado / baile) reproduciendose: el locomotor no debe pisarlo con otro reposo.</summary>
        bool EmoteBusy()
        {
            if (emoteStates == null) return false;
            for (int i = 0; i < emoteStates.Length; i++) { var s = emoteStates[i]; if (s != null && s.enabled && s.weight > 0.02f && an.IsPlaying(s.name)) return true; }
            return false;
        }
        bool IsFidget(string c) { return c != null && (c.EndsWith("stand2") || c.EndsWith("stand3") || c.EndsWith("stand4")); }
        void PlayP(string clip, WrapMode wm, float fade)
        {
            var s = an[clip]; if (s == null) return;
            if (!an.IsPlaying(clip)) s.time = 0f;
            s.wrapMode = wm; an.CrossFade(clip, fade); cur = clip; lastLoopN = Mathf.Floor(s.normalizedTime);
        }
        void PlayerTick()
        {
            if (rmv == null) rmv = GetComponent<RemoteMover>();
            if (Time.time < dashUntil) return;   // bDash: AvatarMove / AvatarStand / AvatarJump no hacen nada durante el segundo que dura el dash de un remoto
            bool air = rmv != null && rmv.Airborne;
            if (air)
            {   // AvatarJump: jumpstart (una vez) y a los 0,25 s jump (bucle)
                string js = Sty("jumpstart"), jp = Sty("jump");
                if (!wasAir) { wasAir = true; airSince = Time.time; if (Has(js)) PlayP(js, WrapMode.Once, 0.1f); else if (Has(jp)) PlayP(jp, WrapMode.Loop, 0.1f); }
                else if (Time.time - airSince > 0.25f && Has(jp) && cur != jp) PlayP(jp, WrapMode.Loop, 0.15f);
                return;
            }
            if (wasAir) { wasAir = false; string je = Sty("jumpend"); if (Has(je)) { PlayP(je, WrapMode.Once, 0.15f); landUntil = Time.time + 0.2f; } }
            if (moving)
            {   // AvatarMove: 'run' (o 'runback' con las teclas 4..6); el original nunca usa 'walk' con jugadores
                string want = Sty(moveKey >= 4 && moveKey <= 6 ? "runback" : "run"); if (!Has(want)) want = Sty("run"); if (!Has(want)) return;
                if (cur != want || !an.IsPlaying(want)) PlayP(want, WrapMode.Loop, 0.15f);
                return;
            }
            if (Time.time < landUntil) return;
            if (EmoteBusy()) { cur = null; return; }
            string s1 = Sty("stand1");
            if (IsAttackClip(cur))
            {   // attackN completo en marcha; al acabar, SetNextMotion lo lleva a 'ready' (attack1/2 -> ready; el resto, GetStandName = ready mientras suene la capa superior)
                var sa = an[cur];
                if (sa != null && an.IsPlaying(cur) && sa.normalizedTime < 1f) return;
                string rd = Sty("ready"); PlayP(Has(rd) ? rd : s1, WrapMode.Loop, 0.3f);
                return;
            }
            if (IsReadyClip(cur))
            {   // 'ready' en bucle; al acabar cada vuelta, 1/7 de volver a stand1 (EndAnimation: "ready" y RandomRange(0,7) == 0)
                var sr = an[cur]; if (sr == null) { cur = null; return; }
                if (!an.IsPlaying(cur)) { PlayP(cur, WrapMode.Loop, 0.3f); return; }
                int rn = Mathf.FloorToInt(sr.normalizedTime);
                if (rn > (int)lastLoopN) { lastLoopN = rn; if (UnityEngine.Random.Range(0, 7) == 0) PlayP(s1, WrapMode.Loop, 0.3f); }
                return;
            }
            if (cur == null || (cur != s1 && !IsFidget(cur))) { PlayP(s1, WrapMode.Loop, 0.3f); return; }
            var st = an[cur]; if (st == null) { cur = null; return; }
            if (cur == s1)
            {   // al acabar cada vuelta de stand1: 3/4 de las veces un gesto stand2..4 (RandomRange(0,4)), si no sigue stand1
                if (!an.IsPlaying(s1)) { PlayP(s1, WrapMode.Loop, 0.3f); return; }
                int n = Mathf.FloorToInt(st.normalizedTime);
                if (n > (int)lastLoopN) { lastLoopN = n; int r = UnityEngine.Random.Range(0, 4); if (r < 3) { string f = Sty("stand" + (r + 2)); if (Has(f)) PlayP(f, WrapMode.Once, 0.3f); } }
            }
            else if (st.normalizedTime >= 1f || !an.IsPlaying(cur)) PlayP(s1, WrapMode.Loop, 0.3f);   // SetNextMotion: tras un clip que no es bucle, GetStandName
        }

        // ---- combate del jugador remoto (cnAvatarAnimation.AvatarAttack / AvatarDash / AvatarDead / DamageMotion / ChangeWpn; los dispara RemoteCombatAnim) ----
        /// <summary>Muerto (AvatarDead): 'die' una vez y despues 'death' en bucle (tabla IsNextAnimationToNext: die -> death) hasta PlayerRevive (PC_REGEN) o hasta que se mueva.</summary>
        public bool Dead;
        /// <summary>Dentro del segundo que dura AvatarDash de un remoto (bDash): ni run ni stand pisan el dash.</summary>
        public bool Dashing { get { return Time.time < dashUntil; } }
        float deadAt, deadEnforceAt, dashUntil; int deadPhase, atkNum = 1; AnimationState upState;
        static bool IsAttackClip(string c) { return c != null && c.IndexOf("attack", System.StringComparison.Ordinal) >= 0; }
        static bool IsReadyClip(string c) { return c != null && c.EndsWith("ready", System.StringComparison.Ordinal); }
        /// <summary>Prefijo del arma SIN alternativa: GetAnimationName del original (si el attackN del arma no existe, vuelve a attack1 del arma).</summary>
        string Pre(string c) { if (AniStyle <= 0 || AniStyle >= StylePre.Length) return c; return StylePre[AniStyle] + c; }
        static float AtkSpeed(AnimationState s, float cool) { return cool > 0f && cool < s.length ? cool / s.length : 1f; }

        /// <summary>AvatarAttack(bUpdate: true) de otro jugador: si esta parado ("stand"/"ready") crossfade 0,15 al attackN completo del arma (SetAniTypeSpeed: dura como mucho 'cool' s) y SIEMPRE la capa superior attackNupper (101, corta la anterior).
        /// N rota 1,2,3.. y vuelve a 1 cuando falta el clip. Al acabar el completo -> ready (bucle, 1/7 de volver a stand1 por vuelta). Con un emote en marcha o corriendo solo suena la capa superior.</summary>
        public void PlayerAttack(float cool)
        {
            if (!Player || an == null || Dead || Dying || External || Vehicle > 0) return;
            if (rmv == null) rmv = GetComponent<RemoteMover>();
            bool air = rmv != null && rmv.Airborne;
            bool stand = !moving && !air && Time.time >= dashUntil && !EmoteBusy() && (cur == null || cur.IndexOf("stand", System.StringComparison.Ordinal) >= 0 || cur.IndexOf("ready", System.StringComparison.Ordinal) >= 0);
            string full = Pre("attack" + atkNum);
            if (!Has(full)) { atkNum = 1; full = Pre("attack1"); }
            string up = Pre("attack" + atkNum + "upper");
            if (!Has(up)) { atkNum = 1; up = Pre("attack1upper"); }
            if (!Has(full)) full = Sty("attack1"); if (!Has(up)) up = Sty("attack1upper");
            if (stand && Has(full)) { var sm = an[full]; sm.speed = AtkSpeed(sm, cool); PlayP(full, WrapMode.Once, 0.15f); }
            if (Has(up))
            {
                if (upState != null && an.IsPlaying(upState.name)) an.Stop(upState.name);
                var su = an[up]; su.layer = 101; su.wrapMode = WrapMode.Once; su.speed = AtkSpeed(su, cool); su.time = 0f; an.CrossFade(up, 0.15f); upState = su;
            }
            atkNum++;
        }

        /// <summary>DamageMotion: woundupper (con el prefijo del arma) aditivo en la capa 104, salvo que este muerto.</summary>
        public void PlayerWound()
        {
            if (!Player || an == null || Dead || Dying || External) return;
            string w = Pre("woundupper"); if (!Has(w)) w = "woundupper"; if (!Has(w)) return;
            an.Rewind(w); an.Play(w);
        }

        /// <summary>cnAvatarAnimation.AvatarDash (otro jugador, NANO_SKILL_USE de tipo 40): bDash 1 s; Play(stickdash / rifledash / rifletumbling segun el arma) y a continuacion, como bJumpFlag es true en remotos,
        /// CrossFade 0,1 a rocketsomersault + capa superior dodgeupper (rifle: rifledodgeupper; cohete: ninguna; resto: stickdodgeupper).</summary>
        public void PlayerDash()
        {
            if (!Player || an == null || Dead || Dying || External) return;
            dashUntil = Time.time + 1f; wasAir = false; landUntil = 0f;
            string dc = AniStyle == 3 ? "rifledash" : AniStyle == 2 ? "rifletumbling" : "stickdash";
            if (Has(dc)) { var sd = an[dc]; sd.wrapMode = WrapMode.Once; sd.time = 0f; an.Play(dc); cur = dc; }
            if (Has("rocketsomersault")) { var ss = an["rocketsomersault"]; ss.wrapMode = WrapMode.Once; ss.time = 0f; an.CrossFade("rocketsomersault", 0.1f); cur = "rocketsomersault"; }
            string du = AniStyle == 3 ? "rifledodgeupper" : AniStyle == 4 ? null : "stickdodgeupper";
            if (du != null && Has(du)) { var su = an[du]; su.layer = 101; su.wrapMode = WrapMode.Once; su.time = 0f; an.CrossFade(du, 0f); upState = su; }
        }

        /// <summary>ChangeWpn: CrossFadeQueued("sheath", 0,15) en la capa 101 al cambiar el arma de otro jugador.</summary>
        public void PlayerSheath()
        {
            if (!Player || an == null || Dead || Dying || External || Vehicle > 0 || Time.time < dashUntil || !Has("sheath")) return;
            var ss = an["sheath"]; ss.layer = 101; ss.wrapMode = WrapMode.Once; ss.time = 0f; an.CrossFade("sheath", 0.15f);
        }

        /// <summary>AvatarDead: corta emotes y capas superiores, crossfade 0,15 a 'die' (una vez) y despues 'death' en bucle. Se queda en el suelo hasta PlayerRevive.</summary>
        public void PlayerDie()
        {
            if (!Player || an == null || Dead) return;
            Dead = true; deadAt = Time.time; deadPhase = 0; wasAir = false; dashUntil = 0f; landUntil = 0f;
            if (upState != null && an.IsPlaying(upState.name)) an.Stop(upState.name);
            if (Has("die")) { PlayP("die", WrapMode.Once, 0.15f); deadPhase = 1; }
            else if (Has("death")) { PlayP("death", WrapMode.Loop, 0.15f); deadPhase = 2; deadEnforceAt = Time.time + 0.8f; }
        }

        /// <summary>PC_REGEN (ReceiveRegen): GotoDir(0) + AvatarJump() => crossfade 0,15 a jumpstart. Aqui el 'jump' en bucle que dejaria el original hasta el siguiente PC_STOP/MOVE se sustituye por stand1 al acabar jumpstart.</summary>
        public void PlayerRevive(bool jumpStart = true)
        {
            if (!Player || an == null) return;
            Dead = false; deadPhase = 0; dashUntil = 0f; wasAir = false; cur = null;
            if (!jumpStart || Vehicle > 0) return;
            string js = Sty("jumpstart");
            if (Has(js)) { PlayP(js, WrapMode.Once, 0.15f); landUntil = Time.time + Mathf.Max(0.3f, an[js].length); }
        }

        void DeadTick()
        {
            if (moving && Time.time - deadAt > 2f) { PlayerRevive(false); return; }   // seguridad: un muerto no camina (se perdio el PC_REGEN)
            if (deadPhase == 1)
            {
                var sd = an["die"];
                if (sd == null || !an.IsPlaying("die") || sd.normalizedTime >= 1f)
                {
                    if (Has("death")) { PlayP("death", WrapMode.Loop, 0.3f); deadPhase = 2; deadEnforceAt = Time.time + 0.8f; } else deadPhase = 3;
                }
                else if (sd.weight < 0.5f && Time.time - deadAt > 0.4f) an.CrossFade("die", 0.1f);   // otro sistema (RestoreRemote de un emote..) cruzo a otro clip
            }
            else if (deadPhase == 2 && Time.time >= deadEnforceAt)
            {
                var sd = an["death"];
                if (sd == null || !an.IsPlaying("death") || sd.weight < 0.9f) { PlayP("death", WrapMode.Loop, 0.15f); deadEnforceAt = Time.time + 0.6f; }
            }
        }

        public void SetMotion(float horizontalSpeed) { motion = horizontalSpeed; moving = horizontalSpeed > 0.25f; run = horizontalSpeed > walkThreshold; }

        bool Has(string clip) => an != null && an[clip] != null;
        void Play(string clip, float fade) { if (!Has(clip)) return; cur = clip; an.CrossFade(clip, fade); }

        void SetStand()
        {
            int r = UnityEngine.Random.Range(0, 100); string pick;
            if (hnpc != null && hnpc.Length == 3 && !string.IsNullOrEmpty(hnpc[0])) { pick = r < 34 ? hnpc[0] : r < 67 ? hnpc[1] : hnpc[2]; if (string.IsNullOrEmpty(pick) || pick == cur) pick = hnpc[0]; }
            else { pick = r < 40 ? "stand1" : r < 70 ? "stand2" : r < 90 ? "stand3" : "stand4"; if (pick == cur) pick = "stand1"; }
            if (!Has(pick)) pick = "stand1";
            if (!Has(pick)) return;
            an[pick].wrapMode = WrapMode.Once; Play(pick, 0.3f);
        }

        void Update()
        {
            if (an == null || Dying || External) return;
            if (!Player && Time.time < actionUntil) return;
            if (Player && Dead) { DeadTick(); return; }
            if (Vehicle > 0) { VehicleTick(); return; }
            if (Inven && !moving && Has("inven")) { if (cur != "inven" || !an.IsPlaying("inven")) { an["inven"].wrapMode = WrapMode.Loop; an.CrossFade("inven", 0.1f); cur = "inven"; } return; }
            if (Player) { PlayerTick(); return; }
            bool locomotion = cur == "walk" || cur == "run";
            if (moving)
            {
                string want = run && Has("run") ? "run" : Has("walk") ? "walk" : Has("run") ? "run" : null;
                if (want != null && cur != want) Play(want, 0.2f);
                return;
            }
            if (locomotion || cur == null) { SetStand(); return; }
            var st = an[cur]; if (st == null || st.normalizedTime >= 1f || !an.IsPlaying(cur)) SetStand();   // el original encadena otro reposo al terminar el clip
        }
    }
}
