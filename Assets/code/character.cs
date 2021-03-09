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
    //############################################//
    // Parameters determining character behaviour //
    //############################################//

    public const float AGRO_RANGE = 8f;
    public const float IDLE_WALK_RANGE = 5f;

    public string display_name;
    public string plural_name;

    public float pathfinding_resolution = 0.5f;
    public float height = 2f;
    public float walk_speed = 1f;
    public float run_speed = 4f;
    public float rotation_lerp_speed = 1f;
    public bool can_walk = true;
    public bool can_swim = false;
    public bool align_to_terrain = false;

    public int max_health = 10;
    public FRIENDLINESS friendliness;
    public float attack_time = 1f;
    public float attack_cooldown = 1f;
    public int attack_damage = 50;

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
        if (!chunk.generation_complete(transform.position)) return;

        // Don't do anything if dead
        if (is_dead) return;

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
    networked_variables.net_int awareness;
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

        health = new networked_variables.net_int(
            default_value: max_health,
            min_value: 0, max_value: max_health);

        health.on_change = () =>
        {
            healthbar.set_fraction(health.value / (float)max_health);
            healthbar.gameObject.SetActive(health.value != 0 && health.value != max_health);
            if (health.value <= 0)
                die();
        };

        awareness = new networked_variables.net_int(
            default_value: 0, min_value: 0, max_value: 100);

        awareness.on_change = () =>
        {
            awareness_meter.set_fraction(awareness.value / 100f);
            awareness_meter.gameObject.SetActive(awareness.value != 0 && awareness.value != 100);
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
        awareness.value = 100;
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
            }
            return _healthbar;
        }
    }
    healthbar _healthbar;

    //###########//
    // AWARENESS //
    //###########//

    public void modify_awareness(int delta) { awareness.value += delta; }
    public bool is_aware => awareness.value > 99;

    healthbar awareness_meter
    {
        get
        {
            if (_awareness_meter == null)
            {
                _awareness_meter = new GameObject("awareness_meter").AddComponent<healthbar>();
                _awareness_meter.transform.SetParent(transform);
                _awareness_meter.transform.position = transform.position + Vector3.up * (height + 0.1f);
                _awareness_meter.height = 5;
                _awareness_meter.foreground_color = Color.yellow;
                _awareness_meter.background_color = Color.black;
            }
            return _awareness_meter;
        }
    }
    healthbar _awareness_meter;

    float delta_awareness
    {
        get => _delta_awareness;
        set
        {
            _delta_awareness = value;

            if (_delta_awareness > 1f)
            {
                int da = Mathf.FloorToInt(_delta_awareness);
                _delta_awareness -= da;
                modify_awareness(da);
            }
            else if (_delta_awareness < -1f)
            {
                int da = Mathf.FloorToInt(-_delta_awareness);
                _delta_awareness += da;
                modify_awareness(-da);
            }
        }
    }
    float _delta_awareness;

    public void run_awareness_checks(player p)
    {
        const float CUTOFF_RADIUS = 16f;    // Ignore players beyond this
        const float MIN_AWARE_TIME = 0.25f; // The minimum amount of time it takes to become aware
        const float DEAWARE_TIME = 4f;      // The amount of time it takes to become fully un-aware              

        // If we're aware, stay that way
        if (is_aware) return;

        Vector3 delta = p.transform.position - transform.position;

        if (delta.magnitude > CUTOFF_RADIUS)
        {
            // Not in sight (too far away)
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        // A measure of proximity 1 => very close, 0 => very far
        float prox = 1f - delta.magnitude / CUTOFF_RADIUS;
        prox = prox * prox;

        // Field of view for awareness (increases as we get closer)
        float fov = 90f + (360 - 90) * prox;

        if (Vector3.Angle(delta, transform.forward) > fov / 2f)
        {
            // Not in sight (out of fov)
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        var ray = new Ray(transform.position + height * Vector3.up / 2f, delta);
        foreach (var hit in Physics.RaycastAll(ray, delta.magnitude))
        {
            if (hit.transform.IsChildOf(transform)) continue; // Can't block my own vision
            if (hit.transform.IsChildOf(p.transform)) break; // Found the player

            // Vision is blocked
            delta_awareness -= 100f * Time.deltaTime / DEAWARE_TIME;
            return;
        }

        // I can see the player, increase awareness at a rate depending on proximity
        delta_awareness += 100f * Time.deltaTime * prox / MIN_AWARE_TIME;
    }

    //#######//
    // DEATH //
    //#######//

    dead_character dead_version;
    public bool is_dead => dead_version != null || (health != null && health.value <= 0);

    void die()
    {
        if (dead_version == null && create_dead_body())
            dead_version = dead_character.create(this);

        on_death();
    }

    protected virtual bool create_dead_body() { return true; }
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
            to_copy.awareness_meter.gameObject.SetActive(false);

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

    public bool check_agro(player p)
    {
        if (p.fly_mode) return false; // Don't agro in fly mode
        return (p.transform.position - transform.position).magnitude < AGRO_RANGE;
    }

    public bool check_deagro(player p)
    {
        if (p.fly_mode) return true;
        return (p.transform.position - transform.position).magnitude > 2f * AGRO_RANGE;
    }

    /// <summary> Call to put the character somewhere sensible. </summary>
    public void unstuck()
    {
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(
            transform.position + Vector3.up * world.MAX_ALTITUDE, Vector3.down),
            out RaycastHit hit);

        if (tc == null)
            Debug.LogError("No terrain found to unstick " + name);
        else
        {
            transform.position = hit.point;
        }
    }

    public bool move_towards(Vector3 point, float speed, out bool failed, float arrive_distance = -1)
    {
        if (arrive_distance < 0)
            arrive_distance = pathfinding_resolution / 2f;

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

    public Vector3 projectile_target()
    {
        return transform.position + Vector3.up * height / 2f;
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
    [UnityEditor.CanEditMultipleObjects]
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
    public int width = 100;
    public int height = 10;
    public Color background_color = Color.red;
    public Color foreground_color = Color.green;

    RectTransform canv_rect;
    UnityEngine.UI.Image foreground;

    public void set_fraction(float f)
    {
        if (foreground == null) create();

        f = Mathf.Clamp(f, 0, 1f);
        foreground.GetComponent<RectTransform>().sizeDelta = new Vector2(
            canv_rect.sizeDelta.x * f,
            canv_rect.sizeDelta.y
        );
    }

    private void create()
    {
        var canv = gameObject.AddComponent<Canvas>();
        canv.renderMode = RenderMode.WorldSpace;
        canv.worldCamera = player.current.camera;
        canv_rect = canv.GetComponent<RectTransform>();
        canv_rect.SetParent(transform);
        canv_rect.localRotation = Quaternion.identity;
        canv_rect.sizeDelta = new Vector2(width, height);

        var background = new GameObject("background").AddComponent<UnityEngine.UI.Image>();
        foreground = new GameObject("foreground").AddComponent<UnityEngine.UI.Image>();
        background.color = background_color;
        foreground.color = foreground_color;
        var background_rect = background.GetComponent<RectTransform>();
        var foreground_rect = foreground.GetComponent<RectTransform>();

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

        canv_rect.transform.localScale = new Vector3(1f, 1f, 1f) / (float)width;
    }
}

//###########################//
// DEFAULT CHARACTER CONTROL //
//###########################//

public class idle_wander : ICharacterController
{
    random_path path;
    int index;
    bool going_forward;

    public void control(character c)
    {
        if (path == null)
        {
            Vector3 start = c.transform.position;
            random_path.success_func sf = (v) => (v - start).magnitude > character.IDLE_WALK_RANGE;
            path = new random_path(start, sf, sf, c);
            path.on_invalid_start = () => c?.unstuck();
            index = 0;
            going_forward = true;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter);
                break;

            case global::path.STATE.FAILED:
                path = null;
                break;

            case global::path.STATE.COMPLETE:
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
    flee_path path;
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
            path.on_invalid_start = () => c?.unstuck();
            index = 0;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case global::path.STATE.FAILED:
                path = null;
                break;

            case global::path.STATE.COMPLETE:
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
    chase_path path;
    int index;
    Transform chasing;
    IAcceptsDamage to_damage;
    attack_point attack;

    public chase_controller(Transform chasing, IAcceptsDamage to_damage)
    {
        this.chasing = chasing;
        this.to_damage = to_damage;
    }

    public void control(character c)
    {
        if (attack != null && attack.state == attack_point.STATE.ATTACKING)
        {
            // I'm attacking
            attack.set_animation(c);
            return;
        }

        if (path == null)
        {
            // Decrease allowed pathfinding iterations for closer targets 
            // (so we fail more quickly in hopeless pathing cases)
            int max_iter = (int)(c.transform.position - chasing.transform.position).magnitude;
            max_iter = Mathf.Min(500, 10 + max_iter * max_iter);

            float goal_distance = c.pathfinding_resolution * 0.8f;
            if (attack != null) goal_distance *= 4; // Don't need to get so close if attack isn't ready

            path = new chase_path(c.transform.position, chasing, c, max_iterations: max_iter, goal_distance: goal_distance);

            path.on_state_change_listener = (s) =>
            {
                if (s == global::path.STATE.COMPLETE || s == global::path.STATE.PARTIALLY_COMPLETE)
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

            // Unstick the character if we fail to find a valid start point
            path.on_invalid_start = () => c?.unstuck();

            index = 0;
        }

        switch (path.state)
        {
            case global::path.STATE.SEARCHING:
                path.pathfind(load_balancing.iter * 2);
                break;

            case global::path.STATE.FAILED:
                path = null;
                break;

            case global::path.STATE.COMPLETE:
            case global::path.STATE.PARTIALLY_COMPLETE:
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

        if (attack == null)
            if (c.distance_to(chasing) < c.pathfinding_resolution)
            {
                attack = attack_point.create(c, chasing);
                attack.on_strike = () =>
                {
                    if (c.is_dead) return;
                    if (c.distance_to(chasing) < c.pathfinding_resolution * 2f)
                        to_damage.take_damage(c.attack_damage);
                };
            }

        if (failed) path = null;
    }

    public void on_end_control(character c) { }
    public void draw_gizmos() { path?.draw_gizmos(); }
    public void draw_inspector_gui() { }
    public string inspect_info() { return "Chasing"; }
}

public class attack_point : MonoBehaviour
{
    public float cooldown_time { get; private set; }
    public float attack_time { get; private set; }

    public float elapsed_time
    {
        get => _elapsed_time;
        set
        {
            _elapsed_time = value;
            if (_elapsed_time > attack_time + cooldown_time) state = STATE.COMPLETE;
            else if (_elapsed_time > attack_time) state = STATE.COOLDOWN;
            else state = STATE.ATTACKING;
        }
    }
    float _elapsed_time;

    public enum STATE
    {
        ATTACKING,
        COOLDOWN,
        COMPLETE
    }

    public STATE state
    {
        get => _state;
        set
        {
            if (_state == value) return; // No change
            _state = value;
            switch (_state)
            {
                case STATE.COOLDOWN:
                    on_strike?.Invoke();
                    break;

                case STATE.COMPLETE:
                    Destroy(gameObject);
                    break;
            }
        }
    }
    STATE _state;

    public void set_animation(character c)
    {
        float a = Mathf.Clamp(elapsed_time / attack_time, 0f, 1f);
        float s = Mathf.Sin(a * a * Mathf.PI);
        c.transform.position = transform.position - transform.forward * s;
        c.transform.forward = transform.forward;
    }

    public delegate void callback();
    public callback on_strike;

    public static attack_point create(character c, Transform target_transform)
    {
        var ret = new GameObject(c.name + "_attack_point").AddComponent<attack_point>();
        ret.elapsed_time = 0f;
        ret.cooldown_time = c.attack_cooldown;
        ret.attack_time = c.attack_time;

        ret.transform.position = c.transform.position;
        ret.transform.forward = (target_transform.position - c.transform.position);

        return ret;
    }

    private void Update()
    {
        // Destroy myself when attack is over
        elapsed_time += Time.deltaTime;
    }

    private void OnDrawGizmos()
    {
        switch (state)
        {
            case STATE.ATTACKING:
                Gizmos.color = Color.red;
                break;

            case STATE.COOLDOWN:
                Gizmos.color = Color.green;
                break;

            case STATE.COMPLETE:
                Gizmos.color = Color.blue;
                break;
        }

        Gizmos.DrawLine(transform.position, transform.position + transform.forward);
    }
}

public class attack_controller : ICharacterController
{
    Transform target;
    float timer = 0;
    string info = "Attacking";

    Vector3 attack_position;
    Vector3 attack_direction;

    public bool triggered { get; private set; }
    public void trigger(character c)
    {
        triggered = true;
        attack_position = target.position;
        attack_direction = (target.position - c.transform.position).normalized;
    }

    public attack_controller(Transform target)
    {
        this.target = target;
    }

    public void control(character c)
    {
        // Increment the timer
        timer += Time.deltaTime;

        // Attack is over
        if (timer > c.attack_time)
        {
            float cooldown = timer - c.attack_time;
            if (cooldown < c.attack_cooldown)
            {
                info = "Attack on cooldown";
                return; // Attack on cooldown
            }

            // Attack has cooled down
            triggered = false;
            timer = 0;
        }

        float t = timer / c.attack_time;
        float s = Mathf.Sin(t * Mathf.PI);
        info = "Attacking " + t;

        c.transform.position = attack_position - attack_direction * (s + 0.5f);
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
    public void on_end_control(character c) { }
    public string inspect_info() { return info; }
}

public class default_character_control : ICharacterController
{
    ICharacterController subcontroller;

    public void control(character c)
    {
        if (subcontroller == null)
            subcontroller = new idle_wander();

        if (player.current == null) return;

        // Apply awareness modifications
        c.run_awareness_checks(player.current);

        if (subcontroller is idle_wander)
        {
            if (c.is_aware)
                switch (c.friendliness)
                {
                    case character.FRIENDLINESS.FRIENDLY:
                        break; // Just wander around idly

                    case character.FRIENDLINESS.AFRAID:
                        subcontroller = new flee_controller(player.current.transform);
                        break;

                    case character.FRIENDLINESS.AGRESSIVE:
                        subcontroller = new chase_controller(player.current.transform, player.current);
                        break;
                }
        }
        else
        {
            if (!c.is_aware)
                subcontroller = new idle_wander();
        }

        subcontroller.control(c);
    }

    public void on_end_control(character c) { subcontroller?.on_end_control(c); }
    public void draw_gizmos() { subcontroller?.draw_gizmos(); }
    public void draw_inspector_gui() { subcontroller?.draw_inspector_gui(); }
    public string inspect_info() { return subcontroller?.inspect_info(); }
}