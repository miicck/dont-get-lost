using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class impact_test_point : MonoBehaviour
{
    public Vector3 centre;
    public float length = 1f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawLine(centre, centre + Vector3.forward * length);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(centre + Vector3.forward * length, centre + Vector3.forward * boosted_length);
    }

    Vector3 last_test;
    Vector3 velocity;
    float last_test_time;

    float boosted_length { get => length + velocity.magnitude * Time.deltaTime / 4f; }

    public bool test(item i)
    {
        float dt = Time.realtimeSinceStartup - last_test_time;
        if (dt < 10e-4f) dt = 10e-4f;
        last_test_time = Time.realtimeSinceStartup;

        Vector3 world_centre = transform.TransformPoint(centre);
        velocity = (world_centre - last_test) / dt;
        last_test = world_centre; 

        Vector3 from = transform.TransformPoint(centre);
        bool hit = false;
        foreach (var h in Physics.RaycastAll(from, transform.forward, boosted_length))
        {
            // Don't consider hits on the current player
            if (h.transform.IsChildOf(player.current.transform))
                continue;

            var rend = h.transform.GetComponent<Renderer>();
            if (rend != null)
                material_sound.play(material_sound.TYPE.HIT, h.point, rend.material);

            var aii = h.collider.GetComponentInParent<accepts_item_impact>();

            if (aii != null)
            {
                if (aii.on_impact(i))
                    return true;
            }

            hit = true;
        }

        return hit;
    }
}

public class accepts_item_impact : MonoBehaviour
{
    public virtual bool on_impact(item i) { return false; }
}