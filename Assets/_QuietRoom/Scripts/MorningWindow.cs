using UnityEngine;

namespace QuietRoom
{
    // Окошко под потолком: ночное небо со звёздами; к шести утра звёзды гаснут, небо заливает рассвет.
    public class MorningWindow : MonoBehaviour
    {
        public Renderer glass;               // небо (картинка в свечении)
        public Renderer[] stars;             // слои звёзд (аддитивные) — мерцают вразнобой
        public Renderer dawn;                // тёплая заливка рассвета (аддитивная)
        public Light spill;
        public Color night = new Color(0.08f, 0.11f, 0.2f);
        public Color dawnColor = new Color(0.75f, 0.62f, 0.55f);

        void Update()
        {
            var game = GameDirector.I;
            float p = game == null ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, game.Progress));
            float t = Time.time;
            if (glass != null) glass.material.SetColor("_EmissionColor", Color.Lerp(Color.white, new Color(1.6f, 1.3f, 1.2f), p));
            if (stars != null)
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] == null) continue;
                    float tw = 0.7f + 0.3f * Mathf.Sin(t * (1.3f + i * 0.9f) + i * 2.1f);
                    stars[i].material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, (1f - p) * tw));
                }
            if (dawn != null) dawn.material.SetColor("_TintColor", new Color(dawnColor.r * 0.5f, dawnColor.g * 0.5f, dawnColor.b * 0.5f, p * 0.9f));
            var c = Color.Lerp(night, dawnColor, p);
            if (spill != null) { spill.color = c; spill.intensity = Mathf.Lerp(0.4f, 1.6f, p); }
        }
    }
}
