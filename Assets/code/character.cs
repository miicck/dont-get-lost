using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICharacterController
{
    void control(character c);
    void draw_gizmos();
    void draw_inspector_gui();
    void on_end_control(character c);
    string inspect_info();
}

public interface IAcceptsDamage
{
    void take_damage(int damage);
}

public class character : networked,
    INotPathBlocking, IDontBlockItemLogisitcs,
    IAcceptsDamage, IPlayerInteractable, IPathingAgent
{
    // Character behaviour
    public string display_name;
    public string plural_name;
    public float walk_speed = 1f;
    public float run_speed = 4f;
    public int max_health = 10;
    public float pathfinding_resolution = 0.5f;
    public float height = 2f;
    public float agro_range = 5f;
    public float rotation_lerp_speed = 1f;
    public float idle_walk_distance = 4f;
    public float flee_distance = 4f;
    public float melee_range = 0.5f;
    public float melee_cooldown = 1f;
    public int melee_damage = 10;
    public FRIENDLINESS friendliness;
    public bool can_walk = true;
    public bool can_swim = false;
    public bool align_to_terrain = false;
    public Transform projectile_target;

    /// <summary> The object currently controlling this character. </summary>
    public ICharacterController controller
    {
        get
        {
            // Default control
            if (_controller == null)
                _controller = default_controller();

            return _controller;
        }
        set
        {
            if (_controller == value)
                return; // No change

            var tmp = _controller;
            _controller = value;

            if (tmp != null)
                tmp.on_end_control(this);
        }
    }
    ICharacterController _controller;

    public enum FRIENDLINESS
    {
        AGRESSIVE,
        FRIENDLY,
        AFRAID
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public virtual player_interaction[] player_interactions()
    {
        return new player_interaction[]
        {
            new player_inspectable(transform)
            {
                text= () =>
                {
                    return display_name.capitalize() + "\n" +
                           controller?.inspect_info();
                }
            }
        };
    }

    //########//
    // SOUNDS //
    //########//

    Dictionary<character_sound.TYPE, List<character_sound>> sounds =
        new Dictionary<character_sound.TYPE, List<character_sound>>();

    AudioSource sound_source;

    void load_sounds()
    {
        // Record all of the sounds I can make
        Vector3 sound_centre = transform.position;
        foreach (var s in GetComponentsInChildren<character_sound>())
        {
            List<character_sound> type_sounds;
            if (!sounds.TryGetValue(s.type, out type_sounds))
            {
                type_sounds = new List<character_sound>();
                sounds[s.type] = type_sounds;
            }
            type_sounds.Add(s);

            sound_centre = s.transform.position;
        }

        // Normalize probabilities
        foreach (var kv in sounds)
        {
            var list = kv.Value;
            float total_prob = 0;
            foreach (var s in list) total_prob += s.probability;
            foreach (var s in list) s.probability /= total_prob;
        }

        if (sound_source == null)
        {
            sound_source = new GameObject("sound_source").AddComponent<AudioSource>();
            sound_source.transform.position = sound_centre;
            sound_source.transform.SetParent(transform);
            sound_source.spatialBlend = 1f; // 3D
        }
    }

    public void play_random_sound(character_sound.TYPE type)
    {
        // Don't play sounds if dead
        if (is_dead) return;

        List<character_sound> sound_list;
        if (!sounds.TryGetValue(type, out sound_list))
        {
            Debug.Log("No character sounds of type " + type + " for " + name);
            return;
        }

        character_sound chosen = sound_list[0];
        float rnd = Random.Range(0, 1f);
        float total = 0;
        foreach (var s in sound_list)
        {
            total += s.probability;
            if (total > rnd)
            {
                chosen = s;
                break;
            }
        }

        sound_source.Stop();
        sound_source.pitch = chosen.pitch_modifier * Random.Range(0.95f, 1.05f);
        sound_source.clip = chosen.clip;
        sound_source.volume = chosen.volume;
        sound_source.Play();
    }

    public void play_idle_sounds()
    {
        // Play idle sounds
        if (!sound_source.isPlaying)
            if (Random.Range(0, 1f) < 0.1f)
                play_random_sound(character_sound.TYPE.IDLE);
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    public bool dont_despawn_automatically = false;

    private void Start()
    {
        load_sounds();
        InvokeRepeating("slow_update", Random.Range(0, 1f), 1f);
        if (!dont_despawn_automatically)
            characters.Add(this);
    }

    private void OnDestroy()
    {
        characters.Remove(this);
    }

    void slow_update()
    {
        play_idle_sounds();
    }

    private void Update()
    {
        // Characters are controlled by the authority client
        if (!has_authority) return;

        // Don't do anyting unless the chunk is generated
        if (chunk.at(transform.position, true) == null) return;

        // Don't do anything if dead
        if (health.value <= 0) return;

        controller?.control(this);
    }

    private void OnDrawGizmos()
    {
        if (is_dead) return;

        controller.draw_gizmos();
        Vector3 f = transform.forward * pathfinding_resolution * 0.5f;
        Vector3 r = transform.right * pathfinding_resolution * 0.5f;
        Vector3[] square = new Vector3[]
        {
            transform.position + f + r,
            transform.position + f - r,
            transform.position - f - r,
            transform.position - f + r,
            transform.position + f + r
        };

        Gizmos.color = Color.green;
        for (int i = 1; i < square.Length; ++i)
            Gizmos.DrawLine(square[i - 1], square[i]);

        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * height);
    }

    //############//
    // NETWORKING //
    //############//

    networked_variables.net_float y_rotation;
    networked_variables.net_int health;
    inventory loot;

    public override void on_init_network_variables()
    {
        y_rotation = new networked_variables.net_float(resolution: 5f);
        y_rotation.on_change = () =>
        {
            var ea = transform.rotation.eulerAngles;
            ea.y = y_rotation.value;
            transform.rotation = Quaternion.Euler(ea);
        };

        health = new networked_variables.net_int(default_value: max_health);

        health.on_change = () =>
        {
            if (health.value >= max_health)
            {
                if (_healthbar != null)
                    Destroy(_healthbar.gameObject);
            }
            else
                healthbar.belongs_to = this;

            if (health.value <= 0)
                die();
        };
    }

    public override void on_network_update()
    {
        if (has_authority)
        {
            networked_position = transform.position;
            y_rotation.value = transform.rotation.eulerAngles.y;
        }
    }

    public override void on_add_networked_child(networked child)
    {
        if (child is inventory)
        {
            var inv = (inventory)child;
            if (inv.name.Contains("loot"))
            {
                loot = inv;
                loot.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = "Dead " + display_name;
            }
        }
    }

    public override bool persistant()
    {
        // Characters despawn when not loaded
        return false;
    }

    //########//
    // HEALTH //
    //########//

    public int remaining_health { get => health.value; }

    public void heal(int amount)
    {
        int max_heal = max_health - health.value;
        health.value += Mathf.Min(amount, max_heal);
    }

    public void take_damage(int damage)
    {
        var hm = hit_marker.create("-" + damage);
        hm.transform.position = transform.position + Vector3.up * height;

        play_random_sound(character_sound.TYPE.INJURY);
        health.value -= damage;
    }

    healthbar healthbar
    {
        get
        {
            if (_healthbar == null)
            {
                _healthbar = new GameObject("healthbar").AddComponent<healthbar>();
                _healthbar.transform.SetParent(transform);
                _healthbar.transform.position = transform.position + Vector3.up * height;
                _healthbar.belongs_to = this;
            }
            return _healthbar;
        }
    }
    healthbar _healthbar;

    //#######//
    // DEATH //
    //#######//

    dead_character dead_version;
    bool is_dead => dead_version != null;

    void die()
    {
        if (!is_dead)
        {
            dead_version = dead_character.create(this);
            on_death();
        }
    }

    protected virtual void on_death() { }

    public class dead_character : MonoBehaviour, INotPathBlocking, IPlayerInteractable
    {
        character character;

        player_interaction[] _interactions;
        public player_interaction[] player_interactions()
        {
            if (_interactions == null) _interactions = new player_interaction[]
            {
                new player_inspectable(transform)
                {
                    text = ()=> "Dead "+character.display_name
                },
                new loot_menu(this)
            };
            return _interactions;
        }

        class loot_menu : left_player_menu
        {
            dead_character dc;
            public loot_menu(dead_character dc) : base("dead " + dc.character.display_name) { this.dc = dc; }
            protected override RectTransform create_menu() { return dc.character.loot?.ui; }
            public override inventory editable_inventory() { return dc.character.loot; }
        }

        void gradual_decay()
        {
            if (transform.childCount == 0)
                Destroy(gameObject);
            else
                Destroy(transform.GetChild(Random.Range(0, transform.childCount)).gameObject);
        }

        void on_create(character to_copy)
        {
            foreach (var r in to_copy.GetComponentsInChildren<MeshRenderer>())
            {
                // Create a render-only copy of each render in the character
                var rcopy = r.inst();

                // Don't copy children (they will be copied later in the
                // GetComponentsInChildren loop)
                foreach (Transform child in rcopy.transform)
                    Destroy(child.gameObject);

                // Move the copy to the exact same place as the original mesh
                rcopy.transform.position = r.transform.position;
                rcopy.transform.rotation = r.transform.rotation;
                rcopy.transform.localScale = r.transform.lossyScale;

                // Delete anything that isn't to do with rendering (perhaps
                // this method could be improved by simply building an object
                // that only has the desired stuff, rather than deleting stuff)
                foreach (var c in rcopy.GetComponentsInChildren<Component>())
                {
                    if (c is Transform) continue;
                    if (c is MeshRenderer) continue;
                    if (c is MeshFilter) continue;
                    if (c is MeshCollider)
                    {
                        // Make sure all colliders are convex so we
                        // can add rigidbodies later
                        var mc = (MeshCollider)c;
                        mc.convex = true;
                        continue;
                    }
                    Destroy(c);
                }

                // Make the copy a child of this dead_character and
                // give it a simple collider
                rcopy.transform.SetParent(transform);

                // Make the character invisisble (do this after
                // we copy, so the copied version isn't invisible)
                r.enabled = false;
            }

            // Delay rigidbodies so they have time to register the new colliders
            Invoke("add_rigidbodies", 0.1f);
        }

        void add_rigidbodies()
        {
            foreach (Transform c in transform)
            {
                var rb = c.gameObject.AddComponent<Rigidbody>();

                // Don't let the body parts ping around everywhere
                // (as fun as that is)
                rb.maxAngularVelocity = 1f;
                rb.maxDepenetrationVelocity = 0.25f;
            }
        }

        public static dead_character create(character to_copy)
        {
            // Create the dead version
            var dead_version = new GameObject("dead_" + to_copy.name).AddComponent<dead_character>();
            dead_version.character = to_copy;
            dead_version.transform.position = to_copy.transform.position;
            dead_version.transform.rotation = to_copy.transform.rotation;
            dead_version.on_create(to_copy);

            // Deactivate the alive version
            foreach (var r in to_copy.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in to_copy.GetComponentsInChildren<Collider>()) c.enabled = false;
            to_copy.healthbar.gameObject.SetActive(false);

            // Create loot on the authority client
            if (to_copy.has_authority)
            {
                // Create the looting inventory
                var loot = (inventory)client.create(
                    to_copy.transform.position,
                    "inventories/character_loot",
                    parent: to_copy);

                // Add loot to the above inventory once it's registered
                loot.add_register_listener(() =>
                {
                    foreach (var p in to_copy.GetComponents<product>())
                        p.create_in(loot);
                });
            }

            // Parent the dead version to the character so they get despawned together
            dead_version.transform.SetParent(to_copy.transform);
            return dead_version;
        }
    }

    //#########//
    // CONTROL //
    //#########//

    protected virtual ICharacterController default_controller()
    {
        return new default_character_control();
    }

    float last_attack_time = 0;
    public void melee_attack(player p)
    {
        if (Time.realtimeSinceStartup - last_attack_time > melee_cooldown)
        {
            p.take_damage(melee_damage);
            last_attack_time = Time.realtimeSinceStartup;
        }
    }

    public bool check_agro(player p)
    {
        if (p.fly_mode) return false; // Don't agro in fly mode
        return (p.transform.position - transform.position).magnitude < agro_range;
    }

    public bool move_towards(Vector3 point, float speed, out bool failed, float arrive_distance = 0.25f)
    {
        // Work out how far to the point
        Vector3 delta = point - transform.position;
        float dis = Time.deltaTime * speed;

        Vector3 new_pos = transform.position;

        if (delta.magnitude < dis) new_pos += delta;
        else new_pos += delta.normalized * dis;

        failed = false;
        if (!is_allowed_at(new_pos))
        {
            failed = true;
            return false;
        }

        // Move along to the new position
        transform.position = new_pos;

        // Look in the direction of travel
        delta.y = 0;
        if (delta.magnitude > 10e-4)
        {
            // Lerp forward look direction
            Vector3 new_forward = Vector3.Lerp(
                transform.forward,
                delta.normalized,
                rotation_lerp_speed * speed * Time.deltaTime
            );

            if (new_forward.magnitude > 10e-4)
            {
                // Set up direction with reference to legs
                Vector3 up = Vector3.zero;
                if (align_to_terrain)
                    foreach (var l in GetComponentsInChildren<leg>())
                        up += l.ground_normal;
                else up = Vector3.up;

                up = Vector3.Lerp(
                    transform.up,
                    up.normalized,
                    rotation_lerp_speed * speed * Time.deltaTime
                );

                new_forward -= Vector3.Project(new_forward, up);
                transform.rotation = Quaternion.LookRotation(
                    new_forward,
                    up.normalized
                );
            }
        }

        return (point - new_pos).magnitude < arrive_distance;
    }

    //###############//
    // IPathingAgent //
    //###############//

    public bool is_allowed_at(Vector3 v)
    {
        // Check we're in the right medium
        if (!can_swim && v.y < world.SEA_LEVEL) return false;
        if (!can_walk && v.y > world.SEA_LEVEL) return false;
        return true;
    }

    public Vector3 validate_position(Vector3 v, out bool valid)
    {
        if (v.y < world.SEA_LEVEL)
        {
            v.y = world.SEA_LEVEL;
            valid = can_swim;
            return v;
        }

        Vector3 ret = pathfinding_utils.validate_walking_position(v, resolution, out valid);
        if (!is_allowed_at(ret)) valid = false;
        return ret;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            resolution, height, resolution / 2f);
    }

    public float resolution => pathfinding_resolution;

    //##############//
    // STATIC STUFF //
    //##############//

    static HashSet<character> characters;

    public static bool characters_enabled
    {
        get => _characters_enabled;
        set
        {
            if (_characters_enabled == value)
                return; // No change

            _characters_enabled = value;
            if (!_characters_enabled)
                foreach (var c in FindObjectsOfType<character>())
                    c.delete();
        }
    }
    static bool _characters_enabled = true;

    public static int target_character_count => character_spawn_point.active_count;

    public static void initialize()
    {
        characters = new HashSet<character>();
    }

    public static void run_spawning()
    {
        if (!characters_enabled) return;

        if (characters.Count < target_character_count)
        {
            // Fewer characters than target, spawn one
            character_spawn_point.spawn();
        }
        else if (characters.Count > target_character_count)
        {
            // More characters than target, despawn the character 
            // that is furthest from the player
            var furthest = utils.find_to_min(characters, (c) =>
            {
                if (c == null) return Mathf.Infinity;

                // Only delete default-controlled characters
                if (c.controller is default_character_control)
                    return -(c.transform.position - player.current.transform.position).sqrMagnitude;

                return Mathf.Infinity;
            });
            furthest?.delete();
        }
    }

    public static string info()
    {
        return "    Total characters : " + characters.Count +
                    "/" + target_character_count;
    }

    //########//
    // EDITOR //
    //########//

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(character), true)]
    new public class editor : networked.editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var c = (character)target;
            c.controller.draw_inspector_gui();
        }
    }
