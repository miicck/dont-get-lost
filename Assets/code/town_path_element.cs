using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary> An object that can be connected to other objects of 
/// the same kind via <see cref="town_path_link"/>s. </summary>
public class town_path_element : MonoBehaviour, IAddsToInspectionText, INonLogistical
{
    public character_interactable interactable => this == null ? null : GetComponentInParent<character_interactable>();

    public virtual void on_character_enter(character c) { }
    public virtual void on_character_move_towards(character c) { }
    public virtual void on_character_leave(character c) { }
    public virtual bool seperates_rooms() => false;
    public virtual settler_animations.animation settler_animation(settler s) => null;

    public float speed_multiplier
    {
        get
        {
            float mult = 1;
            foreach (var msb in GetComponentsInChildren<movement_speed_boost>())
                mult *= msb.speed_multiplier;
            return mult;
        }
    }

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
                    l.update_display(); // Ensure link display is shown for blueprints
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

    void try_link(town_path_element other)
    {
        // Can't link to self
        if (other == this) return;

        // No chance of a link
        if (!other.linkable_region.Intersects(linkable_region)) return;

        foreach (var l in links)
            foreach (var l2 in other.links)
                l.try_link(l2);
    }

    void break_links()
    {
        foreach (var l in links)
            l.break_links();
    }

    public Bounds linkable_region
    {
        get
        {
            if (_linkable_region == default)
                _linkable_region = this.bounds_by_type<town_path_link>(l => l.linkable_region());
            return _linkable_region;
        }
    }
    Bounds _linkable_region;

    public List<town_path_element> linked_elements()
    {
        List<town_path_element> ret = new List<town_path_element>();
        foreach (var l in links)
            foreach (var lt in l)
                if (lt?.path_element != null)
                    ret.Add(lt.path_element);
        return ret;
    }

    public List<town_path_element> linked_elements_in_same_room()
    {
        List<town_path_element> ret = new List<town_path_element>();
        foreach (var l in links)
            foreach (var lt in l)
                if (lt?.path_element != null && lt.path_element.room == room)
                    ret.Add(lt.path_element);
        return ret;
    }

    public delegate bool connected_iter_function(town_path_element e);
    public void iterate_connected(connected_iter_function f, bool same_room = false)
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

