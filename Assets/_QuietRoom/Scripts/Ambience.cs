using UnityEngine;

namespace QuietRoom
{
    // Звуковой фон подвала: редкие капли в углу у котла.
    [RequireComponent(typeof(AudioSource))]
    public class Ambience : MonoBehaviour
    {
        void Start()
        {
            // Низкий гул, гудение лампы и дождь Николай попросил убрать — остались только капли у котла.
            Attach("Boiler", "drips_loop", 0.12f, 0.5f, 6f);
        }

        static void Attach(string objectName, string clip, float volume, float minDist, float maxDist)
        {
            var host = GameObject.Find(objectName);
            if (host != null) Sfx.Loop(host, clip, volume, 1f, minDist, maxDist);
        }
    }
}
