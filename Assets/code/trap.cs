using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class trap : town_path_element
{
    public List<transform_animation> animations = new List<transform_animation>();
    public float fire_time = 0.2f;
    public float reset_time = 1f;

    HashSet<character> targets = new HashSet<character>();

    public override void on_character_enter(character c)
    {
        if (c.GetComponent<town_attacker>() != null)
            targets.Add(c);
    }

    public override void on_character_leave(character c)
    {
        targets.Remove(c);
    }

    public float progress = 0;
    bool resetting = false;

    private void Update()
    {
        if (targets.Count == 0 && !resetting)
            return; // Don't fire unless we have targets

        if (resetting)
        {
            // Reset trap
            progress -= Time.deltaTime / reset_time;
            if (progress < 0)
            {
                progress = 0;
                resetting = false;
            }
        }
        else
        {
            // Fire trap
            progress += Time.deltaTime / fire_time;
            if (progress > 1f)
            {
                progress = 1f;
                resetting = true;

                foreach (var c in new List<character>(targets))
                {
                    if (c.is_dead) targets.Remove(c);
                    c.take_damage(50);
                }
            }
        }

        foreach (var a in animations)
            a.progress = progress;
    }
}
