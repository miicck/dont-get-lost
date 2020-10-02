using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Class dealing with the recoding and playback of cinematics. 
/// Uses an interpolation of keyframes recorded in fly mode.  </summary>
public static class cinematic_recording
{
    /// <summary> A keyframe along the cinematic path. </summary>
    struct keyframe
    {
        public Vector3 position;
        public Quaternion rotation;
    }
    static List<keyframe> keyframes = new List<keyframe>();

    /// <summary> Add the next keyframe at the given position/rotation. </summary>
    public static void add_keyframe(Vector3 position, Quaternion rotation)
    {
        keyframes.Add(new keyframe
        {
            position = position,
            rotation = rotation
        });

        popup_message.create("Added keyframe " + keyframes.Count);
    }

    /// <summary> Remove the last keyframe recorded. </summary>
    public static void remove_last_keyframe()
    {
        if (keyframes.Count == 0)
        {
            popup_message.create("No keyframes to remove!");
            return;
        }
        keyframes.RemoveAt(keyframes.Count - 1);
        popup_message.create("Removed keyframe " + (keyframes.Count + 1));
    }

    /// <summary> The current playback object. / </summary>
    static cinematic_playback current_playback;

    /// <summary> Toggle playback (restarts from beginning). </summary>
    public static void toggle_playback()
    {
        if (current_playback == null)
        {
            current_playback = new GameObject("playback").AddComponent<cinematic_playback>();
            current_playback.set_frames(keyframes);
        }
        else
        {
            Object.Destroy(current_playback.gameObject);
            current_playback = null;
        }
    }

    /// <summary> Class for replaying the cinematic. This takes control of the
    /// player position+rotation so that the camera follows the keyframe path. </summary>
    class cinematic_playback : MonoBehaviour
    {
        float progress = 0;
        float total_length = 0;
        List<keyframe> keyframes;

        public void set_frames(List<keyframe> keyframes)
        {
            this.keyframes = new List<keyframe>(keyframes);
        }

        private void Start()
        {
            if (keyframes.Count == 0)
            {
                popup_message.create("No saved keyframes!");
                Destroy(gameObject);
                return;
            }

            // Make the keyframes loop
            keyframes.Add(keyframes[0]);

            // Go to the start
            transform.position = keyframes[0].position;
            transform.rotation = keyframes[0].rotation;

            for (int i = 1; i < keyframes.Count; ++i)
                total_length += (keyframes[i].position - keyframes[i - 1].position).magnitude;
        }

        private void OnDestroy()
        {
            player.current.camera.enabled = true;
        }

        keyframe interpolated_keyframe(float progress)
        {
            float cum_length = 0;
            float prog_length = total_length * progress;

            for (int i = 1; i < keyframes.Count; ++i)
            {
                // Increament cumulative length with the segment length
                float seg_length = (keyframes[i].position - keyframes[i - 1].position).magnitude;
                cum_length += seg_length;

                // See if the interpolated keyframe is on this segement
                if (cum_length >= prog_length)
                {
                    // Work out how far along the segment the interpolated frame is
                    float seg_start = cum_length - seg_length;
                    float f = (prog_length - seg_start) / seg_length;

                    // Return the interpolated result
                    return new keyframe
                    {
                        position = Vector3.Lerp(keyframes[i - 1].position, keyframes[i].position, f),
                        rotation = Quaternion.Lerp(keyframes[i - 1].rotation, keyframes[i].rotation, f)
                    };
                }
            }

            // cum_length was never > prog_length => we're beyond 
            // the end (or there is only one keyframe)
            return keyframes[keyframes.Count - 1];
        }

        private void Update()
        {
            if (keyframes.Count == 0)
            {
                popup_message.create("No saved keyframes!");
                Destroy(gameObject);
                return;
            }

            // Increment the progress along the path
            progress += 5f * Time.deltaTime / total_length;
            while (progress > 1f) progress -= 1f; // Loop

            // Work out the corresponding interpolated keyframe
            var inter = interpolated_keyframe(progress);

            transform.position = Vector3.Lerp(transform.position, inter.position, Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, inter.rotation, Time.deltaTime);

            // The player follows along such that the camera is at our location
            Vector3 cam_delta = player.current.camera.transform.position -
                                player.current.transform.position;
            player.current.networked_position = transform.position - cam_delta;
            player.current.set_look_rotation(transform.rotation);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);

            for (int i = 1; i < keyframes.Count; ++i)
                Gizmos.DrawLine(keyframes[i].position, keyframes[i - 1].position);
        }
    }
}
