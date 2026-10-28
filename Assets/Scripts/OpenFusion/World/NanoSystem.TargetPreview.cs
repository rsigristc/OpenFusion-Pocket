using System.Collections.Generic;
using OpenFusion.Tables.Gen;
using UnityEngine;
namespace OpenFusion.World
{
    public sealed partial class NanoSystem
    {
        readonly List<NpcInfo> skillPreview = new List<NpcInfo>();
        Texture2D skillTargetTexture;
        // The same enemy list feeds both visual targeting and NANO_SKILL_USE.
        void CollectEnemySkillTargets(List<NpcInfo> result, SkillElement skill, Transform self, NpcInfo selected, Vector3 forward)
        {
            result.Clear(); if (skill == null || self == null || skill.m_iTargetType != 1 || skill.m_iEffectType != 1) return;
            float range = skill.m_iEffectRange * .01f, area = skill.m_iEffectArea * .01f;
            bool valid = selected != null && selected.IsMob && selected.Hp > 0;
            switch (skill.m_iEffectTarget)
            {
                case 1: if (valid && selected.GetBounds().SqrDistance(self.position) <= range * range) result.Add(selected); break;
                case 3:
                    if (!valid) break; Cone(result, forward, skill.m_iEffectAngle, range, self);
                    if (result.Remove(selected)) result.Insert(0, selected); else result.Clear(); break;
                case 5: Near(result, self.position, area); break;
                case 6:
                    if (!valid) break; Near(result, selected.transform.position, area); result.Remove(selected); result.Insert(0, selected); break;
            }
            int max = Mathf.Max(1, skill.m_iTargetNumber); if (result.Count > max) result.RemoveRange(max, result.Count - max);
        }
        void UpdateSkillPreview()
        {
            skillPreview.Clear();
            if (ents?.Self == null || hud == null || wheel == null || !hud.Data.HasActiveNano || hud.Data.Hp <= 0) return;
            int slot = hud.Data.ActiveNanoSlot;
            var skill = wheel.SkillOf(wheel.BankSkill(hud.Data.NanoSlots[slot]));
            if (skill == null || skill.m_iBatteryDrainType != 1 || hud.Data.NanoStamina[slot] <= 0) return;
            var self = ents.Self.transform; var forward = Camera.main != null ? Camera.main.transform.forward : self.forward;
            forward.y = 0; forward.Normalize();
            CollectEnemySkillTargets(skillPreview, skill, self, NpcUI.MobTarget, forward);
        }
        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || GameMenu.HudHidden || NpcUI.Active || ChatHud.Typing || OpenFusion.Screens.LoadingScreen.I?.Visible == true || hud == null || hud.Data.Hp <= 0 || !hud.Data.HasActiveNano) return;
            var camera = Camera.main; if (camera == null || skillPreview.Count == 0) return;
            if (skillTargetTexture == null) skillTargetTexture = OpenFusion.Screens.UiKit.Tex("target_n");
            if (skillTargetTexture == null) return;
            var color = GUI.color; int depth = GUI.depth; GUI.depth = 18;
            GUI.color = new Color(1, 1, 1, .5f + (Mathf.Sin(Time.time * 5) + 1) * .25f);
            float width = skillTargetTexture.width * GameOptions.UiScale, height = skillTargetTexture.height * GameOptions.UiScale;
            foreach (var target in skillPreview)
            {
                if (target == null || target.Hp <= 0) continue;
                var point = camera.WorldToScreenPoint(target.transform.position + Vector3.up * target.Height * .5f);
                if (point.z > 0) GUI.DrawTexture(new Rect(point.x - width * .5f, Screen.height - point.y - height * .5f, width, height), skillTargetTexture, ScaleMode.StretchToFill, true);
            }
            GUI.color = color; GUI.depth = depth;
        }
    }
}
