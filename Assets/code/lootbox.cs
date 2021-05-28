using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lootbox : walk_to_settler_interactable
{
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
        looting = null;
        looting_path_element = null;
        path = null;
        walking_back = false;

        // Search for a character to loot
        group_info.iterate_over_attackers(s.group, (c) =>
        {
            if (c == null) return false; // Can't loot deleted characters
            if (!c.is_dead) return false; // Can't loot alive characters

            var inv = c.GetComponentInChildren<inventory>();
            if (inv == null) return false; // Can't loot character with no inventory
            var cts = inv.contents();
            if (cts.Count == 0) return false; // Nothing to loot

            var tentative_target = town_path_element.nearest_element(c.transform.position, s.group);
            if (tentative_target == null || tentative_target.distance_to(c) > 2f) return false; // Inaccessible

            looting = c;
            looting_path_element = tentative_target;

            return true;
        });

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
