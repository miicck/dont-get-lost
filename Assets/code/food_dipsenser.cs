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

    settler_animations.simple_work work_anim;
    float time_dispensing;

    protected override bool ready_to_assign(settler s)
    {
        // Not hungry
        if (s.nutrition.metabolic_satisfaction > settler.MAX_METABOLIC_SATISFACTION_TO_EAT)
            return false;

        // No food
        return item_dispenser.has_items_to_dispense;
    }

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        time_dispensing = 0f;
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
        if (s.has_authority) s.nutrition.consume_food(food.food_values);
        Destroy(food.gameObject);

        // Complete if we've eaten enough
        if (s.nutrition.metabolic_satisfaction > settler.MAX_METABOLIC_SATISFACTION_TO_EAT)
            return STAGE_RESULT.TASK_COMPLETE;
        else
            return STAGE_RESULT.STAGE_UNDERWAY;
    }

    public override string task_summary()
    {
        return "Getting food";
    }
}
