#pragma warning disable 0108, 0414
// Creación de apariencia del cliente FusionFall original (CnCharCreationMode / CnGuiCharCreation): pantalla única 1020x638 con
// "1. CHOOSE BODY", "2. CHOOSE FEATURES", "3. CHOOSE CLOTHES", ventana del avatar (rotar/zoom/RANDOM), EXIT y CONTINUE (CHAR_CREATE). IMGUI sobre UiKit/CharStage.
// Táctil: áreas táctiles de 36+ unidades (el dibujo conserva el tamaño original) y arrastre horizontal sobre el avatar para girarlo. SIN COMPILAR EN UNITY.
// Usa CcUi/CcMsg de NameCreateScreen.cs (mismo namespace). Textos de mensajes por id (197) y estilos sin dato (bordes de CCRight3BG/CCNameDisplay): marcados "ASUMIDO".
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Tables;
using OpenFusion.Tables.Gen;
using OpenFusion.World;

namespace OpenFusion.Screens
{
    public sealed class CharCreateScreen : MonoBehaviour
    {
        // índices de "kind": ropa 0 camisa, 1 pantalón, 2 zapatos; 3 pelo, 4 cara
        const int Shirt = 0, Pants = 1, Shoes = 2, Hair = 3, Face = 4;
        static readonly string[] HeightText = { "SHORTEST", "SHORT", "MEDIUM", "TALL", "TALLEST" };
        static readonly string[] BuildText = { "HEAVY", "MEDIUM", "LIGHT" };
        static readonly int[] UniqueEyes = { 13, 14 };               // caras con "EYE VARIANT" (UserContainer.customizationOptions.uniqueEyes)
        const int SkinTotal = 36, SkinPerPage = 12, HairTotal = 54, HairPerPage = 18, EyeTotal = 10, EyePerPage = 5;

        LoginSession login; NameSaved name; WorldBootstrap boot; TaskCompletionSource<CharInfo> tcs; bool active, busy, ready;
        CreationItemElement[] rows; readonly EquipmentItemTableScript[] clothesT = new EquipmentItemTableScript[3];
        readonly Dictionary<int, ItemElement>[] byId = new Dictionary<int, ItemElement>[3];
        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        EquipmentItemTableScript faceT, hairT;

        // estado de la apariencia (mismos nombres que iXSelected del original)
        int gender = 1, height, body, skin = 1, hairColor = 1, eyeColor = 1;
        readonly int[] sel = new int[5];                              // índice elegido (2..Max+1) de camisa, pantalón, zapatos, pelo, cara
        int skinPage, hairPage, eyePage;

        CharStage stage; GameObject avatar; CharView view; bool dirty; float headRel = 1.55f;
        int rotDir, zoomDir; bool dragRot; float lastX;
        readonly CcMsg msg = new CcMsg();

        /// <summary>Devuelve el CharInfo que responde CHAR_CREATE_SUCC, o null si se pulsa EXIT (el nombre ya está reservado: el llamador decide qué hacer).</summary>
        public Task<CharInfo> Run(LoginSession login, NameSaved name, WorldBootstrap boot, GameTables tables)
        {
            if (tcs != null && !tcs.Task.IsCompleted) return tcs.Task;
            this.login = login; this.name = name; this.boot = boot; tcs = new TaskCompletionSource<CharInfo>(); busy = false; msg.Visible = false;
            var x = tables != null ? tables.Xdt : null;
            rows = x != null && x.m_pCreationItemTable != null ? x.m_pCreationItemTable.m_pCreationItemData : null;
            ready = rows != null && rows.Length >= 3;
            clothesT[Shirt] = x != null ? x.m_pShirtsItemTable : null; clothesT[Pants] = x != null ? x.m_pPantsItemTable : null; clothesT[Shoes] = x != null ? x.m_pShoesItemTable : null;
            faceT = x != null ? x.m_pFaceItemTable : null; hairT = x != null ? x.m_pHeadItemTable : null;
            for (int k = 0; k < 3; k++)
            {
                byId[k] = new Dictionary<int, ItemElement>();
                if (clothesT[k] != null && clothesT[k].m_pItemData != null) foreach (var it in clothesT[k].m_pItemData) if (it != null) byId[k][it.m_iItemNumber] = it;
            }
            gender = 1; rotDir = 0; zoomDir = 0;
            if (ready) RandomCharacter(true);
            // escenario 3D (avatar re-creado en cada cambio; si no hay fábrica de avatares no hay vista previa)
            if (boot != null && boot.Avatars != null)
            {
                stage = CharStage.Create(boot.Avatars); stage.RimColor = new Color(0xC3 / 255f, 0xCE / 255f, 0xE0 / 255f, 1f) * 0.2f;
            }
            else Debug.LogWarning("[charcreate] sin PlayerAvatarFactory: no hay vista previa del avatar");
            dirty = true; active = true; enabled = true;
            if (!ready) msg.Show("Character creation data is not available.");
            return tcs.Task;
        }

