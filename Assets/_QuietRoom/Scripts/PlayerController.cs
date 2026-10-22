using System.Collections;
using UnityEngine;

namespace QuietRoom
{
    // Ходьба от первого лица, прицел, предметы в руке.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public Transform hand;
        public float speed = 2.2f;
        public float sensitivity = 2.0f;
        public float reach = 2.3f;
        public float hitReach = 1.7f;

        [HideInInspector] public Pickup held;
        [HideInInspector] public bool inputLocked;   // HUD: печатаем, телефон, осмотр
        [HideInInspector] public IInteractable target;
        [HideInInspector] public Suspect lookedSuspect;
        [HideInInspector] public Seat seat;          // сидим на стуле (null — стоим)
        [HideInInspector] public bool seatBusy;      // идёт анимация «подойти и сесть» / «встать»

        CharacterController cc;
        Suspect suspect;
        float pitch, yVel, bob;
        Vector3 camBase;
        float shake;
        float sitLower;          // насколько опущена камера
        float camRoll;           // лёгкий крен камеры, пока садимся/встаём

        CharacterController CC => cc != null ? cc : (cc = GetComponent<CharacterController>());

        void Start()
        {
            if (cam != null) camBase = cam.transform.localPosition;
        }

        void Update()
        {
            var game = GameDirector.I;
            bool active = game != null && game.Playing && !inputLocked && !Binds.Paused;
            // Мышь в игре не нужна ни для чата, ни для телефона — курсор прячем всю ночь.
            bool playing = game != null && game.Playing && !Binds.Paused;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;

            Aim();
            // Ствол на подозреваемом — он это видит и реагирует сам, без нажатий.
            if (suspect == null) suspect = FindAnyObjectByType<Suspect>();
            if (suspect != null && game != null && game.Playing)
                suspect.UpdateAim(held != null && held.kind == ItemKind.Pistol && GunOn(suspect), Time.deltaTime);
            if (seatBusy) { CameraFx(); return; }   // тело ведёт анимация стула
            if (!active) { if (seat == null) Gravity(Vector3.zero); return; }

            Look();
            if (seat != null) { if (AnyMoveKey()) Stand(); }
            else Move();
            Actions();
            CameraFx();
        }

