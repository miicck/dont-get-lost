using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// An object that the player can interact with
public class interactable : MonoBehaviour
{
    public virtual string cursor() { return cursors.DEFAULT_INTERACTION; }
    public virtual void player_interact() { }
    public virtual void on_start_interaction(RaycastHit point_hit) { }
    public virtual void on_end_interaction() { }
    protected void stop_interaction() { player.current.interacting_with = null; }
}

public class player : MonoBehaviour
{
    // Dimensions of a player
    public const float HEIGHT = 1.8f;
    public const float WIDTH = 0.45f;
    public const float EYE_HEIGHT = HEIGHT - WIDTH / 2;
    public const float SPEED = 10f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float GROUND_TEST_DIST = 0.15f;
    public const float TERRAIN_SINK_ALLOW = 0.2f;
    public const float TERRAIN_SINK_RESET_DIST = GROUND_TEST_DIST;
    public const float INTERACTION_RANGE = 3f;
    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;
    public const float MAP_OBSCURER_ALT = world.MAX_ALTITUDE * 1.5f;
    public const int MAX_MOVE_PROJ_REMOVE = 4;

    public static player current;

    public new Camera camera { get; private set; }

    GameObject obscurer;
    GameObject map_obscurer;

    // Called when the render range changes
    public void update_render_range()
    {
        // Set the obscurer size to the render range
        obscurer.transform.localScale = Vector3.one * game.render_range;
        map_obscurer.transform.localScale = Vector3.one * game.render_range;

        if (!map_open)
        {
            // If in 3D mode, set the camera clipping plane range to
            // the same as render_range
            camera.farClipPlane = game.render_range;
            QualitySettings.shadowDistance = camera.farClipPlane;
        }
    }

    // The position of the upper sphere of the player
    // (used for capsule-based collision)
    public Vector3 upperSpherePosition
    {
        get { return transform.position + Vector3.up * (HEIGHT - WIDTH / 2); }
    }

    // The position of the lower sphere of the player
    // (used for capsule-based collision)
    public Vector3 lowerSpherePosition
    {
        get { return transform.position + Vector3.up * WIDTH / 2; }
    }

    // Returns true if the player is on the ground
    bool grounded
    {
        get
        {
            return Physics.CapsuleCast(
                lowerSpherePosition, upperSpherePosition,
                WIDTH / 2, Vector3.down, GROUND_TEST_DIST);
        }
    }

