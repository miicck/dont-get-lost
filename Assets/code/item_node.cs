using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A node in the item logistics network. </summary>
public abstract class item_node : MonoBehaviour, INonBlueprintable, INonEquipable
{
    public const float LINK_DISTANCE_TOLERANCE = 0.25f;
    public const float UPHILL_LINK_ALLOW = 0.05f;

    /// <summary> The parent building to which this item node belongs. </summary>
    public building_material building => GetComponentInParent<building_material>();

    //#######//
    // ITEMS //
    //#######//

    /// <summary> The items this node point is currently responsible for. </summary>
    List<item> items = new List<item>();
    public int item_count
    {
        get
        {
            remove_deleted_items();
            return items.Count;
        }
    }

    protected item get_item(int i)
    {
        remove_deleted_items();
        return items[i];
    }

    void remove_deleted_items()
    {
        items.RemoveAll((i) => i == null);
    }

    /// <summary> Add an item to this node. </summary>
    public void add_item(item i)
    {
        if (!on_add_item(i)) return;

        items.Add(i);
        items.Sort(sort_items);
        i.transform.SetParent(transform);

        if (!on_change_being_called) // Prevent recursive on_change calls
        {
            on_change_being_called = true;
            on_change();
            on_change_being_called = false;
        }
    }

    /// <summary> Called just before an item is added to my items list, in 
    /// case we want to delete it, rather than accept it. </summary>
    protected virtual bool on_add_item(item i) { return true; }

    /// <summary> Release the <paramref name="i"/>th item from this node. </summary>
    public item release_item(int i)
    {
        if (items.Count <= i) return null;
        item ret = items[i];
        items.RemoveAt(i);

        if (!on_change_being_called) // Prevent recursive on_change calls
        {
            on_change_being_called = true;
            on_change();
            on_change_being_called = false;
        }

        return ret;
    }

    /// <summary> Releases the first item from the from this node. </summary>
    public item release_next_item() { return release_item(0); }

    /// <summary> Releases all items from this node. </summary>
    public List<item> relesae_all_items()
    {
        List<item> ret = new List<item>(items);
        items.Clear();

        if (!on_change_being_called) // Prevent recursive on_change calls
        {
            on_change_being_called = true;
            on_change();
            on_change_being_called = false;
        }

        return ret;
    }

    /// <summary> Used to determine the order that items are 
    /// maintained in the list. Defaults to closest-to-output 
    /// at position 0. </summary>
    protected virtual int sort_items(item a, item b)
    {
        float a_dis = (a.transform.position - output_point).magnitude;
        float b_dis = (b.transform.position - output_point).magnitude;
        return a_dis.CompareTo(b_dis);
    }

    public delegate void on_change_func();
    on_change_func on_change = () => { };
    bool on_change_being_called = false;

    public void add_on_change_listener(on_change_func f)
    {
        on_change += f;
    }

    //########//
    // INPUTS //
    //########//

    /// <summary> Should return true if this node can get input
    /// from the given <paramref name="other"/> node. </summary>
    protected abstract bool can_input_from(item_node other);

    /// <summary> The item nodes that input to this node. </summary>
    List<item_node> inputs_from = new List<item_node>();
    public int input_count => inputs_from.Count;

    /// <summary> The point where items are input to this node. </summary>
    public virtual Vector3 input_point(Vector3 input_from) { return transform.position; }

    //#########//
    // OUTPUTS //
    //#########//

    /// <summary> Should return true if this node can output
    /// to the given <paramref name="other"/> node. </summary>
    protected abstract bool can_output_to(item_node other);

    /// <summary> The item nodes that this node outputs to. </summary>
    List<item_node> outputs_to = new List<item_node>();
    public int output_count => outputs_to.Count;

    /// <summary> The point where items are output from this node. </summary>
    public virtual Vector3 output_point => transform.position;

    public item_node nearest_output => utils.find_to_min(outputs_to,
        (o) => (o.input_point(output_point) - output_point).magnitude);

