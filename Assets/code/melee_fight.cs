using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class melee_fight : settler_interactable
{
    character fighting => GetComponentInParent<character>();
    public override string task_summary() => "Fighting " + utils.a_or_an(fighting.display_name, true);

    float timer_me = 0;
    float timer_target = 0;

    protected override void on_assign(settler s)
    {
        timer_me = 0;
        timer_target = 0;
    }

    protected override STAGE_RESULT on_interact(settler s, int stage)
    {
        if (fighting == null) return STAGE_RESULT.TASK_COMPLETE;
        if (fighting.is_dead) return STAGE_RESULT.TASK_COMPLETE;

        // Play fight animation both ways
        play_fight_animation(fighting, s);
        play_fight_animation(s, fighting);

        // Deal damage
        timer_me += Time.deltaTime;
        if (timer_me > s.attack_time)
        {
            timer_me = 0;
            //if (fighting.has_authority)
                fighting.take_damage(s.attack_damage);
        }

        // Take damage
        timer_target += Time.deltaTime;
        if (timer_target > fighting.attack_time)
        {
            timer_target = 0;
            //if (s.has_authority)
                s.take_damage(fighting.attack_damage);
        }

        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    //##############//
    // STATIC STUFF //
    //##############//

    public static void start_fight(character to_fight, settler force_assign = null)
    {
        // Character needs to be alive
        if (to_fight == null || to_fight.is_dead) return;

        to_fight.add_register_listener(() =>
        {
            // Get existing melee fight interactable
            var mf = to_fight.GetComponentInChildren<melee_fight>();
            networked nw = mf?.GetComponentInParent<networked>();

            if (mf == null)
            {
                // Create a melee fight interactable
                nw = client.create(to_fight.transform.position, "misc/melee_fight", to_fight);
                mf = nw.GetComponent<melee_fight>();
            }

            if (nw == null || force_assign == null) return;

            nw.add_register_listener(() =>
            {
                settler_interactable.force_assign(mf, force_assign);
            });
        });
    }

    public static void play_fight_animation(character c, character fighting)
    {
        // Get the centre/axis that the fight takes place along
        Vector3 disp = fighting.transform.position - c.transform.position;
        Vector3 fight_axis = disp.normalized;

        Vector3 forward = fight_axis;
        forward.y = 0;
        c.transform.forward = forward;

        float cos = Mathf.Pow(Mathf.Cos(Mathf.PI * Time.time / c.attack_time), 10);
        float sin = Mathf.Pow(Mathf.Sin(Mathf.PI * Time.time / c.attack_time), 10);

        var arms = c.GetComponentsInChildren<arm>();
        for (int i = 0; i < arms.Length; ++i)
        {
            var arm = arms[i];

            var init = arm.shoulder.position + fight_axis * arm.total_length / 2f;
            var final = fighting.transform.position + fighting.height * Vector3.up * 0.75f;
            final = arm.nearest_in_reach(final);

            float amt = i % 2 == 0 ? sin : cos;
            arm.update_to_grab(Vector3.Lerp(init, final, amt));
        }
    }
}
