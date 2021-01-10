using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class portal : building_material, IPlayerInteractable
{
    public Transform teleport_location;

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public override player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = new player_interaction[] { new menu(this) };
        return interactions;
    }

    class menu : left_player_menu
    {
        portal portal;
        public menu(portal portal) : base(portal.display_name) { this.portal = portal; }

        protected override RectTransform create_menu()
        {
            var ui = Resources.Load<RectTransform>("ui/portal").inst();
            var pr = ui.GetComponentInChildren<portal_renamer>();

            pr.field.onValueChanged.AddListener((new_val) =>
            {
                pr.field.text = portal.attempt_rename(new_val);
            });

            pr.field.onEndEdit.AddListener((final_val) =>
            {
                // Refresh UI
                player.current.ui_state = player.UI_STATE.ALL_CLOSED;
                player.current.ui_state = player.UI_STATE.INVENTORY_OPEN;
            });

            pr.field.text = portal.teleport_name();

            return ui;
        }

        protected override void on_open()
        {
            var content = menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;

            // Destroy the old buttons
            foreach (Transform c in content)
            {
                var b = c.GetComponent<UnityEngine.UI.Button>();
                if (b != null) Destroy(b.gameObject);
            }

            // Load the new buttons
            FindObjectOfType<teleport_manager>().create_buttons(content);
        }
    }

    //#################//
    // Unity callbacks //
    //#################//

    public override void on_first_create()
    {
        base.on_first_create();
        FindObjectOfType<teleport_manager>().register_portal(this);
    }

    public string attempt_rename(string new_name)
    {
        return FindObjectOfType<teleport_manager>().attempt_rename_portal(this, new_name);
    }

    public string teleport_name()
    {
        return FindObjectOfType<teleport_manager>().get_portal_name(this);
    }

    public override void on_forget(bool deleted)
    {
        base.on_forget(deleted);
        if (deleted && has_authority)
            FindObjectOfType<teleport_manager>().unregister_portal(this);
    }
}