using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using networked_variables;

public class teleport_manager : networked
{
    // The list of teleport destinations (saved over the network)
    networked_list<networked_pair<net_string, net_vector3>> destinations =
        new networked_list<networked_pair<net_string, net_vector3>>();

    public void clear_registered_teleporters()
    {
        destinations.clear();
    }

    public void register_portal(portal p)
    {
        var net_name = new net_string();
        net_name.value = p.init_portal_name();
        var net_pos = new net_vector3();
        net_pos.value = p.teleport_location.position;
        destinations.add(new networked_pair<net_string, net_vector3>(net_name, net_pos));
    }

    public void unregister_portal(portal p)
    {
        int to_remove = -1;
        for (int i = 0; i < destinations.length; ++i)
            if ((destinations[i].second.value - p.teleport_location.position).magnitude < 10e-4)
            {
                to_remove = i;
                break;
            }

        if (to_remove < 0) throw new System.Exception("Could not find the portal to unregister!");
        destinations.remove_at(to_remove);
    }

    public string attempt_rename_portal(portal p, string new_name)
    {
        var nearest = utils.find_to_min(destinations,
            (d) => (d.second.value - p.teleport_location.position).magnitude);

        float dis = (nearest.second.value - p.teleport_location.position).magnitude;
        if (dis > 0.1f)
            throw new System.Exception("Portal moved, or not registered?");

        nearest.first.value = new_name;
        destinations.set_dirty();
        return nearest.first.value;
    }

    public string get_portal_name(portal p)
    {
        var nearest = utils.find_to_min(destinations,
            (d) => (d.second.value - p.teleport_location.position).magnitude);

        float dis = (nearest.second.value - p.teleport_location.position).magnitude;
        if (dis > 0.1f)
            throw new System.Exception("Portal moved, or not registered?");
        return nearest.first.value;
    }

    public void create_buttons(RectTransform parent)
    {
        var btn = Resources.Load<UnityEngine.UI.Button>("ui/teleport_button");
        foreach (var d in destinations)
        {
            var b = btn.inst();
            b.GetComponentInChildren<UnityEngine.UI.Text>().text = d.first.value;
            b.transform.SetParent(parent);

            Vector3 target = d.second.value; // Copy for lambda

            b.onClick.AddListener(() =>
            {
                player.current.teleport(target);
            });
        }
    }

    public override float network_radius()
    {
        // The teleport manager is always loaded
        return float.PositiveInfinity;
    }

    public Vector3 nearest_teleport_destination(Vector3 v)
    {
        float min_dis = Mathf.Infinity;
        Vector3 found = v;
        foreach (var d in destinations)
        {
            float dis = (d.second.value - v).magnitude;
            if (dis < min_dis)
            {
                min_dis = dis;
                found = d.second.value;
            }
        }

        return found;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(teleport_manager))]
    new class editor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var tm = (teleport_manager)target;
            string text = "Teleport destinations:\n";
            foreach (var d in tm.destinations)
                text += d.first.value + ": " + d.second.value + "\n";
            UnityEditor.EditorGUILayout.TextArea(text);
        }
    }
#endif
}
