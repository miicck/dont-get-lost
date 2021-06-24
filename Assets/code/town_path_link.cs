using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_path_link : MonoBehaviour, IEnumerable<town_path_link>, INonLogistical
{
    public const float MAX_LINK_DISTANCE = 0.25f;
    public const float LINK_GROUND_CLEARANCE = 0.25f;

    public town_path_element path_element => GetComponentInParent<town_path_element>();

    //#########//
    // LINKING //
    //#########//

    HashSet<town_path_link> linked_to = new HashSet<town_path_link>();
    public IEnumerator<town_path_link> GetEnumerator() { return linked_to.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

    public bool try_link(town_path_link other)
    {
        // Check if the link is possible
        if (!can_link(this, other)) return false;

        // Create the link both ways
        linked_to.Add(other);
        other.linked_to.Add(this);

        // Link created => display update is required
        display_update_required = true;
        other.display_update_required = true;

        return true;
    }

    public void break_links()
    {
        // Break all of my links
        foreach (var l in linked_to)
        {
            l.linked_to.Remove(this);
            l.display_update_required = true;
        }
        linked_to.Clear();
        display_update_required = true;
    }

    public virtual Bounds linkable_region()
    {
        return new Bounds(transform.position, Vector3.one * LINK_WIDTH * 2);
    }

    /// <summary> The point above this link by the ground clearance amount. </summary>
    Vector3 ground_clearance_point => transform.position + Vector3.up * LINK_GROUND_CLEARANCE;

    //#########//
    // DISPLAY //
    //#########//

    public const float LINK_WIDTH = 0.05f;
    public const float POINT_WIDTH = 0.1f;

    GameObject display;
    bool display_update_required = false;

    public void update_display()
    {
        // Reset required flag
        display_update_required = false;

        // Destroy the old display
        if (display != null)
        {
            Destroy(display.gameObject);
            display = null;
        }

        // Create the new display
        display = Resources.Load<GameObject>("misc/path_point").inst();
        display.transform.SetParent(transform);
        display.transform.localPosition = Vector3.up * LINK_GROUND_CLEARANCE;
        display.transform.localRotation = Quaternion.identity;
        display.transform.localScale = Vector3.one * POINT_WIDTH;

        foreach (var lt in linked_to)
        {
            var link = Resources.Load<GameObject>("misc/path_link").inst();
            Vector3 to = lt.transform.position - transform.position;
            link.transform.position = ground_clearance_point + to / 2f;
            link.transform.LookAt(lt.ground_clearance_point);
            link.transform.localScale = new Vector3(LINK_WIDTH, LINK_WIDTH, to.magnitude);
            link.transform.SetParent(display.transform);
        }

        // Make the display red or green depending on if it's linked to anything
        display.GetComponent<Renderer>().material =
            linked_to.Count > 0 ?
            Resources.Load<Material>("materials/green") :
            Resources.Load<Material>("materials/red");

        // Sstate depends on if drawing is enabled
        display.SetActive(town_path_element.draw_links);
    }

    public bool display_enabled
    {
        get => display != null && display.activeInHierarchy;
        set => display?.SetActive(value);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    private void Update()
    {
        if (display_update_required) update_display();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        foreach (var lt in linked_to)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, lt.transform.position);
        }
        Gizmos.DrawWireSphere(transform.position, 0.25f / 2f);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static bool can_link(town_path_link a, town_path_link b)
    {
        if (!link_possible(a, b)) return false;
        if (a.path_element == null || b.path_element == null) return false;

        var a_build = a.GetComponentInParent<building_material>();
        var b_build = b.GetComponentInParent<building_material>();

        Vector3 delta = b.ground_clearance_point - a.ground_clearance_point;
        Ray ray = new Ray(a.ground_clearance_point, delta);
        foreach (var h in Physics.RaycastAll(ray, delta.magnitude))
        {
            // We can't be blocked by the building we belong to (?)
            // if (h.transform.IsChildOf(a_build.transform)) continue;
            // if (h.transform.IsChildOf(b_build.transform)) continue;

            if (h.collider.GetComponentInParent<INotPathBlocking>() == null)
                return false; // Path blocked
        }

        return true;
    }

    static bool link_possible(town_path_link a, town_path_link b)
    {
        if (a is settler_path_section)
        {
            var a_sec = (settler_path_section)a;

            if (b is settler_path_section)
            {
                // Two path sections can be linked if they overlap
                var b_sec = (settler_path_section)b;
                return a_sec.overlaps(b_sec);
            }

            // Path section can link to a point if they are within MAX_LINK_DISTANCE
            return a_sec.distance_to(b.transform.position) < MAX_LINK_DISTANCE;
        }
        else if (b is settler_path_section)
            return can_link(b, a); // Deal with the mixed case when it's the other way around

        // Default: can be linked if they're within MAX_LINK_DISTANCE
        return a.distance_to(b) < MAX_LINK_DISTANCE;
    }
}
