using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class crane_node : item_node
{
    public crane crane;
    public item_output dropoff_node;
    public float dropoff_period = 1f;

    //###############//
    // Crane control //
    //###############//

    crane_pickup_node picking_up_from;

    protected override void on_inputs_change()
    {
        base.on_inputs_change();
        picking_up_from = (crane_pickup_node)next_input();
    }

    enum STATE
    {
        AWAITING_PICKUP,
        PICKUP,
        GOTO_DROPOFF,
        DROP_OFF,
        RETURN_BOX,
    }

    STATE state
    {
        get => _state;
        set
        {
            _state = value;
            switch (_state)
            {
                case STATE.PICKUP:
                    crane.arrive_func on_arrive = () =>
                    {
                        // Pick up the box
                        picking_up_from.box.transform.SetParent(crane.hook);

                        // Centre on the hook
                        Vector3 delta = crane.hook.position - picking_up_from.box.position;
                        delta.y = 0;
                        picking_up_from.box.position += delta;

                        state = STATE.GOTO_DROPOFF;
                    };

                    // Check if hook is already in correct
                    // place from last time around.
                    if (crane.hook.distance_to(picking_up_from) < 0.25f)
                        on_arrive();
                    else
                    {
                        crane.target = picking_up_from.transform.position;
                        crane.on_arrive = on_arrive;
                    }
                    return;

                case STATE.GOTO_DROPOFF:

                    // Set target so that output position is slighly above dropoff_node
                    crane.target = dropoff_node.transform.position +
                        (crane.hook.position - picking_up_from.box_output.position) +
                        Vector3.up * 0.5f;

                    crane.on_arrive = () =>
                    {
                        state = STATE.DROP_OFF;
                    };
                    return;

                case STATE.RETURN_BOX:
                    crane.target = picking_up_from.transform.position;
                    crane.on_arrive = () =>
                    {
                        return_box();
                        state = STATE.AWAITING_PICKUP;
                    };
                    return;
            }
        }
    }
    STATE _state = STATE.AWAITING_PICKUP;

    void return_box()
    {
        if (picking_up_from == null) return;
        picking_up_from.box.SetParent(picking_up_from.box_reset_parent);
        picking_up_from.box.localPosition = picking_up_from.box_reset_local_pos;
        picking_up_from.box.localRotation = picking_up_from.box_reset_local_rot;
    }

    //#################//
    // Unity callbacks //
    //#################//

    float last_dropoff;

    private void Update()
    {
        if (picking_up_from == null)
        {
            // Not connected - reset state
            state = STATE.AWAITING_PICKUP;
            return; 
        }

        switch (state)
        {
            case STATE.DROP_OFF:

                if (picking_up_from.item_count == 0)
                {
                    // All items dropped off
                    state = STATE.RETURN_BOX;
                    break;
                }

                if (Time.realtimeSinceStartup - last_dropoff > dropoff_period)
                {
                    // Drop off the next item
                    last_dropoff = Time.realtimeSinceStartup;
                    var i = picking_up_from.release_item(0);
                    i.transform.position = picking_up_from.box_output.position;
                    i.gameObject.SetActive(true);
                    item_dropper.create(i, i.transform.position, dropoff_node);
                }

                break;

            case STATE.AWAITING_PICKUP:

                // Trigger crane to pick up box
                if (picking_up_from.ready_for_pickup)
                    state = STATE.PICKUP;
                break;
        }
    }

    protected override void OnDestroy()
    {
        return_box();
        base.OnDestroy();
    }

    //###########//
    // item_node //
    //###########//

    protected override bool can_input_from(item_node other)
    {
        // No input from/to destroyed things
        if (this == null || other == null) return false;

        if (other is crane_pickup_node)
        {
            Vector3 delta = transform.position - other.transform.position;

            // Can't pickup things above the crane
            if (delta.y < 0) return false;
            delta.y = 0;

            // Ensure pickup node is (close enough to) directly below
            return delta.magnitude <= LINK_DISTANCE_TOLERANCE;
        }

        // No input from nodes which aren't crane_pickup_nodes
        return false;
    }

    protected override bool can_output_to(item_node other)
    {
        return false;
    }
}
