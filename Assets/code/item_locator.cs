using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_locator : MonoBehaviour
{
    public item item
    {
        get => _item;
        set
        {
            if (_item != null)
                throw new System.Exception("Tried to overwrite item locator!");

            _item = value;
            if (_item == null)
                return;

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
        return tmp;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = item == null ? Color.red : Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.up * item_gutter.ITEM_SEPERATION / 2f, 
            Vector3.one * item_gutter.ITEM_SEPERATION);
    }
}
