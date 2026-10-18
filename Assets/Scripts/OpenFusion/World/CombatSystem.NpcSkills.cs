using System;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class CombatSystem
    {
        void NpcSkillPresentation(byte[] b, bool ready, bool corruption = false)
        {
            if (ents == null || b == null || b.Length < 6) return;
            var npc = ents.NpcById(I(b, 0)); if (npc == null) return;
            int skill = BitConverter.ToInt16(b, 4); var animator = npc.GetComponent<NpcAnimator>();
            if (ready) { animator?.PlayAction("skillready"); return; }
            var info = npc.GetComponent<NpcInfo>();
            var rows = tables?.m_pNpcTable?.m_pNpcData;
            OpenFusion.Tables.Gen.NpcTableElement row = null;
            if (info != null && rows != null) foreach (var r in rows) if (r != null && r.m_iNpcNumber == info.NpcNumber) { row = r; break; }
            int animationId = row == null ? 0 : skill == row.m_iActiveSkill1 ? row.m_iActiveSkill1Ani : skill == row.m_iActiveSkill2 ? row.m_iActiveSkill2Ani : skill == row.m_iSupportSkill ? row.m_iSupportSkillAni : 0;
            var animations = tables?.m_pAnimationTable?.m_pNpcData;
            if (animationId > 0 && animations != null) foreach (var a in animations) if (a != null && a.m_iAniState == animationId) { animator?.PlayAction(a.m_szSequenceName); break; }
            var skills = tables?.m_pSkillTable?.m_pSkillData;
            OpenFusion.Tables.Gen.SkillElement se = null;
            if (skills != null) foreach (var s in skills) if (s != null && s.m_iSkillNumber == skill) { se = s; break; }
            var fx = BulletFx.Instance; if (se == null || fx == null) return;
            if (se.m_iEffect > 0) fx.Spawn(se.m_iEffect, npc.transform.position + Vector3.up, npc.transform.rotation, 1, null, 3);
            // Skill type determines the result layout, independently of its visual effect ID.
            int first = corruption ? 24 : 28;
            if (b.Length < first) return;
            int count = I(b, first - 4), stride = corruption ? 40 : SkillResults.Stride(I(b, 20));
            if (!corruption && I(b, 20) == 30) first += 16;
            if (stride == 0 || count < 0 || count > (b.Length - first) / stride) return;
            for (int i = 0; i < count; i++) {
                int o = first + i * stride, ct = I(b, o), id = I(b, o + 4);
                var target = ct == 1 ? ents.TransformOfPlayer(id) : ents.NpcById(id)?.transform;
                if (target != null && se.m_iTargetEffect > 0) fx.Fire(se.m_iTargetEffect, npc.transform, target, 1.6f, target.position + Vector3.up);
            }
        }
    }
}
