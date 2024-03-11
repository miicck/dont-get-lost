
using UnityEngine;

public class MouseUIText : MonoBehaviour, IMouseTextUI
{
        public string text = "";
        public string mouse_ui_text() => this.text;
}
