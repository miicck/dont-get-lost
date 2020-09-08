using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class accepts_item_impact : MonoBehaviour
{
    public virtual bool on_impact(item i) { return false; }
}

public abstract class equip_in_hand : item
{

}

public class melee_weapon : equip_in_hand
{
    public int damage = 1;
    public float swing_time = 0.5f;
    public float swing_length = 0.15f;
    public float max_forward_in_up = 5f;
    public AudioSource swing_audio;
    float swing_progress = 0;
    float swing_progress_at_impact = -1f;
    new Rigidbody rigidbody;
    bool in_use = false;

    public override bool allow_left_click_held_down() { return true; }

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
            throw new System.Exception("Could not find rigidbody on melee weapon!");
    }

    public override use_result on_use_start(player.USE_TYPE use_type)
    {
        // Only allow left click
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        if (swing_audio == null)
        {
            // Default audio
            swing_audio = gameObject.AddComponent<AudioSource>();
            swing_audio.spatialBlend = 1.0f; // 3D
            swing_audio.clip = Resources.Load<AudioClip>("sounds/woosh_1");
            swing_audio.volume = 0.5f;
        }

        swing_audio.Play();
        swing_progress = 0;
        swing_progress_at_impact = -1f;
        in_use = true;
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
        Vector3 target_pos = player.current.hand_centre.position +
                             swing_length * player.current.hand_centre.forward * sin;

        // Remove some of the right/left component
        // so we strike in the middle
        float fw_amt = Mathf.Max(sin, 0);
        target_pos -= fw_amt * Vector3.Project(
            target_pos - player.current.transform.position,
            player.current.transform.right
        );

        transform.position = target_pos;

        // Work out/apply the swing rotation
        Vector3 up = player.current.hand_centre.up + 
            max_forward_in_up * player.current.hand_centre.forward * sin;
        Vector3 fw = -Vector3.Cross(up, player.current.hand_centre.right +
            player.current.hand_centre.forward * fw_amt / 2f);
        Quaternion target_rotation = Quaternion.LookRotation(fw, up);

        transform.rotation = target_rotation;

        if (swing_progress_at_impact > 0) // We're stuck in something
            return use_result.underway_allows_none;
        return use_result.underway_allows_all; // We're still swinging
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!in_use) return;

        // Ignore collisions with the player
        if (other.transform.IsChildOf(player.current.transform))
            return;

        var rend = other.GetComponent<Renderer>();
        if (rend != null)
            material_sound.play(material_sound.TYPE.HIT, transform.position, rend.material);

        var aii = other.GetComponentInParent<accepts_item_impact>();
        aii?.on_impact(this);

        swing_progress_at_impact = swing_progress;
    }

    public override void on_use_end(player.USE_TYPE use_type)
    {
        swing_progress = 0f;
        swing_progress_at_impact = -1f;
        transform.position = player.current.hand_centre.transform.position;
        transform.rotation = player.current.hand_centre.transform.rotation;
        in_use = false;
    }
}