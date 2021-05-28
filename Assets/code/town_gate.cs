using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : portal, IAddsToInspectionText
{
    public town_path_element path_element;
    public Transform outside_link;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Start()
    {
        // Don't spawn settlers if this is the 
        // equipped or blueprint version
        if (is_equpped || is_blueprint) return;

        update_gate_group(this);
        path_element.add_group_change_listener(() => update_gate_group(this));
        InvokeRepeating("attempt_spawn_settler", SPAWN_SETTLER_TIME, SPAWN_SETTLER_TIME);
    }

    private void OnDestroy()
    {
        if (is_equpped) return;
        if (is_blueprint) return;
        unregister_gate(this);
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        // Don't forward interactions through my attackers
        if (hit.transform.GetComponentInParent<character>() != null)
            return new player_interaction[0];
        return base.player_interactions(hit);
    }

    //##################//
    // SETTLER SPAWNING //
    //##################//

    const float SPAWN_SETTLER_TIME = 30f;
    int bed_count;

    void attempt_spawn_settler()
    {
        // Only spawn settlers on auth client
        if (!has_authority)
            return;

        // Don't do anything until the chunk is loaded
        if (!chunk.generation_complete(outside_link.position))
            return;

        var elements = town_path_element.element_group(path_element.group);
        bed_count = 0;
        foreach (var e in elements)
            if (e.interactable is bed)
                bed_count += 1;

        var settlers = settler.get_settlers_by_group(path_element.group);

        if (settlers.Count >= bed_count) return; // Not enough beds for another settler
        foreach (var s in settlers)
            if (s.nutrition.metabolic_satisfaction == 0)
                return; // Settlers are starving

        client.create(transform.position, "characters/settler");
    }

    //########//
    // PORTAL //
    //########//

    public override string init_portal_name() { return "Town gate"; }
    protected override string portal_ui() { return "ui/town_gate"; }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        return "Beds     : " + bed_count + "\n" +
               "Settlers : " + settler.get_settlers_by_group(path_element.group).Count;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<int, HashSet<town_gate>> town_gates_by_group;

    public static void initialize()
    {
        town_gates_by_group = new Dictionary<int, HashSet<town_gate>>();
    }

    public static HashSet<town_gate> gate_group(int group)
    {
        if (town_gates_by_group.TryGetValue(group, out HashSet<town_gate> set))
            return set;
        return new HashSet<town_gate>();
    }

    public static town_gate nearest_gate(Vector3 pos)
    {
        float min_dis = Mathf.Infinity;
        town_gate ret = null;

        foreach (var gg in town_gates_by_group)
            foreach (var g in gg.Value)
            {
                float dis = (g.transform.position - pos).sqrMagnitude;
                if (dis < min_dis)
                {
                    min_dis = dis;
                    ret = g;
                }
            }

        return ret;
    }

    static void update_gate_group(town_gate g)
    {
        unregister_gate(g);

        int group = g.path_element.group;
        if (town_gates_by_group.TryGetValue(group, out HashSet<town_gate> set))
            set.Add(g);
        else
            town_gates_by_group[group] = new HashSet<town_gate> { g };
    }

    static void unregister_gate(town_gate g)
    {
        foreach (var kv in town_gates_by_group)
            kv.Value.Remove(g);
    }
}