using UnityEngine;

namespace QuietRoom
{
    public enum ItemKind { Water, Chocolate, Folder, SuspectPhone, Photo, Pistol }

    // Предмет, который можно взять в руку, рассмотреть, применить на подозреваемом или положить перед ним.
    public class Pickup : MonoBehaviour, IInteractable
    {
        public ItemKind kind;
        public string title;
        [Tooltip("Что прячется после использования (вода в стакане); пусто — предмет исчезает целиком")]
        public GameObject consumedPart;
        public Vector3 holdOffset = new Vector3(0.14f, -0.12f, 0.38f);
        public Vector3 holdEuler;
        [Tooltip("Поза при осмотре (Q): поднесён к глазам")]
        public Vector3 inspectOffset = new Vector3(0f, -0.01f, 0.3f);
        public Vector3 inspectEuler = new Vector3(88f, 180f, 0f);
        [Tooltip("Рука держит предмет ровно, куда бы ни смотрел игрок (стакан не выливается, когда смотришь вниз)")]
        public bool keepUpright;
        [Tooltip("Как предмет лежит на столе перед подозреваемым")]
        public Vector3 placedEuler;

        [Header("Оживление")]
        [Tooltip("Обложка папки: раскрывается, когда лежит перед ним")]
        public Transform openPivot;
        public Vector3 openEuler = new Vector3(0f, 0f, 178f);
        [Tooltip("Экран телефона: светится, когда в руке или на столе")]
        public Renderer screen;
        public Color screenColor = new Color(0.55f, 0.75f, 0.8f);

        public bool used;
        public bool placed;
        public bool held;
        public bool inspecting;
        public Vector2 spin;   // поворот мышью при осмотре
        public Vector2 handTilt;     // наклон кистью (ПКМ + мышь) — стаканом можно плеснуть
        public bool tilting;
        public float gestureUntil;   // жест в руке (поднести стакан ко рту)
        Vector3 gestureEuler, gestureOffset;
        float gestureLength;
        public bool Consumable => kind == ItemKind.Water || kind == ItemKind.Chocolate;

        public string PickSound
        {
            get
            {
                switch (kind)
                {
                    case ItemKind.Pistol: return "gun_cock";
                    case ItemKind.Folder: case ItemKind.Photo: return "paper_place";
                    case ItemKind.Chocolate: return "cloth";
                    default: return "item_pick";
                }
            }
        }

        public string PutSound
        {
            get
            {
                switch (kind)
                {
                    case ItemKind.Water: return "glass_put";
                    case ItemKind.Folder: case ItemKind.Photo: return "paper_place";
                    default: return "item_put";
                }
            }
        }

        Transform homeParent;
        Vector3 homePos;
        Quaternion homeRot;
        bool homeSaved;
        float glow;

        void SaveHome()
        {
            if (homeSaved) return;
            homeParent = transform.parent;
            homePos = transform.localPosition;
            homeRot = transform.localRotation;
            homeSaved = true;
        }

        void Update()
        {
            if (openPivot != null)
                openPivot.localRotation = Quaternion.Slerp(openPivot.localRotation,
                    placed || inspecting ? Quaternion.Euler(openEuler) : Quaternion.identity, Time.deltaTime * 5f);
            if (held)
            {
                // В руке плавно переходит между обычной позой и осмотром у глаз.
                var pos = inspecting ? inspectOffset : holdOffset;
                var rot = inspecting ? Quaternion.Euler(-spin.y, spin.x, 0f) * Quaternion.Euler(inspectEuler) : Quaternion.Euler(holdEuler);
                if (keepUpright && !inspecting && transform.parent != null)
                {
                    // Только поворот по горизонтали — наклон камеры кисть компенсирует.
                    var p = transform.parent;
                    if (!tilting) handTilt = Vector2.Lerp(handTilt, Vector2.zero, Time.deltaTime * 6f);   // отпустил — кисть выпрямляется
                    rot = Quaternion.Inverse(p.rotation) * Quaternion.Euler(0f, p.eulerAngles.y, 0f)
                        * Quaternion.Euler(handTilt.y, 0f, -handTilt.x) * Quaternion.Euler(holdEuler);
                }
                if (Time.time < gestureUntil)
                {
                    // Жест: плавно туда и обратно (синус от 0 до π).
                    float k01 = 1f - (gestureUntil - Time.time) / gestureLength;
                    float w = Mathf.Sin(Mathf.Clamp01(k01) * Mathf.PI);
                    pos += gestureOffset * w;
                    rot = rot * Quaternion.Euler(gestureEuler * w);
                }
                float k = 1f - Mathf.Exp(-Time.deltaTime * 12f);
                transform.localPosition = Vector3.Lerp(transform.localPosition, pos, k);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, rot, k);
            }
            if (screen != null)
            {
                glow = Mathf.MoveTowards(glow, placed || held ? 1f : 0.08f, Time.deltaTime * 3f);
                var mat = screen.material;
                if (!mat.IsKeywordEnabled("_EMISSION")) mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", screenColor * glow * 1.4f);
            }
        }

        public string Hint(PlayerController player)
        {
            return player.held == null ? Binds.Label(Act.Use) + " — взять: " + title : "";
        }

        public void Interact(PlayerController player)
        {
            if (player.held == null) player.Take(this);
        }

        public void AttachTo(Transform hand)
        {
            SaveHome();
            if (placed) PresentSpot.Clear(this);
            placed = false;
            held = true;
            SetColliders(false);
            SetHeldVisual(true);
            transform.SetParent(hand, false);
            transform.localPosition = holdOffset;
            transform.localRotation = Quaternion.Euler(holdEuler);
        }

        public void Gesture(Vector3 euler, Vector3 offset, float seconds)
        {
            gestureEuler = euler;
            gestureOffset = offset;
            gestureLength = seconds;
            gestureUntil = Time.time + seconds;
        }

        public void SetInspect(bool on)
        {
            inspecting = on;
            spin = Vector2.zero;
            // Рука с пистолетом мешает разглядеть сам пистолет — при осмотре прячем.
            if (held) SetHeldVisual(!on);
            var pages = GetComponent<IInspectPages>();
            if (pages != null) pages.SetInspecting(on);
        }

        public void ReturnHome()
        {
            SaveHome();
            if (inspecting) SetInspect(false);
            placed = false;
            held = false;
            transform.SetParent(homeParent, false);
            transform.localPosition = homePos;
            transform.localRotation = homeRot;
            SetColliders(true);
            SetHeldVisual(false);
        }

        public void PlaceAt(Transform spot)
        {
            SaveHome();
            if (inspecting) SetInspect(false);
            held = false;
            placed = true;
            transform.SetParent(spot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.Euler(placedEuler);
            SetColliders(true);
            SetHeldVisual(false);
        }

        // Вызывается после применения: вода выпита, шоколадка съедена.
        public bool Consume()
        {
            if (!Consumable) return false;
            used = true;
            if (consumedPart != null)
            {
                consumedPart.SetActive(false);
                title = "пустой стакан";
                return false;
            }
            gameObject.SetActive(false);
            return true;
        }

        // Дочерний объект «Held» (рука детектива) показывается только в руке.
        void SetHeldVisual(bool on)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "Held") t.gameObject.SetActive(on);
        }

        void SetColliders(bool on)
        {
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }
    }
}
