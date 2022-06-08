using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class world_menu_item_grid : MonoBehaviour
{
    public bool reverse_direction = false;

    class sprite_switcher : MonoBehaviour
    {
        UnityEngine.UI.Image image;
        RectTransform rect_transform;
        public bool reverse_direction = false;

        const float MIN_TIME_BETWEEN = 12f;
        const float MAX_TIME_BETWEEN = 24f;

        enum STAGE
        {
            WAITING,
            DISAPEARING,
            REAPPEARING
        }
        STAGE stage;
        float next_time = 0;

        private void Start()
        {
            image = GetComponentInChildren<UnityEngine.UI.Image>();
            rect_transform = image.GetComponent<RectTransform>();
            switch_sprite();
            update_next_time();
            next_time -= MIN_TIME_BETWEEN;
        }

        void switch_sprite()
        {
            var items = Resources.LoadAll<item>("items");
            image.sprite = null;
            while (image.sprite == null)
                image.sprite = items[Random.Range(0, items.Length)].sprite;
        }

        float speed => 64f * (reverse_direction ? -1f : 1f);

        void update_next_time()
        {
            next_time = Time.time + Random.Range(MIN_TIME_BETWEEN, MAX_TIME_BETWEEN);
        }

        bool out_of_view()
        {
            if (reverse_direction)
                return rect_transform.anchoredPosition.y < -60f;
            return rect_transform.anchoredPosition.y > 60f;
        }

        bool position_has_reset()
        {
            if (reverse_direction)
                return rect_transform.anchoredPosition.y > 0f;
            return rect_transform.anchoredPosition.y < 0f;
        }

        private void Update()
        {
            switch (stage)
            {
                case STAGE.WAITING:
                    if (Time.time > next_time)
                        stage = STAGE.DISAPEARING;
                    return;

                case STAGE.DISAPEARING:
                    rect_transform.anchoredPosition += Vector2.up * Time.deltaTime * speed;
                    if (out_of_view())
                    {
                        switch_sprite();
                        stage = STAGE.REAPPEARING;
                    }
                    return;

                case STAGE.REAPPEARING:
                    rect_transform.anchoredPosition -= Vector2.up * Time.deltaTime * speed;
                    if (position_has_reset())
                    {
                        stage = STAGE.WAITING;
                        update_next_time();
                    }
                    return;
            }
        }
    }

    void Start()
    {
        var template = transform.Find("image_template");
        template.transform.SetParent(null);

        for (int i = 0; i <= Screen.currentResolution.width / 64; ++i)
        {
            var image = template.inst();
            image.transform.SetParent(transform);
            image.gameObject.AddComponent<sprite_switcher>().reverse_direction = reverse_direction;
            image.GetComponent<RectTransform>().anchoredPosition = new Vector2(i * 64, 0);
        }

        Destroy(template.gameObject);
    }
}
