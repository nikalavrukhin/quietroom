using UnityEngine;

namespace QuietRoom
{
    // Весь интерфейс на IMGUI: брифинг, прицел, разговор, телефон, осмотр предметов, финал.
    public class HUD : MonoBehaviour
    {
        public PlayerController player;
        public Interrogation talk;
        public BankFriend friend;
        public Contacts contacts;

        // Палитра тёплого нуара.
        static readonly Color Ink = new Color(0.07f, 0.06f, 0.05f, 0.88f);
        static readonly Color Paper = new Color(0.86f, 0.82f, 0.74f);
        static readonly Color Ochre = new Color(0.88f, 0.70f, 0.42f);
        static readonly Color Muted = new Color(0.56f, 0.53f, 0.48f);
        static readonly Color Sage = new Color(0.55f, 0.66f, 0.58f);

        bool typing, phoneOpen, examining;
        string sayText = "";
        Font font;
        GUIStyle body, sub, hist, nameStyle, placeholder, keycap, keycapText, barIdle, barActive, small, title, big, hint, panel, bubbleMine, bubbleHis, phoneBody, field, phoneField, phoneScreen, phoneSelStyle, phonePill;
        Texture2D vignette, white, bottomFade, blob;
        int startFrame = -1;
        float startTime = -10f, typingSince, phoneAnim, pickedAt = -10f;
        Pickup lastHeld;
        // Подсказки по клавишам показываются только первые пару раз.
        readonly System.Collections.Generic.Dictionary<string, int> hintSeen = new System.Collections.Generic.Dictionary<string, int>();
        float bumpedPick = -99f;
        float vw, vh;
        int histIndex, histSeen = -1;   // какая реплика в барабане (0 — свежая)
        float histAnim, histAcc;        // плавная позиция барабана и накопитель колеса

        // ---------- громкость: по умолчанию звук выключен ----------

        float volume, lastOnVolume = 0.6f, volumeToastUntil;

        void Start()
        {
            volume = PlayerPrefs.GetFloat("volume", 0f);
            lastOnVolume = Mathf.Max(0.3f, PlayerPrefs.GetFloat("volumeOn", 0.6f));
            AudioListener.volume = volume;
        }

        void SetVolume(float v, bool toast)
        {
            volume = Mathf.Clamp01(v);
            if (volume > 0.01f) lastOnVolume = volume;
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat("volume", volume);
            PlayerPrefs.SetFloat("volumeOn", lastOnVolume);
            if (toast) volumeToastUntil = Time.time + 1.8f;
        }

