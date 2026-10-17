// Sistema de vehiculos (tabla/scooter) = GameFrame (VEHICLE_ON/OFF_SUCC/FAIL, PC_STATE_CHANGE, PC_NEW, LoadVehicle) + Status.UpdateVehicleSpd + cnAvatarAttack.VehicleToggle
// + cnAvatarAnimation.StrModifyForVehicle + UserMoveController/cnAvatarStatus (inclinacion al terreno). SIN COMPILAR NI PROBAR EN UNITY.
//
// Protocolo (servidor OpenFusion): REQ_PC_VEHICLE_ON/OFF (318767263/4, cuerpo vacio) -> SUCC/FAIL al propio jugador; a los demas llega PC_STATE_CHANGE {iPC_ID, sbyte iState}
// con el bit 8 (= iPCState |= 8) y NO existe un VEHICLE_ON/OFF de difusion. PC_NEW/PC_AROUND traen sPCAppearanceData.iPCState (8 = en vehiculo) y aEquip[8] (el vehiculo).
// El servidor solo acepta ON si Equip[8].iID > 0 y en el mundo abierto (instanceID == 0; fuera de eso ignora la peticion sin FAIL), baja solo al desequipar el vehiculo,
// al caducar (iTimeLimit -> PC_DELETE_TIME_LIMIT_ITEM) y al usar un warp de NPC.
//
// Original replicado:
//  * ON_SUCC: oculta el arma (LinkObjectActive 3), iVehicle = ItemElement.m_iEquipType (1 tabla "board_", 2/3 scooter "scooter_"), accel = m_iUp_swimSpeed, efecto 832, FirstUse 72, carga el modelo
//    wear/<m_iMesh>.nif en el hueso "vehicle" (rotado 90 en X), UpdateVehicleSpd (BaseAttrib.iRunSpeed = m_iUp_runSpeed; vehicleSpeed = Distance_SToC), desactiva el nano activo, cooltimes 9/10 (0,5 s),
//    sonido Vehicle_GetOn y bucle de motor (m_iSound1, pitch = max(1, v / (vehicleSpeed * 1.5) * 2)).
//  * OFF_SUCC: iVehicle = 0, efecto 832, arma visible, destruye modelo/sonido, velocidad normal, sonido Vehicle_GetOff.
//  * Otros jugadores: PC_NEW con iPCState == 8 / PC_STATE_CHANGE con estado 8 (apagan 0,1,2,3) -> mismo modelo y animaciones (NpcAnimator.Vehicle).
//  * Restricciones: atacar (clic/Z) con vehiculo = bajar (FirstUse 70) en vez de atacar; TAB (cambio de arma), invocar nanos (NanoWheelHud.InVehicle), emotes (EmoteSystem) y apuntar (rocket/granada) bloqueados;
//    mover/desequipar el vehiculo (slot 8) estando montado envia VEHICLE_OFF; en instancias (bInsMap), con animacion especial (escoba/dash) o, desde el menu/My Stuff, en combate: sonido Action_Failure01.
//  * Caducidad: PC_DELETE_TIME_LIMIT_ITEM -> mensaje 257 "Item Update" (NanoMessages, tipo Etc = 8), FirstUse 71, se para el motor.
//  * Tecla de atajo: ControlMap.K.VehicleToggle (V por defecto; eje de mando incluido) = cnAvatarAttack.VehicleToggle.
// No portado (ver doc): inclinacion lateral al girar (portada con los valores por defecto de UserMoveController: el inspector podria diferir),
// tipo 3 (sin gravedad en remotos), tutorial (cntutorialscript.bTutorial).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    /// <summary>Mantiene la escala MUNDO del modelo del vehiculo en 1 (legacy: transform.parent = hueso con worldPositionStays), compensando la escala del hueso "vehicle" (animada / heredada).</summary>
    public sealed class VehicleWorldScale : MonoBehaviour
    {
        void LateUpdate()
        {
            var p = transform.parent; if (p == null) return; var ps = p.lossyScale;
            if (Mathf.Abs(ps.x) < 1e-4f || Mathf.Abs(ps.y) < 1e-4f || Mathf.Abs(ps.z) < 1e-4f) return;
            // el hijo va girado 90 en X respecto al hueso: los ejes Y/Z del hijo corresponden a Z/Y del padre
            transform.localScale = new Vector3(1f / ps.x, 1f / ps.z, 1f / ps.y);
        }
    }

    public sealed class VehicleRide : MonoBehaviour
    {
        public static VehicleRide Instance;
        /// <summary>cnAvatarAnimation.iVehicle > 0 del jugador propio.</summary>
        public static volatile bool SelfRiding;
        /// <summary>iVehicle del jugador propio (0 a pie, 1 tabla, 2/3 scooter).</summary>
        public static volatile int SelfType;
        /// <summary>Hook: true = el jugador esta en un mapa de instancia (cnOwnAvatarStatus.bInsMap). Sin cablear = nunca.</summary>
        public static Func<bool> InstanceMap;
        public Func<ModelFactory> GetModels; public Func<PlayerAvatarFactory> GetAvatars;

        const int EffectOnOff = 832, ExpireMessage = 257, RightPistolSlot = 3;

        sealed class Rider
        {
            public int Id; public bool Want; public ItemBase Equip8; public Transform Holder; public GameObject Model; public int BuiltItem; public float Retry, WeaponAt;
            public bool WeaponHidden, Tilted, Active; public Transform ModelRoot; public bool LoggedMissing;
            public int Type; public Vector3 LastPos, LastDir; public bool HasLast; public float Lean;
        }
        readonly Dictionary<int, Rider> riders = new Dictionary<int, Rider>();
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();
        WorldEntities ents; PacketDispatcher disp; XdtTableScript tables; ModelCatalog cat; GameClient gc;
        /// <summary>GameCondition.SetUpdateCoolTime(9) al montar (0,5 s): bloquea invocar nanos con las teclas de slot.</summary>
        public static float NanoCoolUntil;
        ItemBase selfEq8; float coolOnUntil, offAt; AudioSource engine; int engineGen; bool enginePitchFixed;

        public static VehicleRide Ensure(GameObject host) { var v = host.GetComponent<VehicleRide>(); if (v == null) v = host.AddComponent<VehicleRide>(); Instance = v; return v; }

        public void Init(WorldEntities e, PacketDispatcher d, XdtTableScript t, ModelCatalog c)
        {
            Detach(); ents = e; disp = d; tables = t; cat = c; Instance = this;
            if (disp != null) { disp.Unhandled += OnPacket; disp.PcEnter += OnPcEnter; disp.PcExit += OnPcExit; }
            if (ents != null) ents.SelfSpawned += OnSelfSpawned;
            var nw = GetComponent<NanoWheelHud>(); if (nw != null) nw.InVehicle = () => SelfRiding;   // cnOwnAvatarStatus.Update: iVehicle > 0 -> Nano_FailSummon
        }
        void Detach()
        {
            if (disp != null) { disp.Unhandled -= OnPacket; disp.PcEnter -= OnPcEnter; disp.PcExit -= OnPcExit; }
            if (ents != null) ents.SelfSpawned -= OnSelfSpawned;
        }
        void OnDestroy() { Detach(); StopEngine(); if (Instance == this) Instance = null; SelfRiding = false; SelfType = 0; }

        GameClient Gc() { if (gc == null) gc = FindObjectOfType<GameClient>(); return gc; }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }
        static ItemBase It(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }
        Rider Get(int id) { Rider r; if (!riders.TryGetValue(id, out r)) { r = new Rider { Id = id }; riders[id] = r; } return r; }

        /// <summary>Fila del vehiculo (ItemElement de la tabla de vehiculos) por numero de item.</summary>
        ItemElement Row(int id)
        {
            var t = tables != null ? tables.m_pVehicleItemTable : null; var d = t != null ? t.m_pItemData : null; if (d == null || id <= 0) return null;
            if (id < d.Length && d[id] != null && d[id].m_iItemNumber == id) return d[id];
            foreach (var e in d) if (e != null && e.m_iItemNumber == id) return e;
            return null;
        }
        string Message(int id)
        {
            var mt = tables != null ? tables.m_pMessageTable : null; var m = mt != null ? mt.m_pMessageData : null;
            return m != null && id >= 0 && id < m.Length && m[id] != null && !string.IsNullOrEmpty(m[id].m_szString) ? m[id].m_szString : "(message " + id + ")";
        }

        // ============================================================ red (hilo de red: solo encolar) ============================================================
        void OnPacket(uint type, byte[] b)
        {
            if (b == null) b = new byte[0];
            if (type == PacketIds.P_FE2CL_PC_VEHICLE_ON_SUCC) q.Enqueue(() => SelfSet(true));
            else if (type == PacketIds.P_FE2CL_PC_VEHICLE_OFF_SUCC) q.Enqueue(() => SelfSet(false));
            else if (type == PacketIds.P_FE2CL_PC_VEHICLE_ON_FAIL) q.Enqueue(() => Debug.Log("[vehiculo] VEHICLE_ON_FAIL (sin vehiculo equipado en el slot 8)"));
            else if (type == PacketIds.P_FE2CL_PC_VEHICLE_OFF_FAIL) q.Enqueue(() => Debug.Log("[vehiculo] VEHICLE_OFF_FAIL"));
            else if (type == PacketIds.P_FE2CL_PC_STATE_CHANGE && b.Length >= 5) { int id = I(b, 0); int st = (sbyte)b[4]; q.Enqueue(() => RemoteState(id, st)); }
            else if (type == PacketIds.P_FE2CL_PC_EQUIP_CHANGE && b.Length >= 20) { int id = I(b, 0), sl = I(b, 4); var item = It(b, 8); if (sl == 8) q.Enqueue(() => EquipChange(id, item)); }
            else if (type == PacketIds.P_FE2CL_PC_ITEM_MOVE_SUCC && b.Length >= 40)
            {   // eFrom, iFromSlotNum, FromSlotItem(12), eTo, iToSlotNum, ToSlotItem(12); eIL 0 = equipo
                int ef = I(b, 0), fs = I(b, 4), et = I(b, 20), ts = I(b, 24); var i1 = It(b, 8); var i2 = It(b, 28);
                if ((ef == 0 && fs == 8) || (et == 0 && ts == 8)) q.Enqueue(() =>
                {
                    if (ef == 0 && fs == 8) selfEq8 = i1; if (et == 0 && ts == 8) selfEq8 = i2;
                    if (ents != null) { Rider r; if (riders.TryGetValue(ents.SelfId, out r) && r.Want) r.Equip8 = selfEq8; }
                    if (SelfRiding) SendOff();   // PC_ITEM_MOVE_SUCC con iToSlotNum == 8 / desequipar el vehiculo: GameFrame/UnequipPopup mandan REQ_PC_VEHICLE_OFF
                });
            }
            else if (type == PacketIds.P_FE2CL_PC_DELETE_TIME_LIMIT_ITEM && b.Length >= 4)
            {   // iItemListCount + sTimeLimitItemDeleteInfo2CL {eIL, iSlotNum}[]
                int n = Math.Min(I(b, 0), (b.Length - 4) / 8); bool veh = false; for (int i = 0; i < n; i++) if (I(b, 4 + 8 * i) == 0 && I(b, 8 + 8 * i) == 8) veh = true;
                bool any = n > 0; q.Enqueue(() => Expired(any, veh));
            }
        }
        void OnPcEnter(PcAppearance a)
        {
            if (a == null) return; int id = a.Id; int st = (int)a.State; var eq = a.Equip != null && a.Equip.Length > 8 ? a.Equip[8] : default(ItemBase);
            q.Enqueue(() => { if (ents != null && id == ents.SelfId) return; var r = Get(id); r.Equip8 = eq; ApplyState(r, st); });
        }
        void OnPcExit(int id) { q.Enqueue(() => { Rider r; if (riders.TryGetValue(id, out r)) { if (r.Model != null) Destroy(r.Model); riders.Remove(id); } }); }

        // ============================================================ hilo principal ============================================================
        void OnSelfSpawned(GameObject go, EnterSucc es)
        {
            selfEq8 = es.Self != null && es.Self.Equip != null && es.Self.Equip.Length > 8 ? es.Self.Equip[8] : default(ItemBase);
            SelfRiding = false; SelfType = 0; StopEngine(); var g = Gc(); if (g != null) g.SetVehicle(0, 0);
            if (ents != null) { Rider r; if (riders.TryGetValue(ents.SelfId, out r)) r.Want = false; }
        }
        /// <summary>GameFrame PC_NEW / PC_STATE_CHANGE: 8 = vehiculo; 0, 1, 2, 3 lo quitan; otros estados no tocan nada. Con el bit 8 activo (el servidor hace iPCState |= 8) tambien se monta.</summary>
        static void ApplyState(Rider r, int st) { if (st == 8 || (st & 8) != 0) r.Want = true; else if (st == 0 || st == 1 || st == 2 || st == 3) r.Want = false; }
        void RemoteState(int id, int st) { if (ents != null && id == ents.SelfId) return; ApplyState(Get(id), st); }
        void EquipChange(int id, ItemBase item)
        {
            if (ents != null && id == ents.SelfId) { selfEq8 = item; Rider rs; if (riders.TryGetValue(id, out rs) && rs.Want) rs.Equip8 = item; return; }
            Get(id).Equip8 = item;
        }

        void SelfSet(bool on)
        {
            if (ents == null) return; var r = Get(ents.SelfId); var g = Gc(); var tr = ents.Self != null ? ents.Self.transform : null;
            if (on)
            {
                var row = Row(selfEq8.Id);
                if (row == null) { Debug.LogWarning("[vehiculo] VEHICLE_ON_SUCC pero el slot 8 no tiene un vehiculo conocido (id " + selfEq8.Id + ")"); return; }
                r.Equip8 = selfEq8; r.Want = true; SelfRiding = row.m_iEquipType > 0; SelfType = row.m_iEquipType;
                if (g != null) g.SetVehicle(row.m_iEquipType, row.m_iUp_runSpeed);   // iVehicle, AvatarMoveForce(0), UpdateVehicleSpd
                Fx(tr); FirstUse.Check(72); SendNanoOff(); coolOnUntil = Time.time + 0.5f; NanoCoolUntil = Time.time + 0.5f; Sfx.Ui("Vehicle_GetOn"); StartEngine(row);
                Debug.Log("[vehiculo] ON tipo=" + row.m_iEquipType + " item=" + selfEq8.Id + " runSpeed=" + row.m_iUp_runSpeed + " accel(m_iUp_swimSpeed)=" + row.m_iUp_swimSpeed);
            }
            else
            {
                r.Want = false; SelfRiding = false; SelfType = 0; if (g != null) g.SetVehicle(0, 0);
                Fx(tr); StopEngine(); Sfx.Ui("Vehicle_GetOff"); Debug.Log("[vehiculo] OFF");
            }
        }
        static void Fx(Transform tr) { if (tr == null || BulletFx.Instance == null) return; try { BulletFx.Instance.Spawn(EffectOnOff, tr.position, tr.rotation, 1f, null, 3f); } catch (Exception) { } }
        void Expired(bool any, bool veh)
        {
            if (veh) { selfEq8 = default(ItemBase); StopEngine(); }
            if (any) { NanoMessages.Post("Item Update", Message(ExpireMessage), NanoMessages.Kind.Etc, 5f, null); FirstUse.Check(71); }
        }

        // ============================================================ envio / entrada ============================================================
        static void Send(byte[] p) { var s = NpcUI.Sender; if (s != null) { try { s(p); } catch (Exception ex) { Debug.LogWarning("[vehiculo] envio: " + ex.Message); } } }
        static void SendOn() { Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VEHICLE_ON, 1)); }
        static void SendOff() { Send(ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_VEHICLE_OFF, 1)); }
        void SendNanoOff()
        {   // GameFrame: iCurActiveNanoSlot >= 0 -> REQ_NANO_ACTIVE { iNanoSlotNum = -1 }
            var hud = GetComponent<PlayerHud>();
            if (hud != null && hud.Data.ActiveNanoSlot >= 0) { var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_ACTIVE, 2); p[4] = 0xFF; p[5] = 0xFF; Send(p); }
        }

        bool InsMap() { var f = InstanceMap; return RaceSystem.InsMap || (f != null && f()); }   // bInsMap: INSTANCE_MAP_INFO lo activa, WARP_USE_NPC_SUCC / entrada al mundo lo apagan (RaceSystem)
        bool Special() { var g = Gc(); return BroomFlight.Active || (g != null && g.Dashing); }   // IsSpecialAnimation: escoba, tirolina, cuerda, dash
        bool InCombat() { var g = Gc(); return g != null && Time.time < g.CombatUntil; }          // GameCondition.IsEventCondition(eC_Combat)
        bool HasVehicle() { return selfEq8.Id > 0; }

        /// <summary>Boton/atajo de HOP ON / HOP OFF. fromMenu = panel izquierdo del menu o My Stuff (CnGuiChat.OnVehicle / Panel_UserClothes: tambien se bloquea en combate). Devuelve false si el sistema no esta listo.</summary>
        public static bool Toggle(bool fromMenu) { if (Instance == null) return false; Instance.DoToggle(fromMenu); return true; }
        void DoToggle(bool fromMenu)
        {
            if (!SelfRiding && !HasVehicle()) { Sfx.Ui("Action_Failure01"); return; }
            if (InsMap() || Special() || (fromMenu && InCombat())) { Sfx.Ui("Action_Failure01"); return; }
            if (Time.time - offAt < 0.15f) return;   // anti-rebote del doble clic
            offAt = Time.time;
            if (SelfRiding) SendOff();
            else if (Time.time >= coolOnUntil) SendOn();   // !GameCondition.IsCoolTime(10)
        }
        /// <summary>cnAvatarAttack.Update: atacar / hablar con vehiculo = bajarse (FirstUse 70). Throttle de 0,5 s para no inundar al servidor mientras se mantiene el boton.</summary>
        public static void HopOffFromAction() { if (Instance != null) Instance.OffFromAction(); }
        void OffFromAction() { if (!SelfRiding || Time.unscaledTime - lastActionOff < 0.5f) return; lastActionOff = Time.unscaledTime; SendOff(); FirstUse.Check(70); }
        float lastActionOff = -10f;
        /// <summary>Panel_UserClothes: el boton esta habilitado si no hay instancia ni animacion especial (el combate lo comprueba quien lo llama).</summary>
        public static bool CanUseUi() { var v = Instance; return v == null || (!v.InsMap() && !v.Special()); }
        /// <summary>true si el jugador (propio o ajeno) va montado en un vehiculo (cnAvatarAnimation.iVehicle > 0).</summary>
        public static bool IsRiding(int id) { var v = Instance; Rider r; return v != null && v.riders.TryGetValue(id, out r) && r.Active; }

        void Update()
        {
            while (q.TryDequeue(out var a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[vehiculo] " + ex.Message); } }
            if (ents == null) return;
            if (!ChatHud.Typing && !NpcUI.Active && !WelcomePopup.Active && !GameMenu.Blocking && ControlMap.Down(ControlMap.K.VehicleToggle, false)) DoToggle(false);   // cnAvatarAttack: Key 37
            foreach (var kv in riders) Reconcile(kv.Value);
            if (engine != null)
            {   // cnAvatarThirdPersonMove.UpdateVehicleMovement: pitch = max(1, v / (vehicleSpeed * 1.5) * 2) salvo vehicle_timesquad
                var g = Gc(); if (g != null && !enginePitchFixed && g.VehicleSpeed > 0.01f) engine.pitch = Mathf.Max(1f, g.VehicleMomentumMag / (g.VehicleSpeed * 1.5f) * 2f);
            }
        }

        /// <summary>Hace que cada jugador refleje su estado: modelo del vehiculo en el hueso "vehicle", arma oculta y animaciones de los remotos.</summary>
        void Reconcile(Rider r)
        {
            var tr = ents.TransformOfPlayer(r.Id); bool own = r.Id == ents.SelfId;
            if (tr == null) { r.Holder = null; r.Model = null; r.ModelRoot = null; r.WeaponHidden = false; r.Tilted = false; r.Active = false; return; }
            if (tr != r.Holder) { r.Holder = tr; r.Model = null; r.ModelRoot = null; r.BuiltItem = 0; r.WeaponHidden = false; r.Tilted = false; r.Retry = 0f; }
            var row = r.Want ? Row(r.Equip8.Id) : null; int type = row != null ? row.m_iEquipType : 0; bool should = type > 0; r.Active = should; r.Type = type;
            if (!own) { var na = tr.GetComponent<NpcAnimator>(); if (na != null) na.SetVehicle(should ? type : 0); }
            if (!should)
            {
                if (r.Model != null) { Destroy(r.Model); r.Model = null; r.BuiltItem = 0; }
                if (r.WeaponHidden) { HideWeapon(tr, false); r.WeaponHidden = false; }
                if (r.Tilted) { ResetTilt(r, own); r.Tilted = false; }
                return;
            }
            if (r.Model != null && r.BuiltItem != r.Equip8.Id) { Destroy(r.Model); r.Model = null; }
            if (r.Model == null && Time.unscaledTime >= r.Retry)
            {
                r.Retry = Time.unscaledTime + 1.5f; r.Model = BuildModel(r, tr, row); if (r.Model != null) r.BuiltItem = r.Equip8.Id;
            }
            if (Time.time >= r.WeaponAt) { r.WeaponAt = Time.time + 0.25f; HideWeapon(tr, true); r.WeaponHidden = true; }   // cnAvatarAnimation.Update: iVehicle > 0 -> LinkObjectActive(3, false) cada frame
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase)) return root;
            for (int i = 0; i < root.childCount; i++) { var f = FindDeep(root.GetChild(i), name); if (f != null) return f; }
            return null;
        }
        static void HideWeapon(Transform holder, bool hide)
        {   // ActorSkinCombiner.LinkObjectActive(AttachName 3 = mano derecha / arma, !hide)
            foreach (var tg in holder.GetComponentsInChildren<ActorAttachTag>(true))
                if (tg.Slot == RightPistolSlot) foreach (var rd in tg.GetComponentsInChildren<Renderer>(true)) rd.enabled = !hide;
        }

        /// <summary>GameFrame.LoadVehicle: modelo wear/&lt;MMesh&gt;.nif, textura MTexture en los materiales "main" y "sub", hijo del hueso "vehicle" con rotacion 90 en X, animaciones en bucle y estelas en tag01..tag04.</summary>
        GameObject BuildModel(Rider r, Transform holder, ItemElement row)
        {
            try
            {
                var mf = GetModels != null ? GetModels() : null; if (mf == null || cat == null) return null;
                var mr = cat.Item(EquipSlot.Vehicle, row.m_iItemNumber); if (mr == null || mr.Model == null) { if (!r.LoggedMissing) { r.LoggedMissing = true; Debug.LogWarning("[vehiculo] sin ruta de modelo para el item " + row.m_iItemNumber); } return null; }
                var rf = mf.Locate(mr.Model); if (!rf.Ok) { if (!r.LoggedMissing) { r.LoggedMissing = true; Debug.LogWarning("[vehiculo] modelo no disponible (aun no descargado?): " + mr.Model); } return null; }
                var go = mf.Builder.BuildPrefabAt(rf, null); if (go == null) return null;
                WorldEntities.ActivateAll(go); go.SetActive(true);
                var bone = FindDeep(holder, "vehicle");
                go.transform.SetParent(bone != null ? bone : holder, false);
                go.transform.localPosition = Vector3.zero; go.transform.localRotation = bone != null ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity; go.transform.localScale = Vector3.one;
                if (bone != null) go.AddComponent<VehicleWorldScale>();   // legacy: Instantiate en mundo + parent = hueso => escala mundo del prefab (1), no la del hueso animado
                int layer = holder.gameObject.layer; foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                Texture main = null; try { if (mr.Tex1 != null) main = mf.Texture(mr.Tex1); } catch (Exception) { }
                var av = GetAvatars != null ? GetAvatars() : null; var asm = av != null ? av.Assembler : null;
                foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (rd is ParticleSystemRenderer) continue; rd.enabled = true;
                    foreach (var m in rd.materials)
                    {
                        bool isMain = m.name.Contains("main"), isSub = m.name.Contains("sub");
                        if ((isMain || isSub) && main != null) { if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", main); else m.mainTexture = main; }
                        if (m.HasProperty("_SpecMap") && asm != null) { if (isMain && asm.MainSpec != null) m.SetTexture("_SpecMap", asm.MainSpec); else if (isSub && asm.SubSpec != null) m.SetTexture("_SpecMap", asm.SubSpec); }
                    }
                }
                foreach (var a2 in go.GetComponentsInChildren<Animation>(true)) { a2.cullingType = AnimationCullingType.AlwaysAnimate; foreach (AnimationState st in a2) st.wrapMode = WrapMode.Loop; a2.Play(); }
                string mesh = System.IO.Path.GetFileNameWithoutExtension(mr.Model);   // "vehicle_hoverboard"
                int fx = 15; if (mesh == "vehicle_hoverboard") fx = 835; else if (mesh == "vehicle_cloud" || mesh == "vehicle_hoverboard2") fx = 836; else if (mesh == "vehicle_jetbike") fx = 15;
                if (BulletFx.Instance != null) foreach (var tagName in new[] { "tag01", "tag02", "tag03", "tag04" })
                { var tg = FindDeep(go.transform, tagName); if (tg != null) BulletFx.Instance.Spawn(fx, tg.position, tg.rotation, 1f, tg, 3600f); }
                if (r.Id == (ents != null ? ents.SelfId : -1)) enginePitchFixed = mesh == "vehicle_timesquad";
                Debug.Log("[vehiculo] modelo montado: " + mr.Model + " en " + (bone != null ? bone.name : "raiz") + " (jugador " + r.Id + ", tex " + (main != null ? main.name : "-") + ", efecto " + fx + ")");
                return go;
            }
            catch (Exception ex) { Debug.LogWarning("[vehiculo] " + ex.Message); return null; }
        }

        // ============================================================ motor ============================================================
        void StartEngine(ItemElement row)
        {   // LoadVehicle (solo propio): "UI sound/" + SoundElement(m_iSound1).m_pstrSoundString1, bucle 2D, volumen maestro
            StopEngine();
            try
            {
                var t = tables.m_pVehicleItemTable; var sd = t != null ? t.m_pItemSoundData : null; if (sd == null || row.m_iSound1 < 0 || row.m_iSound1 >= sd.Length || sd[row.m_iSound1] == null) return;
                string s = sd[row.m_iSound1].m_pstrSoundString1; if (string.IsNullOrEmpty(s) || s == "null") return; s = s.Trim().ToLowerInvariant(); if (s.IndexOf('.') < 0) s += ".wav";
                if (Sfx.Instance == null) return; int gen = ++engineGen;
                Sfx.Instance.Play2D("ui sound/" + s, 1f, true, a => { if (a == null) return; if (gen != engineGen) { Destroy(a.gameObject); return; } engine = a; });
            }
            catch (Exception ex) { Debug.LogWarning("[vehiculo] motor: " + ex.Message); }
        }
        void StopEngine() { engineGen++; if (engine != null) { engine.Stop(); Destroy(engine.gameObject); engine = null; } }

        // ============================================================ inclinacion al terreno (UserMoveController.ConformVehicleToSlope) ============================================================
        static readonly RaycastHit[] rh = new RaycastHit[16];
        static bool GroundNormal(Vector3 from, Transform self, out Vector3 n)
        {
            n = Vector3.up; bool ok = false; float best = float.MaxValue;
            int cnt = Physics.RaycastNonAlloc(from, Vector3.down, rh, 4f, ~((1 << 2) | (1 << 30)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < cnt; i++)
            {
                var h = rh[i]; if (h.collider == null || h.collider.transform.IsChildOf(self) || h.collider is CharacterController) continue;
                if (h.distance < best) { best = h.distance; n = h.normal; ok = true; }
            }
            return ok;
        }
        Transform ModelRootOf(Rider r)
        {
            if (r.ModelRoot != null) return r.ModelRoot;
            var an = r.Holder != null ? r.Holder.GetComponentInChildren<Animation>(true) : null; if (an == null || an.transform == r.Holder) return null;
            r.ModelRoot = an.transform; return r.ModelRoot;
        }
        static Quaternion BaseRot(bool own) { return own ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f); }
        void ResetTilt(Rider r, bool own) { var mt = ModelRootOf(r); if (mt != null) mt.localRotation = BaseRot(own); }
        /// <summary>PrintName.dx: desplazamiento vertical del nombre y la burbuja de chat segun el vehiculo (iVehicle 1 = tabla 0,35; 2 = scooter 0,3; otros 0).</summary>
        public static float NameDx(int id)
        {
            var v = Instance; if (v == null) return 0f; int t = 0;
            if (v.ents != null && id == v.ents.SelfId) t = SelfType;
            else { Rider r; if (v.riders.TryGetValue(id, out r) && r.Active) t = r.Type; }
            return t == 1 ? 0.35f : t == 2 ? 0.3f : 0f;
        }
        // cnAvatarThirdPersonMove (valores por defecto de los campos publicos; el inspector del original podria sobreescribirlos): leanMaxAngle 2.5, leanTurnRateScale 0.01, leanMinSpeed 2, leanEaseInSpeed 4, leanEaseOutSpeed 10, leanDirection 1
        const float LeanMax = 2.5f, LeanTurnRateScale = 0.01f, LeanMinSpeed = 2f, LeanEaseIn = 4f, LeanEaseOut = 10f, LeanDirection = 1f;
        /// <summary>UserMoveController.UpdateTurnLean: inclinacion lateral (giro Z local del modelo) proporcional a la velocidad de giro del rumbo; max = LeanMax x {0,1 tabla, 0,35 scooter, 1 tipo 3}.</summary>
        void UpdateLean(Rider r, Transform mt)
        {
            float dt = Time.deltaTime; Vector3 pos = r.Holder.position; Vector3 vel = Vector3.zero;
            if (r.HasLast && dt > 1e-5f) { Vector3 d = pos - r.LastPos; if (d.sqrMagnitude < 400f) vel = d / dt; }
            r.LastPos = pos; r.HasLast = true;
            float lim = r.Type == 1 ? LeanMax * 0.1f : r.Type == 2 ? LeanMax * 0.35f : r.Type == 3 ? LeanMax : 0f; if (lim <= 0f) return;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z); float mag = flat.magnitude; float target = 0f;
            if (mag > 0.0001f)
            {
                Vector3 dir = flat / mag;
                if (mag > LeanMinSpeed && r.LastDir.sqrMagnitude > 0.0001f)
                {
                    float cur = Mathf.Atan2(r.LastDir.x, r.LastDir.z) * Mathf.Rad2Deg; float tg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    float turn = Mathf.Clamp(Mathf.DeltaAngle(cur, tg), -45f, 45f); float rate = turn / Mathf.Max(dt, 0.0001f);
                    target = Mathf.Clamp(-rate * LeanTurnRateScale * LeanDirection, -lim, lim);
                }
                r.LastDir = dir;
            }
            else r.LastDir = Vector3.zero;
            float ease = Mathf.Abs(target) > Mathf.Abs(r.Lean) ? LeanEaseIn : LeanEaseOut;
            r.Lean = Mathf.LerpAngle(r.Lean, target, dt * ease);
            if (Mathf.Abs(r.Lean) > 0.01f) mt.localRotation *= Quaternion.Euler(0f, 0f, r.Lean);
        }
        void LateUpdate()
        {
            if (ents == null) return;
            foreach (var kv in riders)
            {
                var r = kv.Value; if (!r.Active || r.Holder == null) { r.HasLast = false; r.Lean = 0f; continue; } bool own = r.Id == ents.SelfId;
                var mt = ModelRootOf(r); if (mt == null) continue;
                bool air;
                if (own) { var g = Gc(); air = g != null && g.IsAirborne; }
                else { var rm = r.Holder.GetComponent<RemoteMover>(); air = rm != null && rm.Airborne; }
                if (air) { mt.localRotation = BaseRot(own); r.Tilted = false; UpdateLean(r, mt); continue; }   // animation.bJump: sin inclinacion
                Vector3 up, n; if (!GroundNormal(r.Holder.position + Vector3.up * 2f, r.Holder, out n)) n = Vector3.up; up = n;
                var right = own ? r.Holder.right : -r.Holder.right; var fwd = Vector3.Cross(right, up).normalized;
                if (Vector3.Angle(Vector3.up, fwd) < 30f) { fwd = Vector3.Cross(right, Vector3.up).normalized; up = Vector3.up; }   // pendiente > 60 grados: se queda plano
                if (fwd.sqrMagnitude < 1e-6f) continue;
                mt.rotation = Quaternion.Slerp(mt.rotation, Quaternion.LookRotation(fwd, up), Time.deltaTime * 4f); r.Tilted = true; UpdateLean(r, mt);
            }
        }
    }
}
