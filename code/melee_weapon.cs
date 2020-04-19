using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class melee_weapon : item
{
    public float swing_time = 0.25f;
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

        float sin = Mathf.Min(
                -Mathf.Sin(swing_progress * Mathf.PI * 2f) * swing_progress,
                swing_progress_at_impact > 0 ?
                -Mathf.Sin(swing_progress_at_impact * Mathf.PI * 2f) * swing_progress_at_impact : 1f
            );


        transform.position = player.current.hand.position +
                             player.current.hand.forward * sin;

        float fw_amt = Mathf.Max(sin, 0);
        transform.position -= fw_amt * Vector3.Project(
            transform.position - player.current.camera.transform.position,
            player.current.transform.right
        );

        Vector3 up = player.current.hand.up + max_forward_in_up * player.current.hand.forward * sin;
        Vector3 fw = -Vector3.Cross(up,
            player.current.hand.right +
            player.current.hand.forward * fw_amt / 2f);
        transform.rotation = Quaternion.LookRotation(fw, up);

        if (swing_progress_at_impact < 0)
            foreach (var ip in impact_points)
                if (ip.test(this))
                {
                    swing_progress_at_impact = swing_progress;
                    break;
                }

        if (swing_progress_at_impact > 0)
            return use_result.underway_allows_none;
        return use_result.underway_allows_all;
    }

    public override void on_use_end(player.USE_TYPE use_type)
    {
        swing_progress = 0f;
        swing_progress_at_impact = -1f;
        transform.position = player.current.hand.transform.position;
        transform.rotation = player.current.hand.transform.rotation;
    }
}
