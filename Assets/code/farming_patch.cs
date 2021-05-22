using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class farming_patch : building_with_inventory, IPlayerInteractable
{
    networked_variables.net_int time_planted;

    seed seed;
    GameObject grown;

    int growth_time => seed.growth_time;

    public override void on_init_network_variables()
    {
        base.on_init_network_variables();
        time_planted = new networked_variables.net_int();
    }

    private void Start()
    {
        InvokeRepeating("update_growth", 1, 1);
    }

    void update_growth()
    {
        if (grown != null) return;
        if (seed == null) return;

        // Grow the product
        int delta_time = client.server_time - time_planted.value;

        if (delta_time > growth_time)
        {
            // Grow the product
            // Add happens before remove, because if remove removes the last
            // seed, then the product becomes null (in the inventory on_change method).
            if (inventory.add(seed.growing_into, 1))
                inventory.remove(seed, 1);
        }
    }

    //########################//
    // fixture_with_inventory //
    //########################//

    protected override string inventory_prefab()
    {
        return "inventories/farming_spot";
    }

    protected override void on_set_inventory()
    {
        inventory.add_on_change_listener(() =>
        {
            // Update the recipe that we're growing
            seed = null;
            foreach (var kv in inventory.contents())
                if (kv.Value > 0 && kv.Key is seed)
                {
                    seed = kv.Key as seed;
                    time_planted.value = client.server_time;
                    break;
                }

            // Destroy the representation of the grown product if it has been removed
            if (grown != null && inventory.count(grown.name) < 1)
            {
                Destroy(grown);
                grown = null;
            }

            // Create the representation of grown products
            if (grown == null)
                foreach (var kv in inventory.contents())
                    if (kv.Value > 0 && kv.Key.name == seed.growing_into.name)
                    {
                        grown = create(kv.Key.name, transform.position, transform.rotation).gameObject;
                        grown.transform.SetParent(transform);
                        Destroy(grown.GetComponent<item>());
                        grown.AddComponent<farm_harvest_on_click>().spot = this;
                        break;
                    }
        });
    }

    public void harvest()
    {
        string to_harvest = grown.name;
        if (inventory.remove(to_harvest, 1))
            player.current.inventory.add(to_harvest, 1);
    }

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;

    public override player_interaction[] player_interactions(RaycastHit hit)
    {
        if (is_logistics_version) return base.player_interactions(hit);

        if (interactions == null)
            interactions = base.player_interactions(hit).prepend(new menu(this),
            new player_inspectable(transform)
            {
                text = () =>
                {
                    string ret = "Farming patch\n";
                    ret += (seed == null ? "Nothing growing." :
                            seed?.plural + " growing into " + seed.growing_into.plural + ".");
                    return ret;
                }
            });
        return interactions;
    }

    class menu : left_player_menu
    {
        farming_patch spot;
        public menu(farming_patch spot) : base(spot.display_name) { this.spot = spot; }
        protected override RectTransform create_menu() { return spot.inventory.ui; }
        public override inventory editable_inventory() { return spot.inventory; }
        protected override void on_open() { spot.update_growth(); }
    }
}

public class farm_harvest_on_click : MonoBehaviour, IPlayerInteractable
{
    public farming_patch spot;

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        return new interaction[] { new interaction(spot) };
    }

    class interaction : player_interaction
    {
        farming_patch spot;
        public interaction(farming_patch spot) { this.spot = spot; }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;

        public override bool start_interaction(player player)
        {
            player.play_sound("sounds/click_1");
            spot.harvest();
            return true;
        }

        public override string context_tip()
        {
            return "harvest";
        }
    }
}