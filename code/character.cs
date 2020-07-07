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
public class character : networked, INotPathBlocking
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
    public float reach = 0.5f;
    public float melee_cooldown = 1f;
    public int melee_damage = 10;
    public FRIENDLINESS friendliness;
    public bool can_walk = true;
    public bool can_swim = false;
    public Transform projectile_target;

    /// <summary> The object currently controlling this character. </summary>
    public ICharacterController controller
    {
        get
        {
            // Default control
            if (_controller == null)
                _controller = new default_character_control();
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
        enemies.register_character(this);
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
                p.create_in_inventory(player.current.inventory);
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
                _healthbar.transform.position = transform.position + Vector3.up;
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
    path _path;
    path path
    {
        get { return _path; }
        set { _path = value; path_progress = 0; }
    }
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
                delta.normalized, speed * Time.deltaTime);

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
                // Run pathfinding
                path.pathfind(1);
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
            random_path.success_func f = (v) => (v - character.transform.position).magnitude > 10f;
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
            Vector3 flee_to = character.transform.position + delta * 5f;

            if (is_allowed_at(flee_to))
                // Flee away
                path = new astar_path(character.transform.position, flee_to, this); 
            else
                // Flee back the way we came
                path = new astar_path(character.transform.position, fleeing_from.position - delta * 5f, this);
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
                    move_towards(chasing.position, character.run_speed);
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