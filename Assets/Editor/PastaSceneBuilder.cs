using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Main.unity を丸ごと構築する。メニュー「Pasta/Build Main Scene」またはCLIから
/// PastaSceneBuilder.Build() で呼ぶ。何度実行しても同じ街が建つ（冪等）。
/// イタリアの広場に囲まれた一人称。四方からイタリアンが迫り、パスタ折りの衝撃波で倒す。
/// </summary>
public static class PastaSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Main.unity";
    private const string PastaPrefabPath = "Assets/Prefabs/Spaghetti.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Italian.prefab";
    private const string ShockwavePrefabPath = "Assets/Prefabs/Shockwave.prefab";

    // 街のレイアウト（中央=広場、十字の大通り、区画に建物）
    private const float PlazaRadius = 22f;   // 中央の開けた戦闘エリア
    private const float AvenueHalf = 5.5f;    // 大通りの半幅（この帯は建物を置かず街路にする）
    private const float GridSpacing = 16f;    // 建物区画の間隔
    private const int GridRange = 3;          // 区画の広がり（±GridRange → 端は ±48m）
    private const float StreetSpawnRadius = 26f;

    // パレット
    private static Material[] facadeMats;
    private static Material[] shutterMats;
    private static Material matGround, matPlaza, matRoof, matGlass, matDoor, matStone, matWater,
        matRed, matWhite, matWood, matPlant, matPlanter, matLine, matPasta, matPenne, matLasagna,
        matSkin, matShirt, matPants, matDark, matEye, matScarf, matHair;

    [MenuItem("Pasta/Build Main Scene")]
    public static void Build()
    {
        Random.InitState(20240917);
        EnsureFolder("Assets/Scenes");
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Materials");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        MakeMaterials();
        SetupLighting();
        BuildGround();
        BuildTownGrid();
        var spawnPoints = BuildStreetSpawnPoints();
        BuildProps();

        var pastaPrefabs = BuildPastaPrefabs();
        var enemyPrefab = BuildEnemyPrefab();
        var shockPrefab = BuildShockwavePrefab();

        // --- プレイヤーリグ（カメラを子に、手元のパスタ保持点）
        var player = new GameObject("Player");
        player.transform.position = Vector3.zero;
        var cam = Camera.main;
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.7f, 0f);
        cam.transform.localRotation = Quaternion.identity;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;

        var holdPoint = new GameObject("HoldPoint").transform;
        holdPoint.SetParent(cam.transform, false);
        holdPoint.localPosition = new Vector3(0f, -0.35f, 0.8f);
        holdPoint.localRotation = Quaternion.identity;
        holdPoint.localScale = Vector3.one * 1.6f;

        // 束の両端をにぎる手（常時カメラ前に表示。パスタはこの間で折れる）
        BuildHand(holdPoint, -1f);
        BuildHand(holdPoint, 1f);

        var cctrl = player.AddComponent<CharacterController>();
        cctrl.height = 1.7f; cctrl.radius = 0.35f; cctrl.center = new Vector3(0f, 0.9f, 0f);

        var pc = player.AddComponent<PlayerController>();
        SetRef(pc, "cameraPivot", cam.transform);

        var hand = player.AddComponent<PastaHand>();
        SetRefArray(hand, "pastaPrefabs", pastaPrefabs);
        SetRef(hand, "holdPoint", holdPoint);
        SetRef(hand, "shockwavePrefab", shockPrefab);

        // --- UI
        BuildUi(out var healthFill, out var healthText, out var scoreText, out var killsText,
                out var waveText, out var meterText, out var ammoText, out var centerText, out var crosshair,
                out var popup, out var popupText);

        // --- マネージャ
        var managers = new GameObject("Managers");
        var game = managers.AddComponent<GameManager>();

        var score = managers.AddComponent<ScoreManager>();
        SetRef(score, "popup", popup);
        SetRef(score, "popupText", popupText);

        var spawner = managers.AddComponent<EnemySpawner>();
        SetRef(spawner, "enemyPrefab", enemyPrefab);
        SetRefArray(spawner, "spawnPoints", spawnPoints);

        var hud = managers.AddComponent<Hud>();
        SetRef(hud, "healthFill", healthFill);
        SetRef(hud, "healthText", healthText);
        SetRef(hud, "scoreText", scoreText);
        SetRef(hud, "killsText", killsText);
        SetRef(hud, "waveText", waveText);
        SetRef(hud, "meterText", meterText);
        SetRef(hud, "ammoText", ammoText);
        SetRef(hud, "centerText", centerText);
        SetRef(hud, "crosshair", crosshair);

        _ = game;
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[PastaSceneBuilder] Main.unity 構築完了（イタリア広場ウェーブFPS）");
    }

    // ---------------- 環境 ----------------

    private static void SetupLighting()
    {
        var light = Object.FindAnyObjectByType<Light>();
        light.transform.rotation = Quaternion.Euler(38f, -40f, 0f);
        light.color = new Color(1f, 0.94f, 0.82f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.53f, 0.48f);
    }

    private static void BuildGround()
    {
        // 広い地面（±80m）
        var g = Prim(PrimitiveType.Plane, "Ground", null, Vector3.zero, Vector3.zero, Vector3.one * 16f, matGround, true);
        g.isStatic = true;
        // 中央広場（明るい石畳の円）
        Prim(PrimitiveType.Cylinder, "Plaza", null, new Vector3(0, 0.02f, 0), Vector3.zero,
            new Vector3(PlazaRadius * 2f, 0.02f, PlazaRadius * 2f), matPlaza, false);
    }

    /// <summary>中央広場＋十字の大通りを空け、四つの区画に建物を並べた街を建てる</summary>
    private static void BuildTownGrid()
    {
        var town = new GameObject("Town").transform;
        for (int gx = -GridRange; gx <= GridRange; gx++)
        {
            for (int gz = -GridRange; gz <= GridRange; gz++)
            {
                Vector3 pos = new Vector3(gx * GridSpacing, 0f, gz * GridSpacing);
                if (pos.magnitude < PlazaRadius) continue;                       // 広場は空ける
                if (Mathf.Abs(pos.x) < AvenueHalf || Mathf.Abs(pos.z) < AvenueHalf) continue; // 大通りは空ける

                // 広場中心を向く支配的なカードィナル方向（窓が付く面）
                Vector3 facing = Mathf.Abs(pos.x) >= Mathf.Abs(pos.z)
                    ? new Vector3(-Mathf.Sign(pos.x), 0, 0)
                    : new Vector3(0, 0, -Mathf.Sign(pos.z));
                BuildBuilding(town, pos, facing);
            }
        }
    }

    /// <summary>四方の大通りの奥に、敵の湧き地点を作る</summary>
    private static Transform[] BuildStreetSpawnPoints()
    {
        var parent = new GameObject("SpawnPoints").transform;
        var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var d in dirs)
        {
            Vector3 side = Vector3.Cross(Vector3.up, d); // 通りの横方向
            for (int s = -1; s <= 1; s += 2)
            {
                var sp = new GameObject("Spawn").transform;
                sp.SetParent(parent, false);
                sp.position = d * StreetSpawnRadius + side * (s * 2.6f);
                list.Add(sp);
            }
        }
        return list.ToArray();
    }

    /// <summary>facing = 建物から広場中心を向く方向（窓が付く面）</summary>
    private static void BuildBuilding(Transform parent, Vector3 basePos, Vector3 facing)
    {
        float width = Random.Range(8f, 9.5f);
        float depth = 6f;
        float height = Random.Range(8f, 13f);
        var facade = facadeMats[Random.Range(0, facadeMats.Length)];

        var b = new GameObject("Building").transform;
        b.SetParent(parent, false);
        b.position = basePos + Vector3.up * height * 0.5f;
        b.rotation = Quaternion.LookRotation(facing);
        b.gameObject.isStatic = true;

        // 本体（LookRotationでforward=facing。ローカルz=奥行、x=幅、y=高さ）
        var body = Prim(PrimitiveType.Cube, "Body", b, Vector3.zero, Vector3.zero,
            new Vector3(width, height, depth), facade, true);
        body.isStatic = true;

        // 軒（ルーフライン）
        Prim(PrimitiveType.Cube, "Cornice", b, new Vector3(0, height * 0.5f + 0.15f, 0), Vector3.zero,
            new Vector3(width + 0.5f, 0.3f, depth + 0.5f), matRoof, false);
        // 屋根
        Prim(PrimitiveType.Cube, "Roof", b, new Vector3(0, height * 0.5f + 0.4f, 0), Vector3.zero,
            new Vector3(width + 0.2f, 0.5f, depth + 0.2f), matRoof, false);

        // 窓グリッド（facing面 = ローカル+z面）
        float faceZ = depth * 0.5f + 0.03f;
        int rows = Mathf.Clamp(Mathf.RoundToInt((height - 2.5f) / 2.8f), 1, 3);
        int cols = 3;
        var shutter = shutterMats[Random.Range(0, shutterMats.Length)];
        float colStep = width / (cols + 1);
        for (int r = 0; r < rows; r++)
        {
            float y = -height * 0.5f + 2.2f + r * 2.8f;
            for (int c = 0; c < cols; c++)
            {
                float x = -width * 0.5f + (c + 1) * colStep;
                var local = new Vector3(x, y, faceZ);
                // ガラス
                Prim(PrimitiveType.Quad, "Win", b, local, Vector3.zero, new Vector3(0.9f, 1.3f, 1f), matGlass, false);
                // 枠まわりの鎧戸
                Prim(PrimitiveType.Cube, "ShutL", b, local + new Vector3(-0.62f, 0, 0.02f), Vector3.zero, new Vector3(0.28f, 1.35f, 0.05f), shutter, false);
                Prim(PrimitiveType.Cube, "ShutR", b, local + new Vector3(0.62f, 0, 0.02f), Vector3.zero, new Vector3(0.28f, 1.35f, 0.05f), shutter, false);
                // 窓台
                Prim(PrimitiveType.Cube, "Sill", b, local + new Vector3(0, -0.72f, 0.05f), Vector3.zero, new Vector3(1.4f, 0.12f, 0.18f), matRoof, false);
            }
        }

        // 入口（ローカル+z面の下部中央）
        Prim(PrimitiveType.Quad, "Door", b, new Vector3(0, -height * 0.5f + 1.1f, faceZ), Vector3.zero, new Vector3(1.3f, 2.2f, 1f), matDoor, false);
        Prim(PrimitiveType.Cube, "DoorFrame", b, new Vector3(0, -height * 0.5f + 1.1f, faceZ + 0.02f), Vector3.zero, new Vector3(1.55f, 2.45f, 0.12f), matRoof, false);
    }

    private static void BuildProps()
    {
        var props = new GameObject("Props").transform;

        // 広場の四隅に噴水（対角）
        BuildFountain(new Vector3(15f, 0f, 15f));
        BuildFountain(new Vector3(-15f, 0f, -15f));

        // パラソル付きカフェ席（広場の縁）
        Vector3[] cafes = {
            new Vector3(-16f, 0f, 15f), new Vector3(16f, 0f, -15f),
            new Vector3(15f, 0f, 16f), new Vector3(-15f, 0f, -16f),
        };
        foreach (var p in cafes) BuildCafe(props, p);

        // 広場の縁をぐるりと植木鉢で縁取り（大通りの入口は空ける）
        int ring = 16;
        for (int i = 0; i < ring; i++)
        {
            float a = i / (float)ring * Mathf.PI * 2f;
            Vector3 p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (PlazaRadius - 1.5f);
            if (Mathf.Abs(p.x) < AvenueHalf || Mathf.Abs(p.z) < AvenueHalf) continue; // 通りの入口は空ける
            BuildPlanter(props, p);
        }

        // 大通り沿いに街灯を並べる（奥行きを演出）
        var avenues = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        foreach (var d in avenues)
        {
            Vector3 side = Vector3.Cross(Vector3.up, d);
            for (float dist = PlazaRadius + 4f; dist <= GridRange * GridSpacing; dist += 12f)
            {
                BuildLamp(props, d * dist + side * (AvenueHalf - 0.6f));
                BuildLamp(props, d * dist - side * (AvenueHalf - 0.6f));
            }
        }

        // 洗濯物ロープ（近い建物の間、対角の区画に数本）
        BuildLaundry(props, new Vector3(11f, 8f, 20f), new Vector3(20f, 8.6f, 11f));
        BuildLaundry(props, new Vector3(-11f, 8f, -20f), new Vector3(-20f, 8.6f, -11f));
        BuildLaundry(props, new Vector3(-20f, 8.4f, 11f), new Vector3(-11f, 7.8f, 20f));
    }

    private static void BuildLamp(Transform parent, Vector3 pos)
    {
        var l = new GameObject("Lamp").transform;
        l.SetParent(parent, false);
        l.position = pos;
        Prim(PrimitiveType.Cylinder, "Post", l, new Vector3(0, 1.6f, 0), Vector3.zero, new Vector3(0.09f, 1.6f, 0.09f), matDark, false);
        Prim(PrimitiveType.Cylinder, "Arm", l, new Vector3(0, 3.15f, 0), Vector3.zero, new Vector3(0.16f, 0.12f, 0.16f), matDark, false);
        Prim(PrimitiveType.Sphere, "Bulb", l, new Vector3(0, 3.35f, 0), Vector3.zero, Vector3.one * 0.24f, matWhite, false);
    }

    private static void BuildCafe(Transform parent, Vector3 pos)
    {
        var c = new GameObject("Cafe").transform;
        c.SetParent(parent, false);
        c.position = pos;
        Prim(PrimitiveType.Cylinder, "Pole", c, new Vector3(0, 1.1f, 0), Vector3.zero, new Vector3(0.06f, 1.1f, 0.06f), matWood, false);
        // 縞パラソル
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            var panel = Prim(PrimitiveType.Cube, "Umb", c, new Vector3(Mathf.Cos(a) * 0.9f, 2.15f, Mathf.Sin(a) * 0.9f),
                new Vector3(18f, a * Mathf.Rad2Deg, 0f), new Vector3(0.8f, 0.06f, 1.0f), i % 2 == 0 ? matRed : matWhite, false);
            _ = panel;
        }
        Prim(PrimitiveType.Cylinder, "Table", c, new Vector3(0.9f, 0.55f, 0.4f), Vector3.zero, new Vector3(0.6f, 0.02f, 0.6f), matWhite, false);
        Prim(PrimitiveType.Cylinder, "TLeg", c, new Vector3(0.9f, 0.27f, 0.4f), Vector3.zero, new Vector3(0.06f, 0.27f, 0.06f), matWood, false);
    }

    private static void BuildPlanter(Transform parent, Vector3 pos)
    {
        var p = new GameObject("Planter").transform;
        p.SetParent(parent, false);
        p.position = pos;
        Prim(PrimitiveType.Cube, "Box", p, new Vector3(0, 0.3f, 0), Vector3.zero, new Vector3(0.7f, 0.6f, 0.7f), matPlanter, false);
        Prim(PrimitiveType.Sphere, "Bush", p, new Vector3(0, 0.85f, 0), Vector3.zero, new Vector3(0.85f, 0.8f, 0.85f), matPlant, false);
        Prim(PrimitiveType.Sphere, "Bush2", p, new Vector3(0.2f, 1.1f, -0.1f), Vector3.zero, new Vector3(0.55f, 0.55f, 0.55f), matPlant, false);
    }

    private static void BuildLaundry(Transform parent, Vector3 a, Vector3 b)
    {
        var l = new GameObject("Laundry").transform;
        l.SetParent(parent, false);
        Vector3 mid = (a + b) * 0.5f;
        float len = Vector3.Distance(a, b);
        var rope = Prim(PrimitiveType.Cylinder, "Rope", l, mid, Vector3.zero, new Vector3(0.02f, len * 0.5f, 0.02f), matDark, false);
        rope.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        Material[] clothes = { matRed, matWhite, matShirt, matScarf, matPlant };
        int n = 6;
        for (int i = 1; i < n; i++)
        {
            float t = i / (float)n;
            Vector3 hang = Vector3.Lerp(a, b, t) + Vector3.down * 0.5f;
            Prim(PrimitiveType.Quad, "Cloth", l, hang, new Vector3(0, 0, 0), new Vector3(0.5f, 0.7f, 1f), clothes[i % clothes.Length], false);
        }
    }

    private static void BuildFountain(Vector3 pos)
    {
        var f = new GameObject("Fountain").transform;
        f.position = pos;
        Prim(PrimitiveType.Cylinder, "Basin", f, new Vector3(0, 0.35f, 0), Vector3.zero, new Vector3(2.4f, 0.35f, 2.4f), matStone, false);
        Prim(PrimitiveType.Cylinder, "Water", f, new Vector3(0, 0.55f, 0), Vector3.zero, new Vector3(2.1f, 0.02f, 2.1f), matWater, false);
        Prim(PrimitiveType.Cylinder, "Column", f, new Vector3(0, 1.0f, 0), Vector3.zero, new Vector3(0.35f, 0.6f, 0.35f), matStone, false);
        Prim(PrimitiveType.Cylinder, "TopBasin", f, new Vector3(0, 1.5f, 0), Vector3.zero, new Vector3(1.0f, 0.12f, 1.0f), matStone, false);
        Prim(PrimitiveType.Sphere, "Top", f, new Vector3(0, 1.75f, 0), Vector3.zero, Vector3.one * 0.35f, matStone, false);
    }

    /// <summary>パスタの端をにぎる手（side=-1で左、+1で右）。holdPointの子として常時表示。</summary>
    private static void BuildHand(Transform parent, float side)
    {
        var h = new GameObject(side < 0 ? "HandL" : "HandR").transform;
        h.SetParent(parent, false);
        h.localPosition = new Vector3(side * 0.25f, -0.03f, 0.0f);
        // 手の甲/こぶし
        Prim(PrimitiveType.Sphere, "Palm", h, Vector3.zero, Vector3.zero, new Vector3(0.12f, 0.10f, 0.14f), matSkin, false);
        // 指（甲の上に4本ぶんの塊）
        Prim(PrimitiveType.Cube, "Fingers", h, new Vector3(0f, 0.02f, 0.055f), new Vector3(10f, 0, 0), new Vector3(0.12f, 0.055f, 0.06f), matSkin, false);
        // 親指（内側からパスタを押さえる）
        Prim(PrimitiveType.Capsule, "Thumb", h, new Vector3(-side * 0.055f, 0.0f, 0.05f), new Vector3(0f, 0f, side * 55f), new Vector3(0.032f, 0.05f, 0.032f), matSkin, false);
        // 手首/袖
        Prim(PrimitiveType.Capsule, "Cuff", h, new Vector3(side * 0.06f, -0.05f, -0.06f), new Vector3(60f, 0f, side * 20f), new Vector3(0.1f, 0.12f, 0.1f), matShirt, false);
    }

    // ---------------- Prefab ----------------

    private static SpaghettiBreaker[] BuildPastaPrefabs()
    {
        return new[]
        {
            // スパゲッティ: 細い麺を何本も束ねた「束」
            BuildSegmentedPasta("Spaghetti", "Assets/Prefabs/Spaghetti.prefab", 4, 0.52f, 0.05f,
                StrandVisual(11, 0.010f, 0.032f, matPasta)),
            // ペンネ: 太めの筒を数本まとめた握り
            BuildSegmentedPasta("Penne", "Assets/Prefabs/Penne.prefab", 4, 0.36f, 0.06f,
                StrandVisual(7, 0.022f, 0.040f, matPenne)),
            // ラザニア: 平たいシート
            BuildSegmentedPasta("Lasagna", "Assets/Prefabs/Lasagna.prefab", 4, 0.48f, 0.07f,
                SheetVisual(matLasagna)),
        };
    }

    /// <summary>X方向に並ぶsegCount個のセグメント(Rigidbody)をジョイントで連結。各セグメントの見た目はaddVisualで足す。</summary>
    private static SpaghettiBreaker BuildSegmentedPasta(string name, string path, int segCount,
        float totalLen, float colliderRadius, System.Action<Transform, float> addVisual)
    {
        var root = new GameObject(name);
        var breaker = root.AddComponent<SpaghettiBreaker>();
        root.AddComponent<AudioSource>().playOnAwake = false;

        float segStep = totalLen / segCount;
        var rbs = new Rigidbody[segCount];
        var segs = new GameObject[segCount];

        for (int i = 0; i < segCount; i++)
        {
            var seg = new GameObject($"Segment{i}");
            seg.transform.SetParent(root.transform, false);
            float x = -totalLen * 0.5f + segStep * (i + 0.5f);
            seg.transform.localPosition = new Vector3(x, 0f, 0f);

            var rb = seg.AddComponent<Rigidbody>();
            rb.mass = 0.04f;
            rb.linearDamping = 0.15f;
            rb.interpolation = RigidbodyInterpolation.None; // 保持中は親に密着（置いてけぼり防止）
            rb.isKinematic = true;

            var col = seg.AddComponent<CapsuleCollider>();
            col.direction = 0; // X軸
            col.height = segStep + colliderRadius;
            col.radius = colliderRadius;

            addVisual(seg.transform, segStep);

            rbs[i] = rb;
            segs[i] = seg;
        }

        var joints = new ConfigurableJoint[segCount - 1];
        for (int i = 0; i < segCount - 1; i++)
        {
            var j = segs[i + 1].AddComponent<ConfigurableJoint>();
            j.connectedBody = rbs[i];
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Locked;
            j.projectionMode = JointProjectionMode.PositionAndRotation;
            joints[i] = j;
        }

        var so = new SerializedObject(breaker);
        var pSeg = so.FindProperty("segments");
        pSeg.arraySize = segCount;
        for (int i = 0; i < segCount; i++) pSeg.GetArrayElementAtIndex(i).objectReferenceValue = rbs[i];
        var pJoint = so.FindProperty("joints");
        pJoint.arraySize = joints.Length;
        for (int i = 0; i < joints.Length; i++) pJoint.GetArrayElementAtIndex(i).objectReferenceValue = joints[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<SpaghettiBreaker>();
    }

    /// <summary>細い麺を束にする見た目（中心＋リング配置）</summary>
    private static System.Action<Transform, float> StrandVisual(int count, float strandRadius, float bundleRadius, Material mat)
    {
        return (seg, segLen) =>
        {
            foreach (var o in ClusterOffsets(count, bundleRadius))
            {
                var st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                st.name = "Strand";
                st.transform.SetParent(seg, false);
                st.transform.localPosition = new Vector3(0f, o.x, o.y);
                st.transform.localEulerAngles = new Vector3(0f, 0f, 90f); // 長軸をXへ
                st.transform.localScale = new Vector3(strandRadius * 2f, segLen * 0.52f, strandRadius * 2f);
                Object.DestroyImmediate(st.GetComponent<Collider>());
                st.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        };
    }

    /// <summary>平たいシートの見た目（ラザニア）</summary>
    private static System.Action<Transform, float> SheetVisual(Material mat)
    {
        return (seg, segLen) =>
        {
            var sh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sh.name = "Sheet";
            sh.transform.SetParent(seg, false);
            sh.transform.localScale = new Vector3(segLen, 0.02f, 0.5f);
            Object.DestroyImmediate(sh.GetComponent<Collider>());
            sh.GetComponent<MeshRenderer>().sharedMaterial = mat;
        };
    }

    private static Vector2[] ClusterOffsets(int n, float r)
    {
        var list = new System.Collections.Generic.List<Vector2> { Vector2.zero };
        int ring = Mathf.Max(1, n - 1);
        for (int k = 0; k < ring; k++)
        {
            float a = k / (float)ring * Mathf.PI * 2f;
            list.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
        }
        return list.ToArray();
    }

    private static Enemy BuildEnemyPrefab()
    {
        var root = new GameObject("Italian");
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0, 0.95f, 0);
        col.height = 1.9f;
        col.radius = 0.32f;
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 3f;
        rb.isKinematic = true;

        var t = root.transform;
        // 体（+zが正面＝プレイヤー側）
        Prim(PrimitiveType.Capsule, "Body", t, new Vector3(0, 0.85f, 0), Vector3.zero, new Vector3(0.6f, 0.44f, 0.5f), matShirt, false);
        Prim(PrimitiveType.Capsule, "Legs", t, new Vector3(0, 0.35f, 0), Vector3.zero, new Vector3(0.5f, 0.35f, 0.45f), matPants, false);
        var head = Prim(PrimitiveType.Sphere, "Head", t, new Vector3(0, 1.5f, 0), Vector3.zero, Vector3.one * 0.4f, matSkin, false);
        Prim(PrimitiveType.Sphere, "Hair", t, new Vector3(0, 1.62f, -0.03f), Vector3.zero, new Vector3(0.44f, 0.34f, 0.44f), matHair, false);
        Prim(PrimitiveType.Sphere, "Nose", t, new Vector3(0, 1.47f, 0.19f), Vector3.zero, Vector3.one * 0.1f, matSkin, false);
        Prim(PrimitiveType.Cube, "MoustL", t, new Vector3(-0.08f, 1.40f, 0.185f), new Vector3(0, 0, 12f), new Vector3(0.15f, 0.05f, 0.05f), matHair, false);
        Prim(PrimitiveType.Cube, "MoustR", t, new Vector3(0.08f, 1.40f, 0.185f), new Vector3(0, 0, -12f), new Vector3(0.15f, 0.05f, 0.05f), matHair, false);
        // 怒り眉（ハの字の逆）
        Prim(PrimitiveType.Cube, "BrowL", t, new Vector3(-0.09f, 1.60f, 0.17f), new Vector3(0, 0, -22f), new Vector3(0.12f, 0.03f, 0.04f), matHair, false);
        Prim(PrimitiveType.Cube, "BrowR", t, new Vector3(0.09f, 1.60f, 0.17f), new Vector3(0, 0, 22f), new Vector3(0.12f, 0.03f, 0.04f), matHair, false);
        Prim(PrimitiveType.Cube, "Scarf", t, new Vector3(0, 1.28f, 0.16f), Vector3.zero, new Vector3(0.34f, 0.12f, 0.34f), matScarf, false);
        // 掴みかかる腕（前方へ）
        Prim(PrimitiveType.Capsule, "ArmL", t, new Vector3(-0.34f, 1.0f, 0.28f), new Vector3(70f, 0, 20f), new Vector3(0.12f, 0.28f, 0.12f), matShirt, false);
        Prim(PrimitiveType.Capsule, "ArmR", t, new Vector3(0.34f, 1.0f, 0.28f), new Vector3(70f, 0, -20f), new Vector3(0.12f, 0.28f, 0.12f), matShirt, false);
        Prim(PrimitiveType.Sphere, "HandL", t, new Vector3(-0.4f, 0.95f, 0.62f), Vector3.zero, Vector3.one * 0.12f, matSkin, false);
        Prim(PrimitiveType.Sphere, "HandR", t, new Vector3(0.4f, 0.95f, 0.62f), Vector3.zero, Vector3.one * 0.12f, matSkin, false);
        Prim(PrimitiveType.Cube, "FootL", t, new Vector3(-0.14f, 0.05f, 0.08f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.28f), matDark, false);
        Prim(PrimitiveType.Cube, "FootR", t, new Vector3(0.14f, 0.05f, 0.08f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.28f), matDark, false);

        var eyeL = new GameObject("EyeL").transform; eyeL.SetParent(t, false); eyeL.localPosition = new Vector3(-0.1f, 1.55f, 0.16f);
        Prim(PrimitiveType.Sphere, "W", eyeL, Vector3.zero, Vector3.zero, Vector3.one * 0.09f, matEye, false);
        Prim(PrimitiveType.Sphere, "P", eyeL, new Vector3(0, 0, 0.035f), Vector3.zero, Vector3.one * 0.042f, matDark, false);
        var eyeR = new GameObject("EyeR").transform; eyeR.SetParent(t, false); eyeR.localPosition = new Vector3(0.1f, 1.55f, 0.16f);
        Prim(PrimitiveType.Sphere, "W", eyeR, Vector3.zero, Vector3.zero, Vector3.one * 0.09f, matEye, false);
        Prim(PrimitiveType.Sphere, "P", eyeR, new Vector3(0, 0, 0.035f), Vector3.zero, Vector3.one * 0.042f, matDark, false);

        var enemy = root.AddComponent<Enemy>();
        enemy.leftEye = eyeL;
        enemy.rightEye = eyeR;
        enemy.head = head.transform;
        enemy.shoutFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        root.AddComponent<AudioSource>().playOnAwake = false;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Enemy>();
    }

    private static Shockwave BuildShockwavePrefab()
    {
        var shockMat = TransparentUnlit("ShockMat", new Color(1f, 0.6f, 0.2f, 0.5f));

        var root = new GameObject("Shockwave");
        root.AddComponent<Shockwave>();

        // 円/扇用のリング（Shockwaveがroot側でX/Zを拡大）
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Ring";
        ring.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.GetComponent<MeshRenderer>().sharedMaterial = shockMat;

        // 貫通ビーム用の前方バー（Shockwaveが長さ/幅を制御）
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Beam";
        beam.transform.SetParent(root.transform, false);
        beam.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        beam.transform.localScale = new Vector3(1f, 0.06f, 1f);
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.GetComponent<MeshRenderer>().sharedMaterial = shockMat;
        beam.SetActive(false);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, ShockwavePrefabPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Shockwave>();
    }

    // ---------------- UI ----------------

    private static void BuildUi(out Image healthFill, out Text healthText, out Text scoreText,
        out Text killsText, out Text waveText, out Text meterText, out Text ammoText, out Text centerText,
        out Text crosshair, out GameObject popup, out Text popupText)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var ct = canvasGo.transform;

        scoreText = MakeText(ct, "ScoreText", "0", font, 52, TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(28, -18), new Vector2(500, 66), Color.white, FontStyle.Bold);
        killsText = MakeText(ct, "KillsText", "撃破 0", font, 28, TextAnchor.UpperLeft,
            new Vector2(0, 1), new Vector2(30, -84), new Vector2(500, 40), new Color(1f, 0.9f, 0.6f), FontStyle.Normal);

        // HPバー
        var barBg = MakeImage(ct, "HealthBg", uiSprite, new Color(0, 0, 0, 0.55f),
            new Vector2(0, 1), new Vector2(30, -124), new Vector2(360, 28), Image.Type.Sliced);
        healthFill = MakeImage(barBg.transform, "HealthFill", uiSprite, new Color(0.2f, 0.8f, 0.3f),
            new Vector2(0, 0.5f), Vector2.zero, new Vector2(360, 28), Image.Type.Filled);
        var frt = healthFill.rectTransform; frt.anchorMin = new Vector2(0, 0); frt.anchorMax = new Vector2(1, 1);
        frt.offsetMin = new Vector2(3, 3); frt.offsetMax = new Vector2(-3, -3);
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthText = MakeText(barBg.transform, "HealthText", "HP 100", font, 18, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 28), Color.white, FontStyle.Bold);

        waveText = MakeText(ct, "WaveText", "WAVE 0", font, 40, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(500, 56), Color.white, FontStyle.Bold);
        meterText = MakeText(ct, "MeterText", "", font, 38, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1200, 56), Color.white, FontStyle.Bold);
        ammoText = MakeText(ct, "AmmoText", "スパゲッティ  [円]", font, 30, TextAnchor.LowerRight,
            new Vector2(1, 0), new Vector2(-28, 32), new Vector2(700, 44), new Color(0.95f, 0.8f, 0.45f), FontStyle.Bold);
        MakeText(ct, "HintText",
            "移動 WASD / 視点 マウス / 折る 左クリック長押し→離す / 弾種 Q / 狙撃 右クリック",
            font, 20, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(28, 26), new Vector2(1100, 28),
            new Color(1f, 1f, 1f, 0.55f), FontStyle.Normal);

        crosshair = MakeText(ct, "Crosshair", "+", font, 44, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80), new Color(1f, 1f, 1f, 0.7f), FontStyle.Normal);

        centerText = MakeText(ct, "CenterText", "", font, 72, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 420), Color.white, FontStyle.Bold);
        centerText.gameObject.AddComponent<Outline>().effectColor = Color.black;
        centerText.enabled = false;

        popup = new GameObject("Popup");
        popup.transform.SetParent(ct, false);
        var prt = popup.AddComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = new Vector2(0, 190);
        prt.sizeDelta = new Vector2(1400, 130);
        popupText = MakeText(popup.transform, "PopupText", "", font, 78, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 130), new Color(1f, 0.2f, 0.12f), FontStyle.BoldAndItalic);
        popupText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
    }

    // ---------------- ヘルパ ----------------

    private static void MakeMaterials()
    {
        facadeMats = new[]
        {
            Mat("FacadeTerracotta", new Color(0.79f, 0.42f, 0.30f)),
            Mat("FacadeOchre", new Color(0.85f, 0.62f, 0.28f)),
            Mat("FacadeSienna", new Color(0.70f, 0.38f, 0.24f)),
            Mat("FacadeCream", new Color(0.90f, 0.83f, 0.66f)),
            Mat("FacadeRose", new Color(0.80f, 0.55f, 0.52f)),
            Mat("FacadeGold", new Color(0.83f, 0.68f, 0.36f)),
        };
        shutterMats = new[]
        {
            Mat("ShutterGreen", new Color(0.28f, 0.42f, 0.30f)),
            Mat("ShutterBrown", new Color(0.42f, 0.28f, 0.18f)),
        };
        matGround = Mat("Ground", new Color(0.48f, 0.44f, 0.40f));
        matPlaza = Mat("Plaza", new Color(0.66f, 0.61f, 0.54f));
        matRoof = Mat("Roof", new Color(0.45f, 0.28f, 0.22f));
        matGlass = Mat("Glass", new Color(0.13f, 0.16f, 0.20f));
        matDoor = Mat("DoorMat", new Color(0.35f, 0.22f, 0.15f));
        matStone = Mat("Stone", new Color(0.80f, 0.78f, 0.72f));
        matWater = Mat("WaterMat", new Color(0.30f, 0.55f, 0.68f));
        matRed = Mat("Red", new Color(0.80f, 0.15f, 0.15f));
        matWhite = Mat("White", new Color(0.95f, 0.95f, 0.93f));
        matWood = Mat("Wood", new Color(0.40f, 0.27f, 0.16f));
        matPlant = Mat("Plant", new Color(0.25f, 0.5f, 0.24f));
        matPlanter = Mat("PlanterMat", new Color(0.55f, 0.35f, 0.25f));
        matLine = Mat("Line", new Color(0.2f, 0.2f, 0.2f));
        matPasta = Mat("Pasta", new Color(0.93f, 0.79f, 0.42f));
        matPenne = Mat("Penne", new Color(0.88f, 0.68f, 0.42f));
        matLasagna = Mat("Lasagna", new Color(0.94f, 0.83f, 0.52f));
        matSkin = Mat("Skin", new Color(0.98f, 0.80f, 0.62f));
        matShirt = Mat("Shirt", new Color(0.92f, 0.92f, 0.88f));
        matPants = Mat("Pants", new Color(0.25f, 0.28f, 0.35f));
        matDark = Mat("Dark", new Color(0.12f, 0.09f, 0.07f));
        matEye = Mat("EyeWhite", Color.white);
        matScarf = Mat("Scarf", new Color(0.75f, 0.12f, 0.12f));
        matHair = Mat("Hair", new Color(0.15f, 0.10f, 0.08f));
        _ = matLine;
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent,
        Vector3 localPos, Vector3 localEuler, Vector3 localScale, Material mat, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localEuler;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        return go;
    }

    private static Text MakeText(Transform parent, string name, string content, Font font, int size,
        TextAnchor align, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, Color color, FontStyle style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var text = go.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.alignment = align;
        text.color = color; text.fontStyle = style; text.text = content;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta, Image.Type type)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        return img;
    }

    private static Material Mat(string name, Color c)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = c };
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static Material TransparentUnlit(string name, Color c)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetColor("_BaseColor", c);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    private static void SetRef(Object target, string propertyName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null) { Debug.LogError($"[Builder] {target.GetType().Name}.{propertyName} が無い"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRefArray(Object target, string propertyName, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null) { Debug.LogError($"[Builder] {target.GetType().Name}.{propertyName} が無い"); return; }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
