using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary> Class representing a settler interaction based on a set of selectable options. </summary>
public abstract class character_interactable_options : character_walk_to_interactable, IPlayerInteractable, IAddsToInspectionText
{
    //###################//
    // IExtendsNetworked //
    //###################//

    public int selected_option => option_index.value;

    networked_variables.net_int option_index;

    public override IExtendsNetworked.callbacks get_callbacks()
    {
        var ret = base.get_callbacks();
        ret.init_networked_variables += () =>
        {
            option_index = new networked_variables.net_int(min_value: 0, max_value: options_count - 1);
        };
        return ret;
    }

    public override string added_inspection_text()
    {
        if (!building_operation_requirement.all_operation_requirements_satisfied(this, out string reason))
            return "Can't assign: " + reason + "\n" + base.added_inspection_text();
        return base.added_inspection_text();
    }

    //##################//
    // LEFT PLAYER MENU //
    //##################//

    player_interaction[] interactions;
    public virtual player_interaction[] player_interactions(RaycastHit hit)
    {
        if (interactions == null) interactions = new player_interaction[] { new menu(this) };
        return interactions;
    }

    protected override bool ready_to_assign(character c)
    {
        if (!building_operation_requirement.all_operation_requirements_satisfied(this, out string reason))
            return false;
        return base.ready_to_assign(c);
    }

    class menu : left_player_menu
    {
        character_interactable_options options;
        public menu(character_interactable_options options) : base(
            options.GetComponentInParent<item>()?.display_name)
        {
            this.options = options;
        }

        protected override RectTransform create_menu()
        {
            var ret = Resources.Load<RectTransform>("ui/resource_gatherer").inst();
            ret.GetComponentInChildren<UnityEngine.UI.Text>().text = options.options_title;
            return ret;
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
    protected abstract string options_title { get; }
}

public class character_resource_gatherer : character_interactable_options, IAddsToInspectionText
{
    public string display_name;
    public item_output output;
    public Transform search_origin;
    public float search_radius;

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

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        // Draw the harvesting ray
        if (search_origin == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(search_origin.position, search_radius);
    }

    protected override void Start()
    {
        base.Start();
        load_harvesting();
    }

    void load_harvesting()
    {
        if (!chunk.generation_complete(search_origin.position, search_radius + 16f))
        {
            // Search range not yet generated, wait for generation to complete
            // (note we test quite a bit further than the search_radius in case
            //  objects from neighbouring chunks overhang into this chunk)
            Invoke("load_harvesting", 1);
            return;
        }

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
            item_product.product_plurals_list(a.products).CompareTo(
                item_product.product_plurals_list(b.products)));

        menu_options = new List<option>();
        foreach (var h in harvest_options)
            menu_options.Add(new option
            {
                text = product.product_plurals_list(h.products),
                sprite = h.products[0].sprite
            });
    }

    //##############################//
    // settler_interactable_options //
    //##############################//

    protected override option get_option(int i) { return menu_options[i]; }
    protected override int options_count => menu_options == null ? 0 : menu_options.Count;
    protected override string options_title => "Harvesting";

    //#######################//
    // IAddsToInspectionText //
    //#######################//

    public override string added_inspection_text()
    {
        if (harvest_options == null) return "Waiting for chunks to generate";
        return base.added_inspection_text() + "\n" +
         ((harvesting == null) ? "Nothing in harvest range." :
         "Harvesting " + item_product.product_plurals_list(harvesting.products));
    }

    //######################//
    // SETTLER_INTERACTABLE //
    //######################//

    float time_harvesting;
    int harvested_count = 0;

    protected override bool ready_to_assign(character c)
    {
        // Check we have something to harvest
        return harvesting != null;
    }

    protected override void on_arrive(character c)
    {
        // Reset stuff 
        time_harvesting = 0f;
        harvested_count = 0;
    }

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        if (harvesting == null)
            return STAGE_RESULT.TASK_FAILED;

        // Record how long has been spent harvesting
        time_harvesting += Time.deltaTime;

        if (time_harvesting * current_proficiency.total_multiplier / 4f > harvested_count)
        {
            harvested_count += 1;

            // Create the products
            foreach (var p in harvesting.products)
                p.create_in_node(output, true);
        }

        if (time_harvesting > 10f)
            return STAGE_RESULT.TASK_COMPLETE;
        return STAGE_RESULT.STAGE_UNDERWAY;
    }

    public override string task_summary()
    {
        if (harvesting == null) return "Harvesting nothing!!!";
        return "Harvesting " + product.product_plurals_list(harvesting.products);
    }

    protected override List<proficiency> proficiencies(character c)
    {
        var ret = base.proficiencies(c);

        if (c is settler)
        {
            var s = (settler)c;
            tool tool = tool.find_best_in(s.inventory, tool_type);
            if (tool == null) return ret;
            ret.Add(new item_based_proficiency(
                tool.proficiency, tool.display_name,
                s.inventory, tool, 0.1f));
        }

        return ret;
    }
}