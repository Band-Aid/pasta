using UnityEngine;

public enum BlastKind { Cone, Line }

public struct BlastSpec
{
    public BlastKind kind;
    public Vector3 origin, forward;
    public float radius, halfAngleDeg, length, halfWidth;
    public int tier;
    public PastaType pasta;
    public bool radialLaunch;
}

/// <summary>Hit detection and visible wave share the same circle, sector or line specification.</summary>
public class Shockwave : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;
    private BlastSpec spec;
    private LineRenderer outer, inner;
    private float elapsed;
    private Color color;
    private static Material lineMaterial;
    private readonly Vector3[] points = new Vector3[81];

    public static bool Contains(BlastSpec blast, Vector3 position)
    {
        Vector3 d = position - blast.origin;
        d.y = 0f;
        Vector3 forward = blast.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
        if (blast.kind == BlastKind.Cone)
            return d.sqrMagnitude <= blast.radius * blast.radius
                && (blast.halfAngleDeg >= 180f || d.sqrMagnitude < 0.0001f
                    || Vector3.Angle(forward, d) <= blast.halfAngleDeg);
        float projection = Vector3.Dot(d, forward);
        return projection >= -0.5f && projection <= blast.length
            && (d - forward * projection).sqrMagnitude <= blast.halfWidth * blast.halfWidth;
    }

    public static int CountTargets(BlastSpec blast)
    {
        int count = 0;
        foreach (var enemy in Enemy.Alive)
            if (enemy != null && Contains(blast, enemy.transform.position)) count++;
        return count;
    }

    public static (int kills, int hits) Blast(BlastSpec blast, Shockwave prefab)
    {
        int kills = 0;
        int hits = 0;
        var shot = new DominoShot(blast.tier, blast.pasta);
        foreach (var enemy in Enemy.Alive.ToArray())
        {
            if (enemy == null || !Contains(blast, enemy.transform.position)) continue;
            hits++;
            if (enemy.Launch(shot, blast.radialLaunch ? enemy.transform.position - blast.origin : blast.forward)) kills++;
        }
        var wave = prefab != null ? Instantiate(prefab) : new GameObject("Snap wave").AddComponent<Shockwave>();
        wave.Play(blast);
        return (kills, hits);
    }

    public void Play(BlastSpec blast)
    {
        spec = blast;
        transform.SetPositionAndRotation(blast.origin + Vector3.up * 0.12f, Quaternion.LookRotation(blast.forward));
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        color = blast.tier == 3 ? new Color(1f, 0.87f, 0.4f)
              : blast.tier == 2 ? new Color(0.75f, 1f, 0.72f) : new Color(1f, 0.63f, 0.34f);
        if (lineMaterial == null) lineMaterial = new Material(Resources.Load<Shader>("SnapWave"));
        outer = MakeLine("Leading crack", 0.12f);
        inner = MakeLine("Trailing crack", 0.04f);
        Draw(outer, 0.05f); Draw(inner, 0.03f);
    }

    private LineRenderer MakeLine(string name, float width)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = false;
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width * (spec.tier == 3 ? 1.6f : 1f);
        line.numCornerVertices = 2;
        line.startColor = line.endColor = color;
        return line;
    }

    private void Draw(LineRenderer line, float fraction)
    {
        if (spec.kind == BlastKind.Line)
        {
            float width = spec.halfWidth;
            float length = spec.length * fraction;
            line.positionCount = 5;
            line.SetPosition(0, new Vector3(-width, 0, 0));
            line.SetPosition(1, new Vector3(-width, 0, length));
            line.SetPosition(2, new Vector3(width, 0, length));
            line.SetPosition(3, new Vector3(width, 0, 0));
            line.SetPosition(4, new Vector3(-width, 0, 0));
        }
        else
        {
            bool circle = spec.halfAngleDeg >= 180f;
            int count = circle ? 79 : 81;
            int offset = circle ? 0 : 1;
            if (!circle) points[0] = Vector3.zero;
            for (int i = 0; i < 79; i++)
            {
                float angle = Mathf.Lerp(-spec.halfAngleDeg, spec.halfAngleDeg, i / 78f) * Mathf.Deg2Rad;
                points[i + offset] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (spec.radius * fraction);
            }
            if (!circle) points[80] = Vector3.zero;
            line.positionCount = count;
            for (int i = 0; i < count; i++) line.SetPosition(i, points[i]);
        }
    }

    private void Update()
    {
        if (outer == null) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float progress = 1f - Mathf.Pow(1f - t, 3f);
        Draw(outer, progress);
        Draw(inner, progress * 0.88f);
        outer.widthMultiplier = (1f - t) * (spec.tier == 3 ? 0.2f : 0.12f);
        inner.widthMultiplier = (1f - t) * 0.055f;
        if (t >= 1f) Destroy(gameObject);
    }
}
