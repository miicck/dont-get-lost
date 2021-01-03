using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class mineshaft : settler_interactable_options, IAddsToInspectionText
{
    item_output output => GetComponentInChildren<item_output>();

    public float test_depth = 2f;

    bool on_terrain = false;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position - Vector3.up * test_depth);
    }

    new void Start()
    {
        base.Start();
        chunk.add_generation_listener(transform, (c) =>
        {
            // Test depth 
            var tc = utils.raycast_for_closest<TerrainCollider>(
                new Ray(transform.position, Vector3.down),
                out RaycastHit hit, max_distance: test_depth);

            on_terrain = tc != null;
        });
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (!on_terrain) return "Must be placed on terrain to operate!";
        return "Operating normally.";
    }

    //##############################//
    // settler_interactable_options //
    //##############################//

    public override string left_menu_display_name() { return "Mineshaft"; }

    protected override int options_count => minable_items.Count;

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

    float time_mining;

    public override INTERACTION_RESULT on_assign(settler s)
    {
        time_mining = 0;
        if (!on_terrain) return INTERACTION_RESULT.FAILED;
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override INTERACTION_RESULT on_interact(settler s)
    {
        if (time_mining + Time.deltaTime >= 1f && time_mining < 1f)
        {
            // This is the tick that will take us past mining
            // time, create the item
            var itm = minable_items[selected_option];
            var op = output;
            op.add_item(item.create(itm.name, op.transform.position,
                op.transform.rotation, logistics_version: true));

            return INTERACTION_RESULT.COMPLETE;
        }

        time_mining += Time.deltaTime;
        return INTERACTION_RESULT.UNDERWAY;
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