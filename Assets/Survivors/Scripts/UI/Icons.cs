using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>32x32 pixel-art icons painted from simple shapes, auto-outlined, point filtered.</summary>
    public static class Icons
    {
        private const int S = 32;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static Color[] px;

        private static readonly Color Y = new Color(1f, 0.8f, 0.32f), YL = new Color(1f, 0.92f, 0.6f), YD = new Color(0.85f, 0.58f, 0.18f);
        private static readonly Color Rd = new Color(0.88f, 0.18f, 0.14f), RdD = new Color(0.6f, 0.08f, 0.08f);
        private static readonly Color Wt = new Color(0.98f, 0.97f, 0.93f), Gy = new Color(0.72f, 0.74f, 0.78f), GyD = new Color(0.45f, 0.46f, 0.5f);
        private static readonly Color Gr = new Color(0.3f, 0.72f, 0.3f), GrD = new Color(0.15f, 0.45f, 0.18f);
        private static readonly Color Br = new Color(0.55f, 0.33f, 0.18f), BrD = new Color(0.32f, 0.18f, 0.1f);
        private static readonly Color Bl = new Color(0.3f, 0.5f, 0.95f), Or = new Color(1f, 0.55f, 0.15f);

        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;
            px = new Color[S * S];
            Paint(key);
            Outline(new Color(0.1f, 0.06f, 0.05f, 1f));
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = key };
            tex.SetPixels(px);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 32f);
            cache[key] = sprite;
            return sprite;
        }

        private static void Paint(string key)
        {
            switch (key)
            {
                case "spaghetti":
                    for (int i = 0; i < 5; i++)
                    {
                        float o = i * 2.2f - 4.4f;
                        Line(5 + o, 5 - o, 13 + o, 13 - o, 1.6f, i % 2 == 0 ? Y : YL);
                        Line(18 + o, 18 - o, 27 + o, 27 - o, 1.6f, i % 2 == 0 ? Y : YL);
                    }
                    Spark(16, 16, Wt);
                    break;
                case "domino":
                    for (int i = 0; i < 3; i++) RotRect(9 + i * 7, 12 + i * 2, 5, 11, -20 - i * 15, Wt);
                    for (int i = 0; i < 3; i++) Circle(9 + i * 7, 14 + i * 2, 1, Rd);
                    for (int i = 0; i < 3; i++) Line(4 + i * 2, 26 - i * 2, 12 + i * 2, 30 - i * 2, 1.4f, Y);
                    Spark(24, 26, Or);
                    break;
                case "penne":
                    Tube(6, 8, 18, 20, 6, Y);
                    Tube(14, 6, 26, 18, 6, YL);
                    break;
                case "rigatoni":
                    Tube(4, 16, 14, 26, 6, Y);
                    Tube(11, 9, 21, 19, 6, YL);
                    Tube(18, 2, 28, 12, 6, Y);
                    Line(2, 8, 8, 2, 1f, Wt);
                    break;
                case "lasagna":
                    Lasagna(0);
                    break;
                case "quake":
                    Lasagna(3);
                    Line(4, 4, 10, 8, 1.2f, BrD); Line(10, 8, 8, 12, 1.2f, BrD);
                    Line(22, 3, 26, 9, 1.2f, BrD); Line(26, 9, 29, 8, 1.2f, BrD);
                    break;
                case "fusilli":
                    for (int i = 0; i < 12; i++)
                    {
                        float t = i / 11f;
                        Circle(8 + t * 16, 8 + t * 16 + Mathf.Sin(t * 12f) * 3f, 3f, i % 2 == 0 ? Y : YD);
                    }
                    break;
                case "tornado":
                    for (int r = 0; r < 3; r++) Ring(16, 16, 13 - r * 4, 1.2f, r % 2 == 0 ? YL : Wt);
                    for (int i = 0; i < 8; i++)
                    {
                        float t = i / 7f;
                        Circle(10 + t * 12, 10 + t * 12 + Mathf.Sin(t * 10f) * 2f, 2.3f, i % 2 == 0 ? Y : YD);
                    }
                    break;
                case "farfalle":
                    Bow(16, 16, 1f);
                    break;
                case "hurricane":
                    Bow(10, 22, 0.55f); Bow(22, 22, 0.55f); Bow(16, 10, 0.6f);
                    Ring(16, 16, 14, 1f, Wt);
                    break;
                case "ketchup":
                    Bottle(Rd, Wt);
                    break;
                case "flood":
                    Ellipse(16, 6, 14, 5, RdD);
                    Ellipse(16, 7, 11, 3.5f, Rd);
                    Bottle(Rd, Wt, 4);
                    break;
                case "pizza":
                    Pizza(16, 16);
                    break;
                case "meteor":
                    Tri(new Vector2(2, 30), new Vector2(14, 20), new Vector2(8, 14), Or);
                    Tri(new Vector2(6, 30), new Vector2(18, 24), new Vector2(12, 18), Y);
                    Pizza(19, 13, 0.75f);
                    break;
                case "carbonara":
                    Ellipse(16, 12, 14, 7, Wt);
                    Ellipse(16, 13, 11, 5, Gy);
                    for (int i = 0; i < 5; i++) Ring(12 + i * 2, 15, 3, 0.9f, Y);
                    Circle(19, 19, 4, Wt); Circle(22, 21, 3, Wt);
                    break;
                case "cream":
                    Ring(16, 16, 14, 1.4f, YL);
                    Ellipse(16, 12, 12, 6, Wt);
                    Circle(16, 18, 6, Wt); Circle(19, 23, 3, Wt);
                    Spark(8, 24, YL); Spark(25, 8, YL);
                    break;
                case "cheese":
                    Tri(new Vector2(3, 8), new Vector2(29, 8), new Vector2(29, 24), Y);
                    Rect(3, 3, 29, 8, YD);
                    Circle(20, 13, 2, YD); Circle(25, 18, 1.5f, YD); Circle(14, 10, 1.3f, YD);
                    break;
                case "espresso":
                    Rect(8, 5, 22, 16, Wt);
                    Ring(23, 11, 4, 1.4f, Wt);
                    Ellipse(15, 16, 7, 2, BrD);
                    Ellipse(15, 4, 11, 2, Gy);
                    Line(12, 20, 13, 28, 1f, Gy); Line(17, 20, 16, 28, 1f, Gy);
                    break;
                case "semolina":
                    Ellipse(16, 12, 10, 10, new Color(0.9f, 0.78f, 0.55f));
                    Rect(12, 20, 20, 25, new Color(0.9f, 0.78f, 0.55f));
                    Line(11, 25, 21, 25, 1.2f, BrD);
                    Line(16, 6, 16, 17, 1f, YD);
                    for (int i = 0; i < 3; i++) { Line(16, 8 + i * 3, 13, 10 + i * 3, 0.9f, YD); Line(16, 8 + i * 3, 19, 10 + i * 3, 0.9f, YD); }
                    break;
                case "pot":
                    Rect(6, 5, 26, 18, Gy);
                    Rect(6, 16, 26, 19, GyD);
                    Rect(2, 12, 6, 14, GyD); Rect(26, 12, 30, 14, GyD);
                    Line(11, 22, 12, 29, 1f, Wt); Line(16, 22, 16, 30, 1f, Wt); Line(21, 22, 20, 29, 1f, Wt);
                    break;
                case "oil":
                    Rect(10, 3, 22, 20, new Color(0.62f, 0.72f, 0.2f));
                    Tri(new Vector2(10, 20), new Vector2(22, 20), new Vector2(16, 25), new Color(0.62f, 0.72f, 0.2f));
                    Rect(14, 24, 18, 29, GrD);
                    Ellipse(16, 11, 4, 4, YL);
                    break;
                case "fork":
                    Line(16, 3, 16, 18, 2f, Gy);
                    Rect(10, 17, 22, 20, Gy);
                    for (int i = 0; i < 4; i++) Line(10.5f + i * 3.6f, 19, 10.5f + i * 3.6f, 29, 0.9f, Gy);
                    break;
                case "basil":
                    LeafShape(11, 13, 9, 5, 35, Gr);
                    LeafShape(21, 17, 9, 5, -30, GrD);
                    LeafShape(15, 23, 7, 4, 80, Gr);
                    Line(16, 3, 16, 14, 1f, GrD);
                    break;
                case "book":
                    Rect(6, 4, 26, 27, RdD);
                    Rect(8, 6, 26, 26, Wt);
                    Rect(6, 7, 24, 27, Rd);
                    Rect(10, 18, 20, 22, Y);
                    break;
                case "shield":
                    Rect(7, 12, 25, 27, Bl);
                    Tri(new Vector2(7, 12), new Vector2(25, 12), new Vector2(16, 3), Bl);
                    Rect(14, 6, 18, 25, Wt);
                    Rect(9, 17, 23, 20, Wt);
                    break;
                case "coin":
                    Circle(16, 16, 12, Y);
                    Circle(16, 16, 9, YD);
                    Ring(17, 16, 5, 1.3f, YL);
                    Rect(9, 14, 17, 15.5f, YL); Rect(9, 17, 17, 18.5f, YL);
                    break;
                case "heart":
                    Circle(11, 19, 7, Rd); Circle(21, 19, 7, Rd);
                    Tri(new Vector2(4.5f, 17), new Vector2(27.5f, 17), new Vector2(16, 4), Rd);
                    Circle(10, 21, 2, Wt);
                    break;
                case "gold":
                    Circle(16, 16, 12, Y); Spark(16, 16, Wt);
                    break;
                case "pizzaslice":
                    Tri(new Vector2(4, 26), new Vector2(28, 26), new Vector2(16, 3), Y);
                    Rect(4, 25, 28, 29, YD);
                    Circle(13, 19, 2.3f, Rd); Circle(19, 20, 2.3f, Rd); Circle(16, 12, 2f, Rd);
                    break;
                default:
                    Circle(16, 16, 10, Gy);
                    break;
            }
        }

        // ---------------- composite shapes ----------------

        private static void Tube(float x0, float y0, float x1, float y1, float w, Color c)
        {
            Line(x0, y0, x1, y1, w * 0.5f, c);
            var d = new Vector2(x1 - x0, y1 - y0).normalized;
            for (int i = 1; i < 4; i++)
            {
                var m = Vector2.Lerp(new Vector2(x0, y0), new Vector2(x1, y1), i / 4f);
                var n = new Vector2(-d.y, d.x) * w * 0.45f;
                Line(m.x - n.x, m.y - n.y, m.x + n.x, m.y + n.y, 0.5f, YD);
            }
            Circle(x1, y1, w * 0.42f, YD);
            Circle(x1, y1, w * 0.22f, BrD);
        }

        private static void Lasagna(int drop)
        {
            Rect(4, 10 - drop, 28, 22 - drop, Y);
            Rect(4, 14 - drop, 28, 16 - drop, Rd);
            for (int x = 4; x <= 28; x += 3) { Circle(x, 22 - drop, 1.6f, Y); Circle(x, 10 - drop, 1.6f, Y); }
        }

        private static void Bow(float cx, float cy, float s)
        {
            Tri(new Vector2(cx, cy), new Vector2(cx - 13 * s, cy + 9 * s), new Vector2(cx - 13 * s, cy - 9 * s), Y);
            Tri(new Vector2(cx, cy), new Vector2(cx + 13 * s, cy + 9 * s), new Vector2(cx + 13 * s, cy - 9 * s), Y);
            Line(cx - 10 * s, cy + 5 * s, cx - 10 * s, cy - 5 * s, 0.6f, YD);
            Line(cx + 10 * s, cy + 5 * s, cx + 10 * s, cy - 5 * s, 0.6f, YD);
            Circle(cx, cy, 3.2f * s, YD);
        }

        private static void Bottle(Color body, Color cap, int yOff = 0)
        {
            Rect(10, 4 + yOff, 22, 21 + yOff, body);
            Circle(16, 21 + yOff, 6, body);
            Rect(13, 24 + yOff, 19, 29 + yOff, cap);
            Rect(11, 9 + yOff, 21, 15 + yOff, cap);
            Rect(13, 11 + yOff, 19, 13 + yOff, body);
        }

        private static void Pizza(float cx, float cy, float s = 1f)
        {
            Circle(cx, cy, 13 * s, YD);
            Circle(cx, cy, 11 * s, Rd);
            Circle(cx - 4 * s, cy + 3 * s, 3 * s, YL); Circle(cx + 4 * s, cy - 4 * s, 3 * s, YL); Circle(cx + 3 * s, cy + 5 * s, 2.5f * s, YL);
            Rect(cx - 7 * s, cy - 5 * s, cx - 4 * s, cy - 2 * s, Y); Rect(cx + 5 * s, cy + 1 * s, cx + 8 * s, cy + 4 * s, Y); Rect(cx - 1 * s, cy - 1 * s, cx + 2 * s, cy + 2 * s, Y);
        }

        private static void LeafShape(float cx, float cy, float len, float wid, float angle, Color c)
        {
            float a = angle * Mathf.Deg2Rad;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float u = dx * Mathf.Cos(a) + dy * Mathf.Sin(a), v = -dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
                    float w = wid * (1f - (u / len) * (u / len));
                    if (Mathf.Abs(u) <= len && Mathf.Abs(v) <= w) Set(x, y, c);
                }
        }

        private static void Spark(float cx, float cy, Color c)
        {
            Line(cx - 4, cy, cx + 4, cy, 0.8f, c);
            Line(cx, cy - 4, cx, cy + 4, 0.8f, c);
            Line(cx - 2.5f, cy - 2.5f, cx + 2.5f, cy + 2.5f, 0.6f, c);
            Line(cx - 2.5f, cy + 2.5f, cx + 2.5f, cy - 2.5f, 0.6f, c);
        }

        // ---------------- primitives ----------------

        private static void Set(int x, int y, Color c)
        {
            if (x < 1 || y < 1 || x >= S - 1 || y >= S - 1) return;
            px[y * S + x] = c;
        }

        private static void Circle(float cx, float cy, float r, Color c) => Ellipse(cx, cy, r, r, c);

        private static void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        private static void Ring(float cx, float cy, float r, float w, Color c)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (Mathf.Abs(d - r) <= w) Set(x, y, c);
                }
        }

        private static void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.FloorToInt(y0); y < Mathf.CeilToInt(y1); y++)
                for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++) Set(x, y, c);
        }

        private static void RotRect(float cx, float cy, float w, float h, float angle, Color c)
        {
            float a = angle * Mathf.Deg2Rad;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float u = dx * Mathf.Cos(a) + dy * Mathf.Sin(a), v = -dx * Mathf.Sin(a) + dy * Mathf.Cos(a);
                    if (Mathf.Abs(u) <= w * 0.5f && Mathf.Abs(v) <= h * 0.5f) Set(x, y, c);
                }
        }

        private static void Line(float x0, float y0, float x1, float y1, float w, Color c)
        {
            var a = new Vector2(x0, y0); var b = new Vector2(x1, y1);
            var ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    if ((a + ab * t - p).magnitude <= w) Set(x, y, c);
                }
        }

        private static void Tri(Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Set(x, y, col);
                }
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        private static void Outline(Color k)
        {
            var copy = (Color[])px.Clone();
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (copy[y * S + x].a > 0.5f) continue;
                    bool edge = false;
                    for (int oy = -1; oy <= 1 && !edge; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                            if (copy[ny * S + nx].a > 0.5f) { edge = true; break; }
                        }
                    if (edge) px[y * S + x] = k;
                }
        }
    }
}
