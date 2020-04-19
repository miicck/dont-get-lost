using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class workbench : MonoBehaviour
{
    inventory _inventory;
    public inventory inventory
    {
        get
        {
            if (_inventory == null)
            {
                _inventory = Resources.Load<inventory>("ui/workbench").inst();
                _inventory.transform.SetParent(FindObjectOfType<Canvas>().transform);
                _inventory.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                _inventory.GetComponentInChildren<UnityEngine.UI.Text>().text = GetComponent<item>().name.capitalize();
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
