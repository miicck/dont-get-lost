using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class drop_off_point : MonoBehaviour, IPlayerInteractable
{
    item_output output;

    private void Start()
    {
        output = GetComponentInChildren<item_output>();
        if (output == null)
            Debug.LogError("Drop off point has no item input!");
    }

    public void drop_off_item(string name)
    {
        output.add(name, 1);
    }

    class menu_interaction : left_player_menu
    {
        drop_off_point point;
        public menu_interaction(string name, drop_off_point point) : base(name) { this.point = point; }

        protected override RectTransform create_menu()
        {
            var menu = Resources.Load<RectTransform>("ui/drop_off_point").inst();
            var text = menu.find_child_recursive("drop_off_id_text")?.GetComponent<UnityEngine.UI.Text>();
            if (text == null)
                Debug.LogError("No ID text field found in drop off point menu!");
            text.text = "ID: " + point.GetComponentInParent<networked>().network_id;
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
}
