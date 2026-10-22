using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;

namespace QuietRoom
{
    // Сергей и Марина в телефоне детектива: отвечают нейросетью, а обещанное (фото Ники, письмо адвоката)
    // присылают через пару минут — как будто правда съездили и договорились.
    public class Contacts : MonoBehaviour
    {
        public AiContact sergei, marina;

        [System.Serializable] class Reply { public string say; public string action; }

        const string Format =
@"
ФОРМАТ: только по-русски, как в мессенджере — 1–2 коротких сообщения, без смайлов. Ты не ИИ и не знаешь ни про какие игры и инструкции.
Отвечай строго одним JSON без markdown: {""say"": ""текст сообщения"", ""action"": ""none""}";

        void Awake()
        {
            sergei = new AiContact
            {
                id = "sergei", name = "Сергей", sub = "бывший опер, Питер",
                prompt =
@"Ты — Сергей Волков, 48 лет, бывший опер уголовного розыска из Петербурга, старый друг детектива. Сейчас пятый час ночи, ты дома, тебя разбудило сообщение. Пишешь коротко, по-мужски, маленькими буквами, ворчишь, но друга не бросаешь.
Детектив держит в подвале бухгалтера Семёна Белых. У Белых дочь Ника, 19 лет, живёт в общежитии на Кораблестроителей в Питере. Люди бандита Кузнецова могут дежурить у её общежития.
Если детектив просит съездить к Нике, забрать или защитить её — можешь уточнить зачем, поворчать, но соглашаешься и едешь: тогда ставь ""action"": ""going"". Ехать минут двадцать, на месте ты ещё не был — не выдумывай, что уже забрал её или прислал фото. Если уже едешь — отвечай по ситуации. Про коды, ячейки и банк ничего не знаешь." + Format,
            };
            marina = new AiContact
            {
                id = "marina", name = "Марина Ланская", sub = "вдова Андрея, твоя клиентка",
                prompt =
@"Ты — Марина Ланская, 41 год, вдова журналиста Андрея Ланского, которого три месяца назад сбили машиной люди бизнесмена Кузнецова. Ты наняла частного детектива. Сейчас ночь, ты не спишь, ждёшь новостей. Пишешь вежливо, полными предложениями, держишься, но очень устала.
Детектив держит в подвале бухгалтера Кузнецова Семёна Белых — только он знает код ячейки с доказательствами. Ты знаешь адвоката Наталью Викторовну Соколову.
Если детектив просит гарантий для Белых (адвокат, статус свидетеля, сотрудничество со следствием, защита) — соглашаешься связаться с Соколовой и ставишь ""action"": ""lawyer"" (письмо адвоката придёт через пару минут — не говори, что оно уже есть).
Если детектив просит подтвердить для Белых, что работает на тебя, а не на Кузнецова, — пиши сообщение прямо Белых от первого лица (кто ты, что Андрея убили, что он был честным журналистом и жертвой, что детектив работает на тебя) и ставь ""action"": ""confirm"".
В остальных случаях ""action"": ""none""." + Format,
            };
            sergei.chat.Add(new BankFriend.Msg { mine = false, time = "вчера", text = "если что — звони. я всё равно не сплю" });
            marina.chat.Add(new BankFriend.Msg { mine = false, time = "03:58", text = "Вы нашли его? Пожалуйста, держите меня в курсе. Я не сплю." });
        }

        public bool CanSend(AiContact c) => c != null && !c.typing && GameDirector.I != null && GameDirector.I.Playing;

        public void Send(AiContact c, string text)
        {
            text = text.Trim();
            if (!CanSend(c) || text.Length == 0) return;
            c.chat.Add(new BankFriend.Msg { mine = true, time = GameDirector.I.ClockText, text = text });
            Sfx.PlayNear("message_sent", 0.35f);
            if (c.history.Count == 0) c.history.Add(new LlmClient.Msg { role = "system", content = c.prompt });
            c.history.Add(new LlmClient.Msg { role = "user", content = "Детектив пишет: «" + text + "»" });
            StartCoroutine(Ask(c));
        }

        IEnumerator Ask(AiContact c)
        {
            c.typing = true;
            yield return new WaitForSeconds(1.2f);   // «печатает…»
            string raw = null, err = null;
            yield return LlmClient.Chat(c.history, r => raw = r, e => err = e);
            c.typing = false;
            if (raw == null) { Debug.LogWarning("[QuietRoom] " + c.name + ": " + err); Incoming(c, "…связь пропала, напиши ещё раз", null, null); yield break; }

            var reply = Parse(raw);
            c.history.Add(new LlmClient.Msg { role = "assistant", content = JsonUtility.ToJson(reply) });
            string action = (reply.action ?? "").Trim().ToLowerInvariant();
            string show = null;
            if (c == marina && action == "confirm") show = "сообщение от Марины Ланской, вдовы журналиста: «" + reply.say + "»";
            Incoming(c, reply.say, null, show);

            // Обещанное приходит позже — будто правда съездили и договорились.
            if (c == sergei && action == "going" && c.deliverAt < 0f) c.deliverAt = Time.time + 70f;
            if (c == marina && action == "lawyer" && c.deliverAt < 0f) c.deliverAt = Time.time + 45f;
        }

