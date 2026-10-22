using UnityEngine;

namespace QuietRoom
{
    // Место на столе прямо перед подозреваемым: сюда кладут улики, чтобы он их прочитал.
    public class PresentSpot : MonoBehaviour, IInteractable
    {
        public Pickup current;

        static PresentSpot instance;
        public static PresentSpot I => instance != null ? instance : (instance = FindAnyObjectByType<PresentSpot>());

        public string Hint(PlayerController player)
        {
            if (player.held == null) return "";
            return Binds.Label(Act.Use) + " — положить перед ним: " + player.held.title;
        }

        public void Interact(PlayerController player)
        {
            if (player.held != null) Place(player);
        }

        public void Place(PlayerController player)
        {
            var item = player.held;
            if (item == null) return;
            var suspect = FindAnyObjectByType<Suspect>();
            if (suspect != null && suspect.dead) return;

            // Что лежало раньше — возвращается на верстак.
            if (current != null && current != item) current.ReturnHome();
            player.held = null;
            item.PlaceAt(transform);
            current = item;
            Sfx.Play(item.PutSound, transform.position, 0.6f);

            bool again = item.used;
            item.used = !item.Consumable || item.used;
            if (suspect != null) suspect.Present(item, again);
        }

        public static void Clear(Pickup item)
        {
            if (I != null && I.current == item) I.current = null;
        }
    }
}
