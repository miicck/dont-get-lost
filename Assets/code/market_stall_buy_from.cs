using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class market_stall_buy_from : character_walk_to_interactable
{
    public town_path_element customer_path_element;

    public character customer
    {
        get
        {
            if (_customer != character)
                _customer = null; // Old customer
            return _customer;
        }
        set => _customer = value;
    }
    character _customer;

    public override string task_summary() => "Trading with market stall";

    public override town_path_element path_element(int group = -1)
    {
        if (customer_path_element.group == group)
            return customer_path_element;
        return null;
    }

    float timer = 0;

    protected override bool compatible_character(character c) => c is visiting_character;

    protected override void on_arrive(character c)
    {
        timer = 0;
        customer = c;
    }

    protected override void on_unassign(character c)
    {
        customer = null;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        if (!c.has_authority)
            return STAGE_RESULT.STAGE_UNDERWAY;

        timer += Time.deltaTime;
        return timer > 10f ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_UNDERWAY;
    }
}
