using UnityEngine;

namespace QuietRoom
{
    // Лампа над столом чуть покачивается и изредка моргает.
    public class HangingLamp : MonoBehaviour
    {
        public Light bulb;
        public Renderer glow;
        public float baseIntensity = 3.2f;
        float flickerUntil, nextFlicker = 8f;

        void Update()
        {
            float t = Time.time;
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f) * 1.4f, 0f, Mathf.Sin(t * 0.43f + 1f) * 1.1f);

            if (t > nextFlicker) { flickerUntil = t + Random.Range(0.08f, 0.35f); nextFlicker = t + Random.Range(9f, 25f); }
            float k = t < flickerUntil ? (Random.value < 0.5f ? 0.25f : 0.8f) : 1f;
            k *= 1f + Mathf.Sin(t * 50f) * 0.01f;
            if (bulb != null) bulb.intensity = baseIntensity * k;
            if (glow != null) glow.material.SetColor("_EmissionColor", new Color(1f, 0.78f, 0.5f) * 2.2f * k);
        }
    }
}
