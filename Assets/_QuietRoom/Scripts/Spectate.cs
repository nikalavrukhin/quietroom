using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace QuietRoom
{
    // Режим наблюдателя через ретранслятор на Cloudflare (папка Relay/ рядом с проектом).
    // Игрок (host): при старте партии получает код комнаты и, пока кто-то смотрит, 12 раз в секунду шлёт снимок —
    //   где он и куда смотрит, предметы, «кости» Белых, лампа, что открыто на экране; реплики и сообщения телефона — отдельно.
    // Наблюдатель (watch): у себя ничего не считает и нейросеть не зовёт — расставляет всё по снимкам и рисует тот же интерфейс.
    // V — переключить «его глазами» / свободная камера (WASD + мышь, пробел/C — вверх/вниз).
    public class Spectate : MonoBehaviour
    {
        public const string RelayUrl = "wss://podval-relay.podval-game.workers.dev/room/";
        const float SnapRate = 12f;

        public static Spectate I;
        public static bool Watching;          // эта игра — наблюдатель
        public static string Code = "";       // код комнаты (у игрока — сгенерированный, у наблюдателя — введённый)
        static string role;                   // "host" / "watch" / null

        public bool Connected { get; private set; }
        public int Watchers { get; private set; }
        public int Hosts { get; private set; }
        public bool FreeCam { get; private set; }
        public bool GotSnapshot => snap != null;
        public string LastError { get; private set; }

        // ---------- сообщения ----------
        [Serializable] class Head { public string t; }
        [Serializable] class Room { public string t; public int hosts; public int watchers; }
        [Serializable] class Snap
        {
            public string t = "s";
            public int st; public float el; public bool wait; public string et, ex;
            public int lim;             // лимит партии в минутах (0 — без лимита), чтобы часы у наблюдателя шли так же
            public float[] pl;          // игрок: позиция 3, поворот 4, камера (локально): позиция 3, поворот 4
            public float[] it;          // предметы по 12: позиция 3, поворот 4, активен 1, крышка/обложка 4
            public int held = -1;
            public float[] sb;          // кости Белых по 10: позиция 3, поворот 4, масштаб 3
            public float[] dl;          // голова настольной лампы
            public bool ty, po, pt, ex2; public string tx, ptx, rd; public int pc = -1, ps;
            public int lc; public int[] cc; public bool[] ctp;
            public bool cu; public int cl = 2; public float gf = 1f;
        }
        [Serializable] class LogMsg { public string t = "l"; public int i; public string who, text; public bool player; }
        [Serializable] class ChatMsg { public string t = "c"; public int ch, i; public bool mine; public string text, time, photo, show; }

        // ---------- сеть ----------
        ClientWebSocket ws;
        CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        bool connecting;
        float retryAt;

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }   // после перезапуска сцены остаёмся прежние
            I = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (I == this) { try { cts?.Cancel(); } catch { } }
        }

        // Игрок начал партию: заводим комнату (код сохраняется и после перезапуска дела).
        public void Host()
        {
            if (Watching) return;
            if (string.IsNullOrEmpty(Code))
            {
                const string abc = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
                var sb = new StringBuilder();
                for (int i = 0; i < 4; i++) sb.Append(abc[UnityEngine.Random.Range(0, abc.Length)]);
                Code = sb.ToString();
            }
            role = "host";
            Connect();
        }

        // Наблюдать за игрой по коду.
        public void Watch(string code)
        {
            Code = code.Trim().ToUpperInvariant();
            role = "watch";
            Watching = true;
            snap = null;
            EnterWatchMode();
            Connect();
        }

        void Connect()
        {
            if (connecting || Connected || role == null) return;
            connecting = true;
            cts?.Cancel();
            cts = new CancellationTokenSource();
            Run(RelayUrl + Code + "?role=" + role, cts.Token);
        }

        async void Run(string url, CancellationToken token)
        {
            var sock = new ClientWebSocket();
            ws = sock;
            try
            {
                await sock.ConnectAsync(new Uri(url), token);
                Connected = true;
                connecting = false;
                LastError = null;
                if (role == "watch") outbox.Enqueue("{\"t\":\"need\"}");
                _ = SendLoop(sock, token);
                var buf = new byte[1 << 16];
                while (sock.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult r;
                        do
                        {
                            r = await sock.ReceiveAsync(new ArraySegment<byte>(buf), token);
                            if (r.MessageType == WebSocketMessageType.Close) break;
                            ms.Write(buf, 0, r.Count);
                        } while (!r.EndOfMessage);
                        if (r.MessageType == WebSocketMessageType.Close) break;
                        inbox.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                }
            }
            catch (Exception e) { LastError = e.Message; }
            Connected = false;
            connecting = false;
            retryAt = Time.unscaledTime + 3f;
            try { sock.Dispose(); } catch { }
        }

        async Task SendLoop(ClientWebSocket sock, CancellationToken token)
        {
            try
            {
                while (sock.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    if (outbox.TryDequeue(out var s))
                        await sock.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(s)), WebSocketMessageType.Text, true, token);
                    else await Task.Delay(5, token);
                }
            }
            catch { }
        }

        void Update()
        {
            if (role != null && !Connected && !connecting && Time.unscaledTime > retryAt) Connect();
            while (inbox.TryDequeue(out var msg)) Receive(msg);
            if (role == "host") HostTick();
            else if (Watching) WatchTick();
        }

        void Receive(string msg)
        {
            Head h;
            try { h = JsonUtility.FromJson<Head>(msg); } catch { return; }
            if (h == null) return;
            switch (h.t)
            {
                case "room":
                    var r = JsonUtility.FromJson<Room>(msg);
                    Hosts = r.hosts; Watchers = r.watchers;
                    break;
                case "need": needSync = true; break;
                case "s": if (Watching) { snap = JsonUtility.FromJson<Snap>(msg); snapAt = Time.unscaledTime; } break;
                case "l": if (Watching) ApplyLog(JsonUtility.FromJson<LogMsg>(msg)); break;
                case "c": if (Watching) ApplyChat(JsonUtility.FromJson<ChatMsg>(msg)); break;
            }
        }

        // ---------- ссылки на сцену (после перезапуска дела — заново) ----------
        GameDirector game; PlayerController player; Interrogation talk; HUD hud; BankFriend friend; Contacts contacts;
        Suspect suspect; DeskLamp lamp; ChocolateBar bar; GlassWater glass;
        Pickup[] items;
        Transform[] bones;

        bool Refs()
        {
            if (game != null && player != null && talk != null && hud != null) return true;
            game = GameDirector.I;
            player = FindAnyObjectByType<PlayerController>();
            talk = FindAnyObjectByType<Interrogation>();
            hud = FindAnyObjectByType<HUD>();
            friend = FindAnyObjectByType<BankFriend>();
            contacts = FindAnyObjectByType<Contacts>();
            suspect = FindAnyObjectByType<Suspect>();
            lamp = FindAnyObjectByType<DeskLamp>();
            bar = FindAnyObjectByType<ChocolateBar>(FindObjectsInactive.Include);
            glass = FindAnyObjectByType<GlassWater>(FindObjectsInactive.Include);
            var list = new List<Pickup>(FindObjectsByType<Pickup>(FindObjectsInactive.Include));
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            items = list.ToArray();
            bones = suspect != null ? new[] { suspect.spine, suspect.torso, suspect.head, suspect.eyeL, suspect.eyeR, suspect.browL, suspect.browR, suspect.mouth } : new Transform[0];
            if (Watching && game != null) EnterWatchMode();
            return game != null && player != null && talk != null && hud != null;
        }

        List<BankFriend.Msg> ChatList(int i)
        {
            if (i == 0) return friend != null ? friend.chat : null;
            if (contacts == null) return null;
            return i == 1 ? contacts.sergei?.chat : contacts.marina?.chat;
        }

        // ---------- игрок ----------
        bool needSync;
        int sentLog;
        readonly int[] sentChat = new int[3];
        float nextSnap;

        void HostTick()
        {
            if (!Connected || Watchers == 0 || !Refs()) return;
            if (needSync || sentLog > talk.log.Count) { needSync = false; sentLog = 0; sentChat[0] = sentChat[1] = sentChat[2] = 0; nextSnap = 0f; }
            while (sentLog < talk.log.Count)
            {
                var l = talk.log[sentLog];
                outbox.Enqueue(JsonUtility.ToJson(new LogMsg { i = sentLog, who = l.who, text = l.text, player = l.player }));
                sentLog++;
            }
            for (int c = 0; c < 3; c++)
            {
                var list = ChatList(c);
                if (list == null) continue;
                if (sentChat[c] > list.Count) sentChat[c] = 0;
                while (sentChat[c] < list.Count)
                {
                    var m = list[sentChat[c]];
                    outbox.Enqueue(JsonUtility.ToJson(new ChatMsg { ch = c, i = sentChat[c], mine = m.mine, text = m.text, time = m.time, photo = m.photo != null ? m.photo.name : "", show = m.showText }));
                    sentChat[c]++;
                }
            }
            if (Time.unscaledTime < nextSnap) return;
            nextSnap = Time.unscaledTime + 1f / SnapRate;
            outbox.Enqueue(JsonUtility.ToJson(BuildSnap()));
        }

        static void Put(List<float> f, Vector3 v) { f.Add(v.x); f.Add(v.y); f.Add(v.z); }
        static void Put(List<float> f, Quaternion q) { f.Add(q.x); f.Add(q.y); f.Add(q.z); f.Add(q.w); }

        Snap BuildSnap()
        {
            var s = new Snap { st = (int)game.state, el = game.elapsed, lim = game.unlimited ? 0 : Mathf.RoundToInt(game.realDuration / 60f), wait = talk.waiting, et = game.endTitle, ex = game.endText };
            var f = new List<float>();
            Put(f, player.transform.position); Put(f, player.transform.rotation);
            Put(f, player.cam.transform.localPosition); Put(f, player.cam.transform.localRotation);
            s.pl = f.ToArray();
            f.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                if (it == null) { for (int k = 0; k < 12; k++) f.Add(0f); continue; }
                Put(f, it.transform.position); Put(f, it.transform.rotation);
                f.Add(it.gameObject.activeInHierarchy ? 1f : 0f);
                Put(f, it.openPivot != null ? it.openPivot.localRotation : Quaternion.identity);
                if (player.held == it) s.held = i;
            }
            s.it = f.ToArray();
            f.Clear();
            foreach (var b in bones)
            {
                if (b == null) { for (int k = 0; k < 10; k++) f.Add(0f); continue; }
                Put(f, b.localPosition); Put(f, b.localRotation); Put(f, b.localScale);
            }
            s.sb = f.ToArray();
            if (lamp != null && lamp.head != null) { f.Clear(); Put(f, lamp.head.localRotation); s.dl = f.ToArray(); }
            hud.RemoteState(out s.ty, out s.tx, out s.po, out s.pc, out s.ps, out s.pt, out s.ptx, out s.ex2, out s.rd);
            s.lc = talk.log.Count;
            s.cc = new int[3]; s.ctp = new bool[3];
            for (int c = 0; c < 3; c++) { var l = ChatList(c); s.cc[c] = l != null ? l.Count : 0; }
            s.ctp[0] = friend != null && friend.typing;
            s.ctp[1] = contacts != null && contacts.sergei != null && contacts.sergei.typing;
            s.ctp[2] = contacts != null && contacts.marina != null && contacts.marina.typing;
            if (bar != null) { s.cu = bar.unwrapped; s.cl = bar.Left; }
            if (glass != null) s.gf = glass.fill;
            return s;
        }

        // ---------- наблюдатель ----------
        Snap snap;
        float snapAt, needAt;
        Transform ghost;
        float freeYaw, freePitch;

        // Своё у наблюдателя не работает: тело, часы, ИИ, предметы — всё приходит от игрока.
        void EnterWatchMode()
        {
            if (game == null) return;
            game.enabled = false;
            if (player != null)
            {
                player.enabled = false;
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }
            if (suspect != null) suspect.enabled = false;
            if (lamp != null) lamp.enabled = false;
            if (bar != null) bar.enabled = false;
            if (friend != null) friend.enabled = false;
            if (contacts != null) contacts.enabled = false;
            if (items != null) foreach (var it in items) if (it != null) it.enabled = false;
        }

        void ApplyLog(LogMsg m)
        {
            if (!Refs() || m == null) return;
            while (talk.log.Count <= m.i) talk.log.Add(new Interrogation.Line { who = "", text = "" });
            var l = talk.log[m.i];
            if (l.text == m.text && l.who == m.who) return;
            l.who = m.who; l.text = m.text; l.player = m.player;
            l.shown = m.player ? m.text.Length : 0f;
            l.born = Time.time; l.doneAt = -1f;
        }

        void ApplyChat(ChatMsg m)
        {
            if (!Refs() || m == null) return;
            var list = ChatList(m.ch);
            if (list == null) return;
            var msg = new BankFriend.Msg { mine = m.mine, text = m.text, time = m.time, showText = m.show,
                photo = string.IsNullOrEmpty(m.photo) ? null : Resources.Load<Texture2D>("Photos/" + m.photo) };
            if (m.i < list.Count) list[m.i] = msg;
            else { while (list.Count < m.i) list.Add(new BankFriend.Msg { text = "" }); list.Add(msg); }
        }

        static Vector3 V3(float[] a, int i) => new Vector3(a[i], a[i + 1], a[i + 2]);
        static Quaternion Q4(float[] a, int i) { var q = new Quaternion(a[i], a[i + 1], a[i + 2], a[i + 3]); return q.w == 0f && q.x == 0f && q.y == 0f && q.z == 0f ? Quaternion.identity : q; }

        void WatchTick()
        {
            if (!Refs()) return;
            if (Input.GetKeyDown(KeyCode.V) && !Binds.Paused) { FreeCam = !FreeCam; if (FreeCam) { var e = player.cam.transform.eulerAngles; freeYaw = e.y; freePitch = e.x > 180f ? e.x - 360f : e.x; } }
            Cursor.lockState = Binds.Paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = Binds.Paused;
            if (snap == null) return;
            var s = snap;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);

            game.state = (GameState)s.st;
            game.elapsed = s.el;
            game.ApplyLimit(s.lim);
            game.endTitle = s.et; game.endText = s.ex;
            if (game.Over && game.endTime <= 0f) game.endTime = Time.time;
            talk.waiting = s.wait;

            // Игрок и камера.
            if (s.pl != null && s.pl.Length >= 14)
            {
                var pos = V3(s.pl, 0); var rot = Q4(s.pl, 3);
                if (!FreeCam)
                {
                    player.transform.position = Vector3.Lerp(player.transform.position, pos, k);
                    player.transform.rotation = Quaternion.Slerp(player.transform.rotation, rot, k);
                    player.cam.transform.localPosition = Vector3.Lerp(player.cam.transform.localPosition, V3(s.pl, 7), k);
                    player.cam.transform.localRotation = Quaternion.Slerp(player.cam.transform.localRotation, Q4(s.pl, 10), k);
                }
                else FreeLook();
                Ghost(pos, rot, FreeCam);
            }

            // Предметы.
            if (s.it != null && items != null)
                for (int i = 0; i < items.Length && i * 12 + 11 < s.it.Length; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    int o = i * 12;
                    bool on = s.it[o + 7] > 0.5f;
                    if (it.gameObject.activeSelf != on) it.gameObject.SetActive(on);
                    var p = V3(s.it, o);
                    var t = it.transform;
                    bool jump = (t.position - p).sqrMagnitude > 1f;
                    t.position = jump ? p : Vector3.Lerp(t.position, p, k);
                    t.rotation = jump ? Q4(s.it, o + 3) : Quaternion.Slerp(t.rotation, Q4(s.it, o + 3), k);
                    if (it.openPivot != null) it.openPivot.localRotation = Quaternion.Slerp(it.openPivot.localRotation, Q4(s.it, o + 8), k);
                }
            player.held = s.held >= 0 && items != null && s.held < items.Length ? items[s.held] : null;

            // Белых.
            if (s.sb != null)
                for (int i = 0; i < bones.Length && i * 10 + 9 < s.sb.Length; i++)
                {
                    var b = bones[i];
                    if (b == null) continue;
                    int o = i * 10;
                    b.localPosition = Vector3.Lerp(b.localPosition, V3(s.sb, o), k);
                    b.localRotation = Quaternion.Slerp(b.localRotation, Q4(s.sb, o + 3), k);
                    b.localScale = Vector3.Lerp(b.localScale, V3(s.sb, o + 7), k);
                }
            if (lamp != null && lamp.head != null && s.dl != null && s.dl.Length == 4)
                lamp.head.localRotation = Quaternion.Slerp(lamp.head.localRotation, Q4(s.dl, 0), k);

            // Шоколадка и стакан.
            if (bar != null)
            {
                bar.unwrapped = s.cu;
                if (bar.wrapper != null && bar.wrapper.gameObject.activeSelf == s.cu) bar.wrapper.gameObject.SetActive(!s.cu);
                if (bar.foilRips != null) foreach (var f in bar.foilRips) if (f != null && f.gameObject.activeSelf == s.cu) f.gameObject.SetActive(!s.cu);
                if (bar.full != null) bar.full.SetActive(s.cl >= 2);
                if (bar.bitten != null) bar.bitten.SetActive(s.cl == 1);
            }
            if (glass != null) glass.fill = s.gf;

            // Экран: строка ввода, телефон, бумаги с доски.
            hud.ApplyRemote(s.ty, s.tx, s.po, s.pc, s.ps, s.pt, s.ptx, s.ex2, s.rd);
            if (friend != null && s.ctp != null && s.ctp.Length == 3)
            {
                friend.typing = s.ctp[0];
                if (contacts != null && contacts.sergei != null) contacts.sergei.typing = s.ctp[1];
                if (contacts != null && contacts.marina != null) contacts.marina.typing = s.ctp[2];
            }

            // Разошлись с игроком (он перезапустил дело или мы только подключились) — просим всё заново.
            bool mismatch = s.lc != talk.log.Count;
            if (s.cc != null) for (int c = 0; c < 3 && c < s.cc.Length; c++) { var l = ChatList(c); if (l != null && l.Count != s.cc[c]) mismatch = true; }
            if (mismatch && Time.unscaledTime > needAt)
            {
                if (s.lc < talk.log.Count) talk.log.Clear();
                for (int c = 0; c < 3; c++) { var l = ChatList(c); if (l != null && s.cc != null && c < s.cc.Length && s.cc[c] < l.Count) l.Clear(); }
                outbox.Enqueue("{\"t\":\"need\"}");
                needAt = Time.unscaledTime + 2f;
            }
        }

        // Свободная камера: мышь — смотреть, WASD — летать, пробел / C — вверх / вниз.
        void FreeLook()
        {
            var cam = player.cam.transform;
            if (!Binds.Paused)
            {
                freeYaw += Input.GetAxisRaw("Mouse X") * 2f;
                freePitch = Mathf.Clamp(freePitch - Input.GetAxisRaw("Mouse Y") * 2f, -85f, 85f);
                var rot = Quaternion.Euler(freePitch, freeYaw, 0f);
                var move = Vector3.zero;
                if (Binds.Held(Act.Forward)) move += rot * Vector3.forward;
                if (Binds.Held(Act.Back)) move -= rot * Vector3.forward;
                if (Binds.Held(Act.Right)) move += rot * Vector3.right;
                if (Binds.Held(Act.Left)) move -= rot * Vector3.right;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.C)) move -= Vector3.up;
                var p = cam.position + move * 2.2f * Time.unscaledDeltaTime;
                p.x = Mathf.Clamp(p.x, -3.8f, 3.8f); p.y = Mathf.Clamp(p.y, 0.3f, 2.55f); p.z = Mathf.Clamp(p.z, -3.8f, 3.8f);
                cam.position = p;
                cam.rotation = rot;
            }
        }

        // Фигура игрока — видна только в свободной камере.
        void Ghost(Vector3 pos, Quaternion rot, bool show)
        {
            if (ghost == null)
            {
                ghost = new GameObject("WatchedPlayer").transform;
                var mat = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.15f, 0.14f) };
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(ghost, false);
                body.transform.localPosition = new Vector3(0, 0.85f, 0);
                body.transform.localScale = new Vector3(0.45f, 0.82f, 0.3f);
                body.GetComponent<Renderer>().sharedMaterial = mat;
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(ghost, false);
                head.transform.localPosition = new Vector3(0, 1.62f, 0);
                head.transform.localScale = Vector3.one * 0.24f;
                head.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.62f, 0.5f, 0.42f) };
                var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(hat.GetComponent<Collider>());
                hat.transform.SetParent(ghost, false);
                hat.transform.localPosition = new Vector3(0, 1.76f, 0);
                hat.transform.localScale = new Vector3(0.34f, 0.02f, 0.34f);
                hat.GetComponent<Renderer>().sharedMaterial = mat;
            }
            if (ghost.gameObject.activeSelf != show) ghost.gameObject.SetActive(show);
            if (!show) return;
            ghost.position = Vector3.Lerp(ghost.position, pos, 0.3f);
            ghost.rotation = Quaternion.Slerp(ghost.rotation, rot, 0.3f);
        }

        public bool Stale => Watching && Connected && snap != null && Time.unscaledTime - snapAt > 4f;
    }
}