    void OnDrawGizmos()
    {
        if (grounded) Gizmos.color = Color.green;
        else Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperSpherePosition, WIDTH / 2);
        Gizmos.DrawWireSphere(lowerSpherePosition, WIDTH / 2);
    }

    // Force the player to stay slightly above the terrain
    void stay_above_terrain()
    {
        if (transform.position.y < 0)
        {
            Vector3 pos = transform.position;
            pos.y = 0;
            transform.position = pos;
        }

        var hits = Physics.RaycastAll(
            transform.position + Vector3.up * world.MAX_ALTITUDE,
            Vector3.down, world.MAX_ALTITUDE + TERRAIN_SINK_ALLOW);
        foreach (var hit in hits)
            if (hit.collider != null)
            {
                var terr = hit.collider.gameObject.GetComponent<Terrain>();
                if (terr != null)
                {
                    Vector3 pos = transform.position;
                    pos.y = hit.point.y + TERRAIN_SINK_RESET_DIST;
                    transform.position = pos;
                }
            }
    }

    void Update()
    {
        interact();
        move();
    }

    // The object we are currently interacting with
    RaycastHit last_interaction_hit;
    interactable _interacting_with;
    public interactable interacting_with
    {
        get { return _interacting_with; }
        set
        {
            if (_interacting_with != null)
                _interacting_with.on_end_interaction();

            _interacting_with = value;

            if (value != null)
                value.on_start_interaction(last_interaction_hit);
        }
    }

    void interact()
    {
        // Interact with the current object
        if (interacting_with != null)
        {
            canvas.cursor = interacting_with.cursor();
            interacting_with.player_interact();
            return;
        }

        // See if an interactable object is under the cursor
        var inter = utils.raycast_for_closest<interactable>(
            camera_ray(), out last_interaction_hit, INTERACTION_RANGE);

        if (inter == null)
        {
            canvas.cursor = cursors.DEFAULT;
            return;
        }
        else canvas.cursor = cursors.DEFAULT_INTERACTION;

        // Set the interactable and cursor,
        // interact with the object
        if (Input.GetMouseButtonDown(0))
            interacting_with = inter;
    }

    // The players current velocity
    // in player-local coordinates
    Vector3 local_velocity;

    // Global velocity from local velocty
    public Vector3 velocity
    {
        get
        {
            return local_velocity.x * transform.right +
                   local_velocity.z * transform.forward +
                   local_velocity.y * Vector3.up;
        }
    }

    void move()
    {
        // Toggle the map view
        if (Input.GetKeyDown(KeyCode.M))
            map_open = !map_open;

        // Don't go below the terrain
        stay_above_terrain();

        if (map_open)
        {
            // Zoom the map
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) game.render_range_target /= 1.2f;
            else if (scroll < 0) game.render_range_target *= 1.2f;
            camera.orthographicSize = game.render_range;
        }
        else
        {
            // Rotate the view using the mouse
            // Note that horizontal moves rotate the player
            // vertical moves rotate the camera
            transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
            camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
        }

        // Gravity/Jumping
        if (!grounded) local_velocity.y -= 9.81f * Time.deltaTime;
        else if (local_velocity.y < 0) local_velocity.y = 0;
        else if (Input.GetKeyDown(KeyCode.Space)) local_velocity.y = JUMP_VEL;

        // Move in the x-z plane using WASD
        float dv = Time.deltaTime * SPEED / ACCELERATION_TIME;
        if (Input.GetKey(KeyCode.W)) local_velocity.z += dv;
        else if (Input.GetKey(KeyCode.S)) local_velocity.z -= dv;
        else local_velocity.z = 0;

        if (Input.GetKey(KeyCode.A)) local_velocity.x -= dv;
        else if (Input.GetKey(KeyCode.D)) local_velocity.x += dv;
        else local_velocity.x = 0;

        local_velocity.x = Mathf.Clamp(local_velocity.x, -SPEED, SPEED);
        local_velocity.z = Mathf.Clamp(local_velocity.z, -SPEED, SPEED);

        if (map_open)
        {
            // If the map is open, don't strafe, rotate.
            transform.Rotate(0, local_velocity.x * Time.deltaTime * ROTATION_SPEED, 0);
            local_velocity.x = 0;
        }

        tryMove();
    }

    void tryMove(int attempts = 1)
    {
        // Max attempts = The number of projections that 
        // cause collisions which can be removed from "move"
        // before the whole move is rejected.
        if (attempts > MAX_MOVE_PROJ_REMOVE) return;

        Vector3 move = (local_velocity.x * transform.right +
                        local_velocity.z * transform.forward +
                        local_velocity.y * Vector3.up) * Time.deltaTime;

        // Check if this move will cause a collision
        RaycastHit hit;
        if (Physics.CapsuleCast(
            lowerSpherePosition, upperSpherePosition,
            WIDTH / 2, move.normalized, out hit, move.magnitude))
        {
            // Remove the offending projection of 
            // the proposed move (from both the move
            // and the local velocity) and try again
            Vector3 offending_proj = Vector3.Project(move, hit.normal);
            offending_proj.y = 0;
            move -= offending_proj;

            local_velocity.x = Vector3.Dot(move, transform.right);
            local_velocity.z = Vector3.Dot(move, transform.forward);
            local_velocity.y = move.y;
            local_velocity /= Time.deltaTime;

            tryMove(attempts + 1);
            return;
        }

        // Make the move
        transform.position += move;
    }

    // Return a ray going through the centre of the screen
    public Ray camera_ray()
    {
        return new Ray(camera.transform.position,
                       camera.transform.forward);
    }

    // Saved rotation to restore when we return to the 3D view
    Quaternion saved_camera_rotation;

    // True if in map view
    public bool map_open
    {
        get { return camera.orthographic; }
        set
        {
            // Use the appropriate obscurer for
            // the map or 3D views
            map_obscurer.SetActive(value);
            obscurer.SetActive(!value);

            // Set the camera orthograpic if in 
            // map view, otherwise perspective
            camera.orthographic = value;

            if (value)
            {
                // Save camera rotation to restore later
                saved_camera_rotation = camera.transform.localRotation;

                // Setup the camera in map mode/position   
                camera.orthographicSize = game.render_range;
                camera.transform.localPosition = Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.localRotation = Quaternion.Euler(90, 0, 0);
                camera.farClipPlane = MAP_CAMERA_CLIP;

                // Render shadows further in map view
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;
            }
            else
            {
                // Restore 3D camera view
                camera.transform.localPosition = Vector3.up * EYE_HEIGHT;
                camera.transform.localRotation = saved_camera_rotation;
            }
        }
    }

    // Create and return a player
    public static player create()
    {
        var player = new GameObject("player").AddComponent<player>();

        // Create the player camera 
        player.camera = new GameObject("camera").AddComponent<Camera>();
        player.camera.clearFlags = CameraClearFlags.SolidColor;
        player.camera.transform.SetParent(player.transform);

        // Move the player above the first map chunk so they
        // dont fall off of the map
        player.transform.position = new Vector3(0, world.SEA_LEVEL + 1, 0);

        // Enforce the render limit with a sky-color object
        player.obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        player.obscurer.transform.SetParent(player.transform);
        player.obscurer.transform.localPosition = Vector3.zero;
        var sky_color = player.obscurer.GetComponentInChildren<Renderer>().material.color;

        player.map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        player.map_obscurer.transform.SetParent(player.transform);
        Vector3 map_obsc_pos = player.transform.position;
        map_obsc_pos.y = MAP_OBSCURER_ALT;
        player.map_obscurer.transform.position = map_obsc_pos;

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        player.camera.backgroundColor = sky_color;

        // Initialize the render range
        player.update_render_range();

        // Start with the map closed
        player.map_open = false;

        current = player;
        return player;
    }
}