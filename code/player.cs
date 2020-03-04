using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class player : MonoBehaviour
{
    // Dimensions of a player
    public const float HEIGHT = 1.8f;
    public const float WIDTH = 0.45f;
    public const float EYE_HEIGHT = HEIGHT - WIDTH / 2;
    public const float SPEED = 10f;
    public const float JUMP_VEL = 5f;
    public const float GROUND_TEST_DIST = 0.15f;
    public const int MAX_MOVE_PROJ_REMOVE = 4;

    public const float MAP_CAMERA_ALT = world.MAX_ALTITUDE * 2;
    public const float MAP_CAMERA_CLIP = world.MAX_ALTITUDE * 3;
    public const float MAP_SHADOW_DISTANCE = world.MAX_ALTITUDE * 3;
    public const float MAP_OBSCURER_ALT = world.MAX_ALTITUDE * 1.5f;

    new Camera camera;
    GameObject obscurer;
    GameObject map_obscurer;

    public void update_render_range()
    {
        obscurer.transform.localScale = Vector3.one * game.render_range;
        map_obscurer.transform.localScale = Vector3.one * game.render_range;
        if (!map_open)
        {
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

    float yvel = 0;

    void OnDrawGizmos()
    {
        if (grounded) Gizmos.color = Color.green;
        else Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(upperSpherePosition, WIDTH / 2);
        Gizmos.DrawWireSphere(lowerSpherePosition, WIDTH / 2);
    }

    void tryMove(Vector3 move, int attempts = 1)
    {
        // Max attempts = The number of projections that 
        // cause collisions which can be removed from "move"
        // before the whole move is rejected.
        if (attempts > MAX_MOVE_PROJ_REMOVE) return;

        // Check if this move will cause a collision
        RaycastHit hit;
        if (Physics.CapsuleCast(
            lowerSpherePosition, upperSpherePosition,
            WIDTH / 2, move.normalized, out hit, move.magnitude))
        {
            // Remove the offending projection of 
            // the proposed move and try again
            move -= 1.01f * Vector3.Project(move, hit.normal);
            tryMove(move, attempts + 1);
            return;
        }

        // Make the move
        transform.position += move;
    }

    void Update()
    {
        // Toggle the map view
        if (Input.GetKeyDown(KeyCode.M))
            map_open = !map_open;

        if (map_open)
        {
            // Zoom the map
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0) camera.orthographicSize /= 1.2f;
            else if (scroll < 0) camera.orthographicSize *= 1.2f;
            if (camera.orthographicSize > 2 * game.render_range)
                camera.orthographicSize = 2 * game.render_range;

            // Pan the map
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            camera.transform.position += new Vector3(dx, 0, dy);

            return; // If map is open, don't move the player
        }

        // Gravity/Jumping
        if (!grounded) yvel -= 9.81f * Time.deltaTime;
        else if (yvel < 0) yvel = 0;
        else if (Input.GetKeyDown(KeyCode.Space)) yvel = JUMP_VEL;

        tryMove(yvel * Vector3.up * Time.deltaTime);

        // Move in the x-z plane using WASD
        float lr = 0;
        float fb = 0;

        if (Input.GetKey(KeyCode.W)) fb += 1.0f;
        if (Input.GetKey(KeyCode.A)) lr -= 1.0f;
        if (Input.GetKey(KeyCode.S)) fb -= 1.0f;
        if (Input.GetKey(KeyCode.D)) lr += 1.0f;

        Vector3 move = transform.right * lr + transform.forward * fb;
        tryMove(move * Time.deltaTime * SPEED);

        // Rotate the view using the mouse
        // Note that horizontal moves rotate the player
        // vertical moves rotate the camera
        transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
        camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
    }

    Quaternion saved_camera_rotation;
    public bool map_open
    {
        get { return camera.transform.parent == null; }
        set
        {
            if (value)
            {
                saved_camera_rotation = camera.transform.localRotation;

                obscurer.SetActive(false);
                map_obscurer.SetActive(true);

                Vector3 obsc_pos = transform.position;
                obsc_pos.y = MAP_OBSCURER_ALT;
                map_obscurer.transform.position = obsc_pos;                

                camera.orthographic = true;
                camera.transform.SetParent(null);
                Vector3 pos = camera.transform.position;

                pos.y = MAP_CAMERA_ALT;
                camera.farClipPlane = MAP_CAMERA_CLIP;
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;

                camera.transform.position = pos;
                camera.transform.forward = Vector3.down;
            }
            else
            {
                obscurer.SetActive(true);
                map_obscurer.SetActive(false);
                camera.orthographic = false;
                camera.transform.SetParent(transform);
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

        // Move the player above the first map chunk so they
        // dont fall off of the map
        player.transform.position = Vector3.up * world.MAX_ALTITUDE;

        // Enforce the render limit with a sky-color object
        player.obscurer = Resources.Load<GameObject>("misc/obscurer").inst();
        player.obscurer.transform.SetParent(player.transform);
        player.obscurer.transform.localPosition = Vector3.zero;
        var sky_color = player.obscurer.GetComponentInChildren<Renderer>().material.color;

        player.map_obscurer = Resources.Load<GameObject>("misc/map_obscurer").inst();
        player.map_obscurer.transform.SetParent(player.transform);

        // Make the sky the same color as the obscuring object
        RenderSettings.skybox = null;
        player.camera.backgroundColor = sky_color;

        // Initialize the render range
        player.update_render_range();

        // Start with the map closed
        player.map_open = false;

        return player;
    }
}