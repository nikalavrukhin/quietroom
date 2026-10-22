using System.Collections.Generic;
using UnityEngine;

namespace QuietRoom
{
    // Звуки: настоящие записи из Resources/Audio (CC0, список в CREDITS.txt).
    // Имя без номера («punch») выбирает случайный вариант из punch_1, punch_2…
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioClip[]> variants = new Dictionary<string, AudioClip[]>();

        public static AudioClip Get(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            cache[name] = c;
            return c;
        }

        static AudioClip Pick(string name)
        {
            if (!variants.TryGetValue(name, out var list))
            {
                var found = new List<AudioClip>();
                var exact = Get(name);
                if (exact != null) found.Add(exact);
                for (int i = 0; i <= 6; i++)
                {
                    var v = Get(name + "_" + i);
                    if (v != null) found.Add(v);
                }
                list = found.ToArray();
                variants[name] = list;
            }
            return list.Length == 0 ? null : list[Random.Range(0, list.Length)];
        }

        // Разовый звук в точке. maxSeconds > 0 — обрезать длинную запись (вибрация телефона).
        public static AudioSource Play(string name, Vector3 pos, float volume, float pitchJitter = 0.06f, float spatial = 0.7f, float maxSeconds = 0f)
        {
            var clip = Pick(name);
            if (clip == null) return null;
            var go = new GameObject("sfx_" + name);
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.spatialBlend = spatial;
            src.minDistance = 1.2f;
            src.maxDistance = 12f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.Play();
            float life = maxSeconds > 0f ? Mathf.Min(maxSeconds, clip.length) : clip.length;
            if (maxSeconds > 0f) go.AddComponent<SfxFade>().fadeAt = Time.time + life - 0.15f;
            Object.Destroy(go, life / Mathf.Max(0.5f, src.pitch) + 0.1f);
            return src;
        }

        // Звук прямо «в ушах» игрока: шаги, бумага в руках, телефон.
        public static void PlayNear(string name, float volume, float maxSeconds = 0f)
        {
            var cam = Camera.main;
            Play(name, cam != null ? cam.transform.position : Vector3.zero, volume, 0.06f, 0f, maxSeconds);
        }

        // Петля на объекте: гул лампы, часы, дождь за окном.
        public static AudioSource Loop(GameObject host, string name, float volume, float spatial, float minDist = 1f, float maxDist = 10f)
        {
            var clip = Get(name);
            if (clip == null) return null;
            var src = host.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = volume;
            src.spatialBlend = spatial;
            src.minDistance = minDist;
            src.maxDistance = maxDist;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.time = Random.Range(0f, clip.length * 0.9f);   // чтобы петли не стартовали синхронно
            src.Play();
            return src;
        }

        // Тихий низкий гул подвала под всем остальным — синтез, записи для этого не нужны.
        static AudioClip room;
        public static AudioClip Room
        {
            get
            {
                if (room != null) return room;
                const int rate = 44100;
                int n = rate * 6;
                var data = new float[n];
                var rng = new System.Random(7);
                float brown = 0f;
                for (int i = 0; i < n; i++)
                {
                    brown = Mathf.Clamp(brown + ((float)rng.NextDouble() - 0.5f) * 0.02f, -1f, 1f) * 0.999f;
                    data[i] = brown * 0.6f;
                }
                int fade = rate / 4;
                for (int i = 0; i < fade; i++)
                {
                    float k = (float)i / fade;
                    data[i] = data[i] * k + data[n - fade + i] * (1f - k);
                }
                var trimmed = new float[n - fade];
                System.Array.Copy(data, trimmed, n - fade);
                room = AudioClip.Create("room", n - fade, 1, rate, false);
                room.SetData(trimmed, 0);
                return room;
            }
        }
    }
}
