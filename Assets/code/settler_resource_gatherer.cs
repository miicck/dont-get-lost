using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class settler_resource_gatherer : settler_interactable, IAddsToInspectionText, IExtendsNetworked, ILeftPlayerMenu
{
    public item_output output;
    public Transform search_origin;
    public float search_radius;

    public float time_between_harvests = 1f;
    public int max_harvests = 5;

    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    List<harvestable> harvest_options;
    harvestable harvesting
    {
        get
        {
            if (harvest_options == null) return null;
            if (harvest_options.Count <= harvesting_index.value) return null;
            return harvest_options[harvesting_index.value];
        }
    }

    float time_harvesting;
    int harvested_count = 0;

    private void OnDrawGizmos()
    {
        // Draw the harvesting ray
        if (search_origin == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(search_origin.position, search_radius);
    }

    protected override void Start()
    {
        base.Start();

        // Wait for a little bit after chunk generation to load harvesting objects
        chunk.add_generation_listener(transform, (c) =>
        {
            Invoke("load_harvesting", 1);
        });
    }

    void load_harvesting()
    {
        // Search for harvestable objects within range
        harvest_options = new List<harvestable>();
        foreach (var c in Physics.OverlapSphere(search_origin.position, search_radius))
        {
            var h = c.GetComponentInParent<harvestable>();
            if (h == null) continue;
            if (h.tool.tool_type != tool_type) continue;
            if (h.tool.tool_quality > tool_quality) continue;
            harvest_options.Add(h);
        }

        // Sort alphabetically by display name
        harvest_options.Sort((a, b) =>
            product.product_plurals_list(a.products).CompareTo(
                product.product_plurals_list(b.products)));
    }

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (harvesting == null)
            return "    Nothing in harvest range.";
        return "    Harvesting " + product.product_plurals_list(harvesting.products);
    }

    //##################//
    // LEFT PLAYER MENU //
    //##################//

    public string left_menu_display_name() { return name; }
    public inventory editable_inventory() { return null; }
    public void on_left_menu_close() { }
    public recipe[] additional_recipes() { return null; }

    RectTransform ui;

    public RectTransform left_menu_transform()
    {
        if (ui == null)
            ui = Resources.Load<RectTransform>("ui/resource_gatherer").inst();
        return ui;
    }

    public void on_left_menu_open()
    {
        // Clear the options menu
        var content = ui.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
        foreach (RectTransform child in content)
            Destroy(child.gameObject);

        // Create the options menu
        if (harvest_options == null) return;
        for (int i = 0; i < harvest_options.Count; ++i)
        {
            var trans = Resources.Load<RectTransform>("ui/resource_option_button").inst();
            var but = trans.GetComponentInChildren<UnityEngine.UI.Button>();
            var text = trans.GetComponentInChildren<UnityEngine.UI.Text>();

            UnityEngine.UI.Image image = null;
            foreach (var img in trans.GetComponentsInChildren<UnityEngine.UI.Image>())
                if (img.sprite == null)
                {
                    image = img;
                    break;
                }

            text.text = product.product_plurals_list(harvest_options[i].products);
            image.sprite = harvest_options[i].main_sprite();

            trans.SetParent(content);

            if (i == harvesting_index.value)
            {
                var colors = but.colors;
                colors.normalColor = Color.green;
                colors.pressedColor = Color.green;
                colors.highlightedColor = Color.green;
                colors.selectedColor = Color.green;
                colors.disabledColor = Color.green;
                but.colors = colors;
            }

            int i_copy = i;
            but.onClick.AddListener(() =>
            {
                harvesting_index.value = i_copy;

                // Refresh 
                player.current.ui_state = player.UI_STATE.ALL_CLOSED;
                player.current.ui_state = player.UI_STATE.INVENTORY_OPEN;
            });
        }
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_int harvesting_index;

    public void init_networked_variables()
    {
        harvesting_index = new networked_variables.net_int();
    }

    //######################//
    // SETTLER_INTERACTABLE //
    //######################//

    public override void on_assign(settler s)
    {
        // Reset stuff 
        time_harvesting = 0f;
        harvested_count = 0;
    }

    public override void on_interact(settler s)
    {
        // Record how long has been spent harvesting
        time_harvesting += Time.deltaTime;

        if (time_harvesting > harvested_count)
        {
            harvested_count += 1;

            // Create the products
            if (harvesting != null)
                foreach (var p in harvesting.products)
                    p.create_in_node(output);
        }
    }

    public override bool is_complete(settler s)
    {
        // We're done if we've harvested enough times
        return harvested_count >= max_harvests;
    }

    public override void on_unassign(settler s) { }
}
