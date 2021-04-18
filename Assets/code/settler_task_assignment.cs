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
    public settler_interactable interactable
    {
        get
        {
            // Standalone assignment
            var stand_alone = GetComponent<settler_interactable>(); 
            if (stand_alone != null) return stand_alone;

            // Assign to parent
            return networked_parent.GetComponentInChildren<settler_interactable>();
        }
    }

    private void Update()
    {
        // Only authority client performs checks
        if (!has_authority) return;

        // If the settler or task I'm assigning doesn't exist 
        // on this client => get rid of the assignment
        if (settler == null || interactable == null)
        {
            delete();
            return;
        }

        // See if there is an assignment with my id
        if (assignments_by_id.TryGetValue(settler_id.value, out settler_task_assignment assignment) &&
            assignment != null)
        {
            if (assignment == this)
                return; // Everything is fine, I'm in the assignment dictionary correctly
            else
            {
                // Another assignment is in the assignment dictionary =>
                // I've been superseded
                delete();
                return;
            }
        }

        // There wasn't a (non-null) value in the assignments_by_id dictionary for my settler_id
        // Register the assignment to me
        assignments_by_id[settler_id.value] = this;
        switch (interactable.on_assign(settler))
        {
            case settler_interactable.INTERACTION_RESULT.FAILED:
            case settler_interactable.INTERACTION_RESULT.COMPLETE:
                delete();
                break;
        }
    }

    public override void on_forget(bool deleted)
    {
        // Remove me from the assignements by id dictionary, if I'm in it
        interactable.on_unassign(settler);
        if (assignments_by_id.TryGetValue(settler_id.value, out settler_task_assignment assignment))
        {
            if (assignment == this)
                assignments_by_id.Remove(settler_id.value);
            else
                return;
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

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
        // Stop all tasks that should be stopped when an attack starts
        var copy = new Dictionary<int, settler_task_assignment>(assignments_by_id);
        foreach (var kv in copy)
            if (!kv.Value.interactable.skill.possible_when_under_attack)
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

        // Can't do this task type when under attack
        if (town_gate.group_under_attack(s.group) && !i.skill.possible_when_under_attack)
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

    public static void assign_idle(settler s)
    {
        // Create an idle wander assignment
        var assignment = (settler_task_assignment)client.create(
            s.transform.position + Random.insideUnitSphere * 5, "misc/wander_task_assignment");
        assignment.settler_id.value = s.network_id;
    }

    public static void initialize()
    {
        assignments_by_id = new Dictionary<int, settler_task_assignment>();
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
            if (!Application.isPlaying) return;

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
