using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ladder_path_element : town_path_element
{
    public override settler_animations.animation settler_animation(settler s)
    {
        // If the ladder is sufficiently flat, no need animate
        if (Vector3.Angle(transform.up, Vector3.up) > 45) return null;
        return new climb_ladder(s);
    }

    public override void on_character_move_towards(character c)
    {
        // If sufficienctly flat, no need to face the ladder
        if (Vector3.Angle(transform.up, Vector3.up) > 45) return;

        // Face towards the ladder
        Vector3 fw = transform.forward; fw.y = 0; fw.Normalize();
        c.transform.forward = Vector3.Lerp(c.transform.forward, fw, Time.deltaTime * 10);

        base.on_character_move_towards(c);
    }

    public class climb_ladder : settler_animations.animation
    {
        public climb_ladder(settler s) : base(s) { }

        protected override void animate()
        {
            Vector3 fw = settler.transform.forward;

            var ls = Mathf.Sin(left_arm.following.progress * Mathf.PI);
            var rs = Mathf.Sin(right_arm.following.progress * Mathf.PI);

            Vector3 ld = (fw * 0.25f + Vector3.up * ls * 0.25f) * settler.height_scale.value;
            Vector3 rd = (fw * 0.25f + Vector3.up * rs * 0.25f) * settler.height_scale.value;

            left_hand_pos = left_arm.shoulder.position + ld;
            right_hand_pos = right_arm.shoulder.position + rd;
        }
    }
}