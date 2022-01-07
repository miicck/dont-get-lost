using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_gate : portal, IAddsToInspectionText
{
    public town_path_element path_element;

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
        var set = settler.get_settlers_by_group(path_element.group);

        float av_mood = 0;
        foreach (var s in set)
            av_mood += s.total_mood();
        av_mood /= set.Count;

        return
        "Settlers     : " + set.Count + "\n" +
        "Beds         : " + group_info.bed_count(path_element.group) + "\n" +
        "Average mood : " + Mathf.RoundToInt(av_mood);
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