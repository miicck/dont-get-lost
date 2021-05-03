using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lootbox : walk_to_settler_interactable
{
    town_gate gate;
    character looting;
    town_path_element looting_path_element;
    town_path_element.path path;
    bool walking_back = false;

    public override string task_summary()
    {
        return "Looting dead bodies";
    }

    protected override bool ready_to_assign(settler s)
    {
        // Reset everything
        gate = null;
        looting = null;
        looting_path_element = null;
        path = null;
        walking_back = false;

        // Look for nearest gate that the settler can reach
        gate = utils.find_to_min(town_gate.gate_group(s.group), (g) => g.distance_to(this));
        if (gate == null) return false; // No gate found

        // Search for a character to loot
        foreach (var c in gate.GetComponentsInChildren<character>())
        {
            if (c == null) continue; // Can't loot deleted characters
            if (!c.is_dead) continue; // Can't loot alive characters
            if (c is settler) continue; // Can't loot settlers
            var inv = c.GetComponentInChildren<inventory>();
            if (inv == null) continue; // Can't loot character with no inventory
            var cts = inv.contents();
            if (cts.Count == 0) continue; // Nothing to loot
            var tentative_target = town_path_element.nearest_element(c.transform.position, s.group);
            if (tentative_target == null || tentative_target.distance_to(c) > 2f) continue; // Inaccessible
            looting = c;
            looting_path_element = tentative_target;
            break;
        }
        if (looting == null || looting_path_element == null) return false; // No character to loot, or path element

        // Path from lootbox to character to loot
        path = town_path_element.path.get(path_element(s.group), looting_path_element);
        if (path == null) return false; // No path to loot

        return true;
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        if (looting == null) return STAGE_RESULT.TASK_FAILED; // Nothing to loot
        if (path == null) return STAGE_RESULT.TASK_FAILED; // No path somehow

        switch (path.walk(s, s.walk_speed, forwards: !walking_back))
        {
            case town_path_element.path.WALK_STATE.COMPLETE:

                if (walking_back)
                {
                    // Made our way back - transfer items    
                    var inv = looting.GetComponentInChildren<inventory>();
                    var this_inv = GetComponent<chest>().inventory;
                    foreach (var kv in inv.contents())
                        if (inv.remove(kv.Key, kv.Value))
                            this_inv.add(kv.Key, kv.Value);

                    return STAGE_RESULT.TASK_COMPLETE;
                }
                else
                {
                    // Got to character we're looting - walk back to lootbox
                    walking_back = true;
                    return STAGE_RESULT.STAGE_UNDERWAY;
                }

            case town_path_element.path.WALK_STATE.UNDERWAY: return STAGE_RESULT.STAGE_UNDERWAY;
            default: return STAGE_RESULT.TASK_FAILED;
        }
    }
}
