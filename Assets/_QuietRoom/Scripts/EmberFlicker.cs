using UnityEngine;

namespace QuietRoom
{
    // Огонь в топке котла: свет и светящиеся щели дышат неровно, как живое пламя.
    public class EmberFlicker : MonoBehaviour
    {
        public Light glow;
        public Renderer[] embers;
        public Color emberColor = new Color(1f, 0.48f, 0.18f);

        float baseIntensity;
        float seed;

        void Start()
        {
            if (glow != null) baseIntensity = glow.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time;
            float k = 0.72f + 0.3f * Mathf.PerlinNoise(seed, t * 2.3f) + 0.08f * Mathf.Sin(t * 13f + seed);
            if (glow != null) glow.intensity = baseIntensity * k;
            if (embers != null)
                foreach (var r in embers)
                    if (r != null) r.material.SetColor("_EmissionColor", emberColor * (1.6f * k));
        }
    }
}
