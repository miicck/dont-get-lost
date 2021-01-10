using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chest : building_with_inventory, IPlayerInteractable
{
    protected override string inventory_prefab()
    {
        return "inventories/chest";
    }

    item_input input;

    private void Start()
    {
        input = GetComponentInChildren<item_input>();
        if (input == null) throw new System.Exception("Chest has no item input!");
    }

    private void Update()
    {
        // Transfer input into chest inventory
        var next_item = input.release_next_item();
        if (next_item == null) return;
        if (has_authority) inventory?.add(next_item, 1);
        Destroy(next_item.gameObject);
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;

    public override player_interaction[] player_interactions()
    {
        if (interactions == null)
            interactions = base.player_interactions().prepend(new menu(this));
        return interactions;
    }

    class menu : left_player_menu
    {
        chest chest;
        public menu(chest chest) { this.chest = chest; }
        protected override RectTransform create_menu() { return chest.inventory.ui; }
        public override inventory editable_inventory() { return chest.inventory; }
        public override string display_name() { return chest.display_name; }
    }
}