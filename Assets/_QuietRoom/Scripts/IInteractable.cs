namespace QuietRoom
{
    // То, на что можно навестись прицелом и нажать E.
    public interface IInteractable
    {
        string Hint(PlayerController player);
        void Interact(PlayerController player);
    }
}
