using UnityEngine;

namespace QuietRoom
{
    // Фото при осмотре можно перевернуть и прочитать надпись на обороте.
    public class PhotoFlip : MonoBehaviour, IInspectPages
    {
        public Transform pivot;
        public bool back;

        public string Help => "← → перевернуть · Q — убрать";

        public void Prev() { back = !back; }
        public void Next() { back = !back; }
        public void SetInspecting(bool on) { if (!on) back = false; }

        void Update()
        {
            if (pivot == null) return;
            pivot.localRotation = Quaternion.Slerp(pivot.localRotation, Quaternion.Euler(0f, 0f, back ? 180f : 0f), Time.deltaTime * 8f);
        }
    }
}
