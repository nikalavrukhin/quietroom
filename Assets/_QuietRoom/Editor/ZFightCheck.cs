using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace QuietRoom.EditorTools
{
    // Ищет мерцание (z-fighting): пары граней кубов, лежащих в одной плоскости, смотрящих в одну сторону
    // и перекрывающихся. Такие грани видеокарта рисует то одну, то другую — картинка мерцает.
    public static class ZFightCheck
    {
        struct Face
        {
            public Renderer owner;
            public Vector3 center, normal, u, v;   // u, v — полуоси прямоугольника грани
        }

        public static string Run(Transform root, float tolerance = 0.0012f)
        {
            var pairs = Pairs(root, tolerance);
            var sb = new StringBuilder();
            for (int i = 0; i < pairs.Count && i < 60; i++)
                sb.Append(Path(pairs[i].Key.transform, root)).Append("  <->  ").Append(Path(pairs[i].Value.transform, root)).Append('\n');
            return pairs.Count + " пар\n" + sb;
        }

        // Чинит найденное: у меньшей из двух деталей грани утапливаются на 1 мм с каждой стороны.
        public static int Fix(Transform root, float tolerance = 0.0003f)
        {
            int fixedCount = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                var pairs = Pairs(root, tolerance);
                if (pairs.Count == 0) break;
                var done = new HashSet<Renderer>();
                foreach (var p in pairs)
                {
                    var small = Volume(p.Key) <= Volume(p.Value) ? p.Key : p.Value;
                    if (!done.Add(small)) continue;
                    var t = small.transform;
                    var ls = t.lossyScale;
                    var k = new Vector3(Shrink(ls.x), Shrink(ls.y), Shrink(ls.z));
                    t.localScale = Vector3.Scale(t.localScale, k);
                    fixedCount++;
                }
            }
            return fixedCount;
        }

        static float Shrink(float size)
        {
            size = Mathf.Abs(size);
            if (size < 1e-5f) return 1f;
            return Mathf.Max(0.9f, (size - 0.002f) / size);
        }

        static float Volume(Renderer r)
        {
            var s = r.transform.lossyScale;
            return Mathf.Abs(s.x * s.y * s.z);
        }

        static List<KeyValuePair<Renderer, Renderer>> Pairs(Transform root, float tolerance)
        {
            var result = new List<KeyValuePair<Renderer, Renderer>>();
            var faces = new List<Face>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
                var r = mf.GetComponent<Renderer>();
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var t = mf.transform;
                Vector3 ax = t.right * t.lossyScale.x * 0.5f, ay = t.up * t.lossyScale.y * 0.5f, az = t.forward * t.lossyScale.z * 0.5f;
                Vector3 c = t.position;
                faces.Add(new Face { owner = r, center = c + ax, normal = ax.normalized, u = ay, v = az });
                faces.Add(new Face { owner = r, center = c - ax, normal = -ax.normalized, u = ay, v = az });
                faces.Add(new Face { owner = r, center = c + ay, normal = ay.normalized, u = ax, v = az });
                faces.Add(new Face { owner = r, center = c - ay, normal = -ay.normalized, u = ax, v = az });
                faces.Add(new Face { owner = r, center = c + az, normal = az.normalized, u = ax, v = ay });
                faces.Add(new Face { owner = r, center = c - az, normal = -az.normalized, u = ax, v = ay });
            }

            for (int i = 0; i < faces.Count; i++)
                for (int j = i + 1; j < faces.Count; j++)
                {
                    var a = faces[i]; var b = faces[j];
                    if (a.owner == b.owner) continue;
                    if (Vector3.Dot(a.normal, b.normal) < 0.999f) continue;
                    if (Mathf.Abs(Vector3.Dot(b.center - a.center, a.normal)) > tolerance) continue;
                    if (!Overlap(a, b)) continue;
                    result.Add(new KeyValuePair<Renderer, Renderer>(a.owner, b.owner));
                }
            return result;
        }

        // Перекрываются ли два прямоугольника в общей плоскости (проверка по разделяющим осям).
        static bool Overlap(Face a, Face b)
        {
            var axes = new[] { a.u.normalized, a.v.normalized, b.u.normalized, b.v.normalized };
            foreach (var ax in axes)
            {
                if (ax.sqrMagnitude < 1e-8f) continue;
                float ca = Vector3.Dot(a.center, ax), cb = Vector3.Dot(b.center, ax);
                float ra = Mathf.Abs(Vector3.Dot(a.u, ax)) + Mathf.Abs(Vector3.Dot(a.v, ax));
                float rb = Mathf.Abs(Vector3.Dot(b.u, ax)) + Mathf.Abs(Vector3.Dot(b.v, ax));
                if (Mathf.Abs(ca - cb) >= ra + rb - 0.0005f) return false;
            }
            return true;
        }

        static string Path(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
            return string.Join("/", parts);
        }
    }
}
