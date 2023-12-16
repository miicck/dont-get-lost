using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class railcart : item
{
    public float max_speed = 10f;
    public float acceleration = 1f;

    public override player_interaction[] item_uses() =>
        new player_interaction[] { new ride_interaction(this) };

    class ride_interaction : player_interaction
    {
        railcart riding;
        rail current;
        rail next;
        float progress;
        float speed;

        int stops = 0;

        public ride_interaction(railcart riding) { this.riding = riding; }
        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override string context_tip()
        {
            if (current != null) return "Stop riding";
            return "Ride";
        }

        void announce_stops()
        {
            if (stops <= 0) popup_message.create("Stopping at the next station");
            else if (stops == 1) popup_message.create("Stopping after 1 station");
            else popup_message.create("Stopping after " + stops + " stations");
        }

        protected override bool on_start_interaction(player player)
        {
            // Rails only get used by authority client
            if (!player.has_authority)
                return true;

            // Reset things
            current = null;
            next = null;
            progress = 0;
            speed = 0;
            stops = 0;

            // Get the rail the player clicked on
            var ray = player.camera_ray(player.INTERACTION_RANGE, out float dis);
            current = utils.raycast_for_closest<rail>(ray, out RaycastHit hit, dis);
            if (current == null)
                return true; // No rail found

            // Get the next rail in the chain in the direction the player is looking
            next = current.next(player.eye_transform.forward);

            if (next == null)
            {
                popup_message.create("No next rail found!");
                return true;
            }

            announce_stops();

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

                Vector3 next_foward = next.transform.forward;
                if (Vector3.Dot(next_foward, next.transform.position - current.transform.position) < 0)
                    next_foward = -next_foward;

                rail next_next = next.next(next_foward);
                if (next_next == null)
                {
                    popup_message.create("End of the line!");
                    return true; // End of the line
                }

                // We've arrived at a station, decrement the number of stops left
                if (current.GetComponentInParent<building_material>().name == "rail_stop")
                {
                    if (--stops < 0) return true;
                    announce_stops();
                }

                current = next;
                next = next_next;

                progress = 0f;
            }

            // Player follows the rail cart
            player.transform.position = current.progress_towards(next, progress);

            // Alternate use dismounts the rail
            if (controls.triggered(controls.BIND.ALT_USE_ITEM))
                return true;

            // Use item increments the number of stops
            if (controls.triggered(controls.BIND.USE_ITEM))
            {
                ++stops;
                announce_stops();
            }

            return false;
        }
    }
}
