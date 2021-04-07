using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class lootbox : settler_interactable
{
    class loot_task
    {
        town_gate gate;
        character looting;
        lootbox lootbox;
        town_path_element looting_path_element;
        town_path_element.path path;
        bool walking_back = false;

        private loot_task() { }
        public static loot_task try_get_loot_task(settler s, lootbox l)
        {
            loot_task ret = new loot_task();
            ret.lootbox = l;

            // Look for nearest gate that the settler can reach
            ret.gate = utils.find_to_min(town_gate.gate_group(s.group), (g) => g.distance_to(l));
            if (ret.gate == null) return null; // No gate found

            // Search for a character to loot
            foreach (var c in ret.gate.GetComponentsInChildren<character>())
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
                ret.looting = c;
                ret.looting_path_element = tentative_target;
                break;
            }
            if (ret.looting == null || ret.looting_path_element == null) return null; // No character to loot, or path element

            // Path from lootbox to character to loot
            ret.path = new town_path_element.path(l.path_element(s.group), ret.looting_path_element);
            if (!ret.path.valid) return null; // No path to loot

            return ret;
        }

        public INTERACTION_RESULT on_interact(settler s)
        {
            if (looting == null) return INTERACTION_RESULT.FAILED; // Nothing to loot
            if (path == null || !path.valid) return INTERACTION_RESULT.FAILED; // No path somehow

            var next_element = path.walk(s.transform, s.walk_speed, s);
            if (next_element == null)
            {
                if (walking_back)
                {
                    // Made our way back - transfer items    
                    var inv = looting.GetComponentInChildren<inventory>();
                    var this_inv = lootbox.GetComponent<chest>().inventory;
                    foreach (var kv in inv.contents())
                        if (inv.remove(kv.Key, kv.Value))
                        {
                            this_inv.add(kv.Key, kv.Value);
                            Debug.Log(kv.Key + " x " + kv.Value);
                        }

                    return INTERACTION_RESULT.COMPLETE;
                }
                else
                {
                    // Got to character we're looting - walk back to lootbox
                    walking_back = true;
                    path = new town_path_element.path(looting_path_element, lootbox.path_element(looting_path_element.group));
                    return INTERACTION_RESULT.UNDERWAY;
                }
            }

            return INTERACTION_RESULT.UNDERWAY;
        }
    }

    Dictionary<settler, loot_task> loot_tasks = new Dictionary<settler, loot_task>();

    public override bool ready_to_assign(settler s)
    {
        // Ensure we don't have a task left over
        loot_tasks.Remove(s);

        var lt = loot_task.try_get_loot_task(s, this);
        if (lt != null)
        {
            loot_tasks[s] = lt;
            return true;
        }
        return false;
    }

    public override INTERACTION_RESULT on_assign(settler s)
    {
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override INTERACTION_RESULT on_interact(settler s)
    {
        if (!loot_tasks.TryGetValue(s, out loot_task lt))
            return INTERACTION_RESULT.FAILED;

        var ret = lt.on_interact(s);

        if (ret != INTERACTION_RESULT.UNDERWAY)
            loot_tasks.Remove(s);

        return ret;
    }
}
