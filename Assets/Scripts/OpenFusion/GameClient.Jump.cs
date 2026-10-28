using UnityEngine;
using OpenFusion.World;

namespace OpenFusion
{
    public sealed partial class GameClient
    {
        // Retrobution Legacy: cnAvatarThirdPersonMove.ExecuteJump/UpdateAirControl/
        // ApplyMovementPhysics; GameCondition slot 3 has a 0.5-second cooldown.
        float nextManualJumpAt, jumpExtraGravityUntil;
        bool jumpFirstFrame;

        void StartManualJump(float now)
        {
            float speed = Vehicle > 0 && buffBase ? baseJump : JumpVelocity;
            if (GameOptions.Cur.VariableJump && Vehicle == 0 && now >= jumpExtraGravityUntil) speed *= 1.45f;
            airborne = true; vy = speed; jumpT = now;
            nextManualJumpAt = now + .5f; nextJumpPkt = now + .3f;
            jumpFirstFrame = true; PlayOnce("jumpstart");
        }

        // Both collision movement paths use this step. Holding Jump is read through
        // ControlMap, so gamepad, keyboard and independent touch fingers share it.
        void StepJumpVelocity(float delta, bool held, float now)
        {
            bool variable = GameOptions.Cur.VariableJump && Vehicle == 0;
            bool impulse = now < jumpExtraGravityUntil;
            float gravity = Gravity;
            // Legacy doesn't run UpdateAirControl on the takeoff frame itself.
            if (!jumpFirstFrame && variable && !impulse)
            {
                gravity = held && now >= nextManualJumpAt ? Gravity : Gravity * 2f;
                if (!held && vy > 3f) vy *= .25f;
            }
            jumpFirstFrame = false;
            float acceleration = variable && !impulse && Mathf.Abs(vy) < .75f ? gravity * .3f : gravity;
            vy = Mathf.Max(-gravity, vy - acceleration * delta);
        }

        void ResetJumpControl()
        {
            nextManualJumpAt = jumpExtraGravityUntil = 0;
            jumpFirstFrame = false;
        }
    }
}
