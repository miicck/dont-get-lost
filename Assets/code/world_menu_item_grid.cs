using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_menu_item_grid : MonoBehaviour
{
    public bool reverse_direction = false;

    class image_scroller : MonoBehaviour
    {
        public bool reverse_direction = false;

        RectTransform rect_transform;
        UnityEngine.UI.Image image;

        private void Start()
        {
            rect_transform = GetComponent<RectTransform>();
            image = GetComponent<UnityEngine.UI.Image>();
            switch_sprite();
        }

        void switch_sprite()
        {
            var items = Resources.LoadAll<item>("items");
            image.sprite = null;
            while (image.sprite == null)
                image.sprite = items[Random.Range(0, items.Length)].sprite;
        }

        private void Update()
        {
            if (reverse_direction)
            {
                rect_transform.anchoredPosition += 64 * Vector2.right * Time.deltaTime;

                if (rect_transform.anchoredPosition.x > Screen.currentResolution.width)
                {
                    rect_transform.anchoredPosition = new Vector2(-64, 0);
                    switch_sprite();
                }
            }
            else
            {
                rect_transform.anchoredPosition -= 64 * Vector2.right * Time.deltaTime;

                if (rect_transform.anchoredPosition.x < -64)
                {
                    rect_transform.anchoredPosition = new Vector2(Screen.currentResolution.width, 0);
                    switch_sprite();
                }
            }
        }
    }

    void Start()
    {
        var template = transform.Find("image_template").GetComponent<UnityEngine.UI.Image>();
        template.transform.SetParent(null);

        for (int i = 0; i <= Screen.currentResolution.width / 64; ++i)
        {
            var image = template.inst();
            image.transform.SetParent(transform);
            var scroller = image.gameObject.AddComponent<image_scroller>();
            scroller.reverse_direction = reverse_direction;
            image.GetComponent<RectTransform>().anchoredPosition = new Vector2(i * 64, 0);
        }

        Destroy(template.gameObject);
    }
}