        void Look()
        {
            float mx = Input.GetAxisRaw("Mouse X") * sensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * sensitivity;
            // ПКМ со стаканом: мышь крутит кисть, а не голову — так можно наклонить и разлить.
            bool tilt = held != null && held.keepUpright && Input.GetMouseButton(1);
            if (held != null) held.tilting = tilt;
            if (tilt)
            {
                held.handTilt.x = Mathf.Clamp(held.handTilt.x + mx * 1.6f, -170f, 170f);
                held.handTilt.y = Mathf.Clamp(held.handTilt.y - my * 1.6f, -170f, 170f);
                return;
            }
            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -80f, 80f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, camRoll);
        }

        void Move()
        {
            float x = (Binds.Held(Act.Right) ? 1f : 0f) - (Binds.Held(Act.Left) ? 1f : 0f);
            float z = (Binds.Held(Act.Forward) ? 1f : 0f) - (Binds.Held(Act.Back) ? 1f : 0f);
            var dir = transform.right * x + transform.forward * z;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            Gravity(dir * speed);
            float before = bob;
            bob += dir.magnitude * Time.deltaTime * 9f;
            // Шаг — на каждом полупериоде покачивания камеры.
            if (Mathf.Floor(before / Mathf.PI) != Mathf.Floor(bob / Mathf.PI)) Sfx.PlayNear("step", 0.32f);
        }

        void Gravity(Vector3 horizontal)
        {
            yVel = CC.isGrounded ? -1f : yVel - 9.81f * Time.deltaTime;
            CC.Move((horizontal + Vector3.up * yVel) * Time.deltaTime);
        }

        void Aim()
        {
            target = null;
            lookedSuspect = null;
            if (cam == null) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var hits = Physics.RaycastAll(ray, 12f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                // Считается только первое, что видно, — собственную капсулу пропускаем.
                if (hit.collider.transform.IsChildOf(transform)) continue;
                // Пустыми руками место на столе прозрачно — сквозь него берём лежащий там предмет.
                if (held == null && hit.collider.GetComponent<PresentSpot>() != null) continue;
                var s = hit.collider.GetComponentInParent<Suspect>();
                var it = hit.collider.GetComponentInParent<IInteractable>();
                if (s != null) lookedSuspect = s;
                if (it != null && hit.distance <= (s != null ? reach + 0.6f : reach)) target = it;
                break;
            }
        }

        // Ствол смотрит на него: по углу, а не лучом — луч упирался в стол и улики перед ним.
        bool GunOn(Suspect s)
        {
            if (s.dead || s.head == null) return false;
            var from = cam.transform.position;
            var body = s.head.position - Vector3.up * 0.25f;   // грудь — середина между головой и животом
            var toBody = body - from;
            if (toBody.magnitude > 7f) return false;
            float angle = Vector3.Angle(cam.transform.forward, toBody);
            return angle < 14f;
        }

        public bool CanHit(Suspect s)
        {
            return s != null && Vector3.Distance(cam.transform.position, s.head.position) <= hitReach;
        }

        void Actions()
        {
            // Шоколадка в руке сначала разворачивается; потом E на Белых — дать кусочек.
            var bar = held != null ? held.GetComponent<ChocolateBar>() : null;
            if (Binds.Down(Act.Use) && bar != null && !bar.unwrapped && (target == null || target is Suspect)) bar.Unwrap();
            else if (Binds.Down(Act.Use) && target != null) target.Interact(this);
            if (Binds.Down(Act.Drop) && held != null) Drop();
            if (Binds.Down(Act.Hit) && lookedSuspect != null && held == null && CanHit(lookedSuspect))
            {
                lookedSuspect.Hit();
                shake = 0.6f;
            }
            if (Input.GetMouseButtonDown(0) && held != null && held.kind == ItemKind.Pistol)
            {
                shake = 1.5f;
                if (suspect != null && (lookedSuspect == suspect || GunOn(suspect))) suspect.Shot();
                else Sfx.Play("gunshot", cam.transform.position, 1f, 0.03f, 0.3f);
            }
        }

        void CameraFx()
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 3f);
            var offset = new Vector3(Mathf.Sin(bob) * 0.012f, Mathf.Abs(Mathf.Cos(bob)) * 0.02f, 0f);
            offset += Random.insideUnitSphere * shake * 0.02f;
            cam.transform.localPosition = camBase + offset + Vector3.down * sitLower;
        }

        // ---------- стул ----------

        static bool AnyMoveKey()
        {
            return Binds.Down(Act.Forward) || Binds.Down(Act.Back) || Binds.Down(Act.Left) || Binds.Down(Act.Right);
        }

        public void Sit(Seat s)
        {
            if (seat != null || seatBusy || s == null) return;
            StartCoroutine(SitRoutine(s));
        }

        public void Stand()
        {
            if (seat == null || seatBusy) return;
            StartCoroutine(StandRoutine(seat));
        }

        static float Smooth(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        float SeatedLower(Seat s) => Mathf.Max(0f, (transform.position.y + camBase.y) - (s.transform.position.y + s.eyeHeight));

        Vector3 SeatPoint(Seat s)
        {
            var p = s.transform.TransformPoint(s.seatPoint);
            p.y = transform.position.y;
            return p;
        }

        // Свободное место у стула, откуда садятся и куда встают: спереди или сбоку, ближайшее к from.
        Vector3 SidePoint(Seat s, Vector3 from)
        {
            var st = s.transform;
            float r = CC.radius, h = CC.height;
            Vector3 best = SeatPoint(s);
            float bestD = float.MaxValue;
            foreach (var local in new[] { new Vector3(0f, 0f, 0.45f), new Vector3(0.52f, 0f, 0.05f), new Vector3(-0.52f, 0f, 0.05f), new Vector3(0.45f, 0f, 0.4f), new Vector3(-0.45f, 0f, 0.4f) })
            {
                var p = st.TransformPoint(local);
                p.y = transform.position.y;
                var bottom = p + CC.center + Vector3.down * (h / 2f - r) + Vector3.up * 0.05f;
                var top = p + CC.center + Vector3.up * (h / 2f - r);
                bool blocked = false;
                foreach (var c in Physics.OverlapCapsule(bottom, top, r * 0.9f, ~0, QueryTriggerInteraction.Ignore))
                    if (!c.transform.IsChildOf(transform) && !c.transform.IsChildOf(st)) { blocked = true; break; }
                if (blocked) continue;
                float d = (p - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        void Steps(float distance)
        {
            float before = bob;
            bob += distance * 4.1f;     // как при обычной ходьбе: 9 рад/с на 2,2 м/с
            if (Mathf.Floor(before / Mathf.PI) != Mathf.Floor(bob / Mathf.PI)) Sfx.PlayNear("step", 0.32f);
        }

        void SetCam(float p, float roll)
        {
            pitch = p;
            camRoll = roll;
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, camRoll);
        }

        // Подойти к стулу, развернуться лицом к столу и опуститься на сиденье.
        IEnumerator SitRoutine(Seat s)
        {
            seatBusy = true;
            seat = s;
            CC.enabled = false;           // капсула упиралась бы в стул
            yVel = 0f;
            float chairYaw = s.transform.eulerAngles.y;
            var approach = SidePoint(s, transform.position);

            // 1. Подход шагами: смотрим, куда идём, к концу разворачиваемся по стулу.
            var p0 = transform.position;
            float yaw0 = transform.eulerAngles.y, pitch0 = pitch;
            var flat = approach - p0; flat.y = 0f;
            float dist = flat.magnitude;
            float walkYaw = dist > 0.05f ? Quaternion.LookRotation(flat).eulerAngles.y : chairYaw;
            float walk = Mathf.Max(0.3f, dist / 1.5f);
            var prev = p0;
            for (float t = 0f; t < walk; t += Time.deltaTime)
            {
                float k = Smooth(t / walk);
                transform.position = Vector3.Lerp(p0, approach, k);
                Steps(Vector3.Distance(prev, transform.position));
                prev = transform.position;
                float yawK = Smooth(t / walk * 1.6f);
                float y = Mathf.LerpAngle(Mathf.LerpAngle(yaw0, walkYaw, yawK), chairYaw, Smooth((t / walk - 0.55f) / 0.45f));
                transform.rotation = Quaternion.Euler(0f, y, 0f);
                SetCam(Mathf.Lerp(pitch0, 12f, k), 0f);
                yield return null;
            }
            transform.position = approach;

            // 2. Опуститься: наклон вперёд, тело уходит назад на сиденье, в конце чуть «проседаем» и стул скрипит.
            float lower = SeatedLower(s);
            var seatP = SeatPoint(s);
            float yawA = transform.eulerAngles.y, pitchA = pitch;
            const float down = 0.85f;
            bool creaked = false;
            for (float t = 0f; t < down; t += Time.deltaTime)
            {
                float k = t / down;
                transform.position = Vector3.Lerp(approach, seatP, Smooth(k * 1.15f));
                transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(yawA, chairYaw, Smooth(k * 1.4f)), 0f);
                float settle = 0.035f * Mathf.Sin(Mathf.Clamp01((k - 0.62f) / 0.38f) * Mathf.PI);
                sitLower = lower * Smooth(k / 0.8f) + settle;
                SetCam(Mathf.Lerp(pitchA, 4f, Smooth(k)) + 16f * Mathf.Sin(Mathf.Clamp01(k / 0.85f) * Mathf.PI), 2.5f * Mathf.Sin(k * Mathf.PI));
                if (!creaked && k > 0.65f) { creaked = true; Sfx.Play("chair_creak", s.transform.position + Vector3.up * 0.45f, 0.5f); }
                yield return null;
            }
            transform.position = seatP;
            transform.rotation = Quaternion.Euler(0f, chairYaw, 0f);
            sitLower = lower;
            SetCam(4f, 0f);
            seatBusy = false;
        }

        // Встать: наклониться вперёд, подняться и шагнуть в свободную сторону от стула.
        IEnumerator StandRoutine(Seat s)
        {
            seatBusy = true;
            Sfx.Play("chair_creak", s.transform.position + Vector3.up * 0.45f, 0.4f);
            var seatP = transform.position;
            var outP = SidePoint(s, seatP + s.transform.forward * 0.5f);
            float lower = sitLower, pitch0 = pitch;
            const float up = 0.75f;
            var prev = seatP;
            for (float t = 0f; t < up; t += Time.deltaTime)
            {
                float k = t / up;
                sitLower = lower * (1f - Smooth((k - 0.1f) / 0.75f));
                transform.position = Vector3.Lerp(seatP, outP, Smooth((k - 0.25f) / 0.75f));
                if (k > 0.5f) Steps(Vector3.Distance(prev, transform.position));
                prev = transform.position;
                SetCam(Mathf.Lerp(pitch0, 0f, Smooth(k)) + 14f * Mathf.Sin(Mathf.Clamp01(k / 0.7f) * Mathf.PI), -2f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            transform.position = outP;
            sitLower = 0f;
            SetCam(pitch, 0f);
            seat = null;
            CC.enabled = true;
            seatBusy = false;
        }

        public void Take(Pickup p)
        {
            if (held != null) return;
            held = p;
            p.AttachTo(hand);
            Sfx.PlayNear(p.PickSound, 0.5f);
        }

        public void Drop()
        {
            if (held == null) return;
            held.ReturnHome();
            Sfx.Play(held.PutSound, held.transform.position, 0.5f);
            held = null;
        }

        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }
    }
}
