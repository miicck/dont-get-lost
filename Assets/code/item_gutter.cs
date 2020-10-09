using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A downhill track for items, represented 
/// as an extended item node. </summary>
public class item_gutter : item_node
{
    public const float ITEM_FLOW_TIMESPAN = 60f;
    public const float ITEM_SEPERATION = 0.3f;

    public Transform start;
    public Transform end;

    public override Vector3 output_point => end.position;
    public override Vector3 input_point(Vector3 input_from)
    {
        // Work out the nearest point to the downwards line
        // from input_from on the line from start to end
        // I found the maths for this surprisingly difficult
        // so there is probably a better/faster way to do this

        // Vector from input to end of line
        Vector3 to_end = end.position - input_from;

        // Vector from start of line to end of line
        Vector3 line = end.position - start.position;
        Vector3 along_line = line.normalized;

        // Down
        Vector3 down = Vector3.down;

        // The direction perpendicular to the line and to down
        Vector3 normal = Vector3.Cross(down, along_line);

        // An intermediate point, above the target point
        Vector3 inter = input_from + Vector3.Project(to_end, normal);

        // The direction from start to end, in the x-z plane
        Vector3 in_plane = along_line;
        in_plane.y = 0;
        in_plane.Normalize();

        // Vector to end of line from intermediate point
        Vector3 inter_to_end = end.position - inter;

        // The height of the result above the end of the line
        float h = -line.y * Vector3.Dot(inter_to_end, in_plane) / Vector3.Dot(line, in_plane);

        inter.y = end.position.y + h;
        return inter;
    }

    protected override bool can_input_from(item_node other)
    {
        Vector3 input_point = this.input_point(other.output_point);
        Vector3 delta = input_point - other.output_point;

        // Don't allow uphill links
        if (delta.y > UPHILL_LINK_ALLOW) return false;

        // Check input point is close enough to directly below the other
        delta.y = 0;
        if (delta.magnitude > LINK_DISTANCE_TOLERANCE) return false;

        // Check input position is between start and end (to within tolerance)
        Vector3 start_to_end = end.position - start.position;
        float distance_along = Vector3.Dot(input_point - start.position, start_to_end.normalized);
        if (distance_along < -LINK_DISTANCE_TOLERANCE) return false;
        if (distance_along > start_to_end.magnitude + LINK_DISTANCE_TOLERANCE) return false;


        // Check there is nothing in the way
        Vector3 out_to_in = input_point - other.output_point;
        foreach (var h in Physics.RaycastAll(other.output_point,
            out_to_in.normalized, out_to_in.magnitude))
        {
            // Ignore collisions with self/other
            if (h.transform.IsChildOf(building.transform)) continue;
            if (h.transform.IsChildOf(other.building.transform)) continue;

            // Ignore collisions with dropping items
            if (h.transform.GetComponentInParent<item_dropper>()) continue;

            // Hit something in-between, don't allow the connection
            return false;
        }

        return true;
    }

    protected override bool can_output_to(item_node other)
    {
        return true;
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(start.position, end.position);
    }

    protected override void Start()
    {
        // Switch start/end if they aren't going downhill
        if (start.position.y < end.position.y)
        {
            Transform tmp = start;
            start = end;
            end = tmp;
        }

        base.Start();
    }

    private void Update()
    {
        if (this == null)
            return; // Destroyed

        // Allign items to gutter
        for (int i = 0; i < item_count; ++i)
            get_item(i).transform.forward = end.position - start.position;

        for (int i = 1; i < item_count; ++i)
        {
            item b = get_item(i - 1);
            item a = get_item(i);

            // Get direction towards next item
            Vector3 delta = b.transform.position - a.transform.position;

            // Only move towards the next
            // item if we're far enough apart
            if (delta.magnitude > ITEM_SEPERATION)
            {
                // Move up to ITEM_SEPERATION away from the next item
                delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                float max_move = Time.deltaTime;
                if (delta.magnitude > max_move)
                    delta = delta.normalized * max_move;
                a.transform.position += delta;
            }
        }

        if (item_count > 0)
        {
            var itm = get_item(0);

            // Move first item towards output, dropping it off the end
            if (utils.move_towards(itm.transform, end.position, Time.deltaTime))
                item_dropper.create(release_item(0), end.position, nearest_output);
        }
    }
}
