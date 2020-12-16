using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railcart : item
{
    public float max_speed = 10f;
    public float acceleration = 1f;

    rail current;
    rail next;
    float progress;
    float speed;

    public override use_result on_use_start(player.USE_TYPE use_type, player player)
    {
        // Rails only get used by authority client
        if (!player.has_authority)
            return use_result.complete;

        // Reset things
        current = null;
        next = null;
        progress = 0;
        speed = 0;

        // Get the rail the player clicked on
        current = utils.raycast_for_closest<rail>(player.camera_ray(),
            out RaycastHit hit, player.INTERACTION_RANGE);
        if (current == null)
            return use_result.complete; // No rail found

        // Get the next rail in the chain in the direction the player is looking
        next = current.next(player.eye_transform.forward);

        if (next == null)
            return use_result.complete; // No next rail found

        return use_result.underway_allows_all;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type, player player)
    {
        // Rails only get used by authority client
        if (!player.has_authority) return use_result.complete;
        if (current == null) return use_result.complete; // We forgot what rail we were on somehow

        // Accellerate up to max speed
        speed += acceleration * Time.deltaTime;
        if (speed > max_speed) speed = max_speed;

        // Increment progress along the current rail (normalize by rail length
        // so the speed is in world units, rather than rails/sec)
        progress += speed * Time.deltaTime / current.length;
        if (progress > 1f)
        {
            // We've arrived at the next rail, find the next next rail
            rail next_next = next.next(next.transform.position - current.transform.position);
            if (next_next == null) return use_result.complete; // End of the line
            current = next;
            next = next_next;
            progress = 0f;
        }

        // Player follows the rail cart
        player.transform.position = current.progress_towards(next, progress);

        // Mouse click dismounts the rail
        if (controls.mouse_click(controls.MOUSE_BUTTON.RIGHT) ||
            controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
            return use_result.complete;

        return use_result.underway_allows_all;
    }
}
