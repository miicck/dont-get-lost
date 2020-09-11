using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class scavangable : MonoBehaviour, IInspectable, IAcceptLeftClick
{
    product[] products => GetComponents<product>();

    scavange_timer timer;

    public void on_left_click()
    {
        if (timer != null) Destroy(timer.gameObject);
        timer = Resources.Load<scavange_timer>("ui/scavange_timer").inst();
        timer.transform.SetParent(FindObjectOfType<game>().main_canvas.transform);
        timer.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        timer.scavanging = this;
        controls.disabled = true;
    }

    public void complete_scavange()
    {
        controls.disabled = false;
        foreach (var p in products)
            p.create_in(player.current.inventory);
    }

    // IINspectable
    public string inspect_info() { return product.product_plurals_list(products) + " can be scavanged."; }
    public Sprite main_sprite() { return Resources.Load<Sprite>("sprites/default_interact_cursor"); }
    public Sprite secondary_sprite() { return null; }
}