using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : MonoBehaviour
{
    public recipes.RECIPE_GROUP recipe_group;

    inventory_section _inventory;
    public inventory_section inventory
    {
        get
        {
            if (_inventory == null)
            {
                // Create the workbench inventory
                _inventory = Resources.Load<inventory_section>("ui/workbench").inst();
                _inventory.transform.SetParent(FindObjectOfType<Canvas>().transform);
                _inventory.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                _inventory.GetComponentInChildren<UnityEngine.UI.Text>().text = GetComponent<item>().name.capitalize();
                _inventory.GetComponent<crafting_input>().recipe_group = recipe_group;
            }
            return _inventory;
        }
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            Destroy(_inventory.gameObject);
    }
}