            foreach (var n in same_room ?
                current.linked_elements_in_same_room() :
                current.linked_elements())
            {
                if (open.Contains(n)) continue;
                if (closed.Contains(n)) continue;
                open.Add(n);
            }
        }
    }

    //##################//
    // GROUPS AND ROOMS //
    //##################//

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
    public void add_room_change_listener(room_update_func f) => on_room_change += f;

    public bool is_extremety { get; private set; } = false;
    public Vector3 out_of_town_direction { get; private set; }
    town_bounds bounded_group;

    public string added_inspection_text() => "Group " + group + " room " + room;

    //##############//
    // STATIC STUFF //
    //##############//

    public static void initialize()
    {
        // Initialize the elements collection
        all_elements = new HashSet<town_path_element>();
        grouped_elements = new Dictionary<int, HashSet<town_path_element>>();
        roomed_elements = new Dictionary<int, HashSet<town_path_element>>();
    }

    static float last_room_update_time = 0;
    const float MIN_ROOM_UPDATE_INTERVAL = 1f;

    public static void static_update()
    {
        if (rooms_update_required)
        {
            if (Time.time < last_room_update_time + MIN_ROOM_UPDATE_INTERVAL)
                return; // Too close to last room update
            last_room_update_time = Time.time;

            evaluate_groups();
            evaluate_rooms();
            evaluate_connectivity();
            rooms_update_required = false;
        }
    }

    //##################//
    // ROOMS AND GROUPS //
    //##################//

    const float IN_TOWN_RANGE = 1;
    const float IN_TOWN_RESOLUTION = 10;

    static Dictionary<int, HashSet<town_path_element>> grouped_elements;
    static Dictionary<int, HashSet<town_path_element>> roomed_elements;
    static Dictionary<int, List<town_bounds>> group_bounds;
    static HashSet<int> groups_with_beds;

    public delegate void listener();
    static listener on_groups_update_listener;
    static listener on_rooms_update_listener;

    public static void add_on_groups_update_listener(listener l) => on_groups_update_listener += l;
    public static void add_on_rooms_update_listener(listener l) => on_rooms_update_listener += l;

    class town_bounds : IEnumerable<town_path_element>
    {
        public Bounds bounds { get; private set; }

        HashSet<town_path_element> elements;
        public IEnumerator<town_path_element> GetEnumerator() => elements.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public town_bounds(Bounds bounds, IEnumerable<town_path_element> elements)
        {
            this.bounds = bounds;
            this.elements = new HashSet<town_path_element>(elements);
        }

        // Attempt to combine two bounds
        public bool combine_with(town_bounds other)
        {
            var a = bounds;
            var b = other.bounds;

            // They must intersect to be combined
            if (!a.Intersects(b)) return false;

            Vector3 min = Vector3.Min(a.min, b.min);
            Vector3 max = Vector3.Max(a.max, b.max);
            Vector3 size = max - min;

            // The resulting size would be too large
            if (Mathf.Max(size.x, size.y, size.z) > IN_TOWN_RESOLUTION)
                return false;

            // Combine with other, by increasing bounds to 
            // cover both and adding all the elements in other
            // to this
            bounds = new Bounds((min + max) / 2, size);
            foreach (var e in other.elements)
                elements.Add(e);

            return true;
        }

        public bool remove(town_path_element e) => elements.Remove(e);
    }

    static bool rooms_update_required = false;

    static void evaluate_groups()
    {
        int group = 0;
        HashSet<town_path_element> ungrouped = new HashSet<town_path_element>(all_elements);
        grouped_elements = new Dictionary<int, HashSet<town_path_element>>();
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

        group_bounds = new Dictionary<int, List<town_bounds>>();
        foreach (var kv in grouped_elements)
        {
            // Get a list containing the bounds of each element
            var bounds = new List<town_bounds>(kv.Value.Count);
            foreach (var e in kv.Value)
            {
                var b = e.linkable_region;
                b.size += Vector3.one * 2 * IN_TOWN_RANGE;
                bounds.Add(new town_bounds(b, new town_path_element[] { e }));
            }

            while (true)
            {
                bool combined = false;
                for (int i = 0; i < bounds.Count; ++i)
                {
                    for (int j = 0; j < i; ++j)
                    {
                        // Attempt to combine two bounds
                        if (bounds[i].combine_with(bounds[j]))
                        {
                            // Remove bounds at j (swap with last
                            // element to make removal faster)
                            bounds[j] = bounds[bounds.Count - 1];
                            bounds.RemoveAt(bounds.Count - 1);

                            combined = true;
                            break;
                        }
                    }

                    if (combined) break;
                }

                if (!combined) break;
            }

            group_bounds[kv.Key] = bounds;
        }

        // Record bound groups and town extremeties
        foreach (var kv in group_bounds)
            foreach (var b in kv.Value)
            {
                float max_dis = float.NegativeInfinity;
                town_path_element extreme = null;
                foreach (var e in b)
                {
                    // Record my town bounds group
                    e.bounded_group = b;

                    // Work out the direction out of town and distance from my group.
                    e.out_of_town_direction = e.transform.position - b.bounds.center;
                    if (e.out_of_town_direction.magnitude > max_dis &&
                        e.GetComponentInChildren<attacker_entrypoint>() != null)
                    {
                        // Work out most extreme element of the group
                        max_dis = e.out_of_town_direction.magnitude;
                        extreme = e;
                    }

                    // Clear extremety flag
                    e.is_extremety = false;
                    e.out_of_town_direction.Normalize();
                }

                // Re-flag extremity element
                if (extreme != null) extreme.is_extremety = true;
            }

        on_groups_update_listener?.Invoke();
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

                // Have we hit a path element that seperates rooms
                bool hit_seperator = to_expand.seperates_rooms();

                // Add all linked elements to the open set 
                // (if they arent in the closed set)
                foreach (var n in to_expand.linked_elements())
                {
                    if (n == null || closed.Contains(n)) continue;

                    // Multiple neighbouring seperators will
                    // be combined into the same room
                    if (hit_seperator && !n.seperates_rooms()) continue;

                    open.Add(n);
                }
            }

            roomed_elements[room] = closed;
            foreach (var e in closed)
            {
                unroomed.Remove(e);
                e.room = room;
            }
            ++room;
        }

        on_rooms_update_listener?.Invoke();
    }

    static void evaluate_connectivity()
    {
        // Work out which groups have beds
        groups_with_beds = new HashSet<int>();
        foreach (var e in all_elements)
        {
            if (groups_with_beds.Contains(e.group)) continue;
            if (e.interactable is bed) groups_with_beds.Add(e.group);
        }
    }

    public static void draw_group_gizmos()
    {
        if (group_bounds == null) return;

        foreach (var kv in group_bounds)
            foreach (var b in kv.Value)
            {
                Gizmos.color = ui_colors.cycle[kv.Key % ui_colors.cycle.Length];
                Gizmos.DrawWireCube(b.bounds.center, b.bounds.size);
                foreach (var e in b)
                {
                    Gizmos.color = e.is_extremety ? Color.cyan : new Color(1, 0, 1);
                    Gizmos.DrawLine(b.bounds.center, e.transform.position);
                }
            }
    }

    public static int group_at(Vector3 v)
    {
        if (group_bounds == null) return -1;

        int group = -1;
        float min_dis = float.PositiveInfinity;
        foreach (var kv in group_bounds)
        {
            foreach (var b in kv.Value)
            {
                // We are within this group
                if (b.bounds.Contains(v))
                    return kv.Key;

                var dis = b.bounds.SqrDistance(v);
                if (dis < min_dis)
                {
                    min_dis = dis;
                    group = kv.Key;
                }
            }
        }

        return group;
    }

    //##################//
    // LINK VALIDATAION //
    //##################//

    static town_path_element()
    {
        // Register geometry listener
        world.add_geometry_change_listener(validate_elements_within);
    }

    public static void validate_elements_within(List<Bounds> regions)
    {
        var to_validate = new List<town_path_element>();

        foreach (var e in all_elements)
            foreach (var b in regions)
            {
                var e_affected_region = e.linkable_region;

                if (e_affected_region.size.y < town_path_link.CLEARANCE_HEIGHT)
                {
                    // Increase height to include clearance height
                    float delta_height = town_path_link.CLEARANCE_HEIGHT - e_affected_region.size.y;
                    e_affected_region.size += Vector3.up * delta_height;
                    e_affected_region.center += Vector3.up * delta_height / 2f;
                }

                if (b.Intersects(e_affected_region))
                {
                    to_validate.Add(e);
                    break;
                }
            }

        foreach (var e in to_validate)
            validate_links(e);

        rooms_update_required = true;
    }

    static void validate_links(town_path_element r)
    {
        // Re-make all links to/from r
        r.break_links();
        foreach (var r2 in all_elements)
            r.try_link(r2);

        rooms_update_required = true;
    }

    //###################//
    // Link registration //
    //###################//

    static HashSet<town_path_element> all_elements;

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

        r.break_links();
        r.bounded_group?.remove(r);
        rooms_update_required = true;
    }

    //################//
    // ELEMENT ACCESS //
    //################//

    public static town_path_element nearest_element(Vector3 v, int group = -1)
    {
        if (group < 0) return utils.find_to_min(all_elements, (e) => (e.transform.position - v).sqrMagnitude);
        else return utils.find_to_min(all_elements, (e) => (e.transform.position - v).sqrMagnitude, (e) => e.group == group);
    }

    public static town_path_element nearest_element_connected_to_beds(Vector3 v)
    {
        if (groups_with_beds == null) return null;
        return utils.find_to_min(all_elements, (e) =>
        {
            if (e == null) return Mathf.Infinity;
            if (!groups_with_beds.Contains(e.group)) return Mathf.Infinity;
            return (e.transform.position - v).sqrMagnitude;
        });
    }

    public static bool group_has_beds(int group) => groups_with_beds != null && groups_with_beds.Contains(group);

    public static HashSet<town_path_element> element_group(int group)
    {
        if (grouped_elements.TryGetValue(group, out HashSet<town_path_element> elms)) return elms;
        return new HashSet<town_path_element>();
    }

    public static HashSet<town_path_element> elements_in_room(int room)
    {
        if (roomed_elements.TryGetValue(room, out HashSet<town_path_element> elms)) return elms;
        return new HashSet<town_path_element>();
    }

    public static int largest_group()
    {
        int max = 0;
        int ret = -1;
        foreach (var kv in grouped_elements)
            if (kv.Value.Count > max)
            {
                max = kv.Value.Count;
                ret = kv.Key;
            }
        return ret;
    }

    //#########//
    // DISPLAY //
    //#########//

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

        static float heuristic(town_path_element a, town_path_element b) => a.distance_to(b);

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
            fscore[start] = heuristic(goal, start);

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
                    if (n == null || closed_set.Contains(n))
                        continue;

                    // Work out tentative path length to n, if we wen't via current
                    var tgs = gscore[current] + heuristic(n, current);

                    // Get the current neighbour gscore (infinity if not already scored)
                    if (!gscore.TryGetValue(n, out float gsn))
                        gsn = Mathf.Infinity;

                    if (tgs < gsn)
                    {
                        // This is a better path to n, update it
                        came_from[n] = current;
                        gscore[n] = tgs;
                        fscore[n] = tgs + heuristic(goal, n);
                        open_set.Add(n);
                    }
                }
            }

            // Pathfinding failed
            return null;
        }

        // Overloads of the get method
        public static path get(Vector3 v, town_path_element goal) => get(nearest_element(v), goal);
        public static path get(Vector3 a, Vector3 b, int group) => get(nearest_element(a, group), nearest_element(b, group));

        public static path get_random(town_path_element start, int max_length, int min_length = 0)
        {
            if (start == null) return null;

            // Setup pathfinding state
            var open_set = new HashSet<town_path_element>();
            var closed_set = new HashSet<town_path_element>();
            var came_from = new Dictionary<town_path_element, town_path_element>();
            var gscore = new Dictionary<town_path_element, float>();

            // Initialize pathfinding with just start open
            open_set.Add(start);
            gscore[start] = 0;

            town_path_element current = null;

            while (open_set.Count > 0)
            {
                // Find the largest gscore in the current set (i.e the furthest from the start)
                current = utils.find_to_min(open_set, (c) => -gscore[c]);

                if (came_from.Count >= max_length)
                    break;

                // Close current
                open_set.Remove(current);
                closed_set.Add(current);

                foreach (var n in current.linked_elements())
                {
                    if (closed_set.Contains(n))
                        continue;

                    // Work out tentative path length to n, if we wen't via current
                    var tgs = gscore[current] + heuristic(n, current);

                    // Get the current neighbour gscore (infinity if not already scored)
                    if (!gscore.TryGetValue(n, out float gsn))
                        gsn = Mathf.Infinity;

                    if (tgs < gsn)
                    {
                        // This is a better path to n, update it
                        came_from[n] = current;
                        gscore[n] = tgs;
                        open_set.Add(n);
                    }
                }
            }

            if (current == null) return null;
            if (came_from.Count < min_length) return null;

            // Reconstruct path
            var p = new path();
            p.element_path = new List<town_path_element> { current };
            while (came_from.TryGetValue(current, out current))
                p.element_path.Add(current);
            p.element_path.Reverse();
            return p;
        }

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

            if (utils.move_towards(
                walking.transform,
                next_element.transform.position,
                Time.deltaTime * speed * next_element.speed_multiplier,
                arrive_distance: ARRIVE_DISTANCE))
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
            {
                if (this[i] == null || this[i - 1] == null) continue;
                Gizmos.DrawLine(this[i].transform.position, this[i - 1].transform.position);
            }
        }
    }
}

