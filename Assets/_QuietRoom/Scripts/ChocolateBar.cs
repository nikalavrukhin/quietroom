using System.Collections;
using UnityEngine;

namespace QuietRoom
{
    // Шоколадка «Алёнка»: бумажную обёртку стягивают с плитки (она падает на пол), фольгу рвут кусками —
    // каждый кусок с треском отрывается, сминается и падает. Плитка съедается в два укуса:
    // после первого остаётся половина со следами зубов (и уже этого хватает, чтобы поднять сахар).
    // Состояние хранится в самом объекте — шоколадку можно положить обратно.
    public class ChocolateBar : MonoBehaviour
    {
        public Transform wrapper;            // бумажная гильза с этикеткой
        public Transform[] foilRips;         // куски фольги в порядке отрыва (у руки остаётся свой кусок, его нет в списке)
        public GameObject full, bitten;      // целая плитка и надкушенная половина

        public bool unwrapped;
        public bool busy;                    // идёт анимация — действовать нельзя

        public int Left => full != null && full.activeSelf ? 2 : (bitten != null && bitten.activeSelf ? 1 : 0);

        public void Unwrap()
        {
            if (unwrapped || busy) return;
            StartCoroutine(UnwrapAnim());
        }

        IEnumerator UnwrapAnim()
        {
            busy = true;

            // 1. Бумагу расшатать и стянуть через передний торец.
            if (wrapper != null && wrapper.parent == transform)
            {
                Sfx.PlayNear("paper_place", 0.45f);
                var p0 = wrapper.localPosition;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    float w = Mathf.Sin(t * 55f) * (1f - t / 0.3f);
                    wrapper.localRotation = Quaternion.Euler(0f, 0f, w * 3f);
                    wrapper.localPosition = p0 + new Vector3(0f, 0f, Mathf.Abs(w) * 0.002f);
                    yield return null;
                }
                Sfx.PlayNear("paper_place", 0.35f);
                const float slide = 0.55f;
                for (float t = 0f; t < slide; t += Time.deltaTime)
                {
                    float k = t / slide;
                    wrapper.localPosition = p0 + new Vector3(0f, 0.0015f * k, 0.15f * k * k);
                    wrapper.localRotation = Quaternion.Euler(-4f * k, 0f, Mathf.Sin(k * 9f) * 2f);
                    yield return null;
                }
                Drop(wrapper, 0.01f, 1.2f, Vector3.zero);
                yield return new WaitForSeconds(0.25f);
            }

            // 2. Фольгу рвут кусками: натянуть — треск — кусок отлетает, сминаясь, и падает на пол.
            if (foilRips != null)
                foreach (var piece in foilRips)
                {
                    if (piece == null || piece.parent != transform) continue;
                    var p0 = piece.localPosition;
                    var r0 = piece.localRotation;
                    var outward = new Vector3(p0.x, p0.y * 3f, 0f).normalized;   // от оси плитки к куску
                    if (outward == Vector3.zero) outward = Vector3.forward;
                    for (float t = 0f; t < 0.14f; t += Time.deltaTime)
                    {
                        float k = t / 0.14f;
                        piece.localPosition = p0 + (outward * 0.0025f + Vector3.forward * 0.002f) * k;
                        piece.localRotation = r0 * Quaternion.Euler(-6f * k, 0f, 4f * k);
                        yield return null;
                    }
                    Sfx.PlayNear("foil_rip", 0.6f);
                    StartCoroutine(Crumple(piece));
                    var cam = Camera.main != null ? Camera.main.transform : transform;
                    var side = cam.right * (Random.value < 0.5f ? -0.55f : 0.55f);   // в сторону, а не в лицо
                    Drop(piece, 0.003f, 2.2f, transform.TransformDirection(outward) * 0.2f + side + Vector3.up * 0.3f);
                    yield return new WaitForSeconds(0.16f);
                }
            unwrapped = true;
            busy = false;
        }

