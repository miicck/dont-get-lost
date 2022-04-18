using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_harvest_spot : character_interactable_options
{
    public List<item> options = new List<item>();
    public List<GameObject> enabled_when_operating = new List<GameObject>();
    public float harvest_time = 1;

    //##############################//
    // settler_interactable_options //
    //##############################//

    protected override option get_option(int i)
    {
        return new option
        {
            text = options[i].display_name,
            sprite = options[i].sprite
        };
    }

    protected override int options_count => options.Count;
    protected override string options_title => "Harvesting";

    //######################//
    // SETTLER_INTERACTABLE //
    //######################//

    item_output output => GetComponentInChildren<item_output>();

    float work_completed = 0;
    int harvested_count = 0;

    public override string task_summary() => "Harvesting " + options[selected_option].plural;

    protected override void Start()
    {
        base.Start();

        foreach (var go in enabled_when_operating)
            go.SetActive(false);
    }

    protected override void on_arrive(character c)
    {
        // Reset stuff
        work_completed = 0;
        harvested_count = 0;

        foreach (var go in enabled_when_operating)
            go.SetActive(true);
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        work_completed += Time.deltaTime * current_proficiency.total_multiplier;
        if (work_completed > (harvested_count + 1) * harvest_time)
        {
            harvested_count += 1;
            var itm = options[selected_option];
            production_tracker.register_product(itm);
            output.add_item(item.create(itm.name, output.transform.position,
                output.transform.rotation, logistics_version: true));
        }

        if (work_completed > 5f) return STAGE_RESULT.TASK_COMPLETE;
        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    protected override void on_unassign(character c)
    {
        foreach (var go in enabled_when_operating)
            go.SetActive(false);
    }
}
