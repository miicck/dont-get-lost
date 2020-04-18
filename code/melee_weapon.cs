using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class melee_weapon : item
{
    public float swing_time = 0.25f;
    float swing_progress = 0;

    public override bool allow_left_click_held_down() { return true; }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        // Only allow left click
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        swing_progress = 0;
        return use_result.underway_allows_all;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type)
    {
        // Continue swinging the weapon
        swing_progress += Time.deltaTime / swing_time;
        if (swing_progress >= 1f)
            return use_result.complete;

        float sin = -Mathf.Sin(swing_progress * Mathf.PI * 2f);
        sin *= swing_progress;
        transform.position = player.current.hand.position +
                             player.current.hand.forward * sin;

        float fw_amt = Mathf.Max(sin, 0);
        transform.position -= fw_amt * Vector3.Project(
            transform.position - player.current.camera.transform.position,
            player.current.transform.right
        );

        Vector3 up = player.current.hand.up + player.current.hand.forward * sin;
        Vector3 fw = -Vector3.Cross(up,
            player.current.hand.right +
            player.current.hand.forward * fw_amt / 2f);
        transform.rotation = Quaternion.LookRotation(fw, up);

        return use_result.underway_allows_all;
    }

    public override void on_use_end(player.USE_TYPE use_type)
    {
        swing_progress = 0f;
        transform.position = player.current.hand.transform.position;
        transform.rotation = player.current.hand.transform.rotation;
    }
}