        // Ползунок мышью (на экране дела курсор свободен).
        void VolumeSlider(Rect r)
        {
            var e = Event.current;
            var hit = new Rect(r.x - 8f, r.y - 10f, r.width + 16f, r.height + 20f);
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition) && (e.type == EventType.MouseDrag ? dragVolume : true))
            {
                dragVolume = true;
                SetVolume(Mathf.Round((e.mousePosition.x - r.x) / r.width * 20f) / 20f, false);
                e.Use();
            }
            if (e.type == EventType.MouseUp) dragVolume = false;
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(new Rect(r.x, r.center.y - 2f, r.width, 4f), white);
            GUI.color = volume > 0.01f ? Ochre : new Color(0.5f, 0.48f, 0.44f);
            GUI.DrawTexture(new Rect(r.x, r.center.y - 2f, r.width * volume, 4f), white);
            GUI.DrawTexture(new Rect(r.x + r.width * volume - 7f, r.center.y - 7f, 14f, 14f), Avatar());
            GUI.color = Color.white;
        }
        bool dragVolume;

        bool Ready => player != null && talk != null && friend != null && contacts != null && GameDirector.I != null;

        void Update()
        {
            if (!Ready) return;
            var game = GameDirector.I;

            // Наблюдатель: своё не играем, только Esc — настройки (там «Выйти из игры»).
            if (Spectate.Watching)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && rebinding < 0) { if (settingsOpen) CloseSettings(); else OpenSettings(); }
                Binds.Paused = settingsOpen;
                Time.timeScale = 1f;
                GUIUtility.keyboardControl = 0;
                return;
            }
            // Открыта админка (F2): игра на паузе, клавиши её.
            if (AdminPanel.Open)
            {
                Binds.Paused = true;
                Time.timeScale = game.Playing ? 0f : 1f;
                player.inputLocked = true;
                return;
            }
            bool escKey = Input.GetKeyDown(KeyCode.Escape) && Time.frameCount != AdminPanel.ClosedFrame;   // Esc, закрывший админку, меню не открывает
            // Код комнаты на экране дела: Enter — подключиться, Esc — отмена.
            if (codeEntry)
            {
                if (escKey) codeEntry = false;
                return;
            }
            // Esc в игре: пауза и экран дела («Продолжить»). Пока открыт ввод, телефон или осмотр, Esc закрывает их.
            // На экране дела Esc закрывает настройки, а без них — продолжает игру. После конца партии Esc — настройки.
            if (game.state != GameState.Playing) menuOpen = false;
            if (escKey && rebinding < 0)
            {
                if (settingsOpen) CloseSettings();
                else if (menuOpen) Resume();
                else if (game.Playing && !(typing || phoneOpen || examining || reading != null)) menuOpen = true;
                else if (game.Over) OpenSettings();
            }
            bool paused = settingsOpen || menuOpen;
            Binds.Paused = paused;
            Time.timeScale = paused && game.Playing ? 0f : 1f;

            bool enterKey = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (game.state == GameState.Intro && !settingsOpen && !codeEntry && enterKey) StartRun();
            else if (menuOpen && !settingsOpen && enterKey) Resume();
            // Звук: клавиша из настроек включает/выключает, [ и ] — тише/громче. Не трогаем, пока печатаешь.
            if (!typing && !phoneTyping && !settingsOpen)
            {
                if (Binds.Down(Act.Mute)) SetVolume(volume > 0.01f ? 0f : (lastOnVolume > 0.01f ? lastOnVolume : 0.6f), true);
                if (Input.GetKeyDown(KeyCode.LeftBracket)) SetVolume(Mathf.Max(0f, volume - 0.1f), true);
                if (Input.GetKeyDown(KeyCode.RightBracket)) SetVolume(Mathf.Min(1f, volume + 0.1f), true);
            }
            if (game.Over && Time.time - game.endTime > 1.5f && Input.GetKeyDown(KeyCode.R) && !settingsOpen) game.Restart();
            if (!game.Playing) { typing = phoneOpen = examining = false; player.inputLocked = false; return; }
            if (paused) { player.inputLocked = true; return; }
            if (reading != null)
            {
                player.inputLocked = true;
                // E и Esc закрывают; в кадр открытия не закрываем (нажатие E то же самое).
                if (Time.frameCount > readFrame + 1 && (Input.GetKeyDown(KeyCode.Escape) || Binds.Down(Act.Use))) reading = null;
                return;
            }

            if (!typing)
            {
                // Tab (телефон) обрабатывается в OnGUI — см. там.
                if (Binds.Down(Act.Inspect) && player.held != null && !phoneOpen) SetExamining(!examining);
                if (Input.GetKeyDown(KeyCode.Escape) && !phoneOpen) SetExamining(false);   // в телефоне Esc — «назад», он обрабатывается там
                if (examining && player.held == null) examining = false;
                if (examining) InspectInput();
            }
            phoneAnim = Mathf.MoveTowards(phoneAnim, phoneOpen ? 1f : 0f, Time.deltaTime * 5f);
            if (player.held != lastHeld) { lastHeld = player.held; if (lastHeld != null) pickedAt = Time.time; }
            player.inputLocked = typing || phoneOpen || examining;
        }

        void SetExamining(bool on)
        {
            if (examining == on) return;
            examining = on;
            if (player.held != null) player.held.SetInspect(on);
        }

        // При осмотре: стрелки/AD листают, мышь крутит предмет.
        void InspectInput()
        {
            var item = player.held;
            var pages = item.GetComponent<IInspectPages>();
            bool left = Input.GetKeyDown(KeyCode.LeftArrow) || Binds.Down(Act.Left);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) || Binds.Down(Act.Right);
            if (pages != null)
            {
                if (left) pages.Prev();
                if (right) pages.Next();
            }
            float limit = pages != null ? 20f : 180f;
            item.spin.x = Mathf.Clamp(item.spin.x + Input.GetAxisRaw("Mouse X") * 3f, -limit, limit);
            item.spin.y = Mathf.Clamp(item.spin.y + Input.GetAxisRaw("Mouse Y") * 3f, -limit * 0.5f, limit * 0.5f);
        }

        void Styles()
        {
            if (body != null) return;
            font = FontUtil.Ui;
            bottomFade = MakeFade(64);
            blob = MakeBlob(128, 64);
            white = Texture2D.whiteTexture;
            vignette = MakeVignette(256);

            body = new GUIStyle { font = font, fontSize = 19, wordWrap = true, richText = true };
            body.normal.textColor = Paper;
            sub = new GUIStyle(body) { fontSize = 26, alignment = TextAnchor.UpperCenter };
            sub.normal.textColor = new Color(0.97f, 0.95f, 0.91f);
            hist = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.UpperCenter };
            hist.normal.textColor = new Color(0.78f, 0.76f, 0.71f);
            nameStyle = new GUIStyle(body) { fontStyle = FontStyle.Bold, fontSize = 13, alignment = TextAnchor.UpperCenter };
            nameStyle.normal.textColor = Ochre;
            placeholder = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            placeholder.normal.textColor = new Color(0.62f, 0.6f, 0.56f);
            keycap = new GUIStyle { normal = { background = Rounded(32, 8, new Color(1f, 1f, 1f, 0.10f), new Color(1f, 1f, 1f, 0.22f)) }, border = new RectOffset(8, 8, 8, 8) };
            keycapText = new GUIStyle(nameStyle) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            keycapText.normal.textColor = new Color(0.9f, 0.88f, 0.84f);
            barIdle = new GUIStyle { normal = { background = Rounded(64, 27, new Color(0.06f, 0.055f, 0.05f, 0.55f), new Color(1f, 1f, 1f, 0.10f)) }, border = new RectOffset(27, 27, 27, 27) };
            barActive = new GUIStyle { normal = { background = Rounded(64, 27, new Color(0.06f, 0.055f, 0.05f, 0.85f), new Color(0.88f, 0.70f, 0.42f, 0.6f)) }, border = new RectOffset(27, 27, 27, 27) };
            small = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.UpperLeft };
            small.normal.textColor = Muted;
            hint = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.UpperCenter };
            hint.normal.textColor = new Color(0.9f, 0.86f, 0.78f, 0.85f);
            title = new GUIStyle(body) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            title.normal.textColor = Ochre;
            big = new GUIStyle(body) { fontSize = 19, alignment = TextAnchor.UpperCenter };

            panel = new GUIStyle { normal = { background = Rounded(48, 18, new Color(0.06f, 0.055f, 0.05f, 0.88f), new Color(1f, 1f, 1f, 0.08f)) }, border = new RectOffset(18, 18, 18, 18), padding = new RectOffset(18, 18, 14, 14) };
            phoneBody = new GUIStyle { normal = { background = Rounded(64, 30, new Color(0.13f, 0.122f, 0.115f, 0.98f), new Color(0.38f, 0.36f, 0.33f)) }, border = new RectOffset(30, 30, 30, 30) };
            phoneScreen = new GUIStyle { normal = { background = Rounded(64, 24, new Color(0.065f, 0.06f, 0.055f)) }, border = new RectOffset(24, 24, 24, 24) };
            phoneSelStyle = new GUIStyle { normal = { background = Rounded(32, 12, new Color(1f, 1f, 1f, 0.07f)) }, border = new RectOffset(12, 12, 12, 12) };
            phonePill = new GUIStyle { normal = { background = Rounded(32, 15, new Color(0.15f, 0.14f, 0.13f)) }, border = new RectOffset(15, 15, 15, 15) };
            bubbleMine = new GUIStyle(small) { normal = { background = Rounded(32, 12, new Color(0.30f, 0.40f, 0.36f)), textColor = Paper }, border = new RectOffset(12, 12, 12, 12), padding = new RectOffset(12, 12, 8, 8), fontSize = 15 };
            bubbleHis = new GUIStyle(bubbleMine) { normal = { background = Rounded(32, 12, new Color(0.22f, 0.20f, 0.18f)), textColor = Paper } };
            field = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleLeft, normal = { background = null, textColor = new Color(0.97f, 0.95f, 0.91f) }, wordWrap = false, clipping = TextClipping.Clip };
            phoneField = new GUIStyle(body) { normal = { background = Rounded(32, 10, new Color(0.17f, 0.155f, 0.14f)), textColor = Paper }, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(12, 12, 9, 9), wordWrap = false, clipping = TextClipping.Clip };
            phoneField.focused = phoneField.normal;
            phoneField.hover = phoneField.normal;
            field.focused = field.normal;
            field.hover = field.normal;
        }

        void OnGUI()
        {
            if (!Ready) return;
            Styles();
            // Tab ловим здесь, а не через Input: поле ввода кода само реагирует на Tab (переход между полями),
            // из-за этого повторное открытие телефона терялось. Забираем клавишу себе через Use().
            var ev = Event.current;
            // Идёт переназначение клавиши: первая нажатая клавиша становится новой (Esc — отмена).
            if (settingsOpen && rebinding >= 0 && ev.type == EventType.KeyDown && ev.keyCode != KeyCode.None)
            {
                if (ev.keyCode != KeyCode.Escape) Binds.Set((Act)rebinding, ev.keyCode);
                rebinding = -1;
                ev.Use();
            }
            var phoneKey = Binds.Key(Act.Phone);
            if (ev.type == EventType.KeyDown && ev.keyCode == phoneKey && GameDirector.I.Playing && !AdminPanel.Open && !typing && !settingsOpen && !menuOpen && reading == null && (!phoneTyping || phoneKey == KeyCode.Tab))
            {
                phoneOpen = !phoneOpen;
                SetExamining(false);
                phoneTyping = false;
                if (phoneOpen) phoneChat = -1;   // открываем на списке чатов
                GUIUtility.keyboardControl = 0;
                ev.Use();
            }
            var game = GameDirector.I;
            // Интерфейс рисуется в виртуальном экране высотой ~860 и растягивается под реальный.
            float scale = Mathf.Max(1f, Screen.height / 860f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            vw = w; vh = h;

            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, w, h), vignette);
            GUI.color = Color.white;

            if (Spectate.Watching && game.state == GameState.Intro) { WatchWaiting(w, h); if (settingsOpen) Settings(w, h); return; }
            if (game.state == GameState.Intro || menuOpen) { Intro(w, h); if (settingsOpen) Settings(w, h); return; }
            if (game.Playing)
            {
                Crosshair(w, h);
                Dialogue(w, h);
                Held(w, h);
                if (examining && player.held != null) Examine(w, h);
                if (reading != null) ReadCard(w, h);
                if (phoneOpen || phoneAnim > 0.01f) Phone(w, h);
                if (!phoneOpen && TotalUnread() > 0)
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.Label(new Rect(w - 330, 24, 310, 30), "<color=#8fa894>●</color> Новое сообщение" + Once("tab_unread", " · Tab", 2), hint);
                    GUI.color = Color.white;
                }
                if (Time.time < volumeToastUntil || (volume <= 0.01f && Time.time - startTime < 10f))
                {
                    float va = volume <= 0.01f && Time.time >= volumeToastUntil ? Mathf.Clamp01((10f - (Time.time - startTime)) / 2f) : Mathf.Clamp01((volumeToastUntil - Time.time) * 2f);
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Max(va, Time.time < volumeToastUntil ? va : 0f));
                    GUI.Label(new Rect(24f, 22f, 420f, 24f), volume > 0.01f ? "Звук · " + Mathf.RoundToInt(volume * 100f) + "%" : "Звук выключен · " + Binds.Name(Binds.Key(Act.Mute)) + " — включить", new GUIStyle(hint) { alignment = TextAnchor.UpperLeft });
                    GUI.color = Color.white;
                }
                // Брифинг плавно растворяется — как будто спускаешься в темноту.
                float since = Time.time - startTime;
                if (since < 0.9f) Fill(new Color(0.05f, 0.045f, 0.04f, 0.94f * (1f - Ease(since / 0.9f))));
            }
            else End(w, h);
            RoomBadge(w, h);
            if (settingsOpen) Settings(w, h);
        }

        // ---------- наблюдение ----------

        bool codeEntry;
        string codeText = "";
        static Texture2D pillIdle, pillHot, pillField;

        // Плашка в углу: у игрока — код комнаты и сколько смотрят, у наблюдателя — что он смотрит и как переключить камеру.
        void RoomBadge(float w, float h)
        {
            var sp = Spectate.I;
            if (sp == null) return;
            var st = new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.UpperLeft, richText = true };
            if (Spectate.Watching)
            {
                string state = !sp.Connected ? "<color=#c97a5a>нет связи, переподключаюсь…</color>"
                    : sp.Hosts == 0 ? "<color=#c97a5a>игрок отключился</color>"
                    : sp.Stale ? "<color=#c97a5a>игрок не отвечает…</color>" : "<color=#8fa894>●</color> в эфире";
                Shadowed(new Rect(24f, 20f, 700f, 22f), "<b>НАБЛЮДЕНИЕ · " + Spectate.Code + "</b>  " + state, st);
                Shadowed(new Rect(24f, 42f, 700f, 22f), "V — " + (sp.FreeCam ? "его глазами" : "свободная камера") + (sp.FreeCam ? " · WASD, мышь, пробел/C" : "") + " · Esc — меню", new GUIStyle(st) { normal = { textColor = Muted } });
                return;
            }
            if (string.IsNullOrEmpty(Spectate.Code) || !GameDirector.I.Playing) return;
            string who = sp.Watchers > 0 ? "<color=#8fa894>●</color> смотрят: " + sp.Watchers : (sp.Connected ? "никто не смотрит" : "нет связи");
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            Shadowed(new Rect(w - 324f, h - 30f, 300f, 22f), "Комната " + Spectate.Code + " · " + who, new GUIStyle(st) { alignment = TextAnchor.UpperRight, normal = { textColor = Muted } });
            GUI.color = Color.white;
        }

        // Наблюдатель подключился, а игрок ещё на экране дела.
        void WatchWaiting(float w, float h)
        {
            Fill(new Color(0.05f, 0.045f, 0.04f, 0.97f));
            var sp = Spectate.I;
            string line = sp == null || !sp.Connected ? "Подключаюсь к комнате " + Spectate.Code + "…"
                : sp.Hosts == 0 ? "Комната " + Spectate.Code + ": игрока пока нет. Пусть друг нажмёт «Начать игру»."
                : "Друг читает дело. Как только он начнёт — ты окажешься в подвале.";
            GUI.Label(new Rect(0, h * 0.42f, w, 40f), "НАБЛЮДЕНИЕ", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 30 });
            GUI.Label(new Rect(0, h * 0.42f + 50f, w, 30f), line, new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 17, normal = { textColor = Paper } });
            if (sp != null && !sp.Connected && !string.IsNullOrEmpty(sp.LastError))
                GUI.Label(new Rect(0, h * 0.42f + 84f, w, 24f), sp.LastError, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
            GUI.Label(new Rect(0, h - 60f, w, 24f), "Esc — меню", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        // Состояние экрана игрока — для наблюдателя.
        internal void RemoteState(out bool ty, out string tx, out bool po, out int pc, out int ps, out bool pt, out string ptx, out bool ex, out string rd)
        {
            ty = typing; tx = sayText; po = phoneOpen; pc = phoneChat; ps = phoneSel; pt = phoneTyping; ptx = phoneText; ex = examining;
            rd = reading != null ? reading.name : "";
        }

        internal void ApplyRemote(bool ty, string tx, bool po, int pc, int ps, bool pt, string ptx, bool ex, string rd)
        {
            if (ty && !typing) typingSince = Time.time;
            typing = ty; sayText = tx ?? "";
            phoneOpen = po; phoneChat = pc; phoneSel = ps; phoneTyping = pt; phoneText = ptx ?? "";
            examining = ex;
            phoneAnim = Mathf.MoveTowards(phoneAnim, phoneOpen ? 1f : 0f, Time.deltaTime * 5f);
            string cur = reading != null ? reading.name : "";
            if (cur != (rd ?? ""))
            {
                reading = null;
                if (!string.IsNullOrEmpty(rd))
                    foreach (var b in FindObjectsByType<BoardItem>(FindObjectsSortMode.None))
                        if (b.name == rd) { reading = b; readSince = Time.time; break; }
            }
            if (player.held != lastHeld) { lastHeld = player.held; if (lastHeld != null) pickedAt = Time.time; }
        }

        // ---------- экран дела ----------

        bool menuOpen;      // пауза: в игре открыт экран дела

        // Продолжить после паузы. Enter, которым продолжили, не должен открыть строку ввода в том же кадре.
        void Resume()
        {
            menuOpen = false;
            startFrame = Time.frameCount;
        }

        void StartRun()
        {
            var game = GameDirector.I;
            if (game.state != GameState.Intro) return;
            game.StartGame();
            if (Spectate.I != null) Spectate.I.Host();   // код комнаты для наблюдателя
            startFrame = Time.frameCount;
            startTime = Time.time;
            // Белых не заговаривает первым: событие уйдёт ИИ вместе с первой репликой детектива.
            talk.PlayerAction("Детектив вошёл в подвал через тяжёлую железную дверь. Ты только что очнулся.", false);
        }

        void Intro(float w, float h)
        {
            Fill(new Color(0.05f, 0.045f, 0.04f, 0.96f));
            float pw = Mathf.Min(1100f, w - 140f), x0 = (w - pw) / 2f, gap = 80f, colW = (pw - gap) / 2f;
            var cap = new GUIStyle(nameStyle) { alignment = TextAnchor.UpperLeft, fontSize = 12 };
            var text = new GUIStyle(body) { fontSize = 16, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.82f, 0.79f, 0.73f) } };
            text.richText = true;
            float top = Mathf.Max(36f, h * 0.06f);

            GUI.Label(new Rect(x0, top, 500f, 18f), Spaced("ДЕЛО №1"), cap);
            GUI.Label(new Rect(x0, top + 22f, 900f, 58f), "Бухгалтер", new GUIStyle(title) { alignment = TextAnchor.UpperLeft, fontSize = 44, fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.97f, 0.95f, 0.91f) } });
            float y0 = top + 94f;
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            GUI.DrawTexture(new Rect(x0, y0, pw, 1f), white);
            GUI.color = Color.white;
            y0 += 26f;
            const float SectionGap = 26f, LabelGap = 24f;

            // Левая колонка: что случилось, кто есть кто, как играть.
            float y = y0;
            GUI.Label(new Rect(x0, y, colW, 16f), Spaced("ЧТО СЛУЧИЛОСЬ"), cap); y += LabelGap;
            float sh = text.CalcHeight(new GUIContent(CaseFile.BriefStory), colW);
            GUI.Label(new Rect(x0, y, colW, sh), CaseFile.BriefStory, text); y += sh + SectionGap;

            GUI.Label(new Rect(x0, y, colW, 16f), Spaced("КТО ЕСТЬ КТО"), cap); y += LabelGap;
            for (int i = 0; i < CaseFile.BriefWho.GetLength(0); i++)
            {
                string line = "<b><color=#f0e6d2>" + CaseFile.BriefWho[i, 0] + "</color></b> — " + CaseFile.BriefWho[i, 1];
                float lh = text.CalcHeight(new GUIContent(line), colW - 18f);
                GUI.color = new Color(0.88f, 0.70f, 0.42f);
                GUI.DrawTexture(new Rect(x0, y + 8f, 4f, 4f), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(x0 + 18f, y, colW - 18f, lh), line, text);
                y += lh + 8f;
            }
            y += SectionGap - 8f;

            GUI.Label(new Rect(x0, y, colW, 16f), Spaced("КАК ИГРАТЬ"), cap); y += LabelGap;
            float gh = text.CalcHeight(new GUIContent(CaseFile.BriefTips), colW);
            GUI.Label(new Rect(x0, y, colW, gh), CaseFile.BriefTips, text);

            // Правая колонка: задача в рамке, как проверить код, условия.
            float x1 = x0 + colW + gap;
            y = y0;
            var task = new GUIStyle(body) { fontSize = 19, alignment = TextAnchor.UpperLeft, normal = { textColor = new Color(0.97f, 0.95f, 0.91f) } };
            float th = task.CalcHeight(new GUIContent(CaseFile.BriefTask), colW - 48f);
            float boxH = th + 70f;
            GUI.Box(new Rect(x1, y, colW, boxH), GUIContent.none, panel);
            GUI.Label(new Rect(x1 + 24f, y + 18f, colW - 48f, 16f), Spaced("ТВОЯ ЗАДАЧА"), cap);
            GUI.Label(new Rect(x1 + 24f, y + 44f, colW - 48f, th), CaseFile.BriefTask, task);
            y += boxH + SectionGap;

            GUI.Label(new Rect(x1, y, colW, 16f), Spaced("КАК ПРОВЕРИТЬ КОД"), cap); y += LabelGap;
            float ch = text.CalcHeight(new GUIContent(CaseFile.BriefCheck), colW);
            GUI.Label(new Rect(x1, y, colW, ch), CaseFile.BriefCheck, text); y += ch + SectionGap;

            GUI.Label(new Rect(x1, y, colW, 16f), Spaced("УСЛОВИЯ"), cap); y += LabelGap;
            string rules = CaseFile.Rules(GameDirector.I);
            float rh = text.CalcHeight(new GUIContent(rules), colW);
            GUI.Label(new Rect(x1, y, colW, rh), rules, text);

            // Низ: «Начать игру» (после старта — «Продолжить»). Настройки — шестерёнка справа вверху.
            float by = h - Mathf.Max(36f, h * 0.05f) - 46f;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.4f);
            var bigBtn = new GUIStyle(body) { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { background = Rounded(64, 23, new Color(0.88f, 0.70f, 0.42f, 0.10f), new Color(0.88f, 0.70f, 0.42f, 0.35f + 0.4f * pulse)), textColor = Ochre }, hover = { background = Rounded(64, 23, new Color(0.88f, 0.70f, 0.42f, 0.22f), new Color(0.88f, 0.70f, 0.42f, 0.9f)), textColor = new Color(1f, 0.86f, 0.55f) }, active = { background = Rounded(64, 23, new Color(0.88f, 0.70f, 0.42f, 0.3f), new Color(0.88f, 0.70f, 0.42f, 1f)), textColor = Color.white }, border = new RectOffset(23, 23, 23, 23) };
            bool started = GameDirector.I.state != GameState.Intro;
            if (GUI.Button(new Rect(x0, by, 300f, 46f), started ? "Продолжить" : "Начать игру", bigBtn) && !settingsOpen)
            {
                if (started) Resume(); else StartRun();
            }
            if (started)
            {
                string room = string.IsNullOrEmpty(Spectate.Code) ? "" : "   ·   код для друга: <b><color=#e0b46b>" + Spectate.Code + "</color></b>"
                    + (Spectate.I != null && Spectate.I.Watchers > 0 ? " (смотрят: " + Spectate.I.Watchers + ")" : "");
                GUI.Label(new Rect(x0 + 324f, by, 700f, 46f), "Пауза · часы стоят" + room, new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.MiddleLeft, richText = true, normal = { textColor = Muted } });
            }
            else
            {
                // Наблюдать за игрой друга: он жмёт «Начать игру», ты вводишь код его комнаты.
                if (pillIdle == null)
                {
                    pillIdle = Rounded(64, 23, new Color(1f, 1f, 1f, 0.03f), new Color(0.82f, 0.79f, 0.73f, 0.28f));
                    pillHot = Rounded(64, 23, new Color(1f, 1f, 1f, 0.06f), new Color(0.88f, 0.70f, 0.42f, 0.8f));
                    pillField = Rounded(64, 23, new Color(0f, 0f, 0f, 0.35f), new Color(0.88f, 0.70f, 0.42f, 0.6f));
                }
                var pill = new GUIStyle(body) { fontSize = 16, alignment = TextAnchor.MiddleCenter, border = new RectOffset(23, 23, 23, 23),
                    normal = { background = pillIdle, textColor = Paper }, hover = { background = pillHot, textColor = new Color(1f, 0.86f, 0.55f) }, active = { background = pillHot, textColor = Color.white } };
                float bx = x0 + 324f;
                if (!codeEntry)
                {
                    // Неприметная строчка: играет почти всегда один, наблюдатель найдёт.
                    var link = new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(0.56f, 0.53f, 0.48f, 0.55f) }, hover = { textColor = Paper } };
                    if (GUI.Button(new Rect(bx, by, 200f, 46f), "наблюдать за другом", link) && !settingsOpen) { codeEntry = true; codeText = ""; }
                }
                else
                {
                    var fr = new Rect(bx, by, 170f, 46f);
                    GUI.Box(fr, GUIContent.none, new GUIStyle { border = new RectOffset(23, 23, 23, 23), normal = { background = pillField } });
                    GUI.skin.settings.selectionColor = new Color(0.88f, 0.70f, 0.42f, 0.18f);
                    GUI.skin.settings.cursorColor = new Color(1f, 0.9f, 0.7f);
                    GUI.SetNextControlName("roomcode");
                    var codeStyle = new GUIStyle(field) { fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.9f, 0.7f) } };
                    codeText = GUI.TextField(new Rect(fr.x + 16f, fr.y, fr.width - 32f, fr.height), codeText.ToUpperInvariant(), 6, codeStyle);
                    GUI.FocusControl("roomcode");
                    if (codeText.Length == 0) GUI.Label(fr, "КОД", new GUIStyle(codeStyle) { normal = { textColor = new Color(1f, 1f, 1f, 0.22f) } });
                    var e = Event.current;
                    bool go = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
                    if (GUI.Button(new Rect(fr.xMax + 12f, by, 170f, 46f), "Подключиться", pill)) go = true;
                    if (go && codeText.Trim().Length >= 4 && Spectate.I != null) { codeEntry = false; Spectate.I.Watch(codeText); e.Use(); }
                    GUI.Label(new Rect(bx, by + 52f, 640f, 20f), "Друг жмёт «Начать игру», потом Esc — там его код. Esc — отмена.", new GUIStyle(small) { fontSize = 13 });
                }
            }

            // Шестерёнка настроек.
            var gearRect = new Rect(w - 56f, 20f, 34f, 34f);
            bool hot = gearRect.Contains(Event.current.mousePosition);
            GUI.color = hot ? Ochre : new Color(0.82f, 0.79f, 0.73f, 0.8f);
            GUI.DrawTexture(gearRect, Gear());
            GUI.color = Color.white;
            if (GUI.Button(gearRect, GUIContent.none, GUIStyle.none) && !settingsOpen) OpenSettings();
        }

        static Texture2D gearTex;

        // Значок-шестерёнка: кольцо с восемью зубцами и дыркой, сглаженные края.
        static Texture2D Gear()
        {
            if (gearTex != null) return gearTex;
            const int N = 96;
            gearTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float c = (N - 1) / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (N / 2f);
                    float ang = Mathf.Atan2(dy, dx);
                    float tooth = Mathf.Clamp01((Mathf.Cos(ang * 8f) - 0.2f) * 3f);     // 8 зубцов с плоскими вершинами
                    float outer = Mathf.Lerp(0.70f, 0.94f, tooth);
                    float a = Mathf.Clamp01((outer - r) * N * 0.5f) * Mathf.Clamp01((r - 0.30f) * N * 0.5f);
                    gearTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            gearTex.Apply();
            return gearTex;
        }

        // ---------- настройки: громкость и управление (шестерёнка справа вверху на экране дела, Esc после партии) ----------

        bool settingsOpen;
        int rebinding = -1;

        // ---------- чтение бумаг с детективной доски ----------

        BoardItem reading;
        float readSince;
        int readFrame;

        public void Read(BoardItem item)
        {
            if (reading != null || settingsOpen) return;
            reading = item;
            readSince = Time.unscaledTime;
            readFrame = Time.frameCount;
            Sfx.PlayNear("paper_place", 0.4f);
        }

        Font typeFont, typeFontBold, serifFont;
        Texture2D paperTex;

        Texture2D PaperTexture()
        {
            if (paperTex != null) return paperTex;
            paperTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(5);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = (float)rng.NextDouble();
                    float g = 0.93f + n * 0.05f - (y / 64f) * 0.02f;
                    paperTex.SetPixel(x, y, new Color(0.92f * g, 0.88f * g, 0.78f * g, 1f));
                }
            paperTex.Apply();
            return paperTex;
        }

        // Лист бумаги: тень, сама бумага, тонкая кромка и едва заметные сгибы.
        void PaperSheet(Rect r, float alpha, bool newspaper)
        {
            for (int i = 0; i < 6; i++)
            {
                float d = 3f + i * 3.5f;
                GUI.color = new Color(0f, 0f, 0f, 0.07f * alpha);
                GUI.DrawTexture(new Rect(r.x - d + 8f, r.y - d + 14f, r.width + d * 2f, r.height + d * 2f), white);
            }
            GUI.color = new Color(newspaper ? 0.86f : 1f, newspaper ? 0.86f : 1f, newspaper ? 0.84f : 1f, alpha);
            GUI.DrawTextureWithTexCoords(r, PaperTexture(), new Rect(0, 0, r.width / 64f, r.height / 64f));
            GUI.color = new Color(0.45f, 0.38f, 0.28f, 0.25f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1.5f), white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1.5f, r.width, 1.5f), white);
            GUI.DrawTexture(new Rect(r.x, r.y, 1.5f, r.height), white);
            GUI.DrawTexture(new Rect(r.xMax - 1.5f, r.y, 1.5f, r.height), white);
            // сгибы: горизонтальные складки на третях
            GUI.color = new Color(0.3f, 0.24f, 0.16f, 0.07f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.33f, r.width, 2f), white);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.66f, r.width, 2f), white);
            GUI.color = new Color(1f, 1f, 1f, 0.12f * alpha);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.33f + 2f, r.width, 1.5f), white);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height * 0.66f + 2f, r.width, 1.5f), white);
        }

        void Tape(Vector2 center, float angle, float alpha)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = new Color(0.86f, 0.80f, 0.55f, 0.72f * alpha);
            GUI.DrawTexture(new Rect(center.x - 38f, center.y - 12f, 76f, 24f), white);
            GUI.color = new Color(1f, 1f, 1f, 0.18f * alpha);
            GUI.DrawTexture(new Rect(center.x - 38f, center.y - 12f, 76f, 5f), white);
            GUI.matrix = saved;
        }

        void ReadCard(float w, float h)
        {
            if (typeFont == null)
            {
                typeFont = FontUtil.Mono;
                serifFont = FontUtil.Os(new[] { "Times New Roman", "Georgia", "Times" }, 20);
            }
            float a = Ease((Time.unscaledTime - readSince) / 0.3f);
            Fill(new Color(0.02f, 0.015f, 0.01f, 0.72f * a));
            bool news = reading.newspaper;
            bool hasPhoto = reading.photo != null;
            var ink = new Color(0.14f, 0.12f, 0.1f);

            float photoW = hasPhoto ? Mathf.Min(300f, w * 0.22f) : 0f;
            float photoH = hasPhoto ? photoW * reading.photo.height / reading.photo.width : 0f;
            float paperW = hasPhoto ? Mathf.Min(500f, w - photoW - 220f) : Mathf.Min(560f, w - 160f);
            float pad = 34f;
            var titleStyle = new GUIStyle(body) { font = news ? serifFont : typeFont, fontSize = news ? 30 : 21, fontStyle = FontStyle.Bold, wordWrap = true, normal = { textColor = ink } };
            var bodyStyle = new GUIStyle(body) { font = news ? serifFont : typeFont, fontSize = news ? 21 : 19, alignment = TextAnchor.UpperLeft, wordWrap = true, normal = { textColor = ink } };
            bodyStyle.richText = false; titleStyle.richText = false;
            string titleText = hasPhoto ? "" : (news ? reading.title : reading.title.ToUpper());
            float th = hasPhoto ? 0f : titleStyle.CalcHeight(new GUIContent(titleText), paperW - pad * 2f);
            float bh = bodyStyle.CalcHeight(new GUIContent(reading.body), paperW - pad * 2f);
            float paperH = Mathf.Max(hasPhoto ? 300f : 260f, th + bh + pad * 2f + (hasPhoto ? 40f : (news ? 34f : 92f)));
            float groupH = Mathf.Max(paperH, hasPhoto ? photoH + 120f : 0f);

            float groupW = hasPhoto ? photoW + 60f + paperW : paperW;
            float gx = (w - groupW) / 2f, gy = (h - groupH) / 2f + (1f - a) * 30f;
            var center = new Vector2(w / 2f, h / 2f);
            var matrix = GUI.matrix;
            float settle = 1f - a;
            GUIUtility.ScaleAroundPivot(Vector2.one * (0.9f + 0.1f * a), center);

            // Фотография слева: белая кромка с широким низом, как у распечатки; чуть наклонена.
            if (hasPhoto)
            {
                var pivot = new Vector2(gx + photoW / 2f, gy + photoH / 2f + 20f);
                var m2 = GUI.matrix;
                GUIUtility.RotateAroundPivot(-3f + settle * -6f, pivot);
                // Подпись может занять две строки: кромка снизу растёт под неё.
                var cap = new GUIStyle(body) { font = typeFont, fontSize = 20, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.UpperCenter, wordWrap = true, normal = { textColor = new Color(0.16f, 0.2f, 0.4f, a) } };
                float capH = cap.CalcHeight(new GUIContent(reading.title), photoW + 16f);
                var outer = new Rect(gx - 14f, gy + 20f - 14f, photoW + 28f, photoH + 14f + Mathf.Max(52f, capH + 30f));
                for (int i = 0; i < 6; i++)
                {
                    float d = 3f + i * 3.5f;
                    GUI.color = new Color(0f, 0f, 0f, 0.07f * a);
                    GUI.DrawTexture(new Rect(outer.x - d + 8f, outer.y - d + 14f, outer.width + d * 2f, outer.height + d * 2f), white);
                }
                GUI.color = new Color(0.94f, 0.91f, 0.84f, a);
                GUI.DrawTexture(outer, white);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(gx, gy + 20f, photoW, photoH), reading.photo, ScaleMode.StretchToFill);
                // подпись от руки под снимком
                GUI.color = Color.white;
                GUI.Label(new Rect(outer.x + 6f, gy + 20f + photoH + 14f, outer.width - 12f, capH + 4f), reading.title, cap);
                Tape(new Vector2(gx + photoW / 2f, gy + 8f), -4f, a);
                GUI.matrix = m2;
            }

            // Бумага справа (или единственный лист).
            var paper = new Rect(gx + (hasPhoto ? photoW + 60f : 0f), gy + (hasPhoto ? 14f : 0f), paperW, paperH);
            var m3 = GUI.matrix;
            GUIUtility.RotateAroundPivot((hasPhoto ? 1.6f : -1.2f) + settle * 5f, paper.center);
            PaperSheet(paper, a, news);
            Tape(new Vector2(paper.center.x, paper.y + 4f), hasPhoto ? 3f : -2f, a);
            GUI.color = new Color(1f, 1f, 1f, a);
            float ty = paper.y + pad;
            if (!hasPhoto)
            {
                GUI.Label(new Rect(paper.x + pad, ty, paperW - pad * 2f, th), titleText, titleStyle);
                ty += th + 8f;
                GUI.color = new Color(0.14f, 0.12f, 0.1f, 0.55f * a);
                GUI.DrawTexture(new Rect(paper.x + pad, ty, paperW - pad * 2f, 2f), white);
                GUI.color = new Color(1f, 1f, 1f, a);
                ty += 18f;
            }
            GUI.Label(new Rect(paper.x + pad, ty, paperW - pad * 2f, bh + 6f), reading.body, bodyStyle);
            // красный штамп «ДЕЛО» на документах без фото
            if (!hasPhoto && !news)
            {
                var m4 = GUI.matrix;
                GUIUtility.RotateAroundPivot(-14f, new Vector2(paper.xMax - 90f, paper.yMax - 56f));
                GUI.color = new Color(0.7f, 0.17f, 0.13f, 0.5f * a);
                var st = new GUIStyle(body) { font = typeFontBold ?? typeFont, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.7f, 0.17f, 0.13f, 0.55f * a) } };
                GUI.DrawTexture(new Rect(paper.xMax - 150f, paper.yMax - 82f, 120f, 40f), white);
                GUI.color = new Color(0.92f, 0.88f, 0.78f, a);
                GUI.DrawTexture(new Rect(paper.xMax - 147f, paper.yMax - 79f, 114f, 34f), white);
                GUI.color = Color.white;
                GUI.Label(new Rect(paper.xMax - 150f, paper.yMax - 82f, 120f, 40f), "ДЕЛО №1", st);
                GUI.matrix = m4;
            }
            GUI.matrix = m3;
            GUI.matrix = matrix;
            GUI.color = Color.white;
            // закрыть — снизу, как все остальные подсказки
            Shadowed(new Rect(w / 2f - 420f, h - 62f, 840f, 34f), Binds.Label(Act.Use) + " — закрыть",
                new GUIStyle(hint) { fontSize = 18, normal = { textColor = new Color(0.98f, 0.94f, 0.86f, a) } });
        }

        GUIStyle keyBtn, keyBtnActive, menuBtn, solidPanel;

        void OpenSettings() { settingsOpen = true; rebinding = -1; }

        void CloseSettings() { settingsOpen = false; rebinding = -1; }

        void Settings(float w, float h)
        {
            if (keyBtn == null)
            {
                var edge = new Color(1f, 1f, 1f, 0.16f);
                keyBtn = new GUIStyle(body) { fontSize = 15, alignment = TextAnchor.MiddleCenter, border = new RectOffset(8, 8, 8, 8),
                    normal = { background = Rounded(32, 8, new Color(1f, 1f, 1f, 0.08f), edge), textColor = Paper },
                    hover = { background = Rounded(32, 8, new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.35f)), textColor = Color.white },
                    active = { background = Rounded(32, 8, new Color(1f, 1f, 1f, 0.22f), Ochre), textColor = Color.white } };
                keyBtnActive = new GUIStyle(keyBtn) { normal = { background = Rounded(32, 8, new Color(0.88f, 0.70f, 0.42f, 0.18f), Ochre), textColor = Ochre } };
                keyBtnActive.hover = keyBtnActive.normal; keyBtnActive.active = keyBtnActive.normal;
                menuBtn = new GUIStyle(keyBtn) { fontSize = 16 };
            }
            Fill(new Color(0f, 0f, 0f, 0.78f));
            float pw = Mathf.Min(780f, w - 80f), ph = Mathf.Min(600f, h - 60f);
            var r = new Rect((w - pw) / 2f, (h - ph) / 2f, pw, ph);
            if (solidPanel == null) solidPanel = new GUIStyle { normal = { background = Rounded(48, 18, new Color(0.085f, 0.077f, 0.068f, 1f), new Color(1f, 1f, 1f, 0.10f)) }, border = new RectOffset(18, 18, 18, 18) };
            GUI.Box(r, GUIContent.none, solidPanel);
            float x = r.x + 40f, iw = pw - 80f, y = r.y + 30f;
            var cap = new GUIStyle(nameStyle) { alignment = TextAnchor.UpperLeft, fontSize = 12 };

            GUI.Label(new Rect(x, y, iw, 40f), "Настройки", new GUIStyle(title) { alignment = TextAnchor.UpperLeft, fontSize = 30, fontStyle = FontStyle.Normal, normal = { textColor = new Color(0.97f, 0.95f, 0.91f) } });
            y += 62f;

            GUI.Label(new Rect(x, y, iw, 16f), Spaced("ГРОМКОСТЬ"), cap); y += 26f;
            VolumeSlider(new Rect(x, y, iw - 90f, 20f));
            GUI.Label(new Rect(x + iw - 70f, y - 2f, 70f, 24f), Mathf.RoundToInt(volume * 100f) + "%", new GUIStyle(body) { fontSize = 17, alignment = TextAnchor.MiddleRight });
            y += 54f;

            GUI.Label(new Rect(x, y, iw, 16f), Spaced("УПРАВЛЕНИЕ"), cap); y += 28f;
            int n = Binds.Names.Length, perCol = (n + 1) / 2;
            float colW = (iw - 40f) / 2f;
            for (int i = 0; i < n; i++)
            {
                float cx = x + (i / perCol) * (colW + 40f), cy = y + (i % perCol) * 46f;
                GUI.Label(new Rect(cx, cy, colW - 130f, 36f), Binds.Names[i], new GUIStyle(body) { fontSize = 16, alignment = TextAnchor.MiddleLeft });
                bool waiting = rebinding == i;
                if (GUI.Button(new Rect(cx + colW - 120f, cy, 120f, 36f), waiting ? "нажми клавишу" : Binds.Name(Binds.Key((Act)i)), waiting ? keyBtnActive : keyBtn))
                    rebinding = waiting ? -1 : i;
            }
            y += perCol * 46f + 8f;
            if (GUI.Button(new Rect(x, y, 200f, 34f), "Сбросить управление", menuBtn)) { Binds.Reset(); rebinding = -1; }

            // Низ панели: продолжить и выйти.
            float by = r.yMax - 30f - 44f;
            if (GUI.Button(new Rect(x, by, 200f, 44f), "Закрыть", menuBtn)) CloseSettings();
            if (GUI.Button(new Rect(x + iw - 200f, by, 200f, 44f), "Выйти из игры", menuBtn)) Application.Quit();
        }

        void Crosshair(float w, float h)
        {
            if (phoneOpen || examining || reading != null) return;
            bool hot = player.target != null;
            GUI.color = hot ? Ochre : new Color(1f, 1f, 1f, 0.45f);
            float s = hot ? 6f : 4f;
            GUI.DrawTexture(new Rect(w / 2f - s / 2f, h / 2f - s / 2f, s, s), white);
            GUI.color = Color.white;
            string text = player.target != null ? player.target.Hint(player) : "";
            if (string.IsNullOrEmpty(text) && player.held != null && player.held.kind == ItemKind.Pistol) text = "ЛКМ — выстрелить";
            if (string.IsNullOrEmpty(text) && player.held != null && player.held.kind == ItemKind.Water) text = "ПКМ + мышь — наклонить стакан";
            if (string.IsNullOrEmpty(text) && player.held != null && player.held.kind == ItemKind.Chocolate)
            {
                var cb = player.held.GetComponent<ChocolateBar>();
                if (cb != null && !cb.unwrapped && !cb.busy) text = Binds.Label(Act.Use) + " — развернуть шоколадку";
            }
            if (string.IsNullOrEmpty(text) && player.seat != null && !player.seatBusy)
                text = Binds.Label(Act.Forward) + " " + Binds.Label(Act.Left) + " " + Binds.Label(Act.Back) + " " + Binds.Label(Act.Right) + " — встать";
            // подсказка висит внизу экрана, под субтитрами
            if (!string.IsNullOrEmpty(text))
                Shadowed(new Rect(w / 2f - 420f, h - 62f, 840f, 34f), text, new GUIStyle(hint) { fontSize = 18, normal = { textColor = new Color(0.98f, 0.94f, 0.86f) } });
        }

        void Dialogue(float w, float h)
        {
            Keys();

            float barW = Mathf.Min(760f, w - 60f), barH = 54f;
            var bar = new Rect((w - barW) / 2f, h - 130f, barW, barH);

            // Печатаешь — открыта история диалога и строка ввода. Иначе — только субтитры.
            if (typing && !phoneOpen && !examining)
            {
                float ta = Ease((Time.time - typingSince) / 0.25f);
                Fill(new Color(0f, 0f, 0f, 0.2f * ta));
                bar.y += (1f - ta) * 26f;
                GUI.color = new Color(1f, 1f, 1f, ta);
                // Высота барабана — по самой длинной из видимых реплик, чтобы текст не налезал на подписи.
                float slotH = 100f;
                var histStyle = new GUIStyle(sub) { fontSize = 22 };
                for (int i = Mathf.Max(0, talk.log.Count - 3); i < talk.log.Count; i++)
                    slotH = Mathf.Max(slotH, histStyle.CalcHeight(new GUIContent(talk.log[i].text), bar.width) + 34f);
                History(new Rect(bar.x, bar.y - slotH - 34f, bar.width, slotH));
                InputBar(bar);
                GUI.color = Color.white;
                return;
            }
            Subtitles(w, h, h - 140f);
        }

        // Enter — открыть строку / отправить, Esc — закрыть.
        void Keys()
        {
            if (Spectate.Watching || phoneOpen || examining || reading != null) return;
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (typing)
            {
                if (enter)
                {
                    if (!talk.waiting && sayText.Trim().Length > 0) { talk.PlayerSays(sayText); sayText = ""; typing = false; }
                    else if (sayText.Trim().Length == 0) { typing = false; subsShownAt = Time.time; }
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape) { typing = false; subsShownAt = Time.time; e.Use(); }
            }
            else if (enter && Time.frameCount > startFrame + 1)
            {
                subsShownAt = Time.time;
                typing = true;
                typingSince = Time.time;
                histIndex = 0;
                e.Use();
            }
        }

        // Сколько секунд субтитры висят после последней реплики: 7 с, длинный ответ (больше ~2 строк) — 12 с.
        static float SubsHold(Interrogation.Line his) => his != null && his.text.Length > 110 ? 12f : 7f;
        float subsShownAt = -999f;      // Enter / закрытие строки — субтитры показываются снова

        static float Shown(Interrogation.Line l) => l == null ? -999f : (l.doneAt >= 0f ? l.doneAt : (l.shown >= l.text.Length ? l.born : Time.time));

        void Subtitles(float w, float h, float bottom)
        {
            // Последняя пара реплик: твоя мелко сверху, ответ Белых крупно.
            int iMine = -1, iHis = -1;
            for (int i = 0; i < talk.log.Count; i++)
            {
                var l = talk.log[i];
                if (l.player) iMine = i;
                else if (l.who != "") iHis = i;
            }
            var last = talk.log.Count > 0 ? talk.log[talk.log.Count - 1] : null;
            // Ответ Белых показываем, только если он новее твоей реплики.
            var his = iHis > iMine ? talk.log[iHis] : null;
            var mine = iMine >= 0 ? talk.log[iMine] : null;
            var note = last != null && !last.player && last.who == "" ? last : null;
            if (his == null && mine == null && !talk.waiting && note == null) return;

            // Через 7–12 с после последней реплики субтитры плавно гаснут; новая реплика или Enter возвращают их.
            float quietSince = Mathf.Max(subsShownAt, Shown(his), Shown(mine), Shown(note));
            float a = talk.waiting ? 1f : 1f - Ease((Time.time - quietSince - SubsHold(his)) / 1.5f);
            if (a <= 0.001f) return;

            GUI.color = new Color(1f, 1f, 1f, 0.85f * a);
            GUI.DrawTexture(new Rect(0, h * 0.55f, w, h * 0.45f), bottomFade);
            GUI.color = new Color(1f, 1f, 1f, a);

            float tw = Mathf.Min(860f, w - 80f), tx = (w - tw) / 2f;
            float y = bottom;
            GUI.DrawTexture(new Rect(tx - 160f, bottom - 220f, tw + 320f, 240f), blob);

            if (talk.waiting)
            {
                string dots = new string('•', 1 + (int)(Time.time * 2.5f) % 3);
                y -= 34f;
                GUI.Label(new Rect(tx, y, tw, 34f), dots, new GUIStyle(sub) { normal = { textColor = Muted } });
            }
            else if (his != null)
            {
                string text = his.text.Substring(0, Mathf.Clamp((int)his.shown, 0, his.text.Length));
                float th = sub.CalcHeight(new GUIContent(his.text), tw);
                y -= th;
                float ha = Ease((Time.time - his.born) / 0.35f);
                GUI.color = new Color(1f, 1f, 1f, ha * a);
                Shadowed(new Rect(tx, y + (1f - ha) * 12f, tw, th + 4f), text, sub);
                GUI.color = new Color(1f, 1f, 1f, a);
                y -= 24f;
                GUI.Label(new Rect(tx, y, tw, 20f), Spaced(his.who.ToUpper()), nameStyle);
            }
            if (note != null && !talk.waiting)
            {
                y -= 30f;
                GUI.Label(new Rect(tx, y, tw, 24f), note.text, new GUIStyle(hist) { fontSize = 15, normal = { textColor = Muted } });
            }
            if (mine != null)
            {
                float mh = hist.CalcHeight(new GUIContent(mine.text), tw);
                y -= mh + 14f;
                // Свои реплики — без подписи, просто приглушённым зелёным.
                float ma = Ease((Time.time - mine.born) / 0.3f);
                GUI.color = new Color(1f, 1f, 1f, ma * a);
                Shadowed(new Rect(tx, y + (1f - ma) * 10f, tw, mh + 4f), mine.text, new GUIStyle(hist) { normal = { textColor = new Color(0.62f, 0.74f, 0.65f) } });
                GUI.color = Color.white;
            }
            GUI.color = Color.white;
        }

        // История одной строкой-барабаном: колесо/стрелки крутят реплики, старые выезжают сверху.
        void History(Rect slot)
        {
            int count = talk.log.Count;
            // Пришла новая реплика — она въезжает снизу, барабан возвращается к свежей.
            if (count != histSeen)
            {
                if (histSeen >= 0 && count > histSeen) histAnim = histIndex + (count - histSeen);
                histSeen = count;
                histIndex = 0;
            }

            var e = Event.current;
            if (e.type == EventType.ScrollWheel)
            {
                histAcc += e.delta.y;
                // Колесо мыши даёт ~3 за щелчок, трекпад — мелкими долями.
                while (histAcc <= -2.5f) { histIndex++; histAcc += 2.5f; }
                while (histAcc >= 2.5f) { histIndex--; histAcc -= 2.5f; }
                e.Use();
            }
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.UpArrow) { histIndex++; e.Use(); }
                else if (e.keyCode == KeyCode.DownArrow) { histIndex--; e.Use(); }
            }
            histIndex = Mathf.Clamp(histIndex, 0, Mathf.Max(0, count - 1));
            if (e.type == EventType.Repaint)
                histAnim = Mathf.Lerp(histAnim, histIndex, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 11f));

            if (count == 0)
            {
                GUI.Label(slot, "Он ждёт, что ты скажешь.", new GUIStyle(hist) { normal = { textColor = Muted } });
                return;
            }

            GUI.DrawTexture(new Rect(slot.x - 140f, slot.y - 40f, slot.width + 280f, slot.height + 80f), blob);
            var nameC = new GUIStyle(nameStyle);
            var text = new GUIStyle(sub) { fontSize = 22 };
            var mineText = new GUIStyle(text) { normal = { textColor = new Color(0.62f, 0.74f, 0.65f) } };
            var noteText = new GUIStyle(hist) { fontSize = 16, fontStyle = FontStyle.Italic, normal = { textColor = Muted } };

            float spacing = slot.height * 0.9f;
            var saved = GUI.matrix;
            // k = 0 — самая свежая реплика, k = 1 — перед ней и т.д.
            int from = Mathf.Max(0, Mathf.FloorToInt(histAnim) - 1), to = Mathf.Min(count - 1, Mathf.CeilToInt(histAnim) + 1);
            for (int k = from; k <= to; k++)
            {
                float d = histAnim - k;              // >0 — строка уехала вниз, <0 — ещё сверху
                if (Mathf.Abs(d) >= 1f) continue;
                var l = talk.log[count - 1 - k];
                float y = slot.y + d * spacing;
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(d) * 1.3f);
                // Барабан: строка, уходящая от центра, сплющивается по высоте и чуть уменьшается.
                float sy = Mathf.Max(0.05f, Mathf.Cos(d * Mathf.PI * 0.5f));
                float sx = 1f - Mathf.Abs(d) * 0.12f;
                GUI.matrix = saved;
                GUIUtility.ScaleAroundPivot(new Vector2(sx, sy), new Vector2(slot.center.x, y + slot.height * 0.5f) * saved.m00);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                string body = l.text.Substring(0, Mathf.Clamp((int)l.shown, 0, l.text.Length));
                if (l.who == "") GUI.Label(new Rect(slot.x, y + 20f, slot.width, slot.height), body, noteText);
                else
                {
                    if (!l.player) GUI.Label(new Rect(slot.x, y, slot.width, 20f), Spaced(l.who.ToUpper()), nameC);
                    Shadowed(new Rect(slot.x, y + 24f, slot.width, slot.height - 24f), body, l.player ? mineText : text);
                }
            }
            GUI.matrix = saved;
            GUI.color = Color.white;

            // Где мы в истории.
            if (count > 1)
            {
                string pos = histIndex == 0 ? "↑ листать историю" : (count - histIndex) + " / " + count + "   ↓ к свежей";
                GUI.Label(new Rect(slot.x, slot.yMax + 2f, slot.width, 20f), pos, new GUIStyle(hint) { fontSize = 13, normal = { textColor = new Color(0.6f, 0.58f, 0.54f, 0.8f) } });
            }
            if (talk.waiting && histIndex == 0)
            {
                string dots = new string('•', 1 + (int)(Time.time * 2.5f) % 3);
                GUI.Label(new Rect(slot.x, slot.y - 30f, slot.width, 24f), CaseFile.SuspectName + " молчит " + dots, new GUIStyle(hint) { fontSize = 14, normal = { textColor = Muted } });
            }
        }

        void InputBar(Rect bar)
        {
            GUI.Box(bar, GUIContent.none, barActive);
            var key = new Rect(bar.xMax - 92f, bar.y + 12f, 78f, bar.height - 24f);
            GUI.Box(key, GUIContent.none, keycap);
            GUI.Label(key, "Enter ↵", keycapText);
            var textRect = new Rect(bar.x + 24f, bar.y, bar.width - 130f, bar.height);
            if (Spectate.Watching) GUI.Label(textRect, sayText + (Time.time % 1f < 0.5f ? "|" : ""), field);   // друг печатает
            else
            {
                GUI.SetNextControlName("say");
                bool pasted = Paste(ref sayText, 600);
                sayText = GUI.TextField(textRect, sayText, 600, field);
                GUI.FocusControl("say");
                if (pasted) CursorToEnd();
            }
            if (sayText.Length == 0)
                GUI.Label(textRect, talk.waiting ? "он ещё отвечает…" : "Что скажешь?   Esc — закрыть", placeholder);
        }

        static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) sb.Append(c).Append(' ');
            return sb.ToString().TrimEnd();
        }

        void Held(float w, float h)
        {
            if (player.held == null) return;
            float t = Time.time - pickedAt;
            float a = t < 0.3f ? Ease(t / 0.3f) : Mathf.Clamp01(1f - (t - 2.5f) / 0.6f);
            if (a <= 0f) return;
            string text = "<b>" + Capital(player.held.title) + "</b>" + "\n<color=#8a8378>" + Binds.Label(Act.Inspect) + " — рассмотреть · " + Binds.Label(Act.Drop) + " — положить</color>";
            GUI.color = new Color(1f, 1f, 1f, a);
            Shadowed(new Rect(w - 360f, h - 70f + (1f - a) * 10f, 336f, 60f), text, new GUIStyle(small) { alignment = TextAnchor.LowerRight, normal = { textColor = Paper }, fontSize = 15 });
            GUI.color = Color.white;
        }

        void Examine(float w, float h)
        {
            var item = player.held;
            var pages = item.GetComponent<IInspectPages>();
            string help = pages != null ? pages.Help
                : item.kind == ItemKind.Water ? "мышь — наклонить (осторожно, разольёшь) · Q — убрать"
                : "мышь — повертеть · Q — убрать";
            // Для предметов без страниц — одна короткая мысль детектива внизу.
            if (pages == null)
            {
                string note = CaseFile.Examine(item.kind);
                float th = sub.CalcHeight(new GUIContent(note), Mathf.Min(760f, w - 80f));
                Shadowed(new Rect((w - Mathf.Min(760f, w - 80f)) / 2f, h - 110f - th, Mathf.Min(760f, w - 80f), th + 4f), note, new GUIStyle(sub) { fontSize = 19 });
            }
            help = Once("inspect_" + item.kind, help, 2);
            if (!string.IsNullOrEmpty(help)) Shadowed(new Rect(0, h - 70f, w, 24f), help, hint);
        }

        // ---------- телефон: список чатов и переписка ----------

        class ChatRef
        {
            public string name;
            public Color color;
            public System.Collections.Generic.List<BankFriend.Msg> chat;
            public System.Func<bool> typing, canSend;
            public System.Func<int> unread;
            public System.Action clearUnread;
            public System.Action<string> send;
            public System.Func<string> status;
            public bool digits;
        }
        ChatRef[] chats;
        int phoneChat = -1, phoneSel;
        bool phoneTyping;
        string phoneText = "", flashText;
        float flashUntil;
        Suspect suspectRef;

        ChatRef[] Chats()
        {
            if (chats != null) return chats;
            var c = contacts;
            chats = new[]
            {
                new ChatRef { name = "Димон", color = new Color(0.42f, 0.5f, 0.62f), chat = friend.chat,
                    typing = () => friend.typing, canSend = () => friend.CanSend, unread = () => friend.unread,
                    clearUnread = () => friend.unread = 0, send = friend.SendCode, digits = true,
                    status = () => friend.blocked ? "заблокировал вас" : "банк «Северный» · попыток: " + (friend.maxWrong - friend.wrong) },
                new ChatRef { name = c.sergei.name, color = new Color(0.45f, 0.58f, 0.46f), chat = c.sergei.chat,
                    typing = () => c.sergei.typing, canSend = () => c.CanSend(c.sergei), unread = () => c.sergei.unread,
                    clearUnread = () => c.sergei.unread = 0, send = t => c.Send(c.sergei, t), status = () => c.sergei.sub },
                new ChatRef { name = c.marina.name, color = new Color(0.62f, 0.46f, 0.44f), chat = c.marina.chat,
                    typing = () => c.marina.typing, canSend = () => c.CanSend(c.marina), unread = () => c.marina.unread,
                    clearUnread = () => c.marina.unread = 0, send = t => c.Send(c.marina, t), status = () => c.marina.sub },
            };
            return chats;
        }

        int TotalUnread()
        {
            int n = 0;
            foreach (var ch in Chats()) n += ch.unread();
            return n;
        }

        // Клавиши телефона: ↑↓/1-3 — выбрать чат, Enter — открыть/написать, Esc — назад, E — показать Белых.
        void PhoneKeys()
        {
            if (Spectate.Watching) return;
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            var list = Chats();
            bool enter = e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter;
            if (phoneChat < 0)
            {
                if (e.keyCode == KeyCode.UpArrow || e.keyCode == Binds.Key(Act.Forward)) { phoneSel = (phoneSel + list.Length - 1) % list.Length; e.Use(); }
                else if (e.keyCode == KeyCode.DownArrow || e.keyCode == Binds.Key(Act.Back)) { phoneSel = (phoneSel + 1) % list.Length; e.Use(); }
                else if (e.keyCode >= KeyCode.Alpha1 && e.keyCode < KeyCode.Alpha1 + list.Length) { OpenChat(e.keyCode - KeyCode.Alpha1); e.Use(); }
                else if (enter) { OpenChat(phoneSel); e.Use(); }
                else if (e.keyCode == KeyCode.Escape) { phoneOpen = false; e.Use(); }
                return;
            }
            var ch = list[phoneChat];
            if (phoneTyping)
            {
                if (enter)
                {
                    if (phoneText.Trim().Length > 0 && ch.canSend()) { ch.send(phoneText); phoneText = ""; }
                    phoneTyping = false;
                    GUIUtility.keyboardControl = 0;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape) { phoneTyping = false; GUIUtility.keyboardControl = 0; e.Use(); }
                return;
            }
            if (enter) { if (ch.canSend()) { phoneTyping = true; phoneText = ""; } e.Use(); }
            else if (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.LeftArrow) { phoneChat = -1; e.Use(); }
            else if (e.keyCode == Binds.Key(Act.Use)) { ShowToSuspect(ch); e.Use(); }
        }

        void OpenChat(int i)
        {
            phoneChat = phoneSel = i;
            phoneTyping = false;
            Chats()[i].clearUnread();
        }

        // Поднести телефон к лицу Белых: он увидит последнее присланное (фото, письмо, сообщение).
        void ShowToSuspect(ChatRef ch)
        {
            if (suspectRef == null) suspectRef = FindAnyObjectByType<Suspect>();
            if (suspectRef == null || suspectRef.dead) return;
            if (Vector3.Distance(player.cam.transform.position, suspectRef.head.position) > 2.6f) { Flash("подойди к нему ближе"); return; }
            BankFriend.Msg show = null;
            for (int i = ch.chat.Count - 1; i >= 0; i--)
                if (!ch.chat[i].mine) { show = ch.chat[i]; break; }
            if (show == null) { Flash("показывать нечего"); return; }
            string what = show.showText ?? "переписку с " + ch.name + ": «" + show.text + "»";
            if (show.photo != null && show.photo.name == "nika_safe") talk.sawNikaSafe = true;
            if (show.photo != null && show.photo.name == "lawyer_letter") talk.sawLawyer = true;
            talk.PlayerAction("Детектив поднёс к твоему лицу свой телефон и показал " + what + ".", true);
            Flash("показал Белых");
        }

        void Flash(string text) { flashText = text; flashUntil = Time.time + 1.6f; }

        void Phone(float w, float h)
        {
            if (phoneOpen) PhoneKeys();
            float pw = 360f, ph = Mathf.Min(690f, h - 60f);
            float slide = Ease(phoneAnim);
            var r = new Rect(w - pw - 40f + (1f - slide) * (pw + 80f), (h - ph) / 2f, pw, ph);
            GUI.color = new Color(1f, 1f, 1f, slide);
            // Корпус: боковые кнопки выглядывают из-под рамки, рамка с кромкой, внутри — экран.
            var btn = new Color(0.25f, 0.235f, 0.22f);
            Px(new Rect(r.x - 3f, r.y + 92f, 5f, 20f), btn, slide);
            Px(new Rect(r.x - 3f, r.y + 128f, 5f, 38f), btn, slide);
            Px(new Rect(r.x - 3f, r.y + 174f, 5f, 38f), btn, slide);
            Px(new Rect(r.xMax - 2f, r.y + 150f, 5f, 60f), btn, slide);
            GUI.Box(r, GUIContent.none, phoneBody);
            var scr = new Rect(r.x + 9f, r.y + 9f, pw - 18f, ph - 18f);
            GUI.Box(scr, GUIContent.none, phoneScreen);
            StatusBar(scr, slide);
            var inner = new Rect(scr.x + 8f, scr.y + 12f, scr.width - 16f, scr.height - 34f);
            // Полоска «домой» внизу экрана.
            GUI.color = new Color(1f, 1f, 1f, 0.32f * slide);
            GUI.DrawTexture(new Rect(scr.center.x - 56f, scr.yMax - 11f, 112f, 4f), Avatar());
            GUI.color = new Color(1f, 1f, 1f, slide);

            if (phoneChat < 0) PhoneList(inner, slide);
            else PhoneChat(inner, Chats()[phoneChat], slide);

            if (Time.time < flashUntil)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((flashUntil - Time.time) * 2f) * slide);
                GUI.Label(new Rect(r.x, r.y - 30f, pw, 24f), flashText, new GUIStyle(hint) { normal = { textColor = Ochre } });
            }
            GUI.color = new Color(1f, 1f, 1f, slide);
            string close = Once("phone_close", phoneChat < 0 ? "↑↓ — выбрать · Enter — открыть · Tab — убрать" : "Enter — написать · E — показать Белых · Esc — назад", 3);
            if (!string.IsNullOrEmpty(close)) GUI.Label(new Rect(r.x - 40f, r.yMax + 6f, pw + 80f, 22f), close, hint);
            GUI.color = Color.white;
        }

        void PhoneList(Rect inner, float alpha)
        {
            var list = Chats();
            int total = TotalUnread();
            GUI.Label(new Rect(inner.x + 8f, inner.y + 30f, inner.width, 34f), "<b>Сообщения</b>", new GUIStyle(body) { fontSize = 24 });
            if (total > 0) GUI.Label(new Rect(inner.x, inner.y + 30f, inner.width - 10f, 34f), "<color=#8fa894>новых: " + total + "</color>", new GUIStyle(small) { fontSize = 13, alignment = TextAnchor.MiddleRight });
            // Строка поиска — для вида.
            var search = new Rect(inner.x + 4f, inner.y + 70f, inner.width - 8f, 30f);
            GUI.Box(search, GUIContent.none, phonePill);
            GUI.color = new Color(Muted.r, Muted.g, Muted.b, alpha);
            GUI.DrawTexture(new Rect(search.x + 13f, search.y + 9f, 11f, 11f), Ring());
            GUI.DrawTexture(new Rect(search.x + 22f, search.y + 18f, 3f, 3f), Avatar());
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(search.x + 32f, search.y, search.width, 30f), "Поиск", new GUIStyle(small) { fontSize = 14, alignment = TextAnchor.MiddleLeft });

            float y = inner.y + 112f;
            for (int i = 0; i < list.Length; i++)
            {
                var ch = list[i];
                var row = new Rect(inner.x, y, inner.width, 72f);
                if (i == phoneSel) GUI.Box(row, GUIContent.none, phoneSelStyle);
                GUI.color = new Color(ch.color.r, ch.color.g, ch.color.b, alpha);
                GUI.DrawTexture(new Rect(row.x + 6f, row.y + 12f, 46f, 46f), Avatar());
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(row.x + 6f, row.y + 12f, 46f, 46f), ch.name.Substring(0, 1), new GUIStyle(body) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                bool typingNow = ch.typing();
                if (typingNow)
                {
                    // «В сети»: зелёная точка с обводкой цвета экрана.
                    GUI.color = new Color(0.065f, 0.06f, 0.055f, alpha);
                    GUI.DrawTexture(new Rect(row.x + 39f, row.y + 45f, 15f, 15f), Avatar());
                    GUI.color = new Color(0.45f, 0.66f, 0.5f, alpha);
                    GUI.DrawTexture(new Rect(row.x + 42f, row.y + 48f, 9f, 9f), Avatar());
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                }
                var last = ch.chat.Count > 0 ? ch.chat[ch.chat.Count - 1] : null;
                int un = ch.unread();
                GUI.Label(new Rect(row.x + 64f, row.y + 12f, inner.width - 130f, 24f), "<b>" + ch.name + "</b>", new GUIStyle(body) { fontSize = 17, wordWrap = false, clipping = TextClipping.Clip });
                string preview = typingNow ? "<color=#8fa894>печатает…</color>" : last == null ? "" : (last.mine ? "Вы: " : "") + (last.photo != null ? "Фото · " : "") + last.text;
                GUI.Label(new Rect(row.x + 64f, row.y + 36f, inner.width - 100f, 22f), preview, new GUIStyle(small) { wordWrap = false, clipping = TextClipping.Clip, fontSize = 14, normal = { textColor = un > 0 ? Paper : Muted } });
                if (last != null) GUI.Label(new Rect(row.xMax - 70f, row.y + 14f, 64f, 20f), last.time, new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.UpperRight, normal = { textColor = un > 0 ? Sage : Muted } });
                if (un > 0)
                {
                    GUI.color = new Color(0.45f, 0.62f, 0.5f, alpha);
                    GUI.DrawTexture(new Rect(row.xMax - 30f, row.y + 38f, 20f, 20f), Avatar());
                    GUI.color = new Color(1f, 1f, 1f, alpha);
                    GUI.Label(new Rect(row.xMax - 30f, row.y + 38f, 20f, 20f), un.ToString(), new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                }
                if (i < list.Length - 1) Px(new Rect(row.x + 64f, row.yMax + 2f, inner.width - 70f, 1f), new Color(1f, 1f, 1f, 0.06f), alpha);
                y += 76f;
            }

            // Нижняя панель вкладок — для вида, открыты «Чаты».
            float ty = inner.yMax - 48f;
            Px(new Rect(inner.x - 8f, ty, inner.width + 16f, 1f), new Color(1f, 1f, 1f, 0.07f), alpha);
            string[] tabs = { "Чаты", "Звонки", "Контакты", "Настройки" };
            float tw = inner.width / tabs.Length;
            for (int k = 0; k < tabs.Length; k++)
            {
                var col = k == 0 ? Ochre : Muted;
                float cx = inner.x + tw * (k + 0.5f), iy = ty + 9f;
                GUI.color = new Color(col.r, col.g, col.b, alpha * (k == 0 ? 1f : 0.75f));
                if (k == 0) GUI.Box(new Rect(cx - 10f, iy + 1f, 20f, 14f), GUIContent.none, new GUIStyle { normal = { background = Avatar() }, border = new RectOffset(16, 16, 16, 16) });
                else if (k == 1) GUI.DrawTexture(new Rect(cx - 7f, iy, 15f, 15f), Ring());
                else if (k == 2) { GUI.DrawTexture(new Rect(cx - 4f, iy, 8f, 8f), Avatar()); GUI.DrawTexture(new Rect(cx - 8f, iy + 9f, 16f, 7f), Avatar()); }
                else { GUI.DrawTexture(new Rect(cx - 7f, iy, 15f, 15f), Ring()); GUI.DrawTexture(new Rect(cx - 2f, iy + 5f, 5f, 5f), Avatar()); }
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(cx - tw / 2f, ty + 26f, tw, 18f), tabs[k], new GUIStyle(small) { fontSize = 11, alignment = TextAnchor.UpperCenter, normal = { textColor = col } });
            }
        }

        // Строка состояния: часы, вырез камеры, сигнал, сеть, батарея.
        void StatusBar(Rect s, float a)
        {
            var c = Paper;
            GUI.Label(new Rect(s.x + 24f, s.y + 8f, 80f, 20f), "<b>" + GameDirector.I.ClockText + "</b>", new GUIStyle(small) { fontSize = 14, normal = { textColor = c } });
            GUI.color = new Color(0f, 0f, 0f, a);
            GUI.DrawTexture(new Rect(s.center.x - 8f, s.y + 9f, 16f, 16f), Avatar());
            GUI.color = new Color(0.22f, 0.27f, 0.33f, a);
            GUI.DrawTexture(new Rect(s.center.x - 3f, s.y + 14f, 6f, 6f), Avatar());
            GUI.color = new Color(1f, 1f, 1f, a);

            float bx = s.xMax - 48f, by = s.y + 12f;
            var line = new Color(c.r, c.g, c.b, 0.75f);
            Px(new Rect(bx, by, 24f, 1.5f), line, a);
            Px(new Rect(bx, by + 10.5f, 24f, 1.5f), line, a);
            Px(new Rect(bx, by, 1.5f, 12f), line, a);
            Px(new Rect(bx + 22.5f, by, 1.5f, 12f), line, a);
            Px(new Rect(bx + 24.5f, by + 3.5f, 2f, 5f), line, a);
            Px(new Rect(bx + 3f, by + 3f, 18f * 0.23f, 6f), Ochre, a);
            GUI.Label(new Rect(bx - 34f, s.y + 9f, 30f, 18f), "<b>LTE</b>", new GUIStyle(small) { fontSize = 10, alignment = TextAnchor.MiddleRight, normal = { textColor = c } });
            float sx = bx - 58f;
            for (int i = 0; i < 4; i++)
            {
                float bh = 4f + i * 2.7f;
                Px(new Rect(sx + i * 5f, by + 12f - bh, 3f, bh), new Color(c.r, c.g, c.b, i < 2 ? 0.9f : 0.25f), a);
            }
        }

        void Px(Rect r, Color c, float a)
        {
            GUI.color = new Color(c.r, c.g, c.b, c.a * a);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(1f, 1f, 1f, a);
        }

        Texture2D ringTex;
        Texture2D Ring()
        {
            if (ringTex != null) return ringTex;
            const int size = 32;
            ringTex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                    ringTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(c - d) * Mathf.Clamp01(d - (c - 4.5f))));
                }
            ringTex.Apply();
            return ringTex;
        }

        void PhoneChat(Rect inner, ChatRef ch, float alpha)
        {
            // Шапка: назад, аватар, имя, статус.
            GUI.Label(new Rect(inner.x + 2f, inner.y + 30f, 22f, 40f), "<color=#8a8378>‹</color>", new GUIStyle(body) { fontSize = 32, alignment = TextAnchor.MiddleLeft });
            GUI.color = new Color(ch.color.r, ch.color.g, ch.color.b, alpha);
            GUI.DrawTexture(new Rect(inner.x + 24f, inner.y + 32f, 38f, 38f), Avatar());
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(inner.x + 24f, inner.y + 32f, 38f, 38f), ch.name.Substring(0, 1), new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(inner.x + 72f, inner.y + 30f, inner.width - 76f, 24f), "<b>" + ch.name + "</b>", new GUIStyle(body) { fontSize = 17, wordWrap = false, clipping = TextClipping.Clip });
            GUI.Label(new Rect(inner.x + 72f, inner.y + 52f, inner.width - 76f, 20f), ch.typing() ? "<color=#8fa894>печатает…</color>" : ch.status(), new GUIStyle(small) { fontSize = 13, wordWrap = false, clipping = TextClipping.Clip });
            Px(new Rect(inner.x - 8f, inner.y + 80f, inner.width + 16f, 1f), new Color(1f, 1f, 1f, 0.08f), alpha);

            // Сообщения прижаты к низу; фото — картинкой в пузыре.
            var area = new Rect(inner.x, inner.y + 86f, inner.width, inner.height - 86f - 58f);
            GUI.BeginGroup(area);
            float y = area.height;
            if (ch.typing())
            {
                // Собеседник набирает: пузырь с тремя прыгающими точками.
                y -= 46f;
                GUI.Box(new Rect(0, y, 62f, 32f), GUIContent.none, bubbleHis);
                for (int d = 0; d < 3; d++)
                {
                    float k = Mathf.Max(0f, Mathf.Sin(Time.time * 6f - d * 0.9f));
                    GUI.color = new Color(Paper.r, Paper.g, Paper.b, (0.35f + 0.55f * k) * alpha);
                    GUI.DrawTexture(new Rect(15f + d * 12f, y + 13f - k * 3f, 7f, 7f), Avatar());
                }
                GUI.color = new Color(1f, 1f, 1f, alpha);
            }
            for (int i = ch.chat.Count - 1; i >= 0 && y > 0; i--)
            {
                var m = ch.chat[i];
                if (m.time == "" && !m.mine)
                {
                    y -= 26f;
                    GUI.Label(new Rect(0, y, area.width, 22f), m.text, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                    continue;
                }
                var style = m.mine ? bubbleMine : bubbleHis;
                float bw = m.photo != null ? 214f : Mathf.Min(area.width * 0.8f, style.CalcSize(new GUIContent(m.text)).x + 4f);
                float textH = string.IsNullOrEmpty(m.text) ? 0f : style.CalcHeight(new GUIContent(m.text), bw);
                float photoH = m.photo != null ? (bw - 16f) * m.photo.height / m.photo.width : 0f;
                float bh = textH + (m.photo != null ? photoH + 10f : 0f);
                y -= bh + 18f;
                float bx = m.mine ? area.width - bw : 0f;
                GUI.Box(new Rect(bx, y, bw, bh), GUIContent.none, style);
                if (m.photo != null) GUI.DrawTexture(new Rect(bx + 8f, y + 8f, bw - 16f, photoH), m.photo, ScaleMode.ScaleToFit);
                if (textH > 0f) GUI.Label(new Rect(bx, y + (m.photo != null ? photoH + 6f : 0f), bw, textH), m.text, style);
                GUI.Label(new Rect(bx, y + bh + 1f, bw, 16f), m.mine ? m.time + "  <color=#8fa894>✓✓</color>" : m.time, new GUIStyle(small) { fontSize = 11, alignment = m.mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft });
            }
            GUI.EndGroup();

            var fr = new Rect(inner.x, inner.yMax - 48f, inner.width - 50f, 42f);
            if (phoneTyping && phoneOpen && Spectate.Watching)
            {
                GUI.Box(fr, GUIContent.none, phoneField);
                GUI.Label(new Rect(fr.x + 13f, fr.y + 10f, fr.width, 24f), phoneText + (Time.time % 1f < 0.5f ? "|" : ""), new GUIStyle(small) { normal = { textColor = Paper } });
            }
            else if (phoneTyping && phoneOpen && ch.canSend())
            {
                GUI.SetNextControlName("phone");
                bool pasted = Paste(ref phoneText, ch.digits ? 8 : 600);
                string t = GUI.TextField(fr, phoneText, ch.digits ? 8 : 600, phoneField);
                if (pasted) CursorToEnd();
                phoneText = ch.digits ? System.Text.RegularExpressions.Regex.Replace(t, "[^0-9]", "") : t;
                GUI.FocusControl("phone");
                if (phoneText.Length == 0) GUI.Label(new Rect(fr.x + 13f, fr.y + 10f, fr.width, 24f), ch.digits ? "код ячейки" : "сообщение", small);
            }
            else
            {
                GUI.Box(fr, GUIContent.none, phoneField);
                string label = !ch.canSend() ? (ch.typing() ? "ждёшь ответа…" : "сообщения недоступны") : "Enter — написать";
                GUI.Label(fr, label, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            }
            // Кнопка «отправить»: горит, когда есть что отправить.
            bool ready = phoneTyping && phoneText.Trim().Length > 0 && ch.canSend();
            var sb = new Rect(inner.xMax - 42f, inner.yMax - 48f, 42f, 42f);
            GUI.color = ready ? new Color(0.36f, 0.5f, 0.42f, alpha) : new Color(0.17f, 0.155f, 0.14f, alpha);
            GUI.DrawTexture(sb, Avatar());
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(sb, "<b>↑</b>", new GUIStyle(body) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = ready ? Paper : Muted } });
        }

        Texture2D avatarTex;
        Texture2D Avatar() => avatarTex != null ? avatarTex : (avatarTex = Rounded(64, 32, Color.white));

        void End(float w, float h)
        {
            var game = GameDirector.I;
            float a = Mathf.Clamp01((Time.time - game.endTime) / 1.5f);
            bool won = game.state == GameState.Won;
            Fill(new Color(0.04f, 0.035f, 0.03f, 0.92f * a));
            GUI.color = new Color(1f, 1f, 1f, a);
            float pw = Mathf.Min(680f, w - 60f);
            var t = new GUIStyle(title) { normal = { textColor = won ? Sage : Ochre } };
            GUI.Label(new Rect((w - pw) / 2f, h * 0.3f, pw, 50f), game.endTitle, t);
            GUI.Label(new Rect((w - pw) / 2f, h * 0.3f + 70f, pw, 200f), game.endText, big);
            if (a >= 1f) GUI.Label(new Rect((w - pw) / 2f, h * 0.3f + 260f, pw, 30f), "R — начать дело заново · Esc — выйти", hint);
            GUI.color = Color.white;
        }

        // ---------- помощники ----------

        // Cmd/Ctrl+V: в собранной игре IMGUI-поле само не вставляет из буфера.
        static bool Paste(ref string text, int max)
        {
            var ev = Event.current;
            if (ev.type != EventType.KeyDown || ev.keyCode != KeyCode.V || !(ev.command || ev.control)) return false;
            string clip = (GUIUtility.systemCopyBuffer ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            text = (text + clip);
            if (text.Length > max) text = text.Substring(0, max);
            ev.Use();
            return true;
        }

        static void CursorToEnd()
        {
            var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (te != null) te.MoveTextEnd();
        }

        void Fill(Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(0, 0, vw, vh), white);
            GUI.color = Color.white;
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var sh = new GUIStyle(style) { normal = { textColor = new Color(0f, 0f, 0f, 0.7f) } };
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), StripColor(text), sh);
            GUI.Label(r, text, style);
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        int Count(string key) { return hintSeen.TryGetValue(key, out var n) ? n : 0; }
        void Bump(string key) { hintSeen[key] = Count(key) + 1; }

        // Показывает подсказку, пока её не увидели max раз. Показ считается, когда подсказка появилась заново.
        string Once(string key, string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            bool repaint = Event.current != null && Event.current.type == EventType.Repaint;
            bool continuing = visibleKeys.Contains(key);   // была видна в прошлом кадре — тот же показ
            if (!continuing)
            {
                if (Count(key) >= max) return "";
                if (repaint) Bump(key);
            }
            if (repaint) frameKeys.Add(key);
            return text;
        }
        readonly System.Collections.Generic.HashSet<string> visibleKeys = new System.Collections.Generic.HashSet<string>();
        readonly System.Collections.Generic.HashSet<string> frameKeys = new System.Collections.Generic.HashSet<string>();

        void LateUpdate()
        {
            // Что было видно в прошлом кадре — повторный показ подряд не считается новым.
            visibleKeys.Clear();
            foreach (var k in frameKeys) visibleKeys.Add(k);
            frameKeys.Clear();
        }

        static string StripColor(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<color=[^>]*>|</color>", "");
        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        public static Texture2D Rounded(int size, int radius, Color c) => Rounded(size, radius, c, c);

        public static Texture2D Rounded(int size, int radius, Color c, Color edge)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0f, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Расстояние до края: у самой кромки — цвет обводки.
                    float edgeDist = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float inner = d > 0f ? radius - d : edgeDist + 0.5f;
                    var col = inner < 1.5f ? edge : c;
                    tex.SetPixel(x, y, new Color(col.r, col.g, col.b, col.a * Mathf.Clamp01(radius - d + 0.5f)));
                }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeBlob(int wpx, int hpx)
        {
            var tex = new Texture2D(wpx, hpx, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < hpx; y++)
                for (int x = 0; x < wpx; x++)
                {
                    float u = (x + 0.5f) / wpx * 2f - 1f, v = (y + 0.5f) / hpx * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    tex.SetPixel(x, y, new Color(0.02f, 0.015f, 0.01f, 0.78f * Mathf.SmoothStep(1f, 0f, d)));
                }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeFade(int size)
        {
            var tex = new Texture2D(1, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                // y = 0 — низ экрана (в текстуре низ = первая строка, IMGUI рисует её внизу).
                float a = Mathf.Pow(1f - (float)y / (size - 1), 1.6f) * 0.8f;
                tex.SetPixel(0, y, new Color(0.02f, 0.015f, 0.01f, a));
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MakeVignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                    tex.SetPixel(x, y, new Color(0.02f, 0.015f, 0.01f, Mathf.SmoothStep(0f, 0.75f, (d - 0.55f) / 0.75f)));
                }
            tex.Apply();
            return tex;
        }
    }
}
