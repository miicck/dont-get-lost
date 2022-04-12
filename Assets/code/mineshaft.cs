using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mineshaft : settler_interactable_options, IAddsToInspectionText
{
    item_output output => GetComponentInChildren<item_output>();

    public float test_depth = 2f;

    bool on_valid_ground = false;

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position - Vector3.up * test_depth);
    }

    new void Start()
    {
        base.Start();
        chunk.add_generation_listener(transform, (c) =>
        {
            // Test depth 
            var tc = utils.raycast_for_closest<allows_mineshaft>(
                new Ray(transform.position, Vector3.down),
                out RaycastHit hit, max_distance: test_depth);

            on_valid_ground = tc != null;
        });
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        return base.added_inspection_text() + "\n" +
            (on_valid_ground ? "Operating normally." : "Must be placed on terrain to operate!");
    }

    //##############################//
    // settler_interactable_options //
    //##############################//

    protected override int options_count => minable_items.Count;
    protected override string options_title => "Mining";

    protected override option get_option(int i)
    {
        var mi = minable_items[i];
        return new option()
        {
            text = mi.display_name,
            sprite = mi.sprite
        };
    }

    //######################//
    // settler_interactable //
    //######################//

    float work_done;

    public override string task_summary()
    {
        var itm = minable_items[selected_option];
        return "Mining " + itm.plural;
    }

    protected override bool ready_to_assign(character c) => on_valid_ground;
    protected override void on_arrive(character c) => work_done = 0;

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        float delta_work = Time.deltaTime * current_proficiency.total_multiplier;

        if (work_done + delta_work >= 1f && work_done < 1f)
        {
            // This is the tick that will take us past mining
            // time, create the item
            var itm = minable_items[selected_option];
            var op = output;
            production_tracker.register_product(itm);
            op.add_item(item.create(itm.name, op.transform.position,
                op.transform.rotation, logistics_version: true));

            return STAGE_RESULT.TASK_COMPLETE;
        }

        work_done += delta_work;
        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    protected override List<proficiency> proficiencies(character c)
    {
        var ret = base.proficiencies(c);

        if (c is settler)
        {
            var s = (settler)c;

            tool tool = tool.find_best_in(s.inventory, tool.TYPE.PICKAXE);
            if (tool == null) return ret;

            ret.Add(new item_based_proficiency(
                tool.proficiency, tool.display_name,
                s.inventory, tool, 0.1f));
        }

        return ret;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static List<item> minable_items
    {
        get
        {
            if (_minable_items == null)
            {
                _minable_items = new List<item>();
                foreach (var oim in Resources.LoadAll<obtainable_in_mineshaft>("items/"))
                    _minable_items.Add(oim.GetComponent<item>());
            }
            return _minable_items;
        }
    }
    static List<item> _minable_items;
}