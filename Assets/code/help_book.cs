using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class help_book
{
    public static bool open
    {
        get => menu.gameObject.activeInHierarchy;
        set
        {
            menu.gameObject.SetActive(value);

            // Also close the page when the book is closed
            if (!value)
                page.gameObject.SetActive(false);
        }
    }

    static RectTransform menu
    {
        get
        {
            if (_menu == null)
            {
                // Create the menu + put it in the right place
                _menu = Resources.Load<RectTransform>("ui/help_book").inst();
                _menu.SetParent(game.canvas.transform);
                _menu.anchoredPosition = Vector2.zero;

                // Menu starts disabled
                _menu.gameObject.SetActive(false);

                _menu.find_child_recursive("HelpBookClose").
                    GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(() =>
                    {
                        player.current?.force_interaction(null);
                    });

                // Find the template for creating topic entries
                var topic_template = _menu.find_child_recursive("TopicEntry");

                foreach (var kv in topics)
                {
                    // Create copies of the template for each topic
                    var entry = topic_template.inst();
                    entry.transform.SetParent(topic_template.transform.parent);
                    entry.GetComponentInChildren<UnityEngine.UI.Text>().text = kv.Key;
                    entry.GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(() =>
                    {
                        // Open the help book at this page
                        page.gameObject.SetActive(true);

                        page.find_child_recursive("HelpBookPageTitle").
                            GetComponentInChildren<UnityEngine.UI.Text>().text = kv.Key;
                        page.find_child_recursive("HelpBookPageText").
                            GetComponentInChildren<UnityEngine.UI.Text>().text = kv.Value;

                        page.find_child_recursive("HelpBookPageClose").
                            GetComponentInChildren<UnityEngine.UI.Button>().onClick.AddListener(() =>
                            {
                                page.gameObject.SetActive(false);
                            });
                    });
                }

                // Disable template
                topic_template.gameObject.SetActive(false);
            }
            return _menu;
        }
    }
    static RectTransform _menu;

    static RectTransform page
    {
        get
        {
            if (_page == null)
            {
                // Create a page
                _page = Resources.Load<RectTransform>("ui/help_book_page").inst();
                _page.SetParent(game.canvas.transform);
                _page.anchoredPosition = Vector2.zero;

                // Page starts disabled
                _page.gameObject.SetActive(false);
            }
            return _page;
        }
    }
    static RectTransform _page;

    static Dictionary<string, string> topics = new Dictionary<string, string>();

    /// <summary> Add a help topic to the help book. </summary>
    /// <param name="topic"> The title of the topic. </param>
    /// <param name="help_text"> The help text that should be displayed. </param>
    public static void add_entry(string topic, string help_text)
    {
        topic = topic.ToLower().capitalize();
        if (topics.ContainsKey(topic))
            throw new System.Exception("Help topic " + topic + " already exists!");
        topics[topic] = help_text;
    }
}
