using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ladder_path_element : town_path_element
{
    public override settler_animations.animation settler_animation(settler s)
    {
        // If the ladder is sufficiently flat, no need animate
        if (Vector3.Angle(transform.up, Vector3.up) > 45) return null;
        return new climb_ladder(s, this);
    }

    public class climb_ladder : settler_animations.animation
    {
        ladder_path_element ladder;

        public climb_ladder(settler s, ladder_path_element ladder) : base(s)
        {
            this.ladder = ladder;
        }

        protected override void animate()
        {
            // Face the ladder
            Vector3 fw = ladder.transform.forward; fw.y = 0;
            settler.transform.forward = fw;

            var ls = Mathf.Sin(left_arm.following.progress * Mathf.PI);
            var rs = Mathf.Sin(right_arm.following.progress * Mathf.PI);

            Vector3 ld = (fw * 0.25f + Vector3.up * ls * 0.25f) * settler.height_scale.value;
            Vector3 rd = (fw * 0.25f + Vector3.up * rs * 0.25f) * settler.height_scale.value;

            left_hand_pos = left_arm.shoulder.position + ld;
            right_hand_pos = right_arm.shoulder.position + rd;
        }
    }
}