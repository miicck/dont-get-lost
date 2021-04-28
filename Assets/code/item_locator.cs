using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_locator : MonoBehaviour, IExtendsNetworked
{
    public item item
    {
        get => _item;
        set
        {
            if (_item == value) return; // No change

            // Destroy the previous item
            if (_item != null) Destroy(_item.gameObject);

            // Assign the new item
            _item = value;

            // If we have authority, update item_name
            if (GetComponentInParent<networked>().has_authority)
                item_name.value = _item?.name;

            if (_item == null) return;

            // Move the new item into place
            _item.transform.SetParent(transform);
            _item.transform.localPosition = Vector3.zero;
            _item.transform.localRotation = Quaternion.identity;
        }
    }
    item _item;

    public item release_item()
    {
        var tmp = _item;
        _item = null;

        if (GetComponentInParent<networked>().has_authority)
            item_name.value = null;

        return tmp;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = item == null ? Color.red : Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * item_gutter.ITEM_SEPERATION / 2f,
            Vector3.one * item_gutter.ITEM_SEPERATION);
    }

    //###################//
    // IExtendsNetworked //
    //###################//

    networked_variables.net_string item_name;

    public IExtendsNetworked.callbacks get_callbacks()
    {
        return new IExtendsNetworked.callbacks
        {
            init_networked_variables = () =>
            {
                item_name = new networked_variables.net_string();
                item_name.on_change = () =>
                {
                    if (Resources.Load<item>("items/" + item_name.value) == null)
                    {
                        // The new item_name isn't an item
                        item = null;
                        return;
                    }

                    // New value is a valid item, make sure this.item matches
                    if (item == null || item.name != item_name.value)
                    {
                        if (item != null && item.transform.parent == this)
                            Destroy(item.gameObject);

                        item = item.create(item_name.value, transform.position,
                            transform.rotation, logistics_version: true);
                    }
                };
            }
        };
    }
}
