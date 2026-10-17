// ItemCooldown: port de GameCondition.IsItemCoolTime / IsItemCoolTimePercent / QuickItemCool (cooldown de objetos generales). SIN COMPILAR NI PROBAR EN UNITY.
// El cooldown es por m_iCoolType del skill enlazado (GeneralItem.m_iLinkSkill -> SkillElement): todos los objetos con el mismo tipo comparten temporizador. Duracion = m_iCoolTime * 0.1 s.
// TryUse(): true = sigue enfriando (no se debe usar); si no, arranca el temporizador y devuelve false (igual que el original, que arranca al INTENTAR usar).
// Draw(): relleno oscuro de abajo arriba sobre la casilla con el % restante (cnGUINanocom.BoxAlpha; aqui negro al 55 %).
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    public static class ItemCooldown
    {
        sealed class Entry { public float Max, Start; }
        static readonly Dictionary<int, Entry> byType = new Dictionary<int, Entry>();

        static SkillElement Skill(XdtTableScript t, int skillId)
        {
            var st = t != null ? t.m_pSkillTable : null; if (st == null || st.m_pSkillData == null || skillId <= 0 || skillId >= st.m_pSkillData.Length) return null; return st.m_pSkillData[skillId];
        }
        /// <summary>m_iLinkSkill del objeto general (0 si no hay).</summary>
        public static int LinkSkill(XdtTableScript t, int itemId)
        {
            var g = t != null ? t.m_pGeneralItemTable : null; if (g == null || g.m_pItemData == null || itemId <= 0 || itemId >= g.m_pItemData.Length || g.m_pItemData[itemId] == null) return 0; return g.m_pItemData[itemId].m_iLinkSkill;
        }

        public static bool TryUse(XdtTableScript t, int skillId)
        {
            var sk = Skill(t, skillId); if (sk == null || sk.m_iCoolTime <= 0) return false;
            Entry e; if (byType.TryGetValue(sk.m_iCoolType, out e))
            {
                if (Time.time - e.Start < e.Max) return true;
                e.Max = sk.m_iCoolTime * 0.1f; e.Start = Time.time; return false;
            }
            byType[sk.m_iCoolType] = new Entry { Max = sk.m_iCoolTime * 0.1f, Start = Time.time }; return false;
        }
        public static bool TryUseItem(XdtTableScript t, int itemId) { return TryUse(t, LinkSkill(t, itemId)); }

        /// <summary>Fraccion restante 0..1 (1 = recien usado).</summary>
        public static float Percent(XdtTableScript t, int skillId)
        {
            var sk = Skill(t, skillId); if (sk == null || sk.m_iCoolTime <= 0) return 0f;
            Entry e; if (!byType.TryGetValue(sk.m_iCoolType, out e) || e.Max <= 0f) return 0f;
            float el = Time.time - e.Start; return el < e.Max ? 1f - el / e.Max : 0f;
        }
        public static float PercentItem(XdtTableScript t, int itemId) { return Percent(t, LinkSkill(t, itemId)); }

        /// <summary>QuickItemCool: dibuja el velo sobre la casilla (solo objetos generales, tipo 7).</summary>
        public static void Draw(XdtTableScript t, Rect r, int itemType, int itemId)
        {
            if (itemType != 7 || Event.current.type != EventType.Repaint) return;
            float p = PercentItem(t, itemId); if (p <= 0f) return;
            var old = GUI.color; GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(r.x, r.y + r.height - r.height * p, r.width, r.height * p), Texture2D.whiteTexture); GUI.color = old;
        }
    }
}
