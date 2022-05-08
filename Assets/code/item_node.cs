using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Classes that implement IDontBlockItemLogisitcs aren't considered
/// to block item logistics flow (mainly for the purposes of determining
/// connections bettween <see cref="item_node"/>s). </summary>
public interface IDontBlockItemLogisitcs { }

/// <summary> A node in the item logistics network. </summary>
public abstract class item_node : MonoBehaviour,
    INonBlueprintable, INonEquipableCallback,
    IAddsToInspectionText, IItemCollection,
    INonLogistical
{
    public const float LINK_DISTANCE_TOLERANCE = 0.25f;
    public const float UPHILL_LINK_ALLOW = 0.05f;

    /// <summary> The parent building to which this item node belongs. </summary>
    public building_material building => GetComponentInParent<building_material>();

    public void on_equip_callback(player player)
    {
        if (player.has_authority)
            display_enabled = true;
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public abstract string node_description(int item_count);
    public string added_inspection_text() { return node_description(item_count); }

    //#######//
    // ITEMS //
    //#######//

    /// <summary> The items this node point is currently responsible for. </summary>
    List<item> items = new List<item>();
    public int item_count => items.Count;
    protected item get_item(int i) => items[i];

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
    protected virtual bool on_add_item(item i) => true;

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

    public item peek_item(int i) => i < items.Count && i >= 0 ? items[i] : null;
    public item peek_next_item() => peek_item(0);
    public List<item> peek_all_items() => new List<item>(items);

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
    public void add_on_change_listener(on_change_func f) => on_change += f;

    protected virtual bool is_display_enabled() => outputs_display != null;

    Transform outputs_display;
    protected virtual void set_display(bool enabled)
    {
        if (outputs_display != null)
            Destroy(outputs_display.gameObject);

        if (!enabled) return;

        outputs_display = new GameObject("outputs_display").transform;
        outputs_display.position = output_point;
        outputs_display.SetParent(transform);
        outputs_display.localRotation = Quaternion.identity;

        foreach (var n in outputs_to)
        {
            Vector3 input_point = n.input_point(output_point);
            Vector3 delta = input_point - output_point;

            if (delta.magnitude > 10e-3)
            {
                GameObject path = Resources.Load<GameObject>("misc/output_path").inst();
                path.transform.SetParent(outputs_display);
                path.transform.position = (output_point + input_point) / 2f;
                path.transform.forward = delta;
                path.transform.localScale = new Vector3(0.02f, 0.02f, delta.magnitude);

                var start = Resources.Load<GameObject>("misc/output_path").inst();
                start.transform.SetParent(outputs_display);
                start.transform.position = input_point;
                start.transform.localRotation = Quaternion.identity;
                start.transform.localScale = Vector3.one * 0.05f;
            }

            var end = Resources.Load<GameObject>("misc/output_path").inst();
            end.transform.SetParent(outputs_display);
            end.transform.position = output_point;
            end.transform.localRotation = Quaternion.identity;
            end.transform.localScale = Vector3.one * 0.05f;
        }
    }

    public void refresh_display()
    {
        if (display_enabled)
            set_display(true);
    }

    //#################//
    // IItemCollection //
    //#################//

    public Dictionary<item, int> contents()
    {
        Dictionary<item, int> ret = new Dictionary<item, int>();
        foreach (var i in items)
        {
            var itm = Resources.Load<item>("items/" + i.name);
            if (ret.ContainsKey(itm)) ret[itm] += 1;
            else ret[itm] = 1;
        }
        return ret;
    }

    public bool add(item i, int count)
    {
        if (i == null) return false;

        if (i.is_prefab())
        {
            // Create the neccassary items in the node from the given prefab
            for (int n = 0; n < count; ++n)
                add_item(item.create(i.name,
                    input_point(transform.position),
                    transform.rotation,
                    logistics_version: true));
            return true;
        }

        if (i.is_logistics_version && count == 1)
        {
            // We're just adding one logistics item to the node
            // this is the normal use case of add_item(item i)
            add_item(i);
            return true;
        }

        throw new System.Exception("Tried to add non-logistics/multiple items to node!");
    }

    public bool remove(item i, int count)
    {
        for (int n = items.Count - 1; n >= 0; --n)
            if (items[n].name == i.name)
            {
                Destroy(release_item(n).gameObject);
                if (--count <= 0) return true;
            }
        return false;
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

    /// <summary> Called whenever the inputs change. </summary>
    protected virtual void on_inputs_change() { }

    /// <summary> Get the next input node, in a cyclic fashion. </summary>
    public item_node next_input()
    {
        if (input_count == 0) return null;
        input_number = (input_number + 1) % input_count;
        return inputs_from[input_number];
    }
    int input_number = 0;

    public void iterate_inputs(iter_func callback)
    {
        foreach (var i in inputs_from)
            callback(i);
    }

    //#########//
    // OUTPUTS //
    //#########//

    /// <summary> Should return true if this node can output
    /// to the given <paramref name="other"/> node. Note that
    /// both <see cref="can_output_to(item_node)"/> and
    /// <paramref name="other"/>.<see cref="can_input_from(item_node)"/>
    /// must return true for the connection to be made; so there is
    /// a choice about which of these functions should contain
    /// the more involved connection logic. By convention, heavy
    /// lifting should be done in <see cref="can_input_from(item_node)"/>.</summary>
    protected abstract bool can_output_to(item_node other);

    /// <summary> The item nodes that this node outputs to. </summary>
    List<item_node> outputs_to = new List<item_node>();
    public int output_count => outputs_to.Count;

    /// <summary> The point where items are output from this node. </summary>
    public virtual Vector3 output_point => transform.position;

    public item_node nearest_output => utils.find_to_min(outputs_to,
        (o) => (o.input_point(output_point) - output_point).magnitude);

    /// <summary> Get the next output, in a cyclic fashion. </summary>
    public item_node next_output()
    {
        if (output_count == 0) return null;
        output_number = (output_number + 1) % output_count;
        return outputs_to[output_number];
    }
    int output_number = 0;

    /// <summary> Get the next output in a cyclic fashion, but don't 
    /// increment the <see cref="output_number"/> counter. </summary>
    public item_node peek_next_output()
    {
        if (output_count == 0) return null;
        return outputs_to[(output_number + 1) % output_count];
    }

    /// <summary> Called whenever the outputs change. </summary>
    protected virtual void on_outputs_change() { }

    public void iterate_outputs(iter_func callback)
    {
        foreach (var i in outputs_to)
            callback(i);
    }

    public delegate bool iter_func(item_node node);

    public void iterate_downstream(iter_func f)
    {
        HashSet<item_node> open = new HashSet<item_node> { this };
        HashSet<item_node> closed = new HashSet<item_node>() { };

        while (open.Count > 0)
        {
            item_node current = null;
            foreach (var n in open) { current = n; break; }
            if (current == null) break;

            if (f(current)) break;
            open.Remove(current);
            closed.Add(current);

            foreach (var ds in current.outputs_to)
            {
                if (open.Contains(ds)) continue;
                if (closed.Contains(ds)) continue;
                open.Add(ds);
            }
        }
    }


    //#########//
    // LINKING //
    //#########//

    /// <summary> Called just after my connections have been validated. </summary>
    protected virtual void postprocess_connections(
        List<item_node> outputs_to, List<item_node> inputs_from,
        out HashSet<item_node> outputs_to_remove, out HashSet<item_node> inputs_to_remove)
    {
        outputs_to_remove = new HashSet<item_node>();
        inputs_to_remove = new HashSet<item_node>();
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    protected virtual void Start()
    {
        // Wait a bit to register, sp the physics
        // engine is up-to-date with my geometry.
        Invoke("register_myself", 0.1f);
    }

    void register_myself()
    {
        // Register the node, only after the chunk has finished generating
        chunk.add_generation_listener(transform, (c) => register_node(this));
    }

    protected virtual void OnDestroy()
    {
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

    public static bool display_enabled
    {
        get => _display_enabled;
        set
        {
            foreach (var n in nodes)
                n.set_display(value);
            _display_enabled = value;
        }
    }
    static bool _display_enabled;

    public static void initialize()
    {
        nodes = new HashSet<item_node>();
    }

    public static void refresh_connections()
    {
        foreach (var n in nodes) break_all_connections(n);
        foreach (var n in nodes) validate_connections(n);
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

        from.on_outputs_change();
        to.on_inputs_change();
    }

    /// <summary> Break a connection between two nodes. </summary>
    static protected void break_connection(item_node from, item_node to)
    {
        bool from_changed = from?.outputs_to.Remove(to) ?? false;
        bool to_changed = to?.inputs_from.Remove(from) ?? false;
        if (from_changed) from.on_outputs_change();
        if (to_changed) to.on_inputs_change();
    }

    /// <summary> Remove all input and output nodes 
    /// from <paramref name="node"/>. </summary>
    static void break_all_connections(item_node node)
    {
        foreach (var ip in node.inputs_from)
        {
            ip?.outputs_to.Remove(node);
            ip?.on_outputs_change();
        }

        foreach (var ot in node.outputs_to)
        {
            ot?.inputs_from.Remove(node);
            ot?.on_inputs_change();
        }

        // Inputs/outputs for the given node will only have
        // changed if there were any in the first place
        bool inputs_changed = node.input_count > 0;
        bool outputs_changed = node.output_count > 0;

        node.inputs_from.Clear();
        node.outputs_to.Clear();

        if (inputs_changed) node.on_inputs_change();
        if (outputs_changed) node.on_outputs_change();
    }

    /// <summary> Returns true if the output from <paramref name="from"/>
    /// can be nodeed to the input of <paramref name="to"/>. </summary>
    static bool test_connection(item_node from, item_node to)
    {
        return from.can_output_to(to) && to.can_input_from(from);
    }

    /// <summary> Returns true if the raycast hit given should be ignored
    /// when testing item logisitics paths. </summary>
    protected static bool ignore_logistics_collisions_with(RaycastHit hit, params Transform[] ignore_transforms)
    {
        // Ignore collisions with logistics items
        var itm = hit.transform.GetComponentInParent<item>();
        if (itm != null && itm.is_logistics_version) return true;

        // Ignore collisions with IDontBlockItemLogisitcs objects
        if (hit.transform.GetComponentInParent<IDontBlockItemLogisitcs>() != null) return true;

        foreach (var t in ignore_transforms)
        {
            if (t == null) continue;
            if (t.IsChildOf(hit.transform)) return true;
            if (hit.transform.IsChildOf(t)) return true;
        }

        return false;
    }

    /// <summary> Checks that all of the input/outputs 
    /// for the given node are up-to-date. </summary>
    static void validate_connections(item_node node, bool revalidate_connected = true)
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

        // Revalidate the nodes which are now conencted.
        // Consider the following example with 3 gutters
        // and items flowing left-to-right.
        //
        //  >> items >>
        //  1 ----------|
        //              |      < new connection (1 -> 2)
        //       2 ----------- < new gutter
        //              |      < previous connection (1 -> 3)
        //       3 -----------
        //
        // In the above example, placement of gutter 2 will break
        // the previous connection from 1 to 3. Gutter 1 needs to be
        // re-validated to realise this.
        if (revalidate_connected)
        {
            var to_revalidate = new List<item_node>(node.inputs_from);
            to_revalidate.AddRange(node.outputs_to);

            foreach (var n in to_revalidate)
                validate_connections(n, revalidate_connected: false);
        }

        // Perform connection postprocssing
        node.postprocess_connections(node.outputs_to, node.inputs_from,
            out HashSet<item_node> outputs_to_remove, out HashSet<item_node> inputs_to_remove);
        foreach (var output in outputs_to_remove) break_connection(node, output);
        foreach (var input in inputs_to_remove) break_connection(input, node);

        // Refresh display
        node.refresh_display();
    }

    static void register_node(item_node node)
    {
        // Create the connections to/from the new node
        nodes.Add(node);
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
        nodes.Remove(node);

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
                "    " + (nodes.Contains(node) ? "registered\n" : "unregistered!\n") +
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
