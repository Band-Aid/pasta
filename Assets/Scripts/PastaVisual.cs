using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Arc-length-preserving strands, hollow ridged penne and a ruffled lasagna sheet.</summary>
public class PastaVisual : MonoBehaviour
{
    private PastaType type;
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private static Material material;
    private float bend;
    private readonly List<Vector3> vertices = new List<Vector3>(6000);
    private readonly List<Vector2> uv = new List<Vector2>(6000);
    private readonly List<Color> colors = new List<Color>(6000);
    private readonly List<int> triangles = new List<int>(30000);
    private float Length => type == PastaType.Penne ? 0.38f : 0.59f;

    public void Initialize(PastaType pastaType)
    {
        type = pastaType;
        if (material == null)
        {
            var shader = Resources.Load<Shader>("PastaDry");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "Golden semolina (runtime)" };
            material.SetColor("_BaseColor", new Color(0.96f, 0.7f, 0.26f));
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.3f);
        }
        var model = new GameObject("Continuous pasta");
        model.transform.SetParent(transform, false);
        mesh = new Mesh { name = pastaType + " continuous mesh" };
        mesh.MarkDynamic();
        model.AddComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = model.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        Bend(0f);
    }

    public Vector3 Point(float t)
    {
        float angle = bend * 1.15f;
        if (angle < 0.001f) return new Vector3(t * Length * 0.5f, 0f, 0f);
        float radius = Length / (2f * angle);
        return new Vector3(Mathf.Sin(t * angle) * radius,
            (Mathf.Cos(t * angle) - Mathf.Cos(angle)) * radius, 0f);
    }

    public void Bend(float amount)
    {
        bend = Mathf.Clamp01(amount);
        Build(mesh, -1f, 1f, 22, false);
    }

    private void Build(Mesh target, float from, float to, int steps, bool fractured)
    {
        vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear();
        if (type == PastaType.Lasagna) Sheet(from, to, Mathf.Max(steps, Mathf.CeilToInt((to - from) * 40f)));
        else
        {
            int count = type == PastaType.Penne ? 5 : 23;
            for (int strand = 0; strand < count; strand++) Tube(strand, count, from, to, steps, fractured);
        }
        target.Clear();
        target.SetVertices(vertices); target.SetUVs(0, uv); target.SetColors(colors);
        target.SetTriangles(triangles, 0);
        target.RecalculateNormals(); target.RecalculateBounds();
    }

    private Vector3 Surface(float t, Vector2 offset)
    {
        float angle = t * bend * 1.15f;
        return Point(t) + new Vector3(Mathf.Sin(angle) * offset.x, Mathf.Cos(angle) * offset.x, offset.y);
    }

    private void Vertex(Vector3 p, Vector2 tex, Color color)
    {
        vertices.Add(p); uv.Add(tex); colors.Add(color);
    }

    private void Quad(int a, int b, int c, int d)
    {
        triangles.Add(a); triangles.Add(b); triangles.Add(c);
        triangles.Add(a); triangles.Add(c); triangles.Add(d);
    }

    private void Tube(int strand, int count, float from, float to, int steps, bool fractured)
    {
        bool hollow = type == PastaType.Penne;
        int sides = hollow ? 24 : 8;
        float phi = strand * 2.399963f;
        float spread = hollow ? 0.056f : 0.034f;
        float radial = Mathf.Sqrt((strand + 0.2f) / count) * spread;
        var offset = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * radial;
        float radius = hollow ? 0.024f : 0.0035f + 0.00065f * Mathf.Sin(strand * 7.1f);
        var tint = Color.Lerp(new Color(0.8f, 0.48f, 0.15f), new Color(1f, 0.86f, 0.47f),
            0.45f + 0.35f * Mathf.Sin(strand * 12.7f));
        int start = vertices.Count;
        int walls = hollow ? 2 : 1;
        for (int wall = 0; wall < walls; wall++)
        {
            int wallStart = vertices.Count;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(from, to, (float)i / steps);
                for (int j = 0; j <= sides; j++)
                {
                    float theta = j * Mathf.PI * 2f / sides;
                    float r = wall == 1 ? radius * 0.64f : radius * (hollow && j % 2 == 0 ? 1.09f : 1f);
                    Vector2 cross = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * r + offset;
                    float shift = hollow ? Mathf.Sin(theta) * 0.22f : Mathf.Sin(strand * 8f) * 0.025f;
                    if (fractured) shift += Mathf.Sin(strand * 3f + j * 11f) * 0.009f;
                    var p = Surface(t + shift, cross);
                    p.y += Mathf.Sin(t * 5f + strand) * 0.0009f * (1f - t * t);
                    Vertex(p, new Vector2(t * 5f + strand, (float)j / sides), wall == 1 ? tint * 0.6f : tint);
                }
            }
            for (int i = 0; i < steps; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = wallStart + i * (sides + 1) + j;
                    int b = a + sides + 1;
                    if (wall == 0) Quad(a, a + 1, b + 1, b);
                    else Quad(a, b, b + 1, a + 1);
                }
        }
        // Duplicate rim vertices to give the fracture faces crisp normals and pale interiors.
        for (int end = 0; end < 2; end++)
        {
            int outer = start + end * steps * (sides + 1);
            int inner = outer + (steps + 1) * (sides + 1);
            int cap = vertices.Count;
            Vector3 center = Vector3.zero;
            for (int j = 0; j < sides; j++) center += vertices[outer + j];
            center /= sides;
            for (int j = 0; j <= sides; j++)
            {
                Vertex(vertices[outer + j], Vector2.zero, new Color(1f, 0.9f, 0.63f));
                Vertex(hollow ? vertices[inner + j] : center, Vector2.one, new Color(1f, 0.86f, 0.5f));
            }
            for (int j = 0; j < sides; j++)
            {
                int a = cap + j * 2;
                if (end == 0) Quad(a, a + 1, a + 3, a + 2);
                else Quad(a, a + 2, a + 3, a + 1);
            }
        }
    }

    private void Sheet(float from, float to, int steps)
    {
        const int across = 18;
        for (int side = 0; side < 2; side++)
        {
            int start = vertices.Count;
            for (int i = 0; i <= steps; i++)
                for (int j = 0; j <= across; j++)
                {
                    float t = Mathf.Lerp(from, to, (float)i / steps);
                    float z = ((float)j / across - 0.5f) * 0.26f;
                    float edge = Mathf.Pow(Mathf.Abs(z) / 0.13f, 8f);
                    float y = Mathf.Sin(t * 28f) * 0.007f * edge + (side == 0 ? 0.003f : -0.003f);
                    Vertex(Surface(t, new Vector2(y, z)), new Vector2(t * 2f, z * 5f),
                        Color.Lerp(new Color(0.95f, 0.71f, 0.28f), new Color(1f, 0.86f, 0.47f), edge));
                }
            for (int i = 0; i < steps; i++)
                for (int j = 0; j < across; j++)
                {
                    int a = start + i * (across + 1) + j;
                    int b = a + across + 1;
                    if (side == 0) Quad(a, a + 1, b + 1, b);
                    else Quad(a, b, b + 1, a + 1);
                }
        }
        int bottom = (steps + 1) * (across + 1);
        for (int i = 0; i < steps; i++)
        {
            int a = i * (across + 1), b = a + across + 1;
            Quad(a, b, b + bottom, a + bottom);
            Quad(a + across, a + across + bottom, b + across + bottom, b + across);
        }
        for (int j = 0; j < across; j++)
        {
            Quad(j, j + bottom, j + 1 + bottom, j + 1);
            int a = steps * (across + 1) + j;
            Quad(a, a + 1, a + 1 + bottom, a + bottom);
        }
    }

    public void Shatter(Vector3 impulse)
    {
        meshRenderer.enabled = false;
        float[] cuts = { -1f, -0.12f, 0.16f, 1f };
        for (int i = 0; i < 3; i++)
        {
            var fragmentMesh = new Mesh { name = "Fresh pasta fracture" };
            Build(fragmentMesh, cuts[i], cuts[i + 1], 8, true);
            Vector3 center = fragmentMesh.bounds.center;
            var points = fragmentMesh.vertices;
            for (int v = 0; v < points.Length; v++) points[v] -= center;
            fragmentMesh.vertices = points;
            fragmentMesh.RecalculateBounds();
            var piece = new GameObject("Pasta fragment");
            piece.transform.SetPositionAndRotation(transform.TransformPoint(center), transform.rotation);
            piece.transform.localScale = transform.lossyScale;
            piece.AddComponent<MeshFilter>().sharedMesh = fragmentMesh;
            piece.AddComponent<MeshRenderer>().sharedMaterial = material;
            var collider = piece.AddComponent<BoxCollider>();
            collider.size = fragmentMesh.bounds.size;
            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.035f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = impulse + transform.right * ((i - 1) * 1.8f) + Vector3.up * 1.2f;
            body.angularVelocity = transform.forward * ((i - 1) * 12f) + Random.insideUnitSphere * 5f;
            if (PlayerController.Instance != null)
                Physics.IgnoreCollision(collider, PlayerController.Instance.GetComponent<CharacterController>());
            piece.AddComponent<PastaFragment>();
            Destroy(piece, 2.8f);
        }
    }

    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}

public class PastaFragment : MonoBehaviour
{
    private void OnDestroy()
    {
        var filter = GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
    }
}
