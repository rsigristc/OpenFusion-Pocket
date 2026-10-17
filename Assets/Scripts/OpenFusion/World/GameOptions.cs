// Opciones del juego (port de cnOption: cnGraphicOption/cnSoundOption/cnDisplayOption/cnSocialOption) con persistencia en PlayerPrefs, copia de seguridad (Push/Pop) y presets de nivel de detalle.
// Las claves de PlayerPrefs son las del original ("Detail Level", "Visibility", "Particle Effect", "Toon shading", "Glow", "AnisotropicFiltering", "Fade", "Soft Vegetation", "Character Shadow", "Texture Quality", "Master Volume"...).
// Extras propios del port movil (NO existen en el original): escala de render, antialiasing, intensidad del glow y color; claves "of_x_*". Los FPS reutilizan "of_set_fps" del lanzador.
// SIN COMPILAR EN UNITY.
using System;
using UnityEngine;

namespace OpenFusion.World
{
    public enum DetailLevel { BestQuality, GoodQuality, Balanced, GoodPerformance, BestPerformance, Custom }
    public enum ShadowDetail { PlayerOnly, AllCharacters }
    public enum TextureQuality { High, Medium, Low }

    [Serializable]
    public sealed class OptionData
    {
        // ---- grafico (cnGraphicOption) ----
        public DetailLevel Detail = DetailLevel.Balanced;
        public float Visibility = 0.5f;            // 0..1
        public int ParticleEffect = 2;             // 0..3
        public bool Toon = true, Glow = true, Aniso = true, Fade = true, SoftVegetation = false;
        public ShadowDetail Shadow = ShadowDetail.PlayerOnly;
        public TextureQuality Texture = TextureQuality.High;
        // ---- extras del port movil ----
        public float RenderScale = 1f;             // 0.5..1
        public int AA = 0;                         // 0 off, 1 FXAA, 2 MSAA x2, 3 MSAA x4
        public float GlowStrength = 0.5f;          // 0..1 (intensidad del bloom)
        public float Saturation = 0.5f;            // 0..1 (0,5 = neutro)
        public int Fps = 1;                        // 0 = 30, 1 = 60, 2 = sin limite (mismo indice que el lanzador)
        // ---- sonido (cnSoundOption) ----
        public bool MasterOn = true, SfxOn = true, VoiceOn = true, AmbientOn = true, MusicOn = true;
        public float Master = 0.5f, Sfx = 0.5f, Voice = 0.5f, Ambient = 0.5f, Music = 0.5f;
        // ---- display (cnDisplayOption) ----
        public bool MyName, OtherName, GroupName, NpcName, MonsterName, FloatNumber, BalloonChat = true, AnimatedNanos = true, NanocomClock = true, GameHint = true, EventMsg1 = true, EventMsg2, ChatTimeStamp;
        public bool CurrentObjective = true, GuideEmail = true, WayPoint = true;
        public float UiFactor = 1f;                // cnDisplayOption.fUiScaleFactor: 0 = sin escala, 1 = escala para ajustar (H/681)
        public int ChatGeneral = 1, ChatBuddy = 2, ChatGroup = 3;   // cnTextColorOption (índices de Palette)
        // ---- social (cnSocialOption) ----
        public bool AllowGroup = true, AllowBuddy = true, AllowTrade = true;
        public bool VariableJump = true;           // cnDisplayOption.bVariableJump

        public OptionData Clone() { return (OptionData)MemberwiseClone(); }
    }

    public static class GameOptions
    {
        /// cnOption.arrayColor (24 colores del chat)
        public static readonly Color[] Palette =
        {
            new Color(1f,0f,0f), new Color(1f,.6f,0f), new Color(.5f,.66f,0f), new Color(0f,.39f,0f), new Color(0f,.47f,.47f), new Color(.5f,0f,.5f),
            new Color(1f,1f,0f), new Color(0f,0f,0f), new Color(1f,0f,1f), new Color(1f,.78f,0f), new Color(1f,1f,0f), new Color(0f,1f,0f),
            new Color(0f,1f,1f), new Color(0f,.7f,1f), new Color(.56f,.12f,.35f), new Color(.39f,.39f,.39f), new Color(1f,.6f,.8f), new Color(1f,.8f,.6f),
            new Color(1f,1f,.6f), new Color(.8f,1f,.8f), new Color(.8f,1f,1f), new Color(.6f,.8f,1f), new Color(.8f,.6f,1f), new Color(1f,1f,1f)
        };
        /// FFGUIUtility.GetUiScale = Lerp(1, Screen.height/681, uiScaleFactor)
        public static float UiScale { get { return Mathf.Lerp(1f, Screen.height / 681f, Cur.UiFactor); } }
        public static void DefaultSocial(OptionData o) { o.AllowGroup = true; o.AllowBuddy = true; o.AllowTrade = true; }
        public static void DefaultChatColors(OptionData o) { o.ChatGeneral = 1; o.ChatBuddy = 2; o.ChatGroup = 3; }
        public static OptionData Cur = new OptionData();
        static OptionData backup;
        public static bool Loaded;
        public static event Action Applied;

