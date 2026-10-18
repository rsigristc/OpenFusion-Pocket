using UnityEngine;
using OpenFusion.World;

namespace OpenFusion
{
    public sealed partial class GameClient
    {
        public void InvalidateAvatarAnimation() { pAnim = null; pClip = null; pAnimTry = 0; }
        bool lifeDead; Animation lifeAnimation; float lifeDieEnd; bool lifeRest;
        // Kept in the local locomotor so Walk/idle/attack never overwrite the death clips.
        bool TickLifeAnimation(Transform player)
        {
            var hud = Bootstrap != null ? Bootstrap.GetComponent<PlayerHud>() : GetComponent<PlayerHud>();
            if (hud == null) return false;
            if (hud.Data.Hp > 0) {
                if (lifeDead) {
                    lifeDead = false; lifeAnimation = null; airborne = false; vy = 0; dashT = 0; atkUntil = 0;
                    if (pAnim != null) pAnim.Stop(); pClip = null; SetAnim(player, "stand1");
                    Report("[life] Alive after server HP update: " + hud.Data.Hp);
                }
                return false;
            }
            if (pAnim == null) SetAnim(player, "stand1");
            if (!lifeDead || lifeAnimation != pAnim) {
                lifeDead = true; lifeAnimation = pAnim; lifeRest = false; EndEmote();
                airborne = false; vy = 0; dashT = 0; atkUntil = 0; CombatUntil = 0;
                if (pAnim != null) {
                    pAnim.Stop(); var die = pAnim["die"];
                    if (die != null) { die.speed = 1; die.time = 0; die.layer = 0; die.wrapMode = WrapMode.ClampForever; pAnim.Play("die"); }
                    lifeDieEnd = Time.time + (die != null ? Mathf.Max(.1f, die.length) : 0);
                }
                Report("[life] Defeated; waiting for server-approved respawn.");
            }
            if (!lifeRest && pAnim != null && Time.time >= lifeDieEnd) {
                lifeRest = true; var rest = pAnim["death"];
                if (rest != null) { rest.speed = 1; rest.time = 0; rest.layer = 0; rest.wrapMode = WrapMode.Loop; pAnim.CrossFade("death", .15f); }
            }
            return true;
        }
    }
}
