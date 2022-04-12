using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class market_stall : character_walk_to_interactable, IAddsToInspectionText
{
    public town_path_element shopkeeper_path_element;

    chest storage;
    item_input[] inputs;
    item_output output;

    protected override bool ready_to_assign(character c)
    {
        if (storage.inventory.total_item_count() == 0)
            return false; // Must have stock
        return true;
    }

    public override string added_inspection_text()
    {
        string ret = base.added_inspection_text();
        int items = storage.inventory.total_item_count();
        if (items == 0) ret += "\nNo stock!";
        else ret += "\n" + items + " items in stock.";
        return ret;
    }

    protected override void on_fail_assign(character c, ASSIGN_FAILURE_MODE failure)
    {
        Debug.Log("Failed to assign to market stall: " + failure);
    }

    public override town_path_element path_element(int group = -1)
    {
        if (shopkeeper_path_element.group == group)
            return shopkeeper_path_element;
        return null;
    }

    protected override void Start()
    {
        base.Start();

        storage = GetComponent<chest>();
        inputs = GetComponentsInChildren<item_input>();
        output = GetComponentInChildren<item_output>();

        foreach (var i in inputs)
        {
            var input = i;
            input.add_on_change_listener(() =>
            {
                var item = input.release_next_item();
                while (item != null)
                {
                    if (storage.inventory.add(item, 1))
                        Destroy(item.gameObject);
                    else
                        item_rejector.create(item);

                    item = input.release_next_item();
                }
            });
        }
    }

    enum STATE
    {
        OBTAIN_ITEM_TO_SELL,
        AWAIT_BUYER,
        SELL_ITEM,
        OUTPUT_COIN,
        STATE_COUNT
    }

    item item_selling;
    float timer = 0;
    int coins_to_dispense = 0;
    STATE state = STATE.OBTAIN_ITEM_TO_SELL;

    STATE parse_state(int stage, out bool success)
    {
        int state_number = stage % (int)STATE.STATE_COUNT;

        if (!System.Enum.IsDefined(typeof(STATE), state_number))
        {
            Debug.LogError("Unkown state number: " + state_number);
            success = false;
            return STATE.STATE_COUNT;
        }

        success = true;
        return (STATE)state_number;
    }

    int cycle(int stage) => stage / (int)STATE.STATE_COUNT;

    public override string task_summary()
    {
        switch (state)
        {
            case STATE.OBTAIN_ITEM_TO_SELL:
                return "Running a market stall (getting stock)";

            case STATE.AWAIT_BUYER:
                return "Running a market stall (waiting for a buyer)";

            case STATE.SELL_ITEM:
                return "Running a market stall (haggling for a price)";

            case STATE.OUTPUT_COIN:
                return "Running a market stall (collecting coins)";

            default:
                return "Running a market stall";
        }
    }

    settler_animations.simple_work work_anim;

    protected override void on_stage_change(int old_stage, int new_stage) => timer = 0;
    protected override void on_arrive(character c) =>  work_anim = null;

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        const float BASE_STOCK_TIME = 1f;
        const float BASE_SELL_TIME = 4f;
        const float BASE_COIN_TIME = 0.5f;

        if (work_anim == null)
        {
            c.transform.forward = shopkeeper_path_element.transform.forward;
            if (c is settler)
                work_anim = new settler_animations.simple_work(c as settler, 1f / current_proficiency.total_multiplier);
        }

        if (state != STATE.AWAIT_BUYER)
            work_anim?.play();

        if (cycle(stage) > 4)
            return STAGE_RESULT.TASK_COMPLETE; // Completed enough stages

        state = parse_state(stage, out bool success);
        if (!success)
            return STAGE_RESULT.TASK_FAILED;

        switch (state)
        {
            case STATE.OBTAIN_ITEM_TO_SELL:

                timer += Time.deltaTime * current_proficiency.total_multiplier;
                if (timer < BASE_STOCK_TIME)
                    return STAGE_RESULT.STAGE_UNDERWAY;

                item_selling = storage.inventory.remove_first();
                if (item_selling == null)
                    return STAGE_RESULT.TASK_COMPLETE; // Sold out

                return STAGE_RESULT.STAGE_COMPLETE;

            case STATE.AWAIT_BUYER:

                // Buyer found
                return STAGE_RESULT.STAGE_COMPLETE;

            case STATE.SELL_ITEM:

                // I forgot what I was selling
                if (item_selling == null)
                    return STAGE_RESULT.TASK_FAILED;

                timer += Time.deltaTime * current_proficiency.total_multiplier;
                if (timer > BASE_SELL_TIME)
                {
                    // Finished selling
                    coins_to_dispense = item_selling.value;
                    return STAGE_RESULT.STAGE_COMPLETE;
                }
                else
                    return STAGE_RESULT.STAGE_UNDERWAY;

            case STATE.OUTPUT_COIN:

                if (coins_to_dispense <= 0)
                {
                    return STAGE_RESULT.STAGE_COMPLETE;
                }

                timer += Time.deltaTime * current_proficiency.total_multiplier;
                if (timer > BASE_COIN_TIME)
                {
                    // Dispense the next coin
                    coins_to_dispense -= 1;
                    timer = 0;
                    var coin = Resources.Load<item>("items/coin");
                    output.add(coin, 1);
                    production_tracker.register_product(coin);
                }

                return STAGE_RESULT.STAGE_UNDERWAY;


            default:
                Debug.LogError("Unkown state: " + stage);
                return STAGE_RESULT.STAGE_COMPLETE;
        }
    }
}
