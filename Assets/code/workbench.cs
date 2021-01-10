using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : building_with_inventory, IPlayerInteractable
{
    protected override string inventory_prefab() { return "inventories/workbench"; }

    //#################//
    // ILeftPlayerMenu //
    //#################//

    player_interaction[] interactions;
    public override player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = base.player_interactions().prepend(new menu(this));
        return interactions;
    }

    class menu : left_player_menu
    {
        workbench workbench;
        public menu(workbench workbench) : base(workbench.display_name) { this.workbench = workbench; }

        public override inventory editable_inventory() { return workbench.inventory; }

        protected override RectTransform create_menu()
        {
            var crafting = workbench.inventory.ui.GetComponentInChildren<crafting_input>();
            workbench.inventory.ui.GetComponentInChildren<UnityEngine.UI.Text>().text = workbench.display_name;
            crafting.recipes_folder = "recipes/workbenches/" + workbench.name;
            crafting.craft_from = workbench.inventory;
            crafting.craft_to = player.current.inventory;
            crafting.update_recipies();
            return workbench.inventory.ui;
        }
    }
}
