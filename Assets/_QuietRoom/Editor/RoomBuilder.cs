using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace QuietRoom.EditorTools
{
    // Собирает сцену подвала целиком из примитивов. Меню «Подвал → Собрать сцену».
    public static class RoomBuilder
    {
        const string Root = "Assets/_QuietRoom";
        const string ScenePath = Root + "/Scenes/Basement.unity";
        const string MatDir = Root + "/Materials";

        const float W = 4f;      // полуширина комнаты
        const float H = 2.7f;    // высота потолка

        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        [MenuItem("Подвал/Собрать сцену")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Сначала останови Play."); return; }
            Build();
        }

        public static void Build()
        {
            mats.Clear();
            System.IO.Directory.CreateDirectory(MatDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Lighting();
            var room = new GameObject("Room").transform;
            Shell(room);
            Props(room);
            Details(room);
            var lamp = Lamp(room);
            var suspect = BuildSuspect();
            Items();
            var player = Player();
            Systems(player);

            // Мерцание: совпадающие грани разводим автоматически.
            int zf = ZFightCheck.Fix(room) + ZFightCheck.Fix(GameObject.Find("Items").transform) + ZFightCheck.Fix(suspect.transform);
            if (zf > 0) Debug.Log("[QuietRoom] Разведено совпадающих граней: " + zf);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietRoom] Подвал собран: " + ScenePath);
        }

        // ---------- свет и атмосфера ----------

        static void Lighting()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Сумрак: комната угадывается, а свет собран в два пятна — стол допроса и доска с верстаком.
            RenderSettings.ambientSkyColor = new Color(0.15f, 0.134f, 0.116f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.088f, 0.075f);
            RenderSettings.ambientGroundColor = new Color(0.052f, 0.045f, 0.038f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.07f;
            RenderSettings.fogColor = new Color(0.062f, 0.05f, 0.04f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.3f;
            QualitySettings.shadowDistance = 14f;
            QualitySettings.pixelLightCount = 8;
        }

        // ---------- стены, пол, потолок ----------

        static void Shell(Transform room)
        {
            // Текстуры нарисованы скриптом (бетон, плиты, облупленная краска); один тайл — 2 м.
            var concrete = Surface("Concrete", "tex_wall", new Vector2(4.2f, 1.35f), "#E6DED2", 0.05f);
            var floor = Surface("Floor", "tex_wood_floor", new Vector2(4.2f, 4.2f), "#CDBFAE", 0.22f);   // дощатый пол
            var ceiling = Surface("Ceiling", "tex_wall", new Vector2(4.2f, 4.2f), "#A8A094", 0.02f);
            var stripe = Surface("WallStripe", "tex_panel", new Vector2(4f, 1f), "#E2E2DC", 0.1f);

            Box("Floor", room, new Vector3(0, -0.1f, 0), new Vector3(2 * W + 0.4f, 0.2f, 2 * W + 0.4f), floor, true);
            Box("Ceiling", room, new Vector3(0, H + 0.1f, 0), new Vector3(2 * W + 0.4f, 0.2f, 2 * W + 0.4f), ceiling, true);
            Box("WallN", room, new Vector3(0, H / 2, W + 0.1f), new Vector3(2 * W + 0.4f, H, 0.2f), concrete, true);
            Box("WallS", room, new Vector3(0, H / 2, -W - 0.1f), new Vector3(2 * W + 0.4f, H, 0.2f), concrete, true);
            Box("WallE", room, new Vector3(W + 0.1f, H / 2, 0), new Vector3(0.2f, H, 2 * W), concrete, true);
            Box("WallW", room, new Vector3(-W - 0.1f, H / 2, 0), new Vector3(0.2f, H, 2 * W), concrete, true);

            // Старая крашеная панель снизу стен — пыльный зелёный.
            float ph = 1.0f;
            Box("PanelN", room, new Vector3(0, ph / 2, W - 0.005f), new Vector3(2 * W, ph, 0.01f), stripe, false);
            Box("PanelS", room, new Vector3(0, ph / 2, -W + 0.005f), new Vector3(2 * W, ph, 0.01f), stripe, false);
            Box("PanelE", room, new Vector3(W - 0.005f, ph / 2, 0), new Vector3(0.01f, ph, 2 * W), stripe, false);
            Box("PanelW", room, new Vector3(-W + 0.005f, ph / 2, 0), new Vector3(0.01f, ph, 2 * W), stripe, false);
            var trim = Mat("Trim", "#2F2B27", 0.1f);
            Box("TrimN", room, new Vector3(0, ph, W - 0.012f), new Vector3(2 * W, 0.03f, 0.025f), trim, false);
            Box("TrimS", room, new Vector3(0, ph, -W + 0.012f), new Vector3(2 * W, 0.03f, 0.025f), trim, false);
            Box("TrimE", room, new Vector3(W - 0.012f, ph, 0), new Vector3(0.025f, 0.03f, 2 * W), trim, false);
            Box("TrimW", room, new Vector3(-W + 0.012f, ph, 0), new Vector3(0.025f, 0.03f, 2 * W), trim, false);
            // Плинтусы.
            Box("PlinthN", room, new Vector3(0, 0.045f, W - 0.02f), new Vector3(2 * W - 0.06f, 0.09f, 0.022f), trim, false);
            Box("PlinthS", room, new Vector3(0, 0.045f, -W + 0.02f), new Vector3(2 * W - 0.06f, 0.09f, 0.022f), trim, false);
            Box("PlinthE", room, new Vector3(W - 0.02f, 0.045f, 0), new Vector3(0.022f, 0.09f, 2 * W - 0.06f), trim, false);
            Box("PlinthW", room, new Vector3(-W + 0.02f, 0.045f, 0), new Vector3(0.022f, 0.09f, 2 * W - 0.06f), trim, false);


            // Балки и трубы под потолком.
            var wood = Mat("BeamWood", "#4A3A2C", 0.05f);
            for (int i = -1; i <= 1; i++)
                Box("Beam" + i, room, new Vector3(i * 2.4f, H - 0.09f, 0), new Vector3(0.18f, 0.18f, 2 * W), wood, false);
            var pipe = Mat("Pipe", "#8A6A45", 0.35f);
            CylBetween("PipeA", room, new Vector3(-W, H - 0.28f, 3.6f), new Vector3(W, H - 0.28f, 3.6f), 0.05f, pipe);
            CylBetween("PipeB", room, new Vector3(-W, H - 0.42f, 3.75f), new Vector3(W, H - 0.42f, 3.75f), 0.035f, Mat("PipeGrey", "#5E5E58", 0.3f));
            CylBetween("PipeC", room, new Vector3(-3.3f, H - 0.28f, 3.6f), new Vector3(-3.3f, H - 0.28f, -3.0f), 0.05f, pipe);
            CylBetween("PipeDown", room, new Vector3(-3.3f, H - 0.28f, -3.0f), new Vector3(-3.3f, 1.12f, -3.0f), 0.05f, pipe);   // в котёл
            for (int i = 0; i < 6; i++)
                Box("Bracket" + i, room, new Vector3(-3.5f + i * 1.4f, H - 0.18f, 3.6f), new Vector3(0.03f, 0.22f, 0.03f), Mat("Iron", "#24211F", 0.4f), false);
        }

        // ---------- мебель и декор ----------

        static void Props(Transform room)
        {
            var green = Mat("TableGreen", "#5C6B5A", 0.25f);
            var metal = Mat("DarkMetal", "#2E2D2A", 0.4f);
            var woodLight = Mat("ChairWood", "#7A5E43", 0.1f);

            // Стол допроса.
            var table = new GameObject("Table").transform;
            table.SetParent(room, false);
            Box("Top", table, new Vector3(0, 0.75f, 0), new Vector3(1.3f, 0.04f, 0.8f), green, true);
            Box("Apron", table, new Vector3(0, 0.71f, 0), new Vector3(1.22f, 0.05f, 0.72f), metal, false);
            foreach (var p in new[] { new Vector2(-0.6f, -0.35f), new Vector2(0.6f, -0.35f), new Vector2(-0.6f, 0.35f), new Vector2(0.6f, 0.35f) })
                Box("Leg", table, new Vector3(p.x, 0.36f, p.y), new Vector3(0.04f, 0.72f, 0.04f), metal, false);
            // Пепельница и бумаги.
            Cyl("Ashtray", table, new Vector3(0.48f, 0.78f, 0.22f), new Vector3(0.12f, 0.012f, 0.12f), Mat("Glass", "#6F7A74", 0.7f));
            Box("Paper1", table, new Vector3(-0.15f, 0.772f, -0.18f), new Vector3(0.21f, 0.002f, 0.297f), Mat("Paper", "#CFC6B2", 0.05f), false, new Vector3(0, 12, 0));
            Box("Paper2", table, new Vector3(-0.1f, 0.774f, -0.16f), new Vector3(0.21f, 0.002f, 0.297f), Mat("Paper", "#CFC6B2", 0.05f), false, new Vector3(0, -7, 0));
            Box("Pen", table, new Vector3(0.02f, 0.777f, -0.12f), new Vector3(0.008f, 0.008f, 0.14f), metal, false, new Vector3(0, 30, 0));

            // Стул детектива — отодвинут.
            Chair("DetectiveChair", room, new Vector3(0.15f, 0, -0.95f), 15f, woodLight);
            room.Find("DetectiveChair").gameObject.AddComponent<Seat>();   // на него можно сесть

            // Верстак-вещдоки у восточной стены.
            var bench = new GameObject("Workbench").transform;
            bench.SetParent(room, false);
            bench.localPosition = new Vector3(3.55f, 0, 0.3f);
            var benchWood = Mat("BenchWood", "#5E4A37", 0.08f);
            Box("Top", bench, new Vector3(0, 0.88f, 0), new Vector3(0.75f, 0.05f, 2.0f), benchWood, true);
            Box("Shelf", bench, new Vector3(0, 0.25f, 0), new Vector3(0.7f, 0.03f, 1.9f), benchWood, false);
            foreach (var p in new[] { new Vector2(-0.32f, -0.95f), new Vector2(0.32f, -0.95f), new Vector2(-0.32f, 0.95f), new Vector2(0.32f, 0.95f) })
                Box("Leg", bench, new Vector3(p.x, 0.44f, p.y), new Vector3(0.06f, 0.88f, 0.06f), benchWood, false);
            DetectiveBoard(bench);
            Box("Briefcase", bench, new Vector3(-0.05f, 0.4f, 0.5f), new Vector3(0.45f, 0.3f, 0.12f), Mat("Leather", "#3B2A22", 0.25f), false, new Vector3(0, 90, 0));
            Box("Tray", bench, new Vector3(-0.05f, 0.91f, -0.15f), new Vector3(0.5f, 0.02f, 1.2f), Mat("TrayMetal", "#4A4C48", 0.4f), false);
            // Маленькая лампа над верстаком, чтобы вещдоки было видно.
            var benchLight = new GameObject("BenchLight").AddComponent<Light>();
            benchLight.transform.SetParent(bench, false);
            benchLight.transform.localPosition = new Vector3(-0.1f, 1.9f, -0.1f);
            benchLight.type = LightType.Point;
            benchLight.range = 2.2f;
            benchLight.intensity = 1.3f;
            benchLight.color = new Color(1f, 0.82f, 0.6f);
            // Отдельный свет на стену с инструментами — иначе в подвале её не разглядеть.
            var wallLight = new GameObject("WallLight").AddComponent<Light>();
            wallLight.transform.SetParent(bench, false);
            wallLight.transform.localPosition = new Vector3(-0.55f, 1.95f, 0f);
            wallLight.transform.localRotation = Quaternion.Euler(18f, 90f, 0f);
            wallLight.type = LightType.Spot;
            wallLight.spotAngle = 95f;
            wallLight.range = 2.6f;
            wallLight.intensity = 3.0f;
            wallLight.color = new Color(1f, 0.84f, 0.62f);

            // Стеллаж с банками у западной стены.
            var shelf = new GameObject("Shelves").transform;
            shelf.SetParent(room, false);
            shelf.localPosition = new Vector3(-3.7f, 0, 1.2f);
            var shelfWood = Mat("ShelfWood", "#54432F", 0.05f);
            for (int i = 0; i < 4; i++)
                Box("Board" + i, shelf, new Vector3(0, 0.3f + i * 0.5f, 0), new Vector3(0.45f, 0.03f, 1.6f), shelfWood, i == 0);
            foreach (var z in new[] { -0.78f, 0.78f })
                Box("Side", shelf, new Vector3(0, 1.05f, z), new Vector3(0.45f, 2.1f, 0.03f), shelfWood, true);
            var rng = new System.Random(4);
            string[] jarColors = { "#9C7B3C", "#7A4A36", "#5F7351", "#A88F5C", "#6E5A48" };
            for (int i = 1; i < 4; i++)
                for (int j = 0; j < 6; j++)
                {
                    if (rng.NextDouble() < 0.25) continue;
                    float z = -0.65f + j * 0.26f + (float)rng.NextDouble() * 0.05f;
                    float hgt = 0.12f + (float)rng.NextDouble() * 0.1f;
                    if (rng.NextDouble() < 0.6)
                        Cyl("Jar", shelf, new Vector3((float)rng.NextDouble() * 0.1f, 0.315f + i * 0.5f + hgt / 2, z), new Vector3(0.11f, hgt / 2, 0.11f), Mat("Jar" + j % 5, jarColors[(i + j) % 5], 0.6f));
                    else
                        Box("Box", shelf, new Vector3(0, 0.315f + i * 0.5f + hgt / 2, z), new Vector3(0.3f, hgt, 0.22f), Mat("Cardboard", "#8A6F4E", 0.02f), false, new Vector3(0, (float)rng.NextDouble() * 10f, 0));
                }

            Boiler(room, metal);

            // Старый матрас и ведро.
            Box("Mattress", room, new Vector3(2.6f, 0.08f, -3.1f), new Vector3(1.9f, 0.16f, 0.9f), Mat("Mattress", "#7E7462", 0.02f), true, new Vector3(0, 4, 0));
            Cyl("Bucket", room, new Vector3(1.3f, 0.17f, -3.5f), new Vector3(0.28f, 0.17f, 0.28f), Mat("Zinc", "#7D807A", 0.5f)).AddComponent<CapsuleCollider>();

            // Часы на северной стене, их видно из-за спины Белых.
            var clock = new GameObject("WallClock").transform;
            clock.SetParent(room, false);
            clock.localPosition = new Vector3(1.3f, 1.95f, W - 0.03f);
            var face = Cyl("Face", clock, Vector3.zero, new Vector3(0.36f, 0.015f, 0.36f), Mat("ClockFace", "#E2D8BF", 0.2f));
            face.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var rim = Cyl("Rim", clock, new Vector3(0, 0, 0.009f), new Vector3(0.4f, 0.02f, 0.4f), metal);   // перед ободка на 4 мм за циферблатом — иначе мерцали
            rim.transform.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Box("Mark" + i, clock, new Vector3(Mathf.Sin(a) * 0.15f, Mathf.Cos(a) * 0.15f, -0.018f), new Vector3(0.01f, i % 3 == 0 ? 0.035f : 0.02f, 0.004f), metal, false, new Vector3(0, 0, -i * 30f));
            }
            var hourPivot = new GameObject("HourHand").transform;
            hourPivot.SetParent(clock, false);
            hourPivot.localPosition = new Vector3(0, 0, -0.022f);
            Box("Hand", hourPivot, new Vector3(0, 0.045f, 0), new Vector3(0.014f, 0.09f, 0.004f), metal, false);
            var minPivot = new GameObject("MinuteHand").transform;
            minPivot.SetParent(clock, false);
            minPivot.localPosition = new Vector3(0, 0, -0.03f);   // стрелки не касаются друг друга
            Box("Hand", minPivot, new Vector3(0, 0.065f, 0), new Vector3(0.009f, 0.13f, 0.004f), Mat("ClockRed", "#8C3B2E", 0.2f), false);
            var wc = clock.gameObject.AddComponent<WallClock>();
            wc.hourHand = hourPivot;
            wc.minuteHand = minPivot;
            var tick = clock.gameObject.AddComponent<AudioSource>();
            tick.playOnAwake = false;
            tick.spatialBlend = 1f;
            tick.minDistance = 1f;
            tick.maxDistance = 9f;
            wc.tick = tick;

            // Окошко под потолком: оттуда к утру идёт рассвет.
            var win = new GameObject("Window").transform;
            win.SetParent(room, false);
            win.localPosition = new Vector3(W - 0.02f, 2.3f, -1.8f);
            // Окно у самой земли: ночное небо с крышами и травой, поверх — два слоя мерцающих звёзд и рассветная заливка.
            Box("Back", win, Vector3.zero, new Vector3(0.02f, 0.32f, 0.85f), Mat("WindowBack", "#0A0C12", 0.1f), false);
            var glass = WindowQuad(win, "Sky", -0.0112f, EmissiveTex("WindowSky", TexAsset("sky_night", false)));
            var starA = WindowQuad(win, "StarsA", -0.0135f, ParticleMat("StarsA", "Legacy Shaders/Particles/Additive", "stars_1"));
            var starB = WindowQuad(win, "StarsB", -0.0145f, ParticleMat("StarsB", "Legacy Shaders/Particles/Additive", "stars_2"));
            var dawnQ = WindowQuad(win, "Dawn", -0.0155f, ParticleMat("DawnWash", "Legacy Shaders/Particles/Additive", null));
            dawnQ.sharedMaterial.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0f));   // до рассвета не видно
            Box("FrameT", win, new Vector3(-0.02f, 0.18f, 0), new Vector3(0.05f, 0.04f, 0.93f), Mat("WindowFrame", "#3D3833", 0.1f), false);
            Box("FrameB", win, new Vector3(-0.02f, -0.18f, 0), new Vector3(0.05f, 0.04f, 0.93f), Mat("WindowFrame", "#3D3833", 0.1f), false);
            Box("Bar1", win, new Vector3(-0.03f, 0, -0.2f), new Vector3(0.02f, 0.34f, 0.02f), metal, false);
            Box("Bar2", win, new Vector3(-0.03f, 0, 0.2f), new Vector3(0.02f, 0.34f, 0.02f), metal, false);
            var spill = new GameObject("Spill").AddComponent<Light>();
            spill.transform.SetParent(win, false);
            spill.transform.localPosition = new Vector3(-0.1f, 0, 0);
            spill.transform.localRotation = Quaternion.Euler(35f, -90f, 0f);
            spill.type = LightType.Spot;
            spill.spotAngle = 70f;
            spill.range = 6f;
            spill.intensity = 0.4f;
            var mw = win.gameObject.AddComponent<MorningWindow>();
            mw.glass = glass;
            mw.stars = new[] { starA, starB };
            mw.dawn = dawnQ;
            mw.spill = spill;
        }

        static Texture2D TexAsset(string file, bool alpha)
        {
            string path = Root + "/Textures/" + file + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Clamp || imp.alphaIsTransparency != alpha || imp.npotScale != TextureImporterNPOTScale.None))
            {
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.alphaIsTransparency = alpha;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Материал частиц (аддитивный или полупрозрачный): звёзды, рассвет, паутина. Шейдер без отсечения граней.
        static Material ParticleMat(string name, string shader, string file)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            m.mainTexture = file != null ? TexAsset(file, true) : null;
            m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // Плоскость на окне (смотрит в комнату, на −x); depth — насколько перед задником.
        static Renderer WindowQuad(Transform win, string name, float depth, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = name;
            q.transform.SetParent(win, false);
            q.transform.localPosition = new Vector3(depth, 0f, 0f);
            q.transform.localRotation = Quaternion.Euler(0, 90, 0);
            q.transform.localScale = new Vector3(0.85f, 0.32f, 1f);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            return r;
        }

        static GameObject Sphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // Газовый котёл (как советский АГВ) в юго-западном углу: эмалированный бак, автоматика с запальником,
        // жёлтая газовая труба от счётчика на стене, дымоход в стену. Лицом к середине комнаты (+x котла).
        static void Boiler(Transform room, Material metal)
        {
            var boiler = new GameObject("Boiler").transform;
            boiler.SetParent(room, false);
            boiler.localPosition = new Vector3(-3.3f, 0, -3.1f);
            boiler.localRotation = Quaternion.Euler(0, -43f, 0);
            var enamel = Mat("Enamel", "#D3CBBA", 0.55f);
            var enamelDark = Mat("EnamelDark", "#A9A192", 0.45f);
            var zinc = Mat("Zinc", "#7D807A", 0.5f);
            var grey = Mat("AutoGrey", "#6F726C", 0.35f);
            var gas = Mat("GasYellow", "#C49A22", 0.45f);

            Box("Sheet", boiler, new Vector3(0, 0.003f, 0), new Vector3(0.72f, 0.006f, 0.72f), zinc, false);
            Cyl("Base", boiler, new Vector3(0, 0.08f, 0), new Vector3(0.48f, 0.075f, 0.48f), enamelDark);
            var body = Cyl("Body", boiler, new Vector3(0, 0.6f, 0), new Vector3(0.5f, 0.45f, 0.5f), enamel);
            body.AddComponent<CapsuleCollider>();
            Cyl("Lid", boiler, new Vector3(0, 1.058f, 0), new Vector3(0.515f, 0.012f, 0.515f), enamelDark);
            Cyl("Hood", boiler, new Vector3(-0.03f, 1.12f, -0.05f), new Vector3(0.22f, 0.05f, 0.22f), zinc);
            Cyl("HoodSkirt", boiler, new Vector3(-0.03f, 1.08f, -0.05f), new Vector3(0.3f, 0.012f, 0.3f), zinc);
            // Шов и табличка спереди.
            Cyl("Seam", boiler, new Vector3(0, 0.42f, 0), new Vector3(0.505f, 0.006f, 0.505f), enamelDark);
            Box("Badge", boiler, new Vector3(0.247f, 0.82f, 0), new Vector3(0.006f, 0.05f, 0.12f), Mat("BadgeRed", "#8C2F28", 0.4f), false);
            Box("BadgeText", boiler, new Vector3(0.2505f, 0.82f, 0), new Vector3(0.002f, 0.012f, 0.08f), Mat("Brass", "#A88A4A", 0.5f), false);
            // Термометр на баке.
            var dial = Cyl("TempRim", boiler, new Vector3(0.246f, 0.66f, 0.06f), new Vector3(0.09f, 0.008f, 0.09f), Mat("Brass", "#A88A4A", 0.5f));
            dial.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var face = Cyl("TempFace", boiler, new Vector3(0.251f, 0.66f, 0.06f), new Vector3(0.072f, 0.004f, 0.072f), Mat("GaugeFace", "#D8CFB8", 0.3f));
            face.transform.localRotation = Quaternion.Euler(0, 0, 90);
            Box("TempNeedle", boiler, new Vector3(0.2545f, 0.668f, 0.064f), new Vector3(0.002f, 0.028f, 0.003f), Mat("ClockRed", "#8C3B2E", 0.2f), false, new Vector3(-40, 0, 0));
            // Окно топки с синим огоньком запальника и блок автоматики с кнопкой и ручкой.
            Box("Panel", boiler, new Vector3(0.243f, 0.27f, 0), new Vector3(0.012f, 0.2f, 0.2f), Mat("PanelDark", "#2D2C2A", 0.3f), false);
            var flame = Box("PilotWindow", boiler, new Vector3(0.2505f, 0.24f, 0), new Vector3(0.003f, 0.035f, 0.07f), Emissive("PilotFlame", "#3E7BFF", 1.6f), false);
            Box("Auto", boiler, new Vector3(0.3f, 0.36f, -0.06f), new Vector3(0.09f, 0.1f, 0.08f), grey, false);
            var btn = Cyl("Button", boiler, new Vector3(0.348f, 0.38f, -0.08f), new Vector3(0.024f, 0.006f, 0.024f), Mat("ClockRed", "#8C3B2E", 0.2f));
            btn.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var knob = Cyl("Knob", boiler, new Vector3(0.352f, 0.34f, -0.04f), new Vector3(0.04f, 0.01f, 0.04f), Mat("Coal", "#151312", 0.55f));
            knob.transform.localRotation = Quaternion.Euler(0, 0, 90);
            CylBetween("Thermocouple", boiler, new Vector3(0.3f, 0.31f, -0.02f), new Vector3(0.25f, 0.26f, 0.02f), 0.004f, Mat("Copper", "#9A6440", 0.5f));

            var flicker = boiler.gameObject.AddComponent<EmberFlicker>();
            var pilot = new GameObject("PilotLight").AddComponent<Light>();
            pilot.transform.SetParent(boiler, false);
            pilot.transform.localPosition = new Vector3(0.36f, 0.25f, 0);
            pilot.type = LightType.Point;
            pilot.range = 0.7f;
            pilot.intensity = 0.45f;
            pilot.color = new Color(0.35f, 0.55f, 1f);
            flicker.glow = pilot;
            flicker.embers = new[] { flame.GetComponent<Renderer>() };
            flicker.emberColor = new Color(0.25f, 0.48f, 1f);

            // Дымоход: оцинкованная труба из колпака вверх и в западную стену.
            var top = boiler.TransformPoint(new Vector3(-0.03f, 1.145f, -0.05f)) - room.position;
            CylBetween("Flue1", room, top, new Vector3(top.x, 2.15f, top.z), 0.065f, zinc);
            CylBetween("Flue2", room, new Vector3(top.x, 2.15f, top.z), new Vector3(-W, 2.15f, top.z), 0.065f, zinc);
            Sphere("FlueElbow", room, new Vector3(top.x, 2.15f, top.z), Vector3.one * 0.13f, zinc);
            Cyl("FlueRosette", room, new Vector3(-W + 0.01f, 2.15f, top.z), new Vector3(0.2f, 0.01f, 0.2f), zinc).transform.localRotation = Quaternion.Euler(0, 0, 90);
            // Обратка в южную стену.
            var side = boiler.TransformPoint(new Vector3(-0.05f, 0.9f, -0.26f)) - room.position;
            CylBetween("Return", room, side, new Vector3(side.x, side.y, -W), 0.025f, Mat("Pipe", "#8A6A45", 0.35f));

            // Газ: счётчик на западной стене, жёлтая труба вниз к автоматике, кран-бабочка.
            var meter = new Vector3(-W + 0.09f, 1.55f, -2.3f);
            Box("GasMeter", room, meter, new Vector3(0.16f, 0.26f, 0.22f), Mat("MeterGrey", "#8C8E86", 0.35f), false);
            Box("MeterWindow", room, meter + new Vector3(0.081f, 0.05f, 0), new Vector3(0.004f, 0.05f, 0.13f), Mat("MeterGlass", "#1D1F1E", 0.8f), false);
            Box("MeterDigits", room, meter + new Vector3(0.0835f, 0.05f, 0), new Vector3(0.002f, 0.016f, 0.1f), Mat("GaugeFace", "#D8CFB8", 0.3f), false);
            Box("MeterSeal", room, meter + new Vector3(0.082f, -0.08f, 0.06f), new Vector3(0.004f, 0.02f, 0.02f), Mat("ClockRed", "#8C3B2E", 0.2f), false);
            CylBetween("GasIn", room, new Vector3(-W + 0.06f, H, -2.38f), meter + new Vector3(-0.03f, 0.13f, -0.08f), 0.016f, gas);
            var auto = boiler.TransformPoint(new Vector3(0.3f, 0.36f, -0.1f)) - room.position;
            var down = meter + new Vector3(-0.03f, -0.13f, 0.08f);
            var corner1 = new Vector3(down.x, auto.y, down.z);
            CylBetween("GasDown", room, down, corner1, 0.016f, gas);
            CylBetween("GasRun", room, corner1, new Vector3(auto.x, auto.y, down.z), 0.016f, gas);
            CylBetween("GasIntoAuto", room, new Vector3(auto.x, auto.y, down.z), auto, 0.016f, gas);
            Sphere("GasElbow1", room, corner1, Vector3.one * 0.04f, gas);
            Sphere("GasElbow2", room, new Vector3(auto.x, auto.y, down.z), Vector3.one * 0.04f, gas);
            var valve = Vector3.Lerp(down, corner1, 0.35f);
            Box("ValveBody", room, valve, new Vector3(0.045f, 0.06f, 0.045f), Mat("Brass", "#A88A4A", 0.5f), false);
            Box("ValveLever", room, valve + new Vector3(0.05f, 0, 0), new Vector3(0.09f, 0.014f, 0.022f), Mat("ClockRed", "#8C3B2E", 0.2f), false);
        }

        // Мелочи, чтобы подвал выглядел обжитым: буфет с радио, газеты, шины, бочка, краска, метла, календарь, паутина.
        static void Clutter(Transform room)
        {
            var iron = Mat("Iron", "#24211F", 0.4f);
            var wood = Mat("ShelfWood", "#54432F", 0.05f);
            var woodDark = Mat("CupboardWood", "#4A3626", 0.12f);

            // Старый буфет в северо-восточном углу, на нём радиоприёмник и книги.
            var cup = new GameObject("Cupboard").transform;
            cup.SetParent(room, false);
            cup.localPosition = new Vector3(W - 0.27f, 0, 3.2f);
            Box("Body", cup, new Vector3(0, 0.78f, 0), new Vector3(0.5f, 1.44f, 1.0f), woodDark, true);
            Box("Top", cup, new Vector3(-0.01f, 1.515f, 0), new Vector3(0.55f, 0.03f, 1.06f), woodDark, false);
            Box("Base", cup, new Vector3(-0.01f, 0.04f, 0), new Vector3(0.53f, 0.08f, 1.03f), Mat("Trim", "#2F2B27", 0.1f), false);
            foreach (float z in new[] { -0.245f, 0.245f })
            {
                Box("Door", cup, new Vector3(-0.255f, 0.95f, z), new Vector3(0.012f, 0.9f, 0.46f), wood, false);
                Box("Inset", cup, new Vector3(-0.262f, 0.95f, z), new Vector3(0.004f, 0.74f, 0.36f), woodDark, false);
                Box("Knob", cup, new Vector3(-0.272f, 0.95f, z * 0.16f), new Vector3(0.02f, 0.03f, 0.02f), Mat("Brass", "#A88A4A", 0.5f), false);
            }
            Box("Drawer", cup, new Vector3(-0.255f, 0.3f, 0), new Vector3(0.012f, 0.26f, 0.94f), wood, false);
            Box("DrawerPull", cup, new Vector3(-0.268f, 0.3f, 0), new Vector3(0.016f, 0.025f, 0.16f), Mat("Brass", "#A88A4A", 0.5f), false);
            var radio = new GameObject("Radio").transform;
            radio.SetParent(cup, false);
            radio.localPosition = new Vector3(-0.03f, 1.53f, 0.18f);
            radio.localRotation = Quaternion.Euler(0, -12, 0);
            Box("Case", radio, new Vector3(0, 0.12f, 0), new Vector3(0.2f, 0.24f, 0.42f), Mat("RadioWood", "#6A4A30", 0.35f), false);
            Box("Grille", radio, new Vector3(-0.101f, 0.15f, -0.08f), new Vector3(0.004f, 0.15f, 0.2f), Mat("Grille", "#3A3128", 0.05f), false);
            Box("Dial", radio, new Vector3(-0.101f, 0.15f, 0.12f), new Vector3(0.004f, 0.08f, 0.1f), Emissive("RadioDial", "#C89A4A", 0.35f), false);
            foreach (float z in new[] { 0.09f, 0.15f })
                Cyl("Knob", radio, new Vector3(-0.105f, 0.05f, z), new Vector3(0.03f, 0.01f, 0.03f), iron).transform.localRotation = Quaternion.Euler(0, 0, 90);
            CylBetween("Antenna", radio, new Vector3(0.05f, 0.24f, -0.15f), new Vector3(0.12f, 0.6f, -0.25f), 0.003f, iron);
            string[] bookColors = { "#5A2E28", "#2F3E4A", "#4B4A2A", "#3B2E3E" };
            for (int i = 0; i < 4; i++)
                Box("Book" + i, cup, new Vector3(-0.02f, 1.545f + i * 0.035f, -0.28f), new Vector3(0.2f, 0.033f, 0.28f - i * 0.02f), Mat("Book" + i, bookColors[i], 0.1f), false, new Vector3(0, i * 7f - 8f, 0));

            // Стопка газет у буфета и одна — на полу у ковра.
            var paperTop = Textured("Newspaper", TexAsset("newspaper", false));
            var paperSide = Mat("PaperStack", "#BDB4A0", 0.05f);
            for (int i = 0; i < 9; i++)
                Box("Paper" + i, room, new Vector3(W - 0.35f, 0.012f + i * 0.024f, 2.35f), new Vector3(0.36f, 0.022f, 0.46f), i == 8 ? paperTop : paperSide, false, new Vector3(0, (i * 37 % 11) - 5f, 0));
            Box("Twine", room, new Vector3(W - 0.35f, 0.12f, 2.35f), new Vector3(0.37f, 0.225f, 0.008f), Mat("Twine", "#8A7550", 0.05f), false);
            Box("FloorPaper", room, new Vector3(1.55f, 0.0105f, -1.55f), new Vector3(0.36f, 0.002f, 0.48f), paperTop, false, new Vector3(0, 28, 0));

            // Шины и бочка у западной стены.
            var rubber = Mat("Rubber", "#1A1918", 0.25f);
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(-W + 0.38f + (i == 2 ? 0.03f : 0f), 0.1f + i * 0.2f, -1.95f + (i == 1 ? 0.04f : 0f));
                Cyl("Tire" + i, room, c, new Vector3(0.62f, 0.095f, 0.62f), rubber);
                Cyl("Rim" + i, room, c, new Vector3(0.36f, 0.097f, 0.36f), Mat("TireHole", "#0B0A0A", 0.05f));
            }
            var barrel = new Vector3(-W + 0.35f, 0, -0.9f);
            Cyl("Barrel", room, barrel + new Vector3(0, 0.44f, 0), new Vector3(0.56f, 0.44f, 0.56f), Mat("BarrelBlue", "#34474F", 0.3f)).AddComponent<CapsuleCollider>();
            foreach (float y in new[] { 0.2f, 0.68f })
                Cyl("Hoop", room, barrel + new Vector3(0, y, 0), new Vector3(0.575f, 0.02f, 0.575f), Mat("Rust", "#6A4632", 0.1f));
            Cyl("Lid", room, barrel + new Vector3(0, 0.885f, 0), new Vector3(0.5f, 0.006f, 0.5f), Mat("Rust", "#6A4632", 0.1f));

            // Банки с краской и метла у южной стены.
            string[] paint = { "#7E7A70", "#6E3A2E", "#8C8456" };
            for (int i = 0; i < 3; i++)
            {
                var c = new Vector3(-0.75f + i * 0.24f, 0, -W + 0.2f + (i == 1 ? 0.06f : 0f));
                float h = i == 2 ? 0.14f : 0.2f;
                Cyl("Can" + i, room, c + new Vector3(0, h / 2, 0), new Vector3(0.18f, h / 2, 0.18f), Mat("Paint" + i, paint[i], 0.4f));
                Cyl("Drip" + i, room, c + new Vector3(0, h + 0.002f, 0), new Vector3(0.16f, 0.002f, 0.16f), Mat("PaintDrip" + i, paint[(i + 1) % 3], 0.5f));
            }
            CylBetween("Broom", room, new Vector3(0.62f, 0.2f, -W + 0.2f), new Vector3(0.72f, 1.42f, -W + 0.04f), 0.014f, Mat("BenchWood", "#5E4A37", 0.08f));
            Box("Bristles", room, new Vector3(0.61f, 0.1f, -W + 0.22f), new Vector3(0.28f, 0.2f, 0.06f), Mat("Bristle", "#8E7A55", 0.02f), false, new Vector3(-8, 0, 4));

            // Календарь на северной стене.
            var cal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(cal.GetComponent<Collider>());
            cal.name = "Calendar";
            cal.transform.SetParent(room, false);
            cal.transform.localPosition = new Vector3(2.45f, 1.55f, W - 0.012f);
            cal.transform.localScale = new Vector3(0.32f, 0.45f, 1f);
            cal.GetComponent<Renderer>().sharedMaterial = Textured("Calendar", TexAsset("calendar", false));
            Box("Nail", room, new Vector3(2.45f, 1.79f, W - 0.02f), new Vector3(0.008f, 0.008f, 0.02f), iron, false);

            // Паутина в углах под потолком.
            var web = ParticleMat("Cobweb", "Legacy Shaders/Particles/Alpha Blended", "cobweb");
            void Web(string name, Vector3 corner, float yaw, Vector3 inward)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = name;
                q.transform.SetParent(room, false);
                q.transform.localPosition = corner + inward * 0.29f + Vector3.down * 0.41f;
                q.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                q.transform.localScale = new Vector3(0.82f, 0.82f, 1f);
                q.GetComponent<Renderer>().sharedMaterial = web;
            }
            Web("WebNE", new Vector3(W, H, W), 45f, new Vector3(-1, 0, -1));
            Web("WebSE", new Vector3(W, H, -W), 135f, new Vector3(-1, 0, 1));
            Web("WebNW", new Vector3(-W, H, W), -45f, new Vector3(1, 0, -1));
            Web("WebSW", new Vector3(-W, H, -W), -135f, new Vector3(1, 0, 1));
        }

        // Материал с картинкой из Textures/ (тайлится): tiling — сколько раз картинка ложится на грань куба.
        static Material Surface(string name, string file, Vector2 tiling, string tint, float smooth)
        {
            string path = Root + "/Textures/" + file + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.wrapMode != TextureWrapMode.Repeat || imp.maxTextureSize != 1024 || imp.anisoLevel < 8))
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.maxTextureSize = 1024;
                imp.anisoLevel = 8;
                imp.SaveAndReimport();
            }
            var m = Mat(name, tint, smooth);
            m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            m.mainTextureScale = tiling;
            EditorUtility.SetDirty(m);
            return m;
        }

        // Настенный светильник в решётке: тёплая лампочка и точечный свет без теней.
        static void CageLamp(Transform room, string name, Vector3 pos, Vector3 outward, float intensity)
        {
            var lamp = new GameObject(name).transform;
            lamp.SetParent(room, false);
            lamp.localPosition = pos;
            lamp.localRotation = Quaternion.LookRotation(outward);
            var iron = Mat("Iron", "#24211F", 0.4f);
            Box("Plate", lamp, new Vector3(0, 0, 0.012f), new Vector3(0.12f, 0.16f, 0.024f), iron, false);
            var glass = Cyl("Glass", lamp, new Vector3(0, 0, 0.085f), new Vector3(0.1f, 0.06f, 0.1f), Emissive("CageBulb", "#FFD9A0", 1.6f));
            glass.transform.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 4; i++)
            {
                var bar = Box("Bar" + i, lamp, Vector3.zero, new Vector3(0.008f, 0.008f, 0.13f), iron, false);
                float a = i * 90f * Mathf.Deg2Rad;
                bar.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.058f, Mathf.Sin(a) * 0.058f, 0.085f);
            }
            var ring = Cyl("Ring", lamp, new Vector3(0, 0, 0.152f), new Vector3(0.124f, 0.004f, 0.124f), iron);
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(lamp, false);
            l.transform.localPosition = new Vector3(0, 0, 0.2f);
            l.type = LightType.Point;
            l.range = 4.2f;
            l.intensity = intensity;
            l.color = new Color(1f, 0.8f, 0.56f);
        }

        // Детали, которых не хватало: дверь вместо лестницы, ковёр, батарея, щиток с проводкой, ящики, вешалка, светильники.
        static void Details(Transform room)
        {
            var iron = Mat("Iron", "#24211F", 0.4f);
            var steel = Mat("DoorSteel", "#4E4F4A", 0.3f);
            var rust = Mat("Rust", "#6A4632", 0.1f);

            // Железная дверь в северной стене — через неё сюда и попадают.
            var door = new GameObject("Door").transform;
            door.SetParent(room, false);
            door.localPosition = new Vector3(-1.6f, 0, W);
            Box("FrameL", door, new Vector3(-0.52f, 1.06f, -0.04f), new Vector3(0.07f, 2.12f, 0.08f), steel, false);
            Box("FrameR", door, new Vector3(0.52f, 1.06f, -0.04f), new Vector3(0.07f, 2.12f, 0.08f), steel, false);
            Box("FrameT", door, new Vector3(0, 2.155f, -0.04f), new Vector3(1.11f, 0.07f, 0.08f), steel, false);
            Box("Slab", door, new Vector3(0, 1.05f, -0.03f), new Vector3(0.96f, 2.08f, 0.04f), steel, true);
            Box("PanelTop", door, new Vector3(0, 1.55f, -0.053f), new Vector3(0.74f, 0.62f, 0.008f), Mat("DoorPanel", "#45463F", 0.25f), false);
            Box("PanelBottom", door, new Vector3(0, 0.55f, -0.053f), new Vector3(0.74f, 0.62f, 0.008f), Mat("DoorPanel", "#45463F", 0.25f), false);
            Box("RustEdge", door, new Vector3(0, 0.06f, -0.0525f), new Vector3(0.9f, 0.1f, 0.006f), rust, false);
            Box("Handle", door, new Vector3(0.38f, 1.0f, -0.085f), new Vector3(0.03f, 0.18f, 0.03f), iron, false);
            Box("HandleBase", door, new Vector3(0.38f, 1.0f, -0.058f), new Vector3(0.05f, 0.24f, 0.012f), iron, false);
            Box("Bolt", door, new Vector3(0.28f, 1.25f, -0.064f), new Vector3(0.22f, 0.03f, 0.026f), iron, false);
            foreach (float y in new[] { 0.35f, 1.75f })
                Box("Hinge", door, new Vector3(-0.49f, y, -0.087f), new Vector3(0.04f, 0.14f, 0.012f), iron, false);
            Box("Mat", door, new Vector3(0, 0.006f, -0.45f), new Vector3(0.9f, 0.012f, 0.55f), Mat("DoorMat", "#3D372F", 0.02f), false);
            CageLamp(room, "DoorLamp", new Vector3(-2.3f, 2.0f, W - 0.01f), Vector3.back, 0.3f);   // сбоку от двери: над ней идут трубы

            // Вешалка с пальто и шляпой у двери.
            var coat = new GameObject("CoatHook").transform;
            coat.SetParent(room, false);
            coat.localPosition = new Vector3(-0.55f, 0, W);
            Box("Rail", coat, new Vector3(0, 1.78f, -0.02f), new Vector3(0.6f, 0.06f, 0.03f), Mat("BenchWood", "#5E4A37", 0.08f), false);
            foreach (float x in new[] { -0.2f, 0f, 0.2f })
                Box("Hook", coat, new Vector3(x, 1.76f, -0.06f), new Vector3(0.02f, 0.02f, 0.06f), iron, false);
            var coatMat = Mat("Coat", "#3E3830", 0.03f);
            Box("CoatTop", coat, new Vector3(-0.2f, 1.58f, -0.1f), new Vector3(0.38f, 0.32f, 0.1f), coatMat, false);
            Box("CoatBody", coat, new Vector3(-0.2f, 1.12f, -0.11f), new Vector3(0.44f, 0.62f, 0.12f), coatMat, false, new Vector3(0, 0, 1.5f));
            Box("Collar", coat, new Vector3(-0.2f, 1.72f, -0.115f), new Vector3(0.2f, 0.06f, 0.07f), Mat("CoatDark", "#2E2924", 0.03f), false);
            Box("Belt", coat, new Vector3(-0.2f, 1.2f, -0.175f), new Vector3(0.42f, 0.04f, 0.012f), Mat("CoatDark", "#2E2924", 0.03f), false);
            var hat = Cyl("HatBrim", coat, new Vector3(0.2f, 1.68f, -0.13f), new Vector3(0.3f, 0.008f, 0.3f), Mat("Hat", "#2F2A25", 0.1f));
            hat.transform.localRotation = Quaternion.Euler(-70f, 0, 0);
            var crown = Cyl("HatCrown", coat, new Vector3(0.2f, 1.71f, -0.145f), new Vector3(0.18f, 0.05f, 0.18f), Mat("Hat", "#2F2A25", 0.1f));
            crown.transform.localRotation = Quaternion.Euler(-70f, 0, 0);

            // Ящики на поддоне в углу, где была лестница.
            var crateMat = Surface("Crate", "tex_crate", new Vector2(1f, 1f), "#C8B8A0", 0.05f);
            var pallet = new GameObject("Pallet").transform;
            pallet.SetParent(room, false);
            pallet.localPosition = new Vector3(-3.25f, 0, 3.3f);
            for (int i = 0; i < 5; i++)
                Box("Slat" + i, pallet, new Vector3(-0.5f + i * 0.25f, 0.13f, 0), new Vector3(0.14f, 0.022f, 1.0f), crateMat, false);
            foreach (float z in new[] { -0.42f, 0f, 0.42f })
                Box("Block", pallet, new Vector3(0, 0.06f, z), new Vector3(1.2f, 0.115f, 0.1f), Mat("ShelfWood", "#54432F", 0.05f), false);
            Box("CrateA", pallet, new Vector3(-0.25f, 0.45f, -0.1f), new Vector3(0.62f, 0.6f, 0.62f), crateMat, true, new Vector3(0, 6, 0));
            Box("CrateB", pallet, new Vector3(0.36f, 0.37f, 0.15f), new Vector3(0.5f, 0.44f, 0.5f), crateMat, true, new Vector3(0, -9, 0));
            Box("CrateC", pallet, new Vector3(-0.2f, 0.98f, -0.05f), new Vector3(0.45f, 0.44f, 0.45f), crateMat, false, new Vector3(0, 19, 0));
            Box("Sack", pallet, new Vector3(0.35f, 0.71f, 0.12f), new Vector3(0.42f, 0.22f, 0.32f), Mat("Sack", "#8C7A5C", 0.02f), false, new Vector3(4, -14, 6));

            // Потёртый ковёр под столом.
            Box("Rug", room, new Vector3(0, 0.0045f, -0.05f), new Vector3(2.6f, 0.007f, 2.4f), Surface("Rug", "tex_rug", Vector2.one, "#E8E0D6", 0.02f), false);

            // Батарея под окном.
            var rad = new GameObject("Radiator").transform;
            rad.SetParent(room, false);
            rad.localPosition = new Vector3(W - 0.1f, 0, -1.8f);
            var radMat = Mat("Radiator", "#8E8676", 0.3f);
            for (int i = 0; i < 11; i++)
                Box("Fin" + i, rad, new Vector3(0, 0.48f, -0.4f + i * 0.08f), new Vector3(0.09f, 0.58f, 0.055f), radMat, false);
            CylBetween("PipeT", rad, new Vector3(0, 0.74f, -0.47f), new Vector3(0, 0.74f, 0.47f), 0.015f, radMat);
            CylBetween("PipeB", rad, new Vector3(0, 0.22f, -0.47f), new Vector3(0, 0.22f, 0.47f), 0.015f, radMat);
            CylBetween("Feed", rad, new Vector3(0.02f, 0.22f, 0.47f), new Vector3(0.02f, 0.02f, 0.47f), 0.012f, Mat("Pipe", "#8A6A45", 0.35f));

            // Электрощиток на южной стене и проводка от лампы по потолку.
            var box = new GameObject("FuseBox").transform;
            box.SetParent(room, false);
            box.localPosition = new Vector3(1.4f, 1.55f, -W);
            Box("Case", box, new Vector3(0, 0, 0.06f), new Vector3(0.36f, 0.46f, 0.1f), Mat("FuseGrey", "#6B6C64", 0.2f), false);
            Box("Lid", box, new Vector3(0, 0, 0.114f), new Vector3(0.32f, 0.42f, 0.008f), Mat("FuseLid", "#7A7B72", 0.25f), false);
            Box("Warn", box, new Vector3(0, 0.12f, 0.12f), new Vector3(0.08f, 0.07f, 0.004f), Mat("Warn", "#C9A13A", 0.2f), false);
            Box("Latch", box, new Vector3(0.13f, -0.02f, 0.124f), new Vector3(0.02f, 0.06f, 0.012f), iron, false);
            var cable = Mat("Cord", "#1B1A18", 0.2f);
            CylBetween("Conduit", box, new Vector3(0, 0.23f, 0.05f), new Vector3(0, H - 1.55f - 0.02f, 0.05f), 0.014f, Mat("FuseGrey", "#6B6C64", 0.2f));
            float cy = H - 0.016f;
            CylBetween("Cable1", room, new Vector3(0f, cy, 0.05f), new Vector3(0.15f, cy, 0.05f), 0.007f, cable);
            CylBetween("Cable2", room, new Vector3(0.15f, cy, 0.05f), new Vector3(0.15f, cy, -W + 0.05f), 0.007f, cable);
            CylBetween("Cable3", room, new Vector3(0.15f, cy, -W + 0.05f), new Vector3(1.4f, cy, -W + 0.05f), 0.007f, cable);
            for (int i = 0; i < 8; i++)
                Box("Clip" + i, room, new Vector3(0.15f, H - 0.006f, -0.4f - i * 0.48f), new Vector3(0.025f, 0.012f, 0.012f), iron, false);

            Clutter(room);
        }

        // ---------- детективная доска над верстаком ----------

        static Texture2D BoardPhoto(string name) { return PhotoTex("board_" + name); }

        // Картинка из Textures/ без подгонки под степень двойки (фото, сгенерированные нейросетью).
        static Texture2D PhotoTex(string file)
        {
            string path = Root + "/Textures/" + file + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.npotScale != TextureImporterNPOTScale.None || imp.maxTextureSize < 512))
            {
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 512;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Квадрат на пробке: pivot повёрнут (наклон бумаги), внутри quad лицом в комнату. u — вправо по экрану, v — высота.
        static Transform BoardPaper(Transform wall, string name, float u, float v, float w, float h, float roll, Material m,
            string title, string body, Texture2D photo, Color? pinColor = null)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(wall, false);
            pivot.localPosition = new Vector3(0.3475f, v, -u);
            pivot.localRotation = Quaternion.Euler(roll, 0, 0);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Sheet";
            q.transform.SetParent(pivot, false);
            q.transform.localRotation = Quaternion.Euler(0, 90, 0);
            q.transform.localScale = new Vector3(w, h, 1f);
            q.GetComponent<Renderer>().sharedMaterial = m;
            // тень под бумагой — тёмная подложка, чуть смещённая вниз-вправо
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.name = "Shadow";
            sh.transform.SetParent(pivot, false);
            sh.transform.localPosition = new Vector3(0.0015f, -0.006f, -0.005f);
            sh.transform.localRotation = Quaternion.Euler(0, 90, 0);
            sh.transform.localScale = new Vector3(w + 0.004f, h + 0.004f, 1f);
            sh.GetComponent<Renderer>().sharedMaterial = Mat("PaperShadow", "#2A1E12", 0.0f);
            // булавка сверху
            Pin(wall, pivot, u, v + h * 0.5f - 0.014f, pinColor ?? Hex("#B3302A"));
            var col = pivot.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.06f, h + 0.03f, w + 0.03f);
            col.center = new Vector3(-0.025f, 0, 0);
            col.isTrigger = true;
            var item = pivot.gameObject.AddComponent<BoardItem>();
            item.title = title; item.body = body; item.photo = photo;
            return pivot;
        }

        static void Pin(Transform wall, Transform owner, float u, float v, Color c)
        {
            var head = Cyl("PinHead", wall, new Vector3(0.337f, v, -u), new Vector3(0.017f, 0.003f, 0.017f), MatColor("Pin" + ColorUtility.ToHtmlStringRGB(c), c, 0.7f));
            head.transform.localRotation = Quaternion.Euler(0, 0, 90);
            var needle = Cyl("PinNeedle", wall, new Vector3(0.343f, v, -u), new Vector3(0.003f, 0.007f, 0.003f), Mat("PinSteel", "#BFC3C7", 0.8f));
            needle.transform.localRotation = Quaternion.Euler(0, 0, 90);
        }

        static Material MatColor(string name, Color c, float smooth)
        {
            var m = Mat(name, "#FFFFFF", smooth);
            m.color = c;
            return m;
        }

        // Красная нитка между двумя булавками; чуть разная глубина, чтобы пересечения не мерцали.
        static int stringN;
        static void Thread(Transform wall, float u1, float v1, float u2, float v2)
        {
            float dx = 0.3395f + (stringN++ % 5) * 0.0011f;
            var a = new Vector3(dx, v1, -u1);
            var b = new Vector3(dx, v2, -u2);
            // лёгкое провисание: две половинки через точку чуть ниже середины
            var mid = (a + b) * 0.5f + new Vector3(0, -0.012f, 0);
            var red = Mat("Thread", "#A6261F", 0.1f);
            CylBetween("Thread", wall, a, mid, 0.0013f, red);
            CylBetween("Thread", wall, mid, b, 0.0013f, red);
        }

        static void DetectiveBoard(Transform bench)
        {
            var wall = new GameObject("DetectiveBoard").transform;
            wall.SetParent(bench, false);

            // Пробковая доска в деревянной раме.
            var cork = Textured("Cork", CorkTexture());
            cork.mainTextureScale = new Vector2(7f, 3.5f);
            Box("Cork", wall, new Vector3(0.36f, 1.55f, 0), new Vector3(0.02f, 0.9f, 1.8f), cork, false);
            var frame = Mat("BoardFrame", "#4D3A29", 0.1f);
            Box("FrameTop", wall, new Vector3(0.345f, 2.015f, 0), new Vector3(0.05f, 0.04f, 1.88f), frame, false);
            Box("FrameBottom", wall, new Vector3(0.345f, 1.085f, 0), new Vector3(0.05f, 0.04f, 1.88f), frame, false);
            Box("FrameL", wall, new Vector3(0.345f, 1.55f, -0.92f), new Vector3(0.05f, 0.9f, 0.04f), frame, false);
            Box("FrameR", wall, new Vector3(0.345f, 1.55f, 0.92f), new Vector3(0.05f, 0.9f, 0.04f), frame, false);

            var print = Mat("PhotoPaper", "#FFFFFF", 0.15f);
            Material PhotoMat(string n) { var m = MatColor("BoardPhoto_" + n, Color.white, 0.15f); m.mainTexture = BoardPhoto(n); return m; }

            // ----- фотографии -----
            BoardPaper(wall, "PhotoKuznetsov", 0.00f, 1.705f, 0.19f, 0.255f, 2f, PhotoMat("kuznetsov"),
                "Григорий Кузнецов, 56 лет",
                "Строительный магнат. Владелец «Стройресурса», «Вектора-М» и ЗАО «КиП». Через городские подряды выводит деньги в тень.\n\n" +
                "Связи в полиции и администрации. Ни разу не привлекался: любые дела против него до суда не доходили.",
                BoardPhoto("kuznetsov"));
            BoardPaper(wall, "PhotoLanskoy", -0.50f, 1.685f, 0.19f, 0.255f, -4f, PhotoMat("lanskoy"),
                "Андрей Ланской, 38 лет",
                "Журналист. Три месяца собирал материал о том, как Кузнецов отмывает деньги через стройки.\n\n" +
                "12 июля вечером его сбила машина на Садовой. Водитель скрылся. Дело закрыли за неделю как несчастный случай.",
                BoardPhoto("lanskoy"));
            BoardPaper(wall, "PhotoBelykh", 0.50f, 1.705f, 0.19f, 0.255f, 3f, PhotoMat("belykh"),
                "Семён Белых, 52 года",
                "Главный бухгалтер Кузнецова, пятнадцать лет ведёт его тёмные деньги. Тихий, без охраны, не бандит.\n\n" +
                "Он один знает код ячейки №212. Вдовец, дочь Ника, 19 лет, учится в Петербурге.",
                BoardPhoto("belykh"));
            BoardPaper(wall, "PhotoAccident", -0.72f, 1.345f, 0.25f, 0.1865f, -3f, PhotoMat("accident"),
                "Место, где сбили Андрея",
                "Перекрёсток на Садовой. Камера на углу «не работала» в тот вечер. Единственный свидетель видел тёмный внедорожник без номеров.\n\n" +
                "Свидетеля вызывали один раз, показания в дело не попали.",
                BoardPhoto("accident"));
            BoardPaper(wall, "PhotoBank", 0.72f, 1.365f, 0.25f, 0.1865f, 4f, PhotoMat("bank"),
                "Банк «Северный»\nСадовая, 8",
                "Депозитарий, ячейка №212. Белых приходит сюда раз в месяц с портфелем.\n\n" +
                "По слухам, в ячейке лежат доказательства, которые страхуют Белых от Кузнецова. Вскрыть её можно только по коду.",
                BoardPhoto("bank"));

            // подписи под фото (белые полоски с рукописным текстом)
            Caption(wall, 0.00f, 1.705f - 0.1275f - 0.017f, 0.19f, "КУЗНЕЦОВ", 2f);
            Caption(wall, -0.50f, 1.685f - 0.1275f - 0.017f, 0.19f, "ЛАНСКОЙ", -4f);
            Caption(wall, 0.50f, 1.705f - 0.1275f - 0.017f, 0.19f, "БЕЛЫХ", 3f);

            // ----- документы и вырезка -----
            BoardPaper(wall, "ClipNews", -0.45f, 1.30f, 0.14f, 0.20f, 5f, MatColor("ClipPaper", Color.white, 0.05f).WithTex(NewsTexture()),
                "Заметка в «Вечернем городе»",
                "Третья полоса, мелким шрифтом: «Несчастный случай на Садовой. Вчера вечером водитель сбил пешехода и скрылся с места происшествия. Пострадавший скончался в больнице. Полиция устанавливает обстоятельства».\n\n" +
                "Ни слова о том, что погибший был журналистом и над чем он работал.", null, Hex("#3E6A96"));
            BoardPaper(wall, "DocProtocol", -0.20f, 1.30f, 0.16f, 0.22f, -3f, MatColor("DocPaper1", Color.white, 0.05f).WithTex(DocTexture("protocol", 3, true)),
                "Протокол по делу Ланского",
                "Составлен за двое суток. Показания свидетеля к делу не приобщены. Камера на перекрёстке в списке неработающих.\n\n" +
                "Следователь Орлов Н. П. — до перехода в полицию семь лет работал в охране у Кузнецова. Дело закрыто через шесть дней.", null);
            BoardPaper(wall, "DocContract", 0.19f, 1.30f, 0.16f, 0.22f, 3f, MatColor("DocPaper2", Color.white, 0.05f).WithTex(DocTexture("contract", 7, false)),
                "Договоры подряда",
                "«Стройресурс», «Вектор-М» и ЗАО «КиП»: у всех трёх одни и те же учредители, подставные люди. Цены на городских объектах завышены втрое.\n\n" +
                "Разница уходила через десятки счетов. Тот, кто вёл эти счета, знает всё. Это Белых.", null);
            BoardPaper(wall, "DocCyprus", 0.45f, 1.30f, 0.16f, 0.22f, -5f, MatColor("DocPaper3", Color.white, 0.05f).WithTex(DocTexture("cyprus", 11, true)),
                "Выписка с Кипра",
                "Счёт в Hellenic Bank, Лимасол, на имя Ирины Белых. За три года на него пришёл 41 миллион от фирм Кузнецова.\n\n" +
                "Ирина — родная сестра Семёна. Кузнецов об этих переводах не знает. Белых воровал у собственного хозяина — копил на побег?", null);

            // ----- стикеры -----
            Sticky(wall, 0.00f, 1.915f, "КТО\nЗАПЛАТИЛ?", 2f);
            Sticky(wall, -0.74f, 1.78f, "Не\nспеши", -5f);
            Sticky(wall, 0.74f, 1.865f, "Ника —\nрычаг?", 4f);
            Sticky(wall, 0.74f, 1.57f, "212 ?", -3f);
            Sticky(wall, 0.00f, 1.20f, "Он всё\nзнает", 3f);

            // ----- красные нитки -----
            // булавки на фото: верх по центру
            float kv = 1.705f + 0.1275f - 0.014f, lv = 1.685f + 0.1275f - 0.014f, bv = 1.705f + 0.1275f - 0.014f;
            Thread(wall, 0.00f, kv, -0.50f, lv);                    // Кузнецов — Ланской
            Thread(wall, 0.00f, kv, 0.50f, bv);                     // Кузнецов — Белых
            Thread(wall, -0.50f, lv, -0.72f, 1.345f + 0.09325f - 0.014f);    // Ланской — место
            Thread(wall, -0.50f, lv, -0.45f, 1.30f + 0.10f - 0.014f);      // Ланской — заметка
            Thread(wall, -0.45f, 1.30f + 0.10f - 0.014f, -0.20f, 1.30f + 0.11f - 0.014f); // заметка — протокол
            Thread(wall, 0.00f, kv, -0.20f, 1.30f + 0.11f - 0.014f);        // Кузнецов — протокол (следователь)
            Thread(wall, 0.00f, kv, 0.19f, 1.30f + 0.11f - 0.014f);         // Кузнецов — договоры
            Thread(wall, 0.50f, bv, 0.19f, 1.30f + 0.11f - 0.014f);         // Белых — договоры
            Thread(wall, 0.50f, bv, 0.45f, 1.30f + 0.11f - 0.014f);         // Белых — Кипр
            Thread(wall, 0.50f, bv, 0.72f, 1.365f + 0.09325f - 0.014f);     // Белых — банк

            // полка под доской с мелочью (лампа, чашка, пачка сигарет)
            var ledge = Mat("LedgeWood", "#5E4A37", 0.08f);
            Box("Ledge", wall, new Vector3(0.30f, 1.07f, 0.0f), new Vector3(0.12f, 0.025f, 1.7f), ledge, false);
            Box("LedgeLip", wall, new Vector3(0.245f, 1.09f, 0.0f), new Vector3(0.01f, 0.03f, 1.7f), ledge, false);
            var mug = Cyl("Mug", wall, new Vector3(0.30f, 1.115f, -0.55f), new Vector3(0.07f, 0.04f, 0.07f), Mat("Mug", "#B8B2A2", 0.5f));
            Cyl("MugCoffee", wall, new Vector3(0.30f, 1.152f, -0.55f), new Vector3(0.058f, 0.002f, 0.058f), Mat("Coffee", "#2A1B12", 0.8f));
            Box("Matchbox", wall, new Vector3(0.30f, 1.095f, -0.30f), new Vector3(0.05f, 0.02f, 0.035f), Mat("Matchbox", "#8C3B2E", 0.2f), false, new Vector3(0, 25, 0));
            Box("Notebook", wall, new Vector3(0.30f, 1.098f, 0.20f), new Vector3(0.1f, 0.012f, 0.14f), Mat("NotebookCover", "#2F3B4A", 0.25f), false, new Vector3(0, -12, 0));
            Box("Pencil", wall, new Vector3(0.30f, 1.108f, 0.20f), new Vector3(0.14f, 0.007f, 0.007f), Mat("PencilYellow", "#D4A93A", 0.3f), false, new Vector3(0, 40, 0));
            Cyl("Ashtray", wall, new Vector3(0.30f, 1.092f, 0.55f), new Vector3(0.08f, 0.01f, 0.08f), Mat("AshGlass", "#6F7A74", 0.7f));
        }

        // Рукописный стикер: жёлтый квадрат с крупным текстом.
        static void Sticky(Transform wall, float u, float v, string text, float roll)
        {
            var m = MatColor("StickyYellow", Hex("#E7C94F"), 0.1f);
            var pivot = BoardPaper(wall, "Sticky", u, v, 0.105f, 0.105f, roll, m, "Заметка", text.Replace("\n", " "), null, Hex("#3E6A96"));
            Object.DestroyImmediate(pivot.GetComponent<BoardItem>());   // стикеры читаются глазами, отдельной карточки им не надо
            Object.DestroyImmediate(pivot.GetComponent<BoxCollider>());
            var ui = UICanvas(pivot, "Text", new Vector3(-0.0016f, -0.004f, 0), new Vector3(0, 90, 0), 0.1f, new Vector2(240, 240));
            UIText(ui, text, 8, 30, 224, 190, 38, new Color(0.2f, 0.12f, 0.1f), FontStyle.BoldAndItalic, TextAnchor.MiddleCenter);
        }

        // Белая полоска с подписью под фотографией.
        static void Caption(Transform wall, float u, float v, float w, string text, float roll)
        {
            var strip = new GameObject("Caption").transform;
            strip.SetParent(wall, false);
            strip.localPosition = new Vector3(0.3475f, v, -u);
            strip.localRotation = Quaternion.Euler(roll, 0, 0);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Strip";
            q.transform.SetParent(strip, false);
            q.transform.localRotation = Quaternion.Euler(0, 90, 0);
            q.transform.localScale = new Vector3(w, 0.03f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = MatColor("CaptionPaper", Hex("#E6DFCF"), 0.1f);
            var ui = UICanvas(strip, "Text", new Vector3(-0.0012f, 0, 0), new Vector3(0, 90, 0), w, new Vector2(300, 60));
            UIText(ui, text, 0, 6, 300, 48, 32, new Color(0.15f, 0.12f, 0.1f), FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        static Material WithTex(this Material m, Texture2D t) { m.mainTexture = t; return m; }

        static Texture2D CorkTexture()
        {
            var c = new Paint(128, 128, Hex("#A9824F"));
            var rng = new System.Random(21);
            for (int i = 0; i < 700; i++)
            {
                float x = (float)rng.NextDouble() * 128, y = (float)rng.NextDouble() * 128, r = 0.6f + (float)rng.NextDouble() * 2.2f;
                bool dark = rng.NextDouble() < 0.55;
                c.Circle(x, y, r, dark ? Hex("#7C5C36", 0.5f) : Hex("#C79E68", 0.5f));
            }
            var t = c.Done();
            t.wrapMode = TextureWrapMode.Repeat;
            return Save(t, "T_Cork");
        }

        // Машинописная бумага: заголовок, строки текста, иногда штамп. Читается как документ, подробности — в карточке по E.
        static Texture2D DocTexture(string key, int seed, bool stamp)
        {
            var c = new Paint(120, 168, Hex("#E4DCC8"));
            c.Rect(12, 12, 54, 6, Hex("#4A453E"));
            c.TextLine(12, 24, 80, 2.5f, Hex("#8A847A"), seed);
            c.Rect(12, 34, 96, 1, Hex("#6E685E"));
            for (int r = 0; r < 13; r++)
                c.TextLine(12, 42 + r * 9.4f, r % 4 == 3 ? 56 : 96, 3f, Hex("#6E685E"), seed * 31 + r);
            if (stamp)
            {
                c.Circle(86, 138, 17, Hex("#B0362B", 0.3f));
                c.Circle(86, 138, 14, Hex("#E4DCC8"));
                c.Circle(86, 138, 12, Hex("#B0362B", 0.35f));
                c.Circle(86, 138, 9, Hex("#E4DCC8"));
            }
            c.Line(14, 150, 40, 146, 1.2f, Hex("#2E4C8C"));
            return Save(c.Done(), "T_Doc_" + key);
        }

        static Texture2D NewsTexture()
        {
            var c = new Paint(96, 136, Hex("#D9D5C8"));
            c.Rect(8, 8, 80, 12, Hex("#2A2824"));
            c.Rect(8, 24, 80, 1.2f, Hex("#3A3834"));
            c.Rect(8, 30, 38, 28, Hex("#8E8A7E"));        // фото-врезка
            c.Rect(10, 33, 34, 22, Hex("#6F6C62"));
            for (int r = 0; r < 4; r++) c.TextLine(50, 31 + r * 7, 38, 2.2f, Hex("#4E4B44"), 90 + r);
            for (int r = 0; r < 10; r++) c.TextLine(8, 64 + r * 6.6f, 80, 2.2f, Hex("#4E4B44"), 140 + r);
            return Save(c.Done(), "T_News");
        }

        static void Chair(string name, Transform parent, Vector3 pos, float yaw, Material wood)
        {
            var chair = new GameObject(name).transform;
            chair.SetParent(parent, false);
            chair.localPosition = pos;
            chair.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Seat", chair, new Vector3(0, 0.45f, 0), new Vector3(0.44f, 0.04f, 0.42f), wood, false);
            foreach (var p in new[] { new Vector2(-0.19f, -0.18f), new Vector2(0.19f, -0.18f), new Vector2(-0.19f, 0.18f), new Vector2(0.19f, 0.18f) })
                Box("Leg", chair, new Vector3(p.x, 0.22f, p.y), new Vector3(0.035f, 0.45f, 0.035f), wood, false);
            Box("PostL", chair, new Vector3(-0.19f, 0.7f, -0.19f), new Vector3(0.035f, 0.5f, 0.035f), wood, false);
            Box("PostR", chair, new Vector3(0.19f, 0.7f, -0.19f), new Vector3(0.035f, 0.5f, 0.035f), wood, false);
            Box("Back", chair, new Vector3(0, 0.85f, -0.19f), new Vector3(0.42f, 0.12f, 0.025f), wood, false);
            var col = chair.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, 0);
            col.size = new Vector3(0.45f, 0.9f, 0.45f);
        }

        // ---------- лампы ----------

        static Transform Lamp(Transform room)
        {
            var pivot = new GameObject("HangingLamp").transform;
            pivot.SetParent(room, false);
            pivot.localPosition = new Vector3(0, H, 0.05f);
            var cord = Mat("Cord", "#1B1A18", 0.2f);
            CylBetween("Cord", pivot, Vector3.zero, new Vector3(0, -0.72f, 0), 0.006f, cord);
            var shadeMat = Mat("LampShade", "#4F6158", 0.35f);
            var shade = Cyl("Shade", pivot, new Vector3(0, -0.78f, 0), new Vector3(0.36f, 0.06f, 0.36f), shadeMat);
            Cyl("ShadeTop", pivot, new Vector3(0, -0.73f, 0), new Vector3(0.12f, 0.03f, 0.12f), shadeMat);
            var bulbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(bulbGo.GetComponent<Collider>());
            bulbGo.name = "Bulb";
            bulbGo.transform.SetParent(pivot, false);
            bulbGo.transform.localPosition = new Vector3(0, -0.84f, 0);
            bulbGo.transform.localScale = Vector3.one * 0.08f;
            var bulbMat = Emissive("Bulb", "#FFE2B0", 2.2f);
            bulbGo.GetComponent<Renderer>().sharedMaterial = bulbMat;

            var spot = new GameObject("Light").AddComponent<Light>();
            spot.transform.SetParent(pivot, false);
            spot.transform.localPosition = new Vector3(0, -0.82f, 0);
            spot.transform.localRotation = Quaternion.Euler(90, 0, 0);
            spot.type = LightType.Spot;
            spot.spotAngle = 88f;
            spot.innerSpotAngle = 32f;
            spot.range = 5.5f;
            spot.intensity = 3.3f;
            spot.color = new Color(1f, 0.79f, 0.54f);
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.9f;
            // Малые смещения тени: со стандартными (0.05 / 0.4) тени тонких ножек стульев и стола отрывались от пола.
            spot.shadowBias = 0.008f;
            spot.shadowNormalBias = 0.08f;
            spot.shadowNearPlane = 0.1f;
            spot.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;

            // Слабая заливка, чтобы углы не проваливались в чистый чёрный.
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(pivot, false);
            fill.transform.localPosition = new Vector3(0, -0.5f, 0);
            fill.type = LightType.Point;
            fill.range = 7f;
            fill.intensity = 0.55f;
            fill.color = new Color(1f, 0.75f, 0.5f);

            // Отражённый от стола свет снизу на лицо — без него лицо в тени абажура.
            var bounce = new GameObject("TableBounce").AddComponent<Light>();
            bounce.transform.SetParent(room, false);
            bounce.transform.localPosition = new Vector3(0, 0.95f, 0.1f);
            bounce.type = LightType.Point;
            bounce.range = 1.6f;
            bounce.intensity = 0.9f;
            bounce.color = new Color(0.95f, 0.85f, 0.62f);

            var hl = pivot.gameObject.AddComponent<HangingLamp>();
            hl.bulb = spot;
            hl.glow = bulbGo.GetComponent<Renderer>();

            // Пыль в конусе света.
            var dust = new GameObject("Dust").AddComponent<ParticleSystem>();
            dust.transform.SetParent(room, false);
            dust.transform.localPosition = new Vector3(0, 1.3f, 0);
            var main = dust.main;
            main.startLifetime = 12f;
            main.startSpeed = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
            main.startColor = new Color(1f, 0.88f, 0.7f, 0.5f);
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            var em = dust.emission;
            em.rateOverTime = 30f;
            var sh = dust.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(2.4f, 2.4f, 2.4f);
            var noise = dust.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var col = dust.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var pr = dust.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = DustMaterial();

            // Настольная лампа: её можно развернуть в лицо.
            var desk = new GameObject("DeskLamp").transform;
            desk.SetParent(room, false);
            desk.localPosition = new Vector3(-0.45f, 0.77f, -0.05f);
            desk.localRotation = Quaternion.Euler(0, 0, 0);
            var deskMetal = Mat("DeskLampMetal", "#7A5A3A", 0.45f);
            Cyl("Base", desk, new Vector3(0, 0.01f, 0), new Vector3(0.14f, 0.012f, 0.14f), deskMetal);
            CylBetween("Arm", desk, new Vector3(0, 0.01f, 0), new Vector3(0, 0.32f, -0.02f), 0.012f, deskMetal);
            var head = new GameObject("Head").transform;
            head.SetParent(desk, false);
            head.localPosition = new Vector3(0, 0.33f, -0.02f);
            var cone = Cyl("Cone", head, new Vector3(0, 0, 0.06f), new Vector3(0.11f, 0.07f, 0.11f), deskMetal);
            cone.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var deskBulb = Cyl("Glow", head, new Vector3(0, 0, 0.13f), new Vector3(0.085f, 0.003f, 0.085f), Emissive("DeskBulb", "#FFE7C2", 2.5f));
            deskBulb.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var beam = new GameObject("Beam").AddComponent<Light>();
            beam.transform.SetParent(head, false);
            beam.transform.localPosition = new Vector3(0, 0, 0.14f);
            beam.type = LightType.Spot;
            beam.spotAngle = 50f;
            beam.range = 3f;
            beam.intensity = 1.6f;
            beam.color = new Color(1f, 0.9f, 0.75f);
            var dl = desk.gameObject.AddComponent<DeskLamp>();
            dl.head = head;
            dl.beam = beam;
            // Голова смотрит на +z (к Белых): вниз на стол или прямо в лицо.
            dl.tableEuler = new Vector3(65f, 0f, 0f);
            dl.faceEuler = new Vector3(-14f, 28f, 0f);
            head.localRotation = Quaternion.Euler(dl.tableEuler);
            var dlc = desk.gameObject.AddComponent<BoxCollider>();
            dlc.center = new Vector3(0, 0.2f, 0);
            dlc.size = new Vector3(0.22f, 0.42f, 0.25f);
            return pivot;
        }

        // ---------- Семён Белых ----------
        // Правило: у соседних деталей грани не должны лежать в одной плоскости — иначе мерцают.
        // После сборки ZFightCheck.Run проверяет модель и пишет в консоль, если что-то совпало.

        static Suspect BuildSuspect()
        {
            var root = new GameObject("Suspect").transform;
            root.localPosition = new Vector3(0, 0, 0.82f);
            root.localRotation = Quaternion.Euler(0, 180, 0);

            var skin = Mat("Skin", "#C49C7E", 0.15f);
            var skinShade = Mat("SkinShade", "#B38A6E", 0.12f);
            var stubble = Mat("Stubble", "#B08D78", 0.1f);
            var trousers = Mat("Trousers", "#45423D", 0.05f);
            var shirt = Mat("Shirt", "#9AA4A6", 0.08f);
            var vest = Mat("Vest", "#66604E", 0.02f);
            var tieMat = Mat("Tie", "#6B3832", 0.15f);
            var hair = Mat("Hair", "#8E8982", 0.05f);
            var greyHair = Mat("Mustache", "#6E6A64", 0.05f);
            var dark = Mat("Dark", "#1E1B19", 0.3f);
            var white = Mat("EyeWhite", "#D9D2C6", 0.2f);
            var shoe = Mat("Shoe", "#231E1A", 0.45f);
            var sole = Mat("Sole", "#141210", 0.2f);
            var leather = Mat("Belt", "#2B211B", 0.35f);
            var brass = Mat("Brass", "#A88A4A", 0.5f);
            var button = Mat("Button", "#3E3A30", 0.3f);
            var frame = Mat("GlassesFrame", "#2C2621", 0.5f);
            var chairWood = Mat("ChairWood", "#7A5E43", 0.1f);

            Chair("Chair", root, Vector3.zero, 0f, chairWood);
            Object.DestroyImmediate(root.Find("Chair").GetComponent<BoxCollider>());

            // Ноги.
            Box("Pelvis", root, new Vector3(0, 0.545f, -0.02f), new Vector3(0.36f, 0.15f, 0.30f), trousers, false);
            Box("Belt", root, new Vector3(0, 0.615f, -0.02f), new Vector3(0.37f, 0.034f, 0.306f), leather, false);
            Box("Buckle", root, new Vector3(0, 0.615f, 0.136f), new Vector3(0.042f, 0.03f, 0.008f), brass, false);
            foreach (var s in new[] { -1f, 1f })
            {
                Box("Thigh", root, new Vector3(0.095f * s, 0.553f, 0.17f), new Vector3(0.145f, 0.13f, 0.40f), trousers, false);
                Box("Knee", root, new Vector3(0.1f * s, 0.538f, 0.358f), new Vector3(0.132f, 0.118f, 0.06f), trousers, false);
                Box("Shin", root, new Vector3(0.1f * s, 0.285f, 0.372f), new Vector3(0.118f, 0.45f, 0.118f), trousers, false);
                Box("Sock", root, new Vector3(0.1f * s, 0.075f, 0.372f), new Vector3(0.104f, 0.05f, 0.104f), dark, false);
                Box("Shoe", root, new Vector3(0.1f * s, 0.048f, 0.42f), new Vector3(0.112f, 0.07f, 0.236f), shoe, false);
                Box("ShoeToe", root, new Vector3(0.1f * s, 0.036f, 0.535f), new Vector3(0.106f, 0.052f, 0.03f), shoe, false);
                Box("Sole", root, new Vector3(0.1f * s, 0.009f, 0.428f), new Vector3(0.118f, 0.018f, 0.262f), sole, false);
                Box("AnkleTie", root, new Vector3(0.1f * s, 0.125f, 0.372f), new Vector3(0.13f, 0.014f, 0.13f), dark, false);
            }

            var spine = new GameObject("Spine").transform;
            spine.SetParent(root, false);
            spine.localPosition = new Vector3(0, 0.6f, -0.04f);

            var torso = new GameObject("Torso").transform;
            torso.SetParent(spine, false);
            Box("Shirt", torso, new Vector3(0, 0.27f, 0), new Vector3(0.40f, 0.50f, 0.23f), shirt, false);
            Box("Vest", torso, new Vector3(0, 0.207f, 0.003f), new Vector3(0.41f, 0.364f, 0.24f), vest, false);
            for (int i = 0; i < 3; i++)
                Box("Button" + i, torso, new Vector3(0, 0.29f - i * 0.07f, 0.1252f), new Vector3(0.013f, 0.013f, 0.005f), button, false);
            Box("PocketL", torso, new Vector3(-0.11f, 0.13f, 0.1238f), new Vector3(0.08f, 0.006f, 0.004f), button, false);
            Box("PocketR", torso, new Vector3(0.11f, 0.13f, 0.1238f), new Vector3(0.08f, 0.006f, 0.004f), button, false);
            Box("Tie", torso, new Vector3(0.006f, 0.33f, 0.1305f), new Vector3(0.05f, 0.2f, 0.006f), tieMat, false, new Vector3(0, 0, 6f));
            Box("TieKnot", torso, new Vector3(0.004f, 0.444f, 0.129f), new Vector3(0.046f, 0.042f, 0.018f), tieMat, false);
            Box("CollarL", torso, new Vector3(-0.046f, 0.497f, 0.098f), new Vector3(0.086f, 0.028f, 0.052f), shirt, false, new Vector3(-10, 0, -22));
            Box("CollarR", torso, new Vector3(0.046f, 0.497f, 0.098f), new Vector3(0.086f, 0.028f, 0.052f), shirt, false, new Vector3(-10, 0, 22));

            // Руки стянуты за спиной.
            foreach (var s in new[] { -1f, 1f })
            {
                var shoulder = new Vector3(0.22f * s, 0.44f, 0f);
                var elbow = new Vector3(0.262f * s, 0.2f, -0.12f);
                var wrist = new Vector3(0.068f * s, 0.1f, -0.236f);
                Box("Shoulder", spine, new Vector3(0.205f * s, 0.457f, 0.002f), new Vector3(0.1f, 0.083f, 0.168f), shirt, false);
                BoxBetween("UpperArm", spine, shoulder, elbow, 0.1f, shirt);
                BoxBetween("Forearm", spine, elbow, wrist, 0.088f, shirt);
                Box("Hand", spine, wrist + new Vector3(-0.004f * s, -0.018f, -0.022f), new Vector3(0.058f, 0.082f, 0.048f), skin, false, new Vector3(0, 0, 8f * s));
            }
            Box("WristTie", spine, new Vector3(0, 0.084f, -0.257f), new Vector3(0.21f, 0.016f, 0.058f), dark, false);
            Box("Neck", spine, new Vector3(0, 0.556f, 0.002f), new Vector3(0.098f, 0.088f, 0.098f), skin, false);

            var head = new GameObject("Head").transform;
            head.SetParent(spine, false);
            head.localPosition = new Vector3(0, 0.59f, 0f);
            Box("Skull", head, new Vector3(0, 0.135f, 0.005f), new Vector3(0.205f, 0.24f, 0.225f), skin, false);
            Box("Crown", head, new Vector3(0, 0.267f, -0.003f), new Vector3(0.186f, 0.026f, 0.196f), skin, false);
            Box("Cheeks", head, new Vector3(0, 0.078f, 0.032f), new Vector3(0.21f, 0.052f, 0.15f), skin, false);
            Box("Jaw", head, new Vector3(0, 0.03f, 0.032f), new Vector3(0.168f, 0.068f, 0.186f), stubble, false);
            Box("Chin", head, new Vector3(0, 0.012f, 0.122f), new Vector3(0.072f, 0.038f, 0.022f), stubble, false);
            Box("BrowRidge", head, new Vector3(0, 0.176f, 0.119f), new Vector3(0.168f, 0.022f, 0.02f), skinShade, false);
            Box("Nose", head, new Vector3(0, 0.112f, 0.129f), new Vector3(0.034f, 0.062f, 0.044f), skin, false);
            Box("NoseTip", head, new Vector3(0, 0.086f, 0.147f), new Vector3(0.042f, 0.026f, 0.03f), skinShade, false);
            Box("Mustache", head, new Vector3(0, 0.066f, 0.134f), new Vector3(0.092f, 0.022f, 0.022f), greyHair, false);
            var mouth = Box("Mouth", head, new Vector3(0, 0.041f, 0.1265f), new Vector3(0.056f, 0.008f, 0.006f), Mat("Mouth", "#4A2622", 0.2f), false).transform;
            foreach (var s in new[] { -1f, 1f })
                Box(s < 0 ? "EarL" : "EarR", head, new Vector3(0.112f * s, 0.132f, -0.002f), new Vector3(0.022f, 0.058f, 0.04f), skinShade, false);

            // Глаза — пивоты со склерой и зрачком: Suspect сжимает их по высоте (моргание, прищур).
            var eyeL = Pivot("EyeL", head, new Vector3(-0.045f, 0.151f, 0.1185f));
            var eyeR = Pivot("EyeR", head, new Vector3(0.045f, 0.151f, 0.1185f));
            foreach (var eye in new[] { eyeL, eyeR })
            {
                Box("White", eye, new Vector3(0, 0, 0.001f), new Vector3(0.034f, 0.02f, 0.004f), white, false);
                Box("Pupil", eye, new Vector3(0, -0.001f, 0.0035f), new Vector3(0.014f, 0.014f, 0.004f), dark, false);
            }
            var browL = Pivot("BrowL", head, new Vector3(-0.047f, 0.184f, 0.127f));
            Box("Brow", browL, Vector3.zero, new Vector3(0.062f, 0.014f, 0.014f), greyHair, false);
            var browR = Pivot("BrowR", head, new Vector3(0.047f, 0.184f, 0.127f));
            Box("Brow", browR, Vector3.zero, new Vector3(0.062f, 0.014f, 0.014f), greyHair, false);

            // Волосы: седой венчик вокруг лысины и редкий зачёс поверх.
            Box("HairBack", head, new Vector3(0, 0.166f, -0.036f), new Vector3(0.215f, 0.12f, 0.168f), hair, false);
            Box("HairNape", head, new Vector3(0, 0.1f, -0.106f), new Vector3(0.17f, 0.05f, 0.022f), hair, false);
            foreach (var s in new[] { -1f, 1f })
                Box(s < 0 ? "SideburnL" : "SideburnR", head, new Vector3(0.1045f * s, 0.12f, 0.061f), new Vector3(0.011f, 0.058f, 0.034f), hair, false);
            for (int i = 0; i < 3; i++)
                Box("CombOver" + i, head, new Vector3(0.012f - i * 0.01f, 0.2855f, -0.05f + i * 0.04f), new Vector3(0.16f, 0.008f, 0.026f), hair, false, new Vector3(0, -8 + i * 6, 0));

            // Очки: перекладины не касаются стоек — иначе их грани совпадают.
            foreach (var s in new[] { -1f, 1f })
            {
                float cx = 0.047f * s;
                Box("RimT", head, new Vector3(cx, 0.172f, 0.136f), new Vector3(0.074f, 0.008f, 0.006f), frame, false);
                Box("RimB", head, new Vector3(cx, 0.128f, 0.136f), new Vector3(0.074f, 0.006f, 0.006f), frame, false);
                Box("RimO", head, new Vector3(cx + 0.034f * s, 0.1495f, 0.1357f), new Vector3(0.006f, 0.036f, 0.0055f), frame, false);
                Box("RimI", head, new Vector3(cx - 0.031f * s, 0.1495f, 0.1357f), new Vector3(0.006f, 0.036f, 0.0055f), frame, false);
                Box("Temple", head, new Vector3(0.108f * s, 0.166f, 0.062f), new Vector3(0.005f, 0.005f, 0.14f), frame, false);
            }
            Box("Bridge", head, new Vector3(0, 0.163f, 0.1375f), new Vector3(0.022f, 0.005f, 0.005f), frame, false);

            var sus = root.gameObject.AddComponent<Suspect>();
            sus.spine = spine;
            sus.torso = torso;
            sus.head = head;
            sus.mouth = mouth;
            sus.eyeL = eyeL;
            sus.eyeR = eyeR;
            sus.browL = browL;
            sus.browR = browR;
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.75f, 0.05f);
            col.size = new Vector3(0.6f, 1.5f, 0.7f);

            string z = ZFightCheck.Run(root);
            if (!z.StartsWith("0 ")) Debug.LogWarning("[QuietRoom] Мерцание в модели Белых: " + z);
            return sus;
        }

        // ---------- предметы ----------

        static void Items()
        {
            var items = new GameObject("Items").transform;

            // Место на столе прямо перед Белых — сюда кладут улики.
            var spot = new GameObject("PresentSpot");
            spot.transform.position = new Vector3(0.02f, 0.772f, 0.2f);
            spot.transform.rotation = Quaternion.Euler(0, 180, 0);
            var spotCol = spot.AddComponent<BoxCollider>();
            spotCol.isTrigger = true;
            spotCol.center = new Vector3(0, 0.05f, 0);
            spotCol.size = new Vector3(0.62f, 0.1f, 0.34f);
            spot.AddComponent<PresentSpot>();

            WaterGlass(items, new Vector3(-0.3f, 0.772f, -0.25f));

            // Остальное — на подносе на верстаке.
            float bx = 3.48f, by = 0.921f;
            Chocolate(items, new Vector3(bx, by, -0.55f), new Vector3(0, 20, 0));
            Folder(items, new Vector3(bx, by, -0.2f), new Vector3(0, -8, 0));
            Phone(items, new Vector3(bx, by, 0.15f), new Vector3(0, 35, 0));
            Photo(items, new Vector3(bx - 0.1f, by, 0.38f), new Vector3(0, -15, 0));
            Pistol(items, new Vector3(bx + 0.05f, by, 0.64f), new Vector3(0, 80, 0));
        }

        static void WaterGlass(Transform items, Vector3 pos)
        {
            var water = Item(items, "Water", ItemKind.Water, "стакан воды", pos);
            // Гранёный стакан: меш строится кодом (12 граней, толстое дно, гладкий ободок).
            var go = new GameObject("Glass");
            go.transform.SetParent(water, false);
            go.AddComponent<MeshFilter>().sharedMesh = GlassMesh();
            var mr = go.AddComponent<MeshRenderer>();
            var glassMat = Transparent("GlassClear", new Color(0.88f, 0.94f, 0.93f, 0.42f), 0.96f);
            // Лёгкое свечение граней: в тёмном подвале прозрачное стекло иначе почти не видно.
            glassMat.EnableKeyword("_EMISSION");
            glassMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glassMat.SetColor("_EmissionColor", new Color(0.09f, 0.1f, 0.1f));
            glassMat.renderQueue = 3001;   // стекло рисуется поверх воды
            mr.sharedMaterial = glassMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var gw = water.gameObject.AddComponent<GlassWater>();
            var side = Transparent("WaterBody", new Color(0.62f, 0.8f, 0.82f, 0.4f), 0.9f);
            side.EnableKeyword("_EMISSION");
            side.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            side.SetColor("_EmissionColor", new Color(0.05f, 0.08f, 0.09f));
            side.renderQueue = 2995;
            var top = Transparent("WaterSurface", new Color(0.86f, 0.95f, 0.95f, 0.6f), 1f);
            top.EnableKeyword("_EMISSION");
            top.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            top.SetColor("_EmissionColor", new Color(0.08f, 0.11f, 0.12f));
            top.renderQueue = 2996;
            gw.sideMaterial = side;
            gw.surfaceMaterial = top;
            // Мокрое пятно: светлее пола и с бликом от лампы, иначе в полумраке его не видно.
            var puddle = Transparent("Puddle", new Color(0.4f, 0.47f, 0.5f, 0.42f), 0.98f);
            puddle.EnableKeyword("_EMISSION");
            puddle.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            puddle.SetColor("_EmissionColor", new Color(0.03f, 0.035f, 0.04f));
            puddle.renderQueue = 2990;
            gw.puddleMaterial = puddle;
            var drop = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));
            drop.mainTexture = DustMaterial().mainTexture;
            drop.SetColor("_TintColor", new Color(0.55f, 0.7f, 0.75f, 0.6f));
            string dropPath = MatDir + "/M_WaterDrop.mat";
            var oldDrop = AssetDatabase.LoadAssetAtPath<Material>(dropPath);
            if (oldDrop == null) { AssetDatabase.CreateAsset(drop, dropPath); oldDrop = drop; }
            else { oldDrop.shader = drop.shader; oldDrop.mainTexture = drop.mainTexture; oldDrop.SetColor("_TintColor", drop.GetColor("_TintColor")); }
            gw.dropMaterial = oldDrop;

            // Рука детектива держит стакан — видна только в руке. Пальцы обхватывают стенку с дальней стороны.
            var held = new GameObject("Held").transform;
            held.SetParent(water, false);
            var skinHand = Mat("DetectiveSkin", "#B88E70", 0.2f);
            var knuckle = Mat("DetectiveKnuckle", "#A97F62", 0.2f);
            Box("Palm", held, new Vector3(0.032f, 0.052f, -0.012f), new Vector3(0.014f, 0.06f, 0.042f), skinHand, false, new Vector3(0, -28, 0));
            for (int i = 0; i < 4; i++)
            {
                float y = 0.074f - i * 0.0135f;
                float len = i == 3 ? 0.03f : 0.038f;
                Box("Finger" + i, held, new Vector3(0.012f, y, 0.034f), new Vector3(len, 0.0105f, 0.0105f), i % 2 == 0 ? skinHand : knuckle, false, new Vector3(0, 22, 0));
                Box("Tip" + i, held, new Vector3(-0.009f, y, 0.0385f), new Vector3(0.012f, 0.0098f, 0.0098f), skinHand, false, new Vector3(0, 40, 0));
            }
            Box("Thumb", held, new Vector3(0.006f, 0.068f, -0.036f), new Vector3(0.03f, 0.011f, 0.011f), skinHand, false, new Vector3(0, -25, 8));
            Box("Wrist", held, new Vector3(0.056f, 0.046f, -0.034f), new Vector3(0.038f, 0.038f, 0.06f), skinHand, false, new Vector3(-6, -42, 0));
            Box("Cuff", held, new Vector3(0.076f, 0.043f, -0.056f), new Vector3(0.044f, 0.044f, 0.016f), Mat("ShirtCuff", "#D9D4C8", 0.1f), false, new Vector3(-6, -42, 0));
            Box("Sleeve", held, new Vector3(0.1f, 0.04f, -0.084f), new Vector3(0.056f, 0.056f, 0.08f), Mat("Coat", "#3A3630", 0.05f), false, new Vector3(-6, -42, 0));
            held.gameObject.SetActive(false);

            Collider(water, new Vector3(0, 0.06f, 0), new Vector3(0.09f, 0.13f, 0.09f));
            var p = water.GetComponent<Pickup>();
            p.holdOffset = new Vector3(0.15f, -0.19f, 0.42f);
            p.keepUpright = true;
            p.inspectOffset = new Vector3(0f, -0.06f, 0.26f);
            p.inspectEuler = new Vector3(0f, 0f, 0f);
        }

        // Меш гранёного стакана: внешние грани (плоские нормали), ободок, внутренняя стенка, толстое дно.
        static Mesh GlassMesh()
        {
            const int n = 12;
            const float h = 0.11f, rim = 0.016f, bottom = 0.012f, wall = 0.0028f;
            const float r0 = 0.0295f, r1 = 0.0355f;      // внешний радиус внизу и у начала ободка
            var v = new List<Vector3>();
            var t = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)   // по часовой, если смотреть на лицевую сторону
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }
            Vector3 P(float angle, float r, float y) => new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);

            float hf = h - rim;
            for (int k = 0; k < n; k++)
            {
                float a0 = (float)k / n * Mathf.PI * 2f, a1 = (float)(k + 1) / n * Mathf.PI * 2f;
                float rimR = r1 + 0.0006f;
                // Внешние грани (снизу до ободка) и гладкий ободок.
                Quad(P(a0, r0, 0), P(a0, r1, hf), P(a1, r1, hf), P(a1, r0, 0));
                for (int s = 0; s < 3; s++)
                {
                    float b0 = Mathf.Lerp(a0, a1, s / 3f), b1 = Mathf.Lerp(a0, a1, (s + 1) / 3f);
                    Quad(P(b0, rimR, hf), P(b0, rimR, h), P(b1, rimR, h), P(b1, rimR, hf));
                    // Верхний срез стенки и внутренняя сторона ободка.
                    Quad(P(b0, rimR - wall, h), P(b1, rimR - wall, h), P(b1, rimR, h), P(b0, rimR, h));
                    Quad(P(b1, rimR - wall, hf), P(b1, rimR - wall, h), P(b0, rimR - wall, h), P(b0, rimR - wall, hf));
                }
                // Ступенька ободка.
                Quad(P(a0, r1, hf), P(a0, rimR, hf), P(a1, rimR, hf), P(a1, r1, hf));
                // Внутренняя стенка граней — лицом внутрь.
                Quad(P(a1, r0 - wall, bottom), P(a1, r1 - wall, hf), P(a0, r1 - wall, hf), P(a0, r0 - wall, bottom));
                // Дно: снизу и сверху (толща стекла).
                int c = v.Count;
                v.Add(new Vector3(0, 0, 0)); v.Add(P(a0, r0, 0)); v.Add(P(a1, r0, 0));
                t.Add(c); t.Add(c + 1); t.Add(c + 2);
                c = v.Count;
                v.Add(new Vector3(0, bottom, 0)); v.Add(P(a1, r0 - wall, bottom)); v.Add(P(a0, r0 - wall, bottom));
                t.Add(c); t.Add(c + 1); t.Add(c + 2);
            }

            string path = MatDir + "/GlassMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "GlassMesh" };
            mesh.Clear();
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ---------- шоколадка ----------
        // Плитка — одно целое: основание и 2×5 квадратиков со скосами, между ними неглубокие желобки.
        // Верх — карта высот на сетке, привязанной к рёбрам квадратиков (рёбра чёткие).
        // Надкушенная: передний край обрезан дугой укуса с ямками от зубов.
        const float BarW = 0.037f, BarZ0 = -0.07f, BarZ1 = 0.07f, BarH0 = 0.0035f, BarH1 = 0.0078f;

        static float BarProfile(float d)    // d — расстояние от края квадратика внутрь
        {
            if (d <= 0f) return 0f;
            if (d < 0.0005f) return 0.7f * d / 0.0005f;
            return 0.7f + 0.3f * Mathf.Min(1f, (d - 0.0005f) / 0.0022f);
        }

        static float BarHeight(float x, float z)
        {
            float cx = x < 0f ? -0.0185f : 0.0185f;
            float row = Mathf.Clamp(Mathf.Round((z + 0.056f) / 0.028f), 0f, 4f);
            float cz = -0.056f + row * 0.028f;
            float dx = 0.0175f - Mathf.Abs(x - cx), dz = 0.0128f - Mathf.Abs(z - cz);
            return BarH0 + (BarH1 - BarH0) * BarProfile(Mathf.Min(dx, dz));
        }

        static float BiteZ(float x)         // передний край после укуса: дуга рта и ямки от зубов
        {
            float u = x / BarW;
            // Дуга рта (в середине глубже) + полукруглые ямки от зубов шириной ~9 мм.
            float teeth = Mathf.Sqrt(Mathf.Abs(Mathf.Sin(Mathf.PI * x / 0.009f + 0.5f)));
            return Mathf.Min(BarZ1, -0.004f + 0.032f * u * u + 0.0035f * (teeth - 1f));
        }

        static List<float> Breaks(float lo, float hi, float[] centers, float half, float fill)
        {
            var list = new List<float> { lo, hi };
            foreach (float c in centers)
                foreach (float e in new[] { c - half, c + half })
                {
                    float inward = e < c ? 1f : -1f;
                    list.Add(e); list.Add(e + inward * 0.0005f); list.Add(e + inward * 0.0027f);
                }
            for (float f = lo; f < hi; f += fill) list.Add(f);
            list.RemoveAll(f => f < lo || f > hi);
            list.Sort();
            var outList = new List<float>();
            foreach (float f in list) if (outList.Count == 0 || f - outList[outList.Count - 1] > 0.00015f) outList.Add(f);
            return outList;
        }

        static Mesh BarMesh(bool bitten)
        {
            var xs = Breaks(-BarW, BarW, new[] { -0.0185f, 0.0185f }, 0.0175f, 0.0008f);
            var zs = Breaks(BarZ0, BarZ1, new[] { -0.056f, -0.028f, 0f, 0.028f, 0.056f }, 0.0128f, 0.002f);
            int nx = xs.Count, nz = zs.Count;
            var v = new List<Vector3>();
            var t = new List<int>();
            void Tri(int a, int b, int c) { t.Add(a); t.Add(b); t.Add(c); }
            float Front(float x) => bitten ? BiteZ(x) : BarZ1;
            float TopH(float x, float z)
            {
                float h = BarHeight(x, z);
                if (bitten) h -= 0.0013f * Mathf.Clamp01(1f - (Front(x) - z) / 0.0022f);   // зубы примяли край
                return h;
            }

            // Верх и низ: сетка, точки за линией укуса прижаты к ней.
            for (int layer = 0; layer < 2; layer++)
            {
                int b = v.Count;
                for (int j = 0; j < nz; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        float x = xs[i], z = Mathf.Min(zs[j], Front(x));
                        v.Add(new Vector3(x, layer == 0 ? TopH(x, z) : 0f, z));
                    }
                for (int j = 0; j < nz - 1; j++)
                    for (int i = 0; i < nx - 1; i++)
                    {
                        int i0 = b + j * nx + i, i1 = i0 + 1, i2 = i0 + nx, i3 = i2 + 1;
                        if (layer == 0) { Tri(i0, i2, i1); Tri(i1, i2, i3); }
                        else { Tri(i0, i1, i2); Tri(i1, i3, i2); }
                    }
            }

            // Борта: полоса от кромки верха до низа (три уровня — у укуса край неровный).
            void Wall(List<Vector3> edge, bool rough)
            {
                int b = v.Count;
                for (int k = 0; k < edge.Count; k++)
                {
                    var e = edge[k];
                    float jag = rough ? (Mathf.PerlinNoise(e.x * 400f, 1.3f) - 0.5f) * 0.0016f : 0f;
                    v.Add(e);
                    v.Add(new Vector3(e.x, e.y * 0.5f, e.z + jag));
                    v.Add(new Vector3(e.x, 0f, e.z + (rough ? 0.0008f : 0f)));
                }
                for (int k = 0; k < edge.Count - 1; k++)
                    for (int l = 0; l < 2; l++)
                    {
                        int a0 = b + k * 3 + l, a1 = a0 + 1, b0 = a0 + 3, b1 = b0 + 1;
                        Tri(a0, b0, a1); Tri(a1, b0, b1);
                    }
            }
            var front = new List<Vector3>();
            var back = new List<Vector3>();
            for (int i = nx - 1; i >= 0; i--) { float x = xs[i], z = Front(x); front.Add(new Vector3(x, TopH(x, z), z)); }
            for (int i = 0; i < nx; i++) back.Add(new Vector3(xs[i], TopH(xs[i], BarZ0), BarZ0));
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            for (int j = nz - 1; j >= 0; j--) { float z = Mathf.Min(zs[j], Front(-BarW)); left.Add(new Vector3(-BarW, TopH(-BarW, z), z)); }
            for (int j = 0; j < nz; j++) { float z = Mathf.Min(zs[j], Front(BarW)); right.Add(new Vector3(BarW, TopH(BarW, z), z)); }
            Wall(front, bitten); Wall(back, false); Wall(left, false); Wall(right, false);

            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // Фольга — рубашка вокруг плитки, заранее «разрезанная» рваными линиями на куски, которые потом отрывают.
        // Периметр сечения обходится как у обёртки: верх слева направо, правый бок вниз, низ справа налево, левый бок вверх.
        const float FoilW = 0.0385f, FoilH = 0.005f, FoilY = -0.0008f, FoilZ0 = -0.0705f, FoilZ1 = 0.0705f;
        static float FoilPerim => 4f * FoilW + 4f * FoilH;

        static Vector3 FoilAt(float u, float z, out Vector3 n)
        {
            float P = FoilPerim;
            u = Mathf.Repeat(u, P);
            float w2 = 2f * FoilW, h2 = 2f * FoilH;
            Vector2 p;
            if (u < w2) { p = new Vector2(-FoilW + u, FoilH); n = Vector3.up; }
            else if (u < w2 + h2) { p = new Vector2(FoilW, FoilH - (u - w2)); n = Vector3.right; }
            else if (u < 2f * w2 + h2) { p = new Vector2(FoilW - (u - w2 - h2), -FoilH); n = Vector3.down; }
            else { p = new Vector2(-FoilW, -FoilH + (u - 2f * w2 - h2)); n = Vector3.left; }
            float crinkle = (Mathf.PerlinNoise(u * 300f, z * 300f) - 0.5f) * 0.0005f;
            return new Vector3(p.x, FoilY + p.y, z) + n * crinkle;
        }

        static float FoilCut(int k, float u)   // рваная линия разрыва номер k
        {
            float[] baseZ = { -0.045f, -0.005f, 0.035f };
            u = Mathf.Repeat(u, FoilPerim);
            return baseZ[k] + (Mathf.PerlinNoise(u * 55f + k * 7.1f, k * 3.3f) - 0.5f) * 0.012f + Mathf.Sin(u * 420f + k) * 0.0012f;
        }

        // Кусок фольги: полоса периметра [u0, u1] между линиями z = lo(u) и z = hi(u); cap — закрыть торец.
        static Mesh FoilPiece(float u0, float u1, System.Func<float, float> lo, System.Func<float, float> hi, int cap, out Vector3 center)
        {
            float P = FoilPerim;
            var us = new List<float>();
            for (float u = u0; u < u1 - 0.0001f; u += 0.002f) us.Add(u);
            // Углы сечения — точно, иначе рубашка срезает рёбра.
            foreach (float c in new[] { 0f, 2f * FoilW, 2f * FoilW + 2f * FoilH, 4f * FoilW + 2f * FoilH })
                for (float c2 = c - P; c2 <= c + P; c2 += P)
                    if (c2 > u0 + 0.0002f && c2 < u1 - 0.0002f) us.Add(c2);
            us.Add(u1);
            us.Sort();
            int nu = us.Count, rows = 6;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int layer = 0; layer < 2; layer++)
            {
                int b = v.Count;
                for (int j = 0; j <= rows; j++)
                    foreach (float u in us)
                    {
                        float z = Mathf.Lerp(lo(u), hi(u), (float)j / rows);
                        v.Add(FoilAt(u, z, out _));
                    }
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < nu - 1; i++)
                    {
                        int i0 = b + j * nu + i, i1 = i0 + 1, i2 = i0 + nu, i3 = i2 + 1;
                        if (layer == 0) { t.Add(i0); t.Add(i2); t.Add(i1); t.Add(i1); t.Add(i2); t.Add(i3); }
                        else { t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i1); t.Add(i3); t.Add(i2); }
                    }
            }
            if (cap != 0)
            {
                float z = cap > 0 ? FoilZ1 + 0.0003f : FoilZ0 - 0.0003f;
                int b = v.Count;
                v.Add(new Vector3(-FoilW, FoilY - FoilH, z)); v.Add(new Vector3(-FoilW, FoilY + FoilH, z));
                v.Add(new Vector3(FoilW, FoilY + FoilH, z)); v.Add(new Vector3(FoilW, FoilY - FoilH, z));
                t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3, b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            center = Vector3.zero;
            foreach (var p in v) center += p;
            center /= v.Count;
            for (int i = 0; i < v.Count; i++) v[i] -= center;
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh SaveMesh(Mesh src, string name)
        {
            string path = MatDir + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { src.name = name; AssetDatabase.CreateAsset(src, path); return src; }
            EditorUtility.CopySerialized(src, mesh);
            mesh.name = name;
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Бумажная гильза: прямоугольная трубка без торцов. Снаружи этикетка-развёртка по периметру
        // [лицо | бок | оборот | бок], внутри — простая бумага (второй материал).
        static Mesh WrapperMesh(float hw, float hh, float len)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var outer = new List<int>();
            var inner = new List<int>();
            // Обход периметра: верх (лицо) слева направо, правый бок вниз, низ (оборот) справа налево, левый бок вверх.
            var corners = new[] { new Vector2(-hw, hh), new Vector2(hw, hh), new Vector2(hw, -hh), new Vector2(-hw, -hh) };
            float[] uStart = { 0f, 256f / 592f, 296f / 592f, 552f / 592f };
            float[] uEnd = { 256f / 592f, 296f / 592f, 552f / 592f, 1f };
            int segZ = 6;
            for (int f = 0; f < 4; f++)
            {
                var c0 = corners[f];
                var c1 = corners[(f + 1) % 4];
                int segA = f % 2 == 0 ? 8 : 1;
                for (int layer = 0; layer < 2; layer++)
                {
                    int b = v.Count;
                    for (int j = 0; j <= segZ; j++)
                        for (int i = 0; i <= segA; i++)
                        {
                            float k = (float)i / segA, z = Mathf.Lerp(-len / 2f, len / 2f, (float)j / segZ);
                            var p = Vector2.Lerp(c0, c1, k);
                            v.Add(new Vector3(p.x, p.y, z));
                            uv.Add(new Vector2(Mathf.Lerp(uStart[f], uEnd[f], k), (float)j / segZ));
                        }
                    var list = layer == 0 ? outer : inner;
                    int row = segA + 1;
                    for (int j = 0; j < segZ; j++)
                        for (int i = 0; i < segA; i++)
                        {
                            int i0 = b + j * row + i, i1 = i0 + 1, i2 = i0 + row, i3 = i2 + 1;
                            if (layer == 0) { list.Add(i0); list.Add(i2); list.Add(i1); list.Add(i1); list.Add(i2); list.Add(i3); }
                            else { list.Add(i0); list.Add(i1); list.Add(i2); list.Add(i1); list.Add(i3); list.Add(i2); }
                        }
                }
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.subMeshCount = 2;
            m.SetTriangles(outer, 0);
            m.SetTriangles(inner, 1);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static void Chocolate(Transform items, Vector3 pos, Vector3 euler)
        {
            var choc = Item(items, "Chocolate", ItemKind.Chocolate, "шоколадка «Алёнка»", pos, euler);
            var bar = choc.gameObject.AddComponent<ChocolateBar>();

            // Плитка: целая и надкушенная (вторая скрыта до первого укуса).
            var milk = Mat("ChocoBase", "#5A3521", 0.14f);
            GameObject BarObj(string name, bool b)
            {
                var go = new GameObject(name);
                go.transform.SetParent(choc, false);
                go.transform.localPosition = new Vector3(0, -0.0049f, 0);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(BarMesh(b), "ChocoBar" + name);
                go.AddComponent<MeshRenderer>().sharedMaterial = milk;
                return go;
            }
            bar.full = BarObj("Full", false);
            bar.bitten = BarObj("Bitten", true);
            bar.bitten.SetActive(false);

            // Фольга: куски по рваным линиям. Порядок отрыва: торец, потом верх/низ каждой полосы к руке.
            // Кусок у руки (за линией 0) остаётся на плитке.
            var foilMat = Mat("Foil", "#DCDFE2", 0.86f);
            foilMat.SetFloat("_Metallic", 0.15f);
            float P = FoilPerim;
            float uR = 2f * FoilW + FoilH, uL = 4f * FoilW + 3f * FoilH;     // середины боков — шов между верхом и низом
            var rips = new List<Transform>();
            Transform FoilObj(string name, float u0, float u1, System.Func<float, float> lo, System.Func<float, float> hi, int cap)
            {
                var mesh = FoilPiece(u0, u1, lo, hi, cap, out var c);
                var go = new GameObject(name);
                go.transform.SetParent(choc, false);
                go.transform.localPosition = c;
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, "ChocoFoil" + name);
                go.AddComponent<MeshRenderer>().sharedMaterial = foilMat;
                return go.transform;
            }
            rips.Add(FoilObj("End", 0f, P, u => FoilCut(2, u), u => FoilZ1, 1));
            for (int k = 1; k >= 0; k--)
            {
                int kk = k;
                rips.Add(FoilObj("Top" + k, uL - P, uR, u => FoilCut(kk, u), u => FoilCut(kk + 1, u), 0));
                rips.Add(FoilObj("Bottom" + k, uR, uL, u => FoilCut(kk, u), u => FoilCut(kk + 1, u), 0));
            }
            FoilObj("Hand", 0f, P, u => FoilZ0, u => FoilCut(0, u), -1);
            bar.foilRips = rips.ToArray();

            // Бумажная гильза «Алёнка» с открытыми концами (из-под неё видны торцы фольги). Снимается первой.
            var wrapGo = new GameObject("Wrapper");
            wrapGo.transform.SetParent(choc, false);
            wrapGo.transform.localPosition = new Vector3(0, FoilY, 0);
            wrapGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(WrapperMesh(0.0405f, 0.0068f, 0.124f), "ChocoWrapper");
            wrapGo.AddComponent<MeshRenderer>().sharedMaterials = new[] { Textured("AlenkaWrap", AlenkaTexture()), Mat("WrapInside", "#E6DCC8", 0.1f) };

            bar.wrapper = wrapGo.transform;
            Collider(choc, new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.06f, 0.22f));
            var p = choc.GetComponent<Pickup>();
            // Держим вертикально лицом к игроку, задний конец (за который берёшься) внизу, чуть откинута назад.
            var hold = Quaternion.LookRotation(new Vector3(0.05f, 1f, -0.28f).normalized, new Vector3(0f, 0f, -1f));
            p.holdEuler = hold.eulerAngles;
            p.holdOffset = new Vector3(0.17f, -0.06f, 0.5f);
            p.inspectOffset = new Vector3(0f, -0.02f, 0.2f);
            p.inspectEuler = hold.eulerAngles;
        }

        static void Folder(Transform items, Vector3 pos, Vector3 euler)
        {
            var folder = Item(items, "Folder", ItemKind.Folder, "папка с выписками", pos, euler);
            var manila = Mat("Manila", "#B9925A", 0.05f);
            var paperMat = Mat("Paper", "#E9E2D2", 0.05f);
            var ringMat = Mat("RingSteel", "#A9A79F", 0.55f);
            const float spineH = 0.025f;
            Box("Back", folder, new Vector3(0, 0.002f, 0), new Vector3(0.235f, 0.004f, 0.315f), manila, false);
            Box("Stack", folder, new Vector3(-0.003f, 0.0069f, -0.002f), new Vector3(0.21f, 0.0058f, 0.297f), paperMat, false);

            // Механизм у корешка: планка и два кольца, листы надеты на них дырками.
            float rx = FolderPages.RingX, ry = FolderPages.RingY, rr = FolderPages.RingR;
            Box("RingPlate", folder, new Vector3(rx, 0.005f, -0.002f), new Vector3(0.016f, 0.0016f, 0.24f), ringMat, false);
            var ringMesh = SaveMesh(TorusMesh(rr, 0.0008f, 36, 8), "BinderRing");
            foreach (float rz in new[] { -0.082f, 0.078f })
            {
                var ring = new GameObject("Ring");
                ring.transform.SetParent(folder, false);
                ring.transform.localPosition = new Vector3(rx, ry, rz);
                ring.AddComponent<MeshFilter>().sharedMesh = ringMesh;
                ring.AddComponent<MeshRenderer>().sharedMaterial = ringMat;
            }

            // Четыре листа на кольцах — при осмотре листаются (FolderPages). Точка поворота — в дырке.
            var hole = HoleTexture();
            var sheets = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Sheet" + (i + 1)).transform;
                pivot.SetParent(folder, false);
                FolderPages.Pose(i, 4, 0f, out var sp, out var sr);
                pivot.localPosition = sp;
                pivot.localRotation = sr;
                float cx = -0.105f + FolderPages.Margin;
                Box("Paper", pivot, new Vector3(cx, 0, 0), new Vector3(0.21f, 0.0006f, 0.297f), paperMat, false, new Vector3(0, i == 1 ? 0.6f : 0, 0));
                var page = UICanvas(pivot, "Page", new Vector3(cx, 0.00045f, 0), new Vector3(90, 180, 0), 0.21f, new Vector2(420, 594));
                // Лист рисуется ровным светлым — иначе в тени подвала текст не прочитать.
                UIRect(page, 0, 0, 420, 594, new Color(0.88f, 0.85f, 0.78f));
                if (i == 0) CasePage(page);
                else if (i == 1) StatementPage(page);
                else if (i == 2) SurveillancePage(page);
                else NotesPage(page);
                // Дырки под кольца (лист 2000 px/м, кольца на z = −0.082 и 0.078 от центра листа −0.002).
                foreach (float hy in new[] { 297f - 160f, 297f + 160f })
                    UIPicture(page, hole, 20f - 6f, hy - 6f, 12f, 12f);
                sheets[i] = pivot;
            }

            // Корешок на петле у нижнего края, обложка — на его верхнем крае. Раскрываются вместе на 180°:
            // корешок (Pickup.openPivot) ложится на стол, обложка за ним — внутренней стороной вверх.
            var spine = new GameObject("Spine").transform;
            spine.SetParent(folder, false);
            spine.localPosition = new Vector3(0.1175f, 0.003f, 0);
            Box("SpineBoard", spine, new Vector3(-0.0015f, spineH / 2f, 0), new Vector3(0.003f, spineH, 0.315f), manila, false);
            var hinge = new GameObject("CoverHinge").transform;
            hinge.SetParent(spine, false);
            hinge.localPosition = new Vector3(0, spineH, 0);
            Box("Cover", hinge, new Vector3(-0.1175f, 0.0015f, 0), new Vector3(0.235f, 0.003f, 0.315f), manila, false);
            Box("CoverPrint", hinge, new Vector3(-0.1175f, 0.0028f, 0), new Vector3(0.231f, 0.0014f, 0.311f), Textured("FolderCover", FolderCoverTexture()), false);
            Box("Tab", hinge, new Vector3(-0.241f, 0.0015f, 0.09f), new Vector3(0.012f, 0.003f, 0.06f), manila, false);
            Collider(folder, new Vector3(0, 0.017f, 0), new Vector3(0.26f, 0.04f, 0.34f));

            var fp = folder.GetComponent<Pickup>();
            fp.holdOffset = new Vector3(0.1f, -0.15f, 0.42f);
            fp.holdEuler = new Vector3(55f, 180f, 0);
            fp.inspectOffset = new Vector3(0f, 0f, 0.34f);
            fp.inspectEuler = new Vector3(90f, 180f, 0);
            fp.openPivot = spine;
            fp.openEuler = new Vector3(0, 0, -90f);
            fp.placedEuler = new Vector3(0, 180f, 0);   // перед ним — текстом к нему
            var pages = folder.gameObject.AddComponent<FolderPages>();
            pages.sheets = sheets;
            pages.spine = spine;
            pages.cover = hinge;
            folder.gameObject.AddComponent<DocFonts>().typewriter = true;
        }

        // Кольцо: тор в плоскости XY (ось — z).
        static Mesh TorusMesh(float R, float r, int seg, int side)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j <= side; j++)
                {
                    float b = j * Mathf.PI * 2f / side;
                    var nr = dir * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    v.Add(dir * R + nr * r);
                    n.Add(nr);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a0 = i * (side + 1) + j, a1 = a0 + side + 1;
                    tri.Add(a0); tri.Add(a1); tri.Add(a0 + 1);
                    tri.Add(a1); tri.Add(a1 + 1); tri.Add(a0 + 1);
                }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        static Texture2D HoleTexture()
        {
            var c = new Paint(32, 32, new Color(0, 0, 0, 0));
            c.Circle(16, 16, 15f, Hex("#7A7368"));
            c.Circle(16, 17, 12.5f, Hex("#3A342D"));
            return Save(c.Done(), "T_PunchHole");
        }

        static readonly Color Ink = new Color(0.07f, 0.065f, 0.06f);
        static readonly Color Pen = new Color(0.06f, 0.13f, 0.42f);

        static void CasePage(RectTransform page)
        {
            UIText(page, "СПРАВКА ПО ДЕЛУ", 30, 26, 360, 30, 22, Ink, FontStyle.Bold);
            UIText(page, "для М. Ланской, лично", 30, 56, 360, 20, 13, new Color(0.35f, 0.33f, 0.3f));
            UIRect(page, 30, 80, 360, 1.5f, Ink);
            UIText(page,
                "ЛАНСКОЙ Андрей — журналист. Писал, как\n" +
                "фирмы Кузнецова отмывают деньги через\n" +
                "городские стройки. Убит: сбит машиной 12.07.\n" +
                "Полиция: «несчастный случай».\n\n" +
                "КУЗНЕЦОВ Григорий («Кузня») — владелец\n" +
                "«Стройресурса», «Вектора-М», ЗАО «КиП».\n" +
                "Заплатил, чтобы Ланского убрали. Связи в полиции.\n\n" +
                "БЕЛЫХ Семён — его главбух 15 лет.\n" +
                "Ведёт все тёмные деньги. Не бандит.\n\n" +
                "ЯЧЕЙКА №212, банк «Северный» — флешка\n" +
                "и тетрадь Белых — доказательства вины\n" +
                "Кузнецова: куда шли его деньги и кому\n" +
                "он заплатил за убийство Ланского.", 30, 92, 370, 380, 15, Ink);
            UIText(page, "Код знает только Белых.", 30, 500, 370, 30, 18, Pen, FontStyle.BoldAndItalic);
        }

        static void StatementPage(RectTransform page)
        {
            UIText(page, "ВЫПИСКА ПО СЧЁТУ", 30, 28, 360, 34, 24, Ink, FontStyle.Bold);
            UIText(page, "Hellenic Bank · Лимасол, Кипр\nСчёт CY17 0050 0120 0003 8817 4410\nВладелец: БЕЛЫХ ИРИНА АРКАДЬЕВНА", 30, 62, 370, 60, 15, Ink);
            UIRect(page, 30, 126, 360, 1.5f, Ink);
            UIText(page, "дата        отправитель            сумма, ₽", 30, 132, 370, 18, 13, new Color(0.35f, 0.33f, 0.3f));
            string rows =
                "12.03.23  ООО «Стройресурс»    2 400 000\n" +
                "30.04.23  ООО «Вектор-М»       1 850 000\n" +
                "18.07.23  ЗАО «КиП»            3 100 000\n" +
                "02.10.23  ООО «Стройресурс»    2 950 000\n" +
                "21.01.24  ООО «Альфа-Логист»   4 200 000\n" +
                "11.04.24  ЗАО «КиП»            3 600 000\n" +
                "29.06.24  ООО «Вектор-М»       2 700 000\n" +
                "14.09.24  ООО «Стройресурс»    5 100 000\n" +
                "03.12.24  ЗАО «КиП»            4 400 000\n" +
                "  ...      ещё 9 переводов     10 900 000";
            UIText(page, rows, 30, 152, 370, 230, 15, Ink);
            UIRect(page, 30, 386, 360, 1.5f, Ink);
            UIText(page, "ИТОГО за 3 года:      41 200 000", 30, 394, 370, 22, 15, Ink, FontStyle.Bold);
            UIFrame(page, 228, 390, 168, 28, 2f, Pen);
            UIText(page, "Ирина — родная сестра Белых, живёт на Кипре.\nВсе фирмы-отправители — Кузнецова.\nОн три года обкрадывал хозяина —\nкопит на побег? Кузнецов не в курсе!", 34, 420, 360, 110, 16, Pen, FontStyle.BoldAndItalic);
            UIFrame(page, 300, 22, 96, 34, 2.5f, new Color(0.7f, 0.2f, 0.17f, 0.8f));
            UIText(page, "КОПИЯ", 300, 28, 96, 24, 15, new Color(0.7f, 0.2f, 0.17f, 0.85f), FontStyle.Bold, TextAnchor.UpperCenter);
            // Выписку раскопал журналист: Марина отдала её из его архива.
            var violet = new Color(0.33f, 0.22f, 0.52f, 0.8f);
            UIFrame(page, 214, 542, 182, 44, 2.5f, violet);
            UIText(page, "ИЗ АРХИВА\nА. ЛАНСКОГО", 214, 545, 182, 40, 15, violet, FontStyle.Bold, TextAnchor.UpperCenter);
        }

        static void SurveillancePage(RectTransform page)
        {
            UIText(page, "НАБЛЮДЕНИЕ · 15.09", 30, 28, 360, 30, 22, Ink, FontStyle.Bold);
            UIText(page, "объект: Белых С. А.", 30, 58, 360, 20, 14, new Color(0.35f, 0.33f, 0.3f));
            // Фото слежки: gemini-3.1-flash-image через OpenRouter, образцы — фото с доски.
            var shot = PhotoTex("surveil_bank");
            UIPicture(page, shot != null ? shot : BankPhotoTexture(), 50, 90, 320, 240);
            UIFrame(page, 50, 90, 320, 240, 6f, new Color(0.93f, 0.91f, 0.86f));
            UIText(page, "11:42 — выходит из банка «Северный»\n(ул. Садовая, 8) с портфелем.\n\nОхранник: «в депозитарий ходит раз\nв месяц, всегда к ячейке 212».\n\nКод ячейки — 4 цифры.\nЗнает только он.", 30, 350, 370, 200, 16, Ink);
            UIText(page, "думай, что для него важно", 150, 548, 260, 30, 16, Pen, FontStyle.BoldAndItalic);
        }

        static void NotesPage(RectTransform page)
        {
            UIText(page, "БЕЛЫХ Семён Аркадьевич", 30, 28, 370, 30, 20, Ink, FontStyle.Bold);
            UIText(page,
                "— 52 года, главбух Кузнецова с 2009-го.\n" +
                "— Тихий, без охраны, не бандит.\n" +
                "— Диабет 1 типа: без сахара «плывёт».\n" +
                "— Жена Вера умерла в 2019-м.\n" +
                "— Дочь Ника, 19, учится в Питере.\n   Живёт ради неё.\n" +
                "— Кузнецова боится больше смерти.", 30, 70, 370, 250, 17, Pen, FontStyle.BoldAndItalic);
            UIText(page, "Бить бесполезно — начнёт врать со страху.\nПо-человечески. Вода, сахар.", 30, 330, 370, 70, 17, Pen, FontStyle.BoldAndItalic);
            UIText(page, "Узнает Кузнецов, что Белых его обкрадывал, — убьёт.\nПредложить выход: защита для Ники.", 30, 410, 370, 60, 17, Pen, FontStyle.BoldAndItalic);
            UIRect(page, 30, 512, 140, 2f, Pen);
        }

        static void Phone(Transform items, Vector3 pos, Vector3 euler)
        {
            var phone = Item(items, "SuspectPhone", ItemKind.SuspectPhone, "телефон Белых", pos, euler);
            var frame = Mat("PhoneFrame", "#6D6E70", 0.8f);
            frame.SetFloat("_Metallic", 0.15f);
            Box("Frame", phone, new Vector3(0, 0.0045f, 0), new Vector3(0.074f, 0.0075f, 0.152f), frame, false);
            Box("Back", phone, new Vector3(0, 0.0035f, 0), new Vector3(0.0725f, 0.007f, 0.1505f), Mat("PhoneBody", "#1C1C1E", 0.7f), false);
            Box("Glass", phone, new Vector3(0, 0.0083f, 0), new Vector3(0.0715f, 0.0006f, 0.1495f), Mat("PhoneGlass", "#0A0B0C", 0.95f), false);
            Box("BtnPower", phone, new Vector3(0.0375f, 0.0045f, 0.02f), new Vector3(0.002f, 0.003f, 0.018f), frame, false);
            Box("BtnVolUp", phone, new Vector3(-0.0375f, 0.0045f, 0.03f), new Vector3(0.002f, 0.003f, 0.012f), frame, false);
            Box("BtnVolDown", phone, new Vector3(-0.0375f, 0.0045f, 0.012f), new Vector3(0.002f, 0.003f, 0.012f), frame, false);
            Box("Camera", phone, new Vector3(-0.018f, -0.0005f, 0.05f), new Vector3(0.03f, 0.002f, 0.03f), Mat("PhoneBody", "#1C1C1E", 0.7f), false);

            // Экран — живой интерфейс.
            var ui = UICanvas(phone, "Screen", new Vector3(0, 0.0088f, 0), new Vector3(90, 180, 0), 0.066f, new Vector2(330, 710));
            UIRect(ui, 0, 0, 330, 710, new Color(0.055f, 0.075f, 0.09f));
            var white = new Color(0.92f, 0.94f, 0.95f);
            var grey = new Color(0.5f, 0.56f, 0.6f);
            var red = new Color(0.9f, 0.3f, 0.26f);
            UIText(ui, "04:30", 22, 14, 120, 30, 20, white, FontStyle.Bold).gameObject.name = "Clock";
            UIText(ui, "▮▮▮  23%", 190, 16, 120, 26, 16, white, FontStyle.Normal, TextAnchor.UpperRight);
            UIRect(ui, 120, 14, 90, 26, Color.black);

            var calls = UIGroup(ui, "Calls");
            UIText(calls, "Недавние", 22, 60, 300, 44, 32, white, FontStyle.Bold);
            CallRow(calls, 0, "Григорий Кузнецов (14)", "пропущенный · 02:41", red, grey);
            CallRow(calls, 1, "Григорий Кузнецов (3)", "пропущенный · 01:58", red, grey);
            CallRow(calls, 2, "Ника, дочь", "входящий · 23:10 · 4 мин", white, grey);
            CallRow(calls, 3, "Ирина, сестра (Кипр)", "исходящий · 21:02 · 1 мин", white, grey);
            CallRow(calls, 4, "Банк «Северный»", "входящий · вчера", white, grey);

            var msgs = UIGroup(ui, "Messages");
            UIText(msgs, "Сообщения", 22, 60, 300, 44, 32, white, FontStyle.Bold);
            msgY = 120f;
            MsgRow(msgs, "<color=#E8574B>Григорий Кузнецов</color>  · 02:41", "Ты где? Выйди на связь.\nМои люди уже у дверей Ники. Чуть что — и ты её больше никогда не увидишь.", white, grey, new Color(0.30f, 0.14f, 0.13f));
            MsgRow(msgs, "Ника, дочь  · 23:58", "пап, ты обещал позвонить. я не сплю. люблю тебя", white, grey);
            MsgRow(msgs, "Ирина, сестра  · 21:05", "всё дошло, Сёма. может, хватит уже? приезжайте с Никой", white, grey);
            MsgRow(msgs, "Банк «Северный»  · вчера", "Аренда ячейки №212 истекает 01.12. Продлите в отделении.", white, grey);

            var contacts = UIGroup(ui, "Contacts");
            UIText(contacts, "Контакты", 22, 60, 300, 44, 32, white, FontStyle.Bold);
            string[] names = { "Григорий Кузнецов", "Ирина, сестра (Кипр)", "Мама", "Ника, дочь", "Банк «Северный», депозитарий" };
            for (int i = 0; i < names.Length; i++)
            {
                float y = 128 + i * 82;
                UIRect(contacts, 22, y + 74, 286, 1, new Color(1f, 1f, 1f, 0.08f));
                UIText(contacts, names[i], 22, y + 10, 290, 34, 22, white);
            }

            // Вкладки внизу.
            string[] tabs = { "Звонки", "Сообщения", "Контакты" };
            for (int i = 0; i < 3; i++)
            {
                UIText(ui, tabs[i], 10 + i * 105, 650, 105, 26, 15, white, FontStyle.Normal, TextAnchor.UpperCenter);
                UIRect(ui, 32 + i * 105, 682, 60, 3, white).gameObject.name = "Tab" + i;
            }
            phone.gameObject.AddComponent<PhoneUI>();   // экраны находит сам по именам
            phone.gameObject.AddComponent<DocFonts>().typewriter = false;

            Collider(phone, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.06f, 0.18f));
            var pp = phone.GetComponent<Pickup>();
            pp.holdEuler = new Vector3(62f, 180f, 0);
            pp.holdOffset = new Vector3(0.11f, -0.11f, 0.34f);
            pp.inspectOffset = new Vector3(0f, -0.005f, 0.2f);
            pp.inspectEuler = new Vector3(84f, 180f, 0);
            pp.placedEuler = new Vector3(0, 180f, 0);
        }

        static void CallRow(RectTransform parent, int i, string name, string sub, Color nameColor, Color subColor)
        {
            float y = 128 + i * 90;
            UIText(parent, name, 22, y + 8, 290, 34, 24, nameColor);
            UIText(parent, sub, 22, y + 42, 290, 28, 16, subColor);
            UIRect(parent, 22, y + 82, 286, 1, new Color(1f, 1f, 1f, 0.08f));
        }

        static float msgY;

        // Сообщения с высотой пузыря по длине текста; следующая строка идёт ниже предыдущей.
        static void MsgRow(RectTransform parent, string head, string text, Color c, Color sub, Color? bubble = null)
        {
            int lines = 0;
            foreach (var part in text.Split('\n')) lines += Mathf.Max(1, Mathf.CeilToInt(part.Length / 25f));
            float bh = lines * 23f + 18f;
            UIText(parent, head, 22, msgY, 290, 28, 17, sub);
            UIRect(parent, 18, msgY + 28f, 296, bh, bubble ?? new Color(0.17f, 0.21f, 0.24f));
            UIText(parent, text, 30, msgY + 36f, 276, bh, 18, c);
            msgY += 28f + bh + 12f;
        }

        static void Photo(Transform items, Vector3 pos, Vector3 euler)
        {
            var photo = Item(items, "Photo", ItemKind.Photo, "фото дочери", pos, euler);
            var flip = new GameObject("Flip").transform;
            flip.SetParent(photo, false);
            flip.localPosition = new Vector3(0, 0.0012f, 0);
            // Фото сгенерировано нейросетью (OpenRouter, gemini-3.1-flash-image), лежит в Textures/nika_photo.png.
            var nika = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/nika_photo.png");
            // Подсвечивается, когда в руке или лежит перед ним (через Pickup.screen) — иначе в полумраке не разглядеть.
            var print = Box("Print", flip, Vector3.zero, new Vector3(0.09f, 0.0016f, 0.122f), EmissiveTex("PhotoNika", nika != null ? nika : NikaPhotoTexture()), false);
            // Оборот: надпись её рукой.
            Box("BackPaper", flip, new Vector3(0, -0.0011f, 0), new Vector3(0.088f, 0.0002f, 0.12f), Mat("PhotoBack", "#EEEAE0", 0.1f), false);
            var back = UICanvas(flip, "Back", new Vector3(0, -0.0013f, 0), new Vector3(-90, 0, 0), 0.088f, new Vector2(264, 360));
            UIRect(back, 0, 0, 264, 360, new Color(0.9f, 0.88f, 0.83f));
            UIText(back, "Папе.\nНе грусти.\n\n           Н.", 30, 70, 220, 220, 34, Pen, FontStyle.BoldAndItalic);
            UIText(back, "выпускной, июнь", 30, 300, 220, 30, 16, new Color(0.45f, 0.43f, 0.4f));
            photo.gameObject.AddComponent<PhotoFlip>().pivot = flip;
            photo.gameObject.AddComponent<DocFonts>().typewriter = true;
            Collider(photo, new Vector3(0, 0.02f, 0), new Vector3(0.13f, 0.05f, 0.16f));
            var php = photo.GetComponent<Pickup>();
            php.holdEuler = new Vector3(70f, 180f, 0);
            php.holdOffset = new Vector3(0.08f, -0.1f, 0.3f);
            php.inspectOffset = new Vector3(0f, -0.005f, 0.19f);
            php.inspectEuler = new Vector3(84f, 180f, 0);
            php.placedEuler = new Vector3(0, 180f, 0);
            php.screen = print.GetComponent<Renderer>();
            php.screenColor = new Color(0.5f, 0.48f, 0.45f);
        }

        static void Pistol(Transform items, Vector3 pos, Vector3 euler)
        {
            var gun = Item(items, "Pistol", ItemKind.Pistol, "пистолет", pos, euler);
            var model = new GameObject("Model").transform;   // модель «стоит»; лежащий вид — поворот корня
            model.SetParent(gun, false);
            // Без металлика: в подвале нечего отражать, и «металл» выходит чёрным пятном.
            var steel = Mat("Gun", "#6B7078", 0.62f);
            var dark = Mat("GunDark", "#33363A", 0.5f);
            var grip = Mat("GunGrip", "#3B2A20", 0.35f);

            Box("Slide", model, new Vector3(0, 0.034f, 0.008f), new Vector3(0.029f, 0.027f, 0.162f), steel, false);
            Box("SlideTop", model, new Vector3(0, 0.0485f, 0.008f), new Vector3(0.02f, 0.003f, 0.158f), steel, false);
            for (int i = 0; i < 7; i++)    // насечки на затворе
                Box("Serration", model, new Vector3(0, 0.036f, -0.06f + i * 0.005f), new Vector3(0.0305f, 0.018f, 0.0018f), dark, false);
            Box("Ejection", model, new Vector3(0.0146f, 0.038f, 0.02f), new Vector3(0.001f, 0.01f, 0.03f), dark, false);
            var barrel = Cyl("Barrel", model, new Vector3(0, 0.034f, 0.088f), new Vector3(0.013f, 0.002f, 0.013f), dark);
            barrel.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var bore = Cyl("Bore", model, new Vector3(0, 0.034f, 0.0902f), new Vector3(0.007f, 0.001f, 0.007f), Mat("Dark", "#1E1B19", 0.3f));
            bore.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Box("Frame", model, new Vector3(0, 0.014f, 0.012f), new Vector3(0.026f, 0.016f, 0.14f), steel, false);
            Box("FrontSight", model, new Vector3(0, 0.052f, 0.082f), new Vector3(0.004f, 0.006f, 0.006f), steel, false);
            Box("RearSightL", model, new Vector3(-0.006f, 0.052f, -0.066f), new Vector3(0.006f, 0.006f, 0.006f), steel, false);
            Box("RearSightR", model, new Vector3(0.006f, 0.052f, -0.066f), new Vector3(0.006f, 0.006f, 0.006f), steel, false);
            Box("Hammer", model, new Vector3(0, 0.038f, -0.077f), new Vector3(0.009f, 0.016f, 0.01f), dark, false, new Vector3(-25, 0, 0));
            Box("Safety", model, new Vector3(-0.0155f, 0.036f, -0.05f), new Vector3(0.003f, 0.007f, 0.016f), dark, false);
            // Скоба и спуск.
            Box("GuardBottom", model, new Vector3(0, -0.012f, 0.018f), new Vector3(0.009f, 0.004f, 0.05f), steel, false);
            Box("GuardFront", model, new Vector3(0, -0.002f, 0.042f), new Vector3(0.009f, 0.02f, 0.004f), steel, false, new Vector3(-15, 0, 0));
            Box("Trigger", model, new Vector3(0, -0.001f, 0.016f), new Vector3(0.005f, 0.016f, 0.006f), dark, false, new Vector3(18, 0, 0));
            // Рукоять с бакелитовыми щёчками и рифлением.
            var gripPivot = new GameObject("Grip").transform;
            gripPivot.SetParent(model, false);
            gripPivot.localPosition = new Vector3(0, 0.006f, -0.05f);
            gripPivot.localRotation = Quaternion.identity;   // рукоять прямая, без наклона
            Box("Core", gripPivot, new Vector3(0, -0.04f, 0), new Vector3(0.024f, 0.085f, 0.036f), steel, false);
            Box("PanelL", gripPivot, new Vector3(-0.0135f, -0.038f, 0), new Vector3(0.004f, 0.07f, 0.032f), grip, false);
            Box("PanelR", gripPivot, new Vector3(0.0135f, -0.038f, 0), new Vector3(0.004f, 0.07f, 0.032f), grip, false);
            for (int i = 0; i < 6; i++)
            {
                Box("GrooveL", gripPivot, new Vector3(-0.0157f, -0.012f - i * 0.01f, 0), new Vector3(0.0008f, 0.002f, 0.028f), dark, false);
                Box("GrooveR", gripPivot, new Vector3(0.0157f, -0.012f - i * 0.01f, 0), new Vector3(0.0008f, 0.002f, 0.028f), dark, false);
            }
            Box("MagBase", gripPivot, new Vector3(0, -0.085f, 0.002f), new Vector3(0.025f, 0.007f, 0.04f), dark, false);
            Box("Lanyard", gripPivot, new Vector3(0, -0.082f, -0.02f), new Vector3(0.006f, 0.008f, 0.006f), steel, false);

            // Рука детектива — видна только когда пистолет в руке. Пальцы обхватывают рукоять.
            var held = new GameObject("Held").transform;
            held.SetParent(gripPivot, false);
            var skinHand = Mat("DetectiveSkin", "#B88E70", 0.2f);
            var knuckle = Mat("DetectiveKnuckle", "#A97F62", 0.2f);
            Box("Palm", held, new Vector3(0.006f, -0.042f, -0.027f), new Vector3(0.034f, 0.074f, 0.02f), skinHand, false);
            Box("PalmSide", held, new Vector3(0.019f, -0.042f, -0.008f), new Vector3(0.008f, 0.07f, 0.034f), skinHand, false);
            for (int i = 0; i < 3; i++)
            {
                float y = -0.026f - i * 0.017f;
                Box("Finger" + i, held, new Vector3(0.004f, y, 0.022f), new Vector3(0.036f, 0.0145f, 0.012f), i % 2 == 0 ? skinHand : knuckle, false);
                Box("Tip" + i, held, new Vector3(-0.0165f, y, 0.012f), new Vector3(0.008f, 0.013f, 0.016f), skinHand, false);
            }
            Box("Thumb", held, new Vector3(-0.0175f, 0.004f, -0.006f), new Vector3(0.011f, 0.013f, 0.046f), skinHand, false, new Vector3(14, 6, 0));
            Box("Index", model, new Vector3(0.012f, -0.002f, 0.012f), new Vector3(0.011f, 0.011f, 0.034f), skinHand, false, new Vector3(10, -8, 0)).transform.SetParent(held, true);
            Box("Wrist", held, new Vector3(0.008f, -0.085f, -0.05f), new Vector3(0.042f, 0.045f, 0.07f), skinHand, false, new Vector3(-38, 0, 0));
            Box("Cuff", held, new Vector3(0.009f, -0.112f, -0.085f), new Vector3(0.052f, 0.052f, 0.02f), Mat("ShirtCuff", "#D9D4C8", 0.1f), false, new Vector3(-38, 0, 0));
            Box("Sleeve", held, new Vector3(0.01f, -0.15f, -0.135f), new Vector3(0.07f, 0.07f, 0.11f), Mat("Coat", "#3A3630", 0.05f), false, new Vector3(-38, 0, 0));
            held.gameObject.SetActive(false);

            // На верстаке лежит на боку.
            gun.localRotation = Quaternion.Euler(euler.x, euler.y, 90);
            gun.localPosition += new Vector3(0, 0.016f, 0);
            Collider(gun, new Vector3(0, 0.02f, 0), new Vector3(0.12f, 0.12f, 0.22f));
            var gp = gun.GetComponent<Pickup>();
            gp.holdOffset = new Vector3(0.15f, -0.13f, 0.42f);
            gp.holdEuler = new Vector3(-3f, -10f, 0f);
            gp.inspectOffset = new Vector3(0.02f, -0.03f, 0.3f);
            gp.inspectEuler = new Vector3(0f, -80f, 0f);
            gp.placedEuler = new Vector3(0, 180, 90);
        }

        // ---------- надписи на предметах (world-space uGUI) ----------

        static RectTransform UICanvas(Transform parent, string name, Vector3 localPos, Vector3 euler, float widthMeters, Vector2 px)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<UnityEngine.Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 4f;   // чётче буквы на маленьком холсте
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = px;
            rt.localPosition = localPos;
            rt.localRotation = Quaternion.Euler(euler);
            float k = widthMeters / px.x;
            rt.localScale = new Vector3(k, k, k);
            return rt;
        }

        static RectTransform UIGroup(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform UIBox(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Text UIText(RectTransform parent, string text, float x, float y, float w, float h, int size, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var t = UIBox(parent, "Text", x, y, w, h).gameObject.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = anchor;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.1f;
            t.raycastTarget = false;
            return t;
        }

        static Image UIRect(RectTransform parent, float x, float y, float w, float h, Color c)
        {
            var img = UIBox(parent, "Rect", x, y, w, h).gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static void UIFrame(RectTransform parent, float x, float y, float w, float h, float t, Color c)
        {
            UIRect(parent, x, y, w, t, c);
            UIRect(parent, x, y + h - t, w, t, c);
            UIRect(parent, x, y, t, h, c);
            UIRect(parent, x + w - t, y, t, h, c);
        }

        static void UIPicture(RectTransform parent, Texture2D tex, float x, float y, float w, float h)
        {
            var img = UIBox(parent, "Picture", x, y, w, h).gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
        }

        // ---------- текстуры, нарисованные кодом ----------

        class Paint
        {
            public readonly Texture2D tex;
            readonly int w, h;
            public Paint(int w, int h, Color bg)
            {
                this.w = w; this.h = h;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
                var px = new Color[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = bg;
                tex.SetPixels(px);
            }
            // Координаты сверху вниз, как на бумаге.
            public void Rect(float x, float y, float rw, float rh, Color c)
            {
                for (int j = Mathf.Max(0, (int)y); j < Mathf.Min(h, (int)(y + rh)); j++)
                    for (int i = Mathf.Max(0, (int)x); i < Mathf.Min(w, (int)(x + rw)); i++)
                        Blend(i, j, c);
            }
            public void Frame(float x, float y, float rw, float rh, float t, Color c)
            {
                Rect(x, y, rw, t, c); Rect(x, y + rh - t, rw, t, c); Rect(x, y, t, rh, c); Rect(x + rw - t, y, t, rh, c);
            }
            public void Circle(float cx, float cy, float r, Color c)
            {
                for (int j = (int)(cy - r - 1); j <= (int)(cy + r + 1); j++)
                    for (int i = (int)(cx - r - 1); i <= (int)(cx + r + 1); i++)
                    {
                        if (i < 0 || j < 0 || i >= w || j >= h) continue;
                        float d = Mathf.Sqrt((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy));
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        if (a > 0f) Blend(i, j, new Color(c.r, c.g, c.b, c.a * a));
                    }
            }
            public void Line(float x0, float y0, float x1, float y1, float t, Color c)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f);
                for (int k = 0; k <= steps; k++)
                {
                    float f = steps == 0 ? 0 : (float)k / steps;
                    Circle(Mathf.Lerp(x0, x1, f), Mathf.Lerp(y0, y1, f), t * 0.5f, c);
                }
            }
            // Строка «текста»: серые штрихи-слова разной длины.
            public void TextLine(float x, float y, float width, float hgt, Color c, int seed)
            {
                var rng = new System.Random(seed);
                float cx = x;
                while (cx < x + width)
                {
                    float wl = 4 + (float)rng.NextDouble() * 14;
                    if (cx + wl > x + width) wl = x + width - cx;
                    Rect(cx, y, wl, hgt, c);
                    cx += wl + 3;
                }
            }
            void Blend(int i, int j, Color c)
            {
                int yy = h - 1 - j;
                var o = tex.GetPixel(i, yy);
                tex.SetPixel(i, yy, Color.Lerp(o, new Color(c.r, c.g, c.b, 1f), c.a));
            }
            public Texture2D Done() { tex.Apply(true); return tex; }
        }

        static Color Hex(string hex, float a = 1f) { ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c; }

        // Этикетка-развёртка нарисована скриптом (PIL) в Textures/alenka_wrap.png: лицо, бок, оборот, бок.
        static Texture2D AlenkaTexture()
        {
            string path = Root + "/Textures/alenka_wrap.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && (imp.npotScale != TextureImporterNPOTScale.None || imp.maxTextureSize < 2048 || imp.wrapMode != TextureWrapMode.Clamp))
            {
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.maxTextureSize = 2048;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D NikaPhotoTexture()
        {
            var c = new Paint(120, 160, Hex("#EEE9DE"));
            // Небо и сад за спиной.
            for (int y = 8; y < 130; y++)
                c.Rect(8, y, 104, 1, Color.Lerp(Hex("#D9C7A6"), Hex("#6E8462"), Mathf.InverseLerp(20, 110, y)));
            c.Circle(26, 78, 20, Hex("#5B7350"));
            c.Circle(96, 70, 24, Hex("#56704C"));
            // Девушка: волосы, лицо, белая блузка, красная выпускная лента.
            c.Circle(60, 58, 21, Hex("#4A3022"));
            c.Rect(39, 58, 42, 34, Hex("#4A3022"));
            c.Circle(60, 60, 15, Hex("#EDC2A0"));
            c.Rect(34, 88, 52, 42, Hex("#F4F1EA"));
            c.Circle(60, 92, 26, Hex("#F4F1EA"));
            c.Line(40, 86, 80, 128, 8, Hex("#C0392B"));
            c.Circle(54, 58, 1.6f, Hex("#2A1C14"));
            c.Circle(66, 58, 1.6f, Hex("#2A1C14"));
            c.Line(54, 67, 66, 67, 2f, Hex("#B4554A"));
            c.Rect(8, 130, 104, 0, Hex("#EEE9DE"));
            return Save(c.Done(), "T_NikaPhoto");
        }

        static Texture2D BankPhotoTexture()
        {
            var c = new Paint(120, 90, Hex("#E8E3D8"));
            c.Rect(5, 5, 110, 80, Hex("#6F7472"));
            c.Rect(5, 5, 110, 26, Hex("#8C918E"));
            for (int i = 0; i < 5; i++) c.Rect(14 + i * 20, 12, 10, 14, Hex("#4E5452"));
            c.Rect(38, 34, 44, 51, Hex("#3E4442"));
            c.Rect(46, 20, 28, 6, Hex("#C9A44B"));
            // Фигура с портфелем.
            c.Circle(86, 50, 5, Hex("#2B2E2D"));
            c.Rect(81, 55, 10, 22, Hex("#2B2E2D"));
            c.Rect(92, 66, 8, 6, Hex("#3B2A22"));
            c.Circle(86, 50, 9, Hex("#D23C2C", 0f));
            c.Frame(76, 40, 22, 40, 1.5f, Hex("#D23C2C"));
            return Save(c.Done(), "T_BankPhoto");
        }

        static Texture2D PaperTexture()
        {
            var c = new Paint(140, 198, Hex("#EFE9DC"));
            c.Rect(12, 12, 60, 7, Hex("#4A4640"));
            c.TextLine(12, 24, 90, 3, Hex("#8A847A"), 1);
            c.Frame(96, 10, 34, 20, 2, Hex("#B0362B", 0.8f));
            c.Rect(100, 18, 26, 3, Hex("#B0362B", 0.7f));
            // Таблица переводов.
            c.Rect(12, 40, 116, 1, Hex("#6E685E"));
            for (int r = 0; r < 14; r++)
            {
                float y = 46 + r * 9;
                c.TextLine(12, y, 30, 3, Hex("#6E685E"), r * 7 + 2);
                c.TextLine(48, y, 46, 3, Hex("#8A847A"), r * 7 + 3);
                c.Rect(104, y, 22, 3, Hex("#3D3A35"));
                c.Rect(12, y + 6, 116, 1, Hex("#D4CCBB"));
            }
            // Итог, обведённый ручкой.
            c.Frame(98, 172, 32, 10, 1.5f, Hex("#2E4C8C"));
            c.Rect(102, 176, 24, 3, Hex("#3D3A35"));
            c.Line(14, 182, 30, 176, 1.2f, Hex("#2E4C8C"));
            c.Line(30, 176, 44, 184, 1.2f, Hex("#2E4C8C"));
            c.Line(44, 184, 60, 177, 1.2f, Hex("#2E4C8C"));
            return Save(c.Done(), "T_Statement");
        }

        static Texture2D FolderCoverTexture()
        {
            var c = new Paint(140, 188, Hex("#C29B60"));
            c.Rect(0, 0, 140, 4, Hex("#A8834D"));
            c.Rect(30, 30, 80, 46, Hex("#EDE6D5"));
            c.Frame(30, 30, 80, 46, 1.5f, Hex("#8C7350"));
            c.TextLine(38, 40, 64, 4, Hex("#3D3A35"), 11);
            c.TextLine(38, 52, 50, 3, Hex("#6E685E"), 12);
            c.TextLine(38, 62, 58, 3, Hex("#6E685E"), 13);
            // Красный штамп наискосок.
            for (int k = 0; k < 3; k++)
                c.Line(34 + k, 128 - k, 106 + k, 104 - k, 1.6f, Hex("#B0362B", 0.75f));
            c.Line(30, 120, 102, 96, 1.4f, Hex("#B0362B", 0.6f));
            c.Line(40, 138, 112, 114, 1.4f, Hex("#B0362B", 0.6f));
            c.Line(30, 120, 40, 138, 1.4f, Hex("#B0362B", 0.6f));
            c.Line(102, 96, 112, 114, 1.4f, Hex("#B0362B", 0.6f));
            // Потёртости.
            var rng = new System.Random(5);
            for (int i = 0; i < 40; i++) c.Circle((float)rng.NextDouble() * 140, (float)rng.NextDouble() * 188, 1 + (float)rng.NextDouble() * 3, Hex("#A8834D", 0.25f));
            return Save(c.Done(), "T_FolderCover");
        }

        static Texture2D PhoneScreenTexture()
        {
            var c = new Paint(100, 216, Hex("#0E1418"));
            c.Rect(8, 6, 20, 4, Hex("#C9D1D6"));
            c.Rect(74, 6, 18, 4, Hex("#C9D1D6"));
            c.Rect(36, 4, 28, 8, Hex("#000000"));
            c.Rect(8, 22, 50, 6, Hex("#E8EDF0"));
            // Пропущенные от «Г.К.».
            for (int i = 0; i < 4; i++)
            {
                float y = 40 + i * 20;
                c.Circle(16, y + 6, 6, Hex("#2A3238"));
                c.Line(13, y + 4, 19, y + 8, 2f, Hex("#E2453A"));
                c.Rect(28, y + 1, 30, 4, Hex("#E2453A"));
                c.Rect(28, y + 8, 22, 3, Hex("#6F7A82"));
                c.Rect(78, y + 3, 14, 3, Hex("#6F7A82"));
                c.Rect(8, y + 17, 84, 1, Hex("#1E262C"));
            }
            c.Rect(28, 124, 40, 3, Hex("#6F7A82"));
            // СМС-пузырь с угрозой.
            c.Rect(8, 140, 72, 40, Hex("#2F3A42"));
            c.Circle(8, 180, 5, Hex("#2F3A42"));
            c.TextLine(14, 148, 60, 3, Hex("#E8EDF0"), 21);
            c.TextLine(14, 156, 56, 3, Hex("#E8EDF0"), 22);
            c.TextLine(14, 164, 48, 3, Hex("#E8EDF0"), 23);
            c.Rect(14, 172, 20, 3, Hex("#E2453A"));
            c.Rect(36, 202, 28, 3, Hex("#C9D1D6"));
            // Трещины от угла.
            var crack = Hex("#DDE5EA", 0.75f);
            c.Line(100, 150, 70, 172, 1f, crack);
            c.Line(70, 172, 52, 168, 1f, crack);
            c.Line(70, 172, 64, 198, 1f, crack);
            c.Line(86, 160, 96, 186, 1f, crack);
            c.Line(64, 198, 72, 216, 1f, crack);
            return Save(c.Done(), "T_PhoneScreen");
        }

        static Texture2D Save(Texture2D tex, string name)
        {
            string path = MatDir + "/" + name + ".asset";
            // Обновляем на месте: удаление + создание заново заставляет iCloud плодить копии «… 2».
            var old = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (old == null) { AssetDatabase.CreateAsset(tex, path); return tex; }
            if (old.width == tex.width && old.height == tex.height && old.format == tex.format)
            {
                old.SetPixels32(tex.GetPixels32());
                old.wrapMode = tex.wrapMode;
                old.Apply(true);
                EditorUtility.SetDirty(old);
                return old;
            }
            EditorUtility.CopySerialized(tex, old);
            return old;
        }

        static Material Textured(string name, Texture2D tex)
        {
            var m = Mat(name, "#FFFFFF", 0.15f);
            m.mainTexture = tex;
            return m;
        }

        static Material EmissiveTex(string name, Texture2D tex)
        {
            var m = Mat(name, "#FFFFFF", 0.9f);
            m.mainTexture = tex;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;   // иначе Unity сбрасывает свечение
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", Color.white * 0.2f);
            return m;
        }

        static Material Transparent(string name, Color c, float smooth)
        {
            var m = Mat(name, "#FFFFFF", smooth);
            m.color = c;
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)BlendMode.One);
            m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            return m;
        }

        static Transform Item(Transform parent, string name, ItemKind kind, string title, Vector3 pos, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            var p = go.AddComponent<Pickup>();
            p.kind = kind;
            p.title = title;
            return go.transform;
        }

        static void Collider(Transform t, Vector3 center, Vector3 size)
        {
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
            c.isTrigger = true;
        }

        // ---------- игрок и системы ----------

        static PlayerController Player()
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0.3f, 0.05f, -2.6f);
            go.transform.rotation = Quaternion.Euler(0, -5f, 0);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.28f;
            cc.center = new Vector3(0, 0.875f, 0);
            cc.stepOffset = 0.3f;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(go.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.62f, 0);
            camGo.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 30f;
            cam.fieldOfView = 62f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.025f, 0.02f);
            camGo.AddComponent<AudioListener>();

            var hand = new GameObject("Hand").transform;
            hand.SetParent(camGo.transform, false);
            // Слабый свет у руки: иначе тёмный пистолет или телефон в руке не видно.
            var handLight = new GameObject("HandLight").AddComponent<Light>();
            handLight.transform.SetParent(camGo.transform, false);
            handLight.transform.localPosition = new Vector3(0.12f, 0.02f, 0.22f);
            handLight.type = LightType.Point;
            handLight.range = 0.7f;
            handLight.intensity = 0.8f;
            handLight.color = new Color(1f, 0.85f, 0.65f);

            var pc = go.AddComponent<PlayerController>();
            pc.cam = cam;
            pc.hand = hand;
            return pc;
        }

        static void Systems(PlayerController player)
        {
            var go = new GameObject("Game");
            go.AddComponent<GameDirector>();
            var talk = go.AddComponent<Interrogation>();
            var friend = go.AddComponent<BankFriend>();
            var contacts = go.AddComponent<Contacts>();
            var hud = go.AddComponent<HUD>();
            hud.contacts = contacts;
            hud.player = player;
            hud.talk = talk;
            hud.friend = friend;
            go.AddComponent<AudioSource>();
            go.AddComponent<Ambience>();
            go.AddComponent<AdminPanel>();   // F2 — настройки партии
            new GameObject("Spectate").AddComponent<Spectate>();   // наблюдение за игрой по коду комнаты
        }

        // ---------- помощники геометрии ----------

        static Transform Pivot(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject CylBetween(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material m)
        {
            var go = Cyl(name, parent, (a + b) / 2f, new Vector3(radius * 2f, Vector3.Distance(a, b) / 2f, radius * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return go;
        }

        static GameObject BoxBetween(string name, Transform parent, Vector3 a, Vector3 b, float thick, Material m)
        {
            var go = Box(name, parent, (a + b) / 2f, new Vector3(thick, Vector3.Distance(a, b) + thick * 0.4f, thick), m, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return go;
        }

        // ---------- материалы ----------

        static Material Mat(string name, string hex, float smooth)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(m, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            mats[name] = m;
            return m;
        }

        static Material Emissive(string name, string hex, float power)
        {
            var m = Mat(name, hex, 0.3f);
            ColorUtility.TryParseHtmlString(hex, out var c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c * power);
            return m;
        }

        static Material DustMaterial()
        {
            string path = MatDir + "/M_Dust.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            AssetDatabase.CreateAsset(tex, MatDir + "/T_Dust.asset");
            m.mainTexture = tex;
            m.SetColor("_TintColor", new Color(0.5f, 0.45f, 0.38f, 0.35f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
