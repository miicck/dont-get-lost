using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class simple_menu_object : MonoBehaviour, IPlayerInteractable
{
    public string context_tip = "open menu";
    public RectTransform menu_prefab;

    public player_interaction[] player_interactions(RaycastHit hit) =>
        new player_interaction[] { new open_menu(this) };

    public class open_menu : player.menu_interaction
    {
        static RectTransform ui;

        simple_menu_object menu_object;

        public open_menu(simple_menu_object menu_object) => this.menu_object = menu_object;

        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() => menu_object.context_tip;
        public override bool show_context_tip() => true;
        public override bool mouse_visible() => true;

        protected override void set_menu_state(player player, bool state)
        {
            if (ui == null)
            {
                ui = menu_object.menu_prefab.inst();
                ui.transform.SetParent(game.canvas.transform);
                ui.anchoredPosition = Vector2.zero;
            }

            if (state) ui.GetComponentInChildren<colony_tasks>().refresh();
            ui.gameObject.SetActive(state);
        }
    }
}
