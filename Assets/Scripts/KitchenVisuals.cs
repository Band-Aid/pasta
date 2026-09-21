using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small, recognizable food silhouettes and a wheeled granny, without prefab dependencies.</summary>
public static class KitchenVisuals
{
    public static readonly Color Gold = new Color(1f, 0.73f, 0.22f);
    public static readonly Color Cream = new Color(1f, 0.91f, 0.56f);
    public static readonly Color Pink = new Color(1f, 0.42f, 0.55f);
    public static readonly Color Mint = new Color(0.3f, 1f, 0.73f);
    private static Material solid, glow;
    private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

    public static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        if (solid == null) solid = new Material(Resources.Load<Shader>("KitchenSolid"));
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = solid;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetColor(ColorId, color);
        renderer.SetPropertyBlock(properties);
        return go.transform;
    }

    public static void Bar(Transform parent, string name, Vector3 from, Vector3 to, float width, Color color)
    {
        var bar = Part(parent, name, PrimitiveType.Cylinder, (from + to) * 0.5f,
            new Vector3(width, Vector3.Distance(from, to) * 0.5f, width), color);
        bar.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
    }

    public static LineRenderer Line(Transform parent, string name, Color color, float width, Vector3[] points, bool loop = false)
    {
        if (glow == null) glow = new Material(Resources.Load<Shader>("SnapWave"));
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = glow;
        line.useWorldSpace = false;
        line.startColor = line.endColor = color;
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        return line;
    }

    public static LineRenderer Ring(Transform parent, string name, float radius, Color color, float width = 0.04f)
    {
        var points = new Vector3[40];
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * Mathf.PI * 2f / points.Length;
            points[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
        }
        return Line(parent, name, color, width, points, true);
    }

    public static MeshFilter SauceDisc(Transform parent, float radius, Color color)
    {
        if (glow == null) glow = new Material(Resources.Load<Shader>("SnapWave"));
        const int count = 40;
        var vertices = new Vector3[count + 1];
        var colors = new Color[count + 1];
        var triangles = new int[count * 3];
        colors[0] = color;
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.PI * 2f / count;
            vertices[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            colors[i + 1] = color;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % count + 1;
        }
        var mesh = new Mesh { name = "Transparent sauce", vertices = vertices, colors = colors, triangles = triangles };
        mesh.RecalculateBounds();
        var go = new GameObject("Sauce surface");
        go.transform.SetParent(parent, false);
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = glow;
        return filter;
    }

    public static Transform Fusilli(Transform parent)
    {
        var root = new GameObject("Fusilli spiral").transform;
        root.SetParent(parent, false);
        var points = new Vector3[48];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            float a = t * Mathf.PI * 8f;
            points[i] = new Vector3(Mathf.Cos(a) * 0.15f, t * 0.95f - 0.475f, Mathf.Sin(a) * 0.15f);
        }
        Line(root, "Golden corkscrew", Gold, 0.1f, points);
        return root;
    }

    public static void Farfalle(Transform parent)
    {
        Part(parent, "Pasta knot", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.18f, 0.18f, 0.22f), Gold);
        for (int side = -1; side <= 1; side += 2)
        {
            var wing = Part(parent, "Butterfly wing", PrimitiveType.Cube, Vector3.right * side * 0.23f,
                new Vector3(0.4f, 0.07f, 0.45f), Cream);
            wing.localRotation = Quaternion.Euler(0f, side * 25f, side * 14f);
            for (int i = 0; i < 3; i++)
                Bar(parent, "Pasta pleat", new Vector3(side * 0.08f, 0.065f, 0f), new Vector3(side * 0.43f, 0.065f, (i - 1) * 0.15f), 0.025f, Gold);
        }
    }

    public static void Ravioli(Transform parent)
    {
        Part(parent, "Ravioli pillow", PrimitiveType.Cube, Vector3.zero, new Vector3(0.62f, 0.2f, 0.62f), Gold);
        Part(parent, "Filled center", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0f), new Vector3(0.45f, 0.2f, 0.45f), Cream);
        for (int i = -2; i <= 2; i++)
            for (int side = -1; side <= 1; side += 2)
            {
                Part(parent, "Crimped edge", PrimitiveType.Cube, new Vector3(i * 0.12f, 0.05f, side * 0.3f), new Vector3(0.04f, 0.04f, 0.12f), Cream);
                Part(parent, "Crimped edge", PrimitiveType.Cube, new Vector3(side * 0.3f, 0.05f, i * 0.12f), new Vector3(0.12f, 0.04f, 0.04f), Cream);
            }
    }

    public static Color IngredientColor(PastaIngredient item) => item == PastaIngredient.Ham ? Pink : item == PastaIngredient.Cheese ? Cream : Gold;

    public static void Ingredient(Transform parent, PastaIngredient item)
    {
        if (item == PastaIngredient.Macaroni)
        {
            for (int n = 0; n < 3; n++)
            {
                var points = new Vector3[14];
                for (int i = 0; i < points.Length; i++)
                {
                    float a = i * Mathf.PI * 0.8f / (points.Length - 1);
                    points[i] = new Vector3(Mathf.Cos(a) * 0.14f + (n - 1) * 0.2f, Mathf.Sin(a) * 0.14f, (n - 1) * 0.08f);
                }
                Line(parent, "Macaroni elbow", Gold, 0.1f, points);
            }
        }
        else if (item == PastaIngredient.Cheese)
        {
            Part(parent, "Cheese", PrimitiveType.Cube, Vector3.zero, new Vector3(0.48f, 0.32f, 0.35f), Cream);
            for (int i = 0; i < 3; i++)
                Part(parent, "Cheese hole", PrimitiveType.Sphere, new Vector3((i - 1) * 0.13f, (i % 2) * 0.12f - 0.05f, 0.172f), new Vector3(0.075f, 0.075f, 0.016f), Gold);
        }
        else
        {
            Part(parent, "Ham rind", PrimitiveType.Cube, Vector3.zero, new Vector3(0.52f, 0.1f, 0.43f), Cream);
            Part(parent, "Ham", PrimitiveType.Cube, Vector3.up * 0.045f, new Vector3(0.45f, 0.07f, 0.36f), Pink);
        }
    }

    public static TextMesh Label(Transform parent, string text, Vector3 position, Color color, float size = 0.055f)
    {
        var go = new GameObject("Loot label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var label = go.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
        label.text = text;
        label.fontSize = 48;
        label.characterSize = size;
        label.anchor = TextAnchor.MiddleCenter;
        label.color = color;
        return label;
    }

    public static void GrannyBicycle(Transform parent)
    {
        Color tyre = new Color(0.08f, 0.09f, 0.13f), chrome = new Color(0.77f, 0.87f, 0.9f);
        Color dress = new Color(0.66f, 0.3f, 0.79f), skin = new Color(1f, 0.77f, 0.61f), hair = new Color(0.86f, 0.88f, 0.93f);
        Vector3 rear = new Vector3(0f, 0.55f, -0.85f), front = new Vector3(0f, 0.55f, 0.85f);
        foreach (var hub in new[] { rear, front })
        {
            var wheel = new GameObject("Granny wheel").transform;
            wheel.SetParent(parent, false);
            wheel.localPosition = hub;
            var rim = Ring(wheel, "Tyre", 0.53f, tyre, 0.12f);
            rim.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var metal = Ring(wheel, "Chrome rim", 0.45f, chrome, 0.04f);
            metal.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Bar(wheel, "Spoke", Vector3.zero, new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * 0.44f, 0.018f, chrome);
            }
        }
        Vector3 crank = new Vector3(0, 0.5f, 0), seat = new Vector3(0, 1.13f, -0.35f), stem = new Vector3(0, 1.17f, 0.64f);
        Bar(parent, "Rear frame", rear, seat, 0.075f, Pink);
        Bar(parent, "Chain stay", rear, crank, 0.075f, Pink);
        Bar(parent, "Seat tube", crank, seat, 0.075f, Pink);
        Bar(parent, "Top tube", seat, stem, 0.075f, Pink);
        Bar(parent, "Down tube", crank, stem, 0.075f, Pink);
        Bar(parent, "Fork", stem, front, 0.075f, chrome);
        Part(parent, "Saddle", PrimitiveType.Cube, seat, new Vector3(0.42f, 0.1f, 0.28f), tyre);
        Bar(parent, "Handle stem", stem, new Vector3(0, 1.42f, 0.67f), 0.065f, chrome);
        Bar(parent, "Handlebar", new Vector3(-0.48f, 1.42f, 0.67f), new Vector3(0.48f, 1.42f, 0.67f), 0.075f, chrome);
        Part(parent, "Granny skirt", PrimitiveType.Cylinder, new Vector3(0, 1.22f, -0.25f), new Vector3(0.65f, 0.22f, 0.62f), dress);
        Part(parent, "Granny cardigan", PrimitiveType.Capsule, new Vector3(0, 1.66f, -0.22f), new Vector3(0.48f, 0.36f, 0.42f), dress);
        Part(parent, "Granny hair", PrimitiveType.Sphere, new Vector3(0, 2.17f, -0.24f), new Vector3(0.56f, 0.55f, 0.5f), hair);
        Part(parent, "Granny bun", PrimitiveType.Sphere, new Vector3(0, 2.32f, -0.47f), Vector3.one * 0.26f, hair);
        Part(parent, "Granny face", PrimitiveType.Sphere, new Vector3(0, 2.13f, -0.08f), new Vector3(0.4f, 0.43f, 0.31f), skin);
        Part(parent, "Granny nose", PrimitiveType.Sphere, new Vector3(0, 2.1f, 0.09f), new Vector3(0.1f, 0.12f, 0.12f), skin);
        for (int side = -1; side <= 1; side += 2)
        {
            var glasses = Ring(parent, "Round glasses", 0.085f, tyre, 0.018f);
            glasses.transform.localPosition = new Vector3(side * 0.105f, 2.18f, 0.07f);
            glasses.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            Part(parent, "Eye", PrimitiveType.Sphere, new Vector3(side * 0.1f, 2.18f, 0.065f), Vector3.one * 0.036f, tyre);
            Bar(parent, "Sleeve", new Vector3(side * 0.22f, 1.78f, -0.22f), new Vector3(side * 0.38f, 1.44f, 0.42f), 0.16f, dress);
            Part(parent, "Hand", PrimitiveType.Sphere, new Vector3(side * 0.39f, 1.44f, 0.61f), Vector3.one * 0.16f, skin);
            Bar(parent, "Leg", new Vector3(side * 0.22f, 1.1f, -0.17f), new Vector3(side * 0.28f, 0.62f, 0.18f), 0.14f, skin);
            Part(parent, "Shoe", PrimitiveType.Cube, new Vector3(side * 0.28f, 0.61f, 0.23f), new Vector3(0.2f, 0.13f, 0.29f), tyre);
        }
        Bar(parent, "Glasses bridge", new Vector3(-0.035f, 2.18f, 0.08f), new Vector3(0.035f, 2.18f, 0.08f), 0.018f, tyre);
        Label(parent, "BRITISH\nCARBONARA", new Vector3(0, 2.8f, 0), Cream, 0.07f);
    }
}
