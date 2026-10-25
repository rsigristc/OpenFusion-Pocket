// Pop-up de novedades al entrar al juego = cnUpsell en modo noticias (InitNewsGUI / DoNewsGUI), disparado por cnMainGame.CheckUpsell una vez por login.
// Content: API news.php (ffrunner routing) returns one image URL per line; CDN fallbacks support legacy servers. cada imagen se descarga y se muestra a
// ScaleToFit en un panel de 1032x650 (fondo TutorialAssets/panelback.png 1920x1440), boton CONTINUE (10,610,132,27) que pasa de pagina y boton X (987,10,31,31).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using OpenFusion.Api;

namespace OpenFusion.World
{
    public sealed class WelcomePopup : MonoBehaviour
    {
        public static bool Active;
        bool shownThisSession;
        public string MainFileUrl, ServerUrl; public Func<string> UrlProvider, ServerUrlProvider;   // URL del main.unity3d de la version (la base de news.php)
        public Func<string, Texture> BackLoader;       // carga TutorialAssets/panelback.png desde los bundles del cliente (opcional)
        public bool OncePerSession = true;
        readonly List<Texture2D> pages = new List<Texture2D>();
        Texture back; Texture2D tBtn, tBtnOver, tClose, tCloseOver; int page; bool open, fetching; float fade; BitmapFont font; bool fontTried;

        public static WelcomePopup Ensure(GameObject host) { var p = host.GetComponent<WelcomePopup>(); if (p == null) p = host.AddComponent<WelcomePopup>(); return p; }
        /// Se llama al aparecer el personaje propio (CheckUpsell).
        public void Show()
        {
            if (TutorialTraining.Active || open || fetching || (OncePerSession && shownThisSession)) return;
            if (UrlProvider != null) MainFileUrl = UrlProvider();
            if (ServerUrlProvider != null) ServerUrl = ServerUrlProvider();
            if (WelcomeNews.Endpoints(ServerUrl, MainFileUrl).Count == 0) { Debug.LogWarning("[popup] sin URL de noticias"); return; }
            fetching = true;
            StartCoroutine(Fetch());
        }
        IEnumerator Fetch()
        {
            foreach (var newsUrl in WelcomeNews.Endpoints(ServerUrl, MainFileUrl))
            {
                List<string> images;
                using (var req = UnityWebRequest.Get(newsUrl))
                {
                    req.timeout = 12;
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    { Debug.Log("[popup] " + newsUrl + ": " + req.responseCode + " " + req.error); continue; }
                    images = WelcomeNews.Images(newsUrl, req.downloadHandler.text);
                }
                foreach (var imageUrl in images)
                {
                    using (var req = UnityWebRequestTexture.GetTexture(imageUrl, true))
                    {
                        req.timeout = 20;
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success)
                        {
                            var texture = DownloadHandlerTexture.GetContent(req);
                            if (texture != null) pages.Add(texture);
                        }
                        else Debug.LogWarning("[popup] imagen no descargada " + imageUrl + ": " + req.error);
                    }
                }
                if (pages.Count > 0) break;
            }
            fetching = false;
            // A failed fetch must not consume the session's welcome screen.
            if (pages.Count == 0) { Debug.LogWarning("[popup] no se pudieron cargar noticias; se permite reintentar"); yield break; }
            try { if (BackLoader != null) back = BackLoader("TutorialAssets/panelback.png"); } catch (Exception) { }
            tBtn = Resources.Load<Texture2D>("OpenFusion/UI/Textures/blue_button_normal"); tBtnOver = Resources.Load<Texture2D>("OpenFusion/UI/Textures/blue_button_over");
            tClose = Resources.Load<Texture2D>("OpenFusion/UI/Textures/close_normal"); tCloseOver = Resources.Load<Texture2D>("OpenFusion/UI/Textures/close_over");
            page = 0; fade = 0f; shownThisSession = true; open = true; Active = true;
            Debug.Log("[popup] noticias abiertas: " + pages.Count + " paginas");
        }
        void Exit() { open = false; Active = false; foreach (var t in pages) if (t != null) { if (Application.isPlaying) Destroy(t); else DestroyImmediate(t); } pages.Clear(); back = null; page = 0; }
        void OnDisable() { StopAllCoroutines(); fetching = false; Exit(); }
        void OnDestroy() { Exit(); }
        void Update() { if (!open) return; fade = Mathf.Min(1f, fade + 2f * Time.deltaTime); if (Input.GetKeyDown(KeyCode.Escape)) Exit(); }

        void OnGUI()
        {
            if (!open || pages.Count == 0 || Event.current == null) return;
            if (!fontTried) { fontTried = true; font = BitmapFontLoader.Load("JEFFE___14__903"); }
            float W = Screen.width, H = Screen.height, s = Mathf.Max(.1f, Mathf.Min(GameOptions.UiScale, (W - 24f) / 1032f, (H - 24f) / 650f)); var C = new Vector2(W * 0.5f, H * 0.5f);
            Func<Rect, Rect> M = r => new Rect(C.x + (r.x - C.x) * s, C.y + (r.y - C.y) * s, r.width * s, r.height * s);   // ScaleAroundPivot(centro)
            int od = GUI.depth; GUI.depth = -1600; var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, .8f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            var dlg = new Rect((W - 1032f) * 0.5f, (H - 650f) * 0.5f, 1032f, 650f);
            if (back != null) GUI.DrawTexture(M(new Rect((W - 1920f) * 0.5f, (H - 1440f) * 0.5f, 1920f, 1440f)), back, ScaleMode.ScaleToFit);
            var img = pages[page];
            if (img != null) { GUI.color = new Color(1f, 1f, 1f, Mathf.Max(0.01f, fade)); GUI.DrawTexture(M(dlg), img, ScaleMode.ScaleToFit); GUI.color = Color.white; }
            // CONTINUE (10,610,132,27) y X (987,10,31,31) relativos al panel
            var rc = M(new Rect(dlg.x + 10f, dlg.y + 610f, 132f, 27f)); var rx = M(new Rect(dlg.x + 987f, dlg.y + 10f, 31f, 31f));
            bool hc = rc.Contains(Event.current.mousePosition), hx = rx.Contains(Event.current.mousePosition);
            var bt = hc && tBtnOver != null ? tBtnOver : tBtn; if (bt != null) GUI.DrawTexture(rc, bt, ScaleMode.StretchToFill, true);
            if (font != null) font.Draw(new Rect(dlg.x + 10f, dlg.y + 610f, 132f, 27f), "CONTINUE", TextAnchor.MiddleCenter, false, Color.white, null, C, s);
            var xt = hx && tCloseOver != null ? tCloseOver : tClose; if (xt != null) GUI.DrawTexture(rx, xt, ScaleMode.StretchToFill, true);
            GUI.color = old;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (hc)
                {
                    if (pages.Count > 1) { page = (page + 1) % pages.Count; if (page == 0) Exit(); else fade = 0.01f; } else Exit();
                    Event.current.Use();
                }
                else if (hx) { Exit(); Event.current.Use(); }
                else Event.current.Use();
            }
            GUI.depth = od;
        }
    }
}
