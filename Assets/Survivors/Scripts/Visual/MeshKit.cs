using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>
    /// Combines low-poly primitives into one vertex-coloured mesh. Every primitive template is unit sized and
    /// centred on the origin, so sizes passed in are full extents (diameter, height), matching how you'd sketch a model.
    /// </summary>
    public class MeshKit
    {
        private class Template
        {
            public Vector3[] v, n;
            public Vector2[] uv;
            public int[] t;
        }

        private readonly List<Vector3> verts = new List<Vector3>(2048);
        private readonly List<Vector3> norms = new List<Vector3>(2048);
        private readonly List<Color> cols = new List<Color>(2048);
        private readonly List<Vector2> uvs = new List<Vector2>(2048);
        private readonly List<int> tris = new List<int>(4096);

        /// <summary>Applied before every primitive's own transform; lets callers build sub-assemblies in local space.</summary>
        public Matrix4x4 Frame = Matrix4x4.identity;

        private static Template box, sphere, lowSphere, quad;
        private static readonly Dictionary<int, Template> frustums = new Dictionary<int, Template>();

        public int VertexCount => verts.Count;

        public MeshKit Box(Vector3 pos, Vector3 size, Color col, Vector3 euler = default)
            => Add(box ??= MakeBox(), Matrix4x4.TRS(pos, Quaternion.Euler(euler), size), col);

        public MeshKit Ball(Vector3 pos, Vector3 size, Color col, Vector3 euler = default, bool detailed = false)
            => Add(detailed ? (sphere ??= MakeSphere(8, 14)) : (lowSphere ??= MakeSphere(6, 10)),
                Matrix4x4.TRS(pos, Quaternion.Euler(euler), size), col);

        public MeshKit Ball(Vector3 pos, float diameter, Color col) => Ball(pos, Vector3.one * diameter, col);

        /// <summary>Cylinder: size.x/z = diameters, size.y = height.</summary>
        public MeshKit Cyl(Vector3 pos, Vector3 size, Color col, Vector3 euler = default, int sides = 10)
            => Add(Frustum(1f, sides), Matrix4x4.TRS(pos, Quaternion.Euler(euler), size), col);

        /// <summary>Frustum whose top radius is <paramref name="topRatio"/> times the bottom radius (0 = cone).</summary>
        public MeshKit Cone(Vector3 pos, Vector3 size, Color col, Vector3 euler = default, float topRatio = 0f, int sides = 10)
            => Add(Frustum(topRatio, sides), Matrix4x4.TRS(pos, Quaternion.Euler(euler), size), col);

        public MeshKit Quad(Vector3 pos, Vector2 size, Color col, Vector3 euler = default)
            => Add(quad ??= MakeQuad(), Matrix4x4.TRS(pos, Quaternion.Euler(euler), new Vector3(size.x, 1f, size.y)), col);

        /// <summary>A limb or bar between two points.</summary>
        public MeshKit Bar(Vector3 a, Vector3 b, float thickness, Color col, bool round = true)
        {
            Vector3 d = b - a;
            var rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
            var m = Matrix4x4.TRS((a + b) * 0.5f, rot, new Vector3(thickness, d.magnitude, thickness));
            return Add(round ? Frustum(1f, 8) : (box ??= MakeBox()), m, col);
        }

        public MeshKit Add(MeshKit other, Matrix4x4 m)
        {
            var full = Frame * m;
            var nm = full.inverse.transpose;
            int b = verts.Count;
            for (int i = 0; i < other.verts.Count; i++)
            {
                verts.Add(full.MultiplyPoint3x4(other.verts[i]));
                norms.Add(nm.MultiplyVector(other.norms[i]).normalized);
                cols.Add(other.cols[i]);
                uvs.Add(other.uvs[i]);
            }
            bool flip = full.determinant < 0f;
            for (int i = 0; i < other.tris.Count; i += 3)
            {
                tris.Add(b + other.tris[i]);
                tris.Add(b + (flip ? other.tris[i + 2] : other.tris[i + 1]));
                tris.Add(b + (flip ? other.tris[i + 1] : other.tris[i + 2]));
            }
            return this;
        }

        private MeshKit Add(Template tp, Matrix4x4 m, Color col)
        {
            var lin = Lin(col);
            var full = Frame * m;
            var nm = full.inverse.transpose;
            int b = verts.Count;
            for (int i = 0; i < tp.v.Length; i++)
            {
                verts.Add(full.MultiplyPoint3x4(tp.v[i]));
                norms.Add(nm.MultiplyVector(tp.n[i]).normalized);
                cols.Add(lin);
                uvs.Add(tp.uv[i]);
            }
            bool flip = full.determinant < 0f;
            for (int i = 0; i < tp.t.Length; i += 3)
            {
                tris.Add(b + tp.t[i]);
                tris.Add(b + (flip ? tp.t[i + 2] : tp.t[i + 1]));
                tris.Add(b + (flip ? tp.t[i + 1] : tp.t[i + 2]));
            }
            return this;
        }

        /// <summary>Adds raw geometry (used for rings, sectors and terrain patches).</summary>
        public void Raw(IList<Vector3> v, IList<Vector3> n, IList<Vector2> uv, IList<int> t, Color col)
        {
            int b = verts.Count;
            for (int i = 0; i < v.Count; i++)
            {
                verts.Add(Frame.MultiplyPoint3x4(v[i]));
                norms.Add(Frame.MultiplyVector(n[i]).normalized);
                cols.Add(Lin(col));
                uvs.Add(uv[i]);
            }
            for (int i = 0; i < t.Count; i++) tris.Add(b + t[i]);
        }

        /// <summary>Vertex colours are not gamma-corrected by Unity, so author in sRGB and convert here.</summary>
        public static Color Lin(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        public void Clear()
        {
            verts.Clear(); norms.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
            Frame = Matrix4x4.identity;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return mesh;
        }

        // ---------- templates ----------

        private static Template MakeBox()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                Vector3 u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(d, u);
                int b = v.Count;
                v.Add((d - u - w) * 0.5f); v.Add((d + u - w) * 0.5f); v.Add((d + u + w) * 0.5f); v.Add((d - u + w) * 0.5f);
                for (int i = 0; i < 4; i++) n.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
            return Fix(v, n, uv, t);
        }

        private static Template MakeQuad()
        {
            return new Template
            {
                v = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) },
                n = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                t = new[] { 0, 1, 2, 0, 2, 3 }
            };
        }

        private static Template MakeSphere(int lat, int lon)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = Mathf.PI * 2f * j / lon;
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(p * 0.5f); n.Add(p); uv.Add(new Vector2((float)j / lon, 1f - (float)i / lat));
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j, b = a + lon + 1;
                    t.Add(a); t.Add(a + 1); t.Add(b);
                    t.Add(a + 1); t.Add(b + 1); t.Add(b);
                }
            return Fix(v, n, uv, t);
        }

        private static Template Frustum(float topRatio, int sides)
        {
            int key = Mathf.RoundToInt(topRatio * 100f) * 100 + sides;
            if (frustums.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float rb = 0.5f, rt = 0.5f * topRatio;
            float slope = (rb - rt); // over height 1
            for (int j = 0; j <= sides; j++)
            {
                float a = Mathf.PI * 2f * j / sides;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var normal = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * rb + Vector3.down * 0.5f); n.Add(normal); uv.Add(new Vector2((float)j / sides, 0));
                v.Add(dir * rt + Vector3.up * 0.5f); n.Add(normal); uv.Add(new Vector2((float)j / sides, 1));
            }
            for (int j = 0; j < sides; j++)
            {
                int a = j * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 3);
                t.Add(a); t.Add(a + 3); t.Add(a + 2);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                float r = cap == 0 ? rb : rt;
                if (r < 0.001f) continue;
                float y = cap == 0 ? -0.5f : 0.5f;
                var normal = cap == 0 ? Vector3.down : Vector3.up;
                int c = v.Count;
                v.Add(new Vector3(0, y, 0)); n.Add(normal); uv.Add(new Vector2(0.5f, 0.5f));
                for (int j = 0; j <= sides; j++)
                {
                    float a = Mathf.PI * 2f * j / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(normal);
                    uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int j = 0; j < sides; j++)
                {
                    if (cap == 0) { t.Add(c); t.Add(c + 1 + j); t.Add(c + 2 + j); }
                    else { t.Add(c); t.Add(c + 2 + j); t.Add(c + 1 + j); }
                }
            }
            var tp = Fix(v, n, uv, t);
            frustums[key] = tp;
            return tp;
        }

        private static Template Fix(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            // Ensure outward winding for Unity's clockwise front faces.
            var tp = new Template { v = v.ToArray(), n = n.ToArray(), uv = uv.ToArray(), t = t.ToArray() };
            for (int i = 0; i < tp.t.Length; i += 3)
            {
                Vector3 a = tp.v[tp.t[i]], b = tp.v[tp.t[i + 1]], c = tp.v[tp.t[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                Vector3 avgN = tp.n[tp.t[i]] + tp.n[tp.t[i + 1]] + tp.n[tp.t[i + 2]];
                if (face.sqrMagnitude > 1e-12f && Vector3.Dot(face, avgN) < 0f)
                {
                    (tp.t[i + 1], tp.t[i + 2]) = (tp.t[i + 2], tp.t[i + 1]);
                }
            }
            return tp;
        }

        // ---------- flat shapes for effects ----------

        /// <summary>Flat annulus/sector on XZ, facing up. Angle in degrees centred on +Z.</summary>
        public static Mesh Sector(float inner, float outer, float angleDeg, int segments, string name)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); var c = new List<Color>();
            var n = new List<Vector3>();
            float half = angleDeg * 0.5f * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-half, half, (float)i / segments);
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Add(d * inner); v.Add(d * outer);
                uv.Add(new Vector2((float)i / segments, 0f)); uv.Add(new Vector2((float)i / segments, 1f));
                c.Add(new Color(1, 1, 1, inner > 0.001f ? 0.25f : 0.35f)); c.Add(Color.white);
                n.Add(Vector3.up); n.Add(Vector3.up);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                t.Add(a); t.Add(a + 1); t.Add(a + 3);
                t.Add(a); t.Add(a + 3); t.Add(a + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(c); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Rectangle on XZ from z=0 to z=1, width 1 — for line telegraphs.</summary>
        public static Mesh Lane(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, 0), new Vector3(-0.5f, 0, 1), new Vector3(0.5f, 0, 1), new Vector3(0.5f, 0, 0) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new List<Color> { new Color(1, 1, 1, 0.5f), Color.white, Color.white, new Color(1, 1, 1, 0.5f) });
            mesh.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
