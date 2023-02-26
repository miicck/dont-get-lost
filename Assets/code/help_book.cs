using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class help_book
{
    public delegate string text_generator();

    /// <summary> Add a help topic to the help book. </summary>
    /// <param name="topic"> The title of the topic. Can be a path of subtopics, seperated by '/'. </param>
    /// <param name="help_text"> Function that will generate the help text to display 
    /// (a generator so the help text can be dynamic). </param>
    public static void add_entry(string topic, text_generator help_text_generator)
    {
        topic parent = root_topic;

        var tree = topic.Split('/');

        // Recurse down the topic tree, adding subtopics if neccassary
        for (int i = 0; i < tree.Length; ++i)
        {
            tree[i] = tree[i].ToLower().capitalize_each_word();
            if (parent.try_get_subtopic(tree[i], out topic t))
                // Find this level of the neirarchy
                parent = t;
            else
                // Create missing intermediate levels of the heirarchy
                parent = new topic(tree[i], parent: parent);

            // If this is the topic corresponding to this entry
            // then set the content text
            if (i == tree.Length - 1)
                parent.generator = help_text_generator;
        }
    }

    public static bool open
    {
        get => open_topic != null;
        set
        {
            if (open_topic.open == value)
                return; // Already correct state

            if (value)
                // Open the root topic
                open_topic = root_topic;
            else
                // Close the current topic
                open_topic = null;
        }
    }

    public class topic
    {
        public topic parent { get; private set; }
        public string title { get; private set; }
        public text_generator generator { get; set; }

        public bool try_get_subtopic(string title, out topic t) => _children.TryGetValue(title, out t);

        Dictionary<string, topic> _children = new Dictionary<string, topic>();

        public topic(string title, text_generator generator = null, topic parent = null)
        {
            this.parent = parent;
            this.title = title;
            this.generator = generator;

            if (parent != null)
                parent._children[title] = this;
        }

        public RectTransform ui
        {
            get
            {
                if (_ui == null)
                {
                    // Create the menu & put it in the right place
                    _ui = Resources.Load<RectTransform>("ui/help_book_page").inst(game.canvas.transform);
                    _ui.anchoredPosition = Vector2.zero;

                    // Starts disabled
                    _ui.gameObject.SetActive(false);

                    // Setup the menu close action
                    var close_button = _ui.find_child_recursive("HelpBookClose").GetComponentInChildren<UnityEngine.UI.Button>();
                    close_button.onClick.AddListener(() => { player.current?.force_interaction(null); });

                    // Setup the back action
                    var back_button = _ui.find_child_recursive("HelpBookBack").GetComponentInChildren<UnityEngine.UI.Button>();
                    if (parent == null)
                        back_button.gameObject.SetActive(false);
                    else
                        back_button.onClick.AddListener(() => { open_topic = parent; });

                    // Set the content title/text
                    _ui.find_child_recursive("ContentTitle").GetComponent<UnityEngine.UI.Text>().text = title;
                    
                    // Find template subtopic button
                    var button_template = _ui.find_child_recursive("SubtopicButton").GetComponent<UnityEngine.UI.Button>();

                    // Setup subtopic buttons (alphabetical order)
                    var subtopics = new List<topic>(_children.Values);
                    subtopics.Sort((a, b) => a.title.CompareTo(b.title));

                    foreach (var c in subtopics)
                    {
                        // Create a copy of the button in the right container
                        var button = button_template.inst(button_template.transform.parent);

                        // Setup the switch-topic button
                        var c_copy_for_lambda = c;
                        button.GetComponentInChildren<UnityEngine.UI.Text>().text = c.title;
                        button.onClick.AddListener(() => { open_topic = c_copy_for_lambda; });
                    }

                    // Destroy the template
                    Object.Destroy(button_template.gameObject);
                }

                // Update content text
                _ui.find_child_recursive("ContentText").GetComponent<UnityEngine.UI.Text>().text = generator();

                return _ui;
            }
        }
        RectTransform _ui;

        public bool open
        {
            get => ui.gameObject.activeInHierarchy;
            set => ui.gameObject.SetActive(value);
        }
    }

    // The top-level help topic, containing all subtopics
    static topic root_topic = new topic("Help topics", () => 
        "Remember, if you don't know how to interact with somthing, look at it " +
        "and check the text in the bottom right of the screen. The possible " +
        "interactions also depend on what (if anything) you have equipped.");

    static topic open_topic
    {
        get
        {
            if (_open_topic == null)
                _open_topic = root_topic;
            return _open_topic;
        }
        set
        {
            // Close old topic
            _open_topic.open = false;

            // Set/open new topic
            _open_topic = value;
            if (_open_topic != null)
                _open_topic.open = true;
        }
    }
    static topic _open_topic;
}