        // ---- presets (cnGraphicOption.SetDetailLevel) ----
        public static void SetDetailLevel(OptionData o, DetailLevel l)
        {
            o.Detail = l;
            switch (l)
            {
                case DetailLevel.BestQuality: Set(o, 1f, 3, true, true, true, true, true, ShadowDetail.AllCharacters, TextureQuality.High); break;
                case DetailLevel.GoodQuality: Set(o, 0.75f, 3, true, true, true, true, true, ShadowDetail.AllCharacters, TextureQuality.High); break;
                case DetailLevel.Balanced: Set(o, 0.5f, 2, true, true, true, true, false, ShadowDetail.PlayerOnly, TextureQuality.High); break;
                case DetailLevel.GoodPerformance: Set(o, 0.25f, 1, true, true, false, false, false, ShadowDetail.PlayerOnly, TextureQuality.Medium); break;
                case DetailLevel.BestPerformance: Set(o, 0f, 0, false, false, false, false, false, ShadowDetail.PlayerOnly, TextureQuality.Low); break;
            }
            if (l != DetailLevel.Custom) ExtrasForLevel(o, l);
        }
        static void Set(OptionData o, float vis, int pe, bool toon, bool glow, bool an, bool fade, bool sv, ShadowDetail sh, TextureQuality tq)
        { o.Visibility = vis; o.ParticleEffect = pe; o.Toon = toon; o.Glow = glow; o.Aniso = an; o.Fade = fade; o.SoftVegetation = sv; o.Shadow = sh; o.Texture = tq; }
        // extras: el nivel tambien ajusta resolucion interna y antialiasing para que los presets sirvan en moviles
        static void ExtrasForLevel(OptionData o, DetailLevel l)
        {
            switch (l)
            {
                case DetailLevel.BestQuality: o.RenderScale = 1f; o.AA = Application.isMobilePlatform ? 1 : 3; o.GlowStrength = 0.6f; break;
                case DetailLevel.GoodQuality: o.RenderScale = 1f; o.AA = 1; o.GlowStrength = 0.5f; break;
                case DetailLevel.Balanced: o.RenderScale = 0.85f; o.AA = Application.isMobilePlatform ? 1 : 2; o.GlowStrength = 0.5f; break;
                case DetailLevel.GoodPerformance: o.RenderScale = 0.7f; o.AA = 0; o.GlowStrength = 0.4f; break;
                case DetailLevel.BestPerformance: o.RenderScale = 0.6f; o.AA = 0; o.GlowStrength = 0f; break;
            }
        }
        /// cnGraphicOption.GetCurrentLevel: por hardware. En movil se parte de Balanced (o GoodPerformance con poca RAM).
        public static DetailLevel DefaultLevel()
        {
            int vram = Math.Abs(SystemInfo.graphicsMemorySize), ram = SystemInfo.systemMemorySize, cpu = SystemInfo.processorCount;
            if (Application.isMobilePlatform)
            {
                if (ram <= 0 || ram < 3000) return DetailLevel.BestPerformance;
                return ram >= 6000 ? DetailLevel.Balanced : DetailLevel.GoodPerformance;
            }
            if (vram >= 512 && ram >= 2000 && cpu >= 2) return DetailLevel.BestQuality;
            if (vram >= 512 && cpu >= 2) return DetailLevel.GoodQuality;
            if ((vram >= 512 && ram >= 2000) || cpu >= 2) return DetailLevel.Balanced;
            return vram >= 256 ? DetailLevel.GoodPerformance : DetailLevel.BestPerformance;
        }
        public static void DefaultGraphics(OptionData o) { var f = o.Fps; SetDetailLevel(o, DefaultLevel()); o.Saturation = 0.5f; o.Fps = f; }
        public static void DefaultSound(OptionData o)
        { o.MasterOn = o.SfxOn = o.VoiceOn = o.AmbientOn = o.MusicOn = true; o.Master = o.Sfx = o.Voice = o.Ambient = o.Music = 0.5f; }
        public static void DefaultDisplay(OptionData o)
        {
            o.MyName = true; o.OtherName = false; o.GroupName = false; o.NpcName = true; o.MonsterName = false; o.FloatNumber = true; o.BalloonChat = true; o.AnimatedNanos = true; o.NanocomClock = true;
            o.GameHint = true; o.EventMsg1 = true; o.EventMsg2 = false; o.ChatTimeStamp = false; o.UiFactor = 1f;
        }

