using UnityEngine;

namespace QuietRoom
{
    // Шрифты лежат в самой игре (Resources/Fonts, Roboto — Apache 2.0), поэтому текст одинаково выглядит
    // на маке и на Windows и не зависит от того, какие шрифты стоят в системе. Системный шрифт нужен
    // только для газеты (с засечками); если его нет или в нём нет кириллицы — берём встроенный.
    public static class FontUtil
    {
        static Font ui, mono;

        // Основной шрифт интерфейса.
        public static Font Ui { get { return ui ?? (ui = Load("Roboto-Regular")); } }

        // «Печатная машинка» для бумаг и подписей.
        public static Font Mono { get { return mono ?? (mono = Load("RobotoMono-Regular")); } }

        static Font Load(string name)
        {
            var f = Resources.Load<Font>("Fonts/" + name);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static Font Os(string[] names, int size)
        {
            Font f = null;
            try { f = Font.CreateDynamicFontFromOSFont(names, size); } catch { }
            if (f != null && f.dynamic && f.HasCharacter('Ж') && f.HasCharacter('a')) return f;
            return Ui;
        }
    }
}