        void Finish(CharInfo r)
        {
            active = false; enabled = false; Cleanup();
            if (tcs != null) tcs.TrySetResult(r);
        }
        void Cleanup() { if (stage != null) { stage.Clear(); Destroy(stage.gameObject); stage = null; } avatar = null; view = null; }
        void OnDestroy() { if (stage != null) Destroy(stage.gameObject); if (tcs != null) tcs.TrySetResult(null); }

        // ---------- datos ----------
        static int Fld(CreationItemElement r, int kind, bool f)
        {
            switch (kind)
            {
                case Shirt: return f ? r.m_iShirtF : r.m_iShirtM;
                case Pants: return f ? r.m_iPantsF : r.m_iPantsM;
                case Shoes: return f ? r.m_iShoesF : r.m_iShoesM;
                case Hair: return f ? r.m_iHairF : r.m_iHairM;
                default: return f ? r.m_iFaceF : r.m_iFaceM;
            }
        }
        int Max(int kind) { return rows != null && rows.Length > 1 && rows[1] != null ? Fld(rows[1], kind, gender == 2) : 0; }   // la fila 1 guarda los máximos
        int RowVal(int kind, int idx) { return rows != null && idx >= 0 && idx < rows.Length && rows[idx] != null ? Fld(rows[idx], kind, gender == 2) : 0; }
        int Pick(int kind) { return RowVal(kind, sel[kind]); }

        static Color Hex(int h) { return new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f); }

        /// <summary>RandomCharacter del original. Primera vez la piel sale de la primera página (1..11); el botón RANDOM usa las 36 (1..35).</summary>
        void RandomCharacter(bool first)
        {
            skin = UnityEngine.Random.Range(1, first ? SkinTotal / 3 : SkinTotal);
            hairColor = UnityEngine.Random.Range(1, HairTotal);
            for (int k = 0; k < 5; k++) { int m = Max(k); sel[k] = UnityEngine.Random.Range(2, Math.Max(1, m) + 2); }
            eyeColor = UnityEngine.Random.Range(1, EyeTotal + 1);
            body = UnityEngine.Random.Range(0, 3); height = UnityEngine.Random.Range(0, 5);
            RefreshPages(); dirty = true;
        }
        void RefreshPages() { skinPage = (skin - 1) / SkinPerPage; hairPage = (hairColor - 1) / HairPerPage; eyePage = (eyeColor - 1) / EyePerPage; }

        /// <summary>ChangeShirt/Pants/Shoes/Hair/Face: ciclo 2..Max+1.</summary>
        void Change(int kind, int d)
        {
            int m = Max(kind); if (m < 1) return;
            if (d > 0) { sel[kind]++; if (sel[kind] > m + 1) sel[kind] = 2; }
            else if (d < 0) { sel[kind]--; if (sel[kind] <= 1) sel[kind] = m + 1; }
            dirty = true;
        }
        void SetGender(int g)
        {
            if (g == gender) return;
            gender = g;
            for (int k = 0; k < 5; k++) { int m = Max(k); if (sel[k] > m + 1) sel[k] = m + 1; if (sel[k] < 2) sel[k] = 2; }   // índices fuera del nuevo máximo se acotan a Max+1
            dirty = true;
        }
        bool UniqueEye() { int f = Pick(Face); for (int i = 0; i < UniqueEyes.Length; i++) if (UniqueEyes[i] == f) return true; return false; }

