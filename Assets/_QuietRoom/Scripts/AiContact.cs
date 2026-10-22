using System.Collections.Generic;

namespace QuietRoom
{
    // Контакт в телефоне детектива, за которого отвечает нейросеть (Сергей, Марина).
    public class AiContact
    {
        public string id, name, sub;
        public string prompt;
        public readonly List<BankFriend.Msg> chat = new List<BankFriend.Msg>();
        public readonly List<LlmClient.Msg> history = new List<LlmClient.Msg>();
        public bool typing;
        public int unread;
        public float deliverAt = -1f;      // когда придёт обещанное (фото, письмо)
        public bool delivered;
        public bool nudged;                // сам написал «если что — помогу» (один раз за партию)
    }
}
