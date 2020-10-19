using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class road : building_material
{
    bool dont_register => is_blueprint || is_equpped;

    public road_link[] links { get; private set; }

    public List<road> linked_roads()
    {
        List<road> ret = new List<road>();
        foreach (var l in links)
            if (l.linked_to != null)
            {
                var rl = l.linked_to.GetComponentInParent<road>();
                if (rl != null)
                    ret.Add(rl);
            }
        return ret;
    }

    private void Start()
    {
        // Register this road, if neccassary
        if (dont_register) return;
        links = GetComponentsInChildren<road_link>();
        register_road(this);
    }

    private void OnDestroy()
    {
        // Unregister this road, if neccassary
        if (dont_register) return;
        forget_road(this);
    }

    void try_link(road other)
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

    static HashSet<road> all_roads;

    public static road find_nearest(Vector3 position)
    {
        return utils.find_to_min(all_roads,
            (r) => (r.transform.position - position).sqrMagnitude);
    }

    public static void initialize()
    {
        // Initialize the roads collection
        all_roads = new HashSet<road>();
    }

    static void validate_links(road r)
    {
        // Re-make all links to/from r
        r.break_links();
        foreach (var r2 in all_roads)
            r.try_link(r2);
    }

    static void register_road(road r)
    {
        // Create links to/from r, add r to the collection of roads.
        validate_links(r);
        if (!all_roads.Add(r))
            throw new System.Exception("Tried to register road twice!");
    }

    static void forget_road(road r)
    {
        // Forget all the links to/from r, remove r from the collection of roads
        r.break_links();
        if (!all_roads.Remove(r))
            throw new System.Exception("Tried to forget unregistered road!");
    }
}
