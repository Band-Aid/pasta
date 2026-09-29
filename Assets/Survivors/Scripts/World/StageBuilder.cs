using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>Builds each stage's environment procedurally and returns its collision arena.</summary>
    public static class StageBuilder
    {
        private static MeshKit kit;
        private static System.Random rng;
        private static Arena arena;

        private static readonly Color Stone = new Color(0.78f, 0.74f, 0.66f);
        private static readonly Color DarkStone = new Color(0.5f, 0.48f, 0.45f);
        private static readonly Color Terracotta = new Color(0.72f, 0.36f, 0.24f);
        private static readonly Color Roof = new Color(0.62f, 0.3f, 0.2f);
        private static readonly Color Wood = new Color(0.5f, 0.32f, 0.18f);
        private static readonly Color Iron = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color Leaf = new Color(0.25f, 0.5f, 0.2f);
        private static readonly Color Glass = new Color(0.2f, 0.26f, 0.32f);

        private static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        private static T Pick<T>(T[] arr) => arr[rng.Next(arr.Length)];

        public static Arena Build(StageDef stage, Transform root)
        {
            kit = new MeshKit();
            rng = new System.Random(1234 + stage.index * 77);
            switch (stage.theme)
            {
                case StageTheme.Venezia: return Venezia(root);
                case StageTheme.Napoli: return Napoli(root);
                default: return Roma(root);
            }
        }

        // ---------------- shared ----------------

        private static void Lighting(Color sun, float intensity, Vector3 euler, Color sky, Color ground, Color fog, Color background, float fogEnd = 170f)
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
            }
            light.color = sun;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            light.transform.rotation = Quaternion.Euler(euler);
            Shader.SetGlobalColor("_PS_AmbientSky", sky.linear);
            Shader.SetGlobalColor("_PS_AmbientGround", ground.linear);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = fogEnd;
            if (G.Cam != null && G.Cam.Cam != null) G.Cam.Cam.backgroundColor = background;
        }

        private static void Ground(Transform root, Texture2D tex, Vector2 min, Vector2 max, float tile, float margin)
        {
            var size = max - min + Vector2.one * margin * 2f;
            var center = (min + max) * 0.5f;
            var mesh = new MeshKit().Quad(Vector3.zero, Vector2.one, Color.white).ToMesh("Ground");
            var go = Models.Static(mesh, root, "Ground", Mats.Textured(tex, 1f, "Ground"), false);
            go.transform.position = new Vector3(center.x, 0f, center.y);
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            var mat = go.GetComponent<MeshRenderer>().sharedMaterial;
            mat.SetTextureScale("_BaseMap", new Vector2(size.x / tile, size.y / tile));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.5f, 0f);
            col.size = new Vector3(1f, 1f, 1f);
        }

        private static void Water(Transform root, Vector3 center, Vector2 size)
        {
            var mesh = new MeshKit().Quad(Vector3.zero, Vector2.one, Color.white).ToMesh("Water");
            var mat = Mats.WaterMaterial();
            mat.SetTextureScale("_BaseMap", new Vector2(size.x / 6f, size.y / 6f));
            var go = Models.Static(mesh, root, "Water", mat, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, 1f, size.y);
            go.AddComponent<WaterScroll>();
            // Dark canal bed underneath for depth.
            kit.Box(center + Vector3.down * 0.9f, new Vector3(size.x, 0.2f, size.y), new Color(0.08f, 0.2f, 0.22f));
        }

        private static void Flush(Transform root, string name, bool shadows = true)
        {
            if (kit.VertexCount == 0) return;
            Models.Static(kit.ToMesh(name), root, name, Mats.Lit, shadows);
            kit.Clear();
        }

        /// <summary>A facade building. <paramref name="facing"/> points from the building toward the arena.</summary>
        private static void Building(Vector3 basePos, Vector3 facing, float width, float depth, float height, Color facade, Color shutter, int style)
        {
            var rot = Quaternion.LookRotation(facing);
            kit.Frame = Matrix4x4.TRS(basePos, rot, Vector3.one);
            kit.Box(new Vector3(0, height * 0.5f, 0), new Vector3(width, height, depth), facade);
            kit.Box(new Vector3(0, height + 0.15f, 0), new Vector3(width + 0.4f, 0.3f, depth + 0.4f), style == 1 ? Stone : Roof * 1.1f);
            if (style != 1)
            {
                kit.Box(new Vector3(0, height + 0.55f, -0.4f), new Vector3(width + 0.2f, 0.5f, depth * 0.6f), Roof, new Vector3(12, 0, 0));
                kit.Box(new Vector3(0, height + 0.55f, 0.4f + depth * 0.15f), new Vector3(width + 0.2f, 0.5f, depth * 0.45f), Roof, new Vector3(-12, 0, 0));
            }
            float face = depth * 0.5f + 0.02f;
            int cols = Mathf.Max(2, Mathf.RoundToInt(width / 2.6f));
            int rows = Mathf.Clamp(Mathf.RoundToInt((height - 3f) / 2.9f), 1, 5);
            float colStep = width / cols;
            kit.Box(new Vector3(0, 0.45f, face - 0.05f), new Vector3(width + 0.05f, 0.9f, 0.12f), facade * 0.8f);
            for (int r = 0; r < rows; r++)
            {
                float y = 3.6f + r * 2.9f;
                for (int c = 0; c < cols; c++)
                {
                    float x = -width * 0.5f + (c + 0.5f) * colStep;
                    var p = new Vector3(x, y, face);
                    if (style == 1)
                    {
                        // Venetian gothic: pointed arch window.
                        kit.Box(p, new Vector3(0.9f, 1.4f, 0.06f), Glass);
                        kit.Cone(p + new Vector3(0, 0.95f, 0), new Vector3(0.9f, 0.5f, 0.06f), Glass, default, 0f, 4);
                        kit.Box(p + new Vector3(0, -0.8f, 0.08f), new Vector3(1.2f, 0.12f, 0.2f), Stone);
                    }
                    else
                    {
                        kit.Box(p, new Vector3(0.9f, 1.4f, 0.06f), Glass);
                        kit.Box(p + new Vector3(-0.62f, 0, 0.04f), new Vector3(0.32f, 1.45f, 0.06f), shutter);
                        kit.Box(p + new Vector3(0.62f, 0, 0.04f), new Vector3(0.32f, 1.45f, 0.06f), shutter);
                        kit.Box(p + new Vector3(0, -0.8f, 0.08f), new Vector3(1.3f, 0.12f, 0.2f), Stone);
                        if (r == 0 && rng.NextDouble() < 0.35)
                        {
                            kit.Box(p + new Vector3(0, -0.9f, 0.45f), new Vector3(1.6f, 0.1f, 0.8f), Stone);
                            for (int b = 0; b < 5; b++) kit.Box(p + new Vector3(-0.7f + b * 0.35f, -0.55f, 0.82f), new Vector3(0.04f, 0.6f, 0.04f), Iron);
                            kit.Box(p + new Vector3(0, -0.25f, 0.82f), new Vector3(1.6f, 0.05f, 0.05f), Iron);
                            if (rng.NextDouble() < 0.6)
                                for (int b = 0; b < 3; b++) kit.Ball(p + new Vector3(-0.5f + b * 0.5f, -0.6f, 0.65f), new Vector3(0.35f, 0.3f, 0.3f), rng.NextDouble() < 0.5 ? new Color(0.85f, 0.2f, 0.25f) : Leaf);
                        }
                    }
                }
            }
            // Ground floor: door and a shop front.
            kit.Box(new Vector3(-width * 0.2f, 1.2f, face), new Vector3(1.4f, 2.4f, 0.08f), Wood * 0.8f);
            kit.Box(new Vector3(-width * 0.2f, 2.5f, face + 0.02f), new Vector3(1.7f, 0.2f, 0.1f), Stone);
            if (width > 6f)
            {
                kit.Box(new Vector3(width * 0.2f, 1.3f, face), new Vector3(2.4f, 1.8f, 0.06f), Glass * 1.4f);
                var awning = Pick(new[] { new Color(0.75f, 0.15f, 0.15f), new Color(0.15f, 0.45f, 0.25f), new Color(0.2f, 0.3f, 0.6f) });
                for (int s = 0; s < 6; s++)
                    kit.Box(new Vector3(width * 0.2f - 1.25f + s * 0.5f, 2.55f, face + 0.6f), new Vector3(0.5f, 0.06f, 1.3f), s % 2 == 0 ? awning : Color.white, new Vector3(-20, 0, 0));
            }
            kit.Frame = Matrix4x4.identity;
        }

        private static void Row(Vector3 start, Vector3 along, Vector3 facing, float length, float depth, Vector2 heights, Color[] facades, Color[] shutters, int style)
        {
            float used = 0f;
            while (used < length - 3f)
            {
                float w = Mathf.Min(R(7f, 10f), length - used);
                var pos = start + along * (used + w * 0.5f) - facing * depth * 0.5f;
                Building(pos, facing, w - 0.05f, depth, R(heights.x, heights.y), Pick(facades), Pick(shutters), style);
                used += w;
            }
        }

        private static void Obstacle(Vector3 p, float r) => arena.AddObstacle(p, r);

        private static bool Free(Vector3 p, float r)
        {
            if (p.magnitude < 9f) return false;
            if (!arena.Inside(p, r + 2f)) return false;
            return !arena.Blocked(p, r + 1.5f);
        }

        private static Vector3 RandomSpot(float r)
        {
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(R(arena.min.x, arena.max.x), 0f, R(arena.min.y, arena.max.y));
                if (Free(p, r)) return p;
            }
            return new Vector3(9999, 0, 9999);
        }

        // ---------------- props ----------------

        private static void Fountain(Vector3 p, float r)
        {
            kit.Cyl(p + Vector3.up * 0.4f, new Vector3(r * 2f, 0.8f, r * 2f), Stone, default, 20);
            kit.Cyl(p + Vector3.up * 0.84f, new Vector3(r * 1.8f, 0.06f, r * 1.8f), new Color(0.35f, 0.62f, 0.75f), default, 20);
            kit.Cyl(p + Vector3.up * 1.3f, new Vector3(0.8f, 1.6f, 0.8f), Stone);
            kit.Cyl(p + Vector3.up * 2.1f, new Vector3(r * 0.9f, 0.25f, r * 0.9f), Stone, default, 16);
            kit.Cyl(p + Vector3.up * 2.9f, new Vector3(0.4f, 1.4f, 0.4f), Stone);
            kit.Ball(p + Vector3.up * 3.8f, new Vector3(0.7f, 0.9f, 0.7f), new Color(0.6f, 0.7f, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0, i * 90 + 45, 0) * Vector3.forward;
                kit.Ball(p + d * r * 0.95f + Vector3.up * 0.9f, new Vector3(0.5f, 0.6f, 0.5f), new Color(0.6f, 0.68f, 0.62f));
            }
            Obstacle(p, r);
        }

        private static void Obelisk(Vector3 p)
        {
            kit.Box(p + Vector3.up * 0.8f, new Vector3(2.2f, 1.6f, 2.2f), Stone);
            kit.Cone(p + Vector3.up * 6f, new Vector3(1.2f, 9f, 1.2f), new Color(0.85f, 0.75f, 0.6f), new Vector3(0, 45, 0), 0.55f, 4);
            kit.Cone(p + Vector3.up * 10.8f, new Vector3(0.66f, 0.7f, 0.66f), new Color(0.85f, 0.75f, 0.6f), new Vector3(0, 45, 0), 0f, 4);
            Obstacle(p, 1.4f);
        }

        private static void Cafe(Vector3 p, Color stripe)
        {
            float rot = R(0, 360);
            kit.Cyl(p + Vector3.up * 1.3f, new Vector3(0.07f, 2.6f, 0.07f), Wood);
            for (int i = 0; i < 8; i++)
            {
                float a = rot + i * 45f;
                var d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                kit.Box(p + d * 0.8f + Vector3.up * 2.45f, new Vector3(0.75f, 0.05f, 1.2f), i % 2 == 0 ? stripe : Color.white, new Vector3(22, a, 0));
            }
            kit.Cyl(p + Vector3.up * 0.75f, new Vector3(0.9f, 0.05f, 0.9f), Color.white, default, 14);
            kit.Cyl(p + Vector3.up * 0.37f, new Vector3(0.08f, 0.74f, 0.08f), Iron);
            kit.Cyl(p + Vector3.up * 0.8f, new Vector3(0.12f, 0.12f, 0.12f), new Color(0.6f, 0.1f, 0.15f));
            for (int c = 0; c < 3; c++)
            {
                var d = Quaternion.Euler(0, rot + c * 120f, 0) * Vector3.forward;
                var cp = p + d * 0.85f;
                kit.Box(cp + Vector3.up * 0.45f, new Vector3(0.45f, 0.06f, 0.45f), Wood);
                kit.Box(cp + Vector3.up * 0.75f + d * 0.2f, new Vector3(0.45f, 0.55f, 0.05f), Wood, new Vector3(0, rot + c * 120f, 0));
                kit.Box(cp + Vector3.up * 0.22f, new Vector3(0.35f, 0.44f, 0.35f), Iron * 1.5f);
            }
            Obstacle(p, 1.2f);
        }

        private static void Vespa(Vector3 p, Color color)
        {
            kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(0, R(0, 360), 0), Vector3.one);
            kit.Box(new Vector3(0, 0.45f, -0.25f), new Vector3(0.5f, 0.45f, 0.8f), color);
            kit.Ball(new Vector3(0, 0.5f, -0.45f), new Vector3(0.6f, 0.5f, 0.7f), color);
            kit.Box(new Vector3(0, 0.3f, 0.3f), new Vector3(0.36f, 0.12f, 0.6f), color);
            kit.Box(new Vector3(0, 0.75f, 0.6f), new Vector3(0.42f, 0.8f, 0.1f), color, new Vector3(-12, 0, 0));
            kit.Bar(new Vector3(-0.35f, 1.15f, 0.62f), new Vector3(0.35f, 1.15f, 0.62f), 0.05f, Iron);
            kit.Box(new Vector3(0, 0.72f, -0.3f), new Vector3(0.38f, 0.08f, 0.6f), new Color(0.3f, 0.18f, 0.1f));
            kit.Cyl(new Vector3(0, 0.2f, 0.62f), new Vector3(0.36f, 0.12f, 0.36f), Iron, new Vector3(0, 0, 90));
            kit.Cyl(new Vector3(0, 0.2f, -0.6f), new Vector3(0.36f, 0.12f, 0.36f), Iron, new Vector3(0, 0, 90));
            kit.Frame = Matrix4x4.identity;
            Obstacle(p, 0.8f);
        }

        private static void Planter(Vector3 p)
        {
            kit.Box(p + Vector3.up * 0.35f, new Vector3(0.9f, 0.7f, 0.9f), Terracotta);
            kit.Ball(p + Vector3.up * 1.05f, new Vector3(1.1f, 0.9f, 1.1f), Leaf);
            kit.Ball(p + new Vector3(0.2f, 1.4f, -0.1f), new Vector3(0.6f, 0.6f, 0.6f), Leaf * 1.2f);
            Obstacle(p, 0.7f);
        }

        private static void Lamp(Vector3 p)
        {
            kit.Cyl(p + Vector3.up * 0.2f, new Vector3(0.3f, 0.4f, 0.3f), Iron);
            kit.Cyl(p + Vector3.up * 2f, new Vector3(0.12f, 3.6f, 0.12f), Iron);
            kit.Ball(p + Vector3.up * 3.95f, new Vector3(0.4f, 0.5f, 0.4f), new Color(1f, 0.95f, 0.75f));
            kit.Cone(p + Vector3.up * 4.3f, new Vector3(0.5f, 0.25f, 0.5f), Iron, default, 0f, 6);
            Obstacle(p, 0.25f);
        }

        private static void Bunting(Vector3 a, Vector3 b, int count)
        {
            kit.Bar(a, b, 0.03f, Iron);
            Color[] flag = { new Color(0.1f, 0.55f, 0.25f), Color.white, new Color(0.85f, 0.15f, 0.15f) };
            for (int i = 1; i < count; i++)
            {
                float t = (float)i / count;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * 0.8f;
                kit.Cone(p + Vector3.down * 0.25f, new Vector3(0.35f, 0.5f, 0.05f), flag[i % 3], new Vector3(180, Quaternion.LookRotation(b - a).eulerAngles.y + 90f, 0), 0f, 3);
            }
        }

        private static void Laundry(Vector3 a, Vector3 b)
        {
            kit.Bar(a, b, 0.025f, Color.white);
            Color[] clothes = { Color.white, new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.85f), new Color(0.95f, 0.85f, 0.3f), new Color(0.9f, 0.6f, 0.75f) };
            int n = Mathf.Max(3, (int)(Vector3.Distance(a, b) / 1.3f));
            float yaw = Quaternion.LookRotation(b - a).eulerAngles.y;
            for (int i = 1; i < n; i++)
            {
                float t = (float)i / n;
                var p = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.6f + 0.45f);
                kit.Box(p, new Vector3(0.7f, 0.85f, 0.04f), Pick(clothes), new Vector3(0, yaw, 0));
            }
        }

        // ---------------- Roma ----------------

        private static Arena Roma(Transform root)
        {
            arena = new Arena(new Vector2(-48, -48), new Vector2(48, 48));
            Lighting(new Color(1f, 0.93f, 0.8f), 1.25f, new Vector3(50, -35, 0), new Color(0.62f, 0.66f, 0.72f), new Color(0.45f, 0.38f, 0.32f),
                new Color(0.86f, 0.8f, 0.72f), new Color(0.62f, 0.78f, 0.95f));
            Ground(root, Mats.Cobble, arena.min, arena.max, 5f, 45f);

            // Central piazza: big fountain and an obelisk.
            // Campidoglio-style inlay: concentric bands and a twelve-point star.
            for (int ring = 0; ring < 9; ring++)
            {
                float d = 34f - ring * 3.6f;
                kit.Cyl(new Vector3(0, 0.02f + ring * 0.006f, 0), new Vector3(d, 0.04f, d), ring % 2 == 0 ? new Color(0.8f, 0.74f, 0.64f) : new Color(0.73f, 0.67f, 0.58f), default, 48);
            }
            for (int i = 0; i < 12; i++)
                kit.Box(new Vector3(0, 0.08f, 0), new Vector3(0.18f, 0.02f, 30f), new Color(0.84f, 0.79f, 0.7f), new Vector3(0, i * 15f, 0));
            Flush(root, "Piazza", false);
            Fountain(new Vector3(0, 0, 22f), 3.6f);
            Obelisk(new Vector3(-24f, 0, -8f));
            Fountain(new Vector3(26f, 0, -20f), 2.6f);

            Color[] facades = { new Color(0.88f, 0.62f, 0.32f), new Color(0.82f, 0.45f, 0.3f), new Color(0.92f, 0.78f, 0.5f), new Color(0.86f, 0.56f, 0.5f), new Color(0.95f, 0.86f, 0.66f) };
            Color[] shutters = { new Color(0.22f, 0.4f, 0.25f), new Color(0.45f, 0.28f, 0.18f), new Color(0.3f, 0.45f, 0.35f) };
            Row(new Vector3(-58, 0, 49), Vector3.right, Vector3.back, 116, 8, new Vector2(10, 15), facades, shutters, 0);
            Row(new Vector3(49, 0, 49), Vector3.back, Vector3.left, 100, 8, new Vector2(9, 13), facades, shutters, 0);
            Row(new Vector3(-49, 0, -51), Vector3.forward, Vector3.right, 100, 8, new Vector2(9, 13), facades, shutters, 0);
            // Far south row: behind the top-down camera, but closes the square in first person.
            Row(new Vector3(-70, 0, -64), Vector3.right, Vector3.forward, 140, 8, new Vector2(10, 14), facades, shutters, 0);
            Flush(root, "Buildings");
            // South edge: a low balustrade so the camera never hides the player.
            for (float x = -48; x <= 48; x += 1.2f)
            {
                kit.Cyl(new Vector3(x, 0.45f, -49.5f), new Vector3(0.3f, 0.9f, 0.3f), Stone, default, 8);
            }
            kit.Box(new Vector3(0, 0.95f, -49.5f), new Vector3(97f, 0.15f, 0.5f), Stone);
            kit.Box(new Vector3(0, 0.05f, -49.5f), new Vector3(97f, 0.15f, 0.6f), Stone);
            // Colosseum backdrop beyond the north buildings.
            Colosseum(new Vector3(10, 0, 95), 38f);
            Flush(root, "Backdrop");

            Bunting(new Vector3(-30, 9, 44), new Vector3(-10, 9.5f, 44), 12);
            Bunting(new Vector3(10, 9, 44), new Vector3(35, 9, 44), 14);
            Laundry(new Vector3(44.5f, 7.5f, 20), new Vector3(44.5f, 7.8f, 32));
            Laundry(new Vector3(-44.5f, 7.5f, -10), new Vector3(-44.5f, 7.8f, 4));

            for (int i = 0; i < 9; i++) { var p = RandomSpot(1.3f); Cafe(p, rng.NextDouble() < 0.5 ? new Color(0.8f, 0.15f, 0.15f) : new Color(0.15f, 0.45f, 0.25f)); }
            for (int i = 0; i < 7; i++) Vespa(RandomSpot(1f), Pick(new[] { new Color(0.55f, 0.85f, 0.75f), new Color(0.85f, 0.2f, 0.2f), new Color(0.95f, 0.9f, 0.75f), new Color(0.3f, 0.5f, 0.85f) }));
            for (int i = 0; i < 14; i++) Planter(RandomSpot(0.8f));
            for (int x = -40; x <= 40; x += 16) { Lamp(new Vector3(x, 0, 44)); Lamp(new Vector3(x + 8, 0, -45)); }
            Flush(root, "Props");
            return arena;
        }

        private static void Colosseum(Vector3 c, float radius)
        {
            var travertine = new Color(0.85f, 0.76f, 0.6f);
            int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(-110f, 110f, (float)i / (segments - 1));
                var d = Quaternion.Euler(0, a + 180f, 0) * Vector3.forward;
                var p = c + d * radius;
                float tiers = i % 5 == 0 ? 3 : 4;
                for (int t = 0; t < tiers; t++)
                {
                    float y = t * 6f;
                    var rot = new Vector3(0, a, 0);
                    kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(rot), Vector3.one);
                    kit.Box(new Vector3(-3.3f, y + 3f, 0), new Vector3(1.2f, 6f, 2.2f), travertine * (0.9f + t * 0.03f));
                    kit.Box(new Vector3(0, y + 5.5f, 0), new Vector3(7.8f, 1f, 2.2f), travertine * 0.95f);
                    kit.Ball(new Vector3(0, y + 4.8f, 0), new Vector3(5.4f, 1.6f, 2.3f), travertine * 0.95f);
                    kit.Box(new Vector3(0, y + 2.4f, 0.6f), new Vector3(5.4f, 4.8f, 0.4f), new Color(0.3f, 0.26f, 0.2f));
                }
            }
            kit.Frame = Matrix4x4.identity;
        }

        // ---------------- Venezia ----------------

        private static Arena Venezia(Transform root)
        {
            arena = new Arena(new Vector2(-50, -36), new Vector2(50, 36));
            Lighting(new Color(1f, 0.9f, 0.86f), 1.15f, new Vector3(42, 30, 0), new Color(0.66f, 0.68f, 0.78f), new Color(0.42f, 0.4f, 0.42f),
                new Color(0.85f, 0.84f, 0.88f), new Color(0.75f, 0.82f, 0.95f));
            Ground(root, Mats.Herringbone, new Vector2(-50, -36), new Vector2(50, 36), 6f, 6f);

            // Canals on the north and south with gondolas and mooring poles.
            Water(root, new Vector3(0, -0.35f, -51f), new Vector2(220f, 28f));
            Water(root, new Vector3(0, -0.35f, 47.5f), new Vector2(220f, 21f));
            Water(root, new Vector3(-66f, -0.35f, 0f), new Vector2(20f, 80f));
            Water(root, new Vector3(66f, -0.35f, 0f), new Vector2(20f, 80f));
            // Quay edges
            kit.Box(new Vector3(0, -0.2f, -37f), new Vector3(112f, 0.6f, 2f), Stone);
            kit.Box(new Vector3(0, -0.2f, 37f), new Vector3(112f, 0.6f, 2f), Stone);
            kit.Box(new Vector3(-51f, -0.2f, 0f), new Vector3(2f, 0.6f, 76f), Stone);
            kit.Box(new Vector3(51f, -0.2f, 0f), new Vector3(2f, 0.6f, 76f), Stone);
            for (float x = -45; x <= 45; x += 9f)
            {
                Gondola(new Vector3(x + R(-2, 2), -0.3f, -40.5f), R(-10, 10) + 90f);
                Pole(new Vector3(x + 4f, 0, -38.5f));
            }
            // North: basilica backdrop across the canal.
            Basilica(new Vector3(0, 0, 66f));
            Campanile(new Vector3(-34f, 0, 66f));
            Color[] facades = { new Color(0.95f, 0.88f, 0.85f), new Color(0.9f, 0.7f, 0.68f), new Color(0.86f, 0.8f, 0.7f) };
            Color[] shutters = { new Color(0.3f, 0.42f, 0.35f) };
            Row(new Vector3(-100, 0, 58), Vector3.right, Vector3.back, 45, 8, new Vector2(11, 14), facades, shutters, 1);
            Row(new Vector3(40, 0, 58), Vector3.right, Vector3.back, 55, 8, new Vector2(11, 14), facades, shutters, 1);
            Row(new Vector3(-78, 0, 40), Vector3.back, Vector3.right, 80, 8, new Vector2(10, 13), facades, shutters, 1);
            Row(new Vector3(78, 0, -40), Vector3.forward, Vector3.left, 80, 8, new Vector2(10, 13), facades, shutters, 1);
            Row(new Vector3(-100, 0, -67), Vector3.right, Vector3.forward, 200, 8, new Vector2(10, 14), facades, shutters, 1);
            Flush(root, "Venice");

            for (int i = 0; i < 6; i++) WellHead(RandomSpot(1.2f));
            LionColumn(new Vector3(-20f, 0, -24f));
            LionColumn(new Vector3(20f, 0, -24f));
            for (int i = 0; i < 7; i++) { var p = RandomSpot(1.3f); Cafe(p, new Color(0.2f, 0.25f, 0.5f)); }
            for (int x = -36; x <= 36; x += 12) Lamp(new Vector3(x, 0, 33f));
            for (int i = 0; i < 40; i++) Pigeon(new Vector3(R(-45, 45), 0, R(-32, 32)));
            Flush(root, "Props");
            return arena;
        }

        private static void Gondola(Vector3 p, float yaw)
        {
            kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            var black = new Color(0.08f, 0.08f, 0.1f);
            kit.Ball(Vector3.zero, new Vector3(1.4f, 0.7f, 9f), black);
            kit.Box(new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.1f, 6f), new Color(0.5f, 0.12f, 0.15f));
            kit.Box(new Vector3(0, 0.9f, 4.4f), new Vector3(0.12f, 1.2f, 0.4f), new Color(0.85f, 0.8f, 0.7f), new Vector3(-20, 0, 0));
            kit.Cone(new Vector3(0, 0.8f, -4.4f), new Vector3(0.3f, 1f, 0.3f), black, new Vector3(20, 0, 0), 0.3f, 6);
            kit.Frame = Matrix4x4.identity;
        }

        private static void Pole(Vector3 p)
        {
            for (int i = 0; i < 5; i++)
                kit.Cyl(p + Vector3.up * (i * 0.7f + 0.1f), new Vector3(0.28f, 0.7f, 0.28f), i % 2 == 0 ? new Color(0.2f, 0.3f, 0.7f) : Color.white, default, 8);
            kit.Cone(p + Vector3.up * 3.7f, new Vector3(0.32f, 0.3f, 0.32f), new Color(0.2f, 0.3f, 0.7f), default, 0f, 8);
        }

        private static void Basilica(Vector3 c)
        {
            var marble = new Color(0.93f, 0.88f, 0.82f);
            kit.Box(c + new Vector3(0, 8, 0), new Vector3(56, 16, 14), marble);
            for (int i = 0; i < 5; i++)
            {
                var ap = c + new Vector3(-20 + i * 10, 0, -7.1f);
                kit.Box(ap + new Vector3(0, 5, 0), new Vector3(7f, 10f, 0.4f), new Color(0.45f, 0.3f, 0.25f));
                kit.Ball(ap + new Vector3(0, 10f, 0), new Vector3(7f, 5f, 0.5f), new Color(0.9f, 0.7f, 0.3f));
                kit.Cone(ap + new Vector3(0, 15f, 0.3f), new Vector3(2f, 4f, 0.8f), marble, default, 0f, 4);
            }
            float[] domes = { -18, 0, 18, 0, 0 };
            for (int i = 0; i < 3; i++)
            {
                var d = c + new Vector3(domes[i], 16f, 2f);
                kit.Cyl(d + Vector3.up * 1.5f, new Vector3(8f, 3f, 8f), marble, default, 16);
                kit.Ball(d + Vector3.up * 4f, new Vector3(8.4f, 7f, 8.4f), new Color(0.55f, 0.6f, 0.62f), default, true);
                kit.Cone(d + Vector3.up * 8.2f, new Vector3(0.8f, 2.2f, 0.8f), new Color(1f, 0.82f, 0.35f), default, 0f, 8);
            }
        }

        private static void Campanile(Vector3 p)
        {
            var brick = new Color(0.7f, 0.36f, 0.26f);
            kit.Box(p + Vector3.up * 18f, new Vector3(7f, 36f, 7f), brick);
            kit.Box(p + Vector3.up * 38.5f, new Vector3(7.4f, 5f, 7.4f), new Color(0.9f, 0.86f, 0.8f));
            kit.Box(p + Vector3.up * 38.5f, new Vector3(7.6f, 3.2f, 5f), new Color(0.3f, 0.25f, 0.2f));
            kit.Box(p + Vector3.up * 38.5f, new Vector3(5f, 3.2f, 7.6f), new Color(0.3f, 0.25f, 0.2f));
            kit.Box(p + Vector3.up * 42.5f, new Vector3(7f, 3f, 7f), brick);
            kit.Cone(p + Vector3.up * 48f, new Vector3(9f, 8f, 9f), new Color(0.35f, 0.62f, 0.5f), new Vector3(0, 45, 0), 0f, 4);
            kit.Ball(p + Vector3.up * 52.4f, new Vector3(0.8f, 1.2f, 0.8f), new Color(1f, 0.82f, 0.35f));
        }

        private static void WellHead(Vector3 p)
        {
            kit.Cyl(p + Vector3.up * 0.15f, new Vector3(2.4f, 0.3f, 2.4f), Stone, default, 12);
            kit.Cone(p + Vector3.up * 0.75f, new Vector3(1.8f, 0.9f, 1.8f), new Color(0.9f, 0.86f, 0.8f), default, 1.15f, 12);
            kit.Cyl(p + Vector3.up * 1.25f, new Vector3(2.1f, 0.12f, 2.1f), Stone, default, 12);
            kit.Cyl(p + Vector3.up * 1.3f, new Vector3(1.5f, 0.1f, 1.5f), new Color(0.1f, 0.12f, 0.14f), default, 12);
            Obstacle(p, 1.2f);
        }

        private static void LionColumn(Vector3 p)
        {
            kit.Box(p + Vector3.up * 0.6f, new Vector3(2.4f, 1.2f, 2.4f), Stone);
            kit.Cyl(p + Vector3.up * 6f, new Vector3(1.1f, 10f, 1.1f), new Color(0.75f, 0.72f, 0.7f), default, 12);
            kit.Box(p + Vector3.up * 11.3f, new Vector3(1.8f, 0.6f, 1.8f), Stone);
            var bronze = new Color(0.45f, 0.55f, 0.45f);
            kit.Ball(p + new Vector3(0, 12.4f, 0), new Vector3(1f, 1f, 2f), bronze);
            kit.Ball(p + new Vector3(0, 13.1f, 0.9f), new Vector3(0.9f, 0.9f, 0.9f), bronze);
            kit.Box(p + new Vector3(0.6f, 13.2f, -0.2f), new Vector3(0.1f, 0.9f, 1.4f), bronze, new Vector3(0, 0, -30));
            kit.Box(p + new Vector3(-0.6f, 13.2f, -0.2f), new Vector3(0.1f, 0.9f, 1.4f), bronze, new Vector3(0, 0, 30));
            Obstacle(p, 1.3f);
        }

        private static void Pigeon(Vector3 p)
        {
            if (!arena.Inside(p, 1f) || arena.Blocked(p, 0.5f) || p.magnitude < 6f) return;
            var grey = new Color(0.55f, 0.56f, 0.6f);
            float yaw = R(0, 360);
            kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            kit.Ball(new Vector3(0, 0.14f, 0), new Vector3(0.18f, 0.16f, 0.3f), grey);
            kit.Ball(new Vector3(0, 0.25f, 0.12f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.4f, 0.48f, 0.5f));
            kit.Cone(new Vector3(0, 0.24f, 0.2f), new Vector3(0.04f, 0.06f, 0.04f), new Color(0.9f, 0.6f, 0.3f), new Vector3(90, 0, 0), 0f, 4);
            kit.Frame = Matrix4x4.identity;
        }

        // ---------------- Napoli ----------------

        private static Arena Napoli(Transform root)
        {
            arena = new Arena(new Vector2(-56, -34), new Vector2(56, 34));
            Lighting(new Color(1f, 0.82f, 0.62f), 1.3f, new Vector3(35, -60, 0), new Color(0.66f, 0.6f, 0.62f), new Color(0.42f, 0.32f, 0.28f),
                new Color(0.95f, 0.75f, 0.6f), new Color(0.98f, 0.72f, 0.55f), 320f);
            Ground(root, Mats.Basalt, arena.min, arena.max, 4f, 40f);

            // Sea to the south, Vesuvio to the north.
            Water(root, new Vector3(0, -0.35f, -60f), new Vector2(260f, 40f));
            kit.Box(new Vector3(0, -0.2f, -35f), new Vector3(114f, 0.6f, 2f), DarkStone);
            for (float x = -54; x <= 54; x += 1.4f) kit.Cyl(new Vector3(x, 0.45f, -35f), new Vector3(0.25f, 0.9f, 0.25f), Stone, default, 8);
            kit.Box(new Vector3(0, 0.95f, -35f), new Vector3(112f, 0.15f, 0.45f), Stone);
            kit.Cone(new Vector3(20, 27.5f, 150), new Vector3(170, 55, 120), new Color(0.45f, 0.4f, 0.42f), default, 0.18f, 24);
            kit.Cone(new Vector3(-40, 19f, 165), new Vector3(120, 38, 90), new Color(0.5f, 0.45f, 0.46f), default, 0.3f, 20);
            kit.Ball(new Vector3(24, 60, 150), new Vector3(20, 10, 14), new Color(0.85f, 0.82f, 0.8f));
            kit.Ball(new Vector3(30, 68, 148), new Vector3(16, 8, 12), new Color(0.9f, 0.88f, 0.86f));
            // Capri on the horizon for the first-person view out to sea.
            kit.Cone(new Vector3(-60, 6f, -170), new Vector3(70, 12, 30), new Color(0.45f, 0.5f, 0.5f), default, 0.35f, 16);
            kit.Cone(new Vector3(-45, 10f, -172), new Vector3(30, 20, 20), new Color(0.42f, 0.48f, 0.48f), default, 0.2f, 12);
            Flush(root, "Backdrop");

            Color[] facades = { new Color(0.95f, 0.78f, 0.3f), new Color(0.85f, 0.35f, 0.25f), new Color(0.6f, 0.75f, 0.85f), new Color(0.95f, 0.65f, 0.55f), new Color(0.78f, 0.6f, 0.4f) };
            Color[] shutters = { new Color(0.2f, 0.4f, 0.25f), new Color(0.35f, 0.25f, 0.2f), new Color(0.9f, 0.9f, 0.85f) };
            Row(new Vector3(-66, 0, 35), Vector3.right, Vector3.back, 132, 8, new Vector2(12, 18), facades, shutters, 0);
            Row(new Vector3(57, 0, 35), Vector3.back, Vector3.left, 69, 8, new Vector2(12, 16), facades, shutters, 0);
            Row(new Vector3(-57, 0, -34), Vector3.forward, Vector3.right, 69, 8, new Vector2(12, 16), facades, shutters, 0);
            Flush(root, "Buildings");
            for (float x = -50; x < 50; x += 11f)
            {
                Laundry(new Vector3(x, 10f, 30.5f), new Vector3(x + 7f, 10.4f, 30.5f));
                Laundry(new Vector3(x + 2f, 13.5f, 30.5f), new Vector3(x + 9f, 13.2f, 30.5f));
            }
            for (float z = -26; z < 26; z += 10f)
            {
                Laundry(new Vector3(52.5f, 9f, z), new Vector3(52.5f, 9.3f, z + 7f));
                Laundry(new Vector3(-52.5f, 9f, z), new Vector3(-52.5f, 9.3f, z + 7f));
            }
            Bunting(new Vector3(-40, 11, 30), new Vector3(-15, 11.5f, 30), 16);
            Bunting(new Vector3(5, 11, 30), new Vector3(40, 11, 30), 20);

            for (int i = 0; i < 6; i++) PizzaOven(RandomSpot(1.6f));
            for (int i = 0; i < 5; i++) FruitStand(RandomSpot(1.4f));
            for (int i = 0; i < 8; i++) LemonTree(RandomSpot(1f));
            for (int i = 0; i < 10; i++) Vespa(RandomSpot(1f), Pick(new[] { new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.25f, 0.3f), new Color(0.55f, 0.85f, 0.75f), new Color(0.95f, 0.8f, 0.3f) }));
            for (int x = -48; x <= 48; x += 16) Lamp(new Vector3(x, 0, -32f));
            Flush(root, "Props");
            return arena;
        }

        private static void PizzaOven(Vector3 p)
        {
            var brick = new Color(0.72f, 0.4f, 0.28f);
            kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(0, R(0, 360), 0), Vector3.one);
            kit.Box(new Vector3(0, 0.5f, 0), new Vector3(2.6f, 1f, 2.6f), Stone);
            kit.Ball(new Vector3(0, 1f, 0), new Vector3(2.5f, 2.2f, 2.5f), brick, default, true);
            kit.Box(new Vector3(0, 1.4f, 1.05f), new Vector3(0.9f, 0.7f, 0.4f), new Color(0.1f, 0.06f, 0.05f));
            kit.Ball(new Vector3(0, 1.4f, 1.1f), new Vector3(0.6f, 0.4f, 0.2f), new Color(1f, 0.55f, 0.15f));
            kit.Cyl(new Vector3(0.6f, 2.5f, -0.5f), new Vector3(0.35f, 1.2f, 0.35f), brick * 0.8f);
            kit.Frame = Matrix4x4.identity;
            Obstacle(p, 1.5f);
        }

        private static void FruitStand(Vector3 p)
        {
            float yaw = R(0, 360);
            kit.Frame = Matrix4x4.TRS(p, Quaternion.Euler(0, yaw, 0), Vector3.one);
            kit.Box(new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.9f, 1.2f), Wood);
            Color[] fruit = { new Color(1f, 0.85f, 0.15f), new Color(0.85f, 0.12f, 0.1f), new Color(0.95f, 0.5f, 0.1f), new Color(0.3f, 0.6f, 0.2f) };
            for (int c = 0; c < 4; c++)
            {
                kit.Box(new Vector3(-0.9f + c * 0.6f, 0.95f, 0), new Vector3(0.55f, 0.15f, 1f), Wood * 1.2f);
                for (int k = 0; k < 6; k++)
                    kit.Ball(new Vector3(-0.9f + c * 0.6f + (k % 2 - 0.5f) * 0.22f, 1.1f, (k / 2 - 1) * 0.28f), 0.22f, fruit[c]);
            }
            kit.Cyl(new Vector3(-1.1f, 1.3f, -0.5f), new Vector3(0.06f, 2.6f, 0.06f), Wood);
            kit.Cyl(new Vector3(1.1f, 1.3f, -0.5f), new Vector3(0.06f, 2.6f, 0.06f), Wood);
            for (int s = 0; s < 6; s++)
                kit.Box(new Vector3(-1.25f + s * 0.5f, 2.55f, 0), new Vector3(0.5f, 0.05f, 1.6f), s % 2 == 0 ? new Color(0.2f, 0.5f, 0.3f) : Color.white, new Vector3(-12, 0, 0));
            kit.Frame = Matrix4x4.identity;
            Obstacle(p, 1.4f);
        }

        private static void LemonTree(Vector3 p)
        {
            kit.Box(p + Vector3.up * 0.4f, new Vector3(1f, 0.8f, 1f), Terracotta);
            kit.Cyl(p + Vector3.up * 1.4f, new Vector3(0.22f, 1.6f, 0.22f), Wood);
            kit.Ball(p + Vector3.up * 2.6f, new Vector3(2f, 1.7f, 2f), Leaf);
            for (int i = 0; i < 8; i++)
            {
                var d = Quaternion.Euler(R(-40, 40), i * 45, 0) * Vector3.forward;
                kit.Ball(p + Vector3.up * 2.6f + d * 0.95f, new Vector3(0.2f, 0.26f, 0.2f), new Color(1f, 0.9f, 0.2f));
            }
            Obstacle(p, 0.9f);
        }
    }

    /// <summary>Scrolls the canal texture.</summary>
    public class WaterScroll : MonoBehaviour
    {
        private Material mat;
        private void Start() => mat = GetComponent<MeshRenderer>().sharedMaterial;
        private void Update()
        {
            if (mat == null) return;
            var o = mat.GetTextureOffset("_BaseMap");
            o += new Vector2(0.02f, 0.012f) * Time.deltaTime;
            mat.SetTextureOffset("_BaseMap", o);
        }
    }
}
