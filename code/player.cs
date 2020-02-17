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
    public const int MAX_MOVE_PROJ_REMOVE= 4;

    new Camera camera;

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
                lowerSpherePosition,
                upperSpherePosition,
                WIDTH / 2,
                Vector3.down,
                0.1f);
        }
    }

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
        if (!grounded)
            tryMove(Vector3.down * Time.deltaTime * 5f);

        if (Input.GetKey(KeyCode.W))
            tryMove(transform.forward * Time.deltaTime * SPEED);
        if (Input.GetKey(KeyCode.A))
            tryMove(-transform.right * Time.deltaTime * SPEED);
        if (Input.GetKey(KeyCode.S))
            tryMove(-transform.forward * Time.deltaTime * SPEED);
        if (Input.GetKey(KeyCode.D))
            tryMove(transform.right * Time.deltaTime * SPEED);

        transform.Rotate(0, Input.GetAxis("Mouse X") * 5, 0);
        camera.transform.Rotate(-Input.GetAxis("Mouse Y") * 5, 0, 0);
    }

    // Create and return a player
    public static player create()
    {
        var player = new GameObject("player").AddComponent<player>();

        // Create the player camera 
        player.camera = new GameObject("camera").AddComponent<Camera>();
        player.camera.transform.SetParent(player.transform);
        player.camera.transform.localPosition = Vector3.up * EYE_HEIGHT;

        // Move the player above the first map chunk so they
        // dont fall off of the map
        player.transform.position =
            new Vector3(chunk.SIZE / 2,
                        world.altitude(chunk.SIZE / 2, chunk.SIZE / 2) + 5f,
                        chunk.SIZE / 2);

        return player;
    }
}