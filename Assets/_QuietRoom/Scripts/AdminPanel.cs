using UnityEngine;

namespace QuietRoom
{
    // Админка по F2 (на маке Fn+F2): настройки партии поверх игры. Пока открыта — игра на паузе.
    public class AdminPanel : MonoBehaviour
    {
        public static bool Open;
        public static int ClosedFrame = -1;   // кадр, в котором её закрыли Esc'ом (HUD этот Esc пропускает)

        static readonly int[] Presets = { 0, 10, 15, 30 };
        static readonly Color Ochre = new Color(0.88f, 0.70f, 0.42f);
        static readonly Color Paper = new Color(0.92f, 0.89f, 0.83f);
        static readonly Color Muted = new Color(0.56f, 0.53f, 0.48f);

        string custom = "";
        GUIStyle panel, head, cap, text, btn, btnOn, field;

        void Update()
        {
            if (Spectate.Watching) { Open = false; return; }
            if (Input.GetKeyDown(KeyCode.F2)) SetOpen(!Open);
            else if (Open && Input.GetKeyDown(KeyCode.Escape)) { SetOpen(false); ClosedFrame = Time.frameCount; }
        }

        void SetOpen(bool open)
        {
            Open = open;
            GUIUtility.keyboardControl = 0;
            if (open) custom = GameDirector.LimitMinutes > 0 && System.Array.IndexOf(Presets, GameDirector.LimitMinutes) < 0 ? GameDirector.LimitMinutes.ToString() : "";
        }

        static void SetLimit(int minutes)
        {
            GameDirector.LimitMinutes = minutes;
            if (GameDirector.I != null) GameDirector.I.ApplyLimit(minutes);
        }

        void Styles()
        {
            if (panel != null) return;
            var font = FontUtil.Ui;
            panel = new GUIStyle { normal = { background = HUD.Rounded(48, 18, new Color(0.06f, 0.055f, 0.05f, 0.96f), new Color(0.88f, 0.70f, 0.42f, 0.35f)) }, border = new RectOffset(18, 18, 18, 18) };
            head = new GUIStyle { font = font, fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Ochre } };
            cap = new GUIStyle { font = font, fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = Muted } };
            text = new GUIStyle { font = font, fontSize = 16, wordWrap = true, richText = true, normal = { textColor = Paper } };
            btn = new GUIStyle(text)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = false,
                normal = { background = HUD.Rounded(32, 10, new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.16f)), textColor = Paper },
                hover = { background = HUD.Rounded(32, 10, new Color(0.88f, 0.70f, 0.42f, 0.16f), new Color(0.88f, 0.70f, 0.42f, 0.6f)), textColor = Ochre },
                border = new RectOffset(10, 10, 10, 10),
            };
            btnOn = new GUIStyle(btn) { normal = { background = HUD.Rounded(32, 10, new Color(0.88f, 0.70f, 0.42f, 0.28f), Ochre), textColor = new Color(1f, 0.9f, 0.7f) } };
            btnOn.hover = btnOn.normal;
            field = new GUIStyle(text)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip,
                normal = { background = HUD.Rounded(32, 10, new Color(0.17f, 0.155f, 0.14f), new Color(1f, 1f, 1f, 0.2f)), textColor = Paper },
                border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(8, 8, 4, 4),
            };
            field.focused = field.normal;
            field.hover = field.normal;
        }

        void OnGUI()
        {
            if (!Open) return;
            GUI.depth = -10;   // поверх HUD и первой получает клавиши
            Styles();
            var e = Event.current;

            float scale = Mathf.Max(1f, Screen.height / 860f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = 560f, ph = 330f;
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            GUI.Box(r, GUIContent.none, panel);
            float x = r.x + 28f, y = r.y + 22f, iw = pw - 56f;

            GUI.Label(new Rect(x, y, iw, 30f), "Админка", head);
            GUI.Label(new Rect(x, y + 4f, iw, 30f), "F2 / Esc — закрыть", new GUIStyle(cap) { alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Normal, fontSize = 14 });
            y += 50f;

            GUI.Label(new Rect(x, y, iw, 18f), "ДЛИТЕЛЬНОСТЬ ПАРТИИ", cap);
            y += 26f;
            int cur = GameDirector.LimitMinutes;
            float bw = (iw - 3 * 10f) / 4f;
            for (int i = 0; i < Presets.Length; i++)
            {
                int m = Presets[i];
                if (GUI.Button(new Rect(x + i * (bw + 10f), y, bw, 40f), m == 0 ? "Без лимита" : m + " мин", cur == m ? btnOn : btn))
                {
                    SetLimit(m);
                    custom = "";
                }
            }
            y += 54f;

            // Своё значение: только цифры, Enter или кнопка — применить.
            GUI.Label(new Rect(x, y + 9f, 70f, 24f), "Своё:", text);
            bool isCustom = cur > 0 && System.Array.IndexOf(Presets, cur) < 0;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "admin_custom";
            GUI.SetNextControlName("admin_custom");
            string typed = GUI.TextField(new Rect(x + 62f, y, 90f, 40f), custom, 4, isCustom && custom == cur.ToString() ? new GUIStyle(field) { normal = { background = field.normal.background, textColor = Ochre } } : field);
            custom = System.Text.RegularExpressions.Regex.Replace(typed, "[^0-9]", "");
            GUI.Label(new Rect(x + 162f, y + 9f, 40f, 24f), "мин", text);
            int val;
            bool ok = int.TryParse(custom, out val) && val > 0;
            GUI.enabled = ok;
            if (GUI.Button(new Rect(x + 210f, y, 130f, 40f), "Применить", btn) || (enter && ok))
            {
                SetLimit(val);
                GUIUtility.keyboardControl = 0;
                if (enter) e.Use();
            }
            GUI.enabled = true;
            y += 60f;

            // Что сейчас.
            var game = GameDirector.I;
            string now = cur <= 0 ? "Сейчас: <b>без лимита</b> — утро не наступит, часы просто идут."
                : "Сейчас: <b>" + cur + " мин</b> на партию, в конце — 06:00 и полиция.";
            if (game != null && game.Playing && !game.unlimited)
            {
                float left = Mathf.Max(0f, game.realDuration - game.elapsed);
                now += "\nДо конца этой партии: <b>" + Mathf.FloorToInt(left / 60f) + ":" + Mathf.FloorToInt(left % 60f).ToString("00") + "</b>.";
            }
            else if (game != null && game.Playing) now += "\nЭта партия идёт без лимита.";
            GUI.Label(new Rect(x, y, iw, 60f), now, text);
            GUI.Label(new Rect(x, r.yMax - 34f, iw, 20f), "Меняется сразу, и в текущей партии тоже. Запоминается.", new GUIStyle(cap) { fontStyle = FontStyle.Normal, fontSize = 13 });

            // Пока админка открыта, клавиши дальше не идут (чат, телефон).
            if (e.type == EventType.KeyDown && e.keyCode != KeyCode.F2) e.Use();
            GUI.matrix = oldMatrix;
        }
    }
}
