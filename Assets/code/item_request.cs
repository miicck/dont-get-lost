using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class item_request : MonoBehaviour
{
    public item item;
    public Transform sign_location;
    item_input input;

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

        input = GetComponent<item_input>();
        if (input == null)
            throw new System.Exception("No item input found on item requester!");
    }

    private void Update()
    {
        if (input.item_count > 0)
        {
            var itm = input.release_next_item();
            item_dropper.create(itm, itm.transform.position, null);
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
