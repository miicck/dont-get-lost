using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Created as a child of <see cref="settler_interactable.networked_parent"/> 
/// to represent an assignment of the settler with network id 
/// <see cref="settler_task_assignment.settler_id"/> to the
/// <see cref="settler_interactable"/>. </summary>
public class settler_task_assignment : networked, IAddsToInspectionText
{
    //############//
    // NETWORKING //
    //############//

    /// <summary> The network id of the settler that this assignement refers to. </summary>
    networked_variables.net_int settler_id;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        settler_id = new networked_variables.net_int();
    }

    public override bool persistant() { return false; }

    //#####################//
    // Settler interaction //
    //#####################//

    public string added_inspection_text()
    {
        if (settler == null) return "";
        return settler.name.capitalize() + " is assigned to this.";
    }

    /// <summary> The settler that this assignement refers to. </summary>
    public settler settler
    {
        get
        {
            // Get the settler with network id = settler_id.value
            var nw = try_find_by_id(settler_id.value);
            if (nw != null && nw is settler) return (settler)nw;
            return null;
        }
    }

    /// <summary> The interaction that the settler is assigned to. </summary>
    public settler_interactable interactable => networked_parent.GetComponentInChildren<settler_interactable>();

    // Keep track of what assignments are present on this client
    private void Start() { register_assignment(this); }
    private void OnDestroy() { forget_assignment(this); }

    private void Update()
    {
        // Only authority client performs checks
        if (!has_authority) return;

        // If the settler or task I'm assigning doesn't exist 
        // on this client => get rid of the assignment
        if (settler == null || interactable == null)
            delete();
    }

    private void OnDrawGizmos()
    {
        var s = settler;
        if (s == null) return;
        var i = interactable;
        if (i == null) return;

        Gizmos.color = has_authority ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.05f);

        Gizmos.color = s.has_authority ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.05f);
        Gizmos.DrawLine(s.transform.position, transform.position);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<int, settler_task_assignment> assignments_by_id;

    public static void on_attack_begin()
    {
        // Delete all non-guard tasks, so that settlers man 
        // guard positions as quickly as possible
        var copy = new Dictionary<int, settler_task_assignment>(assignments_by_id);
        foreach (var kv in copy)
            if (kv.Value.interactable.type != settler_interactable.TYPE.GUARD)
                kv.Value.delete();
    }

    public static settler_task_assignment current_assignment(settler s)
    {
        if (assignments_by_id.TryGetValue(s.network_id, out settler_task_assignment a))
            return a;
        return null;
    }

    public static bool try_assign(settler s, settler_interactable i)
    {
        // Ensure everything we need exists, including a registered network
        // parent for the interactable i
        if (s == null || i == null || i.networked_parent == null || i.networked_parent.network_id < 0)
            return false;

        if (!i.ready_to_assign(s)) return false; // Not ready to assign

        if (i.assignments.Length >= i.max_simultaneous_users)
            return false; // Too many users are already using i

        // Create an assignment as a child of i.networked_parent
        var assignment = (settler_task_assignment)client.create(
            i.transform.position, "misc/task_assignment", parent: i.networked_parent);

        // Set the assignment settler id
        assignment.settler_id.value = s.network_id;
        return true;
    }

    public static void initialize()
    {
        assignments_by_id = new Dictionary<int, settler_task_assignment>();
    }

    static void register_assignment(settler_task_assignment a)
    {
        // Record this assignment
        assignments_by_id[a.settler_id.value] = a;
        if (a.settler != null)
        {
            switch (a.interactable.on_assign(a.settler))
            {
                case settler_interactable.INTERACTION_RESULT.FAILED:
                case settler_interactable.INTERACTION_RESULT.COMPLETE:
                    a.delete();
                    break;
            }
        }
    }

    static void forget_assignment(settler_task_assignment a)
    {
        // Forget this assignment
        assignments_by_id.Remove(a.settler_id.value);
        if (a.settler != null)
            a.interactable.on_unassign(a.settler);
    }

    public static string info()
    {
        string ret = "Assignments:\n";
        foreach (var kv in assignments_by_id)
            ret += "    " + kv.Value.settler?.name + " -> " + kv.Value.interactable?.name + "\n";
        return ret;
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(settler_task_assignment))]
    class sta_editor : editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var ta = (settler_task_assignment)target;
            var settler = ta.settler;
            var task = ta.interactable;

            string settler_name = settler == null ? "No settler found!" : settler.name;
            string task_name = task == null ? "No task!" : task.name;

            UnityEditor.EditorGUILayout.TextArea(
                "Settler with network id: " + ta.settler_id.value + " (" + settler_name + ")\n" +
                "Assigned to task: " + task_name
            );
        }
    }
#endif
}
