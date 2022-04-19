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

public class melee_weapon : equip_in_hand, IAddsToInspectionText
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
    player in_use_by;
    float swing_progress_at_impact = -1f;
    float swing_progress = 0;

    public virtual string added_inspection_text() => "Melee damage: " + damage;

    private void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
            throw new System.Exception("Could not find rigidbody on melee weapon!");
    }

    public override void on_equip(player player)
    {
        // Remove all non-trigger colliders
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger)
                c.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only allow one impact per swing
        if (in_use_by == null) return;

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
            if (aii != null)
            {
                aii.on_impact(this);
                in_use_by = null; // Only allow one impact per swing
            }

            var accepts_damage = other.GetComponentInParent<IAcceptsDamage>();
            if (accepts_damage != null)
            {
                accepts_damage.take_damage(damage);
                in_use_by = null; // Only allow one impact per swing
            }
        }

        swing_progress_at_impact = swing_progress;
    }

    //##########//
    // ITEM USE //
    //##########//

    player_interaction[] uses;
    public override player_interaction[] item_uses()
    {
        if (uses == null)
            uses = new player_interaction[]
            {
                new swing_use(this)
            };
        return uses;
    }

    class swing_use : networked_player_interaction
    {
        melee_weapon swinging;

        public swing_use(melee_weapon swinging) { this.swinging = swinging; }

        public override string context_tip()
        {
            return "swing " + swinging.display_name;
        }

        public override bool allow_held => true;
        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override bool start_networked_interaction(player player)
        {
            if (swinging.swing_audio == null)
            {
                // Default audio
                swinging.swing_audio = swinging.gameObject.AddComponent<AudioSource>();
                swinging.swing_audio.spatialBlend = player.has_authority ? 0.75f : 1f; // 3D amount
                swinging.swing_audio.clip = Resources.Load<AudioClip>("sounds/swoosh_1");
                swinging.swing_audio.volume = 0.15f;
            }

            swinging.swing_audio.Play();
            swinging.swing_progress = 0;
            swinging.in_use_by = player;
            swinging.swing_progress_at_impact = -1f;
            return false;
        }

        public override bool continue_networked_interaction(player player)
        {
            // Item was deleted
            if (swinging == null) return true;

            // Continue swinging the weapon
            swinging.swing_progress += 1f * Time.deltaTime / swinging.swing_time;
            if (swinging.swing_progress >= 1f)
                return true;

            // Work out where we are in the swing
            float arg = swinging.swing_progress;
            if (swinging.swing_progress_at_impact > 0)
                arg = Mathf.Min(swinging.swing_progress, swinging.swing_progress_at_impact);
            float sin = -Mathf.Sin(Mathf.PI * 2f * arg);

            // Set the forward/back amount
            Vector3 target_pos = player.hand_centre.position +
                                 swinging.swing_length * player.hand_centre.forward * sin;

            // Remove some of the right/left component
            // so we strike in the middle
            float fw_amt = Mathf.Max(sin, 0);
            target_pos -= fw_amt * Vector3.Project(
                target_pos - player.transform.position, player.transform.right);

            swinging.transform.position = target_pos;

            // Work out/apply the swing rotation
            Vector3 up = player.hand_centre.up +
                swinging.max_forward_in_up * player.hand_centre.forward * sin;
            Vector3 fw = -Vector3.Cross(up, player.hand_centre.right +
                player.hand_centre.forward * fw_amt / 2f);
            Quaternion target_rotation = Quaternion.LookRotation(fw, up);

            swinging.transform.rotation = target_rotation;
            return false;
        }

        public override void end_networked_interaction(player player)
        {
            if (swinging == null) return;

            // Reset
            swinging.in_use_by = null;
            swinging.transform.position = player.hand_centre.transform.position;
            swinging.transform.rotation = player.hand_centre.transform.rotation;
        }
    }
}