        PlayerStyle MakeStyle()
        {
            return new PlayerStyle { Gender = gender, FaceStyle = Pick(Face), HairStyle = Pick(Hair), HairColor = hairColor, SkinColor = skin, EyeColor = eyeColor, Height = height, Body = body };
        }
        PcAppearance MakeAppearance()
        {
            var a = new PcAppearance { Uid = name != null ? name.Uid : 0, FirstName = name != null ? name.FirstName : "", LastName = name != null ? name.LastName : "", Style = MakeStyle() };
            // ropa inicial = camisa/pantalón/zapatos en los huecos 1..3 de ItemEquip (PlayerEquip.FromItemEquip)
            a.Equip[1] = new ItemBase { Type = 1, Id = (short)Pick(Shirt) }; a.Equip[2] = new ItemBase { Type = 2, Id = (short)Pick(Pants) }; a.Equip[3] = new ItemBase { Type = 3, Id = (short)Pick(Shoes) };
            return a;
        }

        // ---------- texto / iconos ----------
        string StyleName(int kind, int id)
        {
            var t = kind == Face ? faceT : hairT;
            if (t != null && t.m_pItemStringData != null)
            {
                if (id >= 0 && id < t.m_pItemStringData.Length && t.m_pItemStringData[id] != null && !string.IsNullOrEmpty(t.m_pItemStringData[id].m_strName)) return t.m_pItemStringData[id].m_strName;   // StringTable(18|21, 1, id) del original
                if (t.m_pItemData != null) foreach (var it in t.m_pItemData)
                    if (it != null && it.m_iItemNumber == id && it.m_iItemName >= 0 && it.m_iItemName < t.m_pItemStringData.Length && t.m_pItemStringData[it.m_iItemName] != null) return t.m_pItemStringData[it.m_iItemName].m_strName;
            }
            return (kind == Face ? "FACE " : "STYLE ") + id;
        }

        /// <summary>Fila CreationItem -> ItemTable(23|22|24,0) -> m_iIcon -> IconTable(,2) -> "OpenFusion/Icons/&lt;nombre&gt;". null si no se resuelve (se dibuja una caja con el índice).</summary>
        Texture2D IconOf(int kind, int rowIdx)
        {
            int id = RowVal(kind, rowIdx); var t = clothesT[kind]; if (t == null) return null;
            ItemElement it; if (!byId[kind].TryGetValue(id, out it)) { it = t.m_pItemData != null && id >= 0 && id < t.m_pItemData.Length ? t.m_pItemData[id] : null; }
            if (it == null || t.m_pItemIconData == null || it.m_iIcon < 0 || it.m_iIcon >= t.m_pItemIconData.Length) return null;
            string n = NanoWheelHud.IconName(t.m_pItemIconData[it.m_iIcon]); if (string.IsNullOrEmpty(n)) return null;
            Texture2D tex; if (icons.TryGetValue(n, out tex)) return tex;
            tex = Resources.Load<Texture2D>("OpenFusion/Icons/" + n); icons[n] = tex; return tex;
        }

        // ---------- avatar 3D ----------
        void Respawn()
        {
            if (stage == null) return;
            var old = avatar; float yaw = old != null ? old.transform.eulerAngles.y : float.NaN;
            var go = stage.Spawn(MakeAppearance(), Vector3.zero);
            if (go != null)
            {
                if (!float.IsNaN(yaw)) go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var head = CharStage.Head(go); headRel = head != null ? head.position.y - go.transform.position.y : 1.55f;
            }
            if (view == null) { if (go != null) { int w = Mathf.Clamp(Mathf.RoundToInt(307f * UiKit.S), 256, 1024), h = Mathf.Clamp(Mathf.RoundToInt(498f * UiKit.S), 256, 1600); view = stage.View(go, w, h, 2f, 0.7f); } }
            else view.Avatar = go;           // antes de Remove: así Remove no libera la vista (conserva zoom y RenderTexture)
            if (old != null) stage.Remove(old);
            avatar = go;
        }

