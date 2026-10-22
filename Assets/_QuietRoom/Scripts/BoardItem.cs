using UnityEngine;

namespace QuietRoom
{
    // Бумага на детективной доске: подойди, наведись и прочитай крупно.
    public class BoardItem : MonoBehaviour, IInteractable
    {
        public string title;
        [TextArea] public string body;
        public Texture2D photo;

        HUD hud;

        // Вырезка рисуется как газетная, остальное — как машинопись.
        public bool newspaper { get { return name == "ClipNews"; } }

        public string Hint(PlayerController player) { return Binds.Label(Act.Use) + " — прочитать"; }

        public void Interact(PlayerController player)
        {
            if (hud == null) hud = FindAnyObjectByType<HUD>();
            if (hud != null) hud.Read(this);
        }
    }
}
