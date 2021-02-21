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

public class character : networked, INotPathBlocking, IDontBlockItemLogisitcs, IAcceptsDamage, IPlayerInteractable
{
    // A character is considered to have arrived at a point
    // if they are within this distance of it.
    public const float ARRIVE_DISTANCE = 0.25f;

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

    private void OnDrawGizmosSelected()
    {
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

    public override bool persistant()
    {
        // Characters despawn when not loaded
        return false;
    }

    networked_variables.net_float y_rotation;
    networked_variables.net_int health;

    public int remaining_health { get => health.value; }

    public void heal(int amount)
    {
        int max_heal = max_health - health.value;
        health.value += Mathf.Min(amount, max_heal);
    }

    public void take_damage(int damage)
    {
        int health_before = health.value;
        var hm = hit_marker.create("-" + damage);
        hm.transform.position = transform.position + Vector3.up * height;

        play_random_sound(character_sound.TYPE.INJURY);
        health.value -= damage;

        if (health.value <= 0 && health_before > 0)
        {
            // Create loot in player inventory only on this client
            foreach (var p in GetComponents<product>())
                p.create_in(player.current.inventory);
        }
    }

    public class dead_character : MonoBehaviour, INotPathBlocking
    {
        private void Start()
        {
            InvokeRepeating("gradual_decay", 1f, 1f);
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
                    Destroy(c);
                }

                // Make the copy a child of this dead_character and
                // give it a simple collider
                rcopy.transform.SetParent(transform);
                var bc = rcopy.gameObject.AddComponent<BoxCollider>();
                bc.size = 0.1f * new Vector3(
                    1f / rcopy.transform.localScale.x,
                    1f / rcopy.transform.localScale.y,
                    1f / rcopy.transform.localScale.z
                );

                // Make the character invisisble (do this after
                // we copy, so the copied version isn't invisible)
                r.enabled = false;
            }

            // Delay rigidbodies so they have time to register the new box colliders
            Invoke("add_rigidbodies", 0.1f);
        }

        void add_rigidbodies()
        {
            foreach (Transform c in transform)
                c.gameObject.AddComponent<Rigidbody>();
        }

        public static dead_character create(character to_copy)
        {
            var dead_version = new GameObject("dead_" + to_copy.name).AddComponent<dead_character>();
            dead_version.transform.position = to_copy.transform.position;
            dead_version.transform.rotation = to_copy.transform.rotation;
            dead_version.on_create(to_copy);
            return dead_version;
        }
    }

    bool dead = false;
    void die()
    {
        if (!dead)
        {
            dead = true;
            dead_character.create(this);
            on_death();
            Invoke("delayed_delete", 1f);
        }
    }

    protected virtual void on_death() { }

    void delayed_delete()
    {
        delete();
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

public class default_character_control : ICharacterController, IPathingAgent
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
            idle_path = new random_path(character.transform.position, f, f, this);
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
                        player.current.transform, this,
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
                    agro_path = new flee_path(character.transform.position, player.current.transform, this);

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
                        if (move_towards(idle_path[idle_path_point],
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
                                    agro_path = new flee_path(character.transform.position, player.current.transform, this);
                                    return;
                            }
                        }

                        if (move_towards(agro_path[agro_path_progress],
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
                if (move_towards(idle_path[idle_path_point],
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

    bool move_towards(Vector3 point, float speed, out bool failed)
    {
        // Work out how far to the point
        Vector3 delta = point - character.transform.position;
        float dis = Time.deltaTime * speed;

        Vector3 new_pos = character.transform.position;

        if (delta.magnitude < dis) new_pos += delta;
        else new_pos += delta.normalized * dis;

        failed = false;
        if (!is_allowed_at(new_pos))
        {
            failed = true;
            return false;
        }

        // Move along to the new position
        character.transform.position = new_pos;

        // Look in the direction of travel
        delta.y = 0;
        if (delta.magnitude > 10e-4)
        {
            // Lerp forward look direction
            Vector3 new_forward = Vector3.Lerp(
                character.transform.forward,
                delta.normalized,
                character.rotation_lerp_speed * speed * Time.deltaTime
            );

            if (new_forward.magnitude > 10e-4)
            {
                // Set up direction with reference to legs
                Vector3 up = Vector3.zero;
                if (character.align_to_terrain)
                    foreach (var l in character.GetComponentsInChildren<leg>())
                        up += l.ground_normal;
                else up = Vector3.up;

                up = Vector3.Lerp(
                    character.transform.up,
                    up.normalized,
                    character.rotation_lerp_speed * speed * Time.deltaTime
                );

                new_forward -= Vector3.Project(new_forward, up);
                character.transform.rotation = Quaternion.LookRotation(
                    new_forward,
                    up.normalized
                );
            }
        }

        return (point - new_pos).magnitude < character.ARRIVE_DISTANCE;
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

    //###############//
    // IPathingAgent //
    //###############//

    protected virtual bool is_allowed_at(Vector3 v)
    {
        // Check we're in the right medium
        if (!character.can_swim && v.y < world.SEA_LEVEL) return false;
        if (!character.can_walk && v.y > world.SEA_LEVEL) return false;
        return true;
    }

    public Vector3 validate_position(Vector3 v, out bool valid)
    {
        if (v.y < world.SEA_LEVEL)
        {
            v.y = world.SEA_LEVEL;
            valid = character.can_swim;
            return v;
        }

        Vector3 ret = pathfinding_utils.validate_walking_position(v, resolution, out valid);
        if (!is_allowed_at(ret)) valid = false;
        return ret;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            resolution, character.height, resolution / 2f);
    }

    public float resolution => character.pathfinding_resolution;
}