#endif
}

//###########//
// HEALTHBAR //
//###########//

class healthbar : MonoBehaviour
{
    public const int WIDTH = 100;
    public const int HEIGHT = 10;

    Canvas canv;
    RectTransform canv_rect;
    public character belongs_to;

    UnityEngine.UI.Image background;
    UnityEngine.UI.Image foreground;
    RectTransform background_rect;
    RectTransform foreground_rect;

    private void Start()
    {
        canv = gameObject.AddComponent<Canvas>();
        canv.renderMode = RenderMode.WorldSpace;
        canv.worldCamera = player.current.camera;
        canv_rect = canv.GetComponent<RectTransform>();
        canv_rect.SetParent(transform);
        canv_rect.localRotation = Quaternion.identity;
        canv_rect.sizeDelta = new Vector2(WIDTH, HEIGHT);

        background = new GameObject("background").AddComponent<UnityEngine.UI.Image>();
        foreground = new GameObject("foreground").AddComponent<UnityEngine.UI.Image>();
        background.color = Color.red;
        foreground.color = Color.green;
        background_rect = background.GetComponent<RectTransform>();
        foreground_rect = foreground.GetComponent<RectTransform>();

        background_rect.SetParent(canv_rect);
        foreground_rect.SetParent(canv_rect);

        background_rect.sizeDelta = canv_rect.sizeDelta;
        foreground_rect.sizeDelta = canv_rect.sizeDelta;

        background_rect.localPosition = Vector3.zero;
        foreground_rect.localPosition = Vector3.zero;

        background_rect.anchoredPosition = Vector2.zero;
        foreground_rect.anchoredPosition = Vector2.zero;

        background_rect.localRotation = Quaternion.identity;
        foreground_rect.localRotation = Quaternion.identity;

        canv_rect.transform.localScale = new Vector3(1f, 1f, 1f) / (float)WIDTH;
    }

