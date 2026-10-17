// BarberWindow: port de BarberMode + BarberGui (NPC tipo 28, icono 24 -> NpcIconMode.StartBarber). SIN COMPILAR NI PROBAR EN UNITY.
// Fuente original: Assembly-CSharp/BarberMode.cs, BarberGui.cs, NpcIconMode.StartBarber.
// Paquetes: REQ_PC_BARBER_OPEN {iNPC_ID} (4 B) -> REP_PC_BARBER_OPEN_SUCC (288 B: iChangeGender, iGenderCost, iBodyCost, iSkinColorCost, iHairStyleCost, iHairColorCost, iFaceStyleCost, iEyeColorCost
//   + aSpecialHair/aSpecialFace/aSpecialHairCost/aSpecialFaceCost[16] que el cliente original no usa);
//   REQ_PC_BARBER_CONFIRM {sPCStyle (76 B)} -> REP_PC_BARBER_CONFIRM (56 B: iErrorCode, iTaros, aUnequipSlots[3], aUnequipItems[3]). iErrorCode 0 = OK, 1 = sin hueco para desequipar ropa de otro genero, 2 = error.
// MonoBehaviour independiente (OnGUI propio): Ensure(host) + Init(...) + Open(npcId); bandera estatica Active. Mismo diseno (CC*) que la creacion de personaje (CharCreateScreen), con vista previa 3D
// por AvatarPreview.CreateExtra (avatar propio clonado, no toca la vista de inventario).
// Diferencias asumidas: (1) el zoom de la camara original (Distance/fHeight) se aproxima recortando la textura del avatar (zoom hacia la cabeza); (2) la voz del NPC (eVoice.BarberOK) no se reproduce;
//   (3) el original busca el indice de cara/pelo con un bucle que para mujeres nunca coincide (iGender==0); aqui se busca igual para ambos generos; (4) al cambiar de genero se actualiza tambien el indice
//   de cara/pelo seleccionado (el original dejaba el valor anterior); (5) los textos de error son los de TextManager del original (Loc.T), la MessageTable no tiene ids propios de la peluqueria.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public sealed class BarberWindow : MonoBehaviour
    {
        public static BarberWindow Instance;
        public static BarberWindow Ensure(GameObject host) { var w = host.GetComponent<BarberWindow>(); if (w == null) w = host.AddComponent<BarberWindow>(); Instance = w; return w; }
        /// <summary>Ventana abierta (o esperando OPEN_SUCC): bloquea clics al mundo y movimiento (anadir a GameMenu.Blocking / busyPrev).</summary>
        public static bool Active { get { return Instance != null && (Instance.open || Instance.pendingOpen || Time.frameCount <= closeFrame + 2); } }
        static int closeFrame = -100;   // el cierre se retrasa 2 frames para que GameMenu no reciba el mismo ESC/clic (abria el mapa del mundo)
        /// <summary>Ventana abierta de verdad (sin contar la espera de OPEN_SUCC): oculta HUD y etiquetas (GameMenu.HudHidden).</summary>
        public static bool Shown { get { return Instance != null && Instance.open; } }

        /// <summary>Carga de texturas del bundle ("TutorialAssets/panelback" + ext). Opcional.</summary>
        public Func<string, Texture> TexLoader;

        PacketDispatcher disp; WorldEntities ents; XdtTableScript tables; PlayerHud hud;
        readonly ConcurrentQueue<Action> q = new ConcurrentQueue<Action>();

        public void Init(PacketDispatcher d, WorldEntities e, XdtTableScript t, PlayerHud h)
        {
            if (disp != null) disp.Unhandled -= OnPacket;
            disp = d; ents = e; tables = t; hud = h; if (disp != null) disp.Unhandled += OnPacket;
        }
        void Awake() { Instance = this; }
        void OnDestroy() { if (disp != null) disp.Unhandled -= OnPacket; DestroyPreview(); if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- estado ----------------------------------------------------------------
        const int Shirt = 0, Pants = 1, Shoes = 2, Hair = 3, Face = 4;     // mismos indices que CharCreateScreen (solo Hair y Face usan CreationItem aqui)
        static readonly string[] HeightText = { "SHORTEST", "SHORT", "MEDIUM", "TALL", "TALLEST" };
        static readonly string[] BuildText = { "HEAVY", "MEDIUM", "LIGHT" };
        static readonly int[] UniqueEyes = { 13, 14 };                      // customizationOptions.uniqueEyes
        const int SkinPageCount = 4, HairPageCount = 3, EyePageCount = 2;   // BarberMode.iSkinPageCount, iHairPageCount, iEyePageCount

        bool open, pendingOpen; float pendT; bool waiting; float waitT; int npcId;
        // sP_FE2CL_REP_PC_BARBER_OPEN_SUCC
        int chgGender, costGender, costBody, costSkin, costHairStyle, costHairColor, costFaceStyle, costEyeColor;
        PcAppearance selfApp; PlayerStyle orig, cur;                        // originalStyle / sStyle
        int originalFace, originalHair;                                     // indices 2..Max+1 de CreationItem
        int selFace, selHair;                                               // iFaceStyleSelected / iHairStyleSelected
        int originalShirt, originalPants, originalShoes;
        bool bShirtBad, bPantsBad, bShoesBad;                               // incompatibles con el genero elegido (se desequiparan)
        int skinPage, hairPage, eyePage;
        CreationItemElement[] rows; EquipmentItemTableScript faceT, hairT;
        readonly Dictionary<int, ItemElement>[] reqSex = new Dictionary<int, ItemElement>[3];
        readonly CcMsg msg = new CcMsg();
        Action afterMsg;

        // vista previa 3D
        AvatarPreview preview; PcAppearance prevApp; int rotDir, zoomDir; float zoom = 1f; bool dragRot; float lastX; string lastSig = "";
        Texture back; float backRetry;

        // ---------------------------------------------------------------- apertura / red ----------------------------------------------------------------
        static void Send(byte[] p) { if (NpcUI.Sender != null) { try { NpcUI.Sender(p); } catch (Exception ex) { Debug.LogWarning("[barber] envio: " + ex.Message); } } }
        static int I(byte[] b, int o) { return BitConverter.ToInt32(b, o); }

        /// <summary>StartBarber: pide los precios al servidor; la ventana se abre al llegar BARBER_OPEN_SUCC.</summary>
        public void Open(int npcId)
        {
            if (open || pendingOpen || ents == null || tables == null) return;
            var inv = InventoryWindow.Instance; if (inv == null || inv.SelfApp == null) { ChatHud.AddEventString(4, "Barber: character data not ready."); return; }
            this.npcId = npcId;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_BARBER_OPEN, 4); ClientPackets.PutInt(p, 4, npcId); Send(p);
            pendingOpen = true; pendT = Time.unscaledTime + 6f;
        }

        void OnPacket(uint type, byte[] b)
        {
            if (b == null) return;
            if (type == PacketIds.P_FE2CL_REP_PC_BARBER_OPEN_SUCC && b.Length >= 32) { var body = b; q.Enqueue(() => OnOpenSucc(body)); }
            else if (type == PacketIds.P_FE2CL_REP_PC_BARBER_CONFIRM && b.Length >= 8) { var body = b; q.Enqueue(() => OnConfirm(body)); }
        }

        void OnOpenSucc(byte[] b)
        {
            if (!pendingOpen) return; pendingOpen = false;
            chgGender = I(b, 0); costGender = I(b, 4); costBody = I(b, 8); costSkin = I(b, 12); costHairStyle = I(b, 16); costHairColor = I(b, 20); costFaceStyle = I(b, 24); costEyeColor = I(b, 28);
            if (!Begin()) { ChatHud.AddEventString(4, "Barber: creation data not available."); return; }
            open = true; waiting = false; msg.Visible = false; afterMsg = null; Sfx.Ui("Open_Screen");
        }

        void OnConfirm(byte[] b)
        {
            waiting = false; if (!open) return;
            int code = I(b, 0);
            if (code == 0)
            {
                int taros = I(b, 4);
                if (hud != null && hud.Data != null) hud.Data.Taros = taros;                 // ownAvatarStatus.iTaros = iTaros
                var app = selfApp;
                for (int i = 0; i < 3; i++)
                {   // aUnequipSlots[i] = ranura de inventario donde queda la prenda de la ranura de equipo i+1
                    int slot = b.Length >= 20 ? I(b, 8 + 4 * i) : -1;
                    if (slot > -1)
                    {
                        if (app != null) app.Equip[i + 1] = default(ItemBase);
                        if (slot < InventoryWindow.Items.Length && b.Length >= 20 + 12 * (i + 1)) InventoryWindow.Items[slot] = ReadItem(b, 20 + 12 * i);
                    }
                }
                if (app != null) { app.Style = cur; RebuildOwnAvatar(app); }
                Exit();
            }
            else if (code == 1) ShowMsg("NOTE!\nThe gender change could not be applied because you do not have space in your inventory to unequip gender-exclusive clothing. Please make space in your inventory and try again.", Exit);
            else if (code == 2) ShowMsg("ERROR\nThe changes were not applied.", Exit);
            else Exit();
        }
        static ItemBase ReadItem(byte[] b, int o) { return new ItemBase { Type = BitConverter.ToInt16(b, o), Id = BitConverter.ToInt16(b, o + 2), Opt = BitConverter.ToInt32(b, o + 4), TimeLimit = BitConverter.ToInt32(b, o + 8) }; }

        /// <summary>Confirm(): REQ_PC_BARBER_CONFIRM con el sPCStyle completo del avatar propio y el estilo nuevo.</summary>
        void ConfirmPurchase()
        {
            var a = selfApp; if (a == null || waiting) return;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_BARBER_CONFIRM, 76);
            BitConverter.GetBytes(a.Uid).CopyTo(p, 4); p[4 + 8] = (byte)(a.NameChecked ? 1 : 0);
            ClientPackets.PutStr(p, 4 + 10, a.FirstName, 9); ClientPackets.PutStr(p, 4 + 28, a.LastName, 17);
            p[4 + 62] = unchecked((byte)cur.Gender); p[4 + 63] = unchecked((byte)cur.FaceStyle); p[4 + 64] = unchecked((byte)cur.HairStyle); p[4 + 65] = unchecked((byte)cur.HairColor);
            p[4 + 66] = unchecked((byte)cur.SkinColor); p[4 + 67] = unchecked((byte)cur.EyeColor); p[4 + 68] = unchecked((byte)cur.Height); p[4 + 69] = unchecked((byte)cur.Body);
            ClientPackets.PutInt(p, 4 + 72, a.Class);
            Send(p); waiting = true; waitT = Time.unscaledTime + 8f;
        }

        // ---------------------------------------------------------------- inicio / cierre ----------------------------------------------------------------
        static PcAppearance CloneApp(PcAppearance a)
        {
            if (a == null) return null;
            var c = (PcAppearance)typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(a, null);
            c.Equip = (ItemBase[])a.Equip.Clone(); return c;
        }

        bool Begin()
        {
            var inv = InventoryWindow.Instance; selfApp = inv != null ? inv.SelfApp : null; if (selfApp == null || tables == null) return false;
            rows = tables.m_pCreationItemTable != null ? tables.m_pCreationItemTable.m_pCreationItemData : null; if (rows == null || rows.Length < 3) return false;
            faceT = tables.m_pFaceItemTable; hairT = tables.m_pHeadItemTable;
            var tt = new[] { tables.m_pShirtsItemTable, tables.m_pPantsItemTable, tables.m_pShoesItemTable };
            for (int k = 0; k < 3; k++)
            {
                reqSex[k] = new Dictionary<int, ItemElement>();
                if (tt[k] != null && tt[k].m_pItemData != null) foreach (var it in tt[k].m_pItemData) if (it != null) reqSex[k][it.m_iItemNumber] = it;
            }
            originalShirt = selfApp.Equip[1].Id; originalPants = selfApp.Equip[2].Id; originalShoes = selfApp.Equip[3].Id;
            DestroyPreview();
            if (inv.Preview != null && inv.Preview.Factory != null) preview = AvatarPreview.CreateExtra(gameObject, inv.Preview.Factory, false); if (preview != null) preview.Yaw = AvatarPreview.FrontYaw;
            SetupCharacter(); zoom = 1f; rotDir = zoomDir = 0; lastSig = "";
            return true;
        }

        /// <summary>SetupCharacter (tambien RESET TO DEFAULT): estilo = el del avatar propio; localiza sus indices de cara/pelo en CreationItem (2..Max+1).</summary>
        void SetupCharacter()
        {
            orig = selfApp.Style; cur = orig;
            bool f = cur.Gender == 2;
            originalFace = FindRow(Face, cur.FaceStyle, f); selFace = originalFace;
            originalHair = FindRow(Hair, cur.HairStyle, f); selHair = originalHair;
            bShirtBad = bPantsBad = bShoesBad = false;
            RefreshPages();
        }
        int FindRow(int kind, int val, bool female)
        {
            int m = MaxOf(kind, female), i = 2;
            while (i < m + 2 && RowVal(kind, i, female) != val) i++;
            return i;                                                          // m + 2 = no encontrado (como el original)
        }
        void RefreshPages()
        {
            int sp = Mathf.Max(1, ActorPalettes.SkinHex.Length / SkinPageCount), hp = Mathf.Max(1, ActorPalettes.HairHex.Length / HairPageCount), ep = Mathf.Max(1, ActorPalettes.EyeHex.Length / EyePageCount);
            skinPage = Mathf.Max(0, (cur.SkinColor - 1) / sp); hairPage = Mathf.Max(0, (cur.HairColor - 1) / hp); eyePage = Mathf.Max(0, (cur.EyeColor - 1) / ep);
        }

        void DestroyPreview() { if (preview != null) { Destroy(preview.gameObject); preview = null; } prevApp = null; }
        void Exit()
        {   // OnExit
            if (open) closeFrame = Time.frameCount;
            open = false; pendingOpen = false; waiting = false; msg.Visible = false; afterMsg = null; DestroyPreview();
        }
        void ShowMsg(string text, Action then = null) { afterMsg = then; msg.Show(Loc.T(text), () => { var a = afterMsg; afterMsg = null; if (a != null) a(); }); Sfx.Ui("Action_Failure01"); }

        // ---------------------------------------------------------------- avatar propio en el mundo ----------------------------------------------------------------
        static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer); }
        /// <summary>Reconstruye el modelo del avatar propio con el estilo nuevo (originalAvatarStatus.SetLook): se crea uno nuevo con PlayerAvatarFactory y su modelo (raiz con ActorShape) sustituye al antiguo
        /// dentro del mismo contenedor, de modo que Self, la camara, la sombra y el movimiento siguen valiendo.</summary>
        void RebuildOwnAvatar(PcAppearance a)
        {
            var self = ents != null ? ents.Self : null; var inv = InventoryWindow.Instance; var factory = inv != null && inv.Preview != null ? inv.Preview.Factory : null;
            if (self == null || factory == null) { Debug.LogWarning("[barber] sin avatar propio o fabrica: el nuevo aspecto se vera al reentrar"); return; }
            try
            {
                var fresh = factory.Create(a, self.transform.parent, true); if (fresh == null) return;
                var ns = fresh.GetComponentInChildren<ActorShape>(true); if (ns == null) { Destroy(fresh); return; }
                var newModel = ns.gameObject; var os = self.GetComponentInChildren<ActorShape>(true); var oldModel = os != null ? os.gameObject : null;
                Transform parent = oldModel != null && oldModel.transform.parent != null ? oldModel.transform.parent : self.transform;
                newModel.transform.SetParent(parent, false); newModel.transform.localPosition = Vector3.zero; newModel.transform.localRotation = Quaternion.identity; newModel.transform.localScale = Vector3.one;
                SetLayer(newModel.transform, self.layer);
                if (oldModel != null) { oldModel.SetActive(false); Destroy(oldModel); }
                Destroy(fresh);
                WorldEntities.ActivateAll(newModel); WorldEntities.PlayIdle(newModel, "yo");
            }
            catch (Exception ex) { Debug.LogWarning("[barber] reconstruir avatar: " + ex.Message); }
        }

        // ---------------------------------------------------------------- datos de estilos ----------------------------------------------------------------
        static int Fld(CreationItemElement r, int kind, bool f) { return kind == Hair ? (f ? r.m_iHairF : r.m_iHairM) : (f ? r.m_iFaceF : r.m_iFaceM); }
        int MaxOf(int kind, bool female) { return rows != null && rows.Length > 1 && rows[1] != null ? Fld(rows[1], kind, female) : 0; }   // la fila 1 guarda los maximos (LoadAssets)
        int RowVal(int kind, int idx, bool female) { return rows != null && idx >= 0 && idx < rows.Length && rows[idx] != null ? Fld(rows[idx], kind, female) : 0; }
        string StyleName(int kind, int id)
        {
            var t = kind == Face ? faceT : hairT;
            if (t != null && t.m_pItemStringData != null)
            {
                if (id >= 0 && id < t.m_pItemStringData.Length && t.m_pItemStringData[id] != null && !string.IsNullOrEmpty(t.m_pItemStringData[id].m_strName)) return t.m_pItemStringData[id].m_strName;   // StringTable(18|21, 1, id)
                if (t.m_pItemData != null) foreach (var it in t.m_pItemData)
                    if (it != null && it.m_iItemNumber == id && it.m_iItemName >= 0 && it.m_iItemName < t.m_pItemStringData.Length && t.m_pItemStringData[it.m_iItemName] != null) return t.m_pItemStringData[it.m_iItemName].m_strName;
            }
            return (kind == Face ? "FACE " : "STYLE ") + id;
        }
        bool Bad(int k, int id, int g)
        {   // (ItemElement.m_iReqSex | gender) != gender
            ItemElement it; if (reqSex[k] == null || !reqSex[k].TryGetValue(id, out it) || it == null) return false; return (it.m_iReqSex | g) != g;
        }
        static Color Hex(int h) { return new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f); }
        bool UniqueEye() { for (int i = 0; i < UniqueEyes.Length; i++) if (UniqueEyes[i] == cur.FaceStyle) return true; return false; }

        // ---------------------------------------------------------------- acciones de edicion (BarberMode.*Selected / BarberGui.Change*) ----------------------------------------------------------------
        void GenderSelected(int g)
        {
            if (g == cur.Gender) return; bool f = g == 2; cur.Gender = g;
            int fm = MaxOf(Face, f); int fi = originalFace > fm ? fm + 1 : originalFace; selFace = fi; cur.FaceStyle = RowVal(Face, fi, f);
            int hm = MaxOf(Hair, f); int hi = originalHair > hm ? hm + 1 : originalHair; selHair = hi; cur.HairStyle = RowVal(Hair, hi, f);
            bShirtBad = Bad(0, originalShirt, g); bPantsBad = Bad(1, originalPants, g); bShoesBad = Bad(2, originalShoes, g);
            if (bShirtBad || bPantsBad || bShoesBad) ShowMsg("NOTE!\nSome of your current clothing items are not compatible with this gender. They will be unequipped and moved back to your inventory.");
        }
        void ChangeHair(int d) { bool f = cur.Gender == 2; int n = MaxOf(Hair, f); if (n < 1) return; selHair = (selHair - 2 + d + n) % n + 2; cur.HairStyle = RowVal(Hair, selHair, f); }
        void ChangeFace(int d) { bool f = cur.Gender == 2; int n = MaxOf(Face, f); if (n < 1) return; selFace = (selFace - 2 + d + n) % n + 2; cur.FaceStyle = RowVal(Face, selFace, f); }

        // ---------------------------------------------------------------- bucle ----------------------------------------------------------------
        void Update()
        {
            Action a; int guard = 0; while (guard++ < 64 && q.TryDequeue(out a)) { try { a(); } catch (Exception ex) { Debug.LogWarning("[barber] " + ex.Message); } }
            if (pendingOpen && Time.unscaledTime > pendT) pendingOpen = false;
            if (waiting && Time.unscaledTime > waitT) waiting = false;
            if (!open) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !msg.Visible) { Sfx.Button(); Exit(); return; }          // botón Atrás de Android / ConfigurableInput Key 4
            float dt = Time.unscaledDeltaTime;
            if (preview != null && rotDir != 0) preview.Yaw += rotDir * dt * 100f;                           // RotateLeft +, RotateRight - (fSpeed = 100)
            if (zoomDir != 0) zoom = Mathf.Clamp(zoom + zoomDir * dt * 0.35f, 1f, 2.2f);
        }

        // ---------------------------------------------------------------- dibujo ----------------------------------------------------------------
        const float OX = -518f, OY = -327f;                                  // grupo rectBG de 1036x654 centrado
        static Rect R(float x, float y, float w, float h) { return new Rect(OX + x, OY + y, w, h); }
        static Matrix4x4 Mat(Vector2 origin, float s) { return Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0f), Quaternion.identity, new Vector3(s, s, 1f)); }

        void LoadBack()
        {
            if (back != null || Time.unscaledTime < backRetry) return; backRetry = Time.unscaledTime + 2f;
            if (TexLoader != null) foreach (var ext in new[] { ".png", ".psd", ".dds", ".tga", "" }) { try { back = TexLoader("TutorialAssets/panelback" + ext); } catch (Exception) { back = null; } if (back != null) break; }
            if (back == null) back = UiKit.Tex("panelback");
        }

        void OnGUI()
        {
            if (!open || Screen.height <= 0) return; var e = Event.current; if (e == null) return;
            if (back == null) LoadBack();
            float W = Screen.width, H = Screen.height, Sc = Mathf.Min(GameOptions.UiScale, W / 1050f, H / 681f);
            var om = GUI.matrix; var oc = GUI.color; int od = GUI.depth; GUI.depth = -1500; GUI.color = Color.white;
            try
            {
                GUI.matrix = Mat(new Vector2(W * 0.5f, H * 0.5f), Sc);
                if (e.type == EventType.Repaint)
                {
                    GUI.color = new Color(0f, 0f, 0f, back != null ? 1f : 0.85f); GUI.DrawTexture(new Rect(-W * 0.5f / Sc, -H * 0.5f / Sc, W / Sc, H / Sc), Texture2D.whiteTexture); GUI.color = Color.white;
                    if (back != null) GUI.DrawTexture(new Rect(-960f, -720f, 1920f, 1440f), back, ScaleMode.ScaleToFit);
                }
                bool en = !waiting && !msg.Visible;
                UiKit.Label(CcUi.F16, R(75, 10, 240, 20), Loc.T("Dr. Barber's Surgery and Haircut"), TextAnchor.MiddleCenter, CcUi.Cyan);
                DrawAvatarWindow(en);
                DrawCreationWindow(en);
                // RESET TO DEFAULT (rectButton6): solo con cambios con coste
                var rr = R(84, 550, 295, 40);
                if (CcUi.Btn(rr, rr, UiKit.Tex("CSEnterButton"), UiKit.Tex("CSEnterGameOver"), Loc.T("RESET TO DEFAULT"), CcUi.F16, Color.white, en && changed, 0)) { Sfx.Button(); SetupCharacter(); lastSig = ""; }
                var cl = R(1036f - 42f, 10, 32, 32);
                if (Skin.Button(cl, "", Skin.Get(Skin.Menu, "CloseButton") ?? Skin.Get(Skin.Inven, "closebut"), en)) { Sfx.Button(); Exit(); }
                if (waiting) UiKit.Label(CcUi.F14, R(84, 530, 295, 20), Loc.T("PLEASE WAIT..."), TextAnchor.MiddleCenter, CcUi.Yellow);
                msg.Draw();
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();   // la ventana es modal
            }
            finally { GUI.matrix = om; GUI.color = oc; GUI.depth = od; }
        }

        bool changed;                                                         // bChanged: coste total > 0

        PcAppearance PreviewApp()
        {
            if (prevApp == null) prevApp = CloneApp(selfApp);
            prevApp.Style = cur;
            for (int i = 0; i < 9; i++) prevApp.Equip[i] = selfApp.Equip[i];
            prevApp.Equip[4] = default(ItemBase);                              // iEquipSlot[4] = 0: sin sombrero para ver el pelo
            if (bShirtBad) prevApp.Equip[1] = default(ItemBase); if (bPantsBad) prevApp.Equip[2] = default(ItemBase); if (bShoesBad) prevApp.Equip[3] = default(ItemBase);
            return prevApp;
        }
        static readonly FieldInfo RetryField = typeof(AvatarPreview).GetField("retry", BindingFlags.NonPublic | BindingFlags.Instance);

        void DrawAvatarWindow(bool en)
        {
            var av = R(77, 44, 307, 498);
            UiKit.Img(av, UiKit.Tex("CCCharacterDisplayArea"));
            if (preview != null)
            {
                var pa = PreviewApp();
                string sig = cur.Gender + "|" + cur.FaceStyle + "|" + cur.HairStyle + "|" + cur.HairColor + "|" + cur.SkinColor + "|" + cur.EyeColor + "|" + cur.Height + "|" + cur.Body + "|" + bShirtBad + bPantsBad + bShoesBad;
                if (sig != lastSig) { lastSig = sig; try { if (RetryField != null) RetryField.SetValue(preview, 0f); } catch (Exception) { } }   // reconstruir ya (AvatarPreview limita a 1 vez cada 1,5 s)
                var tex = preview.Get(pa, 400, 650);
                if (tex != null && Event.current.type == EventType.Repaint)
                {   // zoom: recorte de UV hacia la cabeza (aproxima ZoomIn/ZoomOut de la camara original)
                    float z = Mathf.Max(1f, zoom), w = 1f / z, cy = Mathf.Lerp(0.5f, 0.8f, Mathf.Clamp01((z - 1f) / 1.2f)), y = Mathf.Clamp(cy - w * 0.5f, 0f, 1f - w);
                    GUI.DrawTextureWithTexCoords(av, tex, new Rect(0.5f - w * 0.5f, y, w, w));
                }
            }
            else UiKit.Label(CcUi.ChaletSmall, av, Loc.T("Preview not available"), TextAnchor.MiddleCenter, CcUi.Cyan);

            var rl = R(77 + 10, 44 + 387, 60, 100); var rg = R(77 + 242, 44 + 387, 60, 100);
            bool l = CcUi.Held(rl, CcUi.Pad(rl, 60, 100), UiKit.Tex("CCRotateLeftNormal"), UiKit.Tex("CCRotateLeftOver"), en);
            bool r = CcUi.Held(rg, CcUi.Pad(rg, 60, 100), UiKit.Tex("CCRotateRightNormal"), UiKit.Tex("CCRoatateRightOver"), en);
            var zi = R(77 + 118, 44 + 454, 38, 38); var zo = R(77 + 155, 44 + 454, 38, 38);
            bool zin = CcUi.Held(zi, zi, UiKit.Tex("CCZoomInNormal"), UiKit.Tex("CCZoomInOver"), en);
            bool zout = CcUi.Held(zo, zo, UiKit.Tex("CCZoomOutNormal"), UiKit.Tex("CCZoomOutOver"), en);
            rotDir = (l ? 1 : 0) - (r ? 1 : 0); zoomDir = (zin ? 1 : 0) - (zout ? 1 : 0);

            // arrastre horizontal sobre el avatar = girarlo (tactil)
            int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current;
            if (en)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && av.Contains(e.mousePosition) && GUIUtility.hotControl == 0) { GUIUtility.hotControl = id; dragRot = true; lastX = e.mousePosition.x; e.Use(); }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id) { float dx = e.mousePosition.x - lastX; lastX = e.mousePosition.x; if (preview != null) preview.Yaw -= dx * 0.6f; e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; dragRot = false; e.Use(); }
            }
        }

        static void Plate(Rect r) { var t = UiKit.Tex("CCNameDisplay"); if (t != null) UiKit.Slice(r, t, 5, 5, 5, 5); else UiKit.Fill(r, new Color(0f, 0.05f, 0.15f, 0.8f)); }
        static void Panel12(Rect r) { var t = UiKit.Tex("CCLeftIn12BG"); if (t != null) UiKit.Slice(r, t, 5, 40, 5, 40); else UiKit.Fill(r, new Color(0f, 0.08f, 0.2f, 0.7f)); }
        /// <summary>Flecha 22x22 (BodyRightButton = apunta a la izquierda, BodyLeftButton = a la derecha).</summary>
        static bool Arrow(Rect r, int dir, bool en)
        {
            string n = dir < 0 ? "CCBodyshapeRightNormal" : "CCBodyshapeLeftNormal", o = dir < 0 ? "CCBodyshapeRightOver" : "CCBodyshapeLeftOver";
            return CcUi.Btn(r, CcUi.Pad(r, 36, 26), UiKit.Tex(n), UiKit.Tex(o), null, null, Color.white, en);
        }
        static void TextBox(Rect r, string s)
        {
            var t = UiKit.Tex("CCBodyshapeDisplay"); if (t != null) UiKit.Img(r, t);
            UiKit.Label(CcUi.F14, r, s, TextAnchor.MiddleCenter, CcUi.Cyan);
        }

        void DrawCreationWindow(bool en)
        {
            const float PX = 440f;                                           // rectPos2.x
            UiKit.Img(R(PX, 1, 550, 651), UiKit.Tex("CCRightBG"));
            var cy = CcUi.Cyan2; float num = 0f; var info = chgGender > 0;
            if (info)
            {
                num = 144f;
                UiKit.Label(CcUi.F14, R(PX + 53, 15, 140, 15), Loc.T("CHOOSE BODY"), TextAnchor.MiddleLeft, cy);
                DrawBody(PX + 45f, 33f, en);
            }
            UiKit.Label(CcUi.F14, R(PX + 53, num + 15, 180, 15), Loc.T("CHOOSE FEATURES"), TextAnchor.MiddleLeft, cy);
            DrawFeatures(PX + 45f, num + 36f, en);
            float ry = num + 196f;
            UiKit.Label(CcUi.F14, R(PX + 53, ry, 240, 20), Loc.T("REVIEW CHANGES"), TextAnchor.MiddleLeft, cy);

            bool gChg = orig.Gender != cur.Gender;
            bool face = gChg || orig.FaceStyle != cur.FaceStyle, hair = gChg || orig.HairStyle != cur.HairStyle;
            bool hairC = orig.HairColor != cur.HairColor, skinC = orig.SkinColor != cur.SkinColor, eyeC = orig.EyeColor != cur.EyeColor;
            bool hgt = orig.Height != cur.Height, bld = orig.Body != cur.Body;
            int n = 0, total = 0;
            if (gChg) { Cost(PX, Loc.T("GENDER"), costGender, ry + 28 * n, false); n++; total += costGender; }
            if (bld || hgt) { Cost(PX, Loc.T("BODY SHAPE"), costBody, ry + 28 * n, false); n++; total += costBody; }
            if (skinC) { Cost(PX, Loc.T("SKIN COLOR"), costSkin, ry + 28 * n, false); n++; total += costSkin; }
            if (hair) { Cost(PX, Loc.T("HAIR STYLE"), costHairStyle, ry + 28 * n, false); n++; total += costHairStyle; }
            if (hairC) { Cost(PX, Loc.T("HAIR COLOR"), costHairColor, ry + 28 * n, false); n++; total += costHairColor; }
            if (face) { Cost(PX, Loc.T("FACE STYLE"), costFaceStyle, ry + 28 * n, false); n++; total += costFaceStyle; }
            if (eyeC) { Cost(PX, UniqueEye() ? Loc.T("EYE VARIANT") : Loc.T("EYE COLOR"), costEyeColor, ry + 28 * n, false); n++; total += costEyeColor; }
            changed = total > 0;
            int taros = hud != null && hud.Data != null ? hud.Data.Taros : 0; bool afford = taros >= total;
            Cost(PX, Loc.T("TOTAL:"), total, ry + 28 * n, !afford);
            var cr = R(PX + 130, 595, 295, 40);
            if (CcUi.Btn(cr, cr, UiKit.Tex("CSEnterButton"), UiKit.Tex("CSEnterGameOver"), Loc.T("CONTINUE"), CcUi.F16, Color.white, en && total > 0 && afford, 0)) { Sfx.Ui("Continue_01"); ConfirmPurchase(); }
        }

        /// <summary>AddCostEntry: nombre (53,y+25,240,20), coste a la derecha (300,y+25,180,25) y icono de taros (480,y+18,28,28).</summary>
        void Cost(float px, string name, int cost, float y, bool red)
        {
            var col = red ? new Color(1f, 0.25f, 0.25f) : CcUi.Cyan;
            UiKit.Label(CcUi.F14, R(px + 53, y + 25, 240, 20), name, TextAnchor.MiddleLeft, col);
            // estilo "rightText" del original: JEFFE_14, UpperRight, padding 2; el icono (TarosIcon = tarosfpo 32x32) va en el mismo estilo dentro de (480,y+18,28,28)
            UiKit.Label(CcUi.F14, R(px + 302, y + 27, 176, 21), cost.ToString(), TextAnchor.UpperRight, red ? col : Color.white);
            var ic = UiKit.Tex("tarosfpo"); if (ic != null) UiKit.Img(R(px + 482, y + 20, 24, 24), ic, ScaleMode.ScaleToFit);
        }

        void DrawBody(float gx, float gy, bool en)
        {
            Func<float, float, float, float, Rect> G = (x, y, w, h) => R(gx + x, gy + y, w, h);
            Panel12(G(0, 0, 466, 113));
            for (int i = 0; i < 2; i++)
            {   // BOY / GIRL
                bool girl = i == 1; bool on = (cur.Gender == 2) == girl; var r = G(girl ? 108 : 25, 11, 72, 22);
                var cb = new Rect(r.x, r.y, 24f, 22f);
                if (CcUi.Btn(cb, CcUi.Pad(r, 80, 30), UiKit.Tex(on ? "CCCheckboxChecked" : "CCCheckboxNormal"), UiKit.Tex(on ? "CCCheckboxCkeckedOver" : "CCCheckboxOver"), null, null, Color.white, en)) { if (!on) { Sfx.Button(); GenderSelected(girl ? 2 : 1); } }
                UiKit.Label(CcUi.F14, new Rect(r.x + 28f, r.y, 44f, r.height), Loc.T(girl ? "GIRL" : "BOY"), TextAnchor.MiddleLeft, CcUi.Cyan);
            }
            // altura y complexion (ChangeHeight / ChangeBuild)
            if (Arrow(G(26, 42, 22, 22), -1, en)) { Sfx.Ui("Height_Down"); cur.Height = (cur.Height + 4) % 5; }
            TextBox(G(47, 42, 114, 23), Loc.T(HeightText[Mathf.Clamp(cur.Height, 0, 4)]));
            if (Arrow(G(160, 42, 22, 22), 1, en)) { Sfx.Ui("Height_Up"); cur.Height = (cur.Height + 1) % 5; }
            if (Arrow(G(26, 71, 22, 22), -1, en)) { Sfx.Ui("Girth_Narrow"); cur.Body = (cur.Body + 2) % 3; }
            TextBox(G(47, 71, 114, 23), Loc.T(BuildText[Mathf.Clamp(cur.Body, 0, 2)]));
            if (Arrow(G(160, 71, 22, 22), 1, en)) { Sfx.Ui("Girth_Wide"); cur.Body = (cur.Body + 1) % 3; }
            UiKit.Label(CcUi.F14, G(318, 11, 41, 12), Loc.T("SKIN"), TextAnchor.MiddleCenter, CcUi.Cyan);
            Plate(G(250, 27, 177, 65));
            Colors(0, G, en);
        }

        void DrawFeatures(float gx, float gy, bool en)
        {
            Func<float, float, float, float, Rect> G = (x, y, w, h) => R(gx + x, gy + y, w, h);
            Panel12(G(0, 0, 466, 148));
            if (Arrow(G(40, 14, 22, 22), -1, en)) { Sfx.Button(); ChangeHair(-1); }
            TextBox(G(61, 14, 125, 23), StyleName(Hair, cur.HairStyle));
            if (Arrow(G(185, 14, 22, 22), 1, en)) { Sfx.Button(); ChangeHair(1); }
            UiKit.Label(CcUi.F14, G(315, 17, 48, 17), Loc.T("FACE"), TextAnchor.MiddleCenter, CcUi.Cyan);
            if (Arrow(G(256, 40, 22, 22), -1, en)) { Sfx.Button(); ChangeFace(-1); }
            TextBox(G(277, 40, 130, 23), StyleName(Face, cur.FaceStyle));
            if (Arrow(G(405, 40, 22, 22), 1, en)) { Sfx.Button(); ChangeFace(1); }
            Plate(G(36, 46, 177, 89));
            Colors(1, G, en);
            Plate(G(261, 99, 164, 31));
            Colors(2, G, en);
            UiKit.Label(CcUi.F14, G(290, 79, 98, 18), Loc.T(UniqueEye() ? "EYE VARIANT" : "EYE COLOR"), TextAnchor.MiddleCenter, CcUi.Cyan);
        }

        /// <summary>DoColorSelector: kind 0 piel (2x6, 4 paginas), 1 pelo (3x6, 3 paginas), 2 ojos (1x5, 2 paginas). total = longitud de la paleta; porPagina = total / numeroDePaginas (BarberMode).</summary>
        void Colors(int kind, Func<float, float, float, float, Rect> G, bool en)
        {
            int rowsN, colsN, perPage, total, limit, curVal; float px, py, sx, sy; int[] pal; Rect aL, aR; bool clothesStyle;
            switch (kind)
            {
                case 0: pal = ActorPalettes.SkinHex; total = pal.Length; perPage = Mathf.Max(1, total / SkinPageCount); rowsN = 2; colsN = 6; px = 263; py = 35; sx = 26; sy = 30; limit = 12; curVal = cur.SkinColor; aL = G(231, 28, 23, 64); aR = G(422, 28, 23, 64); clothesStyle = true; break;
                case 1: pal = ActorPalettes.HairHex; total = pal.Length; perPage = Mathf.Max(1, total / HairPageCount); rowsN = 3; colsN = 6; px = 47; py = 55; sx = 27; sy = 26; limit = 18; curVal = cur.HairColor; aL = G(17, 47, 23, 88); aR = G(209, 47, 23, 88); clothesStyle = true; break;
                default: pal = ActorPalettes.EyeHex; total = pal.Length; perPage = Mathf.Max(1, total / EyePageCount); rowsN = 1; colsN = 5; px = 274; py = 105; sx = 30; sy = 30; limit = 5; curVal = cur.EyeColor; aL = G(420, 100, 22, 30); aR = G(244, 100, 22, 30); clothesStyle = false; break;
            }
            int pages = (total + perPage - 1) / perPage; int page = kind == 0 ? skinPage : kind == 1 ? hairPage : eyePage;
            if (total > limit && pages > 1)
            {
                Rect hL = clothesStyle ? CcUi.Pad(aL, 36, aL.height) : CcUi.Pad(aL, 28, 30), hR = clothesStyle ? CcUi.Pad(aR, 36, aR.height) : CcUi.Pad(aR, 28, 30);
                if (CcUi.Btn(aL, hL, UiKit.Tex(clothesStyle ? "CCClothesLeftButtonNormal" : "CCBodyshapeLeftNormal"), UiKit.Tex(clothesStyle ? "CCClothesLeftButtonOver" : "CCBodyshapeLeftOver"), null, null, Color.white, en)) { page = (page - 1 + pages) % pages; Sfx.Button(); }
                if (CcUi.Btn(aR, hR, UiKit.Tex(clothesStyle ? "CCClothesRightButtonNormal" : "CCBodyshapeRightNormal"), UiKit.Tex(clothesStyle ? "CCClothesRightButtonOver" : "CCBodyshapeRightOver"), null, null, Color.white, en)) { page = (page + 1) % pages; Sfx.Button(); }
                if (kind == 0) skinPage = page; else if (kind == 1) hairPage = page; else eyePage = page;
            }
            bool uniq = kind == 2 && UniqueEye(); var outline = UiKit.Tex("CCColorOutline"); var selT = UiKit.Tex("CCSelectedColor"); var inside = UiKit.Tex("CCColorInside");
            int start = page * perPage;
            for (int i = 0; i < rowsN; i++)
                for (int j = 0; j < colsN; j++)
                {
                    int idx = start + i * colsN + j; if (idx >= total || idx >= pal.Length) continue;
                    var sw = G(px + j * sx - 1f, py + i * sy - 1f, 22f, 21f); bool isSel = curVal - 1 == idx;
                    UiKit.Img(sw, outline);
                    if (isSel) UiKit.Img(sw, selT);
                    var o0 = GUI.color; GUI.color = uniq ? Hex(0x0B4689) : Hex(pal[idx]);
                    bool hit = CcUi.Btn(sw, CcUi.Pad(sw, sx, sy), inside, inside, null, null, Color.white, en);
                    GUI.color = o0;
                    if (uniq) UiKit.Label(CcUi.F12, sw, (idx + 1).ToString(), TextAnchor.MiddleCenter, isSel ? Color.yellow : Color.white);   // variantes numeradas
                    if (hit && !isSel)
                    {
                        Sfx.Ui("Select_Color");
                        if (kind == 0) cur.SkinColor = idx + 1; else if (kind == 1) cur.HairColor = idx + 1; else cur.EyeColor = idx + 1;
                    }
                }
        }
    }
}
