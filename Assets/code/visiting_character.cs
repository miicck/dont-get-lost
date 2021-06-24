using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class visiting_character : character, ICharacterController, IAddsToInspectionText
{
    public float visiting_time = 60f;

    protected override void Start()
    {
        visitors.Add(this);
    }

    protected override void OnDestroy()
    {
        visitors.Remove(this);
    }

    //######################//
    // ICharacterController //
    //######################//

    float remaining_time_alive = 60f;
    town_path_element next_element;

    bool control_or_delete(character c)
    {
        if (town_path_element == null)
            town_path_element = town_path_element.nearest_element(transform.position, group_info.largest_group());

        if (next_element == null)
        {
            var linked = town_path_element.linked_elements();
            if (linked.Count == 0) return false;

            if (Random.Range(0, 3) == 0)
                next_element = linked[Random.Range(0, linked.Count)];
            else
                next_element = utils.find_to_min(linked, (e) =>
                    -Vector3.Dot(e.transform.position - transform.position, transform.forward));
        }

        if (next_element == null) return false;

        if (utils.move_towards_and_look(transform, next_element.transform.position, Time.deltaTime * c.walk_speed))
        {
            town_path_element = next_element;
            next_element = null;
        }

        remaining_time_alive -= Time.deltaTime;
        if (remaining_time_alive <= 0) return false;

        return true;
    }

    public void control(character c)
    {
        if (!control_or_delete(c))
            c.delete();
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
    public void on_end_control(character c) { }
    public string inspect_info() => display_name;

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text() => string.Format("Leaving in {0:0.0} seconds", remaining_time_alive);

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<visiting_character> visitors = new HashSet<visiting_character>();
    static float next_spawn_time = 0;

    public static bool try_spawn(attacker_entrypoint entrypoint)
    {
        if (visitors.Count > 0) return false; // Too mnay visitors
        if (Time.time < next_spawn_time) return false; // Not ready to spawn
        if (!entrypoint.has_authority) return false; // Only spawn on auth client
        if (!entrypoint.path_complete) return false; // Path not ready

        var vc = client.create(entrypoint.path_end, "characters/wandering_trader") as visiting_character;
        vc.controller = new attacker_entrypoint.approach_controller(entrypoint, vc, should_run: false);
        vc.remaining_time_alive = vc.visiting_time;
        next_spawn_time = Time.time + Random.Range(3 * 60, 5 * 60);
        return true;
    }
}