    private void Update()
    {
        foreground.GetComponent<RectTransform>().sizeDelta = new Vector2(
            canv_rect.sizeDelta.x * belongs_to.remaining_health / belongs_to.max_health,
            canv_rect.sizeDelta.y
        );
    }
}

//###########################//
// DEFAULT CHARACTER CONTROL //
//###########################//

public class idle_wander : ICharacterController
{
    path path;
    int index;
    bool going_forward;

    public void control(character c)
    {
        if (path == null)
        {
            Vector3 start = c.transform.position;
            random_path.success_func sf = (v) => (v - start).magnitude > c.idle_walk_distance;
            path = new random_path(start, sf, sf, c);
            index = 0;
            going_forward = true;
        }

        switch (path.state)
        {
            case path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter);
                break;

            case path.STATE.FAILED:
                path = null;
                break;

            case path.STATE.COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in idle_wander!");
                break;
        }
    }

    void walk_path(character c)
    {
        if (path.length < 2)
        {
            path = null;
            return;
        }

        // Walked off the start of the path - change direction
        if (index < 0)
        {
            going_forward = true;
            index = 1;
        }

        // Walked off the end of the path - change direction
        if (index >= path.length)
        {
            going_forward = false;
            index = path.length - 1;
        }

        if (c.move_towards(path[index], c.walk_speed, out bool failed))
        {
            if (going_forward) ++index;
            else --index;
        }

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() { path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "Wandering idly"; }
}

