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
    public const float ROTATION_SPEED = 90f;
    public const float JUMP_VEL = 5f;
    public const float GROUND_TEST_DIST = 0.15f;
    public const float TERRAIN_SINK_ALLOW = 0.2f;
    public const float TERRAIN_SINK_RESET_DIST = GROUND_TEST_DIST;
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
        // Toggle the map view
        if (Input.GetKeyDown(KeyCode.M))
            map_open = !map_open;

        // Don't go below the terrain
        stay_above_terrain();

        if (map_open)
        {
            // Set the map to the correct size
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
        if (!grounded) yvel -= 9.81f * Time.deltaTime;
        else if (yvel < 0) yvel = 0;
        else if (Input.GetKeyDown(KeyCode.Space)) yvel = JUMP_VEL;

        tryMove(yvel * Vector3.up * Time.deltaTime);

        // Move in the x-z plane using WASD
        float lr = 0;
        float fb = 0;

        if (Input.GetKey(KeyCode.W)) fb += 1.0f;
        if (Input.GetKey(KeyCode.S)) fb -= 1.0f;
        if (Input.GetKey(KeyCode.A)) lr -= 1.0f;
        if (Input.GetKey(KeyCode.D)) lr += 1.0f;

        Vector3 move = transform.forward * fb;
        if (map_open) transform.Rotate(0, lr * Time.deltaTime * ROTATION_SPEED, 0);
        else move += transform.right * lr; // Strafing 

        float speed = SPEED;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= 10;
        tryMove(move * Time.deltaTime * speed);
    }

    Quaternion saved_camera_rotation;
    public bool map_open
    {
        get { return camera.orthographic; }
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
                camera.orthographicSize = game.render_range;
                camera.transform.localPosition = Vector3.up * (MAP_CAMERA_ALT - transform.position.y);
                camera.transform.localRotation = Quaternion.Euler(90, 0, 0);

                camera.farClipPlane = MAP_CAMERA_CLIP;
                QualitySettings.shadowDistance = MAP_SHADOW_DISTANCE;
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
        player.transform.position = new Vector3(0, world.SEA_LEVEL + 1, 0);

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