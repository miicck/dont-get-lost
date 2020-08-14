using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A point-like object representing a waypoint
/// in the item transport system. </summary>
public abstract class item_transport_node : MonoBehaviour
{
    protected const float ITEM_SPACING = 0.25f;
    protected const float NODE_OVERLAP_DIST = ITEM_SPACING * 1.01f;

    // The transport nodes I output to/input from
    List<item_transport_node> outputs = new List<item_transport_node>();
    List<item_transport_node> inputs = new List<item_transport_node>();
    public int outputs_count => outputs.Count;
    public int inputs_count => inputs.Count;

    /// <summary> Functions determining when this node can be linked to others
    /// can_input_from will can only be called if the can_output_to from
    /// the other direction has already been validated (most of the time
    /// a.can_output_to(b) should imply b.can_input_from(a), hence can_input_from
    /// defaults to true). </summary>
    protected abstract bool can_output_to(item_transport_node other);
    protected virtual bool can_input_from(item_transport_node other) { return true; }

    /// <summary> Should return false if item input is currently disabled. </summary>
    protected virtual bool allow_incoming_items(item_transport_node from) { return true; }

    /// <summary> Called on Update() when we have an item but no output. </summary>
    protected virtual void on_item_no_output() { }

    /// <summary> The player-constructed item that I am part of. </summary>
    public building_material building => GetComponentInParent<building_material>();

    // The item currently controlled by this transport node
    protected item item
    {
        get => _item;
        set
        {
            if (_item != null)
            {
                var err = "Tried to overwrite item at link point; " +
                    "you should check before setting link.item!";
                throw new System.Exception(err);
            }

            _item = value;
            if (_item != null)
            {
                _item.transform.SetParent(transform);
                _item.transform.localPosition = Vector3.zero;
            }
        }
    }
    private item _item;

    // Release/return the item currently at this node
    public item release_item()
    {
        item tmp = item;
        _item = null;
        return tmp;
    }

    public bool has_item => item != null;

    // Set to true in inspector to request an item to spawn
    public bool spawn_request = false;

    // Utilities
    public Vector3 position => transform.position;

    int output_to = 0;
    protected virtual void Update()
    {
        if (spawn_request)
            if (!has_item) 
                item = Resources.Load<item>("items/iron_ore").inst();

        // Don't do anything unless we have an item
        // and somewhere to output it to
        if (!has_item) return;
        if (outputs_count == 0)
        {
            on_item_no_output();
            return;
        }

        // Ensure output_to is in range
        output_to = output_to % outputs_count;

        // See if we can currently send items to the chosen output
        if (outputs[output_to].has_item || 
           !outputs[output_to].allow_incoming_items(this))
        {
            // Output has item, cycle to next output
            output_to = (output_to + 1 ) % outputs_count;
            return;
        }

        // Move item towards output
        Vector3 delta = outputs[output_to].position - item.transform.position;
        float max_move = Time.deltaTime;
        bool arrived = false;
        if (delta.magnitude > max_move)
            delta = delta.normalized * max_move;
        else arrived = true;
        item.transform.position += delta;

        if (arrived)
        {
            // Transfer item to output
            outputs[output_to].item = release_item();

            // Cycle selected output
            output_to = output_to + 1;
        }
    }

    void OnDrawGizmos()
    {
        if (this == null) return; // Been deleted

        if (inputs_count == 0 && outputs_count == 0)
            Gizmos.color = new Color(1,0,0); // Red
        else if (inputs_count == 0)
            Gizmos.color = new Color(1,1,0); // Yellow
        else if (outputs_count == 0)
            Gizmos.color = new Color(0,1,1); // Cyan
        else
            Gizmos.color = new Color(0,1,0); // Green
        Gizmos.DrawWireSphere(position, ITEM_SPACING/2f);

        // Draw inputs in yellow
        Gizmos.color = new Color(1,1,0);
        foreach(var n in inputs)
        {
            Vector3 delta = n.position - position;
            Gizmos.DrawLine(position, position+delta/2f);
        }

        // Draw outputs in green
        Gizmos.color = new Color(0,1,0);
        foreach(var n in outputs)
        {
            if (n == null) continue;
            Vector3 delta = n.position - position;
            Gizmos.DrawLine(position, position+delta/2f);
        }
    }

    protected virtual void Start() { register_node(this); }
    protected virtual void OnDestroy() { forget_node(this); }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<item_transport_node> nodes;

    public static void initialize()
    {
        nodes = new HashSet<item_transport_node>();
    }

    /// <summary> Attempts to link output from to input to,
    /// returns true if linked successfully. </summary>
    static bool try_link(item_transport_node from, item_transport_node to)
    {
        if (from.can_output_to(to) && to.can_input_from(from))
        {
            from.outputs.Add(to);
            to.inputs.Add(from);
            return true;
        }

        return false;
    }

    /// <summary> forces a link to exist between the given nodes </summary>
    protected static void force_link(item_transport_node from, item_transport_node to)
    {
        from.outputs.Add(to);
        to.inputs.Add(from);
    }

    static void validate_links(item_transport_node p)
    {
        // Break previous output links
        foreach (var p2 in p.outputs)
            p2.inputs.Remove(p);
        p.outputs.Clear();

        // Break previous input links
        foreach (var p2 in p.inputs)
            p2.outputs.Remove(p);
        p.inputs.Clear();

        // Create new links
        foreach (var p2 in nodes)
        {
            // Try to link both ways
            try_link(p,  p2);
            try_link(p2, p);
        }
    }

    static void register_node(item_transport_node p)
    {
        if (nodes.Contains(p))
            throw new System.Exception("Tried to register a node multiple times!");

        validate_links(p);
        nodes.Add(p);
    }

    static void forget_node(item_transport_node p)
    {
        if (!nodes.Remove(p))
            throw new System.Exception("Tried to remove unregisterd node!");

        // Validate any broken links
        foreach (var p2 in p.inputs.ToArray())
            validate_links(p2);
        foreach (var p2 in p.outputs.ToArray())
            validate_links(p2);
    }

    //###############//
    // CUSTOM EDITOR //
    //###############//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_transport_node), true)]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var node = (item_transport_node)target;
            UnityEditor.EditorGUILayout.IntField("Inputs", node.inputs_count);
            UnityEditor.EditorGUILayout.IntField("Outputs", node.outputs_count);
        }
    }
#endif
}
