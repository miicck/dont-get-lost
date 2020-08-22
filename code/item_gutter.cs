using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> A simple track for items. This component ensures 
/// that the items roll downhill. It also records/remembers
/// the average item flow, so that it can reproduce it when
/// it's input isn't loaded. </summary>
public class item_gutter : building_material, IInspectable
{
    public const float ITEM_FLOW_TIMESPAN = 60f;
    public const float ITEM_SEPERATION = 0.25f;

    item_link_point input;
    item_link_point output;

    List<item> items = new List<item>();

    Dictionary<string, float> item_flow_float = new Dictionary<string, float>();
    networked_variables.net_string_counts_v2 item_flow;

    Dictionary<string, float> last_mimic_times = new Dictionary<string, float>();
    void mimic_flow()
    {
        if (input.item != null) return; // Can't mimic anything unless input is free

        foreach (var kv in item_flow)
        {
            var itm = Resources.Load<item>("items/" + kv.Key);
            if (itm == null)
            {
                Debug.Log("Could not mimic flow of item " + kv.Key + "!");
                continue;
            }

            // Spawn item at a rate of kv.Value items per ITEM_FLOW_TIMESPAN
            if (!last_mimic_times.TryGetValue(kv.Key, out float last_time)
                || Time.realtimeSinceStartup - last_time > ITEM_FLOW_TIMESPAN / kv.Value)
            {
                last_mimic_times[kv.Key] = Time.realtimeSinceStartup;
                input.item = itm.inst();
                return;
            }
        }
    }

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        item_flow = new networked_variables.net_string_counts_v2();
    }

    public override void on_create()
    {
        base.on_create();
        foreach (var kv in item_flow)
            item_flow_float[kv.Key] = kv.Value;
    }

    void Start()
    {
        item_link_point[] links = GetComponentsInChildren<item_link_point>();
        if (links.Length != 2)
            throw new System.Exception("Item gutters must have exactly 2 link points!");

        // The lower end is the output, the higher end is the input
        if (links[0].position.y < links[1].position.y)
        {
            links[0].type = item_link_point.TYPE.OUTPUT;
            output = links[0];

            links[1].type = item_link_point.TYPE.INPUT;
            input = links[1];
        }
        else
        {
            links[0].type = item_link_point.TYPE.INPUT;
            input = links[0];

            links[1].type = item_link_point.TYPE.OUTPUT;
            output = links[1];
        }

        InvokeRepeating("slow_update", Random.Range(0, 1f), 1f);
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);
        if (!deleted) return;

        // If I've been deleted, reset the flow of anything that I output to
        if (output.linked_to != null)
        {
            var gutter = output.linked_to.GetComponentInParent<item_gutter>();
            if (gutter != null)
            {
                if (gutter.has_authority)
                    gutter.item_flow.clear();
                gutter.item_flow_float.Clear();
            }
        }
    }

    void OnDestroy()
    {
        // Destroy my items along with me
        foreach (var itm in items)
            if (itm != null)
                Destroy(itm.gameObject);
    }

    bool can_accept_input()
    {
        // No items on gutter => free
        if (items.Count == 0) return true;

        // See if there is enough space at the start of the gutter
        Vector3 delta = items[items.Count - 1].transform.position - input.position;
        return delta.magnitude > ITEM_SEPERATION;
    }

    bool can_output()
    {
        // Can't output if output is already occupied
        if (output.item != null) return false;

        // Can assign to output if it is a dead end
        if (output.linked_to == null) return true;

        // Can't assign to output if the output's output
        // isn't free (as it is likely less than ITEM_SEPERATION away)
        if (output.linked_to.item != null) return false;

        // g2g
        return true;
    }

    void slow_update()
    {
        if (has_authority)
        {
            // Update the networked flow with the floored flow
            Dictionary<string, int> floor_flows = new Dictionary<string, int>();
            foreach (var kv in item_flow_float)
            {
                int fflow = Mathf.FloorToInt(kv.Value);
                if (fflow > 0)
                    floor_flows[kv.Key] = fflow;
            }

            item_flow.set(floor_flows);
        }
    }

    private void Update()
    {
        if (item_flow == null) return; // Wait for networked variables

        if (input.linked_to == null && item_flow.item_types > 0)
        {
            // Mimic flow from unloaded items
            mimic_flow();
        }

        if (input.item != null)
        {
            // Take an item from the input
            // and place it on the gutter
            if (can_accept_input())
            {
                var to_add = input.release_item();
                items.Add(to_add);
                to_add.transform.forward = output.position - input.position;

                // Bump the item flow by 1
                if (item_flow_float.ContainsKey(to_add.name))
                    item_flow_float[to_add.name] += 1;
                else item_flow_float[to_add.name] = 1;
            }
        }

        // Asymptotically reduce the flow to 0
        foreach (var k in new List<string>(item_flow_float.Keys))
            item_flow_float[k] -= item_flow_float[k] * Time.deltaTime / ITEM_FLOW_TIMESPAN;

        for (int i = 1; i < items.Count; ++i)
        {
            // Get direction towards next item
            Vector3 delta = items[i - 1].transform.position -
                            items[i].transform.position;

            // Only move towards the next
            // item if we're far enough apart
            if (delta.magnitude > ITEM_SEPERATION)
            {
                // Move up to ITEM_SEPERATION away from the next item
                delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                float max_move = Time.deltaTime;
                if (delta.magnitude > max_move)
                    delta = delta.normalized * max_move;
                items[i].transform.position += delta;
            }
        }

        if (items.Count > 0)
        {
            // Move the item nearest the output
            if (can_output())
            {
                // Move first item towards (unoccupied) output
                if (utils.move_towards(items[0].transform,
                    output.position, Time.deltaTime))
                {
                    output.item = items[0];
                    items.RemoveAt(0);
                }
            }
            else
            {
                // Move up to ITEM_SEPERATION away from (occupied) output
                Vector3 delta = output.position - items[0].transform.position;
                if (delta.magnitude > ITEM_SEPERATION)
                {
                    delta = delta.normalized * (delta.magnitude - ITEM_SEPERATION);
                    float max_move = Time.deltaTime;
                    if (delta.magnitude > max_move)
                        delta = delta.normalized * max_move;
                    items[0].transform.position += delta;
                }
            }
        }
    }

    //##############//
    // IINspectable //
    //##############//

    new public string inspect_info()
    {
        string ret = display_name + "\nItem flow:\n";

        if (item_flow.item_types == 0)
            ret += "No flow.";
        else foreach (var kv in item_flow)
                ret += "    " + kv.Value + " " + kv.Key + "/m\n";

        return ret;
    }

    //###############//
    // CUSTOM EDITOR //
    //###############//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(item_gutter), true)]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            item_gutter g = (item_gutter)target;

            string text = g.inspect_info();
            text += "\nLast mimic times:\n";
            foreach (var kv in g.last_mimic_times)
                text += "    " + kv.Key + ": " + kv.Value + "\n";

            text += "\nFloat flows:\n";
            foreach (var kv in g.item_flow_float)
                text += "    " + kv.Key + ": " + kv.Value + "\n";

            UnityEditor.EditorGUILayout.TextArea(text);
        }
    }
#endif
}
