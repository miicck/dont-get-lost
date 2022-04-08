using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class market_stall_poi : town_element_point_of_interest
{
    public visiting_character buyer { get; private set; }
    public float buyer_time_at_stall { get; private set; }

    public override string description() => string.Format("Trading at a market stall for {0:0.0} seconds", buyer_time_at_stall);

    public override INTERACTION_RESULT on_arrive(visiting_character visitor)
    {
        visitor.transform.forward = target_element.transform.forward;
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override INTERACTION_RESULT interact(visiting_character visitor)
    {
        if (buyer == null)
        {
            buyer = visitor;
        }
        else if (buyer != visitor)
            return INTERACTION_RESULT.COMPLETE;

        buyer_time_at_stall += Time.deltaTime;
        if (buyer_time_at_stall > 30f)
        {
            buyer_time_at_stall = 0;
            return INTERACTION_RESULT.COMPLETE;
        }

        return INTERACTION_RESULT.UNDERWAY;
    }

    public override void on_leave(visiting_character visitor)
    {
        buyer = null;
    }
}
