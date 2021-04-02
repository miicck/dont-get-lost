using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class town_path_link : MonoBehaviour, IEnumerable<town_path_link>
{
    public const float LINK_DISPLAY_ALT = 0.25f;
    public const float LINK_WIDTH = 0.05f;
    public const float POINT_WIDTH = 0.1f;

    public town_path_element path_element => GetComponentInParent<town_path_element>();

    public void link_to(town_path_link other)
    {
        // Create the link both ways
        linked_to.Add(other);
        other.linked_to.Add(this);
        update_display();
        other.update_display();
    }

    public void break_links(bool destroying = false)
    {
        // Break all of my links
        foreach (var l in linked_to)
        {
            l.linked_to.Remove(this);
            l.update_display();
        }
        linked_to.Clear();
        if (!destroying) update_display();
    }

    HashSet<town_path_link> linked_to = new HashSet<town_path_link>();
    public IEnumerator<town_path_link> GetEnumerator() { return linked_to.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

    GameObject display;

    public void update_display()
    {
        // Destroy the old display
        if (display != null)
        {
            Destroy(display.gameObject);
            display = null;
        }

        // Create the new display
        display = Resources.Load<GameObject>("misc/path_point").inst();
        display.transform.SetParent(transform);
        display.transform.localPosition = Vector3.up * LINK_DISPLAY_ALT;
        display.transform.localRotation = Quaternion.identity;
        display.transform.localScale = Vector3.one * POINT_WIDTH;

        foreach (var lt in linked_to)
        {
            var link = Resources.Load<GameObject>("misc/path_link").inst();
            Vector3 to = lt.transform.position - transform.position;
            link.transform.position = transform.position + to / 2f + Vector3.up * LINK_DISPLAY_ALT;
            link.transform.LookAt(lt.transform.position + Vector3.up * LINK_DISPLAY_ALT);
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
        get
        {
            return display != null && display.activeInHierarchy;
        }
        set
        {
            if (display == null) return;
            display.SetActive(value);
        }
    }

    private void Start()
    {
        // Don't do anything if this is equipped
        if (transform.GetComponentInParent<player>() != null)
            return;
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

    public static bool can_link(town_path_link a, town_path_link b)
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

            // Path section can link to a point if they are within 0.25f
            return a_sec.distance_to(b.transform.position) < 0.25f;
        }
        else if (b is settler_path_section)
            return can_link(b, a); // Deal with the mixed case when it's the other way around

        // Default: can be linked if they're within 0.25f
        return (a.transform.position - b.transform.position).magnitude < 0.25f;
    }
}
