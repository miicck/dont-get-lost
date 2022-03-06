using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scavangable : MonoBehaviour, IPlayerInteractable
{
    static scavangable()
    {
        tips.add("You can scavange for items if your hands are free. Press " +
            controls.bind_name(controls.BIND.QUICKBAR_1) + " a few times to de-equip what you are holding.");
    }

    item_product[] products => GetComponents<item_product>();

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public player_interaction[] player_interactions(RaycastHit hit)
    {
        if (interactions == null) interactions = new player_interaction[]
        {
            new scavange_interaction(this),
            new player_inspectable(transform)
            {
                text = () => product.product_plurals_list(products) + " can be scavanged",
                sprite = () => Resources.Load<Sprite>("sprites/default_interact_cursor")
            }
        };
        return interactions;
    }

    class scavange_interaction : player_interaction
    {
        scavange_timer timer;
        scavangable scavangable;
        public scavange_interaction(scavangable scavangable) { this.scavangable = scavangable; }

        public override controls.BIND keybind => controls.BIND.USE_ITEM;
        public override bool allow_held => true;
        public override bool allows_mouse_look() => false;
        public override bool allows_movement() => false;

        public override string context_tip()
        {
            var str = "scavange for " + product.product_plurals_list(scavangable.products);
            if (str.Length < 40) return str;
            return "scavange";
        }

        protected override bool on_start_interaction(player player)
        {
            if (timer != null) Destroy(timer.gameObject);
            timer = Resources.Load<scavange_timer>("ui/scavange_timer").inst();
            timer.transform.SetParent(game.canvas.transform);
            timer.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            timer.scavanging = scavangable;
            controls.disabled = true;
            return false;
        }

        public override bool continue_interaction(player player)
        {
            controls.disabled = false;
            if (!triggered(player)) return true;
            controls.disabled = true;
            return timer == null;
        }

        protected override void on_end_interaction(player player)
        {
            controls.disabled = false;
            if (timer == null)
            {
                foreach (var p in scavangable.products)
                    p.create_in(player.current.inventory);
            }
            else Destroy(timer.gameObject);
        }
    }
}