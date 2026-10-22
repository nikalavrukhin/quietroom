using UnityEngine;
using UnityEngine.UI;

namespace QuietRoom
{
    // Интерфейс телефона Белых на его экране: вкладки «Звонки», «Сообщения», «Контакты».
    // Ссылки на экраны ищутся по именам при запуске: сериализованные поля с UI-типами
    // в этом компоненте ломали чтение сцены в собранной игре (level0 is corrupted).
    public class PhoneUI : MonoBehaviour, IInspectPages
    {
        GameObject[] screens;
        Graphic[] tabMarks;
        Text clock;
        int current;

        public string Help => "← → вкладки · Q — убрать";

        public void Prev() { if (current > 0) Sfx.PlayNear("lamp_click", 0.15f); Show(current - 1); }
        public void Next() { if (screens != null && current < screens.Length - 1) Sfx.PlayNear("lamp_click", 0.15f); Show(current + 1); }
        public void SetInspecting(bool on) { }

        void Start()
        {
            var root = transform.Find("Screen");
            if (root == null) return;
            screens = new[] { Child(root, "Calls"), Child(root, "Messages"), Child(root, "Contacts") };
            tabMarks = new Graphic[3];
            for (int i = 0; i < 3; i++)
            {
                var t = root.Find("Tab" + i);
                tabMarks[i] = t != null ? t.GetComponent<Graphic>() : null;
            }
            var c = root.Find("Clock");
            clock = c != null ? c.GetComponent<Text>() : null;
            Show(0);
        }

        static GameObject Child(Transform root, string name)
        {
            var t = root.Find(name);
            return t != null ? t.gameObject : null;
        }

        void Show(int i)
        {
            if (screens == null) return;
            current = Mathf.Clamp(i, 0, screens.Length - 1);
            for (int k = 0; k < screens.Length; k++) if (screens[k] != null) screens[k].SetActive(k == current);
            for (int k = 0; k < tabMarks.Length; k++)
                if (tabMarks[k] != null) tabMarks[k].color = k == current ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.15f);
        }

        void Update()
        {
            if (clock != null && GameDirector.I != null) clock.text = GameDirector.I.ClockText;
        }
    }
}