/// <summary> A centralised source for information about pathing groups. </summary>
public static class group_info
{
    public static bool under_attack(int group) =>
        attacker_entrypoint.group_under_attack(group);

    public delegate bool attack_iterator(character attacker);
    public static void iterate_over_attackers(int group, attack_iterator f) =>
        attacker_entrypoint.iterate_over_attackers(group, f);

    public static character closest_attacker(Vector3 position, int group = -1) =>
        attacker_entrypoint.closest_attacker(position, group);

    public static bool has_beds(int group) => town_path_element.group_has_beds(group);

    public static bool has_starvation(int group)
    {
        foreach (var s in settler.get_settlers_by_group(group))
            if (s.starving)
                return true;
        return false;
    }

    public static int bed_count(int group)
    {
        int ret = 0;
        foreach (var e in town_path_element.element_group(group))
            if (e.interactable is bed) ++ret;
        return ret;
    }

    public static int max_visitors(int group) => 1 + settlers(group).Count / 4;

    public static int largest_group() => town_path_element.largest_group();

    public static HashSet<settler> settlers(int group) => settler.get_settlers_by_group(group);
}

/// <summary> A centralised source for information about rooms. </summary>
public static class room_info
{
    public static bool is_dining_room(int room) => dining_spot.is_dining_room(room);
}