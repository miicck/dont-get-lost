using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A memeber of a settlment. </summary>
public class settler : pathfinding_agent, IInspectable, ILeftPlayerMenu
{
    const float MAX_DISTANCE_FROM_BED = 10;

    public bed bed { get => GetComponentInParent<bed>(); }
    settler_trade[] trades { get => GetComponentsInChildren<settler_trade>(); }
    item_requirement[] requirements { get => GetComponents<item_requirement>(); }

    /// <summary> The ui element containing my trades. </summary>
    settler_trade_window trade_window
    {
        get
        {
            if (_trade_window == null)
            {
                _trade_window = Resources.Load<settler_trade_window>("ui/settler_trade_window").inst();
                _trade_window.setup(name.capitalize(), trades);
            }
            return _trade_window;
        }
    }
    settler_trade_window _trade_window;

    /// <summary> Check if a set of fixtures is enough 
    /// for this settler to move in. </summary>
    public bool requirements_satisfied(IEnumerable<fixture> fixtures)
    {
        foreach (var r in requirements)
        {
            bool satisfied = false;
            foreach (var f in fixtures)
                if (r.satisfied(f))
                {
                    satisfied = true;
                    break;
                }

            if (!satisfied)
                return false;
        }

        return true;
    }

    protected override bool path_constriant(Vector3 v)
    {
        return (v - bed.transform.position).magnitude < MAX_DISTANCE_FROM_BED;
    }

    int next_target = 0;
    void idle()
    {
        Vector3 targ = transform.position;
        if (next_target < bed.fixture_count)
        {
            targ = bed[next_target].settler_stands_here.position;
            ++next_target;
        }
        else if (next_target == bed.fixture_count)
        {
            targ = bed.transform.position;
            ++next_target;
        }
        else
        {
            targ = random_target(5f);
            next_target = 0;
        }


        go_to(targ, on_fail: idle, on_arrive: idle);
    }

    public override void on_gain_authority()
    {
        base.on_gain_authority();

        idle();
    }

    protected override void Update()
    {
        // Pathfinding agent updates
        base.Update();

        if (has_authority)
        {
            // Accumulate stock
            foreach (var t in trades)
                t.run_stock_updates();
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bed.transform.position, MAX_DISTANCE_FROM_BED);
    }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_string_counts_v2 shop_stock;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();

        shop_stock = new networked_variables.net_string_counts_v2();

        // Record initial stock
        foreach (var t in trades)
            shop_stock[t.slot.item.name] = t.slot.stock;

        shop_stock.on_change = () =>
        {
            // Update the stock in the shop from the network
            foreach (var t in trades)
            {
                var slot = t.slot;
                bool found = false;
                foreach (var kv in shop_stock)
                    if (slot.item.name == kv.Key)
                    {
                        slot.stock = kv.Value;
                        found = true;
                    }

                // Key not present => no stock
                if (!found)
                    slot.stock = 0;
            }
        };

        // Keep the network up-to-date with the stock
        foreach (var t in trades)
            t.slot.add_on_change_listener(() =>
            shop_stock[t.slot.item.name] = t.slot.stock);
    }

    public override void on_create()
    {
        // Load the stock from the networked value
        foreach (var t in trades)
            t.slot.stock = shop_stock[t.slot.item.name];
    }

    public override void on_forget(bool deleted)
    {
        // Close the trade window
        if (this == (Object)player.current.left_menu)
            player.current.left_menu = null;
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    public RectTransform left_menu_transform() { return trade_window.GetComponent<RectTransform>(); }
    public void on_left_menu_open() { }
    public void on_left_menu_close() { }

    //##############//
    // IInspectable //
    //##############//

    public string inspect_info() { return name.capitalize(); }
    public Sprite main_sprite() { return null; }
    public Sprite secondary_sprite() { return null; }
}