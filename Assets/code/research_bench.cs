using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_bench : walk_to_settler_interactable, IPlayerInteractable
{
    //##############//
    // INTERACTABLE //
    //##############//

    public override string task_summary() =>
        "Researching " + tech_tree.current_research_project() +
        " at " + GetComponentInParent<item>().display_name;

    float work_done;
    float time_researching;
    settler_animations.simple_work work_anim;

    protected override bool ready_to_assign(settler s)
    {
        var tech = tech_tree.current_research_technology();
        if (tech == null)
            return false;

        return true;
    }

    protected override void on_arrive(settler s)
    {
        // Reset stuff
        work_done = 0;
        time_researching = 0;
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
        time_researching += Time.deltaTime;

        // Work until 10 work done, then perform 1 unit of research
        if (work_done < 10) return STAGE_RESULT.STAGE_UNDERWAY;
        tech_tree.perform_research(1);
        work_done = 0;

        // Go agaun until 60 seconds has passed
        return time_researching > 60 ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.STAGE_UNDERWAY;
    }

    public override string added_inspection_text()
    {
        return base.added_inspection_text() +
            "\nResearching " + tech_tree.current_research_project() +
            " (" + tech_tree.get_research_amount(tech_tree.current_research_project()) + " % complete)";
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    public class open_tech_tree : player.menu_interaction
    {
        static RectTransform ui;

        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() => "open tech tree";
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
