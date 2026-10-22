using UnityEngine;

namespace QuietRoom
{
    public enum Act { Forward, Back, Left, Right, Use, Drop, Inspect, Hit, Phone, Mute }

    // Переназначаемое управление: хранится в PlayerPrefs, правится из меню по Esc.
    // Enter, Esc, стрелки и мышь не переназначаются.
    public static class Binds
    {
        public static readonly string[] Names =
            { "Вперёд", "Назад", "Влево", "Вправо", "Взять / применить", "Положить", "Осмотреть", "Ударить", "Телефон", "Звук вкл/выкл" };

        static readonly KeyCode[] Defaults =
            { KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.E, KeyCode.G, KeyCode.Q, KeyCode.F, KeyCode.Tab, KeyCode.M };

        static KeyCode[] keys;

        // Меню открыто: игра на паузе, курсор свободен.
        public static bool Paused;

        static void Load()
        {
            if (keys != null) return;
            keys = new KeyCode[Defaults.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = (KeyCode)PlayerPrefs.GetInt("bind_" + i, (int)Defaults[i]);
        }

        // Название клавиши действия — для подсказок на экране.
        public static string Label(Act a) { return Name(Key(a)); }

        public static KeyCode Key(Act a) { Load(); return keys[(int)a]; }
        public static bool Down(Act a) { return Input.GetKeyDown(Key(a)); }
        public static bool Held(Act a) { return Input.GetKey(Key(a)); }

        // Назначить клавишу; если она уже занята другим действием — они меняются местами.
        public static void Set(Act a, KeyCode k)
        {
            Load();
            int i = (int)a;
            for (int j = 0; j < keys.Length; j++)
                if (j != i && keys[j] == k) { keys[j] = keys[i]; PlayerPrefs.SetInt("bind_" + j, (int)keys[j]); }
            keys[i] = k;
            PlayerPrefs.SetInt("bind_" + i, (int)k);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            Load();
            for (int i = 0; i < keys.Length; i++) { keys[i] = Defaults[i]; PlayerPrefs.DeleteKey("bind_" + i); }
            PlayerPrefs.Save();
        }

        public static string Name(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.Space: return "Пробел";
                case KeyCode.Tab: return "Tab";
                case KeyCode.Return: return "Enter";
                case KeyCode.LeftShift: case KeyCode.RightShift: return "Shift";
                case KeyCode.LeftControl: case KeyCode.RightControl: return "Ctrl";
                case KeyCode.LeftAlt: case KeyCode.RightAlt: return "Alt";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "Num " + s.Substring(6);
            return s;
        }
    }
}