public class flee_controller : ICharacterController
{
    path path;
    int index;
    Transform fleeing;

    public flee_controller(Transform fleeing)
    {
        // The object we are fleeing from
        this.fleeing = fleeing;
    }

    public void control(character c)
    {
        if (fleeing == null)
        {
            Debug.LogError("You should probably think about this case more; somehow we need to return to idle...");
            return;
        }

        if (path == null)
        {
            // Get a new fleeing path
            path = new flee_path(c.transform.position, fleeing, c);
            index = 0;
        }

        switch (path.state)
        {
            case path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case path.STATE.FAILED:
                path = null;
                break;

            case path.STATE.COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in flee_controller!");
                break;
        }
    }

    void walk_path(character c)
    {
        if (path.length <= index)
        {
            // Reached the end of the path, time for a new one
            path = null;
            return;
        }

        // Walk along the path
        if (c.move_towards(path[index], c.run_speed, out bool failed))
            ++index;

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() { path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "Fleeing"; }
}

public class chase_controller : ICharacterController
{
    path path;
    int index;
    Transform chasing;

    public chase_controller(Transform chasing)
    {
        this.chasing = chasing;
    }

    public void control(character c)
    {
        if (path == null)
        {
            // Decrease allowed pathfinding iterations for closer targets 
            // (so we fail more quickly in hopeless pathing cases)
            int max_iter = (int)(c.transform.position - chasing.transform.position).magnitude;
            max_iter = Mathf.Min(500, 10 + max_iter * max_iter);
            path = new chase_path(c.transform.position, chasing, c, max_iterations: max_iter);
            path.on_state_change_listener = (s) =>
            {
                if (s == path.STATE.COMPLETE || s == path.STATE.PARTIALLY_COMPLETE)
                {
                    // Ensure the path actually goes somewhere
                    if (path.length < 2)
                    {
                        path = null;
                        return;
                    }

                    // Ensure the end isn't right next to the start
                    Vector3 delta = path[path.length - 1] - path[0];
                    if (delta.magnitude < c.pathfinding_resolution) path = null;
                }
            };
            index = 0;
        }

        switch (path.state)
        {
            case path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case path.STATE.FAILED:
                path = null;
                break;

            case path.STATE.COMPLETE:
            case path.STATE.PARTIALLY_COMPLETE:
                walk_path(c);
                break;

            default:
                Debug.LogError("Unkown path state in chase_controller!");
                break;
        }
    }

