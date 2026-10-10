using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Micromobility > Build City Scene
// Builds a Los Angeles style city grid from primitives and saves it as Assets/Scenes/City.unity:
// 5 x 5 boulevards with bike lanes, signals, crosswalks, downtown towers, mid rise and low rise
// blocks, parks, palm trees, hills in the haze, background traffic, the right-hook car and the scooter.
// If Assets/Models/Scooter.fbx exists (exported from Blender), it is used as the scooter model.
public static class SceneBuilder
{
    const string ScenePath = "Assets/Scenes/City.unity";
    const string ModelPath = "Assets/Models/Scooter.fbx";
    const float ModelYaw = 0f;               // if the scooter model faces backwards in Unity, change to 180
    const bool UseStaticBatching = true;     // faster rendering; set to false if scene reloads feel slow
    const float HW = City.HalfWidth;
    const float HookX = -100f;               // the right hook happens at the intersection (HookX, 0)

    struct Span
    {
        public float a, b;
        public Span(float a, float b) { this.a = a; this.b = b; }
    }

    static Dictionary<string, Material> matCache;
    static System.Random rng;
    static Transform decor;
    static Texture2D windowTex;

    static readonly Color[][] Tints =
    {
        new[] { new Color(0.55f, 0.68f, 0.80f), new Color(0.45f, 0.55f, 0.60f), new Color(0.75f, 0.78f, 0.82f) },
        new[] { new Color(0.88f, 0.82f, 0.70f), new Color(0.70f, 0.45f, 0.36f), new Color(0.92f, 0.92f, 0.90f), new Color(0.62f, 0.64f, 0.68f) },
        new[] { new Color(0.95f, 0.88f, 0.76f), new Color(0.92f, 0.74f, 0.68f), new Color(0.72f, 0.86f, 0.82f), new Color(0.96f, 0.94f, 0.86f) }
    };

    static readonly Color[] CarColors =
    {
        new Color(0.92f, 0.92f, 0.92f), new Color(0.08f, 0.08f, 0.09f), new Color(0.65f, 0.67f, 0.70f), new Color(0.75f, 0.12f, 0.12f),
        new Color(0.12f, 0.28f, 0.65f), new Color(0.35f, 0.37f, 0.40f), new Color(0.10f, 0.35f, 0.22f), new Color(0.85f, 0.72f, 0.15f)
    };

