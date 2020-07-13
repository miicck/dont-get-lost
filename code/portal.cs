using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal : building_material, ILeftPlayerMenu
{
    public Transform path_start;

    public path path { get; private set; }

    bool path_success(Vector3 end)
    {
        return (end - path_start.position).magnitude > 32f;
    }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    RectTransform menu;

    public RectTransform left_menu_transform()
    {
        if (menu == null)
        {
            // Create the menu
            menu = Resources.Load<RectTransform>("ui/portal").inst();
            menu.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        }
        return menu;
    }

    public void on_left_menu_open()
    {
        var content = menu.gameObject.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

        // Destroy the old buttons
        foreach (Transform c in content)
        {
            var b = c.GetComponent<UnityEngine.UI.Button>();
            if (b != null) Destroy(b.gameObject);
        }

        // Load the new buttons
        FindObjectOfType<teleport_manager>().create_buttons(content);
    }

    public void on_left_menu_close() { }

    //#################//
    // Unity callbacks //
    //#################//

    private void Start()
    {
        InvokeRepeating("create_pulse", 0.2f, 0.2f);
        InvokeRepeating("spawn_attacker", 1f, 1f);
    }

    private void Update()
    {
        // Updates only happen on the authority client
        if (!has_authority)
            return;

        // Don't work out the path until the chunk is generated
        if (chunk.at(path_start.position, generated_only: true) == null)
            return;

        if (path == null || path.state == path.STATE.FAILED)
            path = new random_path(path_start.position, path_success, path_success, new portal_pather());
        else
        {
            switch (path.state)
            {
                case path.STATE.SEARCHING:
                    path.pathfind(load_balancing.iter);
                    break;

                case path.STATE.COMPLETE:
                    if (Time.frameCount % 2 == 0)
                        path.optimize(load_balancing.iter);
                    else if (!path.validate(load_balancing.iter))
                    {
                        Debug.Log("path failed validation: " + portal_pather.last_reason);
                        path = null;
                    }
                    break;
            }
        }
    }

    void spawn_attacker()
    {
        // Only spawn on authority client, when a path is available
        if (!has_authority) return;
        if (path == null || path.state != path.STATE.COMPLETE) return;

        if (Random.Range(0, 2) == 0)
            client.create(path[path.length - 1], "characters/smoke_spider", this);
        else
            client.create(path[path.length - 1], "characters/chicken", this);
    }

    public override void on_add_networked_child(networked child)
    {
        base.on_add_networked_child(child);

        // Child characters are controlled by the portal
        if (child is character)
        {
            var c = (character)child;
            c.controller = new portal_character_control(this);
        }
    }

    public override void on_first_create()
    {
        base.on_first_create();
        FindObjectOfType<teleport_manager>().register_portal(this);
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);
        if (deleted && has_authority)
            FindObjectOfType<teleport_manager>().unregister_portal(this);
    }

    void create_pulse()
    {
        if (path == null) return;
        if (path.state != path.STATE.COMPLETE) return;

        var pulse = new GameObject("pulse").AddComponent<portal_path_display>();
        pulse.transform.SetParent(transform);
        pulse.transform.position = path[path.length - 1];
        pulse.portal = this;
    }

    void OnDrawGizmosSelected()
    {
        path?.draw_gizmos();
    }

    class portal_pather : IPathingAgent
    {
        public static string last_reason;

        public Vector3 validate_position(Vector3 v, out bool valid)
        {
            return pathfinding_utils.validate_walking_position(v, resolution, out valid);
        }

        public bool validate_move(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            if (Vector3.Angle(delta, Vector3.up) < 40) return false; // Maximum incline of 50 degrees
            bool valid = pathfinding_utils.validate_walking_move(a, b, 1f, 2f, 0.5f, out string reason);
            if (!valid) last_reason = reason;
            return valid;
        }

        public float resolution { get => 0.5f; }
    }

    class portal_path_display : MonoBehaviour
    {
        const float SPEED = 10f;

        public portal portal;

        private void Start()
        {
            var trail = Resources.Load<GameObject>("particle_systems/portal_trail_renderer").inst();
            trail.transform.SetParent(transform);
            trail.transform.localPosition = Vector3.up;
        }

        int progress = 0;
        private void Update()
        {
            if (portal?.path == null || progress >= portal.path.length)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 targ = portal.path[portal.path.length - progress - 1];
            Vector3 delta = targ - transform.position;
            if (delta.magnitude < 0.25f)
                progress += 1;

            if (delta.magnitude > Time.deltaTime * SPEED)
                delta = delta.normalized * Time.deltaTime * SPEED;

            transform.position += delta;
        }
    }
}

/// <summary> Controlls characters attacking the portal. </summary>
public class portal_character_control : ICharacterController
{
    portal portal;
    character character;
    int progress;

    public portal_character_control(portal portal)
    {
        this.portal = portal;
    }

    public void control(character character)
    {
        this.character = character;

        if (portal.path == null ||
            portal.path.state != path.STATE.COMPLETE ||
            progress >= portal.path.length)
        {
            character.delete();
            return;
        }

        Vector3 next = portal.path[portal.path.length - 1 - progress];
        Vector3 delta = next - character.transform.position;
        if (delta.magnitude < character.ARRIVE_DISTANCE)
            progress += 1;

        delta = delta.clamp_magnitude(0, Time.deltaTime * character.walk_speed);
        character.transform.position += delta;
        character.lerp_forward(delta);
    }

    public void draw_gizmos() { }
    public void draw_inspector_gui() { }
}