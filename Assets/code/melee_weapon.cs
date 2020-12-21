using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class accepts_item_impact : MonoBehaviour
{
    public virtual bool on_impact(item i) { return false; }
}

public abstract class equip_in_hand : item
{
    public override void on_equip(player player)
    {
        // Remove all colliders
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }
}

public class melee_weapon : equip_in_hand
{
#if UNITY_EDITOR
    new
#endif
    Rigidbody rigidbody;

    public int damage = 1;
    public float swing_time = 0.5f;
    public float swing_length = 0.15f;
    public float max_forward_in_up = 5f;
    public AudioSource swing_audio;
    float swing_progress = 0;
    float swing_progress_at_impact = -1f;
    player in_use_by;

    public override bool allow_left_click_held_down() { return true; }

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
            throw new System.Exception("Could not find rigidbody on melee weapon!");
    }

    public override string equipped_context_tip()
    {
        return "Left click to swing " + display_name;
    }

    public override void on_equip(player player)
    {
        // Remove all non-trigger colliders
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger)
                c.enabled = false;
    }

    public override use_result on_use_start(player.USE_TYPE use_type, player player)
    {
        // Only allow left click
        if (use_type != player.USE_TYPE.USING_LEFT_CLICK)
            return use_result.complete;

        if (swing_audio == null)
        {
            // Default audio
            swing_audio = gameObject.AddComponent<AudioSource>();
            swing_audio.spatialBlend = player.has_authority ? 0.75f : 1f; // 3D amount
            swing_audio.clip = Resources.Load<AudioClip>("sounds/swoosh_1");
            swing_audio.volume = 0.35f;
        }

        swing_audio.Play();
        swing_progress = 0;
        swing_progress_at_impact = -1f;
        in_use_by = player;
        return use_result.underway_allows_all;
    }

    public override use_result on_use_continue(player.USE_TYPE use_type, player player)
    {
        if (player != in_use_by)
            throw new System.Exception("Player using melee weapon shouldn't change mid-use!");

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
        Vector3 target_pos = player.hand_centre.position +
                             swing_length * player.hand_centre.forward * sin;

        // Remove some of the right/left component
        // so we strike in the middle
        float fw_amt = Mathf.Max(sin, 0);
        target_pos -= fw_amt * Vector3.Project(
            target_pos - player.transform.position, player.transform.right);

        transform.position = target_pos;

        // Work out/apply the swing rotation
        Vector3 up = player.hand_centre.up +
            max_forward_in_up * player.hand_centre.forward * sin;
        Vector3 fw = -Vector3.Cross(up, player.hand_centre.right +
            player.hand_centre.forward * fw_amt / 2f);
        Quaternion target_rotation = Quaternion.LookRotation(fw, up);

        transform.rotation = target_rotation;
        return use_result.underway_allows_all;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Not being used
        if (in_use_by == null) return;

        // Only allow one impact per swing
        if (swing_progress_at_impact > 0) return;

        // Ignore collisions with the player
        if (other.transform.IsChildOf(in_use_by.transform))
            return;

        var rend = other.GetComponent<Renderer>();
        if (rend != null)
            material_sound.play(material_sound.TYPE.HIT, transform.position, rend.material);

        // Only affect the world if the user has authority
        if (in_use_by.has_authority)
        {
            var aii = other.GetComponentInParent<accepts_item_impact>();
            aii?.on_impact(this);

            var accepts_damage = other.GetComponentInParent<IAcceptsDamage>();
            accepts_damage?.take_damage(damage);
        }

        swing_progress_at_impact = swing_progress;
    }

    public override void on_use_end(player.USE_TYPE use_type, player player)
    {
        swing_progress = 0f;
        swing_progress_at_impact = -1f;
        transform.position = player.hand_centre.transform.position;
        transform.rotation = player.hand_centre.transform.rotation;
        in_use_by = null;
    }
}