        // ---- persistencia ----
        static bool B(string k, bool d) { string s = PlayerPrefs.GetString(k, null); return string.IsNullOrEmpty(s) ? d : s == "true"; }
        static void SB(string k, bool v) { PlayerPrefs.SetString(k, v ? "true" : "false"); }
        public static void Load()
        {
            var o = new OptionData(); DefaultDisplay(o);
            if (PlayerPrefs.HasKey("Detail Level")) o.Detail = (DetailLevel)Mathf.Clamp(PlayerPrefs.GetInt("Detail Level", 2), 0, 5); else SetDetailLevel(o, DefaultLevel());
            if (PlayerPrefs.HasKey("Detail Level"))
            {
                o.Visibility = Mathf.Clamp01(PlayerPrefs.GetFloat("Visibility", o.Visibility)); o.ParticleEffect = Mathf.Clamp(PlayerPrefs.GetInt("Particle Effect", o.ParticleEffect), 0, 3);
                o.Toon = B("Toon shading", o.Toon); o.Glow = B("Glow", o.Glow); o.Aniso = B("AnisotropicFiltering", o.Aniso); o.Fade = B("Fade", o.Fade); o.SoftVegetation = B("Soft Vegetation", o.SoftVegetation);
                o.Shadow = (ShadowDetail)Mathf.Clamp(PlayerPrefs.GetInt("Character Shadow", (int)o.Shadow), 0, 1); o.Texture = (TextureQuality)Mathf.Clamp(PlayerPrefs.GetInt("Texture Quality", (int)o.Texture), 0, 2);
                o.RenderScale = Mathf.Clamp(PlayerPrefs.GetFloat("of_x_scale", o.RenderScale), 0.5f, 1f); o.AA = Mathf.Clamp(PlayerPrefs.GetInt("of_x_aa", o.AA), 0, 3);
                o.GlowStrength = Mathf.Clamp01(PlayerPrefs.GetFloat("of_x_glow", o.GlowStrength)); o.Saturation = Mathf.Clamp01(PlayerPrefs.GetFloat("of_x_sat", o.Saturation));
            }
            o.Fps = Mathf.Clamp(PlayerPrefs.GetInt("of_set_fps", Application.isMobilePlatform ? 0 : 1), 0, 2);
            o.MasterOn = B("Master Volume Flag", true); o.Master = PlayerPrefs.GetFloat("Master Volume", 0.5f); o.SfxOn = B("SFX Volume Flag", true); o.Sfx = PlayerPrefs.GetFloat("SFX Volume", 0.5f);
            o.VoiceOn = B("Voice Volume Flag", true); o.Voice = PlayerPrefs.GetFloat("Voice Volume", 0.5f); o.AmbientOn = B("Ambient Volume Flag", true); o.Ambient = PlayerPrefs.GetFloat("Ambient Volume", 0.5f);
            o.MusicOn = B("Music Volume Flag", true); o.Music = PlayerPrefs.GetFloat("Music Volume", 0.5f);
            o.MyName = B("Floating My Name", o.MyName); o.OtherName = B("Floating User Name", o.OtherName); o.GroupName = B("Floating Group Name", o.GroupName); o.NpcName = B("Floating Npc Name", o.NpcName);
            o.MonsterName = B("Floating Monster Name", o.MonsterName); o.FloatNumber = B("Floating Number", o.FloatNumber); o.BalloonChat = B("Baloon Chat", o.BalloonChat);
            o.CurrentObjective = B("Current Objective", true); o.GuideEmail = B("Guide Email", true); o.WayPoint = B("Game WayPoint", true); o.GameHint = B("Game Hint", o.GameHint);
            o.EventMsg1 = B("Event msg1", o.EventMsg1); o.EventMsg2 = B("Event msg2", o.EventMsg2); o.NanocomClock = B("Nanocom Clock", o.NanocomClock); o.AnimatedNanos = B("Animated Nanos", o.AnimatedNanos);
            o.ChatTimeStamp = B("Chatbox Timestamp", o.ChatTimeStamp);
            o.UiFactor = Mathf.Clamp01(PlayerPrefs.GetFloat("UI Scale Factor", 1f)); o.ChatGeneral = Mathf.Clamp(PlayerPrefs.GetInt("General Text Color", 1), 0, 23); o.ChatBuddy = Mathf.Clamp(PlayerPrefs.GetInt("Buddy Text Color", 2), 0, 23); o.ChatGroup = Mathf.Clamp(PlayerPrefs.GetInt("Group Text Color", 3), 0, 23);
            o.AllowGroup = B("Allow Group", true); o.AllowBuddy = B("Allow Buddy", true); o.AllowTrade = B("Allow Trade", true); o.VariableJump = B("Variable Jump", true);
            ControlMap.Load();
            Cur = o; Loaded = true;
        }
        public static void Save()
        {
            var o = Cur;
            PlayerPrefs.SetInt("Detail Level", (int)o.Detail); PlayerPrefs.SetFloat("Visibility", o.Visibility); PlayerPrefs.SetInt("Particle Effect", o.ParticleEffect);
            SB("Toon shading", o.Toon); SB("Glow", o.Glow); SB("AnisotropicFiltering", o.Aniso); SB("Fade", o.Fade); SB("Soft Vegetation", o.SoftVegetation);
            PlayerPrefs.SetInt("Character Shadow", (int)o.Shadow); PlayerPrefs.SetInt("Texture Quality", (int)o.Texture);
            PlayerPrefs.SetFloat("of_x_scale", o.RenderScale); PlayerPrefs.SetInt("of_x_aa", o.AA); PlayerPrefs.SetFloat("of_x_glow", o.GlowStrength); PlayerPrefs.SetFloat("of_x_sat", o.Saturation); PlayerPrefs.SetInt("of_set_fps", o.Fps);
            SB("Master Volume Flag", o.MasterOn); PlayerPrefs.SetFloat("Master Volume", o.Master); SB("SFX Volume Flag", o.SfxOn); PlayerPrefs.SetFloat("SFX Volume", o.Sfx);
            SB("Voice Volume Flag", o.VoiceOn); PlayerPrefs.SetFloat("Voice Volume", o.Voice); SB("Ambient Volume Flag", o.AmbientOn); PlayerPrefs.SetFloat("Ambient Volume", o.Ambient);
            SB("Music Volume Flag", o.MusicOn); PlayerPrefs.SetFloat("Music Volume", o.Music);
            SB("Floating My Name", o.MyName); SB("Floating User Name", o.OtherName); SB("Floating Group Name", o.GroupName); SB("Floating Npc Name", o.NpcName); SB("Floating Monster Name", o.MonsterName);
            SB("Floating Number", o.FloatNumber); SB("Baloon Chat", o.BalloonChat); SB("Current Objective", o.CurrentObjective); SB("Guide Email", o.GuideEmail); SB("Game WayPoint", o.WayPoint); SB("Game Hint", o.GameHint);
            SB("Event msg1", o.EventMsg1); SB("Event msg2", o.EventMsg2); SB("Nanocom Clock", o.NanocomClock); SB("Animated Nanos", o.AnimatedNanos); SB("Chatbox Timestamp", o.ChatTimeStamp);
            PlayerPrefs.SetFloat("UI Scale Factor", o.UiFactor); PlayerPrefs.SetInt("General Text Color", o.ChatGeneral); PlayerPrefs.SetInt("Buddy Text Color", o.ChatBuddy); PlayerPrefs.SetInt("Group Text Color", o.ChatGroup);
            SB("Allow Group", o.AllowGroup); SB("Allow Buddy", o.AllowBuddy); SB("Allow Trade", o.AllowTrade); SB("Variable Jump", o.VariableJump);
            ControlMap.Save();
            PlayerPrefs.Save();
        }
        public static void Push() { backup = Cur.Clone(); ControlMap.Push(); }
        public static void Pop() { if (backup != null) Cur = backup.Clone(); ControlMap.Pop(); }
        public static void Apply() { GraphicsFx.ApplyAll(Cur); if (Applied != null) Applied(); }
        public static void EnsureLoaded() { if (!Loaded) Load(); }
    }
}