        // Оторванный кусок фольги за полсекунды сминается: точки уходят к центру и сбиваются в складки.
        IEnumerator Crumple(Transform piece)
        {
            var mf = piece.GetComponent<MeshFilter>();
            if (mf == null) yield break;
            var mesh = Instantiate(mf.sharedMesh);
            mf.sharedMesh = mesh;
            var v0 = mesh.vertices;
            var v = new Vector3[v0.Length];
            float seed = Random.value * 100f;
            for (float t = 0f; t <= 0.5f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.5f);
                k = 1f - (1f - k) * (1f - k);
                for (int i = 0; i < v0.Length; i++)
                {
                    var p = v0[i];
                    // Шум по положению (а не по номеру вершины), чтобы лицевая и изнанка мялись одинаково.
                    float nx = Mathf.PerlinNoise(p.x * 160f + seed, p.z * 160f) - 0.5f;
                    float ny = Mathf.PerlinNoise(p.z * 160f + seed, p.y * 160f + 3f) - 0.5f;
                    float nz = Mathf.PerlinNoise(p.y * 160f + 7f, p.x * 160f + seed) - 0.5f;
                    v[i] = Vector3.Lerp(p, p * 0.55f, k) + new Vector3(nx, ny, nz) * 0.012f * k;
                }
                mesh.vertices = v;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                yield return null;
            }
        }

        // Отпустить кусок в свободный полёт; полежав на полу, он становится декорацией (без физики и коллайдера).
        void Drop(Transform t, float mass, float drag, Vector3 push)
        {
            t.SetParent(null, true);
            var col = t.gameObject.AddComponent<BoxCollider>();
            var mf = t.GetComponent<MeshFilter>();
            if (mf != null)
            {
                var b = mf.sharedMesh.bounds;
                col.center = b.center;
                col.size = Vector3.Max(b.size, Vector3.one * 0.004f);
            }
            foreach (var c in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
            var rb = t.gameObject.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = drag;
            rb.angularDamping = 0.6f;
            var cam = Camera.main != null ? Camera.main.transform : transform;
            if (push == Vector3.zero) push = cam.right * 0.7f + Vector3.up * 0.4f + transform.forward * 0.3f;
            rb.linearVelocity = push + Random.insideUnitSphere * 0.15f;
            rb.angularVelocity = Random.insideUnitSphere * 8f;
            StartCoroutine(Settle(rb, col));
        }

        IEnumerator Settle(Rigidbody rb, Collider col)
        {
            float until = Time.time + 7f;
            yield return new WaitForSeconds(0.8f);
            while (rb != null && !rb.IsSleeping() && Time.time < until) yield return null;
            if (rb != null) Destroy(rb);
            if (col != null) Destroy(col);
        }

        // ---------- укусы ----------

        // Откусить: через delay секунд половина плитки исчезает — остаётся край со следами зубов, сыплются крошки.
        public void EatPiece(float delay)
        {
            if (busy || Left <= 0) return;
            StartCoroutine(Bite(delay));
        }

        IEnumerator Bite(float delay)
        {
            busy = true;
            yield return new WaitForSeconds(delay);
            Sfx.PlayNear("choco_snap", 0.6f);
            var src = full != null && full.activeSelf ? full : bitten;
            Crumbs(src, src == full ? 0.002f : -0.045f);
            if (src == full) { full.SetActive(false); if (bitten != null) bitten.SetActive(true); }
            else if (bitten != null) bitten.SetActive(false);
            yield return new WaitForSeconds(0.2f);
            busy = false;
        }

        void Crumbs(GameObject src, float z)
        {
            var r = src.GetComponent<MeshRenderer>();
            for (int i = 0; i < 5; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Crumb";
                var cc = c.GetComponent<Collider>();
                foreach (var own in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(cc, own);
                c.transform.position = transform.TransformPoint(new Vector3(Random.Range(-0.03f, 0.03f), 0f, z + Random.Range(0f, 0.012f)));
                c.transform.rotation = Random.rotation;
                c.transform.localScale = Vector3.one * Random.Range(0.0018f, 0.0035f);
                if (r != null) c.GetComponent<MeshRenderer>().sharedMaterial = r.sharedMaterial;
                var rb = c.AddComponent<Rigidbody>();
                rb.mass = 0.001f;
                rb.linearVelocity = Random.insideUnitSphere * 0.3f;
                Destroy(c, 2.5f);
            }
        }
    }
}
