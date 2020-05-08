using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class equip_in_hand : item
{

}

public class melee_weapon : equip_in_hand
{
    public float swing_time = 0.25f;
    public float swing_length = 0.5f;
    public float max_forward_in_up = 5f;
    float swing_progress = 0;
    float swing_progress_at_impact = -1f;

    impact_test_point[] impact_points { get { return GetComponentsInChildren<impact_test_point>(); } }

    public override bool allow_left_click_held_down() { return true; }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        // Only allow left click
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        swing_progress = 0;
        swing_progress_at_impact = -1f;
        return use_result.underway_allows_all;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type)
    {
        // Continue swinging the weapon
        swing_progress += 1f * Time.deltaTime / swing_time;
        if (swing_progress >= 1f)
            return use_result.complete;

        // Work out where we are in the swing
        float arg = swing_progress;
        if (swing_progress_at_impact > 0)
            arg = Mathf.Min(swing_progress, swing_progress_at_impact);
        float sin = -Mathf.Sin(Mathf.PI * 2f * arg);

        // Set the forward/back amount
        transform.position = player.current.hand_centre.position +
                             swing_length * player.current.hand_centre.forward * sin;

        // Remove some of the right/left component
        // so we strike in the middle
        float fw_amt = Mathf.Max(sin, 0);
        transform.position -= fw_amt * Vector3.Project(
            transform.position - player.current.transform.position,
            player.current.transform.right
        );

        // Work out/apply the swing rotation
        Vector3 up = player.current.hand_centre.up + max_forward_in_up * player.current.hand_centre.forward * sin;
        Vector3 fw = -Vector3.Cross(up,
            player.current.hand_centre.right +
            player.current.hand_centre.forward * fw_amt / 2f);
        transform.rotation = Quaternion.LookRotation(fw, up);

        // Check to ee if we've hit something
        if (swing_progress_at_impact < 0)
            foreach (var ip in impact_points)
                if (ip.test(this))
                {
                    swing_progress_at_impact = swing_progress;
                    break;
                }

        if (swing_progress_at_impact > 0) // We're stuck in something
            return use_result.underway_allows_none;
        return use_result.underway_allows_all; // We're still swinging
    }

    public override void on_use_end(player.USE_TYPE use_type)
    {
        swing_progress = 0f;
        swing_progress_at_impact = -1f;
        transform.position = player.current.hand_centre.transform.position;
        transform.rotation = player.current.hand_centre.transform.rotation;
    }
}
