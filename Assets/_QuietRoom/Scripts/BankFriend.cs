using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuietRoom
{
    // Димон из банка в телефоне игрока: проверяет код ячейки, три ошибки — и он сливается.
    public class BankFriend : MonoBehaviour
    {
        public class Msg
        {
            public bool mine;
            public string text;
            public string time;
            public Texture2D photo;      // присланное фото (Сергей, Марина)
            public string showText;      // что увидит Белых, если показать ему это сообщение
        }

        public readonly List<Msg> chat = new List<Msg>();
        public int wrong;
        public int maxWrong = 3;
        public bool typing;
        public bool blocked;
        [Tooltip("Сколько реальных секунд Димон проверяет код")]
        public float checkSeconds = 9f;
        public int unread;

        void Start()
        {
            chat.Add(new Msg { mine = true, time = "04:12", text = "Димон, выручай. Ячейка 212 у вас. Если скину код — пробьёшь по системе?" });
            chat.Add(new Msg { mine = false, time = "04:19", text = "ты вообще понимаешь что просишь. ладно. только быстро" });
            chat.Add(new Msg { mine = false, time = "04:19", text = "три попытки максимум. дальше безопасность увидит и мне конец" });
        }

        public bool CanSend => !typing && !blocked && GameDirector.I != null && GameDirector.I.Playing;

        public void SendCode(string raw)
        {
            if (!CanSend) return;
            var digits = new System.Text.StringBuilder();
            foreach (char c in raw) if (char.IsDigit(c)) digits.Append(c);
            if (digits.Length == 0) return;
            string code = digits.ToString();
            chat.Add(new Msg { mine = true, time = GameDirector.I.ClockText, text = code });
            Sfx.PlayNear("message_sent", 0.35f);
            StartCoroutine(Check(code));
        }

        IEnumerator Check(string code)
        {
            yield return new WaitForSeconds(1.5f);
            typing = true;
            yield return new WaitForSeconds(checkSeconds);
            typing = false;
            if (GameDirector.I == null || !GameDirector.I.Playing) yield break;

            if (code == CaseFile.SecretCode)
            {
                Say("...");
                yield return new WaitForSeconds(2f);
                Say("да. это тот код. 212 открылась в системе");
                yield return new WaitForSeconds(2.5f);
                Say("там флешка и тетрадь. я ничего не видел. удали переписку");
                yield return new WaitForSeconds(3f);
                GameDirector.I.Win("Код 1406 — день рождения Ники. Он сказал его сам.\nДимон подтвердил: ячейка №212 открыта. Флешка и тетрадь Кузнецова твои.\nДо рассвета осталось " + GameDirector.I.MinutesLeft + " мин. Пора уходить.");
                yield break;
            }

            wrong++;
            if (wrong == 1) Say("нет. не подходит. осталось две");
            else if (wrong == 2) Say("опять мимо. последняя, серьёзно. мне за это объяснительную писать");
            else
            {
                Say("всё, брат. я и так нарушаю регламент, безопасник уже спрашивал, чего я в 212-й копаюсь");
                yield return new WaitForSeconds(2.5f);
                Say("меня щас уволят. извини, но пока");
                blocked = true;
                yield return new WaitForSeconds(2.5f);
                Say("Димон заблокировал вас", true);
                yield return new WaitForSeconds(2f);
                GameDirector.I.Lose(GameState.LostFriend, "ДИМОН СЛИЛСЯ",
                    "Три неверных кода. Димон заблокировал тебя, а утром в банке узнают, кто копался в ячейке №212.\nБелых смотрит на тебя и впервые за ночь смеётся.");
            }
        }

        void Say(string text, bool system = false)
        {
            chat.Add(new Msg { mine = false, time = system ? "" : GameDirector.I.ClockText, text = text });
            unread++;
            Sfx.PlayNear("phone_vibrate", 0.5f, 1.1f);
        }
    }
}
