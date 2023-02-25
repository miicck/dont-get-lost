using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Stores items / is accesible to the player. </summary>
public class chest : building_with_inventory, IPlayerInteractable
{
    public item_input automatic_input;

    public string chest_inventory_prefab = "inventories/chest";
    protected override string inventory_prefab()
    {
        return chest_inventory_prefab;
    }

    void run_input()
    {
        // Transfer input into chest inventory
        if (automatic_input == null) return;
        var next_input_item = automatic_input.release_next_item();
        if (next_input_item == null) return;
        if (has_authority) inventory?.add(next_input_item, 1);
        Destroy(next_input_item.gameObject);
    }

    private void Update()
    {
        if (inventory == null) return;
        run_input();
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;

    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        if (is_logistics_version) return base.player_interactions(hit);
        if (interactions == null)
            interactions = base.player_interactions(hit).prepend(new menu(this));
        return interactions;
    }

    class menu : left_player_menu
    {
        chest chest;
        public menu(chest chest) : base(chest.display_name) => this.chest = chest;
        protected override RectTransform create_menu(Transform parent) => chest.inventory.ui;
        public override inventory editable_inventory() => chest.inventory;
    }
}