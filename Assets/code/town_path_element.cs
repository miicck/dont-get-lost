using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> An object that can be connected to other objects of 
/// the same kind via <see cref="town_path_link"/>s. </summary>
public class town_path_element : MonoBehaviour, IAddsToInspectionText
{
    public settler_interactable interactable => GetComponentInParent<settler_interactable>();

    public virtual void on_character_enter(character c) { }
    public virtual void on_character_move_towards(character c) { }
    public virtual void on_character_leave(character c) { }
    public virtual bool seperates_rooms() { return false; }
    public virtual settler_animations.animation settler_animation(settler s) { return null; }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    bool registered = false;
    protected virtual void Start()
    {
        // Don't register this path element if we are 
        // part of an unplaced building material
        var bm = GetComponentInParent<building_material>();
        if (bm != null && (bm.is_equpped || bm.is_blueprint))
        {
            if (bm.is_blueprint)
                foreach (var l in links)
                    l.update_display();
            return;
        }

        // Register this element, if neccassary
        registered = true;
        register_element(this);
    }

    private void OnDestroy()
    {
        // Unregister this element, if neccassary
        if (registered)
            forget_element(this, true);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var l in links)
            foreach (var lt in l)
            {
                Gizmos.DrawLine(transform.position, l.transform.position);
                Gizmos.DrawLine(l.transform.position, lt.transform.position);
            }
    }

    //#########//
    // LINKING //
    //#########//

    public town_path_link[] links
    {
        get
        {
            if (_links == null)
                _links = GetComponentsInChildren<town_path_link>();
            return _links;
        }
    }
    town_path_link[] _links;

    public List<town_path_element> linked_elements()
    {
        List<town_path_element> ret = new List<town_path_element>();
        foreach (var l in links)
            foreach (var lt in l)
                ret.Add(lt.path_element);
        return ret;
    }

    public List<town_path_element> linked_elements_in_same_room()
    {
        List<town_path_element> ret = new List<town_path_element>();
        foreach (var l in links)
            foreach (var lt in l)
                if (lt.path_element.room == room)
                    ret.Add(lt.path_element);
        return ret;
    }

    public int group
    {
        get => _group;
        private set
        {
            if (_group == value)
                return; // No change

            _group = value;
            on_group_change();
        }
    }
    int _group;

    public delegate void group_update_func();
    group_update_func on_group_change = () => { };
    public void add_group_change_listener(group_update_func f) { on_group_change += f; }

    public int room
    {
        get => _room;
        private set
        {
            if (_room == value)
                return; // No change

            _room = value;
            on_room_change();
        }
    }
    int _room;

    public delegate void room_update_func();
    room_update_func on_room_change = () => { };
    public void add_room_change_listener(room_update_func f) { on_room_change += f; }

    public string added_inspection_text()
    {
        return "Group " + group + " room " + room;
    }

    void try_link(town_path_element other)
    {
        // Can't link to self
        if (other == this) return;

        foreach (var l in links)
            foreach (var l2 in other.links)
                if (town_path_link.can_link(l, l2))
                    l.link_to(l2);
    }

    void break_links(bool destroying = false)
    {
        foreach (var l in links)
            l.break_links(destroying);
    }

    float heuristic(town_path_element other)
    {
        return (transform.position - other.transform.position).magnitude;
    }

    public delegate bool connected_iter_function(town_path_element e);

    public void iterate_connected(connected_iter_function f)
    {
        HashSet<town_path_element> open = new HashSet<town_path_element> { this };
        HashSet<town_path_element> closed = new HashSet<town_path_element> { };

        while (open.Count > 0)
        {
            town_path_element current = null;
            foreach (var s in open) { current = s; break; }
            if (current == null) break;

            if (f(current)) return;
            closed.Add(current);
            open.Remove(current);

            foreach (var n in current.linked_elements())
            {
                if (open.Contains(n)) continue;
                if (closed.Contains(n)) continue;
                open.Add(n);
            }
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<town_path_element> all_elements;
    static Dictionary<int, HashSet<town_path_element>> grouped_elements;
    static Dictionary<int, HashSet<town_path_element>> roomed_elements;

    public static town_path_element nearest_element(Vector3 v, int group = -1)
    {
        if (group < 0) return utils.find_to_min(all_elements, (e) => (e.transform.position - v).sqrMagnitude);
        else return utils.find_to_min(all_elements, (e) => (e.transform.position - v).sqrMagnitude, (e) => e.group == group);
    }

    public static HashSet<town_path_element> element_group(int group)
    {
        if (grouped_elements.TryGetValue(group, out HashSet<town_path_element> elms))
            return elms;
        return new HashSet<town_path_element>();
    }

    public static HashSet<town_path_element> elements_in_room(int room)
    {
        if (roomed_elements.TryGetValue(room, out HashSet<town_path_element> elms))
            return elms;
        return new HashSet<town_path_element>();
    }

    public static town_path_element find_nearest(Vector3 position)
    {
        return utils.find_to_min(all_elements,
            (r) => (r.transform.position - position).sqrMagnitude);
    }

    public static void initialize()
    {
        // Initialize theelements collection
        all_elements = new HashSet<town_path_element>();
        grouped_elements = new Dictionary<int, HashSet<town_path_element>>();
        roomed_elements = new Dictionary<int, HashSet<town_path_element>>();
    }

    static void evaluate_groups_and_rooms()
    {
        evaluate_groups();
        evaluate_rooms();
    }

    static void evaluate_groups()
    {
        int group = 0;
        HashSet<town_path_element> ungrouped = new HashSet<town_path_element>(all_elements);

        while (ungrouped.Count > 0)
        {
            // Create an open set from the first ungrouped element
            HashSet<town_path_element> open = new HashSet<town_path_element> { ungrouped.First() };
            HashSet<town_path_element> closed = new HashSet<town_path_element>();

            while (open.Count > 0)
            {
                // Get the first open element
                var to_expand = open.First();
                if (to_expand == null) break;
                open.Remove(to_expand);
                closed.Add(to_expand);

                // Add all linked elements to the open set 
                // (if they arent in the closed set)
                foreach (var n in to_expand.linked_elements())
                    if (!closed.Contains(n))
                        open.Add(n);
            }

            grouped_elements[group] = closed;
            foreach (var e in closed)
            {
                ungrouped.Remove(e);
                e.group = group;
            }
            ++group;
        }
    }

    static void evaluate_rooms()
    {
        int room = 0;
        HashSet<town_path_element> unroomed = new HashSet<town_path_element>(all_elements);

        while (unroomed.Count > 0)
        {
            // Create an open set from the first unroomed element
            HashSet<town_path_element> open = new HashSet<town_path_element> { unroomed.First() };
            HashSet<town_path_element> closed = new HashSet<town_path_element>();

            while (open.Count > 0)
            {
                // Get the first open element
                var to_expand = open.First();
                if (to_expand == null) break;
                closed.Add(to_expand);
                open.Remove(to_expand);

                // Don't expand room searches from path elements
                // that seperate rooms
                if (to_expand.seperates_rooms())
                    continue;

                // Add all linked elements to the open set 
                // (if they arent in the closed set)
                foreach (var n in to_expand.linked_elements())
                    if (!closed.Contains(n))
                        open.Add(n);
            }

            roomed_elements[room] = closed;
            foreach (var e in closed)
            {
                unroomed.Remove(e);
                e.room = room;
            }
            ++room;
        }
    }

    static void validate_links(town_path_element r)
    {
        // Re-make all links to/from r
        r.break_links();
        foreach (var r2 in all_elements)
            r.try_link(r2);

        evaluate_groups_and_rooms();
    }

    static void register_element(town_path_element r)
    {
        // Create links to/from r, add r to the collection of elements.
        if (!all_elements.Add(r))
            throw new System.Exception("Tried to register element twice!");

        validate_links(r);
    }

    static void forget_element(town_path_element r, bool destroying = false)
    {
        // Forget all the links to/from r, remove r from the collection of elements
        if (!all_elements.Remove(r))
            throw new System.Exception("Tried to forget unregistered element!");

        r.break_links(destroying);
        evaluate_groups_and_rooms();
    }

    /// <summary> Set to true to draw objects representing 
    /// links between path elements. </summary>
    public static bool draw_links
    {
        get => _draw_links;
        set
        {
            _draw_links = value;
            foreach (var e in all_elements)
                foreach (var l in e.links)
                    l.display_enabled = value;
        }
    }
    static bool _draw_links;

    //#############//
    // PATHFINDING //
    //#############//

    public class path
    {
        public const float ARRIVE_DISTANCE = 0.25f;

        List<town_path_element> element_path;
        public int index { get; private set; }

        public town_path_element this[int i]
        {
            get
            {
                if (i < 0) return null;
                if (element_path == null) return null;
                if (i >= element_path.Count) return null;
                return element_path[i];
            }
        }

        public int count => element_path == null ? 0 : element_path.Count;

        /// <summary> Find a path between the start and end elements, using 
        /// the A* algorithm. Returns null if no such path exists. </summary>
        public static path get(town_path_element start, town_path_element goal)
        {
            if (start == null || goal == null) return null;
            if (start.group != goal.group) return null;

            // Setup pathfinding state
            var open_set = new HashSet<town_path_element>();
            var closed_set = new HashSet<town_path_element>();
            var came_from = new Dictionary<town_path_element, town_path_element>();
            var fscore = new Dictionary<town_path_element, float>();
            var gscore = new Dictionary<town_path_element, float>();

            // Initialize pathfinding with just start open
            open_set.Add(start);
            gscore[start] = 0;
            fscore[start] = goal.heuristic(start);

            while (open_set.Count > 0)
            {
                // Find the lowest fscore in the open set
                var current = utils.find_to_min(open_set, (c) => fscore[c]);

                if (current == goal)
                {
                    // Success - reconstruct path
                    var p = new path();
                    p.element_path = new List<town_path_element> { current };
                    while (came_from.TryGetValue(current, out current))
                        p.element_path.Add(current);
                    p.element_path.Reverse();
                    return p;
                }

                // Close current
                open_set.Remove(current);
                closed_set.Add(current);

                foreach (var n in current.linked_elements())
                {
                    if (closed_set.Contains(n))
                        continue;

                    // Work out tentative path length to n, if we wen't via current
                    var tgs = gscore[current] + n.heuristic(current);

                    // Get the current neighbour gscore (infinity if not already scored)
                    if (!gscore.TryGetValue(n, out float gsn))
                        gsn = Mathf.Infinity;

                    if (tgs < gsn)
                    {
                        // This is a better path to n, update it
                        came_from[n] = current;
                        gscore[n] = tgs;
                        fscore[n] = tgs + goal.heuristic(n);
                        open_set.Add(n);
                    }
                }
            }

            // Pathfinding failed
            return null;
        }

        public static path get(Vector3 v, town_path_element goal) => get(nearest_element(v), goal);

        // Private constructor, paths should be created with the get method
        private path() { }

        public enum WALK_STATE
        {
            COMPLETE,
            UNDERWAY,
            FAILED
        }

        public interface ITownWalker
        {
            public void on_walk_towards(town_path_element element);
            public void on_end_walk();
            public Transform transform { get; }
        }

        public WALK_STATE walk(ITownWalker walking, float speed, bool forwards = true)
        {
            // There is no path/thing walking
            if (walking == null) return WALK_STATE.FAILED;
            if (count == 0)
            {
                walking.on_end_walk();
                return WALK_STATE.FAILED;
            }

            if (index >= element_path.Count) // Walked off the end of the path
            {
                if (forwards)
                {
                    walking.on_end_walk();
                    return WALK_STATE.COMPLETE;
                }
                else index = element_path.Count - 1;
            }
            else if (index < 0) // Walked off the start of the path
            {
                if (forwards) index = 0;
                else
                {
                    walking.on_end_walk();
                    return WALK_STATE.COMPLETE;
                }
            }

            // Walk the path to completion
            var next_element = this[index];
            if (next_element == null)
            {
                // Path has been destroyed
                walking.on_end_walk();
                return WALK_STATE.FAILED;
            }

            run_animations(walking, forwards);
            walking.on_walk_towards(next_element);

            if (utils.move_towards(walking.transform, next_element.transform.position,
                Time.deltaTime * speed, arrive_distance: ARRIVE_DISTANCE))
            {
                if (forwards) ++index;
                else --index;
            }

            return WALK_STATE.UNDERWAY;
        }

        settler_animations.animation settler_animation;

        void run_animations(ITownWalker walking, bool forwards)
        {
            if (walking is settler)
            {
                // Select the animation based on current element first, then on next element
                var next_anim = this[index - 1]?.settler_animation((settler)walking);
                if (next_anim == null)
                    next_anim = this[index]?.settler_animation((settler)walking);

                // Animation changed, switch to the new one
                if (settler_animation?.GetType() != next_anim?.GetType())
                    settler_animation = next_anim;

                if (settler_animation != null)
                {
                    settler_animation.play();
                    return;
                }
            }

            default_animation(walking, forwards);
        }

        void default_animation(ITownWalker walking, bool forwards)
        {
            var next_element = this[index];
            Vector3 next_point = next_element.transform.position;
            Vector3 forward = (next_point - walking.transform.position).normalized;

            var next_next_element = this[index + (forwards ? 1 : -1)];
            if (next_next_element != null)
            {
                // Gradually turn towards the next direction, to make
                // going round sharp corners look natural
                Vector3 next_next_point = next_next_element.transform.position;
                Vector3 next_forward = (next_next_point - next_point).normalized;

                // Weight forward vector by proximity
                float w1 = 1f / (walking.transform.position - next_point).magnitude;
                float w2 = 1f / (walking.transform.position - next_next_point).magnitude;
                forward = forward * w1 + next_forward * w2;
                forward /= (w1 + w2);
            }

            // Only face towards the forward direction if we have a reasonably
            // large component in the not-y directions, so that if we're climbing
            // ladders directly up/down this code does not trigger
            forward.y = 0;
            if (forward.magnitude > 0.1f)
            {
                // If we need to do > 90 degree turn, just do it instantly
                if (Vector3.Dot(walking.transform.forward, forward) < 0)
                    walking.transform.forward = forward;
                else // Otherwise, lerp our forward vector
                    walking.transform.forward = Vector3.Lerp(walking.transform.forward, forward, Time.deltaTime * 5f);
            }
        }

        public void draw_gizmos(Color color)
        {
            Gizmos.color = color;
            for (int i = 1; i < count; ++i)
                Gizmos.DrawLine(this[i].transform.position, this[i - 1].transform.position);
        }
    }
}