using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_bench : walk_to_settler_interactable
{
    //##############//
    // INTERACTABLE //
    //##############//

    public override string task_summary() => "Researching at " + GetComponentInParent<item>().display_name;

    float work_done;
    settler_animations.simple_work work_anim;

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        work_done = 0;
        work_anim = new settler_animations.simple_work(s,
            period: 1f / current_proficiency.total_multiplier);
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Play the work animation
        work_anim.play();

        // Only perform research on authority client
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        work_done += Time.deltaTime * current_proficiency.total_multiplier;

        // Work until 10 work done
        if (work_done < 10)
            return STAGE_RESULT.STAGE_UNDERWAY;

        return STAGE_RESULT.TASK_COMPLETE;
    }
}
