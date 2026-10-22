using UnityEngine;
using UnityEngine.UI;

namespace QuietRoom
{
    // Подменяет шрифты на документах и телефоне системными: машинопись на бумагах, гладкий — на экране.
    public class DocFonts : MonoBehaviour
    {
        public bool typewriter = true;

        void Awake()
        {
            var font = typewriter ? FontUtil.Mono : FontUtil.Ui;
            if (font == null) return;
            foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
        }
    }
}
