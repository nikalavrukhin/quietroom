using UnityEngine;

namespace QuietRoom
{
    // Настенные часы показывают игровое время и тикают.
    public class WallClock : MonoBehaviour
    {
        public Transform hourHand, minuteHand;
        public AudioSource tick;
        float nextTick;
        bool tock;

        void Update()
        {
            var game = GameDirector.I;
            if (game == null) return;
            float minutes = game.GameMinutes;
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0f, 0f, -(minutes % 60f) * 6f);
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0f, 0f, -((minutes / 60f) % 12f) * 30f);

            // Редкое тиканье: «тик» — пауза — «так», раз в две секунды.
            if (tick != null && game.Playing && Time.time >= nextTick)
            {
                nextTick = Time.time + 2f;
                tock = !tock;
                var clip = Sfx.Get(tock ? "clock_tock" : "clock_tick");
                if (clip != null) tick.PlayOneShot(clip, 0.35f);
            }
        }
    }
}