    [MenuItem("Micromobility/Build City Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Textures");
        matCache = new Dictionary<string, Material>();
        rng = new System.Random(11);

        try
        {
            EditorUtility.DisplayProgressBar("Building city", "Setting up", 0.02f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            decor = new GameObject("City").transform;
            Transform signals = new GameObject("Signals").transform;
            Transform traffic = new GameObject("Traffic").transform;
            windowTex = MakeWindowTexture();

            EditorUtility.DisplayProgressBar("Building city", "Sky, hills and light", 0.08f);
            BuildEnvironment();
            EditorUtility.DisplayProgressBar("Building city", "Blocks, buildings and parks", 0.2f);
            BuildBlocks();
            EditorUtility.DisplayProgressBar("Building city", "Roads and lane paint", 0.45f);
            BuildRoads();
            EditorUtility.DisplayProgressBar("Building city", "Intersections and signals", 0.6f);
            BuildIntersections(signals);
            EditorUtility.DisplayProgressBar("Building city", "Palm trees", 0.75f);
            BuildStreetTrees();

            EditorUtility.DisplayProgressBar("Building city", "Traffic", 0.85f);
            var ambient = new List<CarAI>();
            BuildTraffic(traffic, ambient);
            CarAI hook = BuildHookCar(traffic);

            EditorUtility.DisplayProgressBar("Building city", "Scooter and systems", 0.93f);
            var scooter = BuildScooter();
            var systems = new GameObject("Systems");
            var logger = systems.AddComponent<DataLogger>();
            logger.scooter = scooter;
            var mgr = systems.AddComponent<ScenarioManager>();
            mgr.hookCar = hook;
            mgr.ambientCars = ambient.ToArray();
            mgr.scooter = scooter;
            mgr.logger = logger;
            mgr.hookCenter = new Vector3(HookX, 0f, 0f);

            if (UseStaticBatching)
                foreach (var t in decor.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("City scene built. Press Play.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ---------- environment ----------

    static void BuildEnvironment()
    {
        Box("Ground", decor, new Vector3(0, -0.6f, 0), new Vector3(3000f, 1f, 3000f), Mat("Ground", new Color(0.52f, 0.48f, 0.40f)));
        Material hillA = Mat("HillA", new Color(0.50f, 0.45f, 0.33f));
        Material hillB = Mat("HillB", new Color(0.42f, 0.44f, 0.32f));
        for (int i = 0; i < 16; i++)
        {
            float ang = i * Mathf.PI * 2f / 16f;
            float rad = 850f + (float)rng.NextDouble() * 150f;
            float s = 350f + (float)rng.NextDouble() * 250f;
            float h = 120f + (float)rng.NextDouble() * 160f;
            Part(decor, PrimitiveType.Sphere, "Hill", new Vector3(Mathf.Cos(ang) * rad, -20f, Mathf.Sin(ang) * rad),
                 new Vector3(s, h, s), Vector3.zero, i % 2 == 0 ? hillA : hillB);
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 200f;
        RenderSettings.fogEndDistance = 1400f;
        RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);

        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            var l = sun.GetComponent<Light>();
            l.color = new Color(1f, 0.95f, 0.85f);
            l.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    // ---------- blocks, buildings, parks ----------

    static List<Span> BlockSpans()
    {
        var l = new List<Span>();
        float[] r = City.Roads;
        l.Add(new Span(-City.Extent, r[0] - HW));
        for (int i = 0; i < r.Length - 1; i++) l.Add(new Span(r[i] + HW, r[i + 1] - HW));
        l.Add(new Span(r[r.Length - 1] + HW, City.Extent));
        return l;
    }

    static void BuildBlocks()
    {
        Material walk = Mat("Sidewalk", new Color(0.66f, 0.65f, 0.62f));
        var spans = BlockSpans();
        foreach (var sx in spans)
            foreach (var sz in spans)
            {
                float cx = (sx.a + sx.b) / 2f, cz = (sz.a + sz.b) / 2f;
                Box("Block", decor, new Vector3(cx, 0f, cz), new Vector3(sx.b - sx.a, 0.3f, sz.b - sz.a), walk);

                float far = Mathf.Max(Mathf.Abs(cx), Mathf.Abs(cz));
                int zone = far <= 60f ? 0 : (far <= 160f ? 1 : 2);   // 0 downtown, 1 mid rise, 2 low rise

                float ax = sx.a + 4f, bx = sx.b - 4f, az = sz.a + 4f, bz = sz.b - 4f;
                int nx = (bx - ax) > 60f ? 2 : 1, nz = (bz - az) > 60f ? 2 : 1;
                float lw = ((bx - ax) - (nx - 1) * 4f) / nx, ld = ((bz - az) - (nz - 1) * 4f) / nz;
                for (int i = 0; i < nx; i++)
                    for (int j = 0; j < nz; j++)
                    {
                        float lx = ax + lw / 2f + i * (lw + 4f), lz = az + ld / 2f + j * (ld + 4f);
                        if (zone > 0 && rng.NextDouble() < 0.18) Park(lx, lz, lw, ld);
                        else Building(lx, lz, lw, ld, zone);
                    }
            }
    }

    static void Building(float x, float z, float lw, float ld, int zone)
    {
        float room = Mathf.Min(lw, ld) * 0.92f;
        int[] widths = zone == 0 ? new[] { 30, 36 } : new[] { 24, 30, 36 };
        int w = widths[0];
        foreach (int c in widths) if (c <= room) w = c;
        int[] fl = zone == 0 ? new[] { 18, 24, 30, 36, 44 } : (zone == 1 ? new[] { 4, 6, 8, 10 } : new[] { 2, 3 });
        int floors = fl[rng.Next(fl.Length)];
        int tint = rng.Next(Tints[zone].Length);
        float h = floors * 3.6f;

        Material m = Mat("B" + zone + "_" + tint + "_" + w + "_" + floors, Tints[zone][tint], zone == 0 ? 0.7f : 0.1f);
        m.mainTexture = windowTex;
        m.mainTextureScale = new Vector2(w / 3f, floors);
        Box("Building", decor, new Vector3(x, 0.15f + h / 2f, z), new Vector3(w, h, w), m);
        if (zone < 2)
            Box("Roof", decor, new Vector3(x, 0.15f + h + 1.2f, z), new Vector3(w * 0.35f, 2.4f, w * 0.35f), Mat("RoofBox", new Color(0.35f, 0.35f, 0.37f)));
    }

    static void Park(float x, float z, float lw, float ld)
    {
        Box("Park", decor, new Vector3(x, 0.16f, z), new Vector3(lw * 0.9f, 0.04f, ld * 0.9f), Mat("Grass", new Color(0.28f, 0.50f, 0.22f)));
        for (int i = 0; i < 5; i++)
        {
            float px = x + ((float)rng.NextDouble() - 0.5f) * lw * 0.7f;
            float pz = z + ((float)rng.NextDouble() - 0.5f) * ld * 0.7f;
            Palm(new Vector3(px, 0.18f, pz), 8f + (float)rng.NextDouble() * 5f);
        }
    }

    static void Palm(Vector3 basePos, float height)
    {
        Material trunk = Mat("PalmTrunk", new Color(0.45f, 0.35f, 0.25f));
        Material leaf = Mat("PalmLeaf", new Color(0.16f, 0.46f, 0.16f));
        Part(decor, PrimitiveType.Cylinder, "Trunk", basePos + Vector3.up * (height / 2f), new Vector3(0.5f, height / 2f, 0.5f), Vector3.zero, trunk);
        Vector3 top = basePos + Vector3.up * height;
        for (int k = 0; k < 6; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward;
            var f = Part(decor, PrimitiveType.Cube, "Frond", top + dir * 1.7f + Vector3.down * 0.4f, new Vector3(0.8f, 0.06f, 3.4f), Vector3.zero, leaf);
            f.transform.localRotation = Quaternion.LookRotation((dir + Vector3.down * 0.45f).normalized);
        }
    }

    static bool NearRoad(float s)
    {
        foreach (float r in City.Roads) if (Mathf.Abs(s - r) < 16f) return true;
        return false;
    }

    static void BuildStreetTrees()
    {
        foreach (float rc in City.Roads)
            foreach (bool alongX in new[] { true, false })
                foreach (int side in new[] { -1, 1 })
                    for (float s = -240f; s <= 240f; s += 40f)
                    {
                        if (NearRoad(s)) continue;
                        float off = side * (HW + 2.5f);
                        Vector3 p = alongX ? new Vector3(s, 0.15f, rc + off) : new Vector3(rc + off, 0.15f, s);
                        Palm(p, 9f + (float)rng.NextDouble() * 4f);
                    }
    }

    // ---------- roads and paint ----------

    static void BuildRoads()
    {
        Material road = Mat("Road", new Color(0.17f, 0.17f, 0.19f));
        Material yellow = Mat("Yellow", new Color(0.95f, 0.75f, 0.10f));
        Material white = Mat("White", new Color(0.92f, 0.92f, 0.92f));
        Material green = Mat("BikeLane", new Color(0.10f, 0.55f, 0.35f));

        foreach (float rc in City.Roads)
        {
            Box("RoadEW", decor, new Vector3(0f, -0.05f, rc), new Vector3(2f * City.Extent, 0.1f, 2f * HW), road);
            Box("RoadNS", decor, new Vector3(rc, -0.05f, 0f), new Vector3(2f * HW, 0.1f, 2f * City.Extent), road);
        }

        // Paint between the crosswalk zones of the intersections
        var b = new List<float> { -City.Extent };
        foreach (float r in City.Roads) { b.Add(r - 12f); b.Add(r + 12f); }
        b.Add(City.Extent);

        foreach (float rc in City.Roads)
            foreach (bool alongX in new[] { true, false })
                for (int k = 0; k + 1 < b.Count; k += 2)
                {
                    float mid = (b[k] + b[k + 1]) / 2f, len = b[k + 1] - b[k];
                    if (len < 1f) continue;
                    Strip(alongX, rc, mid, 0.15f, len, 0.12f, yellow, 0.015f);
                    Strip(alongX, rc, mid, -0.15f, len, 0.12f, yellow, 0.015f);
                    foreach (int sd in new[] { -1, 1 })
                    {
                        Strip(alongX, rc, mid, sd * 3.5f, len, 0.12f, white, 0.015f);
                        Strip(alongX, rc, mid, sd * 4.5f, len, 1.9f, green, 0.010f);
                        Strip(alongX, rc, mid, sd * 5.5f, len, 0.12f, white, 0.015f);
                    }
                }
    }

    static void Strip(bool alongX, float rc, float mid, float off, float len, float width, Material m, float y)
    {
        Vector3 pos = alongX ? new Vector3(mid, y, rc + off) : new Vector3(rc + off, y, mid);
        Vector3 sc = alongX ? new Vector3(len, 0.02f, width) : new Vector3(width, 0.02f, len);
        Box("Paint", decor, pos, sc, m);
    }

    // ---------- intersections: crosswalks, stop lines, signals ----------

    static void BuildIntersections(Transform signals)
    {
        Material white = Mat("White", new Color(0.92f, 0.92f, 0.92f));
        Material poleMat = Mat("Pole", new Color(0.30f, 0.30f, 0.33f));
        Material housing = Mat("SignalHousing", new Color(0.05f, 0.05f, 0.06f));
        Material lampOff = Mat("LampOff", new Color(0.15f, 0.15f, 0.15f));
        Vector3[] headings = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };

        foreach (float x in City.Roads)
            foreach (float z in City.Roads)
            {
                Vector3 c = new Vector3(x, 0f, z);
                var go = new GameObject("Signal_" + x + "_" + z);
                go.transform.SetParent(signals, false);
                var tl = go.AddComponent<TrafficLight>();
                tl.center = c;
                tl.startOffset = (x == HookX && z == 0f) ? 0f : (float)rng.NextDouble() * 80f;
                var list = new List<SignalHead>();

                foreach (Vector3 h in headings)
                {
                    Vector3 r = Vector3.Cross(Vector3.up, h);
                    bool alongX = Mathf.Abs(h.x) > 0.5f;

                    // Zebra crosswalk on this approach
                    Vector3 cw = c - h * (HW + 2.5f);
                    for (int i = 0; i < 9; i++)
                    {
                        float o = -6f + i * 1.5f;
                        Box("Crosswalk", decor, cw + r * o + Vector3.up * 0.015f,
                            alongX ? new Vector3(2.4f, 0.02f, 0.7f) : new Vector3(0.7f, 0.02f, 2.4f), white);
                    }
                    // Stop line in the car lane
                    Box("StopLine", decor, c - h * City.StopDistance + r * 1.75f + Vector3.up * 0.015f,
                        alongX ? new Vector3(0.4f, 0.02f, 3.4f) : new Vector3(3.4f, 0.02f, 0.4f), white);

                    // Mast arm signal over the car lane
                    Vector3 P = c - h * (HW + 2f) + r * (HW + 1.3f);
                    Vector3 E = c - h * (HW + 2f) + r * 1.75f;
                    Part(go.transform, PrimitiveType.Cylinder, "Pole", P + Vector3.up * 3.25f, new Vector3(0.32f, 3.1f, 0.32f), Vector3.zero, poleMat);
                    var arm = Part(go.transform, PrimitiveType.Cube, "Arm", (P + E) / 2f + Vector3.up * 6.25f,
                                   new Vector3(0.14f, 0.14f, (P - E).magnitude), Vector3.zero, poleMat);
                    arm.transform.localRotation = Quaternion.LookRotation((E - P).normalized);
                    var head = Part(go.transform, PrimitiveType.Cube, "Head", E + Vector3.up * 5.5f, new Vector3(0.45f, 1.3f, 0.35f), Vector3.zero, housing);
                    head.transform.localRotation = Quaternion.LookRotation(h);

                    var sh = new SignalHead();
                    sh.red = Lamp(go.transform, E - h * 0.2f + Vector3.up * 5.92f, lampOff);
                    sh.yellow = Lamp(go.transform, E - h * 0.2f + Vector3.up * 5.5f, lampOff);
                    sh.green = Lamp(go.transform, E - h * 0.2f + Vector3.up * 5.08f, lampOff);
                    sh.eastWest = alongX;
                    list.Add(sh);
                }
                tl.heads = list.ToArray();
            }
    }

    static Renderer Lamp(Transform parent, Vector3 pos, Material m)
    {
        return Part(parent, PrimitiveType.Sphere, "Lamp", pos, Vector3.one * 0.28f, Vector3.zero, m).GetComponent<Renderer>();
    }

    // ---------- traffic ----------

    static void BuildTraffic(Transform parent, List<CarAI> ambient)
    {
        int n = 0;
        foreach (float rc in City.Roads)
            foreach (bool alongX in new[] { true, false })
                foreach (int dir in new[] { 1, -1 })
                {
                    if (alongX && rc == 0f && dir == 1) continue;   // the experiment lane stays free of background traffic
                    Vector3 h = alongX ? new Vector3(dir, 0f, 0f) : new Vector3(0f, 0f, dir);
                    Vector3 r = Vector3.Cross(Vector3.up, h);
                    Vector3 lane = (alongX ? new Vector3(0f, 0f, rc) : new Vector3(rc, 0f, 0f)) + r * 1.75f;
                    Vector3 a = lane - h * City.Extent, e = lane + h * City.Extent;
                    for (int slot = 0; slot < 3; slot++)
                    {
                        var car = MakeCar("Car" + n, parent, n % CarColors.Length, new[] { a, e }, new[] { 99f, 99f },
                                          10f + (float)rng.NextDouble() * 3f, true, true);
                        car.slot = slot;
                        car.startAlong = 30f + slot * 170f + (float)rng.NextDouble() * 40f;
                        ambient.Add(car);
                        n++;
                    }
                }
    }

    // Right turn from the eastbound lane into the southbound lane at the hook intersection (6 m radius arc)
    static CarAI BuildHookCar(Transform parent)
    {
        float cx = HookX, cz = 0f;
        var pts = new[]
        {
            new Vector3(cx - 35f, 0f, cz - 1.75f), new Vector3(cx - 7.75f, 0f, cz - 1.75f),
            new Vector3(cx - 4.75f, 0f, cz - 2.554f), new Vector3(cx - 2.554f, 0f, cz - 4.75f),
            new Vector3(cx - 1.75f, 0f, cz - 7.75f), new Vector3(cx - 1.75f, 0f, cz - 85f)
        };
        var car = MakeCar("HookCar", parent, 0, pts, new[] { 99f, 5.5f, 5f, 5f, 5f, 99f }, 10f, false, false);
        car.gameObject.SetActive(false);
        return car;
    }

    static CarAI MakeCar(string name, Transform parent, int colorIndex, Vector3[] pts, float[] lims, float speed, bool obey, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Material body = Mat("Car" + colorIndex, CarColors[colorIndex], 0.5f);
        Material glass = Mat("Glass", new Color(0.10f, 0.14f, 0.18f), 0.8f);
        Material tire = Mat("Tire", new Color(0.05f, 0.05f, 0.05f));
        Part(go.transform, PrimitiveType.Cube, "Body", new Vector3(0, 0.75f, 0), new Vector3(1.9f, 0.8f, 4.4f), Vector3.zero, body);
        Part(go.transform, PrimitiveType.Cube, "Cabin", new Vector3(0, 1.4f, -0.2f), new Vector3(1.7f, 0.6f, 2.2f), Vector3.zero, glass);
        foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Part(go.transform, PrimitiveType.Cylinder, "Wheel", new Vector3(sx * 0.95f, 0.35f, sz * 1.4f),
                     new Vector3(0.7f, 0.12f, 0.7f), new Vector3(0, 0, 90), tire);

        var bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0.8f, 0);
        bc.size = new Vector3(1.9f, 1.6f, 4.4f);

        var car = go.AddComponent<CarAI>();
        car.path.AddRange(pts);
        car.limits.AddRange(lims);
        car.cruiseSpeed = speed;
        car.obeyLight = obey;
        car.loop = loop;
        car.Body = bc;
        go.transform.position = pts[0];
        return car;
    }

    // ---------- scooter ----------

    static ScooterController BuildScooter()
    {
        var scooter = new GameObject("Scooter");
        scooter.transform.position = new Vector3(HookX - 50f, 0f, -4.5f);    // riding east in the bike lane
        scooter.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        var sc = scooter.AddComponent<ScooterController>();
        BuildScooterVisual(scooter.transform);

        var body = new GameObject("RiderBody");
        body.transform.SetParent(scooter.transform, false);
        Material pants = Mat("RiderPants", new Color(0.15f, 0.17f, 0.25f));
        Material shirt = Mat("RiderShirt", new Color(0.10f, 0.60f, 0.45f));
        Material skin = Mat("RiderSkin", new Color(0.80f, 0.60f, 0.45f));
        Part(body.transform, PrimitiveType.Capsule, "Legs", new Vector3(0, 0.62f, -0.15f), new Vector3(0.26f, 0.45f, 0.26f), Vector3.zero, pants);
        Part(body.transform, PrimitiveType.Capsule, "Torso", new Vector3(0, 1.28f, -0.15f), new Vector3(0.34f, 0.34f, 0.24f), Vector3.zero, shirt);
        Part(body.transform, PrimitiveType.Sphere, "Head", new Vector3(0, 1.78f, -0.15f), Vector3.one * 0.26f, Vector3.zero, skin);
        foreach (int sx in new[] { -1, 1 })
        {
            Vector3 from = new Vector3(sx * 0.22f, 1.5f, -0.15f), to = new Vector3(sx * 0.22f, 1.13f, 0.29f);
            var arm = Part(body.transform, PrimitiveType.Capsule, "Arm", (from + to) / 2f, new Vector3(0.09f, 0.29f, 0.09f), Vector3.zero, shirt);
            arm.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
        }
        body.SetActive(false);

        var cam = Camera.main;
        cam.transform.SetParent(scooter.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.5f, -0.15f);
        cam.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        cam.fieldOfView = 75f;
        cam.farClipPlane = 2500f;
        cam.clearFlags = CameraClearFlags.Skybox;

        sc.cam = cam.transform;
        sc.riderBody = body;
        return sc;
    }

    static void BuildScooterVisual(Transform root)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab != null)
        {
            var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            m.transform.SetParent(root, false);
            m.transform.localRotation = Quaternion.Euler(0f, ModelYaw, 0f);
            return;
        }
        Material dark = Mat("ScooterDark", new Color(0.12f, 0.12f, 0.14f));
        Material acc = Mat("ScooterAccent", new Color(0.1f, 0.6f, 0.45f));
        Part(root, PrimitiveType.Cube, "Deck", new Vector3(0, 0.15f, 0), new Vector3(0.22f, 0.05f, 1.0f), Vector3.zero, dark);
        Part(root, PrimitiveType.Cylinder, "WheelFront", new Vector3(0, 0.11f, 0.55f), new Vector3(0.22f, 0.04f, 0.22f), new Vector3(0, 0, 90), dark);
        Part(root, PrimitiveType.Cylinder, "WheelRear", new Vector3(0, 0.11f, -0.45f), new Vector3(0.22f, 0.04f, 0.22f), new Vector3(0, 0, 90), dark);
        Part(root, PrimitiveType.Cylinder, "Stem", new Vector3(0, 0.64f, 0.395f), new Vector3(0.04f, 0.5f, 0.04f), new Vector3(-12f, 0, 0), acc);
        Part(root, PrimitiveType.Cylinder, "Handlebar", new Vector3(0, 1.13f, 0.29f), new Vector3(0.025f, 0.275f, 0.025f), new Vector3(0, 0, 90), dark);
    }

    // ---------- helpers ----------

    static Texture2D MakeWindowTexture()
    {
        string path = "Assets/Textures/Window.png";
        int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color wall = new Color(0.95f, 0.95f, 0.95f, 1f), glass = new Color(0.30f, 0.38f, 0.46f, 1f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool win = x > 22 && x < 106 && y > 30 && y < 100 && !(x > 62 && x < 66);
                t.SetPixel(x, y, win ? glass : wall);
            }
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    static Material Mat(string name, Color c, float smoothness = 0.15f)
    {
        Material m;
        if (matCache.TryGetValue(name, out m)) return m;
        string path = "Assets/Materials/" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        EditorUtility.SetDirty(m);
        matCache[name] = m;
        return m;
    }

    static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = Quaternion.Euler(euler);
        g.GetComponent<Renderer>().sharedMaterial = m;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        return Part(parent, PrimitiveType.Cube, name, pos, scale, Vector3.zero, m);
    }
}
