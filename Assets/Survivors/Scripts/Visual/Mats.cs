using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>Shared materials and procedurally generated textures.</summary>
    public static class Mats
    {
        public static Material Lit, Flash, Glow, FxAlpha, FxAdd, Telegraph, Ground, Water;
        public static Texture2D SoftDot, Cobble, Herringbone, Basalt, Ripple;
        private static Shader litShader, fxShader;

        public static void Init()
        {
            if (Lit != null) return;
            litShader = Resources.Load<Shader>("PS_VertexLit");
            fxShader = Resources.Load<Shader>("PS_UnlitFx");
            Lit = new Material(litShader) { name = "PS Lit", enableInstancing = true };
            Flash = new Material(Lit) { name = "PS Flash" };
            Flash.SetFloat("_Flash", 0.55f);
            Glow = new Material(Lit) { name = "PS Glow" };
            Glow.SetFloat("_Emission", 0.7f);
            Glow.SetFloat("_Rim", 0.6f);
            SoftDot = MakeSoftDot();
            FxAlpha = MakeFx("PS Fx Alpha", BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, CompareFunction.LessEqual, null);
            FxAdd = MakeFx("PS Fx Add", BlendMode.SrcAlpha, BlendMode.One, CompareFunction.LessEqual, null);
            Telegraph = MakeFx("PS Telegraph", BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, CompareFunction.LessEqual, null);
            Telegraph.renderQueue = 2990;
            Cobble = MakeCobble(new Color(0.62f, 0.58f, 0.53f), new Color(0.34f, 0.31f, 0.28f), 8, 11);
            Herringbone = MakeHerringbone();
            Basalt = MakeCobble(new Color(0.46f, 0.44f, 0.45f), new Color(0.24f, 0.22f, 0.23f), 5, 23);
            Ripple = MakeRipple();
        }

        public static Material Particles(bool additive)
        {
            var m = MakeFx(additive ? "PS Particles Add" : "PS Particles", BlendMode.SrcAlpha,
                additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha, CompareFunction.LessEqual, SoftDot);
            return m;
        }

        public static Material Textured(Texture2D tex, float tiling, string name)
        {
            var m = new Material(litShader) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.SetFloat("_Rim", 0f);
            return m;
        }

        public static Material WaterMaterial()
        {
            var m = MakeFx("PS Water", BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, CompareFunction.LessEqual, Ripple);
            m.renderQueue = 2900;
            return m;
        }

        private static Material MakeFx(string name, BlendMode src, BlendMode dst, CompareFunction ztest, Texture tex)
        {
            var m = new Material(fxShader) { name = name };
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_ZTest", (float)ztest);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            return m;
        }

        private static Texture2D MakeSoftDot()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "Soft dot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 144665;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Tileable voronoi cobblestones ("sampietrini").</summary>
        private static Texture2D MakeCobble(Color stone, Color grout, int cells, int seed)
        {
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Cobble", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float fx = (float)x / s * cells, fy = (float)y / s * cells;
                    int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
                    float d1 = 9f, d2 = 9f; float tone = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = cx + ox, gy = cy + oy;
                            int wx = ((gx % cells) + cells) % cells, wy = ((gy % cells) + cells) % cells;
                            float px2 = gx + 0.2f + 0.6f * Hash(wx, wy, seed), py2 = gy + 0.2f + 0.6f * Hash(wy, wx, seed + 7);
                            float d = (fx - px2) * (fx - px2) + (fy - py2) * (fy - py2);
                            if (d < d1) { d2 = d1; d1 = d; tone = Hash(wx, wy, seed + 3); }
                            else if (d < d2) d2 = d;
                        }
                    float edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
                    float stoneMask = Smooth(0.02f, 0.09f, edge);
                    float noise = Hash(x, y, seed + 11) * 0.08f;
                    var c = Color.Lerp(grout, stone * (0.85f + tone * 0.3f), stoneMask);
                    c *= 0.95f + noise;
                    c.a = 1f;
                    px[y * s + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D MakeHerringbone()
        {
            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Herringbone", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color[s * s];
            var light = new Color(0.86f, 0.82f, 0.74f);
            var dark = new Color(0.62f, 0.58f, 0.54f);
            const int unit = 16;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // Diagonal herringbone: bricks 2 units long, 1 unit wide, alternating orientation.
                    int u = (x + y) / unit, v = (x - y + s * 4) / unit;
                    bool horizontal = ((u + v) & 1) == 0;
                    int bx = horizontal ? (x + y) : (x - y + s * 4);
                    float along = (bx % (unit * 2)) / (float)(unit * 2);
                    float across = ((horizontal ? (x - y + s * 4) : (x + y)) % unit) / (float)unit;
                    float edge = Mathf.Min(Mathf.Min(along, 1f - along) * 2f, Mathf.Min(across, 1f - across)) * unit;
                    float mask = Smooth(0.4f, 1.6f, edge);
                    float tone = Hash(u, v, 5) * 0.12f;
                    var c = Color.Lerp(dark, (horizontal ? light : light * 0.94f) * (0.92f + tone), mask);
                    c.a = 1f;
                    px[y * s + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        private static Texture2D MakeRipple()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = "Ripple", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float a = Mathf.Sin((x + Mathf.Sin(y * 0.098f) * 6f) * 0.098f) * Mathf.Sin((y + Mathf.Sin(x * 0.049f) * 9f) * 0.147f);
                    float hl = Smooth(0.55f, 0.9f, a);
                    px[y * s + x] = Color.Lerp(new Color(0.16f, 0.42f, 0.46f, 0.92f), new Color(0.7f, 0.9f, 0.9f, 0.95f), hl * 0.6f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
