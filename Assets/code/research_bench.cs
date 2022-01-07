using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_bench : walk_to_settler_interactable, IPlayerInteractable
{
    //##############//
    // INTERACTABLE //
    //##############//

    public override string task_summary() =>
        "Researching " + tech_tree.reseach_project() +
        " at " + GetComponentInParent<item>().display_name;

    float work_done;
    settler_animations.simple_work work_anim;

    protected override bool ready_to_assign(settler s) => tech_tree.research_project_set();

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        work_done = 0;
        work_anim = new settler_animations.simple_work(s,
            period: 1f / current_proficiency.total_multiplier);
    }

    protected override STAGE_RESULT on_interact_arrived(settler s, int stage)
    {
        // Play the work animation
        work_anim.play();

        // Only perform research on authority client
        if (!s.has_authority) return STAGE_RESULT.STAGE_UNDERWAY;

        work_done += Time.deltaTime * current_proficiency.total_multiplier;

        // Work until 10 work done
        if (work_done < 10)
            return STAGE_RESULT.STAGE_UNDERWAY;

        // Perform 1 unit of research
        tech_tree.perform_research(1);

        return STAGE_RESULT.TASK_COMPLETE;
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public class open_tech_tree : player.menu_interaction
    {
        static RectTransform ui;

        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() =>  "open tech tree";
        public override bool show_context_tip() => true;
        public override bool allows_movement() => false;
        public override bool allows_mouse_look() => false;

        protected override void set_menu_state(player player, bool state)
        {
            if (ui == null)
                ui = tech_tree.generate_tech_tree();
            ui.gameObject.SetActive(state);
        }
    }

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new player_interaction[] { new open_tech_tree() };
    }
}
