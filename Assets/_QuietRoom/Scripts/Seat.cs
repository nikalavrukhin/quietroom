using UnityEngine;

namespace QuietRoom
{
    // Стул, на который можно сесть: E — сесть, шаг (WASD) — встать.
    // Смотрит вдоль своего +z; seatPoint — где окажутся ноги игрока (на полу, под сиденьем).
    public class Seat : MonoBehaviour, IInteractable
    {
        public Vector3 seatPoint = new Vector3(0f, 0f, -0.03f);
        public float eyeHeight = 1.15f;     // глаза сидящего над полом

        public string Hint(PlayerController player)
        {
            if (player.seat == this || player.seatBusy) return "";
            return Binds.Label(Act.Use) + " — сесть";
        }

        public void Interact(PlayerController player)
        {
            if (player.seat != this) player.Sit(this);
        }
    }
}
