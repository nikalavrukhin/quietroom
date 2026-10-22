using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace QuietRoom
{
    // Разговор с подозреваемым через ИИ: копит события, отправляет реплики, раздаёт ответы в HUD.
    public class Interrogation : MonoBehaviour
    {
        public class Line
        {
            public string who;      // "" — системная строка
            public string text;
            public float shown;     // сколько символов уже напечатано
            public bool player;
            public float born = Time.time;   // для анимации появления
            public float doneAt = -1f;       // когда реплика допечаталась (для угасания субтитров)
        }

        [System.Serializable] class Reply { public string say; public string emotion; public string need; [System.NonSerialized] public bool silent; }

        public readonly List<Line> log = new List<Line>();
        public bool waiting;
        // Что Белых видел своими глазами. Отсюда порядок его страхов: сначала Ника, потом юрист.
        public bool sawThreat, sawNikaSafe, sawLawyer;
        public float typeSpeed = 38f;

        readonly List<LlmClient.Msg> history = new List<LlmClient.Msg>();
        readonly List<string> pending = new List<string>();
        bool reactPending;
        const int MaxHistory = 120;   // хватает на всю партию — Белых не забывает начало разговора

        Suspect suspect;
        Suspect S => suspect != null ? suspect : (suspect = FindAnyObjectByType<Suspect>());

        public Line Current => log.Count > 0 ? log[log.Count - 1] : null;
        public bool Typing => Current != null && !Current.player && Current.shown < Current.text.Length;

        void Update()
        {
            foreach (var l in log)
            {
                if (l.shown >= l.text.Length) { if (l.doneAt < 0f) l.doneAt = Time.time; continue; }
                l.shown = Mathf.Min(l.text.Length, l.shown + typeSpeed * Time.deltaTime);
            }
            if (S != null) S.talking = Typing && Current.who == CaseFile.SuspectName;
        }

        public void PlayerSays(string text)
        {
            text = text.Trim();
            if (text.Length == 0 || S == null || S.dead) return;
            log.Add(new Line { who = "Ты", text = text, shown = text.Length, player = true });
            Send("Детектив говорит: «" + text + "»");
        }

        // Действие без слов. reactNow — подозреваемый отвечает сразу, иначе событие уйдёт со следующей репликой.
        public void PlayerAction(string description, bool reactNow)
        {
            if (S == null || S.dead) return;
            // В чат не пишем: игрок и так видит, что сделал. Событие уходит только ИИ.
            pending.Add(description);
            if (reactNow)
            {
                if (waiting) reactPending = true;
                else Send(null);
            }
        }

        void Send(string speech)
        {
            if (waiting) { if (speech != null) pending.Add(speech); reactPending = true; return; }
            var game = GameDirector.I;
            var sb = new StringBuilder();
            sb.Append("[СИСТЕМА] Время ").Append(game.ClockText).Append(". ");
            if (!game.unlimited) sb.Append("До приезда полиции ").Append(game.MinutesLeft).Append(" мин. ");
            sb.Append(S.BodyForAI());
            sb.Append(Stage());
            if (pending.Count > 0)
            {
                sb.Append("\nЧто произошло: ");
                foreach (var p in pending) sb.Append(p).Append(' ');
                pending.Clear();
            }
            sb.Append('\n').Append(speech ?? "Детектив молчит и смотрит на тебя.");

            if (history.Count == 0) history.Add(new LlmClient.Msg { role = "system", content = CaseFile.SystemPrompt });
            history.Add(new LlmClient.Msg { role = "user", content = sb.ToString() });
            while (history.Count > MaxHistory) history.RemoveAt(1);

            waiting = true;
            game.clockPaused = true;
            StartCoroutine(LlmClient.Chat(history, OnReply, OnError));
        }

        void OnReply(string raw)
        {
            Finish();
            if (S == null || S.dead) return;
            var reply = Parse(raw);
            if (reply.silent)
            {
                retried = false;
                history.Add(new LlmClient.Msg { role = "assistant", content = "{\"say\": \"\", \"emotion\": \"" + (reply.emotion ?? "nervous") + "\"}" });
                S.emotion = ParseEmotion(reply.emotion, S.emotion);
                if (reactPending && pending.Count > 0) { reactPending = false; Send(null); }
                reactPending = false;
                return;
            }
            if (IsJunk(reply.say))
            {
                // Модель выдала мусор вроде {" ": "say: "} — молча переспрашиваем без строгого JSON.
                Debug.LogWarning("[QuietRoom] Мусорный ответ ИИ, переспрашиваю: " + raw);
                if (!retried)
                {
                    retried = true;
                    LlmClient.plainOnce = true;
                    waiting = true;
                    GameDirector.I.clockPaused = true;
                    StartCoroutine(LlmClient.Chat(history, OnReply, OnError));
                    return;
                }
                reply = new Reply { say = "…", emotion = reply.emotion };   // второй раз — просто молчит
            }
            retried = false;
            // В историю кладём уже чистый JSON, чтобы модель не училась на своих кривых ответах.
            history.Add(new LlmClient.Msg { role = "assistant", content = JsonUtility.ToJson(reply) });

            S.emotion = ParseEmotion(reply.emotion, S.emotion);
            if (!string.IsNullOrEmpty(reply.say))
            {
                log.Add(new Line { who = CaseFile.SuspectName, text = reply.say.Trim(), shown = 0f });
                string need = Need(reply);
                if (need == "nika" && !sawThreat) need = null;       // Сергей — только когда Белых видел угрозу дочери
                if (need == "lawyer" && !sawNikaSafe) need = null;   // Марина — только когда с Никой уже ясно
                if (need != null) FindAnyObjectByType<Contacts>()?.Nudge(need);
            }

            if (reactPending && pending.Count > 0) { reactPending = false; Send(null); }
            reactPending = false;
        }

        void OnError(string why)
        {
            Finish();
            // Последнюю реплику убираем из истории, чтобы при повторе не было двух вопросов подряд.
            if (history.Count > 1 && history[history.Count - 1].role == "user") history.RemoveAt(history.Count - 1);
            log.Add(new Line { who = "", text = "(связь с ИИ оборвалась: " + why + ". Скажи ещё раз.)", shown = 0f });
            Debug.LogWarning("[QuietRoom] LLM: " + why);
        }

        void Finish()
        {
            waiting = false;
            if (GameDirector.I != null) GameDirector.I.clockPaused = false;
        }

        static Reply Parse(string raw)
        {
            string s = raw.Trim();
            int a = s.IndexOf('{'), b = s.LastIndexOf('}');
            if (a >= 0 && b > a)
            {
                try
                {
                    var r = JsonUtility.FromJson<Reply>(s.Substring(a, b - a + 1));
                    if (r != null && !string.IsNullOrEmpty(r.say)) { r.say = Clean(r.say); return r; }
                    // Пустой say — Белых решил промолчать. Это не сбой.
                    if (r != null && Regex.IsMatch(s, "\"say\"\\s*:\\s*\"\\s*\"")) return new Reply { say = "", emotion = r.emotion, silent = true };
                }
                catch { }
            }
            // Битый JSON — пробуем вытащить хотя бы поле say.
            // Ключ бывает испорчен («say:», «Say»), поэтому берём любое поле, начинающееся с say.
            var m = Regex.Match(s, "\"say[^\"]*\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase);
            if (m.Success) return new Reply { say = Clean(Regex.Unescape(m.Groups[1].Value)) };
            // Ответ оборвался посреди строки: берём всё после "say":" до конца.
            var cut = Regex.Match(s, "\"say[^\"]*\"\\s*:\\s*\"(.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (cut.Success)
            {
                string tail = cut.Groups[1].Value.TrimEnd('}', ' ', '\n', '"');
                try { tail = Regex.Unescape(tail); } catch { }
                return new Reply { say = Clean(tail) + "…" };
            }
            // Совсем не JSON — показываем текст, но без служебных блоков.
            return new Reply { say = Clean(s.Trim('`', ' ', '\n')), emotion = null };
        }

        // Вырезает то, что игрок видеть не должен: эхо блока [СИСТЕМА] и markdown.
        static string Clean(string text)
        {
            text = Regex.Replace(text, @"\[СИСТЕМА\][^\n]*(\n|$)", "");
            // Ремарки в звёздочках или скобках («*щурится*», «(морщится)») игрок видеть не должен.
            text = Regex.Replace(text, @"\*[^*]*\*", "");
            text = Regex.Replace(text, @"\([^)]*\)", "");
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = Regex.Replace(text, @"^\s*[,.—–-]\s*", "");
            text = Regex.Replace(text, @"^```\w*|```$", "");
            text = Regex.Replace(text, @"\n{2,}", "\n");
            return text.Trim();
        }

        bool retried;

        // Где Белых сейчас в своих страхах — по тому, что ему реально показали.
        string Stage()
        {
            if (sawThreat && !sawNikaSafe)
                return " Ты видел угрозу Кузнецова дочери. Доказательства, что Ника в безопасности, ты ещё НЕ видел: слова и обещания детектива не в счёт. Сейчас ты думаешь только о ней — про юриста, тюрьму и себя пока не думаешь и не говоришь.";
            if (sawNikaSafe && !sawLawyer)
                return " Ты своими глазами видел фото: Ника цела, она с человеком детектива. За неё ты больше не боишься — теперь тебя пугает, что будет с тобой, если ячейку откроют.";
            return "";
        }

        // Чего Белых сейчас просит: пометка модели, а если она её забыла — по словам реплики.
        static string Need(Reply r)
        {
            string n = (r.need ?? "").Trim().ToLowerInvariant();
            if (n == "nika" || n == "lawyer") return n;
            string s = r.say ?? "";
            if (Regex.IsMatch(s, @"(?i)\b(юрист|адвокат)")) return "lawyer";
            if (Regex.IsMatch(s, @"(?i)\bник|\bдоч") && Regex.IsMatch(s, @"(?i)убедит|докаж|доказательств|покажите|нужно знать|надо знать|что с ней|жива ли|в порядке ли")) return "nika";
            return null;
        }

        // Мусор: пусто, одни знаки, или в тексте торчат куски JSON.
        static bool IsJunk(string say)
        {
            if (string.IsNullOrWhiteSpace(say)) return true;
            string t = say.Trim();
            if (Regex.IsMatch(t, @"(?i)\bsay\b|emotion|[{}]|^""|""\s*:")) return true;
            return !Regex.IsMatch(t, @"[А-Яа-яЁёA-Za-z]{2,}");
        }

        static Emotion ParseEmotion(string e, Emotion fallback)
        {
            switch ((e ?? "").Trim().ToLowerInvariant())
            {
                case "calm": return Emotion.Calm;
                case "nervous": return Emotion.Nervous;
                case "scared": return Emotion.Scared;
                case "angry": return Emotion.Angry;
                case "broken": return Emotion.Broken;
                case "pain": return Emotion.Pain;
                case "weak": return Emotion.Weak;
                case "smug": return Emotion.Smug;
            }
            return fallback;
        }
    }
}
