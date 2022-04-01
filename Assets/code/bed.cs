using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IDoesntCoverBeds { }

public class bed : walk_to_settler_interactable, IAddsToInspectionText
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
                foreach (var h in Physics.RaycastAll(t.position, Vector3.up))
                {
                    if (h.collider.gameObject.GetComponentInParent<IDoesntCoverBeds>() != null) continue;
                    covered += 1f;
                    break;
                }
            return covered / covered_test_points.Count;
        }
    }

    public override string added_inspection_text() =>
        base.added_inspection_text() + "\n" + ((int)(100 * covered_amt)) + "% covered";

    protected override bool ready_to_assign(settler s) => s.needs_sleep;

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        delta_tired = 0f;

        // Lie down
        s.transform.position = sleep_orientation.position;
        s.transform.rotation = sleep_orientation.rotation;

        // Close eyes
        s.GetComponentInChildren<facial_expression>().eyes_closed = true;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Only modify tiredness on authority client
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        // Beomce un-tired
        delta_tired -= TIREDNESS_RECOVERY_RATE * Time.deltaTime;
        if (delta_tired < -1f)
        {
            delta_tired = 0f;
            s.tiredness.value -= 1;
        }

        int target_tiredness = s.starving ? 60 : 5;
        return s.tiredness.value < target_tiredness ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_UNDERWAY;
    }

    protected override void on_unassign(settler s)
    {
        // Un-lie down
        s.transform.rotation = Quaternion.identity;

        // Un-close (open?) eyes
        s.GetComponentInChildren<facial_expression>().eyes_closed = false;

        // Add mood effects
        s.add_mood_effect("just_got_up");
        float covered_amt = this.covered_amt;
        if (covered_amt < 0.25f) s.add_mood_effect("uncovered_bed");
        else if (covered_amt < 0.99f) s.add_mood_effect("partially_covered_bed");
    }

    public override string task_summary()
    {
        if (!arrived) return "Walking to bed";
        return "Sleeping";
    }
}
