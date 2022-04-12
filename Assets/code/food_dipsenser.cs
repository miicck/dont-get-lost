using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class food_dipsenser : character_walk_to_interactable, IAddsToInspectionText
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

    protected override bool ready_to_assign(character c)
    {
        if (room_info.is_dining_room(path_element().room)) return false;
        if (c is settler && !(c as settler).ready_to_eat()) return false;
        if (!food_available) return false;

        return true;
    }

    protected override void on_arrive(character c)
    {
        // Reset stuff
        time_dispensing = 0f;
        time_started = Time.time;
        if (c is settler)
            work_anim = new settler_animations.simple_work(c as settler);
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        work_anim?.play();

        // Run dispenser timer
        time_dispensing += Time.deltaTime;
        if (time_dispensing < 1f) return STAGE_RESULT.STAGE_UNDERWAY;
        time_dispensing = 0f;

        // Search for food
        var food = item_dispenser.dispense_first_item();
        if (food == null) return STAGE_RESULT.TASK_FAILED;

        // delete food on all clients
        var food_values = food.food_values; // Remember for later
        Destroy(food.gameObject);

        if (c is settler)
        {
            var s = (settler)c;
            s.consume_food(food_values);

            // Complete if we've eaten enough
            if (s.nutrition.metabolic_satisfaction > GUARANTEED_FULL ||
                Time.time - time_started > 5f)
            {
                s.add_mood_effect("ate_without_table");
                return STAGE_RESULT.TASK_COMPLETE;
            }
            else
                return STAGE_RESULT.STAGE_UNDERWAY;
        }

        return STAGE_RESULT.TASK_COMPLETE;
    }

    public override string task_summary()
    {
        return "Getting food";
    }
}