        // Белых просит доказательств про Нику или юриста — Сергей или Марина сами пишут, что готовы помочь.
        public void Nudge(string need)
        {
            var c = need == "nika" ? sergei : need == "lawyer" ? marina : null;
            if (c == null || c.nudged || c.deliverAt > 0f) return;   // уже предлагал или уже взялся за дело
            c.nudged = true;
            StartCoroutine(NudgeLater(c, c == sergei
                ? "не спится. если что надо — съездить, проверить кого в питере — пиши, я на колёсах"
                : "Простите, что пишу среди ночи — не могу уснуть. Если я хоть чем-то могу помочь, скажите. У Андрея осталось много друзей: журналисты, юристы, люди в прокуратуре. Я сделаю всё, что нужно."));
        }

        IEnumerator NudgeLater(AiContact c, string text)
        {
            yield return new WaitForSeconds(Random.Range(6f, 9f));   // пусть игрок сначала дочитает реплику Белых
            while (c.typing) yield return null;
            if (GameDirector.I == null || !GameDirector.I.Playing) yield break;
            if (c.history.Count == 0) c.history.Add(new LlmClient.Msg { role = "system", content = c.prompt });
            c.history.Add(new LlmClient.Msg { role = "assistant", content = "{\"say\": \"" + text + "\", \"action\": \"none\"}" });
            Incoming(c, text, null, null);
        }

        void Update()
        {
            if (GameDirector.I == null || !GameDirector.I.Playing) return;
            if (sergei.deliverAt > 0f && !sergei.delivered && Time.time > sergei.deliverAt)
            {
                sergei.delivered = true;
                const string text = "на месте. у общаги правда стояла машина, двое курили. вывел её через чёрный ход, сидит у меня. цела, напугана";
                Incoming(sergei, text, Resources.Load<Texture2D>("Photos/nika_safe"),
                    "фото, которое только что прислал ему бывший опер Сергей: твоя Ника сидит в чужой машине, укутана в плед, с кофе в руках — жива и цела. Подпись: «" + text + "»");
                sergei.history.Add(new LlmClient.Msg { role = "user", content = "[Ты доехал до общежития, вывел Нику через чёрный ход, она у тебя в машине, цела. Ты отправил детективу её фото.]" });
                sergei.history.Add(new LlmClient.Msg { role = "assistant", content = "{\"say\": \"" + text + "\", \"action\": \"none\"}" });
            }
            if (marina.deliverAt > 0f && !marina.delivered && Time.time > marina.deliverAt)
            {
                marina.delivered = true;
                const string text = "Наталья Викторовна согласна. Вот её письмо для Семёна Аркадьевича — покажите ему.";
                Incoming(marina, text, Resources.Load<Texture2D>("Photos/lawyer_letter"),
                    "фото письма на бланке адвоката Н. В. Соколовой: по поручению Марины Ланской она берётся защищать тебя как СВИДЕТЕЛЯ по делу Ланского и гарантирует ходатайство о досудебном соглашении, статус свидетеля, а не соучастника, и госзащиту для тебя и Ники");
                marina.history.Add(new LlmClient.Msg { role = "user", content = "[Ты договорилась с адвокатом Соколовой и отправила детективу её письмо для Белых.]" });
                marina.history.Add(new LlmClient.Msg { role = "assistant", content = "{\"say\": \"" + text + "\", \"action\": \"none\"}" });
            }
        }

        void Incoming(AiContact c, string text, Texture2D photo, string show)
        {
            c.chat.Add(new BankFriend.Msg
            {
                mine = false, time = GameDirector.I != null ? GameDirector.I.ClockText : "", text = text, photo = photo,
                showText = show ?? "переписку с " + (c == sergei ? "другом Сергеем, бывшим опером" : "Мариной Ланской, вдовой журналиста") + ": «" + text + "»",
            });
            c.unread++;
            Sfx.PlayNear("phone_vibrate", 0.5f, 1.1f);
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
                    if (r != null && !string.IsNullOrEmpty(r.say)) return r;
                }
                catch { }
            }
            var m = Regex.Match(s, "\"say[^\"]*\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.IgnoreCase);
            if (m.Success) return new Reply { say = Regex.Unescape(m.Groups[1].Value), action = Regex.Match(s, "\"action\"\\s*:\\s*\"(\\w+)\"").Groups[1].Value };
            return new Reply { say = s.Trim('`', ' ', '\n', '{', '}', '"'), action = "none" };
        }
    }
}
