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

    product[] products => GetComponents<product>();

    //#####################//
    // IPlayerInteractable //
    //#####################//

    player_interaction[] interactions;
    public player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = new player_interaction[]
        {
            new interaction(this),
            new player_inspectable(transform)
            {
                text = () => product.product_plurals_list(products) + " can be scavanged",
                sprite = () => Resources.Load<Sprite>("sprites/default_interact_cursor")
            }
        };
        return interactions;
    }

    class interaction : player_interaction
    {
        scavange_timer timer;
        scavangable scavangable;
        public interaction(scavangable scavangable) { this.scavangable = scavangable; }

        public override bool conditions_met()
        {
            return controls.triggered(controls.BIND.USE_ITEM);
        }

        public override string context_tip()
        {
            var str = "Left click to scavange for " + product.product_plurals_list(scavangable.products);
            if (str.Length < 40) return str;
            return "Left click to scavange";
        }

        public override bool start_interaction(player player)
        {
            if (timer != null) Destroy(timer.gameObject);
            timer = Resources.Load<scavange_timer>("ui/scavange_timer").inst();
            timer.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
            timer.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            timer.scavanging = scavangable;
            controls.disabled = true;
            return false;
        }

        public override bool continue_interaction(player player)
        {
            return timer == null;
        }

        public override void end_interaction(player player)
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