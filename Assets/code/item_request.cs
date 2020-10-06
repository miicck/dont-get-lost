using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_request : MonoBehaviour
{
    public item item;
    public Transform sign_location;
    item_link_point input;

    void Start()
    {
        var sign = GameObject.CreatePrimitive(PrimitiveType.Quad);
        foreach (var c in sign.GetComponentsInChildren<Collider>()) Destroy(c);
        sign.transform.SetParent(sign_location);
        sign.transform.localPosition = Vector3.zero;
        sign.transform.localScale /= 2f;
        sign.AddComponent<face_player>();

        var rend = sign.GetComponentInChildren<Renderer>();
        rend.material = Resources.Load<Material>("materials/item_sign");
        rend.material.SetTexture("_BaseColorMap", item.sprite.texture);

        input = GetComponent<item_link_point>();
        if (input.type != item_link_point.TYPE.INPUT)
            throw new System.Exception("Item request link point must be an input!");
    }

    private void Update()
    {
        if (input.item != null)
        {
            if (input.item.name == item.name)
                input.drop_item();
        }
    }

    class face_player : MonoBehaviour
    {
        private void Update()
        {
            if (player.current != null)
            {
                Vector3 delta = transform.position - player.current.camera.transform.position;
                transform.forward = delta.normalized;
            }
        }
    }
}
