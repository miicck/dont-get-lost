using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An item output node from an object, such as a mining spot. </summary>
public class item_output : item_node
{
    public override string node_description(int item_count) { return item_count + " items waiting at output"; }

    private void Update()
    {
        if (item_count == 0) return; // No items => nothing to do
        item_dropper.create(release_next_item(), transform.position, next_output());
    }

    protected override bool can_output_to(item_node other)
    {
        // Only one output is allowed
        return output_count == 0;
    }

    protected override bool can_input_from(item_node other)
    {
        // Outputs don't accept inputs
        return false;
    }
}

public class item_dropper : MonoBehaviour
{
    item item;
    Vector3 from;
    Vector3 to;
    item_node give_to;
    float speed = 0;

    public static item_dropper create(item i, Vector3 from, item_node give_to)
    {
        Vector3 target;
        if (give_to != null)
        {
            // Drop to the given node
            target = give_to.input_point(from);
        }
        else
        {
            // Drop to the floor
            var collided = utils.raycast_for_closest<Collider>(
                new Ray(from, Vector3.down), out RaycastHit hit, accept: (h, c) =>
            {
                var itm = c.transform.GetComponentInParent<item>();
                if (itm == null) return true; // Accept collisions with non-items
                if (itm.is_logistics_version) return false; // Reject collisions with logistics items
                return true; // Accept collisions with non-logistics items
            });

            if (collided != null)
                target = hit.point;
            else
                target = from + 100f * Vector3.down;
        }

        var ret = new GameObject("item_dropper").AddComponent<item_dropper>();
        ret.item = i;
        ret.from = from;
        ret.to = target;
        ret.give_to = give_to;
        ret.transform.position = from;
        i.transform.SetParent(ret.transform);
        return ret;
    }

    private void Start()
    {
        item.transform.position = from;
    }

    private void Update()
    {
        if (item == null)
        {
            Destroy(gameObject);
            return;
        }

        // Accelerate with gravity
        speed += Time.deltaTime * 10f;

        if (utils.move_towards(item.transform, to, speed * Time.deltaTime))
        {
            // Pass item onto give_to, if it exists 
            // (otherwise destroy the item)
            if (give_to != null)
                give_to.add_item(item);

            // Destroy the item dropper
            Destroy(gameObject);
        }
    }
}
