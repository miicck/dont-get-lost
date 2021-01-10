using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railcart : item
{
    public float max_speed = 10f;
    public float acceleration = 1f;

    class ride_interaction : player_interaction
    {
        railcart riding;
        rail current;
        rail next;
        float progress;
        float speed;

        public ride_interaction(railcart riding) { this.riding = riding; }

        public override bool conditions_met()
        {
            return controls.mouse_click(controls.MOUSE_BUTTON.LEFT);
        }

        public override string context_tip()
        {
            return "Left click on a rail to ride";
        }

        public override bool start_interaction(player player)
        {
            // Rails only get used by authority client
            if (!player.has_authority)
                return true;

            // Reset things
            current = null;
            next = null;
            progress = 0;
            speed = 0;

            // Get the rail the player clicked on
            current = utils.raycast_for_closest<rail>(player.camera_ray(),
                out RaycastHit hit, player.INTERACTION_RANGE);
            if (current == null)
                return true; // No rail found

            // Get the next rail in the chain in the direction the player is looking
            next = current.next(player.eye_transform.forward);

            if (next == null)
                return true; // No next rail found

            return false;
        }

        public override bool continue_interaction(player player)
        {
            // Rails only get used by authority client
            if (!player.has_authority) return true;
            if (current == null) return true; // We forgot what rail we were on somehow

            // Accellerate up to max speed
            speed += riding.acceleration * Time.deltaTime;
            if (speed > riding.max_speed) speed = riding.max_speed;

            // Increment progress along the current rail (normalize by rail length
            // so the speed is in world units, rather than rails/sec)
            progress += speed * Time.deltaTime / current.length;
            if (progress > 1f)
            {
                // We've arrived at the next rail, find the next next rail
                rail next_next = next.next(next.transform.position - current.transform.position);
                if (next_next == null) return true; // End of the line
                current = next;
                next = next_next;
                progress = 0f;
            }

            // Player follows the rail cart
            player.transform.position = current.progress_towards(next, progress);

            // Mouse click dismounts the rail
            if (controls.mouse_click(controls.MOUSE_BUTTON.RIGHT) ||
                controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
                return true;

            return false;
        }
    }
}