        void Update()
        {
            if (!active) return;
            float dt = Time.unscaledDeltaTime;
            if (dirty) { dirty = false; Respawn(); }
            if (avatar != null && rotDir != 0) avatar.transform.Rotate(0f, rotDir * dt * 100f, 0f);   // RotateLeft +, RotateRight - (fSpeed = 100)
            if (view != null && zoomDir != 0)
            {   // ZoomIn/ZoomOut: Distance 1..3 a (3-1)/5 por segundo; la altura sube hacia la cabeza al acercarse
                float dz = (3f - 1f) / 5f, dh = (headRel - 0.7f) / 2.5f;
                if (zoomDir > 0) { if (view.Distance > 1f) view.Distance -= dt * dz; if (view.Distance < 2f && view.Height < headRel) view.Height += dt * dh; }
                else { if (view.Distance < 3f) view.Distance += dt * dz; if (view.Distance < 2f && view.Height > 0.7f) view.Height -= dt * dh; }
                view.Distance = Mathf.Clamp(view.Distance, 1f, 3f); view.Height = Mathf.Clamp(view.Height, 0.7f, Mathf.Max(0.7f, headRel));
            }
        }

        // ---------- dibujo ----------
        void OnGUI()
        {
            if (!active) return;
            GUI.depth = -10;
            CcUi.Background();
            UiKit.BeginCenter();
            try
            {
                var rs = UiKit.RefSize; float f = Mathf.Min(1f, (rs.x - 8f) / 1020f, (rs.y - 8f) / 638f);   // en pantallas estrechas se reduce toda la ventana
                GUI.matrix = GUI.matrix * Matrix4x4.Scale(new Vector3(f, f, 1f));
                DrawAll();
                msg.Draw();
            }
            finally { UiKit.End(); }
        }

        const float OX = -510f, OY = -319f;
        static Rect R(float x, float y, float w, float h) { return new Rect(OX + x, OY + y, w, h); }

