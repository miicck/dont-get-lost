using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class visiting_character : character, ICharacterController, IAddsToInspectionText
{
    static visiting_character()
    {
        help_book.add_entry("towns/visitors",
            () => "Ocassionally, your town will recive visitors."
        );
    }


    public float visiting_time = 60f;

    protected override void Start() => visitors.Add(this);
    protected override void OnDestroy() => visitors.Remove(this);

    float remaining_visiting_time;

    //######################//
    // ICharacterController //
    //######################//

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
    public void on_end_control(character c) { }
    public string inspect_info() => interaction?.task_summary() ?? "Looking for something to do";

    /// <summary> The interactable object that we are currently interacting with. </summary>
    public character_interactable interaction => character_interactable.assigned_to(this);

    wander_task wander => this.add_or_get_component<wander_task>();

    void leave()
    {
        interaction?.unassign();
        delete();
    }

    public void control(character c)
    {
        remaining_visiting_time -= Time.deltaTime;

        if (remaining_visiting_time < 0)
        {
            remaining_visiting_time = 0;
            if (has_authority)
            {
                leave();
                return;
            }
        }

        if (c != this)
        {
            Debug.LogError("Visiting character tried to control another character!");
            return;
        }

        var inter = interaction;
        if (inter == null) wander?.wander(this);
        else inter.interact(this);
    }

    class wander_task : MonoBehaviour
    {
        town_path_element target;
        town_path_element.path path;

        public void wander(character c)
        {
            if (target == null)
            {
                var options = new List<town_path_element>(town_path_element.element_group(c.town_path_element.group));
                if (options.Count == 0) return;
                target = options[Random.Range(0, options.Count)];
                return;
            }

            if (path == null)
            {
                path = town_path_element.path.get(c.town_path_element, target);
                if (path == null) return;
            }

            switch (path.walk(c, c.walk_speed))
            {
                case town_path_element.path.WALK_STATE.UNDERWAY:
                    return;

                default:
                    target = null;
                    path = null;
                    return;
            }
        }
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        return string.Format("Leaving in {0:0.0} seconds", remaining_visiting_time);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<visiting_character> visitors = new HashSet<visiting_character>();
    static float next_spawn_time = 0;

    class approach_controller : attacker_entrypoint.approach_controller
    {
        public approach_controller(
            attacker_entrypoint entrypoint,
            ICharacterController controller_on_arrive) :
            base(entrypoint, controller_on_arrive, should_run: false)
        { }

        public override string inspect_info() => "Entering town.";
    }

    public static void try_spawn_now()
    {
        next_spawn_time = Time.time;
    }

    public static bool try_spawn(attacker_entrypoint entrypoint)
    {
        if (Time.time < next_spawn_time) return false; // Not ready to spawn
        if (!entrypoint.has_authority) return false; // Only spawn on auth client
        if (!entrypoint.path_complete) return false; // Path not ready

        int max_visitors = group_info.max_visitors(entrypoint.element.group);
        if (visitors.Count >= max_visitors) return false; // Too many visitors

        int to_spawn = Random.Range(1, max_visitors - visitors.Count);

        for (int i = 0; i < to_spawn; ++i)
        {
            var vc = client.create(entrypoint.path_end, "characters/wandering_trader") as visiting_character;
            vc.controller = new approach_controller(entrypoint, vc);
            vc.remaining_visiting_time = vc.visiting_time;
        }

        next_spawn_time = Time.time + Random.Range(3 * 60, 5 * 60);
        return true;
    }
}
