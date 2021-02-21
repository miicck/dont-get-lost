using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bird : character
{
    public float flight_speed = 5f;

    protected override ICharacterController default_controller()
    {
        return new default_bird_controller();
    }
}

public class flight_path
{
    List<Vector3> path;
    int next_point = 0;

    public void draw_gizmos()
    {
        if (path == null) return;
        Gizmos.color = Color.green;
        for (int i = 1; i < path.Count; ++i)
            Gizmos.DrawLine(path[i], path[i - 1]);
    }

    public Vector3 next_position(Vector3 current_position, float distance_travelled)
    {
        float remaining_distance = distance_travelled;
        while (remaining_distance > 0)
        {
            Vector3 delta = path[next_point] - current_position;
            if (delta.magnitude < remaining_distance)
            {
                current_position += delta;
                remaining_distance -= delta.magnitude;
                next_point = (next_point + 1) % path.Count;
            }
            else return current_position + delta.normalized * remaining_distance;
        }
        return current_position;
    }

    public static flight_path looped_flight_path(Vector3 take_off_from, Vector3 direction, float radius, float altitiude)
    {
        var ret = new flight_path();
        ret.path = new List<Vector3>();

        direction.y = 0;
        direction.Normalize();
        Vector3 perp_direction = Quaternion.Euler(0, 90, 0) * direction;
        Vector3 centre = take_off_from + direction * radius;

        for (float angle = -180; angle < 180; angle += 20)
        {
            // Convert to radians
            float rad = Mathf.PI * angle / 180f;

            // Trace out the circle
            Vector3 location = centre - direction * radius * Mathf.Cos(rad)
                                 + perp_direction * radius * Mathf.Sin(rad);

            // Add the altititue, including landing/takeoff region
            float alt_frac = 1f;
            if (Mathf.Abs(rad) <= Mathf.PI / 2f) alt_frac = Mathf.Sin(Mathf.Abs(rad));
            location += alt_frac * altitiude * Vector3.up;

            // Start at the zero angle
            if (Mathf.Abs(angle) < 4f) ret.next_point = ret.path.Count;

            // Ensure we don't fly through anything
            if (ret.path.Count > 0 && alt_frac > 0.1f)
            {
                Vector3 from = ret.path[ret.path.Count - 1];
                Vector3 delta = location - from;
                if (Physics.Raycast(new Ray(from, delta), delta.magnitude))
                    return null;
            }

            ret.path.Add(location);
        }

        return ret;
    }
}

public class default_bird_controller : ICharacterController
{
    bird bird;
    flight_path flight_path;

    bool flying
    {
        get => _flying;
        set
        {
            foreach (var w in bird.GetComponentsInChildren<wing>())
                w.is_flying = value;

            foreach (var l in bird.GetComponentsInChildren<leg>())
                l.state = value ? leg.STATE.BIRD_TUCKED : leg.STATE.NORMAL;
        }
    }
    bool _flying;

    public void control(character c)
    {
        bird = (bird)c;
        if (!flying) flying = true;

        if (flight_path == null)
        {
            flight_path = global::flight_path.looped_flight_path(
                bird.transform.position, Random.onUnitSphere, Random.Range(2f, 20f), Random.Range(10f, 30f));
            return;
        }

        Vector3 next = flight_path.next_position(bird.transform.position, Time.deltaTime * bird.flight_speed);
        Vector3 delta = next - bird.transform.position;
        bird.transform.position = next;
        bird.transform.forward = delta.normalized;
    }

    public void on_end_control(character c)
    {
        flying = false;
    }

    public void draw_gizmos() { flight_path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "A bird."; }
}