        void DrawAll()
        {
            var e = Event.current; bool en = !busy && !msg.Visible;
            bool eg = en && ready;
            var bg = UiKit.Tex("CCBG"); if (bg != null) UiKit.Slice(R(0, 0, 1020, 638), bg, 10, 10, 0, 0); else UiKit.Fill(R(0, 0, 1020, 638), new Color(0.02f, 0.1f, 0.25f, 0.95f));
            UiKit.Label(CcUi.F16, R(10, 10, 240, 20), "CHARACTER CREATION", TextAnchor.MiddleCenter, CcUi.Cyan);
            DrawAvatarWindow(eg);
            DrawRightPanel(eg);

            var cr = R(84, 550, 295, 40);
            if (CcUi.Btn(cr, CcUi.Pad(cr, 295, 40), UiKit.Tex("CSEnterButton"), UiKit.Tex("CSEnterGameOver"), "CONTINUE", CcUi.F16, Color.white, eg, 0)) Submit();
            var xr = R(22, 600, 68, 28);
            if (CcUi.Btn(xr, CcUi.Pad(xr, 68, 36), UiKit.Tex("QuitButNor"), UiKit.Tex("QuitButUp"), "EXIT", CcUi.F14, CcUi.Cyan, en, 5)) AskExit();
            if (busy) UiKit.Label(CcUi.F14, R(84, 530, 295, 20), "CREATING CHARACTER...", TextAnchor.MiddleCenter, CcUi.Yellow);
            if (en && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { AskExit(); e.Use(); }   // botón Atrás de Android
        }

        void DrawAvatarWindow(bool en)
        {
            var av = R(77, 44, 307, 498);
            UiKit.Img(av, UiKit.Tex("CCCharacterDisplayArea"));
            if (view != null && view.Texture != null) UiKit.Img(av, view.Texture);
            else if (stage == null) UiKit.Label(CcUi.ChaletSmall, av, "Preview not available", TextAnchor.MiddleCenter, CcUi.Cyan);

            var rl = R(77 + 10, 44 + 387, 60, 100); var rr = R(77 + 242, 44 + 387, 60, 100);
            bool l = CcUi.Held(rl, CcUi.Pad(rl, 60, 100), UiKit.Tex("CCRotateLeftNormal"), UiKit.Tex("CCRotateLeftOver"), en);
            bool r = CcUi.Held(rr, CcUi.Pad(rr, 60, 100), UiKit.Tex("CCRotateRightNormal"), UiKit.Tex("CCRoatateRightOver"), en);
            var zi = R(77 + 118, 44 + 454, 38, 38); var zo = R(77 + 155, 44 + 454, 38, 38);
            bool zin = CcUi.Held(zi, zi, UiKit.Tex("CCZoomInNormal"), UiKit.Tex("CCZoomInOver"), en);
            bool zout = CcUi.Held(zo, zo, UiKit.Tex("CCZoomOutNormal"), UiKit.Tex("CCZoomOutOver"), en);
            rotDir = (l ? 1 : 0) - (r ? 1 : 0); zoomDir = (zin ? 1 : 0) - (zout ? 1 : 0);
            var rnd = R(77 + 210, 44 + 5, 90, 20);
            if (CcUi.Btn(rnd, CcUi.Pad(rnd, 90, 36), UiKit.Tex("QuitButNor"), UiKit.Tex("QuitButUp"), "RANDOM", CcUi.F14, CcUi.Grey, en, 5)) RandomCharacter(false);

            // arrastre horizontal sobre el avatar = girarlo (táctil)
            int id = GUIUtility.GetControlID(FocusType.Passive); var e = Event.current;
            if (en)
            {
                if (e.type == EventType.MouseDown && e.button == 0 && av.Contains(e.mousePosition) && GUIUtility.hotControl == 0) { GUIUtility.hotControl = id; dragRot = true; lastX = e.mousePosition.x; e.Use(); }
                else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id) { float dx = e.mousePosition.x - lastX; lastX = e.mousePosition.x; if (avatar != null) avatar.transform.Rotate(0f, -dx * 0.6f, 0f); e.Use(); }
                else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; dragRot = false; e.Use(); }
            }
        }

        void DrawRightPanel(bool en)
        {
            UiKit.Img(R(450, 1, 550, 637), UiKit.Tex("CCRightBG"));
            var cy = CcUi.Cyan2;
            UiKit.Label(CcUi.F14, R(450 + 53, 15, 140, 15), "1. CHOOSE BODY", TextAnchor.MiddleLeft, cy);
            UiKit.Label(CcUi.F14, R(450 + 53, 159, 180, 15), "2. CHOOSE FEATURES", TextAnchor.MiddleLeft, cy);
            UiKit.Label(CcUi.F14, R(450 + 53, 342, 170, 15), "3. CHOOSE CLOTHES", TextAnchor.MiddleLeft, cy);
            DrawBody(495f, 33f, en); DrawFeatures(495f, 180f, en); DrawClothes(495f, 362f, en);
        }

        static void Plate(Rect r) { var t = UiKit.Tex("CCNameDisplay"); if (t != null) UiKit.Slice(r, t, 5, 5, 5, 5); else UiKit.Fill(r, new Color(0f, 0.05f, 0.15f, 0.8f)); }   // borde ASUMIDO
        static void Panel12(Rect r) { var t = UiKit.Tex("CCLeftIn12BG"); if (t != null) UiKit.Slice(r, t, 5, 40, 5, 40); else UiKit.Fill(r, new Color(0f, 0.08f, 0.2f, 0.7f)); }

        /// <summary>Flecha 22x22 de las filas de cuerpo/rasgos: dir -1 = izquierda (textura CCBodyshapeRight, apunta a la izquierda), +1 = derecha.</summary>
        bool Arrow(Rect r, int dir, bool en)
        {
            string n = dir < 0 ? "CCBodyshapeRightNormal" : "CCBodyshapeLeftNormal", o = dir < 0 ? "CCBodyshapeRightOver" : "CCBodyshapeLeftOver";
            return CcUi.Btn(r, CcUi.Pad(r, 36, 26), UiKit.Tex(n), UiKit.Tex(o), null, null, Color.white, en);
        }
        void TextBox(Rect r, string s)
        {
            var t = UiKit.Tex("CCBodyshapeDisplay"); if (t != null) UiKit.Img(r, t);
            UiKit.Label(CcUi.F14, r, s, TextAnchor.MiddleCenter, CcUi.Cyan);
        }

        void DrawBody(float gx, float gy, bool en)
        {
            Func<float, float, float, float, Rect> G = (x, y, w, h) => new Rect(OX + gx + x, OY + gy + y, w, h);
            Panel12(G(0, 0, 466, 113));
            // BOY / GIRL (casillas)
            for (int i = 0; i < 2; i++)
            {
                bool girl = i == 1; bool on = (gender == 2) == girl; var r = G(girl ? 108 : 25, 11, 72, 22);
                var cb = new Rect(r.x, r.y, 24f, 22f);
                if (CcUi.Btn(cb, CcUi.Pad(r, 80, 30), UiKit.Tex(on ? "CCCheckboxChecked" : "CCCheckboxNormal"), UiKit.Tex(on ? "CCCheckboxCkeckedOver" : "CCCheckboxOver"), null, null, Color.white, en)) SetGender(girl ? 2 : 1);
                UiKit.Label(CcUi.F14, new Rect(r.x + 28f, r.y, 44f, r.height), girl ? "GIRL" : "BOY", TextAnchor.MiddleLeft, CcUi.Cyan);
            }
            // altura y complexión
            if (Arrow(G(26, 42, 22, 22), -1, en)) { height = (height + 4) % 5; dirty = true; }
            TextBox(G(47, 42, 114, 23), HeightText[Mathf.Clamp(height, 0, 4)]);
            if (Arrow(G(160, 42, 22, 22), 1, en)) { height = (height + 1) % 5; dirty = true; }
            if (Arrow(G(26, 71, 22, 22), -1, en)) { body = (body + 2) % 3; dirty = true; }
            TextBox(G(47, 71, 114, 23), BuildText[Mathf.Clamp(body, 0, 2)]);
            if (Arrow(G(160, 71, 22, 22), 1, en)) { body = (body + 1) % 3; dirty = true; }
            UiKit.Label(CcUi.F14, G(318, 11, 41, 12), "SKIN", TextAnchor.MiddleCenter, CcUi.Cyan);
            Plate(G(250, 27, 177, 65));
            Colors(0, G, en);
        }

        void DrawFeatures(float gx, float gy, bool en)
        {
            Func<float, float, float, float, Rect> G = (x, y, w, h) => new Rect(OX + gx + x, OY + gy + y, w, h);
            Panel12(G(0, 0, 466, 148));
            if (Arrow(G(40, 14, 22, 22), -1, en)) Change(Hair, -1);
            TextBox(G(61, 14, 125, 23), StyleName(Hair, Pick(Hair)));
            if (Arrow(G(185, 14, 22, 22), 1, en)) Change(Hair, 1);
            UiKit.Label(CcUi.F14, G(315, 17, 48, 17), "FACE", TextAnchor.MiddleCenter, CcUi.Cyan);
            if (Arrow(G(256, 40, 22, 22), -1, en)) Change(Face, -1);
            TextBox(G(277, 40, 130, 23), StyleName(Face, Pick(Face)));
            if (Arrow(G(405, 40, 22, 22), 1, en)) Change(Face, 1);
            Plate(G(36, 46, 177, 89));
            Colors(1, G, en);
            Plate(G(261, 99, 164, 31));
            Colors(2, G, en);
            UiKit.Label(CcUi.F14, G(290, 79, 98, 18), UniqueEye() ? "EYE VARIANT" : "EYE COLOR", TextAnchor.MiddleCenter, CcUi.Cyan);
        }

        /// <summary>DoColorSelector: kind 0 piel (2x6, 3 páginas de 12 de 36), 1 pelo (3x6, 3 de 18), 2 ojos (1x5, 2 de 5).</summary>
        void Colors(int kind, Func<float, float, float, float, Rect> G, bool en)
        {
            int rowsN, colsN, perPage, total, limit, curVal; float px, py, sx, sy; int[] pal; Rect aL, aR; bool clothesStyle;
            switch (kind)
            {
                case 0: rowsN = 2; colsN = 6; px = 263; py = 35; sx = 26; sy = 30; perPage = SkinPerPage; total = SkinTotal; limit = 12; curVal = skin; pal = ActorPalettes.SkinHex; aL = G(231, 28, 23, 64); aR = G(422, 28, 23, 64); clothesStyle = true; break;
                case 1: rowsN = 3; colsN = 6; px = 47; py = 55; sx = 27; sy = 26; perPage = HairPerPage; total = HairTotal; limit = 18; curVal = hairColor; pal = ActorPalettes.HairHex; aL = G(17, 47, 23, 88); aR = G(209, 47, 23, 88); clothesStyle = true; break;
                default: rowsN = 1; colsN = 5; px = 274; py = 105; sx = 30; sy = 30; perPage = EyePerPage; total = EyeTotal; limit = 5; curVal = eyeColor; pal = ActorPalettes.EyeHex; aL = G(420, 100, 22, 30); aR = G(244, 100, 22, 30); clothesStyle = false; break;
            }
            int pages = (total + perPage - 1) / perPage; int page = kind == 0 ? skinPage : kind == 1 ? hairPage : eyePage;
            if (total > limit && pages > 1)
            {
                Rect hL = clothesStyle ? CcUi.Pad(aL, 36, aL.height) : CcUi.Pad(aL, 28, 30), hR = clothesStyle ? CcUi.Pad(aR, 36, aR.height) : CcUi.Pad(aR, 28, 30);
                // "aL" resta una página, "aR" suma una (en los ojos el original los cruza, pero con 2 páginas da igual)
                if (CcUi.Btn(aL, hL, UiKit.Tex(clothesStyle ? "CCClothesLeftButtonNormal" : "CCBodyshapeLeftNormal"), UiKit.Tex(clothesStyle ? "CCClothesLeftButtonOver" : "CCBodyshapeLeftOver"), null, null, Color.white, en)) page = (page - 1 + pages) % pages;
                if (CcUi.Btn(aR, hR, UiKit.Tex(clothesStyle ? "CCClothesRightButtonNormal" : "CCBodyshapeRightNormal"), UiKit.Tex(clothesStyle ? "CCClothesRightButtonOver" : "CCBodyshapeRightOver"), null, null, Color.white, en)) page = (page + 1) % pages;
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
                    if (uniq) UiKit.Label(CcUi.F12, sw, (idx + 1).ToString(), TextAnchor.MiddleCenter, isSel ? Color.yellow : Color.white);   // variantes numeradas 1..10
                    if (hit && !isSel)
                    {
                        if (kind == 0) skin = idx + 1; else if (kind == 1) hairColor = idx + 1; else eyeColor = idx + 1;
                        dirty = true;
                    }
                }
        }

        void DrawClothes(float gx, float gy, bool en)
        {
            Func<float, float, float, float, Rect> G = (x, y, w, h) => new Rect(OX + gx + x, OY + gy + y, w, h);
            var p3 = UiKit.Tex("CCLeftIn3BG"); if (p3 != null) UiKit.Slice(G(0, 0, 466, 267), p3, 5, 5, 5, 5); else UiKit.Fill(G(0, 0, 466, 267), new Color(0f, 0.08f, 0.2f, 0.7f));   // borde ASUMIDO
            UiKit.Img(G(40, 15, 387, 239), UiKit.Tex("CCClothesBG"));
            string[] lt = { "CCClothesLeftTopButtonNormal", "CCClothesLeftButtonNormal", "CCClothesLeftBottomButtonNormal" };
            string[] lo = { "CCClothesLeftTopOver", "CCClothesLeftButtonOver", "CCClothesLeftBottomButtonOver" };
            string[] rt = { "CCClothesRightTopButtonNormal", "CCClothesRightButtonNormal", "CCClothesRightBottomButtonNormal" };
            string[] ro = { "CCClothesRightTopOver", "CCClothesRightButtonOver", "CCClothesRightBottomOver" };
            float[] ay = { 16f, 95f, 174f }, iy = { 25f, 105f, 183f }; float[] ix = { 51f, 126f, 202f, 277f, 353f }; float[] al = { 0.5f, 0.75f, 1f, 0.75f, 0.5f };
            for (int k = 0; k < 3; k++)
            {
                var l = G(18, ay[k], 23, 79); var r = G(425, ay[k], 23, 79);
                if (CcUi.Btn(l, CcUi.Pad(l, 36, 79), UiKit.Tex(lt[k]), UiKit.Tex(lo[k]), null, null, Color.white, en)) Change(k, -1);
                if (CcUi.Btn(r, CcUi.Pad(r, 36, 79), UiKit.Tex(rt[k]), UiKit.Tex(ro[k]), null, null, Color.white, en)) Change(k, 1);
            }
            for (int k = 0; k < 3; k++)
            {
                int m = Max(k); if (m < 1) continue;
                for (int c = 0; c < 5; c++)
                {
                    int rowIdx = 2 + (((sel[k] - 2 + (c - 2)) % m) + m) % m;      // ventana cíclica de 5 sobre los índices 2..Max+1 centrada en el elegido
                    var ir = G(ix[c], iy[k], 62, 62); var tex = IconOf(k, rowIdx);
                    var o0 = GUI.color; GUI.color = new Color(1f, 1f, 1f, al[c] * o0.a);
                    if (tex != null) UiKit.Img(ir, tex, ScaleMode.ScaleToFit);
                    else { UiKit.Fill(ir, new Color(0f, 0.15f, 0.35f, 0.8f)); UiKit.Outline(ir, new Color(0f, 0.9f, 1f, 0.7f)); UiKit.Label(CcUi.F12, ir, (rowIdx - 1).ToString(), TextAnchor.MiddleCenter, CcUi.Cyan); }   // sin icono: caja con el índice
                    GUI.color = o0;
                    if (c != 2)
                    {   // icono exterior = cambiar x2, interior = x1 (a la izquierda -1, a la derecha +1)
                        int steps = Mathf.Abs(c - 2); int dir = c < 2 ? -1 : 1;
                        if (CcUi.Tap(ir, en)) { for (int s = 0; s < steps; s++) Change(k, dir); }
                    }
                }
            }
        }

        // ---------- lógica ----------
        void AskExit() { msg.Confirm("Would you like to exit Character Creation?\nYou can come back later and finish.", () => Finish(null)); }   // msg 197

        /// <summary>CONTINUE (EnterGame): REQ_CHAR_CREATE con el estilo, los ids de camisa/pantalón/zapatos y los índices elegidos.</summary>
        async void Submit()
        {
            if (busy || !ready) return;
            busy = true;
            try
            {
                var st = MakeStyle();
                CharInfo ci = await login.CreateCharacterAsync(name.Uid, st, Pick(Shirt), Pick(Pants), Pick(Shoes), sel[Shirt], sel[Pants], sel[Shoes], sel[Face], sel[Hair]);
                busy = false; Finish(ci);
            }
            catch (LoginException le) { busy = false; msg.Show("Character creation failed. (" + le.Code + ")\n" + le.Message); }
            catch (Exception ex) { busy = false; msg.Show("Character creation failed.\n" + ex.Message); }
        }
    }
}
