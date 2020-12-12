using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class rifle : equip_in_hand
{
    public Transform butt;
    public Transform sight;
    public Transform grip;

    public float field_of_view_multiplier = 1f;
    public float kick_angle_x = 20f;
    public float kick_angle_y = 10f;
    public float kick_recover_rate = 90f;

    arm right_arm;
    arm left_arm;

    float reset_clip_plane;
    float reset_field_of_view;
    Vector2 acc_kick_angle;

    player player => GetComponentInParent<player>();

    public override void on_equip(bool local_player)
    {
        base.on_equip(local_player);

        foreach (var a in player.GetComponentsInChildren<arm>())
        {
            if (a.name.Contains("right")) right_arm = a;
            else left_arm = a;
        }

        left_arm.to_grab = grip;
        right_arm.to_grab = transform;
        if (player.camera != null)
        {
            reset_clip_plane = player.camera.nearClipPlane;
            reset_field_of_view = player.camera.fieldOfView;
        }

        locate();
    }

    private void Update()
    {
        if (controls.mouse_click(controls.MOUSE_BUTTON.LEFT))
            fire();
        locate();
    }

    void fire()
    {
        acc_kick_angle.x += kick_angle_x;
        acc_kick_angle.y += kick_angle_y;
        player.mod_x_rotation(-kick_angle_x);
        player.mod_y_rotation(kick_angle_y);
    }

    void locate()
    {
        // Recover from kick
        Vector2 recover_angle = new Vector2(
            Mathf.Min(Time.deltaTime * kick_recover_rate, acc_kick_angle.x),
            Mathf.Min(Time.deltaTime * kick_recover_rate, acc_kick_angle.y)
        );
        acc_kick_angle -= recover_angle;
        player.mod_x_rotation(recover_angle.x);
        player.mod_y_rotation(-recover_angle.y);

        // Allign rifle but with shoulder
        transform.forward = player.eye_transform.forward;
        Vector3 delta = right_arm.shoulder.transform.position - butt.transform.position;
        transform.position += delta;

        // Hip fire
        player.cursor_sprite = "crosshair";

        if ((player.camera == null) || !player.first_person)
            return;

        if (controls.mouse_down(controls.MOUSE_BUTTON.RIGHT))
        {
            // ADS - lerp camera to sight position / sight field of view
            player.camera.fieldOfView = Mathf.Lerp(player.camera.fieldOfView,
                reset_field_of_view * field_of_view_multiplier, 10 * Time.deltaTime);
            player.cursor_sprite = "transparent";
            player.camera.nearClipPlane = 0.01f;
            player.camera.transform.position =
                Vector3.Lerp(player.camera.transform.position,
                sight.transform.position, 10f * Time.deltaTime);
        }
        else
        {
            // Reset camera position when not in ADS
            player.camera.nearClipPlane = reset_clip_plane;
            player.camera.transform.localPosition = Vector3.Lerp(
                player.camera.transform.localPosition, Vector3.zero, 10f * Time.deltaTime);
            player.camera.fieldOfView = Mathf.Lerp(player.camera.fieldOfView, reset_field_of_view, 10 * Time.deltaTime);
            player.camera.transform.localRotation = Quaternion.identity;
        }
    }
}