    /// <summary> Get the next output, in a cyclic fashion. </summary>
    protected item_node next_output()
    {
        if (output_count == 0) return null;
        output_number = (output_number + 1) % output_count;
        return outputs_to[output_number];
    }
    int output_number = 0;

    //#################//
    // UNITY CALLBACKS //
    //#################//

    // Keep track of nodes
    bool registered = false;
    protected virtual void Start()
    {
        // Register the node, only after the chunk has finished generating
        chunk.add_generation_listener(transform.position, (c) =>
        {
            registered = true;
            register_node(this);
        });
    }

    protected virtual void OnDestroy()
    {
        if (registered)
            forget_node(this);
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var l in outputs_to)
            Gizmos.DrawLine(output_point, l.input_point(output_point));
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<item_node> nodes;

    public static void initialize()
    {
        nodes = new HashSet<item_node>();
    }

    /// <summary> Create a node where <paramref name="from"/> 
    /// outputs to <paramref name="to"/>. </summary>
    static void create_connection(item_node from, item_node to)
    {
        if (from == to)
            throw new System.Exception("Can't connect to self!");

        if (from.outputs_to.Contains(to))
            throw new System.Exception("Output node already exists!");

        if (to.inputs_from.Contains(from))
            throw new System.Exception("Input node already exists!");

        from.outputs_to.Add(to);
        to.inputs_from.Add(from);
    }

    /// <summary> Remove all input and output nodes 
    /// from <paramref name="node"/>. </summary>
    static void break_all_connections(item_node node)
    {
        foreach (var ip in node.inputs_from) ip?.outputs_to.Remove(node);
        foreach (var ot in node.outputs_to) ot?.inputs_from.Remove(node);
        node.inputs_from.Clear();
        node.outputs_to.Clear();
    }

    /// <summary> Returns true if the output from <paramref name="from"/>
    /// can be nodeed to the input of <paramref name="to"/>. </summary>
    static bool test_connection(item_node from, item_node to)
    {
        return from.can_output_to(to) && to.can_input_from(from);
    }

    /// <summary> Checks that all of the input/outputs 
    /// for the given node are up-to-date. </summary>
    static void validate_connections(item_node node)
    {
        // Break previous nodes
        break_all_connections(node);

        // Remove deleted nodes
        nodes.RemoveWhere((n) => n == null);

        // Loop over all nodes, creating connections where possible
        foreach (var n in nodes)
        {
            if (n.Equals(node)) continue; // Can't connect to self

            // Attempt to create nodes in both directions
            if (test_connection(n, node)) create_connection(n, node);
            else if (test_connection(node, n)) create_connection(node, n);
        }
    }

    static void register_node(item_node node)
    {
        if (!nodes.Add(node))
            throw new System.Exception("node already registered!");

        // Create the connections to/from the new node
        validate_connections(node);
    }

    static void forget_node(item_node node)
    {
        // Remember the nodes that the node was involved with, 
        // so that they can be re-validated.
        List<item_node> to_validate = new List<item_node>();
        to_validate.AddRange(node.inputs_from);
        to_validate.AddRange(node.outputs_to);

        // Remove the node/break all of it's connections
        break_all_connections(node);
        if (!nodes.Remove(node))
            throw new System.Exception("Tried to remove unregistered node!");

        // Re-validate all the nodes that the node was connected with
        foreach (var l in to_validate)
            validate_connections(l);
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_node), true)]
    class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (!Application.isPlaying) return;

            item_node node = (item_node)target;

            UnityEditor.EditorGUILayout.TextArea(
                "Node information\n" +
                "    " + node.input_count + " inputs\n" +
                "    " + node.output_count + " outputs\n" +
                "    " + node.item_count + " items\n" +
                "    " + nodes.Count + " total nodes"
            );

            if (UnityEditor.EditorGUILayout.Toggle("validate connections", false))
                validate_connections(node);
        }
    }
#endif
}
