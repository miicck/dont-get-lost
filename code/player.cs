using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// An object that the player can interact with
public class interactable : MonoBehaviour
{
    [System.Flags]
    public enum FLAGS
    {
        NONE = 0,
        DISALLOWS_MOVEMENT = 2,
        DISALLOWS_ROTATION = 4,
    };

    public virtual string cursor() { return cursors.DEFAULT_INTERACTION; }
    public virtual FLAGS player_interact() { return FLAGS.NONE; }
    public virtual void on_start_interaction(RaycastHit point_hit) { }
    public virtual void on_end_interaction() { }
    protected void stop_interaction() { player.current.interacting_with = null; }
}

public class player : MonoBehaviour
{
    //###########//
    // CONSTANTS //
    //###########//

    public const float HEIGHT = 1.8f;
    public const float WIDTH = 0.45f;
    public const float GRAVITY = 10f;
    public const float BOUYANCY = 5f;
    public const float WATER_DRAG = 1.5f;

    public const float SPEED = 10f;
    public const float ACCELERATION_TIME = 0.2f;
    public const float ACCELERATION = SPEED / ACCELERATION_TIME;
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;

    public const float INTERACTION_RANGE = 3f;

    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;

    //###############//
    // SERIALIZATION //
    //###############//

    public void save()
    {
        var floats = new float[]
        {
            transform.position.x,
            transform.position.y,
            transform.position.z
        };

        using (var fs = new System.IO.FileStream(world.save_folder() + "/player",
            System.IO.FileMode.Create, System.IO.FileAccess.Write))
        {
            for (int i = 0; i < floats.Length; ++i)
            {
                var float_bytes = System.BitConverter.GetBytes(floats[i]);
                fs.Write(float_bytes, 0, float_bytes.Length);
            }
        }
    }

    void load()
    {
        if (!System.IO.File.Exists(world.save_folder() + "/player")) return;

        var floats = new float[3];

        using (var fs = new System.IO.FileStream(world.save_folder() + "/player",
            System.IO.FileMode.Open, System.IO.FileAccess.Read))
        {
            byte[] float_bytes = new byte[sizeof(float)];
            for (int i = 0; i < floats.Length; ++i)
            {
                fs.Read(float_bytes, 0, float_bytes.Length);
                floats[i] = System.BitConverter.ToSingle(float_bytes, 0);
            }
        }

        Vector3 pos = new Vector3(floats[0], floats[1], floats[2]);
        transform.position = pos;
    }

    //#################//
    // UNITY CALLBACKS //
    //#################//

    void Update()
    {
        var inter_flags = interact();

        // Toggle the map view
        if (Input.GetKeyDown(KeyCode.M))
            map_open = !map_open;

        if (map_open)
        {
            // Zoom the map
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) game.render_range_target /= 1.2f;
            else if (scroll < 0) game.render_range_target *= 1.2f;
            camera.orthographicSize = game.render_range;
        }

        if (inter_flags.HasFlag(interactable.FLAGS.DISALLOWS_MOVEMENT))
            velocity = Vector3.zero;
        else move();

        if (!inter_flags.HasFlag(interactable.FLAGS.DISALLOWS_ROTATION))
            mouse_look();

        // Float in water
        float amt_submerged = (world.SEA_LEVEL - transform.position.y) / HEIGHT;
        if (amt_submerged > 1.0f) amt_submerged = 1.0f;
        if (amt_submerged > 0)
        {
            // Bouyancy (sink if shift is held)
            if (!Input.GetKey(KeyCode.LeftShift))
                velocity.y += amt_submerged * (GRAVITY + BOUYANCY) * Time.deltaTime;

            // Drag
            velocity -= velocity * amt_submerged * WATER_DRAG * Time.deltaTime;
        }

