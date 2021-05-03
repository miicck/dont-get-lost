using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An item input node to an object, such as an autocrafter. </summary>
public class item_input : item_node
{
    public override string node_description(int item_count)
    {
        if (item_count == 0) return "Input free";
        else return peek_next_item().display_name + " waiting at input";
    }

    protected override bool can_input_from(item_node other)
    {
        if (this == null || other == null)
            return false;

        Vector3 delta = input_point(other.output_point) - other.output_point;
        if (delta.y > UPHILL_LINK_ALLOW) return false; // Can't accept input from below

        if (delta.magnitude < LINK_DISTANCE_TOLERANCE)
            return true; // Close enough for direct node

        // Check if from/to are in line for a drop
        Vector3 dxz = new Vector3(delta.x, 0, delta.z);
        if (dxz.magnitude > LINK_DISTANCE_TOLERANCE)
            return false; // Don't line up properly

        // Check if the drop is possible
        foreach (var h in Physics.RaycastAll(other.output_point, delta, delta.magnitude))
            if (!ignore_logistics_collisions_with(h, building.transform, other.building.transform))
                return false; // Something in the way of the drop

        return true;
    }

    protected override bool on_add_item(item i)
    {
        // Only one items allowed to wait at input
        if (item_count > 0)
        {
            item_rejector.create(i, i.transform.position);
            //item_dropper.create(i, i.transform.position, null);
            return false;
        }
        return true;
    }

    protected override bool can_output_to(item_node other)
    {
        // Inputs can't output to anything
        return false;
    }
}

public class item_rejector : MonoBehaviour
{
    item item;
    Vector3 velocity;
    float time_created;

    public static item_rejector create(item i, Vector3 from)
    {
        var ret = new GameObject("rejector").AddComponent<item_rejector>();
        ret.transform.position = from;
        ret.time_created = Time.time;
        ret.item = i;

        ret.velocity.x = Random.Range(-1f, 1f);
        ret.velocity.z = Mathf.Sqrt(1f - ret.velocity.x * ret.velocity.x);
        if (Random.Range(0, 2) == 0) ret.velocity.z = -ret.velocity.z;
        ret.velocity.y = 2;

        i.transform.SetParent(ret.transform);
        i.transform.localPosition = Vector3.zero;

        return ret;
    }

    private void Update()
    {
        velocity -= Vector3.up * 10 * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        if (Time.time - time_created > 2)
            Destroy(gameObject);
    }
}