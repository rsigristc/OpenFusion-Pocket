// Interacción con NPC al estilo del cliente original (NpcIconMode / cnPlayerCamera.SubTargetCamera / NpcMoveController.MakeGameIcon).
// - Iconos de servicio sobre la cabeza: el original instancia el efecto NpcTable.m_iEffect (ES[672..686] -> game_icon_N.dds); aquí se dibujan esas mismas texturas.
// - Símbolos de misión: efecto 866 ('!' misión nueva) / 865 ('?' avanzar), según MissionState.
// - Cerca de un NPC: burbuja + panel con retrato (npcicon/mobicon/hnpcicon) y nombre. Clic izquierdo: diálogo con cámara en primer plano (posición = NPC + forward*radio*3, altura 0.6*alto).
// Dialogo (ventana de servicios + burbuja de saludo + barras de cine + fundido) reescrito segun NpcIconMode/PrintName originales con fuentes de mapa de bits (BitmapFontLoader.cs).
// SIN COMPILAR NI PROBAR EN UNITY (sintaxis comprobada con dotnet contra stubs). Las texturas están en Resources/OpenFusion/Icons y /Portraits (extraídas de los bundles del propio cliente).
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class NpcInfo : MonoBehaviour
    {
        public int Id, NpcNumber, Type, MapIcon, Service, Effect, IconNumber = -1, IconType = -1, Team, Level, Hp, MaxHp = 1, Style; public float SortDist; public bool IsMob => Team != 1 && Type <= 110 && Type != 25; public string Name = "", Greeting = ""; public float Radius = 0.5f, TableHeight;
        public int Symbol; public float SymbolAt = -10f; public bool HasFx; public NpcIconBillboard Fx; public bool FxTried; public string Voice; public bool VoiceTried;
        public GameObject Marker, Indicator, SvcFx; public float SvcTryAt = -10f; public int MarkerSym; public float MarkerTryAt = -10f, IndTryAt = -10f;
        Bounds b; bool hasB; float bAt; Vector3 boundsOrigin;
        // cnAvatarAttack/NpcContainer.GetConeList: hablan los NPC con Team 1 de tipo <= 110 salvo 25 (el original no mira nada mas; el rango de vision m_iSightRange lo filtra). Se mantienen ademas los tipos de servicio 1..28 de versiones anteriores.
        public bool Clickable => (Type >= 1 && Type <= 28 && Type != 6 && Type != 25) || (Team == 1 && Type <= 110 && Type != 25);
        /// <summary>Caja de seleccion por clic: modelo + capsula de la tabla (alto/radio), para NPC pequenos u oscuros cuyo modelo apenas tiene volumen.</summary>
        public Bounds PickBounds() { var bb = GetBounds(); float h = Mathf.Max(1f, TableHeight), r = Mathf.Max(0.5f, Radius); bb.Encapsulate(new Bounds(transform.position + Vector3.up * (h * 0.5f), new Vector3(r * 2f, h, r * 2f))); return bb; }
        public Bounds GetBounds() { if (!hasB || Time.time - bAt > 2f) { var rs = GetComponentsInChildren<Renderer>(); bool has = false; foreach (var r in rs) { if (r.GetComponentInParent<NpcFxMark>() != null) continue; if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); } if (!has) b = new Bounds(transform.position + Vector3.up, new Vector3(1, 2, 1)); hasB = true; bAt = Time.time; boundsOrigin = transform.position; } return new Bounds(b.center + transform.position - boundsOrigin, b.size); }
        public Vector3 Head(float extra = 0.3f) { var bb = GetBounds(); return new Vector3(transform.position.x, bb.max.y + extra, transform.position.z); }
        public float Height => TableHeight > 0.1f ? TableHeight : Mathf.Max(1f, GetBounds().size.y);
    }

    public sealed class NpcUI : MonoBehaviour
    {
        public static NpcUI Instance; public static System.Action<byte[]> Sender; public static bool Active => Instance != null && Instance.talking != null;
        public static bool NanoStationActive => Active && Instance.talking.Type >= 7 && Instance.talking.Type <= 9;
        public float InteractRange = 8f, IconRange = 60f;
        WorldEntities ents; ModelFactory models; NpcInfo near, talking; Texture2D white; readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>(); readonly HashSet<string> missing = new HashSet<string>();
        GUIStyle label, btn, wrap, small; string note; readonly List<Renderer> hidden = new List<Renderer>();

        public void Init(WorldEntities e, ModelFactory m) { if (ents != null) ents.Teleported -= OnTeleported; ents = e; models = m; Instance = this; if (ents != null) ents.Teleported += OnTeleported; }
        void OnDestroy() { Restore(); if (ents != null) ents.Teleported -= OnTeleported; if (Instance == this) Instance = null; }

        // ES[n] -> particleName (leído de los bundles): 669 game_icon_17 d, 670/671 ep_icon_02/03, 672..678 game_icon_1..7, 679..681 game_icon_8..10, 682..685 game_icon_13..16, 686 game_icon_11, 824 vehicle, 825 combine, 826 egger
        static string EffectIcon(int e)
        {
            if (e >= 672 && e <= 678) return "game_icon_" + (e - 671); if (e >= 679 && e <= 681) return "game_icon_" + (e - 671);
            switch (e) { case 669: return "game_icon_17_d"; case 670: return "ep_icon_02"; case 671: return "ep_icon_03"; case 682: return "game_icon_13"; case 683: return "game_icon_14"; case 684: return "game_icon_15"; case 685: return "game_icon_16"; case 686: return "game_icon_11"; case 824: return "gameicon_vehicle"; case 825: return "gameicon_combine"; case 826: return "gameicon_egger"; }
            return null;
        }
        Texture2D Res(string path) { if (cache.TryGetValue(path, out var t)) return t; if (missing.Contains(path)) return null; Watchdog.At("Resources.Load " + path); t = Resources.Load<Texture2D>(path); Watchdog.At("Resources.Load ok"); if (t == null) { missing.Add(path); Debug.LogWarning("[npcui] falta " + path); } else { t.filterMode = FilterMode.Bilinear; cache[path] = t; } return t; }
        Texture2D Portrait(NpcInfo n)
        {
            if (n.IconNumber < 0) return null; string pre = n.IconType == 4 ? "npcicon_" : n.IconType == 8 ? "mobicon_" : n.IconType == 10 ? "hnpcicon_" : null; if (pre == null) return null;
            return Res("OpenFusion/Portraits/" + pre + n.IconNumber.ToString("00"));
        }

        void EnsureTex()
        {
            if (white != null) return;
            white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply();
        }

        public bool TryGetCamera(out Vector3 pos, out Quaternion rot)
        {   // cnPlayerCamera.SubTargetCamera: pos = destino - (rotación*Euler(0,180,0))*forward*dist, y += alto*0.6; mira al mismo punto a esa altura
            pos = default; rot = default; if (talking == null) return false;
            var t = talking.transform; float h = talking.Height * 0.6f; pos = t.position + t.forward * (talking.Radius * 3f) + Vector3.up * h; var aim = t.position + Vector3.up * h;
            var dir = aim - pos; if (dir.sqrMagnitude < 1e-4f) return false; rot = Quaternion.LookRotation(dir, Vector3.up); return true;
        }

        void Begin(NpcInfo i, bool greet = true)
        {
            Debug.Log("[npcui] abrir diálogo con " + i.Name + " tipo " + i.Type + " id " + i.Id + " team " + i.Team + " npc# " + i.NpcNumber); talking = i; note = null; hidden.Clear();
            if (ents.Self != null) foreach (var r in ents.Self.GetComponentsInChildren<Renderer>()) if (r.enabled) { hidden.Add(r); r.enabled = false; }
            fadeAt = Time.unscaledTime;   // SetCameraAngle -> eFadeType.In
            // NpcGreetingBubble -> PrintName.SetChatBubble(m_strComment, Barker): el original descarta null/""/" "; aqui Trim() para que no salga una burbuja verde vacia
            var g = greet ? i.Greeting : null; bubbleText = string.IsNullOrWhiteSpace(g) ? null : g.Trim(); bubbleAt = Time.unscaledTime; bubbleSized = false;
            if (greet)
            {   // NpcGreetingBubble: voz del NPC (tipo 14 con carrera = racefinished, con misiones disponibles/terminables = qgreeting, resto = greeting01/02)
                if (nhud == null) nhud = FindObjectOfType<NanocomHud>(); bool bq = false;
                if (nhud != null) { nhud.MissionLists(i.NpcNumber, mStart, mComp); bq = mStart.Count + mComp.Count > 0; mFor = null; }
                NpcVoice.Play(i.transform, VoiceOf(i), i.Type == 14 && RaceSystem.Racing ? NpcVoice.V.RaceEnd : bq && i.Type != 14 ? NpcVoice.V.NpcClick1 : NpcVoice.V.NpcClick);
            }
            { int ty = i.Type, nn = i.NpcNumber; int h = 0;   // cnFirstUseSysManager.CheckCondition según el tipo de NPC
              if (nn == 668 || nn == 1067 || nn == 1060) h = 47; else if (ty >= 7 && ty <= 9) h = 21; else if (ty >= 18 && ty <= 22) h = 44; else if (ty == 5) h = 12; else if (ty == 15) h = 27; else if (ty == 16) h = 28; else if (ty == 13) h = 9; else if (ty == 26) h = 67; else if (ty == 12 || ty == 50) h = 30;
              if (h > 0) FirstUse.Check(h); }
            if ((i.Type == 15 || i.Type == 16) && TransportWindow.Instance != null) TransportWindow.Instance.RegisterAt(i.Id, i.NpcNumber, i.Type);   // NpcIconMode.CheckTransport
            if (bubbleText != null) ChatHud.AddEventString(4, i.Name + ": " + bubbleText);   // (el original lo envia aunque la opcion de burbujas este apagada)   // PrintName.SetChatBubble -> AddEventString(4, ...)
        }
        void End(bool fade = true) { Debug.Log("[npcui] cerrar diálogo"); var was = talking; talking = null; bubbleText = null; Restore(); if (fade) { fadeAt = Time.unscaledTime; if (was != null) NpcVoice.Play(was.transform, VoiceOf(was), NpcVoice.V.CloseNpcUI); } }   // EndMode: voz _farewell0N salvo al hacer warp
        string VoiceOf(NpcInfo i) { if (i == null) return ""; if (!i.VoiceTried) { i.VoiceTried = true; i.Voice = NpcVoice.CommentOf(ents != null ? ents.NpcTable : null, i.NpcNumber); } return i.Voice ?? ""; }
        void Restore() { foreach (var r in hidden) if (r != null) r.enabled = true; hidden.Clear(); }

        int SymbolOf(NpcInfo i)
        {
            if (Time.time - i.SymbolAt > 1f) { i.SymbolAt = Time.time + Random.value * 0.3f; i.Symbol = ents.Missions != null ? ents.Missions.SymbolFor(i.NpcNumber, i.Type) : 0; }
            return i.Symbol;
        }

        // ---- marcador 3D de mision ES[865] '?' / ES[866] '!' (NpcMoveController.SetQuestSymbol) e indicador inteligente ES[668] (SetSmartIndicator) ----
        float markAt, nhudAt;
        void UpdateMarkers(Transform self)
        {
            if (Time.time - markAt < 0.2f || ents == null) return; markAt = Time.time;
            if (nhud == null && Time.time - nhudAt > 2f) { nhudAt = Time.time; nhud = FindObjectOfType<NanocomHud>(); }
            int sel = nhud != null ? nhud.SelectedWayPointNpc : 0; bool hide = GameMenu.Blocking; float rr = IconRange * IconRange;
            foreach (var go in ents.NpcObjects)
            {
                if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i == null) continue;
                bool nearNpc = (go.transform.position - self.position).sqrMagnitude <= rr;
                int sym = nearNpc ? SymbolOf(i) : 0;
                if (sym != i.MarkerSym || (sym != 0 && i.Marker == null))
                {
                    if (i.Marker != null) { Destroy(i.Marker); i.Marker = null; }
                    if (sym != i.MarkerSym) i.MarkerTryAt = -10f;
                    i.MarkerSym = sym;
                    if (sym != 0 && Time.time - i.MarkerTryAt > 5f) { i.MarkerTryAt = Time.time; i.Marker = SpawnMarker(i, go.transform, sym); }
                }
                if (i.Marker != null && i.Marker.activeSelf == hide) i.Marker.SetActive(!hide);
                // icono de servicio sin Spec de quad (p. ej. holograma de guias del Guide Changer): se instancia el prefab real ES[n] (NpcMoveController.GameIconCoroutine)
                if (!i.HasFx && i.Type != 0 && i.Type != 6)
                {
                    int se = NpcIconFx.ChooseEffect(i.Type, i.Effect, false, false); NpcIconFx.Spec sp0;
                    if (se >= 0 && !NpcIconFx.TryGetSpec(se, out sp0))
                    {
                        if (!nearNpc) { if (i.SvcFx != null) { Destroy(i.SvcFx); i.SvcFx = null; } }
                        else
                        {
                            if (i.SvcFx == null && Time.time - i.SvcTryAt > 5f) { i.SvcTryAt = Time.time; i.SvcFx = SpawnServiceFx(i, go.transform, se); }
                            if (i.SvcFx != null) { bool off = hide || sym != 0; if (i.SvcFx.activeSelf == off) i.SvcFx.SetActive(!off); }
                        }
                    }
                }
                // RefreshQuestSymbol: smartIndicator = la tarea activa de la mision seleccionada tiene m_iSTGrantWayPoint == numero de este NPC (tipos 0 y 25 excluidos); SetSmartIndicatorDelete(true) mientras se habla con el
                bool wantInd = nearNpc && sel != 0 && i.NpcNumber == sel && i.Type != 0 && i.Type != 25 && talking != i;
                if (wantInd && i.Indicator == null && Time.time - i.IndTryAt > 5f) { i.IndTryAt = Time.time; i.Indicator = SpawnIndicator(i, go.transform); }
                else if (!wantInd && i.Indicator != null) { Destroy(i.Indicator); i.Indicator = null; }
                if (i.Indicator != null && i.Indicator.activeSelf == hide) i.Indicator.SetActive(!hide);
            }
        }
        static Transform FindChildNamed(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int k = 0; k < t.childCount; k++) { var r = FindChildNamed(t.GetChild(k), name); if (r != null) return r; }
            return null;
        }
        GameObject SpawnMarker(NpcInfo i, Transform npc, int sym)
        {
            var fx = BulletFx.Instance; if (fx == null) return null;
            Transform anchor = FindChildNamed(npc, "GameIcon"); Vector3 lp = Vector3.zero;
            if (anchor == null) { anchor = npc; lp = new Vector3(0f, (i.TableHeight > 0.1f ? i.TableHeight : i.Height) * NpcIconFx.QuestMarkerHeightFactor, 0f); }
            var go = fx.Spawn(sym == 2 ? 865 : 866, anchor.position, anchor.rotation, 1f, null, 1000000f);
            if (go == null) return null;
            if (go.GetComponentsInChildren<Renderer>(true).Length == 0) { Destroy(go); return null; }
            go.AddComponent<NpcFxMark>();
            var t = go.transform; t.SetParent(anchor, false); t.localPosition = lp; t.localRotation = Quaternion.identity;
            bool anim = false; var an = go.GetComponentInChildren<Animation>(true);
            if (an != null)
            {
                if (an.clip != null) { an.wrapMode = WrapMode.Loop; anim = an.Play(); }
                else foreach (AnimationState st in an) { st.wrapMode = WrapMode.Loop; anim = an.Play(st.name); break; }
            }
            if (!anim) go.AddComponent<NpcMarkerSpin>();
            Debug.Log("[npcui] marcador 3D ES[" + (sym == 2 ? 865 : 866) + "] en " + i.Name + (anim ? " (animacion del prefab)" : " (giro por codigo)"));
            return go;
        }
        GameObject SpawnServiceFx(NpcInfo i, Transform npc, int es)
        {
            var fx = BulletFx.Instance; if (fx == null) return null;
            Transform anchor = FindChildNamed(npc, "GameIcon"); Vector3 lp = Vector3.zero; Quaternion lr = Quaternion.identity;
            if (anchor == null) { anchor = FindChildNamed(npc, "Bip01 Head"); lp = new Vector3(-0.6f, 0f, 0f); }
            if (anchor == null) { anchor = npc; lr = Quaternion.Euler(90f, 0f, 0f); lp = new Vector3(0f, (i.TableHeight > 0.1f ? i.TableHeight : i.Height) * NpcIconFx.RootFallbackHeightFactor, 0f); }
            var go = fx.Spawn(es, anchor.position, anchor.rotation, 1f, null, 1000000f);
            if (go == null) return null;
            go.AddComponent<NpcFxMark>();
            var t = go.transform; t.SetParent(anchor, false); t.localPosition = lp; t.localRotation = lr;
            Debug.Log("[npcui] icono de servicio ES[" + es + "] en " + i.Name + " (tipo " + i.Type + ") ancla " + anchor.name + " renderers " + go.GetComponentsInChildren<Renderer>(true).Length);
            return go;
        }
        GameObject SpawnIndicator(NpcInfo i, Transform npc)
        {
            var fx = BulletFx.Instance; if (fx == null) return null;
            var go = fx.Spawn(668, npc.position, npc.rotation, Mathf.Max(0.2f, i.Radius * 2f), null, 1000000f);
            if (go == null) return null;
            go.AddComponent<NpcFxMark>(); go.transform.SetParent(npc, true);
            return go;
        }

        // ---- warp con NPC (NpcIconMode.NpcWarpOK / Update / LoadWarpEffect): efecto 394 en el jugador, sonido Dexbot_Warp, espera de 1,5 s y entonces REQ_PC_WARP_USE_NPC con fundido blanco (eFadeType.WIn) ----
        float warpWait, whiteAt = -10f, whiteStart; int warpNpcId, warpNumber; bool whiteHold;
        void BeginWarp(NpcInfo npc, int warpNum)
        {
            if (npc == null || warpWait > 0f) return;
            warpNpcId = npc.Id; warpNumber = warpNum; warpWait = 1.5f; WarpFlow.BeginWait();   // fWarpWaitTime
            var self = ents != null ? ents.Self : null;
            if (self != null && BulletFx.Instance != null) BulletFx.Instance.Spawn(394, self.transform.position, self.transform.rotation, 1f, null, 6f);   // LoadWarpEffect
            Sfx.PlayAt("sound/Dexbot_Warp.wav", npc.transform.position);
            NpcVoice.Play(npc.transform, VoiceOf(npc), NpcVoice.V.WarpOK);
            Debug.Log("[warp] espera 1.5 s npc=" + npc.Id + " warp=" + warpNum);
            End(false);   // cnPlayerCamera.EndSubTarget: la camara vuelve al jugador para ver el efecto
        }
        void TickWarp()
        {
            if (warpWait > 0f)
            {
                warpWait -= Time.deltaTime;
                if (warpWait <= 0f)
                {
                    warpWait = 0f;
                    if (Sender == null) return;
                    if (ents != null) ents.LastWarpFail = 0;
                    Sender(ClientPackets.WarpUseNpc(warpNpcId, warpNumber)); WarpFlow.Sent(); Debug.Log("[warp] enviado npc=" + warpNpcId + " warp=" + warpNumber);
                    whiteHold = true; whiteStart = Time.unscaledTime;
                }
            }
            else if (whiteHold && ((ents != null && ents.LastWarpFail != 0) || Time.unscaledTime - whiteStart > 5f)) { whiteHold = false; whiteAt = Time.unscaledTime; }
        }
        void OnTeleported(int x, int y, int z) { if (!whiteHold) return; whiteHold = false; whiteAt = Time.unscaledTime; }

        float errAt;
        void Err(string where, System.Exception e) { if (Time.time - errAt > 2f) { errAt = Time.time; Debug.LogError("[npcui] " + where + ": " + e); } }
        void Update() { try { Tick(); } catch (System.Exception e) { Err("Update", e); } }
        /// <summary>NPC objetivo actual con el que se puede hablar (bNpcTalkEnable del original) y su distancia; null / MaxValue si ninguno. Lo usa PlayerTarget para decidir NPC vs jugador.</summary>
        public static NpcInfo TalkNpc; public static float TalkNpcDist = float.MaxValue;
        GameClient gcl; float gclAt = -10f; int lastNearId;
        readonly Dictionary<int, float> sightCache = new Dictionary<int, float>();
        /// <summary>NpcTable.m_iSightRange * 0.01 en metros (CoordUtil.Distance_SToC); -1 si no hay fila.</summary>
        float SightOf(NpcInfo i)
        {
            float v; if (sightCache.TryGetValue(i.NpcNumber, out v)) return v;
            v = -1f; var nt = ents != null ? ents.NpcTable : null;
            if (nt != null && nt.m_pNpcData != null) foreach (var r in nt.m_pNpcData) if (r != null && r.m_iNpcNumber == i.NpcNumber) { v = r.m_iSightRange * 0.01f; break; }
            sightCache[i.NpcNumber] = v; return v;
        }
        /// <summary>Lo llama PlayerTarget.Intercept (que llama CombatSystem) con el flanco de "atacar": cnAvatarAttack.Update -> TalkNpc cuando hay NPC objetivo y no objetivo de ataque. Devuelve true si el ataque debe suprimirse.</summary>
        public static bool TalkIntercept(bool edge)
        {
            var u = Instance; if (u == null || u.talking != null || u.near == null || PlayerTarget.TalkId != 0 || u.ents == null || u.ents.Self == null) return false;
            if (edge && !(ChatHud.Typing || WelcomePopup.Active || GameMenu.Blocking || TradeOverlay.Active))
            { if (VehicleRide.SelfRiding) VehicleRide.HopOffFromAction(); else if (u.warpWait <= 0f) u.Begin(u.near); }
            return true;
        }

        void Tick()
        {
            Watchdog.At("NpcUI.Tick");
            var self = ents != null ? ents.Self : null; if (self == null) { near = null; return; }
            TickWarp(); UpdateMarkers(self.transform);
            if (talking != null)
            {
                near = null; TalkNpc = null; TalkNpcDist = float.MaxValue;
                if (Input.GetKeyDown(KeyCode.Escape) || (self.transform.position - talking.transform.position).sqrMagnitude > 25f * 25f) End();
                return;
            }
            // Seleccion de NPC como cnAvatarAttack.GetConeList/UpdateTargetUI: el NPC mas cercano (cualquier equipo) dentro del cono de vision (90 grados respecto a la camara aplanada, distancia max(radio, alto/2) + 15/2);
            // si es un NPC con el que se puede hablar y esta dentro de su m_iSightRange (suelo 3 m) es el objetivo (marco npc_info + icono) y atacar = hablar. En combate no se selecciona (npc = null).
            NpcInfo best = null; float bd = float.MaxValue;
            if (gcl == null && Time.time - gclAt > 2f) { gclAt = Time.time; gcl = FindObjectOfType<GameClient>(); }
            bool inCombat = gcl != null && Time.time < gcl.CombatUntil;
            if (!inCombat)
            {
                var cam = Camera.main; Vector3 fw = cam != null ? cam.transform.forward : self.transform.forward; fw.y = 0f; if (fw.sqrMagnitude < 1e-4f) fw = self.transform.forward; fw.Normalize();
                Vector3 pos = self.transform.position + Vector3.up * 0.8f;
                foreach (var go in ents.NpcObjects)
                {
                    if (go == null || !go.activeInHierarchy) continue; var i = go.GetComponent<NpcInfo>(); if (i == null || i.Type > 110 || i.Type == 25) continue;
                    if (i.Hp <= 0 && !i.Clickable) continue;   // monstruo muerto (Status.IsLive)
                    float h = Mathf.Max(0.5f, i.Height), r = Mathf.Max(0.2f, i.Radius); var p2 = go.transform.position;
                    if (p2.y < pos.y && pos.y < p2.y + h) p2.y = pos.y; else if (p2.y > pos.y) { } else p2.y += h;
                    var dv = p2 - pos; float dist = dv.magnitude; bool inView;
                    if (dist < r) inView = true;
                    else { float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dv / dist, fw), -1f, 1f)) * Mathf.Rad2Deg; inView = ang <= 90f && dist < Mathf.Max(r, h * 0.5f) + 7.5f; }
                    if (!inView) continue;
                    if (dist < bd) { bd = dist; best = i; }
                }
            }
            if (best != null && !best.Clickable) best = null;   // el mas cercano es un monstruo hostil: se ataca (CombatSystem), no se habla
            if (best != null) { float sg = SightOf(best); if (sg < 0f) sg = InteractRange; else if (sg < 3f) sg = 3f; if (bd >= sg) best = null; }
            if ((best != null ? best.Id : 0) != lastNearId)
            {
                lastNearId = best != null ? best.Id : 0;
                if (best != null) Debug.Log("[npcui] objetivo NPC " + best.Name + " tipo " + best.Type + " team " + best.Team + " npc# " + best.NpcNumber + " dist " + bd.ToString("0.0") + " vision " + SightOf(best).ToString("0.0"));
            }
            near = best; TalkNpc = best; TalkNpcDist = best != null ? bd : float.MaxValue;
            if (near != null) EnsurePortrait(near); if (MobTarget != null) EnsurePortrait(MobTarget);
            Vector2 clickPos = Vector2.zero; bool clicked = false;
            if (TouchControls.Active) clicked = TouchControls.TryTap(out clickPos);   // táctil: toque corto (los toques de joystick/cámara no seleccionan)
            else if (Input.GetMouseButtonDown(0)) { clicked = true; clickPos = Input.mousePosition; }
            if (GameMenu.Blocking || GameMenu.MouseOverHud || FirstUse.MouseOver || TradeOverlay.Active) clicked = false;   // menu/ventanas abiertos: el clic no habla con NPC
            if (clicked && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(clickPos); NpcInfo hit = null; float hd = 1e9f;
                foreach (var go in ents.NpcObjects) { if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i == null || !i.Clickable) continue; if ((go.transform.position - self.transform.position).sqrMagnitude > InteractRange * InteractRange * 1.5f) continue; var bb = i.PickBounds(); bb.Expand(0.3f); if (bb.IntersectRay(ray, out float dd) && dd < hd) { hd = dd; hit = i; } }
                if (hit != null) { if (VehicleRide.SelfRiding) VehicleRide.HopOffFromAction(); else if (warpWait <= 0f) Begin(hit); }   // cnAvatarAttack.Update: con vehiculo, el clic sobre un NPC baja del vehiculo en vez de hablar (FirstUse 70)
                else TradeOverlay.TryPick(ents, ray, self.transform.position, InteractRange * 1.5f);
            }
        }

        void Styles()
        {
            if (label != null) return; float s = Screen.height / 1080f;
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft }; label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(20 * s), fontStyle = FontStyle.Normal, wordWrap = true }; small.normal.textColor = new Color(1f, 0.9f, 0.5f);
            wrap = new GUIStyle(label) { wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(24 * s) }; wrap.normal.textColor = new Color(0.05f, 0.15f, 0.05f);
            btn = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(Mathf.RoundToInt(20 * s), 4, 4, 4) };
        }

        const string UI = "OpenFusion/UI/Textures/";
        Texture2D U(string n) { return Res(UI + n); }
        void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
        void Panel(Rect r) { Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), new Color(0.1f, 0.85f, 0.95f)); Fill(r, new Color(0.03f, 0.2f, 0.4f, 0.96f)); }
        bool ToScreen(Vector3 w, out Vector2 p, out float pxPerMeter) { p = default; pxPerMeter = 0; var cam = Camera.main; if (cam == null) return false; var v = cam.WorldToScreenPoint(w); if (v.z <= 0.1f) return false; p = new Vector2(v.x, Screen.height - v.y); pxPerMeter = Screen.height / (2f * v.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)); return true; }
        void DrawIcon(Texture2D t, Vector2 c, float px) { if (t == null) return; GUI.DrawTexture(new Rect(c.x - px / 2, c.y - px / 2, px, px), t, ScaleMode.ScaleToFit, true); }
        void DrawIconW(Texture2D t, Vector2 c, float px, float wScale) { if (t == null) return; float w = px * wScale; GUI.DrawTexture(new Rect(c.x - w / 2, c.y - px / 2, w, px), t, ScaleMode.StretchToFill, true); }

        void OnGUI() { try { Draw(); } catch (System.Exception e) { Err("OnGUI", e); } }
        void Draw()
        {
            if (GameMenu.Blocking && talking == null) return;   // iconos de NPC / objetivo de mob no se ven con menús abiertos
            if (WelcomePopup.Active || ents == null || ents.Self == null || Camera.main == null) return; Watchdog.At("NpcUI.Draw"); EnsureTex(); Styles(); float s = Screen.height / 1080f; var self = ents.Self.transform;
            if (talking == null)
            {
                foreach (var go in ents.NpcObjects)
                {
                    if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i == null) continue; if ((go.transform.position - self.position).sqrMagnitude > IconRange * IconRange) continue;
                    float y = 0.7f; float bob = Mathf.Sin(Time.time * 2.5f + i.Id) * 0.05f;
                    int sym = SymbolOf(i);
                    if (i.HasFx && !i.FxTried) { i.FxTried = true; i.Fx = go.GetComponent<NpcIconBillboard>(); }
                    if (i.Fx != null) i.Fx.Hidden = sym != 0;   // NpcMoveController.SetQuestSymbol: el simbolo de mision SUSTITUYE al icono de servicio (se destruye pEffectGameIcon) y vuelve con SetQuestSymbol(0)
                    var name = sym == 0 && !i.HasFx && i.Type != 0 && i.Type != 6 && i.Effect != 30 ? EffectIcon(i.Effect) : null;   // con HasFx el icono es un quad aditivo en el mundo (NpcIconBillboard)
                    if (name != null && ToScreen(i.Head(y + bob), out var p, out var ppm)) { DrawIcon(Res("OpenFusion/Icons/" + name), p, Mathf.Clamp(0.9f * ppm, 20, 110)); y += 1.0f; }
                    if (sym != 0 && i.Marker == null)
                    {   // (sprite de reserva si el marcador 3D ES[865]/ES[866] no se pudo crear) marcador ES[865] '?' (avanzar/terminar) / ES[866] '!' (mision nueva): anclado a la raiz del NPC + fHeight*0.85 (o al objeto "GameIcon"); la malla gira -45 grados/s sobre Y (aproximado aqui con el ancho del sprite)
                        float qh = (i.TableHeight > 0.1f ? i.TableHeight : i.Height) * NpcIconFx.QuestMarkerHeightFactor;
                        if (ToScreen(go.transform.position + Vector3.up * (qh + bob), out var q, out var qpm))
                        {
                            float spin = Mathf.Abs(Mathf.Cos(Time.time * 45f * Mathf.Deg2Rad)); float qpx = Mathf.Clamp(1.1f * qpm, 24, 130);
                            DrawIconW(Res(sym == 2 ? "OpenFusion/Icons/questicon_questionmark" : "OpenFusion/Icons/questicon_exclamationmark"), q, qpx, Mathf.Lerp(0.3f, 1f, spin));
                        }
                    }
                }
                bool plWins = PlayerTarget.TalkId != 0;   // UpdateTargetUI: si gana el jugador (NPC - 5 m > jugador) solo se ve el marco del jugador
                if (near != null && !plWins)
                {
                    if (ToScreen(near.Head(0.35f), out var p, out var ppm))
                    {   // icono de burbuja de texto (target_icon2) + mano "click" (target_icon3), como el original
                        float px = Mathf.Clamp(0.8f * ppm, 36, 90); float bob = Mathf.Sin(Time.time * 4f) * 3f * s; var bt = U("target_icon2");
                        if (bt != null) GUI.DrawTexture(new Rect(p.x - px / 2, p.y - px * 0.9f + bob, px, px * 0.9f), bt, ScaleMode.ScaleToFit, true);
                    }
                    TargetPanel(near, s);
                }
                // monstruo objetivo: solo si no hay NPC ni jugador objetivo (un unico marco a la vez, como CnGuiMonster_info.MobInfo). Tipo >= 1 -> marco de NPC (DoWindowNpc), tipo 0 -> marco de monstruo (DoWindowMob)
                if (near == null && !plWins && MobTarget != null && MobTarget.Hp > 0) { if (MobTarget.Type >= 1) TargetPanel(MobTarget, s); else MobPanel(MobTarget, s); }
            }
            else DialogGui();
            DrawPlayerBubbles();
            DrawFade();
        }

        // CnGuiMonster_info.DoWindowNpc: ventana 255x66 centrada arriba, retrato 64x64 en (5,-3), nombre en (70,0), disco 69x23 en (1,49)
        static string IconFor(string k)
        {
            if (k.StartsWith("MISSION")) return "npcicon_mission"; if (k.StartsWith("ENTER STORE")) return "npcicon_vendor"; if (k.StartsWith("NANO")) return "npcicon_Nanotune";
            if (k.StartsWith("BANK")) return "npcicon_bank"; if (k.StartsWith("GUIDE")) return "npcicon_guide"; if (k.StartsWith("WARP")) return "npcicon_warp"; if (k.StartsWith("START RACE") || k.StartsWith("RACE")) return "npcicon_monkey";
            return "npcicon_exit";
        }
        // Retrato alternativo: si no existe la textura npcicon/hnpcicon (no está en los bundles locales), se renderiza la cabeza del propio modelo una sola vez.
        readonly Dictionary<int, RenderTexture> rtCache = new Dictionary<int, RenderTexture>();
        Texture PortraitAny(NpcInfo n) { var t = Portrait(n); if (t != null) return t; rtCache.TryGetValue(n.Id, out var c); return c; }
        // La captura se hace en Update (no dentro de OnGUI: renderizar una cámara dentro de OnGUI falla con URP).
        void EnsurePortrait(NpcInfo n)
        {
            if (n == null || rtCache.ContainsKey(n.Id) || Portrait(n) != null) return;
            RenderTexture rt = null;
            try
            {
                rt = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32); float h = Mathf.Max(0.8f, n.Height); float d = h * 0.55f;
                var center = n.transform.position + Vector3.up * (h * 0.86f); var go = new GameObject("portraitCam"); var cam = go.AddComponent<Camera>(); cam.enabled = false;
                cam.transform.position = center + n.transform.forward * d; cam.transform.rotation = Quaternion.LookRotation(center - cam.transform.position, Vector3.up);
                cam.fieldOfView = 30f; cam.nearClipPlane = Mathf.Max(0.05f, d - h * 0.35f); cam.farClipPlane = d + h * 0.3f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
                PreviewRender.Render(cam, rt);
                Destroy(go); Debug.Log("[npcui] retrato por cámara para " + n.Name);
            }
            catch (System.Exception e) { Debug.LogWarning("[npcui] retrato por cámara: " + e.Message); }
            rtCache[n.Id] = rt;
        }
        BitmapFont fChalet; bool chaletTried; static readonly Color HudLabelCol = new Color32(0xE5, 0xE5, 0xE5, 255);
        /// <summary>Objetivo hostil actual (lo fija CombatSystem; null = ninguno).</summary>
        public static NpcInfo MobTarget; float shownHp = -1f; int shownId = -1;
        // CnGuiMonster_info.DoWindowMob: ventana 290x66 (mop_info) centrada arriba; nombre (70,0,190,20), nivel (90,23,60,20), barra mob_hpRect (58,22,199,4) HP_BAR (roja = daño reciente), disco 69x23, icono ABC (25,55,21,21) y efecto (6,-13,58,72)
        void MobPanel(NpcInfo n, float s)
        {
            float k = s * 1.5f; float gx = Screen.width / 2f - 290f * k / 2f, gy = 5f * k;
            Rect R(float x, float y, float w, float h) { return new Rect(gx + x * k, gy + y * k, w * k, h * k); }
            GUI.BeginGroup(new Rect(gx, gy, 290f * k, 76f * k)); gx = 0f; gy = 0f;   // el original dibuja dentro de GUI.BeginGroup(nRect) (alto 66+10): recorta el efecto/retrato que sobresalen por arriba
            // orden y rects del original (DoWindowMob): ventana (0,5,290,66), disco (1,49,69,23), nombre (70,0,190,20), nivel (90,23,60,20), efecto ABC, retrato (5,-3,64,64), efecto otra vez, icono ABC (25,55,21,21), barra (58,22,199,4)
            var back = U("mop_info"); if (back != null) GUI.DrawTexture(R(0, 5, 290, 66), back); else Fill(R(0, 5, 290, 66), new Color(0.03f, 0.2f, 0.4f, 0.9f));
            var disk = U("DiskFront"); if (disk != null) GUI.DrawTexture(R(1, 49, 69, 23), disk);
            int st = Mathf.Clamp(n.Style, 0, 2);
            var eff = U(new[] { "blue_icon", "red_icon", "yellow_icon" }[st]);
            if (eff != null) GUI.DrawTexture(R(6, -13, 58, 72), eff, ScaleMode.StretchToFill, true);
            var tex = PortraitAny(n); if (tex != null) GUI.DrawTexture(R(5, -3, 64, 64), tex, ScaleMode.StretchToFill, true);
            if (eff != null) GUI.DrawTexture(R(6, -13, 58, 72), eff, ScaleMode.StretchToFill, true);
            var ic = U(new[] { "a_icon", "b_icon", "c_icon" }[st]); if (ic != null) GUI.DrawTexture(R(25, 55, 21, 21), ic, ScaleMode.ScaleToFit, true);
            float v = Mathf.Clamp01(n.Hp / (float)Mathf.Max(1, n.MaxHp));
            if (shownId != n.Id) { shownId = n.Id; shownHp = v; } else if (shownHp > v) shownHp = Mathf.MoveTowards(shownHp, v, Time.deltaTime * 0.6f); else shownHp = v;
            var bar = U("HP_BAR"); var r = R(58, 22, 199, 4); var oc = GUI.color;
            Rect rd = r; rd.width *= shownHp; GUI.color = Color.red; if (bar != null) GUI.DrawTexture(rd, bar, ScaleMode.StretchToFill, true); else Fill(rd, Color.red);
            Rect rv = r; rv.width *= v; GUI.color = Color.white; if (bar != null) GUI.DrawTexture(rv, bar, ScaleMode.StretchToFill, true); else Fill(rv, Color.green); GUI.color = oc;
            if (!chaletTried) { chaletTried = true; fChalet = BitmapFontLoader.Load("ChaletBook-Regular_Small__1018"); }
            var xf = new Xf { P = new Vector2(gx, gy), S = k };
            Txt(fChalet, new Rect(gx + 70f, gy, 190f, 20f), n.Name, TextAnchor.MiddleLeft, false, HudLabelCol, null, xf);
            Txt(fChalet, new Rect(gx + 90f, gy + 23f, 60f, 20f), n.Level.ToString("00"), TextAnchor.MiddleLeft, false, HudLabelCol, null, xf);
            GUI.EndGroup();
        }
        void TargetPanel(NpcInfo n, float s)
        {
            float k = s * 1.5f; float gx = Screen.width / 2f - 255f * k / 2f, gy = 5f * k;
            Rect R(float x, float y, float w, float h) { return new Rect(gx + x * k, gy + y * k, w * k, h * k); }
            var back = U("npc_info"); if (back != null) GUI.DrawTexture(R(0, 5, 255, 66), back);
            var disk = U("DiskFront"); if (disk != null) GUI.DrawTexture(R(1, 49, 69, 23), disk);
            var tex = PortraitAny(n); if (tex != null) GUI.DrawTexture(R(5, -3, 64, 64), tex, ScaleMode.StretchToFill, true);
            // CnGuiMonster_info.DoWindowNpc: GUI.Label(name_Rect = (70,0,190,20), nombre) con el estilo "label" de FusionFallHUDSkin (fuente ChaletBook-Regular Small de mapa de bits)
            if (!chaletTried) { chaletTried = true; fChalet = BitmapFontLoader.Load("ChaletBook-Regular_Small__1018"); }
            var xf = new Xf { P = new Vector2(gx, gy), S = k };
            Txt(fChalet, new Rect(gx + 70f, gy, 190f, 20f), n.Name, TextAnchor.MiddleLeft, false, HudLabelCol, null, xf);
        }

        // =====================================================================================================================
        // DIALOGO DE NPC AL ESTILO ORIGINAL  (NpcIconMode.DoNpcWindow + PrintName.DrawBarker / SetChatBubble(Barker); ver REPORT.md)
        //  - Escala de UI original: FFGUIUtility.GetUiScale() = Screen.height/681 (uiScaleFactor=1), ScaleAroundPivot(pivote) / ScaleAroundRect(rect).
        //  - Barras de cine: 0.15673982*Screen.height arriba y abajo (textura 'back' negra), sin escalar.
        //  - Ventana: pivote (Screen.width*3/4, Screen.height/2), 242 x {114 (solo CLOSE) | 171 (1 servicio + CLOSE) | 228 (>=2 servicios + CLOSE)}, centrada en el pivote.
        //    Fondo npc_win (borde 20,45,0,0) / multifunc_win (borde 20,45,40,20); titulo en (15,10,w-55,40) estilo label (JEFFE_14, #E5E5E5, UL, pad 0,0,3,3);
        //    CloseButton (w-36, 0.5, 32x32) close.png/closeover.png; botones 212x48 en x=(w-212)/2, y=h-n*54+i*54-10, estilo "button" (blue_button_normal/over, borde 6, pad 10,6,3,6,
        //    icono 38x33 a la izquierda + texto " NOMBRE" JEFFE_14 alineado ML, colores #E5E5E5 / hover #00477A / pulsado #FFFFFF).
        //  - Burbuja (bbBox): fuente JEFFE_14 negra, MC, wrap, pad 10, borde 6, overflow (4,4,0,2) de bbubble.png; ancho = clamp(anchoTexto+20,100,300); alto = lineas*13.71+20;
        //    cola bb_009.png 5x20 en (centroX-4, yMax); escala UiScale alrededor del centro de la burbuja; borde inferior en la cabeza (pos + 0.8*alto) proyectada; dura max(7, alto/5) s; sin fundido.
        //  - Fundido negro 0.5 s (GameFrame eFadeType.In: timer 1->0 a 2/s) al abrir y al cerrar (salvo warp).
        // =====================================================================================================================
        const float UiRefH = 681f, BarFrac = 0.15673982f, FadeSecs = 0.5f;
        static readonly Color LabelCol = new Color32(0xE5, 0xE5, 0xE5, 255), BtnHoverCol = new Color32(0x00, 0x47, 0x7A, 255);
        BitmapFont fJ14, fJ16; bool fontsTried;
        BitmapFont J14 { get { LoadFonts(); return fJ14; } }
        BitmapFont J16 { get { LoadFonts(); return fJ16; } }
        void LoadFonts() { if (fontsTried) return; fontsTried = true; fJ14 = BitmapFontLoader.Load("JEFFE___14__903"); fJ16 = BitmapFontLoader.Load("JEFFE___16__1012"); if (fJ14 == null) Debug.LogWarning("[npcui] fuente JEFFE_14 no disponible: se usa la fuente IMGUI por defecto"); }

        // Transformacion de "espacio de diseno" a pantalla: p' = pivot + (p - pivot) * s   (= GUIUtility.ScaleAroundPivot)
        struct Xf
        {
            public Vector2 P; public float S;
            public Rect R(Rect r) { return new Rect(P.x + (r.x - P.x) * S, P.y + (r.y - P.y) * S, r.width * S, r.height * S); }
            public Vector2 Inv(Vector2 m) { return new Vector2(P.x + (m.x - P.x) / S, P.y + (m.y - P.y) / S); }
        }

        // Fondo de GUIStyle: 9 porciones, bordes (L,R,T,B) en texels a 1:1 (la escala la pone Xf), overflow expande el rect dibujado. Si L+R > ancho (multifunc_win: 20+45>64) Unity
        // muestrea una franja invertida de 1 texel => se usa la columna de 1 px.
        void Slice9(Texture2D t, Rect r, float bl, float br, float bt, float bb, Xf xf, float ol = 0, float orr = 0, float ot = 0, float ob = 0)
        {
            if (t == null || Event.current.type != EventType.Repaint) return;
            r = new Rect(r.x - ol, r.y - ot, r.width + ol + orr, r.height + ot + ob); float W = t.width, H = t.height;
            float[] sx0 = { 0, bl, W - br }, sx1 = { bl, W - br, W }, sy0 = { 0, bt, H - bb }, sy1 = { bt, H - bb, H };
            if (sx1[1] <= sx0[1]) { sx0[1] = Mathf.Max(0, bl - 1); sx1[1] = bl; }
            if (sy1[1] <= sy0[1]) { sy0[1] = Mathf.Max(0, bt - 1); sy1[1] = bt; }
            float[] dx = { r.x, r.x + bl, r.xMax - br, r.xMax }, dy = { r.y, r.y + bt, r.yMax - bb, r.yMax };
            for (int j = 0; j < 3; j++) for (int i = 0; i < 3; i++)
                {
                    var d = new Rect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]); if (d.width <= 0f || d.height <= 0f || sx1[i] <= sx0[i] || sy1[j] <= sy0[j]) continue;
                    var uv = new Rect(sx0[i] / W, 1f - sy1[j] / H, (sx1[i] - sx0[i]) / W, (sy1[j] - sy0[j]) / H);   // textura derecha: fila 0 = arriba => v = 1 - y/H
                    GUI.DrawTextureWithTexCoords(xf.R(d), t, uv, true);
                }
        }

        GUIStyle fallback;
        // Texto con la fuente de mapa de bits; si faltan los recursos, cae a IMGUI.
        void Txt(BitmapFont f, Rect r, string text, TextAnchor a, bool wrap, Color c, RectOffset pad, Xf xf)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (f != null) { f.Draw(r, text, a, wrap, c, pad, xf.P, xf.S); return; }
            if (fallback == null) fallback = new GUIStyle(GUI.skin.label);
            fallback.fontSize = Mathf.Max(8, Mathf.RoundToInt(14 * xf.S)); fallback.alignment = a; fallback.wordWrap = wrap; fallback.normal.textColor = c;
            if (pad != null) { var q = new Rect(r.x + pad.left, r.y + pad.top, r.width - pad.horizontal, r.height - pad.vertical); r = q; }
            GUI.Label(xf.R(r), text, fallback);
        }

        // Control pulsable: devuelve true al soltar el raton dentro. over = ratón encima (sin otro control activo), down = pulsado dentro.
        bool Click(Rect r, Xf xf, out bool over, out bool down)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current; bool inside = r.Contains(xf.Inv(e.mousePosition)); bool hot = GUIUtility.hotControl == id;
            over = inside && (GUIUtility.hotControl == 0 || hot); down = hot && inside;
            if (e.type == EventType.MouseDown && e.button == 0 && over && !hot) { GUIUtility.hotControl = id; e.Use(); }
            else if (e.type == EventType.MouseUp && hot) { GUIUtility.hotControl = 0; e.Use(); return inside; }
            return false;
        }

        // Estilo "button" del skin FusionFallInteractionSkin con GUIContent(" TEXTO", icono).
        bool ServiceButton(Rect r, string label, Texture2D icon, Xf xf)
        {
            bool over, down; bool clicked = Click(r, xf, out over, out down);
            if (Event.current.type == EventType.Repaint)
            {
                Slice9(U(over && !down ? "blue_button_over" : "blue_button_normal"), r, 6, 6, 6, 6, xf);
                var col = down ? Color.white : over ? BtnHoverCol : LabelCol;
                float cx = r.x + 10f, cy = r.y + 3f, cw = r.width - 16f, ch = r.height - 9f, iw = 0f;   // contenido = rect - padding (L10,R6,T3,B6)
                if (icon != null) { float k = Mathf.Min(1f, ch / icon.height); iw = icon.width * k; float ih = icon.height * k; GUI.DrawTexture(xf.R(new Rect(cx, cy + (ch - ih) * 0.5f, iw, ih)), icon, ScaleMode.StretchToFill, true); }
                Txt(J14, new Rect(cx + iw, cy, cw - iw, ch), label, TextAnchor.MiddleLeft, false, col, null, xf);
            }
            return clicked;
        }

        // NpcIconMode.DoPcWindow (menu de jugador): ventana 242x393 en (3/4 W - 121, H/2 - 169), pivote (3/4 W, H/2); fondo multifunc_win, titulo J14, CloseButton close/closeover,
        // botones "button" (blue_button_*) 212x48 en (15, 50 + 54 i) con icono pButtonTex (12 trade, 13 group, 14 buddy, 16 antibuddy = ignorar/reportar, 17 exit). Devuelve -2 nada, -1 cerrar (X), i = boton.
        public int PcMenu(string title, string[] labels, string[] icons, float scale)
        {
            float W = Screen.width, H = Screen.height; var e = Event.current; if (e == null) return -2;
            var xf = new Xf { P = new Vector2(W * 3f / 4f, H * 0.5f), S = scale }; var win = new Rect(Mathf.Floor(W * 3 / 4) - 121f, Mathf.Floor(H / 2) - 169f, 242f, 393f); int res = -2;
            Slice9(U("multifunc_win"), win, 20, 45, 40, 20, xf);
            Txt(J14, new Rect(win.x + 15f, win.y + 10f, 187f, 40f), title, TextAnchor.UpperLeft, true, LabelCol, new RectOffset(0, 0, 3, 3), xf);
            bool over, down; var cr = new Rect(win.xMax - 36f, win.y + 0.5f, 32f, 32f); bool cc = Click(cr, xf, out over, out down);
            if (e.type == EventType.Repaint) { var ct = U(over && !down ? "closeover" : "close"); if (ct != null) GUI.DrawTexture(xf.R(cr), ct, ScaleMode.StretchToFill, true); }
            if (cc) res = -1;
            for (int i = 0; i < labels.Length; i++)
                if (ServiceButton(new Rect(win.x + 15f, win.y + 50f + 54f * i, 212f, 48f), " " + Loc.T(labels[i]), U(icons[i]), xf) && res == -2) res = i;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && win.Contains(xf.Inv(e.mousePosition))) e.Use();
            return res;
        }

        string bubbleText; float bubbleAt, bubbleDur; Vector2 bubbleSize; bool bubbleSized; float fadeAt = -10f;

        void DrawBubble(float s)
        {   // PrintName.DrawBarker -> GUI.Box(ChatRect, texto, bbBox) + GUI.Label(cola 5x20, bbLabel), todo escalado alrededor del centro de ChatRect
            if (!GameOptions.Cur.BalloonChat) return;
            if (bubbleText == null || talking == null) return; var f = J14; var cam = Camera.main; if (f == null || cam == null) return;
            if (!bubbleSized)
            {   // SetChatBubble: x = clamp(CalcMinMaxWidth.max (+pad), 100, 300); y = box.CalcHeight(texto, x) ; fMaxTime = max(7, y/5)
                float cw0 = Mathf.Max(100f, Mathf.Min(300f, f.MaxLineWidth(bubbleText) + 20f)); bubbleSize = new Vector2(cw0, f.Height(bubbleText, cw0 - 20f) + 20f); bubbleDur = Mathf.Max(7f, bubbleSize.y / 5f); bubbleSized = true;
            }
            if (Time.unscaledTime - bubbleAt > bubbleDur) return;
            var v = cam.WorldToScreenPoint(talking.transform.position + Vector3.up * talking.Height * 0.8f); if (v.z <= 0f) return;
            var rect = new Rect(v.x - bubbleSize.x * 0.5f, Screen.height - v.y - bubbleSize.y, bubbleSize.x, bubbleSize.y); var xf = new Xf { P = rect.center, S = UiScale() };
            Slice9(U("bbubble"), rect, 6, 6, 6, 6, xf, 4, 4, 0, 2);
            Txt(f, rect, bubbleText, TextAnchor.MiddleCenter, true, Color.black, new RectOffset(10, 10, 10, 10), xf);
            var tail = U("bb_009"); if (tail != null) GUI.DrawTexture(xf.R(new Rect(rect.x + rect.width * 0.5f - 4f, rect.yMax, 5f, 20f)), tail, ScaleMode.StretchToFill, true);
        }


        // ---- burbujas de chat de jugadores (PrintName.SetChatBubble/DrawBarker del original), sobre la cabeza de quien habla ----
        sealed class PBubble { public Transform T; public string Text; public float At, Dur, H; public Vector2 Size; public bool Sized; }
        readonly Dictionary<int, PBubble> playerBubbles = new Dictionary<int, PBubble>();
        public void AddPlayerBubble(int id, Transform t, string text)
        {
            if (t == null || string.IsNullOrWhiteSpace(text) || !GameOptions.Cur.BalloonChat) return;
            playerBubbles[id] = new PBubble { T = t, Text = text.Trim(), At = Time.unscaledTime };
        }
        /// <summary>PrintName.SetChatBubble(texto, QuestChat) de un NPC: linea en el chat (AddEventString 4) + burbuja a fHeight*0.8.</summary>
        public void AddNpcBubble(int npcInstId, Transform t, string npcName, float height, string text)
        {
            if (t == null || string.IsNullOrEmpty(text) || text.Length <= 1 || text.Equals(" ")) return;
            ChatHud.AddEventString(4, npcName + ": " + text);
            if (!GameOptions.Cur.BalloonChat) return;
            playerBubbles[-1000000 - npcInstId] = new PBubble { T = t, Text = text.Trim(), At = Time.unscaledTime, H = height };
        }
        public float PlayerBubbleHeight = 2.15f;
        void DrawPlayerBubbles()
        {
            if (playerBubbles.Count == 0) return; var f = J14; var cam = Camera.main; if (f == null || cam == null) return; List<int> dead = null;
            foreach (var kv in playerBubbles)
            {
                var b = kv.Value; if (b.T == null) { (dead ?? (dead = new List<int>())).Add(kv.Key); continue; }
                if (!b.Sized) { float cw0 = Mathf.Max(100f, Mathf.Min(300f, f.MaxLineWidth(b.Text) + 20f)); b.Size = new Vector2(cw0, f.Height(b.Text, cw0 - 20f) + 20f); b.Dur = Mathf.Max(7f, b.Size.y / 5f); b.Sized = true; }
                if (Time.unscaledTime - b.At > b.Dur) { (dead ?? (dead = new List<int>())).Add(kv.Key); continue; }
                var v = cam.WorldToScreenPoint(b.T.position + Vector3.up * ((b.H > 0.1f ? b.H : PlayerBubbleHeight) + (kv.Key > -1000000 ? VehicleRide.NameDx(kv.Key) : 0f))); if (v.z <= 0f) continue;   // PrintName.dx: +0.35 tabla / +0.3 scooter
                var rect = new Rect(v.x - b.Size.x * 0.5f, Screen.height - v.y - b.Size.y, b.Size.x, b.Size.y); var xf = new Xf { P = rect.center, S = UiScale() };
                bool npcB = kv.Key <= -1000000;   // NPC: bbBox/bbLabel (verde); jugador: FreeChat = fbBox/fbLabel (azul, fbubble.png / fb_009.png)
                Slice9(U(npcB ? "bbubble" : "fbubble"), rect, 6, 6, 6, 6, xf, 4, 4, 0, 2);
                Txt(f, rect, b.Text, TextAnchor.MiddleCenter, true, Color.black, new RectOffset(10, 10, 10, 10), xf);
                var tail = U(npcB ? "bb_009" : "fb_009"); if (tail != null) GUI.DrawTexture(xf.R(new Rect(rect.x + rect.width * 0.5f - 4f, rect.yMax, 5f, 20f)), tail, ScaleMode.StretchToFill, true);
            }
            if (dead != null) foreach (var k in dead) playerBubbles.Remove(k);
        }

        void DrawFade()
        {   // GameFrame: eFadeType.In (negro): alfa 1 -> 0 a 2/s
            float wa = whiteHold ? 1f : 1f - (Time.unscaledTime - whiteAt) / FadeSecs;
            if (wa > 0f) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 1f, 1f, Mathf.Clamp01(wa)));
            float a = 1f - (Time.unscaledTime - fadeAt) / FadeSecs; if (a <= 0f) return; Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, a));
        }
        static float UiScale() { return GameOptions.UiScale; }

        // NpcIconMode.WarpOK: comprobaciones en cliente antes de pedir el warp (110 taros, 111 nivel, 112/113 objetos, 114 mision, 115 grupo lejos)
        int WarpCheck(WarpElement w)
        {
            if (nhud == null) nhud = FindObjectOfType<NanocomHud>(); if (nhud == null || talking == null) return 0;
            if (nhud.PlayerTaros < w.m_iCost) return 110;
            if (nhud.PlayerLevel > 0 && nhud.PlayerLevel < w.m_iLimit_Level) return 111;
            if (w.m_iLimit_ItemID != 0 && !HasWarpItem(w.m_iLimit_ItemType == 8, w.m_iLimit_ItemType, w.m_iLimit_ItemID)) return 112;
            if (w.m_iLimit_UseItemID != 0 && !HasWarpItem(w.m_iLimit_ItemType == 8, w.m_iLimit_UseItemType, w.m_iLimit_UseItemID)) return 113;   // (sic) el original decide el inventario con m_iLimit_ItemType
            if (w.m_iLimit_TaskID != 0 && !nhud.TaskIsActive(w.m_iLimit_TaskID) && !nhud.MissionIsActive(w.m_iMissionID)) return 114;
            if (w.m_iWarpGroupType == 1 && ents != null)
            {
                foreach (int pid in GroupState.Members())
                {
                    if (pid == ents.SelfId) continue; var pt = ents.TransformOfPlayer(pid);
                    if (pt == null) return 115;
                    Vector3 d = talking.transform.position - pt.position; d.y = 0f; if (d.magnitude > 5f) return 115;
                }
            }
            return 0;
        }
        bool HasWarpItem(bool questInv, int type, int id)
        {
            if (questInv) return nhud != null && nhud.QuestItemQty(id) > 0;
            for (int k = 0; k < InventoryWindow.Items.Length; k++) if (InventoryWindow.Items[k].Type == type && InventoryWindow.Items[k].Id == id) return true;
            return false;
        }

        sealed class SvcItem { public string Label, Icon; public System.Action Do; public SvcItem(string l, string i, System.Action d) { Label = l; Icon = i; Do = d; } }

        // ---- lista de misiones del NPC (NpcIconMode.DoNpcWindow con bQuest): M_Top + "Active missions" (terminar) + "Available missions" (empezar) + M_Bottom ----
        readonly List<MissionElement> mStart = new List<MissionElement>(), mComp = new List<MissionElement>(); float mAt = -10f, missionSentAt = -10f; NpcInfo mFor; NanocomHud nhud;
        static readonly string[] MissionIcons = { "world_icon", "edd_icon__540", "dexter_icon__327", "mojo_icon", "ben_icon", "compu_icon__556" };   // pMissionIcon[0..5]

        void DrawMissionPanel(Rect win, int nC, int nS, int n, Xf xf)
        {
            Slice9(U("mission_top"), new Rect(win.x, win.y, 332f, 50f), 20, 45, 0, 0, xf);
            float y = win.y + 50f;
            if (nC > 0) y = DrawMissionSection(win, y, Loc.T("Active missions") + ":", mComp, nC, true, xf);
            if (nS > 0) y = DrawMissionSection(win, y, Loc.T("Available missions") + ":", mStart, nS, false, xf);
            if (n > 1) Slice9(U("mission_bottom_func2"), new Rect(win.x, y, 332f, 131f), 20, 20, 0, 0, xf); else Slice9(U("mission_bottom_func"), new Rect(win.x, y, 332f, 76f), 20, 20, 0, 0, xf);
        }
        float DrawMissionSection(Rect win, float y, string head, List<MissionElement> list, int cnt, bool complete, Xf xf)
        {
            Slice9(U("mission_body"), new Rect(win.x, y, 332f, 25f + 32f * cnt), 10, 10, 0, 0, xf);
            var hr = new Rect(win.x, y, 328f, 20f); Slice9(U("sel_bar"), hr, 0, 0, 0, 0, xf);
            Txt(J14, hr, head, TextAnchor.MiddleLeft, false, new Color(0f, 0.4f, 0.6f), new RectOffset(10, 6, 4, 6), xf);
            y += 20f;
            for (int i = 0; i < cnt && i < list.Count; i++)
            {
                var me = list[i]; int ic = me.m_iHMissionType == 1 ? Mathf.Clamp(me.m_iCSTReqGuide, 0, 5) : 0;   // pMissionIcon[m_iCSTReqGuide]
                if (MissionRow(new Rect(win.x + 7f, y + 5f + i * 32f, 312f, 27f), " " + (nhud != null ? nhud.MissionNameOf(me) : ""), U(MissionIcons[ic]), xf)) OnMissionClick(me, complete);
            }
            return y + 32f * cnt + 5f;
        }
        bool MissionRow(Rect r, string label, Texture2D icon, Xf xf)
        {   // estilo m_button: mission_button / mission_button_over, texto amarillo (hover 0.19,0.22,0.35), padding L10 T2, alineado a la izquierda
            bool over, down; bool clicked = Click(r, xf, out over, out down);
            if (Event.current.type == EventType.Repaint)
            {
                Slice9(U(over && !down ? "mission_button_over" : "mission_button"), r, 5, 5, 5, 5, xf);
                var col = over && !down ? new Color(0.19f, 0.22f, 0.35f) : Color.yellow;
                float cx = r.x + 10f, cy = r.y + 2f, cw = r.width - 10f, ch = r.height - 2f, iw = 0f;
                if (icon != null) { float k = Mathf.Min(1f, ch / icon.height); iw = icon.width * k; float ih = icon.height * k; GUI.DrawTexture(xf.R(new Rect(cx, cy + (ch - ih) * 0.5f, iw, ih)), icon, ScaleMode.StretchToFill, true); }
                Txt(J14, new Rect(cx + iw, cy, cw - iw, ch), label, TextAnchor.MiddleLeft, false, col, null, xf);
            }
            return clicked;
        }
        void OnMissionClick(MissionElement me, bool complete)
        {
            var npc = talking; if (npc == null || nhud == null || Time.unscaledTime - missionSentAt < 1.5f) return; Sfx.Button();
            if (!complete) { var jw = JournalWindow.Instance; if (jw == null) { note = "Misiones: ventana no disponible"; return; } End(false); jw.OpenMode(1, me, npc.Id); return; }
            if (me.m_iSUReward > 0) { var jw = JournalWindow.Instance; if (jw == null) { note = "Misiones: ventana no disponible"; return; } End(false); jw.OpenMode(2, me, npc.Id); return; }
            missionSentAt = Time.unscaledTime; nhud.RequestTaskEnd(me.m_iHTaskID, npc.Id, 0, 0);   // sin recompensa: se completa directamente (cnEvent 12,6 -> RequestTaskComplete)
        }
        /// <summary>Vuelve al dialogo del NPC al cerrar la ventana de oferta / recompensa (cnMissionJournal.EndModeToTarget).</summary>
        public static void ReturnTo(int npcInstId)
        {
            var u = Instance; if (u == null || u.ents == null || u.ents.Self == null || npcInstId == 0) return;
            foreach (var go in u.ents.NpcObjects) { if (go == null) continue; var i = go.GetComponent<NpcInfo>(); if (i != null && i.Id == npcInstId) { if ((go.transform.position - u.ents.Self.transform.position).sqrMagnitude <= u.InteractRange * u.InteractRange * 4f) { u.mFor = null; u.Begin(i, false); } return; } }
        }

        void DialogGui()
        {
            float W = Screen.width, H = Screen.height, s = UiScale(), barH = BarFrac * H;
            Fill(new Rect(0, 0, W, barH), Color.black); Fill(new Rect(0, H - barH, W, barH), Color.black);   // GUI.DrawTexture(rect, BlackTextue) arriba y abajo, sin escalar
            if (!string.IsNullOrEmpty(note)) { var top = new Rect(0, 0, W, barH); Txt(J16, top, note, TextAnchor.MiddleCenter, true, Color.white, null, new Xf { P = top.center, S = s }); }   // estilo BigFont16 (JEFFE_16, blanco, MC) en la barra superior (TopString)

            if (talking.Type == 13 && InventoryWindow.FirstFreeSlot() < 0)
            {   // DoNpcWindow: aviso rojo BigFont16 en la barra superior si el inventario esta lleno (boton START/CANCEL RACE)
                var wr = new Rect((W - 400f) / 2f, barH / 2f, 400f, 30f); Txt(J16, wr, Loc.T("YOUR INVENTORY IS FULL. YOU WILL BE UNABLE TO ACCEPT ANY ITEM REWARDS."), TextAnchor.MiddleCenter, true, new Color(1f, 0f, 0f), null, new Xf { P = new Vector2(W * 0.5f, barH * 0.5f), S = s });
            }

            DrawBubble(s);

            var items = new List<SvcItem>(); var t = talking.Type;
            void Todo(string n) { note = n + ": todavia no implementado"; Debug.Log("[npcui] " + note); }
            // ---- misiones (NpcIconMode.CheckQuest): se listan dentro de la propia ventana, no como boton ----
            if (nhud == null) nhud = FindObjectOfType<NanocomHud>();
            if (mFor != talking || Time.unscaledTime - mAt > 0.5f) { mFor = talking; mAt = Time.unscaledTime; if (nhud != null) nhud.MissionLists(talking.NpcNumber, mStart, mComp); else { mStart.Clear(); mComp.Clear(); } }
            int nC = Mathf.Min(4, mComp.Count), nS = Mathf.Min(4 - nC, mStart.Count); bool bQuest = nC + nS > 0;
            if (VendorWindow.Instance != null ? VendorWindow.Instance.Has(talking.NpcNumber) : (t == 2 || t == 4)) items.Add(new SvcItem("ENTER STORE", "npcicon_vendor", () => { var npc = talking; if (VendorWindow.Instance == null) { Todo("Tienda"); return; } VendorWindow.Instance.Open(npc.Id, npc.NpcNumber, npc.Name, npc.Service); End(false); }));
            if (t == 7 || t == 8 || t == 9) items.Add(new SvcItem("NANO STATION", "npcicon_Nanotune", () => { var npc = talking; if (InventoryWindow.Instance == null) { Todo("Estacion de nanos"); return; } End(false); InventoryWindow.Instance.OpenStation(npc.NpcNumber, npc.Name); }));
            if (t == 12 || t == 50) items.Add(new SvcItem(t == 50 ? "LOCAL BANK" : "BANK", "npcicon_bank", () => { var npc = talking; if (BankWindow.Instance == null) { Todo("Banco"); return; } BankWindow.Instance.Open(npc.Id); End(false); }));
            if (t >= 18 && t < 23) items.Add(new SvcItem("GUIDE CHANGER", "npcicon_guide", () => { var npc = talking; if (GuideChangerWindow.Instance == null) { Todo("Cambio de guia"); return; } End(false); GuideChangerWindow.Instance.Open(npc.Id, npc.Type, 7); }));
            if (t < 18 || t > 22) { var nm = talking.Name != null ? talking.Name.Trim() : ""; if (string.Equals(nm, "Guide Changer", System.StringComparison.OrdinalIgnoreCase)) items.Add(new SvcItem("GUIDE CHANGER", "npcicon_guide", () => { var npc = talking; if (GuideChangerWindow.Instance == null) { Todo("Cambio de guia"); return; } End(false); GuideChangerWindow.Instance.Open(npc.Id, npc.Type, 7); })); }   // reserva: NPC llamado "Guide Changer" cuyo tipo de tabla no esta en 18..22
            if (t == 23) items.Add(new SvcItem("WARP TO PAST", "npcicon_warp", () => { var npc = talking; if (GuideChangerWindow.Instance == null) { Todo("Warp al pasado"); return; } End(false); GuideChangerWindow.Instance.Open(npc.Id, npc.Type, 18); }));
            if (t == 5 && ents.Instances?.m_pWarpData != null) foreach (var wp in ents.Instances.m_pWarpData) { if (wp == null || wp.m_iNpcNumber != talking.NpcNumber) continue; var warp = wp; items.Add(new SvcItem("WARP", "npcicon_warp", () => { if (Sender == null) { note = "sin conexion"; return; } int wc = WarpCheck(warp); if (wc != 0) { note = nhud != null ? nhud.MessageText(wc) : ("WARP " + wc); return; } BeginWarp(talking, warp.m_iWarpNumber); })); break; }   // NpcIconMode.WarpOK: solo la primera fila del NPC
            if (t == 15) items.Add(new SvcItem("WARP", "npcicon_scamp", () => { var npc = talking; if (TransportWindow.Instance == null) { Todo("Transporte"); return; } End(false); TransportWindow.Instance.Open(npc.Id, npc.NpcNumber, 15); }));
            if (t == 16) items.Add(new SvcItem("WARP", "npcicon_monkey", () => { var npc = talking; if (TransportWindow.Instance == null) { Todo("Transporte"); return; } End(false); TransportWindow.Instance.Open(npc.Id, npc.NpcNumber, 16); }));
            if (t == 17) { RaceSystem.RegisterRecall(talking.Id); End(); return; }   // NpcIconMode.StartRXCom: sin menu
            if (t == 14 && RaceSystem.Racing && SymbolOf(talking) == 0 && RaceSystem.Instance != null) { RaceSystem.Instance.EndRace(talking.Id); End(); return; }   // StartEECom con carrera en curso
            if (t == 13) items.Add(new SvcItem(RaceSystem.Racing ? "CANCEL RACE" : "START RACE", "npcicon_ep", () => { var npc = talking; if (RaceSystem.Instance == null) { Todo("Carrera"); return; } RaceSystem.Instance.StartRace(npc.Id); End(false); }));
            if (t == 13 || t == 14) items.Add(new SvcItem("RACE RANK", "npcicon_ep", () => { var npc = talking; if (RaceSystem.Instance == null) { Todo("Ranking"); return; } End(false); RaceSystem.Instance.ShowRank(npc.Id); }));
            if (t == 26) items.Add(new SvcItem("COMBINE ITEMS", "CombiIcon", () => { var npc = talking; if (CombineWindow.Instance == null) { Todo("Combinar"); return; } End(false); CombineWindow.Instance.Open(npc.Id, false); }));
            if (t == 27) items.Add(new SvcItem("ENCHANT ITEMS", "CombiRulesIcon", () => { var npc = talking; if (EnchantWindow.Instance == null) { Todo("Encantar"); return; } End(false); EnchantWindow.Instance.Open(npc.Id); }));
            if (t == 28) items.Add(new SvcItem("BARBER", "npcicon_auction", () => { var npc = talking; if (BarberWindow.Instance == null) { Todo("Peluqueria"); return; } End(false); BarberWindow.Instance.Open(npc.Id); }));
            { int ri = RulesWindow.RuleIndexFor(talking.Service, talking.MapIcon); if (ri > 0 && RulesWindow.Instance != null) { string rt = RulesWindow.Instance.Title(ri); items.Add(new SvcItem(string.IsNullOrEmpty(rt) ? "RULES" : rt, "CombiRulesIcon", () => { End(false); RulesWindow.Instance.Open(ri); })); } }
            items.Add(new SvcItem("CLOSE", "npcicon_exit", () => End()));

            // ---- ventana (NpcIconMode.DoNpcWindow): n incluye CLOSE
            int n = items.Count; float ww = 242f, wh = n == 1 ? 114f : n == 2 ? 171f : n == 3 ? 228f : 54f * n + 66f;   // >3 botones: el original desborda por arriba; aqui crece
            if (bQuest) { ww = 332f; wh = 50f + (nS > 0 ? 25f + nS * 32 : 0f) + (nC > 0 ? 25f + nC * 32 : 0f) + (n > 1 ? 131f : 76f); }
            float wx = Mathf.Floor(Screen.width * 3 / 4) - ww * 0.5f, wy = bQuest ? Screen.height / 2 - wh / 2f : Screen.height / 2 - Mathf.Floor(wh / 2f); var xf = new Xf { P = new Vector2(W * 3f / 4f, H * 0.5f), S = s };
            var win = new Rect(wx, wy, ww, wh);
            if (bQuest) DrawMissionPanel(win, nC, nS, n, xf);
            else if (n == 1) Slice9(U("npc_win"), win, 20, 45, 0, 0, xf); else Slice9(U("multifunc_win"), win, 20, 45, 40, 20, xf);
            if (talking == null) return;
            Txt(J14, new Rect(wx + 15f, wy + 10f, ww - 55f, 40f), talking.Name, TextAnchor.UpperLeft, true, LabelCol, new RectOffset(0, 0, 3, 3), xf);

            bool over, down; var cr = new Rect(wx + ww - 36f, wy + 0.5f, 32f, 32f);
            bool closeClicked = Click(cr, xf, out over, out down);
            if (Event.current.type == EventType.Repaint) { var ct = U(over && !down ? "closeover" : "close"); if (ct != null) GUI.DrawTexture(xf.R(cr), ct, ScaleMode.StretchToFill, true); }
            if (closeClicked) { End(); return; }

            for (int i = 0; i < n; i++)
            {
                var r = new Rect(wx + (ww - 212f) * 0.5f, wy + wh - n * 54f + i * 54f - 10f, 212f, 48f);
                if (ServiceButton(r, " " + Loc.T(items[i].Label), U(items[i].Icon), xf))   // " " + TextManager.GetStr(texto)
                { items[i].Do(); if (talking == null) return; }
            }
        }
    }
    /// <summary>Marca los objetos de efecto colgados de un NPC (marcador de mision, indicador) para excluirlos de NpcInfo.GetBounds.</summary>
    public sealed class NpcFxMark : MonoBehaviour { }
    /// <summary>Reserva si el prefab del marcador no trae animacion: giro de -45 grados/s sobre Y y vaiven suave (ciclo de 8 s del original).</summary>
    public sealed class NpcMarkerSpin : MonoBehaviour
    {
        Vector3 basePos; float t0;
        void Start() { basePos = transform.localPosition; t0 = Random.value * 8f; }
        void Update() { float t = Time.time + t0; transform.localRotation = Quaternion.Euler(0f, -45f * t, 0f); transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t * 0.785f) * 0.05f); }
    }
    /// <summary>AvatarUtil.CallVoicePlay: voces de NPC "VO/<comment2>_<sufijo>.wav" (comment2 = NpcTable string m_strComment2). Volumen de voz y opcion VO como cnOption.soundOption.</summary>
    public static class NpcVoice
    {
        public enum V { Tutorial, NpcClick, NpcClick1, CloseNpcUI, QuestAcce, QuestEnd, NanoComMessage, QuestStartEscort, WarpOK, MoveOK, RaceStart, RaceEnd, BarberOK }
        public static string CommentOf(NpcTableScript nt, int npcNumber)
        {
            if (nt == null || nt.m_pNpcData == null || nt.m_pNpcStringData == null) return "";
            foreach (var r in nt.m_pNpcData)
                if (r != null && r.m_iNpcNumber == npcNumber)
                { var sd = nt.m_pNpcStringData; return r.m_iNpcName >= 0 && r.m_iNpcName < sd.Length && sd[r.m_iNpcName] != null ? sd[r.m_iNpcName].m_strComment2 ?? "" : ""; }
            return "";
        }
        public static void Play(Transform tr, string npcName, V type)
        {
            if (!GameOptions.Cur.VoiceOn || string.IsNullOrEmpty(npcName) || npcName.Length <= 1) return;
            string[] a = npcName.Split('_'); string t2 = npcName; if (a.Length >= 3) t2 = a[0] + "_" + a[1]; string text;
            switch (type)
            {
                case V.Tutorial: text = npcName + "_tut"; break;
                case V.NpcClick: text = npcName + "_greeting0" + Random.Range(1, 3); break;
                case V.NpcClick1: text = t2 + "_qgreeting"; break;
                case V.CloseNpcUI: text = t2 + "_farewell0" + Random.Range(1, 4); break;
                case V.QuestAcce: text = t2 + "_goodluck0" + Random.Range(1, 4); break;
                case V.QuestEnd: text = t2 + "_nicejob0" + Random.Range(1, 4); break;
                case V.NanoComMessage: Sfx.Ui(t2 + "_CommOut0" + Random.Range(1, 4)); return;
                case V.QuestStartEscort: text = npcName + "_laugh0" + Random.Range(1, 4); break;
                case V.WarpOK: text = npcName + "_clickwarp0" + Random.Range(1, 4); break;
                case V.MoveOK: text = npcName + "_clickmove0" + Random.Range(1, 4); break;
                case V.BarberOK: text = npcName + "_clickbarb01"; break;
                case V.RaceStart: text = npcName + "_clickstart0" + Random.Range(1, 4); break;
                case V.RaceEnd: text = npcName + "_racefinished0" + Random.Range(1, 4); break;
                default: return;
            }
            Vector3 pos = tr != null ? tr.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            Sfx.PlayAt("VO/" + text + ".wav", pos, 1f, false, 0f, null, true);
        }
    }
}
