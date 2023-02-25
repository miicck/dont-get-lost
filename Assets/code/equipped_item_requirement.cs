using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class equipped_item_requirement : tutorial_object
{
    public UnityEngine.UI.Text equip_text;
    public UnityEngine.UI.Image item_image;

    public delegate void on_complete_func();
    on_complete_func on_complete;
    item item;

    public static equipped_item_requirement create(item item, on_complete_func on_complete = null)
    {
        if (item == null)
        {
            Debug.LogError("required item == null in equipped_item_requirement!");
            on_complete?.Invoke();
            return null;
        }

        var rt = Resources.Load<RectTransform>("ui/equipped_item_requirement").inst(game.canvas.transform);
        rt.anchoredPosition = Vector2.zero;
        var ir = rt.GetComponent<equipped_item_requirement>();
        ir.equip_text.text = "Equip " + utils.a_or_an(item.display_name) + " " + item.display_name;
        ir.item_image.sprite = item.sprite;
        ir.item = item;
        ir.on_complete = on_complete;
        return ir;
    }

    private void Update()
    {
        if (player.current == null) return;
        if (player.current.equipped == null) return;
        if (player.current.equipped.name == item.name)
        {
            on_complete?.Invoke();
            Destroy(gameObject);
        }
    }
}
