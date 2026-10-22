using UnityEngine;

namespace QuietRoom
{
    // Папка на кольцах: листы надеты дырками на два кольца у корешка (справа, по +x папки)
    // и при осмотре переворачиваются по одному — дырка едет по кольцу, лист плавно ложится налево.
    public class FolderPages : MonoBehaviour, IInspectPages
    {
        [Tooltip("Листы сверху вниз: точка поворота — в дырке листа, лист тянется по −x")]
        public Transform[] sheets;
        [Tooltip("Корешок (это Pickup.openPivot) и обложка на нём: обложка повторяет поворот корешка")]
        public Transform spine, cover;
        public int page;
        bool inspecting;
        float[] t;
        Canvas[] faces;
        Pickup pickup;

        // Кольца (в координатах папки): центр, радиус. Корешок по +x, листы — по −x от колец.
        public const float RingX = 0.1f, RingY = 0.0135f, RingR = 0.0085f;
        public const float Margin = 0.01f;   // от дырки до края листа
        const float Flip = 0.6f;             // секунд на один лист

        public string Help => "← → листать · Q — убрать";

        public void Prev() { if (page > 0) { page--; Sfx.PlayNear("page", 0.55f); } }
        public void Next() { if (page < sheets.Length - 1) { page++; Sfx.PlayNear("page", 0.55f); } }
        public void SetInspecting(bool on)
        {
            if (on != inspecting) Sfx.PlayNear(on ? "folder_open" : "folder_close", 0.45f);
            inspecting = on;
            if (!on) page = 0;
        }

        // Поза листа i из n при доле переворота p (0 — лежит в стопке справа, 1 — перевёрнут налево).
        public static void Pose(int i, int n, float p, out Vector3 pos, out Quaternion rot)
        {
            float e = p * p * p * (p * (6f * p - 15f) + 10f);   // плавно разгоняется и тормозит

            // Справа: верхний лист выше всех. Слева: первый перевёрнутый ложится вниз, на обложку.
            float hR = 0.0105f + (n - 1 - i) * 0.0011f;
            float hL = 0.0075f + i * 0.0012f;
            float aR = Mathf.Asin(Mathf.Clamp((hR - RingY) / RingR, -1f, 1f)) * Mathf.Rad2Deg;
            float aL = 180f - Mathf.Asin(Mathf.Clamp((hL - RingY) / RingR, -1f, 1f)) * Mathf.Rad2Deg;
            // Перевёрнутый лист свисает концом на раскрытую обложку.
            float droop = Mathf.Atan2(hL - (0.0042f + i * 0.0006f), 0.2f) * Mathf.Rad2Deg;

            // Считаем зеркально (корешок слева), потом отражаем по x.
            float a = Mathf.Lerp(aR, aL, e) * Mathf.Deg2Rad;
            float ang = Mathf.Lerp(0f, 180f + droop, e);
            float x = -RingX + RingR * Mathf.Cos(a), y = RingY + RingR * Mathf.Sin(a);
            // Свободный уголок приподнимается первым: ось поворота чуть скошена посреди переворота.
            float skew = 7f * Mathf.Sin(Mathf.PI * e) * Mathf.Deg2Rad;
            var axis = new Vector3(-Mathf.Sin(skew), 0f, Mathf.Cos(skew));
            pos = new Vector3(-x, y, -0.002f);
            rot = Quaternion.AngleAxis(-ang, axis);
        }

        void Update()
        {
            if (spine != null && cover != null) cover.localRotation = spine.localRotation;
            if (sheets == null) return;
            if (t == null || t.Length != sheets.Length)
            {
                t = new float[sheets.Length];
                faces = new Canvas[sheets.Length];
                for (int i = 0; i < sheets.Length; i++) if (sheets[i] != null) faces[i] = sheets[i].GetComponentInChildren<Canvas>(true);
            }
            var cam = Camera.main;
            // Листы нарисованы без освещения (иначе в подвале не прочесть) — сбоку из щели закрытой папки они светились.
            // Текст и белый фон показываем, только когда папка раскрыта (по углу корешка — так видно и наблюдателю).
            bool open = spine == null || Quaternion.Angle(spine.localRotation, Quaternion.identity) > 25f;
            // Перед ним папка раскрыта на выписке: справку детектива ему не показывают.
            if (!inspecting)
            {
                if (pickup == null) pickup = GetComponent<Pickup>();
                page = pickup != null && pickup.placed ? 1 : 0;
            }
            for (int i = 0; i < sheets.Length; i++)
            {
                if (sheets[i] == null) continue;
                float goal = i < page ? 1f : 0f;
                t[i] = Mathf.MoveTowards(t[i], goal, Time.deltaTime / Flip);
                Pose(i, sheets.Length, t[i], out var pos, out var rot);
                sheets[i].localPosition = pos;
                sheets[i].localRotation = rot;
                // Текст листа рисуется с обеих сторон — с оборота он проступал бы сквозь бумагу зеркально.
                if (faces[i] != null)
                    faces[i].enabled = open && (cam == null || Vector3.Dot(cam.transform.position - sheets[i].position, sheets[i].up) > 0f);
            }
        }
    }
}