    void walk_path(character c)
    {
        if (path.length <= index)
        {
            // Reached the end of the path, time for a new one
            path = null;
            return;
        }

        // Walk along the path
        if (c.move_towards(path[index], c.run_speed, out bool failed))
            ++index;

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() { path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "Chasing"; }
}

public class default_character_control : ICharacterController
{
    ICharacterController subcontroller;

    public void control(character c)
    {
        if (subcontroller == null)
            subcontroller = new idle_wander();

        if (subcontroller is idle_wander)
        {
            if (player.current != null)
                switch (c.friendliness)
                {
                    case character.FRIENDLINESS.FRIENDLY:
                        break; // Just wander around idly

                    case character.FRIENDLINESS.AFRAID:
                        if (c.distance_to(player.current) < c.agro_range)
                            subcontroller = new flee_controller(player.current.transform);
                        break;

                    case character.FRIENDLINESS.AGRESSIVE:
                        if (c.distance_to(player.current) < c.agro_range)
                            subcontroller = new chase_controller(player.current.transform);
                        break;
                }
        }
        else
        {
            if (player.current != null)
                if (c.distance_to(player.current) > c.agro_range * 2f)
                    subcontroller = new idle_wander();
        }

        subcontroller.control(c);
    }

    public void on_end_control(character c) { subcontroller?.on_end_control(c); }
    public void draw_gizmos() { subcontroller?.draw_gizmos(); }
    public void draw_inspector_gui() { subcontroller?.draw_inspector_gui(); }
    public string inspect_info() { return subcontroller?.inspect_info(); }
}

public class default_character_control_old : ICharacterController
{
    // The character being controlled
    character character
    {
        get => _character;
        set
        {
            if (_character == null) _character = value;
            if (_character != value)
                throw new System.Exception("Tried to overwrite character!");
        }
    }
    character _character;

