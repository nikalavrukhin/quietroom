namespace QuietRoom
{
    // Предмет, который при осмотре листается: страницы папки, экраны телефона, оборот фото.
    public interface IInspectPages
    {
        void Prev();
        void Next();
        void SetInspecting(bool on);
        string Help { get; }
    }
}
