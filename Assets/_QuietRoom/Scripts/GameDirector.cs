using UnityEngine;
using UnityEngine.SceneManagement;

namespace QuietRoom
{
    public enum GameState { Intro, Playing, Won, LostMorning, LostDead, LostFriend }

    // Часы до приезда полиции и исход уровня.
    public class GameDirector : MonoBehaviour
    {
        static GameDirector instance;
        public static GameDirector I
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<GameDirector>();
                return instance;
            }
        }

        [Tooltip("Сколько реальных секунд длится ночь")]
        public float realDuration = 1800f;   // 30 минут на партию
        [Tooltip("Без лимита: утро не наступает, полиция не едет, часы просто идут (1 игровая минута = 1 реальная)")]
        public bool unlimited = true;
        public int startMinutes = 4 * 60 + 30;
        public int endMinutes = 6 * 60;

        public GameState state = GameState.Intro;
        public float elapsed;
        [Tooltip("Часы стоят, пока ИИ сочиняет ответ")]
        public bool clockPaused;
        public string endTitle, endText;
        public float endTime;

        public bool Playing => state == GameState.Playing;
        public bool Over => state != GameState.Intro && state != GameState.Playing;
        public float Progress => unlimited ? 0f : Mathf.Clamp01(elapsed / realDuration);
        // С лимитом часы идут минута в минуту и показывают 06:00 ровно в конце партии.
        public float GameMinutes => unlimited ? startMinutes + elapsed / 60f : endMinutes - Mathf.Max(0f, realDuration - elapsed) / 60f;
        public int MinutesLeft => Mathf.CeilToInt(endMinutes - GameMinutes);

        public string ClockText
        {
            get
            {
                int m = Mathf.FloorToInt(GameMinutes) % (24 * 60);
                return (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
            }
        }

        // Лимит партии в минутах, 0 — без лимита. Ставится в админке (F2) и запоминается.
        public static int LimitMinutes
        {
            get => PlayerPrefs.GetInt("time_limit", 0);
            set { PlayerPrefs.SetInt("time_limit", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public void ApplyLimit(int minutes)
        {
            unlimited = minutes <= 0;
            if (!unlimited) realDuration = minutes * 60f;
        }

        void Awake()
        {
            instance = this;
            Application.runInBackground = true;
            ApplyLimit(LimitMinutes);
        }

        void Update()
        {
            if (!Playing || clockPaused) return;
            elapsed += Time.deltaTime;
            if (!unlimited && elapsed >= realDuration)
                Lose(GameState.LostMorning, "УТРО",
                    "06:00. Во дворе мигалки, в дверь стучат.\nБелых улыбается разбитыми губами: «Я же говорил — у тебя нет времени».\nКод так и остался в его голове.");
        }

        public void StartGame()
        {
            if (state != GameState.Intro) return;
            state = GameState.Playing;
        }

        public void Win(string text)
        {
            if (!Playing) return;
            state = GameState.Won;
            endTitle = "ДЕЛО ЗАКРЫТО";
            endText = text;
            endTime = Time.time;
        }

        public void Lose(GameState how, string title, string text)
        {
            if (!Playing) return;
            state = how;
            endTitle = title;
            endText = text;
            endTime = Time.time;
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
        }
    }
}