    // The path we walk along idly
    path idle_path
    {
        get => _idle_path;
        set
        {
            _idle_path = value;
            idle_path_point = 0;
            idle_path_direction = 1;
        }
    }
    path _idle_path;
    int idle_path_point = 0;
    int idle_path_direction = 1;

    // The path that we're chasing/fleeing from the player
    path agro_path
    {
        get => _agro_path;
        set
        {
            _agro_path = value;
            agro_path_progress = 0;
        }
    }
    path _agro_path;
    int agro_path_progress;
    int agro_path_idle_link = 0; // Where, in the idle path, the agro path begins

    public void control(character c)
    {
        character = c;

        // Ensure we have an idle path to walk
        if (idle_path == null)
        {
            random_path.success_func f = (v) =>
                (v - character.transform.position).magnitude > character.idle_walk_distance;
            idle_path = new random_path(character.transform.position, f, f, character);
            return;
        }

        // If we're not already agro to a player/a player is in agro range
        if (agro_path == null && c.check_agro(player.current))
            switch (character.friendliness)
            {
                case character.FRIENDLINESS.AGRESSIVE:

                    // Find the nearest point along the idle path to the player
                    float min_dis = Mathf.Infinity;
                    for (int i = 0; i < idle_path.length; ++i)
                    {
                        float dis = (idle_path[i] - player.current.transform.position).magnitude;
                        if (dis < min_dis)
                        {
                            min_dis = dis;
                            agro_path_idle_link = i;
                        }
                    }

                    // Path from that point, to the player
                    agro_path = new chase_path(idle_path[agro_path_idle_link],
                        player.current.transform, character,
                        max_iterations: 100,
                        goal_distance: character.melee_range * 0.8f);

                    break;

                case character.FRIENDLINESS.AFRAID:

                    // Find the furthest point along the idle path to the player
                    float max_dis = Mathf.Infinity;
                    for (int i = 0; i < idle_path.length; ++i)
                    {
                        float dis = (idle_path[i] - player.current.transform.position).magnitude;
                        if (dis > max_dis)
                        {
                            max_dis = dis;
                            agro_path_idle_link = i;
                        }
                    }

                    // Path from that point, away from the player
                    agro_path = new flee_path(character.transform.position,
                        player.current.transform, character);

                    break;
            }

        if (agro_path != null)
        {
            if (player.current == null || player.current.is_dead)
            {
                // Stop chasing
                agro_path = null;
                idle_path = null;
                return;
            }

            switch (agro_path.state)
            {
                // Chase path failed, reset to no chasing
                case path.STATE.FAILED:
                    agro_path = null;
                    break;

                // Continue chase pathfinding
                case path.STATE.SEARCHING:
                    agro_path.pathfind(load_balancing.iter);
                    break;

                // Chase the player
                case path.STATE.COMPLETE:

                    if (idle_path_point != agro_path_idle_link)
                    {
                        // Get to the point in the idle path where the
                        // chase path starts from
                        if (character.move_towards(idle_path[idle_path_point],
                            character.run_speed, out bool failed))
                        {
                            if (failed) agro_path = null;
                            int dir = agro_path_idle_link - idle_path_point;
                            if (dir > 0) idle_path_point += 1;
                            else if (dir < 0) idle_path_point -= 1;
                        }
                    }
                    else
                    {
                        float agro_path_speed = character.run_speed;
                        if (agro_path_progress == agro_path.length - 1)
                        {
                            // Got to the end of the path             
                            switch (character.friendliness)
                            {
                                case character.FRIENDLINESS.AGRESSIVE:
                                    // Close to the player, slow down 
                                    // just enough to keep up
                                    agro_path_speed = Mathf.Min(
                                        character.run_speed,
                                        player.BASE_SPEED * 1.2f);
                                    break;

                                case character.FRIENDLINESS.AFRAID:
                                    // Keep fleeing
                                    agro_path = new flee_path(character.transform.position,
                                        player.current.transform, character);
                                    return;
                            }
                        }

                        if (character.move_towards(agro_path[agro_path_progress],
                            agro_path_speed, out bool failed))
                        {
                            if (failed) agro_path = null;
                            agro_path_progress = Mathf.Min(
                                agro_path_progress + 1,
                                agro_path.length - 1);
                        }

                        // Attack the player if we're close enough
                        if ((player.current.transform.position -
                            character.transform.position).magnitude < character.melee_range)
                            character.melee_attack(player.current);
                    }
                    return;

            }
        }

        // Move around idly
        switch (idle_path.state)
        {
            // Walk back and forth along the idle path
            case path.STATE.COMPLETE:
                if (character.move_towards(idle_path[idle_path_point],
                    character.walk_speed, out bool failed))
                {
                    idle_path_point += idle_path_direction;
                    if (idle_path_point < 0)
                    {
                        idle_path_point = 0;
                        idle_path_direction = 1;
                    }
                    else if (idle_path_point >= idle_path.length)
                    {
                        idle_path_point = idle_path.length - 1;
                        idle_path_direction = -1;
                    }

                }
                if (failed) idle_path = null;
                return;

            // Couldn't create an idle path, delete the character
            case path.STATE.FAILED:
                character.delete();
                idle_path = null;
                return;

            // Continue idle pathfinding
            case path.STATE.SEARCHING:
                idle_path.pathfind(load_balancing.iter);
                return;
        }
    }

    public void on_end_control(character c) { }

    public void draw_gizmos()
    {
        idle_path?.draw_gizmos();
        agro_path?.draw_gizmos();
    }

    public string inspect_info()
    {
        return "";
    }

    public void draw_inspector_gui()
    {
#if UNITY_EDITOR
        UnityEditor.EditorGUILayout.TextArea(idle_path?.info_text());
#endif
    }
}
