using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pickup_point : character_walk_to_interactable, IExtendsNetworked, IPlayerInteractable, IAddsToInspectionText
{
    public override string task_summary() => "Hauling item";

    item_input input;
    item_locator[] locators;
    networked_variables.net_int drop_off_id;

    public drop_off_point drop_off => networked.try_find_by_id(
        drop_off_id.value, error_if_not_recently_forgotten: false)?.GetComponentInChildren<drop_off_point>();

    town_path_element.path get_path_to_drop_off()
    {
        var from = GetComponentInChildren<town_path_element>();
        if (from == null) Debug.LogError("Pick-up path element not set correctly!");
        var to = drop_off.GetComponentInChildren<town_path_element>();
        if (to == null) Debug.LogError("Drop-off path element not set correctly!");
        return town_path_element.path.get(from, to);
    }

    new public IExtendsNetworked.callbacks get_callbacks()
    {
        return IExtendsNetworked.callbacks.combine_callbacks(new IExtendsNetworked.callbacks[]
        {
            base.get_callbacks(),
            new IExtendsNetworked.callbacks
            {
                init_networked_variables = () =>
                {
                    drop_off_id = new networked_variables.net_int();
                }
            }
        });
    }

    new public string added_inspection_text()
    {
        var base_text = base.added_inspection_text();
        var new_text = "Drop off point: " +
            (drop_off == null ? "none" : drop_off.GetComponentInParent<item>().display_name +
            " id = " + drop_off.GetComponentInParent<networked>().network_id);
        return base_text + "\n" + new_text;
    }

    class menu_interaction : left_player_menu
    {
        pickup_point point;
        public menu_interaction(string name, pickup_point point) : base(name) { this.point = point; }

        protected override RectTransform create_menu(Transform parent)
        {
            var menu = Resources.Load<RectTransform>("ui/pick_up_point").inst(parent);

            var field = menu.GetComponentInChildren<UnityEngine.UI.InputField>();
            if (field == null)
                Debug.LogError("Pick up point menu has no input field!");
            else
                field.onEndEdit.AddListener((val) =>
                {
                    if (!int.TryParse(val, out int id))
                        field.text = "Invalid id";
                    else
                    {
                        point.drop_off_id.value = id;
                        if (point.drop_off == null)
                            field.text = "Id not found";
                    }
                });

            // Initialize text
            if (point.drop_off == null)
                field.text = "Id not found";
            else
                field.text = "" + point.drop_off_id.value;

            return menu;
        }
    }

    player_interaction[] _interactions;

    public player_interaction[] player_interactions(RaycastHit hit)
    {
        if (_interactions == null)
            _interactions = new player_interaction[]
            {
                new menu_interaction(GetComponentInParent<item>().display_name, this)
            };
        return _interactions;
    }

    protected override void Start()
    {
        base.Start();

        input = GetComponentInChildren<item_input>();
        if (input == null)
            Debug.LogError("Pickup point has no input!");

        locators = GetComponentsInChildren<item_locator>();
        if (locators.Length == 0)
            Debug.LogError("Pickup point has no locators!");
    }

    private void Update()
    {
        if (input.item_count == 0)
            return; // No input to process

        foreach (var l in locators)
        {
            if (l.item != null)
                continue; // Locator already has item

            l.item = input.release_next_item();
            if (input.item_count == 0)
                break;
        }
    }

    protected override bool ready_to_assign(character c)
    {
        if (drop_off == null)
            return false;

        foreach (var l in locators)
            if (l.item != null)
                return true;
        return false;
    }

    Dictionary<character, town_path_element.path> character_paths =
        new Dictionary<character, town_path_element.path>();

    protected override STAGE_RESULT on_interact_arrived(character c, int stage)
    {
        switch (stage)
        {
            case 0:
                // Pickup items
                int i_picked_up = 0;
                foreach (var l in locators)
                    if (l.item != null)
                    {
                        var i = l.release_item();
                        if (c is settler)
                        {
                            var s = c as settler;
                            i.transform.SetParent(i_picked_up == 0 ? s.right_hand : s.left_hand);
                            i.transform.localPosition = Vector3.zero;
                            ++i_picked_up;
                        }

                        if (i_picked_up >= 2)
                            break;
                    }
                return STAGE_RESULT.STAGE_COMPLETE;

            case 1:

                // Get/remember path to drop-off
                if (!character_paths.TryGetValue(c, out town_path_element.path path))
                    path = character_paths[c] = get_path_to_drop_off();

                // Walk to drop off
                switch (path.walk(c, c.walk_speed))
                {
                    case town_path_element.path.WALK_STATE.UNDERWAY:
                        return STAGE_RESULT.STAGE_UNDERWAY;
                    default:
                        character_paths.Remove(c);
                        return STAGE_RESULT.STAGE_COMPLETE;
                }

            case 2:
                // Drop off items
                bool at_drop_off = c.town_path_element == drop_off?.GetComponentInChildren<town_path_element>();
                if (drop_off?.GetComponentInChildren<town_path_element>() == null)
                    at_drop_off = false;

                if (c is settler)
                {
                    var s = c as settler;
                    foreach (var t in new Transform[] { s.left_hand, s.right_hand })
                        foreach (var i in t.GetComponentsInChildren<item>())
                        {
                            if (at_drop_off)
                                // We're at the drop-off
                                drop_off.drop_off_item(i.name);
                            Destroy(i.gameObject);
                        }
                }
                return at_drop_off ? STAGE_RESULT.TASK_COMPLETE : STAGE_RESULT.TASK_FAILED;

            default:
                Debug.LogError("Unkown stage in pickup point: " + stage);
                return STAGE_RESULT.TASK_FAILED;
        }
    }
}
