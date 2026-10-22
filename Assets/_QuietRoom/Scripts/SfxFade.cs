using UnityEngine;

namespace QuietRoom
{
    // Плавно глушит обрезанный звук, чтобы не было щелчка в конце.
    public class SfxFade : MonoBehaviour
    {
        public float fadeAt;
        AudioSource src;

        void Update()
        {
            if (Time.time < fadeAt) return;
            if (src == null) src = GetComponent<AudioSource>();
            if (src != null) src.volume = Mathf.MoveTowards(src.volume, 0f, Time.deltaTime * 6f);
        }
    }
}
