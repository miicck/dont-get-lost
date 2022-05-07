using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bed : character_walk_to_interactable, IAddsToInspectionText
{
    public const float TIREDNESS_RECOVERY_RATE = 100f / 60f;
    public Transform sleep_orientation;
    public List<Transform> covered_test_points = new List<Transform>();

    float delta_tired;

    float covered_amt
    {
        get
        {
            float covered = 0f;
            foreach (var t in covered_test_points)
                if (weather.spot_is_covered(t.position))
                    covered += 1f;
            return covered / covered_test_points.Count;
        }
    }

    public override string added_inspection_text() =>
        base.added_inspection_text() + "\n" + ((int)(100 * covered_amt)) + "% covered";

    protected override bool ready_to_assign(character c) => (c is settler) && (c as settler).needs_sleep;

    protected override void on_arrive(character c)
    {
        // Reset stuff
        delta_tired = 0f;

        // Lie down
        c.transform.position = sleep_orientation.position;
        c.transform.rotation = sleep_orientation.rotation;

        // Close eyes
        c.GetComponentInChildren<facial_expression>().eyes_closed = true;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        // Only modify tiredness on authority client
        if (!c.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Beomce un-tired
        delta_tired -= TIREDNESS_RECOVERY_RATE * Time.deltaTime;


        if (c is settler)
        {
            var s = (settler)c;

            if (delta_tired < -1f)
            {
                delta_tired = 0f;
                s.tiredness.value -= 1;
            }

            int target_tiredness = s.starving ? 60 : 5;
            return s.tiredness.value < target_tiredness ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_UNDERWAY;
        }

        return STAGE_RESULT.TASK_COMPLETE;
    }

    protected override void on_unassign(character c)
    {
        // Un-lie down
        c.transform.rotation = Quaternion.identity;

        // Un-close (open?) eyes
        c.GetComponentInChildren<facial_expression>().eyes_closed = false;

        if (c is settler)
        {
            // Add mood effects
            var s = (settler)c;
            s.add_mood_effect("just_got_up");
            float covered_amt = this.covered_amt;
            if (covered_amt < 0.25f) s.add_mood_effect("uncovered_bed");
            else if (covered_amt < 0.99f) s.add_mood_effect("partially_covered_bed");
        }
    }

    public override string task_summary()
    {
        if (!arrived) return "Walking to bed";
        return "Sleeping";
    }
}
