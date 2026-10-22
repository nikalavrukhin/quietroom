using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QuietRoom
{
    // Вода в гранёном стакане.
    // • Поверхность всегда горизонтальна и раскачивается от движения (пружина с затуханием).
    // • При наклоне уровень подбирается так, чтобы объём воды не менялся.
    // • Если наклонить сильнее, чем позволяет уровень, вода переливается через нижнюю точку края:
    //   струйка капель, лужа там, куда она падает, звук льющейся воды.
    public class GlassWater : MonoBehaviour
    {
        public int sides = 12;
        public float bottomY = 0.012f;
        public float topY = 0.106f;
        public float radiusBottom = 0.0265f;
        public float radiusTop = 0.0325f;
        [Range(0f, 1f)] public float fill = 0.8f;
        public float sip = 0.22f;
        public Material sideMaterial, surfaceMaterial, puddleMaterial, dropMaterial;

        public bool Empty => fill < 0.03f;
        public bool Pouring { get; private set; }

        Mesh mesh;
        MeshRenderer mr;
        Pickup pickup;
        Vector2 wobble, wobbleVel;
        Vector3 lastPos, lastVel;
        Quaternion lastRot;
        bool started;
        ParticleSystem stream;
        AudioSource pourSound;
        float spilledSinceRay, nextRay;
        bool wasPouring;
        float pouredThisTime, nextNotify, lastPourTime;
        readonly List<Transform> puddles = new List<Transform>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<int> sideTris = new List<int>(), topTris = new List<int>();
        static Vector2[] samples;   // точки по сечению стакана для подсчёта объёма

        void Start()
        {
            pickup = GetComponent<Pickup>();
            mesh = new Mesh { name = "Water" };
            mesh.MarkDynamic();
            var go = new GameObject("WaterMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { sideMaterial, surfaceMaterial };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lastPos = transform.position;
            lastRot = transform.rotation;
            BuildStream();
            if (samples == null) samples = DiskSamples(61);
        }

        // Глоток: вода плавно убывает, пока стакан у его губ.
        public void Sip()
        {
            if (Empty) return;
            StartCoroutine(Drain(Mathf.Max(0f, fill - sip)));
        }

        IEnumerator Drain(float target)
        {
            yield return new WaitForSeconds(0.35f);
            while (fill > target + 0.001f)
            {
                fill = Mathf.MoveTowards(fill, target, Time.deltaTime * 0.35f);
                yield return null;
            }
            fill = target;
        }

        void LateUpdate()
        {
            if (mesh == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            // Раскачка от ускорения стакана и от поворота.
            var pos = transform.position;
            var vel = (pos - lastPos) / dt;
            var acc = started ? Vector3.ClampMagnitude((vel - lastVel) / dt, 25f) : Vector3.zero;
            float turn = Quaternion.Angle(lastRot, transform.rotation) / dt;
            lastPos = pos; lastVel = vel; lastRot = transform.rotation; started = true;
            var force = new Vector2(-acc.x, -acc.z) * 0.01f
                      + new Vector2(Mathf.Sin(Time.time * 5.3f), Mathf.Cos(Time.time * 4.7f)) * Mathf.Min(turn, 300f) * 0.00002f;
            wobbleVel += (force - wobble * 120f - wobbleVel * 4.2f) * dt;    // ~1,7 Гц, затухает за пару секунд
            wobble = Vector2.ClampMagnitude(wobble + wobbleVel * dt, 0.35f);

            var nWorld = new Vector3(wobble.x, 1f, wobble.y).normalized;
            var n = transform.InverseTransformDirection(nWorld);

            // Сколько воды влезает при таком наклоне — лишнее выливается.
            bool drinking = pickup != null && Time.time < pickup.gestureUntil;
            float capacity = Capacity(n, out Vector3 lip);
            float over = fill - capacity;
            Pouring = over > 0.003f && !drinking && !Empty;
            if (Pouring)
            {
                float pour = Mathf.Min(over, (0.25f + over * 3f) * dt);
                fill -= pour;
                spilledSinceRay += pour;
                pouredThisTime += pour;
                lastPourTime = Time.time;
                Spill(transform.TransformPoint(lip), pour / dt);
            }
            UpdateStream(Pouring, transform.TransformPoint(lip));
            // Новый перелив — только если вода не лилась дольше секунды (иначе это тот же самый).
            if (!Pouring && Time.time - lastPourTime > 1f) pouredThisTime = 0f;
            // Вылилось заметно (около 10% стакана) — сообщаем Белых, не чаще раза в 8 секунд.
            if (pouredThisTime > 0.08f && Time.time > nextNotify)
            {
                nextNotify = Time.time + 8f;
                pouredThisTime = -999f;   // за этот перелив больше не сообщаем
                NotifySuspect(transform.TransformPoint(lip));
            }
            wasPouring = Pouring;

            Rebuild(n);
        }

        // ---------- объём ----------

        // Высота столба воды в точке (x,z) сечения при плоскости уровня h и нормали n (локальные оси).
        float Column(Vector3 n, float h, float x, float z)
        {
            return Mathf.Clamp(h - (n.x * x + n.z * z) / n.y, bottomY, topY) - bottomY;
        }

        float Fraction(Vector3 n, float h)
        {
            float sum = 0f, r = (radiusBottom + radiusTop) * 0.5f;
            foreach (var s in samples) sum += Column(n, h, s.x * r, s.y * r);
            return sum / samples.Length / (topY - bottomY);
        }

        // Уровень h, при котором воды ровно fill (бинарный поиск).
        float SolveLevel(Vector3 n, float target)
        {
            float slope = (Mathf.Abs(n.x) + Mathf.Abs(n.z)) / Mathf.Max(0.05f, n.y) * radiusTop;
            float lo = bottomY - slope, hi = topY + slope;
            for (int i = 0; i < 18; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Fraction(n, mid) < target) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        // Максимум воды без перелива; lip — нижняя точка края, откуда польётся.
        float Capacity(Vector3 n, out Vector3 lip)
        {
            float best = float.MaxValue; int bestI = 0;
            for (int i = 0; i < sides * 2; i++)
            {
                float a = i / (sides * 2f) * Mathf.PI * 2f;
                float d = n.x * Mathf.Cos(a) + n.z * Mathf.Sin(a);
                if (d < best) { best = d; bestI = i; }
            }
            float ang = bestI / (sides * 2f) * Mathf.PI * 2f;
            lip = new Vector3(Mathf.Cos(ang) * radiusTop, topY, Mathf.Sin(ang) * radiusTop);
            if (n.y < 0.06f) return 0f;   // почти лёжа или вверх дном — всё выльется
            float hCap = topY + best * radiusTop / n.y;
            return Mathf.Clamp01(Fraction(n, hCap));
        }

        // ---------- меш воды ----------

        void Rebuild(Vector3 n)
        {
            mr.enabled = !Empty && n.y > 0.06f;
            if (!mr.enabled) return;
            float h = SolveLevel(n, fill);

            verts.Clear(); sideTris.Clear(); topTris.Clear();
            int ring = sides;
            for (int i = 0; i < ring; i++)
            {
                float a = (i + 0.5f) / ring * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * radiusBottom, bottomY, Mathf.Sin(a) * radiusBottom));
            }
            float sumY = 0f;
            for (int i = 0; i < ring; i++)
            {
                float a = (i + 0.5f) / ring * Mathf.PI * 2f;
                float r = Mathf.Lerp(radiusBottom, radiusTop, Mathf.InverseLerp(bottomY, topY, h));
                float y = bottomY + Column(n, h, Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                y = Mathf.Clamp(y, bottomY + 0.0004f, topY - 0.0008f);
                float ry = Mathf.Lerp(radiusBottom, radiusTop, Mathf.InverseLerp(bottomY, topY, y)) - 0.0004f;
                verts.Add(new Vector3(Mathf.Cos(a) * ry, y, Mathf.Sin(a) * ry));
                sumY += y;
            }
            verts.Add(new Vector3(0, bottomY, 0));
            verts.Add(new Vector3(0, sumY / ring, 0));
            int cb = 2 * ring, ct = 2 * ring + 1;
            for (int i = 0; i < ring; i++)
            {
                int j = (i + 1) % ring;
                sideTris.Add(i); sideTris.Add(ring + i); sideTris.Add(ring + j);
                sideTris.Add(i); sideTris.Add(ring + j); sideTris.Add(j);
                sideTris.Add(cb); sideTris.Add(i); sideTris.Add(j);
                topTris.Add(ct); topTris.Add(ring + j); topTris.Add(ring + i);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sideTris, 0);
            mesh.SetTriangles(topTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // ---------- перелив: струйка, лужа, звук ----------

        void BuildStream()
        {
            var go = new GameObject("Stream");
            go.transform.SetParent(transform, false);
            stream = go.AddComponent<ParticleSystem>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = stream.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
            main.startColor = new Color(0.75f, 0.88f, 0.92f, 0.7f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = stream.emission;
            em.rateOverTime = 0f;
            var shape = stream.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.004f;
            var col = stream.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.dampen = 0.8f;
            col.bounce = 0.1f;
            col.lifetimeLoss = 0.6f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = dropMaterial != null ? dropMaterial : sideMaterial;

            pourSound = gameObject.AddComponent<AudioSource>();
            pourSound.clip = Sfx.Get("water_pour");
            pourSound.loop = true;
            pourSound.volume = 0f;
            pourSound.spatialBlend = 0.8f;
            pourSound.minDistance = 0.5f;
            pourSound.maxDistance = 8f;
        }

        void UpdateStream(bool on, Vector3 lipWorld)
        {
            if (stream == null) return;
            stream.transform.position = lipWorld;
            var em = stream.emission;
            em.rateOverTime = on ? 220f : 0f;
            if (on && !stream.isPlaying) stream.Play();
            if (pourSound != null && pourSound.clip != null)
            {
                pourSound.volume = Mathf.MoveTowards(pourSound.volume, on ? 0.5f : 0f, Time.deltaTime * 3f);
                if (pourSound.volume > 0.01f && !pourSound.isPlaying) pourSound.Play();
                if (pourSound.volume <= 0.01f && pourSound.isPlaying) pourSound.Pause();
            }
        }

        void NotifySuspect(Vector3 lipWorld)
        {
            var talk = FindAnyObjectByType<Interrogation>();
            if (talk == null) return;
            string where = "на пол";
            if (Physics.Raycast(lipWorld + Vector3.down * 0.01f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Suspect>() != null) where = "прямо на тебя";
                else if (hit.point.y > 0.5f) where = "на стол";
            }
            talk.PlayerAction("Детектив наклонил стакан и выливает воду " + where + "." +
                (where == "прямо на тебя" ? "" : " Решай сам, нужно ли на это что-то говорить."), true);
        }

        // Куда падает струя — там растёт лужа.
        void Spill(Vector3 lipWorld, float rate)
        {
            if (Time.time < nextRay) return;
            nextRay = Time.time + 0.12f;
            if (!Physics.Raycast(lipWorld + Vector3.down * 0.01f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.transform.IsChildOf(transform)) return;

            Transform puddle = null;
            foreach (var p in puddles)
                if (p != null && Vector3.Distance(p.position, hit.point) < p.localScale.x * 0.5f + 0.06f) { puddle = p; break; }
            if (puddle == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(go.GetComponent<Collider>());
                go.name = "Puddle";
                go.GetComponent<Renderer>().sharedMaterial = puddleMaterial;
                puddle = go.transform;
                puddle.position = hit.point + hit.normal * 0.0015f;
                puddle.up = hit.normal;
                puddle.localScale = new Vector3(0.03f, 0.0004f, 0.03f);
                puddles.Add(puddle);
            }
            // Площадь лужи растёт с пролитым объёмом (стакан ~200 мл → лужа сантиметров 35).
            float area = puddle.localScale.x * puddle.localScale.x + spilledSinceRay * 0.6f;
            float d = Mathf.Min(0.45f, Mathf.Sqrt(area));
            puddle.localScale = new Vector3(d, 0.0004f, d * Random.Range(0.85f, 1f));
            spilledSinceRay = 0f;
        }

        static Vector2[] DiskSamples(int count)
        {
            var list = new Vector2[count];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float r = Mathf.Sqrt((i + 0.5f) / count);
                float a = i * golden;
                list[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return list;
        }
    }
}
