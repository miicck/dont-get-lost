using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICharacterController
{
    void control(character c);
    void draw_gizmos();
    void draw_inspector_gui();
}

[RequireComponent(typeof(character_hitbox))]
public class character : networked, INotPathBlocking, IInspectable
{
    // A character is considered to have arrived at a point
    // if they are within this distance of it.
    public const float ARRIVE_DISTANCE = 0.25f;

    // Character behaviour
    public float walk_speed = 1f;
    public float run_speed = 4f;
    public int max_health = 10;
    public float pathfinding_resolution = 0.5f;
    public float height = 2f;
    public float agro_range = 5f;
    public float rotation_lerp_speed = 1f;
    public float idle_walk_distance = 4f;
    public float flee_distance = 4f;
    public float reach = 0.5f;
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
                _controller = new character_control_v2();
            return _controller;
        }
        set => _controller = value;
    }
    ICharacterController _controller;

    public enum FRIENDLINESS
    {
        AGRESSIVE,
        FRIENDLY,
        AFRAID
    }

    public void lerp_forward(Vector3 new_forward)
    {
        // Lerp forward look direction
        new_forward = Vector3.Lerp(transform.forward, new_forward, run_speed * Time.deltaTime);
        if (new_forward.magnitude > 10e-4) transform.forward = new_forward;
    }

    //##############//
    // IINspectable //
    //##############//

    public string inspect_info() { return name.Replace('_', ' ').capitalize(); }
    public Sprite main_sprite() { return null; }
    public Sprite secondary_sprite() { return null; }

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

    private void Start()
    {
        load_sounds();
        InvokeRepeating("slow_update", Random.Range(0, 1f), 1f);
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

    public void take_damage(int damage)
    {
        play_random_sound(character_sound.TYPE.INJURY);
        health.value -= damage;
        if (health.value <= 0)
        {
            foreach (var p in GetComponents<product>())
                p.create_in(player.current.inventory);
            delete();
        }
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

        health = new networked_variables.net_int();
        health.value = max_health;

        health.on_change = () =>
        {
            if (health.value >= max_health)
            {
                if (_healthbar != null)
                    Destroy(_healthbar.gameObject);
            }
            else
                healthbar.belongs_to = this;
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

    float last_attack_time = 0;
    public void melee_attack(player p)
    {
        if (Time.realtimeSinceStartup - last_attack_time > melee_cooldown)
        {
            p.take_damage(melee_damage);
            last_attack_time = Time.realtimeSinceStartup;
        }
    }

    //##############//
    // STATIC STUFF //
    //##############//

    const int AREA_PER_CHARACTER = 32 * 32;

    static HashSet<character> characters;

    public static int target_character_count
    {
        get
        {
            return (int)(Mathf.PI * game.render_range * game.render_range /
                AREA_PER_CHARACTER);
        }
    }

    public static void initialize()
    {
        characters = new HashSet<character>();
    }

    public static void run_spawning()
    {
        if (characters.Count < target_character_count)
            character_spawn_point.spawn();
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
    character character
    {
        get => _character;
        set
        {
            if (_character != null && _character != value)
                throw new System.Exception("Cannot switch control!");
            _character = value;
        }
    }
    character _character;

    public void control(character c)
    {
        character = c;

        // Within agro range
        if ((character.transform.position - player.current.transform.position).magnitude < character.agro_range)
        {
            switch (character.friendliness)
            {
                case character.FRIENDLINESS.AGRESSIVE:
                    chase(player.current.transform);
                    break;

                case character.FRIENDLINESS.AFRAID:
                    flee(player.current.transform);
                    break;

                default:
                    idle_walk();
                    break;
            }
        }
        else
            idle_walk();
    }

    public void draw_gizmos() { path?.draw_gizmos(); }

    public void draw_inspector_gui()
    {
#if UNITY_EDITOR
        UnityEditor.EditorGUILayout.TextArea(path?.info_text());
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
        Vector3 ret = pathfinding_utils.validate_walking_position(v, resolution, out valid);
        if (!is_allowed_at(ret)) valid = false;
        return ret;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            resolution, character.height, resolution / 2f);
    }

    public float resolution { get => character.pathfinding_resolution; }

    //#############//
    // PATHFINDING //
    //#############//

    // The current path the character is walking
    path path
    {
        get => _path;
        set
        {
            _path = value;
            path_progress = 0;
        }
    }
    path _path;
    int path_progress = 0;

    bool move_towards(Vector3 point, float speed)
    {
        // Work out how far to the point
        Vector3 delta = point - character.transform.position;
        float dis = Time.deltaTime * speed;

        Vector3 new_pos = character.transform.position;

        if (delta.magnitude < dis) new_pos += delta;
        else new_pos += delta.normalized * dis;

        if (!is_allowed_at(new_pos)) return false;
        character.transform.position = new_pos;

        // Look in the direction of travel
        delta.y = 0;
        if (delta.magnitude > 10e-4)
        {
            // Lerp forward look direction
            Vector3 new_forward = Vector3.Lerp(character.transform.forward,
                delta.normalized, character.rotation_lerp_speed * speed * Time.deltaTime);

            if (new_forward.magnitude > 10e-4)
                character.transform.forward = new_forward;
        }
        return true;
    }

    void move_along_path(float speed)
    {
        switch (path.state)
        {
            case path.STATE.SEARCHING:
                // Run pathfinding (walking back+forth along the last path
                // whilst we do so)
                path.pathfind(load_balancing.iter);
                return;

            case path.STATE.FAILED:
                // Path failed, diffuse around a little to try and fix it
                path = null;
                character.transform.position += Random.onUnitSphere * 0.05f;
                return;

            case path.STATE.COMPLETE:

                if (path.length <= path_progress)
                {
                    // Path complete, reset
                    path = null;
                    return;
                }

                // Move towards the next path point
                if (!move_towards(path[path_progress], speed))
                {
                    // Couldn't walk along the path
                    path = null;
                    return;
                }

                Vector3 delta = path[path_progress] - character.transform.position;

                // Increment progress if we've arrived at the next path point
                if (delta.magnitude < character.ARRIVE_DISTANCE) ++path_progress;
                return;

            default:
                throw new System.Exception("Unkown path state!");
        }
    }

    // Just idly wonder around
    void idle_walk()
    {
        if (path == null)
        {
            random_path.success_func f = (v) =>
                (v - character.transform.position).magnitude > character.idle_walk_distance;
            path = new random_path(character.transform.position, f, f, this);
        }
        else move_along_path(character.walk_speed);
    }

    // Run from the given transform
    void flee(Transform fleeing_from)
    {
        character.play_idle_sounds();

        if (path == null)
        {
            Vector3 delta = (character.transform.position - fleeing_from.position).normalized;
            Vector3 flee_to = character.transform.position + delta * character.flee_distance;

            if (is_allowed_at(flee_to))
                // Flee away
                path = new astar_path(character.transform.position, flee_to, this);
            else
                // Flee back the way we came
                path = new astar_path(character.transform.position,
                    fleeing_from.position - delta * character.flee_distance, this);
        }
        else move_along_path(character.run_speed);
    }

    void chase(Transform chasing)
    {
        character.play_idle_sounds();

        if (path == null)
        {
            Vector3 delta = chasing.transform.position - character.transform.position;

            if (delta.magnitude < character.pathfinding_resolution)
            {
                if (delta.magnitude > character.reach)
                    move_towards(chasing.position, delta.magnitude);
                else
                    melee_attack(chasing);
                return;
            }

            if (is_allowed_at(chasing.position))
                path = new chase_path(character.transform.position, chasing, this);
            else
                idle_walk();
        }
        else move_along_path(character.run_speed);
    }

    float last_attacked = 0;

    void melee_attack(Transform attacking)
    {
        if (Time.realtimeSinceStartup - last_attacked > character.melee_cooldown)
        {
            last_attacked = Time.realtimeSinceStartup;
            attacking.GetComponent<player>()?.take_damage(character.melee_damage);
        }
    }
}

//######################//
// CHARACTER CONTROL V2 //
//######################//

public class character_control_v2 : ICharacterController, IPathingAgent
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

    // The path that we chase the player along
    path chase_path
    {
        get => _chase_path;
        set
        {
            _chase_path = value;
            chase_path_progress = 0;
        }
    }
    path _chase_path;
    int chase_path_progress;

    // Where, in the idle path, the chase path begins
    int chase_path_idle_path_link = 0;

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

        // If we're not already chasing a player/a player is in agro range
        // then chase the player
        if (chase_path == null &&
            (player.current.transform.position - c.transform.position).magnitude <
            c.agro_range)
        {
            chase_path = new chase_path(idle_path[idle_path_point],
                player.current.transform, this,
                max_iterations: 100,
                goal_distance: character.reach * 0.8f);
            chase_path_idle_path_link = idle_path_point;
        }

        if (chase_path != null)
            switch (chase_path.state)
            {
                // Chase path failed, reset to no chasing
                case path.STATE.FAILED:
                    chase_path = null;
                    break;

                // Continue chase pathfinding
                case path.STATE.SEARCHING:
                    chase_path.pathfind(load_balancing.iter);
                    break;

                // Chase the player
                case path.STATE.COMPLETE:

                    if (idle_path_point != chase_path_idle_path_link)
                    {
                        // Get to the point in the idle path where the
                        // chase path starts from
                        if (move_towards(idle_path[idle_path_point],
                            character.run_speed, out bool failed))
                        {
                            if (failed) chase_path = null;
                            int dir = chase_path_idle_path_link - idle_path_point;
                            if (dir > 0) idle_path_point += 1;
                            else if (dir < 0) idle_path_point -= 1;
                        }
                    }
                    else
                    {
                        float chase_speed = character.run_speed;
                        if (chase_path_progress == chase_path.length - 1)
                        {
                            // Close to the player, slow down 
                            // just enough to keep up
                            chase_speed = Mathf.Min(
                                character.run_speed,
                                player.BASE_SPEED * 1.2f);
                        }

                        if (move_towards(chase_path[chase_path_progress],
                            chase_speed, out bool failed))
                        {
                            if (failed) chase_path = null;
                            chase_path_progress = Mathf.Min(
                                chase_path_progress + 1,
                                chase_path.length - 1);
                        }

                        // Attack the player if we're close enough
                        if ((player.current.transform.position -
                            character.transform.position).magnitude < character.reach)
                            character.melee_attack(player.current);
                    }
                    return;

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

            // Couldn't create an idle path, try again
            case path.STATE.FAILED:
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

                character.transform.rotation = Quaternion.LookRotation(
                    new_forward,
                    up.normalized
                );
            }
        }

        return (point - new_pos).magnitude < character.ARRIVE_DISTANCE;
    }

    public void draw_gizmos()
    {
        idle_path?.draw_gizmos();
        chase_path?.draw_gizmos();
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
        Vector3 ret = pathfinding_utils.validate_walking_position(v, resolution, out valid);
        if (!is_allowed_at(ret)) valid = false;
        return ret;
    }

    public bool validate_move(Vector3 a, Vector3 b)
    {
        return pathfinding_utils.validate_walking_move(a, b,
            resolution, character.height, resolution / 2f);
    }

    public float resolution { get => character.pathfinding_resolution; }
}
