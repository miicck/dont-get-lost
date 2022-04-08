using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class visiting_character : character, ICharacterController, IAddsToInspectionText
{
    public float visiting_time = 60f;

    protected override void Start() => visitors.Add(this);
    protected override void OnDestroy() => visitors.Remove(this);

    //######################//
    // ICharacterController //
    //######################//

    float remaining_time_alive = 60f;
    town_path_element.path path_to_poi;
    visitor_point_of_interest next_poi;

    enum STATE
    {
        SEARCHING_FOR_POI,
        PATHING_TO_POI,
        WALKING_TO_POI,
        INTERATING_WITH_POI
    }

    STATE state = STATE.SEARCHING_FOR_POI;

    void leave()
    {
        delete();
    }

    public virtual void control(character c)
    {
        if (c != this)
        {
            Debug.LogError("Visiting character controlling another character!");
            return;
        }

        remaining_time_alive -= Time.deltaTime;
        if (remaining_time_alive <= 0)
        {
            leave();
            return;
        }

        if (town_path_element == null)
            town_path_element = town_path_element.nearest_element(transform.position, town_path_element.largest_group());

        switch (state)
        {
            case STATE.SEARCHING_FOR_POI:
                // Look for the next point of interest to visit
                var pois = visitor_point_of_interest.all_in_group(town_path_element.group);

                if (pois.Count > 0 && Random.Range(0, 3) > 0) // Always a chance of just exploring
                    next_poi = pois[Random.Range(0, pois.Count)];
                else
                {
                    var elements = new List<town_path_element>(town_path_element.element_group(town_path_element.group));
                    if (elements.Count == 0)
                    {
                        // I have no elements to explore - just leave
                        leave();
                        return;
                    }

                    var explore = c.add_or_get_component<explore_point_of_interest>();
                    explore.element = elements[Random.Range(0, elements.Count)];
                    next_poi = explore;
                }
                state = STATE.PATHING_TO_POI;
                break;

            case STATE.PATHING_TO_POI:
                // Find a path to the next point of interest
                if (next_poi == null)
                {
                    state = STATE.SEARCHING_FOR_POI;
                    break;
                }

                path_to_poi = town_path_element.path.get(town_path_element, next_poi.target_element);
                state = STATE.WALKING_TO_POI;
                break;

            case STATE.WALKING_TO_POI:
                // Walk to the next point of interest
                if (path_to_poi == null)
                {
                    state = STATE.SEARCHING_FOR_POI;
                    break;
                }

                switch (path_to_poi.walk(c, c.walk_speed))
                {
                    case town_path_element.path.WALK_STATE.UNDERWAY:
                        return; // Still walking

                    case town_path_element.path.WALK_STATE.COMPLETE:
                        state = STATE.INTERATING_WITH_POI;
                        break;

                    default:
                        state = STATE.SEARCHING_FOR_POI;
                        break;
                }

                break;

            case STATE.INTERATING_WITH_POI:
                // Interact with the point of interest
                if (next_poi == null)
                {
                    state = STATE.SEARCHING_FOR_POI;
                    break;
                }

                switch (next_poi.interact(this))
                {
                    case visitor_point_of_interest.INTERACTION_RESULT.UNDERWAY:
                        break;

                    default:
                        state = STATE.SEARCHING_FOR_POI;
                        break;
                }

                break;
        }
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
    public void on_end_control(character c) { }
    public string inspect_info() => display_name;

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        string ret = "";
        switch (state)
        {
            case STATE.SEARCHING_FOR_POI:
                ret += "Looking for something to do.";
                break;
            case STATE.PATHING_TO_POI:
                ret += next_poi?.description() + " (pathing)";
                break;
            case STATE.WALKING_TO_POI:
                ret += next_poi?.description() + " (walking)";
                break;
            case STATE.INTERATING_WITH_POI:
                ret += next_poi?.description();
                break;
        }
        ret += "\n" + string.Format("Leaving in {0:0.0} seconds", remaining_time_alive);
        return ret;
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

    public static bool try_spawn(attacker_entrypoint entrypoint)
    {
        if (visitors.Count > 0) return false; // Too many visitors
        if (Time.time < next_spawn_time) return false; // Not ready to spawn
        if (!entrypoint.has_authority) return false; // Only spawn on auth client
        if (!entrypoint.path_complete) return false; // Path not ready

        var vc = client.create(entrypoint.path_end, "characters/wandering_trader") as visiting_character;
        vc.controller = new approach_controller(entrypoint, vc);
        vc.remaining_time_alive = vc.visiting_time;
        next_spawn_time = Time.time + Random.Range(3 * 60, 5 * 60);
        return true;
    }
}
