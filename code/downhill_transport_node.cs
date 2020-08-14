using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class downhill_transport_node : item_transport_node
{
    public bool allow_drop = false;

    protected override bool can_output_to(item_transport_node other)
    {
        Vector3 delta = other.position - position;
        if (delta.y > 0) return false; // Don't allow uphill
        if (allow_drop) delta.y = 0; // Ignore y-componenet
        return delta.magnitude < NODE_OVERLAP_DIST;
    }

    protected override bool allow_incoming_items(item_transport_node from)
    {
        // Only allow input if we have output (including dropping potential)
        if (allow_drop) return true;
        return outputs_count > 0;
    }

    protected override void on_item_no_output()
    {
        if (!allow_drop) return;
        item_dropper.create(release_item(), this); 
    }
}

public class item_dropper : MonoBehaviour
{
    item item;
    item_transport_node point;
    float target_alt;
    float start_time = 0;

    private void Start()
    {
        start_time = Time.time;
        target_alt = transform.position.y - 100f;
        var found = utils.raycast_for_closest<Transform>(
            new Ray(transform.position, Vector3.down), out RaycastHit hit,
            accept: (t) =>
            {
                // Don't drop onto the processor I came
                // from, or onto other items.
                if (t.IsChildOf(point.building.transform)) return false;
                if (t.GetComponentInParent<item>() != null) return false;
                return true;
            });

        if (found != null)
            target_alt = hit.point.y;
    }

    private void Update()
    {
        // Make the item fall
        float dt = Time.time - start_time;
        item.transform.position += Vector3.down * Time.deltaTime * dt * 10f;

        // Item has reached the bottom
        if (item.transform.position.y < target_alt)
        {
            Destroy(item.gameObject);
            Destroy(gameObject);
        }
    }

    public static item_dropper create(item i, item_transport_node point)
    {
        var dr = new GameObject("item_dropper").AddComponent<item_dropper>();
        dr.item = i;
        dr.point = point;
        dr.transform.position = i.transform.position;
        return dr;
    }
}
