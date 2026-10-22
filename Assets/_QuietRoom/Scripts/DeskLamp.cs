using UnityEngine;

namespace QuietRoom
{
    // Настольная лампа на столе: E — развернуть в лицо подозреваемому и обратно.
    public class DeskLamp : MonoBehaviour, IInteractable
    {
        public Transform head;
        public Light beam;
        public Vector3 tableEuler = new Vector3(70f, 0f, 0f);
        public Vector3 faceEuler = new Vector3(8f, 0f, 0f);
        public bool inFace;

        void Update()
        {
            if (head == null) return;
            var target = Quaternion.Euler(inFace ? faceEuler : tableEuler);
            head.localRotation = Quaternion.Slerp(head.localRotation, target, Time.deltaTime * 6f);
            if (beam != null)
                beam.intensity = Mathf.Lerp(beam.intensity, inFace ? 4.5f : 1.6f, Time.deltaTime * 6f);
        }

        public string Hint(PlayerController player)
        {
            return Binds.Label(Act.Use) + (inFace ? " — отвернуть лампу" : " — направить лампу ему в лицо");
        }

        public void Interact(PlayerController player)
        {
            inFace = !inFace;
            Sfx.Play("lamp_click", transform.position, 0.5f);
            var suspect = FindAnyObjectByType<Suspect>();
            if (suspect != null) suspect.lampInFace = inFace;
            var talk = FindAnyObjectByType<Interrogation>();
            if (talk != null)
                talk.PlayerAction(inFace
                    ? "Детектив развернул настольную лампу тебе прямо в глаза. Слепит."
                    : "Детектив отвернул лампу от твоего лица.", inFace);
        }
    }
}
