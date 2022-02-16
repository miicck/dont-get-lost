using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class research_material_product : product
{
    public research_material material;

    public override Sprite sprite => material.sprite;
    public override bool unlocked => true;
    public override float average_amount_produced(item i) => 0f;
    public override string product_name() => material.name.Replace("_", " ").capitalize();
    public override string product_name_plural() => product_name();
    public override string product_name_quantity() => product_name();

    public override void create_in(IItemCollection inv, int count = 1, bool track_production = false)
    {
        tech_tree.add_research_materials(material, count);
    }

    public override void create_in_node(item_node node, bool track_production = false)
    {
        tech_tree.add_research_materials(material, 1);
    }
}
