using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Seperates connected sections of
/// path elements into rooms. </summary>
public class doorway : town_path_element, IAddsToInspectionText
{
    public Transform door;
    public Component detection_center;
    public AXIS door_axis;
    public float closed_angle;
    public float open_angle;

    int in_use_by = 0;
    bool opening => in_use_by > 0 || (player.current != null && player.current.distance_to(detection_center) < 2f);
    float door_angle = 0;

    public override void on_character_enter(character s) { ++in_use_by; }
    public override void on_character_leave(character s) { --in_use_by; }

    public override bool seperates_rooms() { return true; }

    protected override void Start()
    {
        base.Start();
        door_angle = closed_angle;
    }

    private void Update()
    {
        if (opening)
        {
            if (Mathf.Abs(door_angle - open_angle) < 1) return;
        }
        else if (Mathf.Abs(door_angle - closed_angle) < 1) return;

        Vector3 angles = door.transform.localRotation.eulerAngles;

        float max_swing = Time.deltaTime * (opening ? 180f : 45f);
        float target_angle = opening ? open_angle : closed_angle;
        float delta = Mathf.Clamp(target_angle - door_angle, -max_swing, max_swing);
        door_angle += delta;

        switch (door_axis)
        {
            case AXIS.X_AXIS:
                angles.x = door_angle;
                break;

            case AXIS.Y_AXIS:
                angles.y = door_angle;
                break;

            case AXIS.Z_AXIS:
                angles.z = door_angle;
                break;
        }

        door.transform.localRotation = Quaternion.Euler(angles);
    }
}