        underwater_screen.SetActive(camera.transform.position.y < world.SEA_LEVEL && !map_open);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, game.render_range);
    }

    //##################//
    // ITEM INTERACTION //
    //##################//

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

    interactable.FLAGS interact()
    {
        // Interact with the current object
        if (interacting_with != null)
        {
            canvas.cursor = interacting_with.cursor();
            return interacting_with.player_interact();
        }

        // See if an interactable object is under the cursor
        var inter = utils.raycast_for_closest<interactable>(
            camera_ray(), out last_interaction_hit, INTERACTION_RANGE);

        if (inter == null)
        {
            canvas.cursor = cursors.DEFAULT;
            return interactable.FLAGS.NONE;
        }
        else canvas.cursor = cursors.DEFAULT_INTERACTION;

        // Set the interactable and cursor,
        // interact with the object
        if (Input.GetMouseButtonDown(0))
            interacting_with = inter;

        return interactable.FLAGS.NONE;
    }

    //###########//
    //  MOVEMENT //
    //###########//

    CharacterController controller;
    Vector3 velocity = Vector3.zero;

    void move()
    {
        if (controller.isGrounded)
        {
            if (velocity.y < 0) velocity.y = 0;
            if (Input.GetKeyDown(KeyCode.Space))
                velocity.y += JUMP_VEL;
        }
        else velocity.y -= GRAVITY * Time.deltaTime;

        if (Input.GetKey(KeyCode.W)) velocity += transform.forward * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.S)) velocity -= transform.forward * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, transform.forward);

        if (Input.GetKey(KeyCode.D)) velocity += camera.transform.right * ACCELERATION * Time.deltaTime;
        else if (Input.GetKey(KeyCode.A)) velocity -= camera.transform.right * ACCELERATION * Time.deltaTime;
        else velocity -= Vector3.Project(velocity, camera.transform.right);

        float xz = new Vector3(velocity.x, 0, velocity.z).magnitude;
        if (xz > SPEED)
        {
            velocity.x *= SPEED / xz;
            velocity.z *= SPEED / xz;
        }

        Vector3 move = velocity * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            move.x *= 10f;
            move.z *= 10f;
        }

        controller.Move(move);

        stay_above_terrain();
    }

    void stay_above_terrain()
    {
        Vector3 pos = transform.position;
        if (pos.y > 0) return;
        pos.y = world.MAX_ALTITUDE;
        RaycastHit hit;
        var tc = utils.raycast_for_closest<TerrainCollider>(new Ray(pos, Vector3.down), out hit);
        transform.position = hit.point;
    }

    //#####################//
    // VIEW/CAMERA CONTROL //
    //#####################//

    // Objects used to obscure player view
    public new Camera camera { get; private set; }
    GameObject obscurer;
    GameObject map_obscurer;
    GameObject underwater_screen;

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

    void mouse_look()
    {
        if (map_open)
        {
            // Rotate the player with A/D
            float xr = 0;
            if (Input.GetKey(KeyCode.A)) xr = -1f;
            else if (Input.GetKey(KeyCode.D)) xr = 1.0f;
            transform.Rotate(0, xr * Time.deltaTime * ROTATION_SPEED, 0);
            return;
        }

        // Rotate the view using the mouse
        // Note that horizontal moves rotate the player
        // vertical moves rotate the camera
        transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
        camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
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
                camera.transform.localPosition = Vector3.up * (controller.height - controller.radius);
                camera.transform.localRotation = saved_camera_rotation;
            }
        }
    }

    // Return a ray going through the centre of the screen
    public Ray camera_ray()
    {
        return new Ray(camera.transform.position,
                       camera.transform.forward);
    }

    //################//
    // STATIC METHODS //
    //################//

    // The current player
    public static player current;

    // Create and return a player
    public static player create()
    {
        var player = new GameObject("player").AddComponent<player>();
        player.controller = player.gameObject.AddComponent<CharacterController>();
        player.controller.height = HEIGHT;
        player.controller.radius = WIDTH / 2;
        player.controller.center = new Vector3(0, player.controller.height / 2f, 0);

        // Create the player camera 
        player.camera = new GameObject("camera").AddComponent<Camera>();
        player.camera.clearFlags = CameraClearFlags.SolidColor;
        player.camera.transform.SetParent(player.transform);
        player.camera.transform.localPosition = new Vector3(0, player.controller.height - player.controller.radius, 0);
        player.camera.nearClipPlane = 0.1f;

        // Create a short range light with no shadows to light up detail
        // on nearby objects to the player
        var point_light = new GameObject("point_light").AddComponent<Light>();
        point_light.type = LightType.Point;
        point_light.range = item.WELD_RANGE;
        point_light.transform.SetParent(player.camera.transform);
        point_light.transform.localPosition = Vector3.zero;
        point_light.intensity = 0.5f;

        // Enforce the render limit with a sky-color object
        player.obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        player.obscurer.transform.SetParent(player.transform);
        player.obscurer.transform.localPosition = Vector3.zero;
        var sky_color = player.obscurer.GetComponentInChildren<Renderer>().material.color;

        player.map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        player.map_obscurer.transform.SetParent(player.camera.transform);
        player.map_obscurer.transform.localPosition = Vector3.forward;
        player.map_obscurer.transform.up = -player.camera.transform.forward;

        player.underwater_screen = Resources.Load<GameObject>("misc/underwater_screen").inst();
        player.underwater_screen.transform.SetParent(player.camera.transform);
        player.underwater_screen.transform.localPosition = Vector3.forward * player.camera.nearClipPlane * 1.1f;
        player.underwater_screen.transform.forward = player.camera.transform.forward;

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        player.camera.backgroundColor = sky_color;

        // Initialize the render range
        player.update_render_range();

        // Start with the map closed
        player.map_open = false;

        // Load the player state
        player.load();

        current = player;
        return player;
    }
}