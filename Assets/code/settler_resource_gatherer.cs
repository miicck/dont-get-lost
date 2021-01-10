using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Class representing a settler interaction based on a set of selectable options. </summary>
public abstract class settler_interactable_options : settler_interactable, IPlayerInteractable, IExtendsNetworked
{
    //###################//
    // IExtendsNetworked //
    //###################//

    public int selected_option => option_index.value;

    networked_variables.net_int option_index;

    public void init_networked_variables()
    {
        option_index = new networked_variables.net_int();
    }

    //##################//
    // LEFT PLAYER MENU //
    //##################//

    player_interaction[] interactions;
    public player_interaction[] player_interactions()
    {
        if (interactions == null) interactions = new player_interaction[] { new menu(this) };
        return interactions;
    }

    class menu : left_player_menu
    {
        settler_interactable_options options;
        public menu(settler_interactable_options options)
        {
            this.options = options;
        }

        protected override RectTransform create_menu()
        {
            return Resources.Load<RectTransform>("ui/resource_gatherer").inst();
        }

        protected override void on_open()
        {
            // Clear the options menu
            var content = menu.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content;
            foreach (RectTransform child in content)
                Destroy(child.gameObject);

            // Create the options menu
            for (int i = 0; i < options.options_count; ++i)
            {
                // Create a button for each option
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

                // Set the button text/sprite
                var opt = options.get_option(i);
                text.text = opt.text;
                image.sprite = opt.sprite;

                trans.SetParent(content);

                if (i == options.option_index.value)
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
                    options.option_index.value = i_copy;

                    // Refresh 
                    on_open();
                });
            }
        }
    }

    protected struct option
    {
        public string text;
        public Sprite sprite;
    }

    protected abstract option get_option(int i);
    protected abstract int options_count { get; }
}

public class settler_resource_gatherer : settler_interactable_options, IAddsToInspectionText
{
    public string display_name;
    public item_output output;
    public Transform search_origin;
    public float search_radius;

    public float time_between_harvests = 1f;
    public int max_harvests = 5;

    public tool.TYPE tool_type = tool.TYPE.AXE;
    public tool.QUALITY tool_quality = tool.QUALITY.TERRIBLE;

    List<harvestable> harvest_options;
    List<option> menu_options;

    harvestable harvesting
    {
        get
        {
            if (harvest_options == null) return null;
            if (harvest_options.Count <= selected_option) return null;
            return harvest_options[selected_option];
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

        // Sort alphabetically by text
        harvest_options.Sort((a, b) =>
            product.product_plurals_list(a.products).CompareTo(
                product.product_plurals_list(b.products)));

        menu_options = new List<option>();
        foreach (var h in harvest_options)
            menu_options.Add(new option
            {
                text = product.product_plurals_list(h.products),
                sprite = h.products[0].sprite()
            });
    }

    //##############################//
    // settler_interactable_options //
    //##############################//

    protected override option get_option(int i) { return menu_options[i]; }
    protected override int options_count => menu_options.Count;

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public string added_inspection_text()
    {
        if (harvesting == null)
            return "    Nothing in harvest range.";
        return "    Harvesting " + product.product_plurals_list(harvesting.products);
    }

    //######################//
    // SETTLER_INTERACTABLE //
    //######################//

    public override INTERACTION_RESULT on_assign(settler s)
    {
        if (harvesting == null)
            return INTERACTION_RESULT.FAILED;

        // Reset stuff 
        time_harvesting = 0f;
        harvested_count = 0;
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override INTERACTION_RESULT on_interact(settler s)
    {
        if (harvesting == null)
            return INTERACTION_RESULT.FAILED;

        // Record how long has been spent harvesting
        time_harvesting += Time.deltaTime;

        if (time_harvesting > harvested_count)
        {
            harvested_count += 1;

            // Create the products
            foreach (var p in harvesting.products)
                p.create_in_node(output);
        }

        if (harvested_count >= max_harvests)
            return INTERACTION_RESULT.COMPLETE;
        return INTERACTION_RESULT.UNDERWAY;
    }

    public override string task_info()
    {
        if (harvesting == null) return "Harvesting nothing!!!";
        return "Harvesting " + product.product_plurals_list(harvesting.products) +
            " (" + harvested_count + "/" + max_harvests + ")";
    }

    public override void on_unassign(settler s) { }
}
