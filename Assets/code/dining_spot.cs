using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dining_spot : character_walk_to_interactable, IAddsToInspectionText
{
    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        return base.added_inspection_text() + "\n" +
            "Dining spot (connected dispensers : " + food_dispensers.Count + ")";
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    protected override void Start()
    {
        base.Start();

        town_path_element.add_on_rooms_update_listener(() =>
        {
            if (this == null) return;

            // Identify connected food dispensers
            food_dispensers.Clear();
            var spot = path_element();
            spot.iterate_connected((e) =>
            {
                var fd = e.interactable as food_dipsenser;
                if (fd != null) food_dispensers.Add(fd);
                return false;
            }, same_room: true);

            // Record room :-> dining spots map
            room_dining_spots.access_or_set(spot.room, () => new List<dining_spot>()).Add(this);
        });
    }

    //##############################//
    // walk_to_settler_interactable //
    //##############################//

    List<food_dipsenser> food_dispensers = new List<food_dipsenser>();
    List<item> foods = new List<item>();
    int index = 0;
    town_path_element.path path;
    float eating_timer = 0;
    settler_animations.simple_work work_anim;

    void reset()
    {
        // Reset stuff
        index = 0;
        path = null;
        eating_timer = 0;
        work_anim = null;

        foreach (var f in foods)
            if (f != null)
                Destroy(f.gameObject);

        foods.Clear();
    }

    public bool food_available()
    {
        foreach (var fd in food_dispensers)
            if (fd.food_available) return true;
        return false;
    }

    protected override bool ready_to_assign(character c)
    {
        if (!food_available()) return false;
        if (c is settler && !(c as settler).ready_to_eat()) return false;
        return true;
    }

    protected override void on_arrive(character c) => reset();
    protected override void on_unassign(character c) => reset();
    public override string task_summary() => "Eating";

    STAGE_RESULT gather_foods(character c)
    {
        if (path == null)
        {
            if (index < food_dispensers.Count)
            {
                // Path to the next food dispenser
                var goal = food_dispensers[index]?.path_element(c.group);
                path = town_path_element.path.get(c.town_path_element, goal);
                if (path == null) return STAGE_RESULT.TASK_FAILED;
            }
            else
            {
                // Path back to the dining spot
                var goal = this?.path_element(c.group);
                path = town_path_element.path.get(c.town_path_element, goal);
                if (path == null) return STAGE_RESULT.TASK_FAILED;
            }
        }

        switch (path.walk(c, c.walk_speed))
        {
            // Arrived at destination
            case town_path_element.path.WALK_STATE.COMPLETE:
                path = null;

                if (index < food_dispensers.Count)
                {
                    // Pickup some food
                    var food = food_dispensers[index]?.item_dispenser?.dispense_first_item();
                    if (food != null)
                    {
                        foods.Add(food);

                        Transform food_parent = null;
                        if (c is settler)
                        {
                            var s = (settler)c;
                            food_parent = index % 2 == 0 ? s.right_hand.transform : s.left_hand.transform;
                        }
                        else food_parent = c.transform;

                        food.transform.SetParent(food_parent);
                        food.transform.localPosition = Vector3.zero;
                    }

                    // Move to next thing
                    ++index;
                    return STAGE_RESULT.STAGE_UNDERWAY;
                }

                // Getting food stage is complete and we're back at the spot
                else return STAGE_RESULT.STAGE_COMPLETE;

            // Continue pathing
            case town_path_element.path.WALK_STATE.UNDERWAY:
                return STAGE_RESULT.STAGE_UNDERWAY;

            // Pathing failed => task failed
            default:
                return STAGE_RESULT.TASK_FAILED;
        }
    }

    STAGE_RESULT arrive_at_spot(character c)
    {
        c.transform.position = transform.position;
        c.transform.forward = transform.forward;

        if (c is settler)
        {
            var s = (settler)c;

            work_anim = new settler_animations.simple_work(s);

            var chair = GetComponentInChildren<chair>();
            if (chair == null)
                s.add_mood_effect("ate_without_chair");
        }

        return STAGE_RESULT.STAGE_COMPLETE;
    }

    STAGE_RESULT eat_foods(character c)
    {
        work_anim?.play();

        eating_timer += Time.deltaTime;
        if (eating_timer < foods.Count * 2)
            return STAGE_RESULT.STAGE_UNDERWAY;

        foreach (var f in foods)
        {
            (c as settler)?.consume_food(f.food_values);
            Destroy(f.gameObject);
        }
        foods.Clear();

        return STAGE_RESULT.TASK_COMPLETE;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        // Don't do anything on non-auth clients
        if (!c.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        switch (stage)
        {
            case 0: return gather_foods(c);
            case 1: return arrive_at_spot(c);
            default: return eat_foods(c);
        }
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        path?.draw_gizmos(color: Color.cyan);
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static Dictionary<int, List<dining_spot>> room_dining_spots = new Dictionary<int, List<dining_spot>>();

    static dining_spot()
    {
        help_book.add_entry("towns/Dining spots",
            "A room with a dining spot (e.g a dining table with chairs) allows you " +
            "to control the diet of settlers. Settlers interacting with a given dining " +
            "spot will construct a meal consisting of one food item from each food dispenser " +
            "(e.g, a pantry) in the same room."
         );

        town_path_element.add_on_rooms_update_listener(() =>
        {
            room_dining_spots.Clear();
        });
    }

    public static bool is_dining_room(int room)
    {
        if (room_dining_spots.TryGetValue(room, out List<dining_spot> spots))
            foreach (var s in spots)
                if (s != null)
                    return true;
        return false;
    }
}
