using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object that can be connected to other 
/// objects of the same kind via road_links. </summary>
public class settler_path_element : MonoBehaviour, INonBlueprintable, INotEquippable
{
    public road_link[] links { get; private set; }

    public List<settler_path_element> linked_elements()
    {
        List<settler_path_element> ret = new List<settler_path_element>();
        foreach (var l in links)
            if (l.linked_to != null)
            {
                var rl = l.linked_to.GetComponentInParent<settler_path_element>();
                if (rl != null)
                    ret.Add(rl);
            }
        return ret;
    }

    bool start_called = false;
    private void Start()
    {
        // Register this element, if neccassary
        start_called = true;
        links = GetComponentsInChildren<road_link>();
        register_element(this);
    }

    private void OnDestroy()
    {
        // Unregister this element, if neccassary
        if (start_called)
            forget_element(this);
    }

    void try_link(settler_path_element other)
    {
        // Can't link to self
        if (other == this) return;

        foreach (var l in links)
        {
            // L already linked
            if (l.linked_to != null) continue;

            foreach (var l2 in other.links)
            {
                // L2 already linked
                if (l2.linked_to != null) continue;

                if ((l.transform.position - l2.transform.position).magnitude < 0.1f)
                {
                    // Make link both ways
                    l.linked_to = l2;
                    l2.linked_to = l;
                    break;
                }
            }
        }
    }

    void break_links()
    {
        foreach (var l in links)
        {
            if (l.linked_to != null)
            {
                l.linked_to.linked_to = null;
                l.linked_to = null;
            }
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<settler_path_element> all_elements;

    public static settler_path_element find_nearest(Vector3 position)
    {
        return utils.find_to_min(all_elements,
            (r) => (r.transform.position - position).sqrMagnitude);
    }

    public static void initialize()
    {
        // Initialize theelements collection
        all_elements = new HashSet<settler_path_element>();
    }

    static void validate_links(settler_path_element r)
    {
        // Re-make all links to/from r
        r.break_links();
        foreach (var r2 in all_elements)
            r.try_link(r2);
    }

    static void register_element(settler_path_element r)
    {
        // Create links to/from r, add r to the collection of elements.
        validate_links(r);
        if (!all_elements.Add(r))
            throw new System.Exception("Tried to register element twice!");
    }

    static void forget_element(settler_path_element r)
    {
        // Forget all the links to/from r, remove r from the collection of elements
        r.break_links();
        if (!all_elements.Remove(r))
            throw new System.Exception("Tried to forget unregistered element!");
    }
}
