using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> An object which potentially processes items from 
/// <see cref="item_link_point"/>s of INPUT type and potentially 
/// outputs items from <see cref="item_link_point"/>s of OUTPUT type
/// </summary>
public abstract class item_proccessor : MonoBehaviour
{
    public item_link_point[] link_points =>
        GetComponentsInChildren<item_link_point>();
}

/// <summary> A point where item_processors link together. </summary>
public class item_link_point : MonoBehaviour
{
    const float END_MATCH_DISTANCE = 0.2f;
    const float UPHILL_LINK_ALLOW = 0.1f;

    /// <summary> The item currently occupying this link point, it 
    /// will be automatically moved to the next link point (if one
    /// is connected). </summary>
    public item item
    {
        get => _item;
        set
        {
            if (_item == value)
                return; // No change

            if (_item != null)
            {
                var err = "Tried to overwrite item at link point; " +
                    "you should check before setting link.item!";
                throw new System.Exception(err);
            }

            _item = value;
            time_got_item = Time.time;

            if (_item != null)
                _item.transform.position = transform.position;
        }
    }
    item _item;

    float time_got_item = 0;

    /// <summary> Remove my item, and 
    /// return a reference to it. </summary>
    public item release_item()
    {
        var tmp = item;
        _item = null;
        return tmp;
    }

    /// <summary> Called when an item is transferred 
    /// to a new link point. </summary>
    public void transfer_item(item_link_point to)
    {
        to.item = release_item();
    }

    /// <summary> Drop the item I have. </summary>
    public void drop_item()
    {
        item_dropper.create(release_item(), this);
    }

    /// <summary> Delete the item I have. </summary>
    public void delete_item()
    {
        Destroy(release_item().gameObject);
    }

    public TYPE type;
    public Vector3 position => transform.position;

    public building_material building => GetComponentInParent<building_material>();
    public item_link_point linked_to { get; private set; }

    public delegate void on_disconnect_function();
    public on_disconnect_function on_disconnect = () => { };

    private void Start()
    {
        register_point(this);
    }

    private void OnDestroy()
    {
        if (item != null)
            Destroy(item.gameObject);

        forget_point(this);
    }

    private void Update()
    {
        // Don't do anything unless we're an output and have an item
        if (type != TYPE.OUTPUT) return;
        if (item == null) return;

        // Not linked to anything => drop+delete the item
        if (linked_to == null)
        {
            item.transform.position = position;
            drop_item();
            return;
        }
        else if (linked_to.item != null)
        {
            // The next link isn't free => don't do anything
            return;
        }

        // Move the item along the path to the 
        // next link (accelerate with gravity)
        Vector3 delta = linked_to.position - item.transform.position;
        float dt = Time.time - time_got_item;
        float max_move = Time.deltaTime * dt * 10f;

        bool arrived = false;
        if (delta.magnitude > max_move) delta = delta.normalized * max_move;
        else arrived = true;

        item.transform.position += delta;

        if (arrived)
        {
            // Check there is free space at the next link
            if (linked_to.item == null)
            {
                // Transfer item to the point I am linked to
                transfer_item(linked_to);
            }
        }
    }

    bool try_link_to(item_link_point other)
    {
        switch (type)
        {
            case TYPE.INPUT:
                if (other.type != TYPE.OUTPUT)
                    return false;
                if (test_connection(other, this))
                {
                    other.linked_to = this;
                    this.linked_to = other;
                    return true;
                }
                return false;

            case TYPE.OUTPUT:
                if (other.type != TYPE.INPUT)
                    return false;
                if (test_connection(this, other))
                {
                    other.linked_to = this;
                    this.linked_to = other;
                }
                return false;

            default:
                throw new System.Exception("Unkown type!");
        }
    }

    bool test_connection(item_link_point output, item_link_point input)
    {
        if (output.type != TYPE.OUTPUT)
            throw new System.Exception("Provided output is not an output!");
        if (input.type != TYPE.INPUT)
            throw new System.Exception("Provided input is not an input!");

        if (output.position.y + UPHILL_LINK_ALLOW < input.position.y)
            return false; // Can't output uphill

        Vector3 delta = input.position - output.position;

        // Check if the ends are close enough for direct link
        if (delta.magnitude < END_MATCH_DISTANCE)
            return true;

        // Check if the ends are in line for a drop
        Vector3 dxz = delta;
        dxz.y = 0;
        if (dxz.magnitude > END_MATCH_DISTANCE)
            return false;

        // Check if the drop is possible
        foreach (var h in Physics.RaycastAll(
            output.position, delta, delta.magnitude))
        {
            if (h.transform.IsChildOf(output.transform) ||
                h.transform.IsChildOf(input.transform))
                continue; // Ignore collisions with to/from

            if (h.transform.GetComponentInParent<item>() != null)
                continue; // Ignore collisions with items             

            // Something in the way of the drop
            return false;
        }

        return true;
    }

    public enum TYPE
    {
        INPUT,
        OUTPUT
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        if (linked_to != null)
        {
            // Draw line halfway to my link (the other half should
            // be drawn the other way by my link)
            Vector3 delta = linked_to.position - position;
            Gizmos.DrawLine(position, position + delta / 2f);
        }

        if (type == TYPE.OUTPUT)
        {
            if (linked_to == null) Gizmos.color = Color.red;
            else Gizmos.color = Color.green;
        }
        else if (type == TYPE.INPUT)
        {
            if (linked_to == null) Gizmos.color = new Color(1, 0.5f, 0);
            else Gizmos.color = new Color(0, 1, 0.5f);
        }

        Gizmos.DrawWireSphere(position, END_MATCH_DISTANCE);
    }

    class item_dropper : MonoBehaviour
    {
        item item;
        item_link_point point;
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
                    // Don't drop onto the building I came 
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

        public static item_dropper create(item i, item_link_point point)
        {
            var dr = new GameObject("item_dropper").AddComponent<item_dropper>();
            dr.item = i;
            dr.point = point;
            dr.transform.position = i.transform.position;
            return dr;
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<item_link_point> points;

    public static void init_links()
    {
        points = new HashSet<item_link_point>();
    }

    static void validate_links(item_link_point p)
    {
        // Break previous link
        if (p.linked_to != null)
        {
            p.linked_to.on_disconnect();
            p.on_disconnect();
            p.linked_to.linked_to = null;
            p.linked_to = null;
        }

        // Create new links
        foreach (var p2 in points)
            if (p.try_link_to(p2))
                break;
    }

    static void register_point(item_link_point p)
    {
        if (points.Contains(p))
            throw new System.Exception("Tried to register a point multiple times!");

        validate_links(p);
        points.Add(p);
    }

    static void forget_point(item_link_point p)
    {
        if (!points.Remove(p))
            throw new System.Exception("Tried to remove unregisterd point!");

        // Check if any links have been freed up
        if (p.linked_to != null)
            validate_links(p.linked_to);
    }
}
