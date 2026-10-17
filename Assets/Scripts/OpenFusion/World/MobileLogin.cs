// Pantalla de acceso para el móvil (allí no hay Inspector): usuario + contraseña escritos por la persona, en memoria (no se guardan en disco ni se registran en el log).
// Solo aparece en móvil (o en el Editor con ForceInEditor) y mientras GameClient no tenga usuario y contraseña. SIN COMPILAR.
using UnityEngine;

namespace OpenFusion.World
{
    public sealed class MobileLogin : MonoBehaviour
    {
        public bool ForceInEditor = false;
        GameClient gc; string user = "", pass = ""; string status = "";
        bool Needed { get { return (Application.isMobilePlatform || ForceInEditor) && gc != null && (string.IsNullOrEmpty(gc.Username) || string.IsNullOrEmpty(gc.Password)); } }
        void Start() { gc = GetComponent<GameClient>(); if (gc != null) gc.Status += s => status = s; }
        void OnGUI()
        {
            if (!Needed) return; GUI.depth = -2000;
            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f; float dp = dpi / 160f; float w = Mathf.Min(Screen.width * 0.8f, 420f * dp), h = 300f * dp;
            var box = new Rect((Screen.width - w) / 2f, Mathf.Max(10f * dp, (Screen.height - h) / 2f - Screen.height * 0.12f), w, h);
            var o = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.82f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = o;
            var l = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18f * dp), alignment = TextAnchor.MiddleLeft }; l.normal.textColor = Color.white;
            var f = new GUIStyle(GUI.skin.textField) { fontSize = Mathf.RoundToInt(20f * dp), alignment = TextAnchor.MiddleLeft };
            var b = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20f * dp), fontStyle = FontStyle.Bold };
            float x = box.x + 16f * dp, iw = w - 32f * dp, y = box.y + 12f * dp, rh = 40f * dp;
            GUI.Label(new Rect(x, y, iw, rh), "OpenFusion · Iniciar sesión", l); y += rh + 4f * dp;
            GUI.Label(new Rect(x, y, iw, rh * 0.7f), "Usuario", l); y += rh * 0.7f; user = GUI.TextField(new Rect(x, y, iw, rh), user, 32, f); y += rh + 6f * dp;
            GUI.Label(new Rect(x, y, iw, rh * 0.7f), "Contraseña", l); y += rh * 0.7f; pass = GUI.PasswordField(new Rect(x, y, iw, rh), pass, '•', 64, f); y += rh + 10f * dp;
            GUI.enabled = user.Length > 0 && pass.Length > 0;
            if (GUI.Button(new Rect(x, y, iw, rh * 1.15f), "Entrar", b)) { gc.Username = user.Trim(); gc.Password = pass; pass = ""; }
            GUI.enabled = true;
        }
    }
}
