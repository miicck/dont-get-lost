using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class chest : building_with_inventory, IPlayerInteractable
{
    public item_input automatic_input;

    public string chest_inventory_prefab = "inventories/chest";
    protected override string inventory_prefab()
    {
        return chest_inventory_prefab;
    }

    item_buffer_output buffer_output; // For buffer chests only

    private void Start()
    {
        buffer_output = GetComponentInChildren<item_buffer_output>();
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

    void run_buffer_output()
    {
        // Buffer ouput
        if (buffer_output == null) return;
        if (!buffer_output.ready_for_input) return;
        var to_buffer = has_authority ? inventory.remove_first() : inventory.get_first();
        if (to_buffer == null) return;
        buffer_output.add_item(create(to_buffer.name, buffer_output.transform.position,
                                      buffer_output.transform.rotation, logistics_version: true));
    }

    private void Update()
    {
        if (inventory == null) return;
        run_input();
        run_buffer_output();
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
        public menu(chest chest) : base(chest.display_name) { this.chest = chest; }
        protected override RectTransform create_menu() { return chest.inventory.ui; }
        public override inventory editable_inventory() { return chest.inventory; }
    }
}