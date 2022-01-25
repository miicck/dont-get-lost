using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class noticeboard : MonoBehaviour, IPlayerInteractable
{
    public player_interaction[] player_interactions(RaycastHit hit) =>
        new player_interaction[] { new open_task_manager() };

    public class open_task_manager : player.menu_interaction
    {
        static RectTransform ui;

        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() => "open job manager";
        public override bool show_context_tip() => true;
        protected override bool mouse_visible() => true;

        protected override void set_menu_state(player player, bool state)
        {
            if (ui == null)
            {
                ui = Resources.Load<RectTransform>("ui/colony_tasks").inst();
                ui.transform.SetParent(game.canvas.transform);
                ui.anchoredPosition = Vector2.zero;
            }

            if (state) ui.GetComponentInChildren<colony_tasks>().refresh();
            ui.gameObject.SetActive(state);
        }
    }
}