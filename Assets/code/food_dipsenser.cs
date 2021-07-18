using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class food_dipsenser : walk_to_settler_interactable
{
    public item_dispenser item_dispenser;

    protected override void Start()
    {
        base.Start();
        item_dispenser.accept_item = (i) => i.GetComponent<food>() != null;
    }

    //##############//
    // INTERACTABLE //
    //##############//

    const byte GUARANTEED_FULL = 220;
    const byte GUARANTEED_EAT = 64;

    settler_animations.simple_work work_anim;
    float time_dispensing;
    float time_started;

    public bool food_available => item_dispenser.has_items_to_dispense;

    protected override bool ready_to_assign(settler s)
    {
        int ms = s.nutrition.metabolic_satisfaction;

        // Not hungry
        if (ms > GUARANTEED_FULL) return false;
        if (ms > GUARANTEED_EAT)
        {
            float probability = ms - GUARANTEED_EAT;
            probability /= (GUARANTEED_FULL - GUARANTEED_EAT);
            probability = 1 - probability;
            if (probability < Random.Range(0, 1f)) return false;
        }

        // Don't eat if one of my friends is starving
        if (!s.starving && group_info.has_starvation(s.group))
            return false;

        // Check if food is available 
        return food_available;
    }

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        time_dispensing = 0f;
        time_started = Time.time;
        work_anim = new settler_animations.simple_work(s);
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        work_anim.play();

        // Run dispenser timer
        time_dispensing += Time.deltaTime;
        if (time_dispensing < 1f) return STAGE_RESULT.STAGE_UNDERWAY;
        time_dispensing = 0f;

        // Search for food
        var food = item_dispenser.dispense_first_item();
        if (food == null) return STAGE_RESULT.TASK_FAILED;

        // Eat food on authority client, delete food on all clients
        if (s.has_authority) s.consume_food(food.food_values);
        Destroy(food.gameObject);

        // Complete if we've eaten enough
        if (s.nutrition.metabolic_satisfaction > GUARANTEED_FULL ||
            Time.time - time_started > 5f)
            return STAGE_RESULT.TASK_COMPLETE;
        else
            return STAGE_RESULT.STAGE_UNDERWAY;
    }

    public override string task_summary()
    {
        return "Getting food";
    }
}
