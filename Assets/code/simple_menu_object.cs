using System.Collections;
using System.Collections.Generic;
using UnityEngine;

interface ISimpleMenuObject
{
    public void on_menu_open();
}

public class simple_menu_object : MonoBehaviour, IPlayerInteractable
{
    public string context_tip = "open menu";
    public RectTransform menu_prefab;

    player_interaction[] interactions;

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        if (interactions == null)
            interactions = new player_interaction[] { new open_menu(this) };
        return interactions;
    }

    public class open_menu : player.menu_interaction
    {
        static Dictionary<RectTransform, RectTransform> ui_instances = new Dictionary<RectTransform, RectTransform>();
        simple_menu_object menu_object;

        public open_menu(simple_menu_object menu_object) => this.menu_object = menu_object;

        public override controls.BIND keybind => controls.BIND.OPEN_INVENTORY;
        public override string context_tip() => menu_object.context_tip;
        public override bool show_context_tip() => true;
        public override bool mouse_visible() => true;
        public override bool allows_movement() => false;

        protected override void set_menu_state(player player, bool state)
        {
            if (!ui_instances.TryGetValue(menu_object.menu_prefab, out RectTransform ui))
            {
                ui = ui_instances[menu_object.menu_prefab] = menu_object.menu_prefab.inst();
                ui.transform.SetParent(game.canvas.transform);
                ui.anchoredPosition = Vector2.zero;
            }

            if (state)
                foreach (var smo in ui.GetComponentsInChildren<ISimpleMenuObject>())
                    smo.on_menu_open();

            ui.gameObject.SetActive(state);
        }
    }
}
