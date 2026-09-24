using UnityEngine;

/// <summary>One shared score ledger for an entire shot, including delayed collisions.</summary>
public sealed class DominoShot
{
    public int Hits { get; private set; }
    public int Collisions { get; private set; }
    public int Tier { get; }
    public PastaType Type { get; }
    public float Speed { get; }
    public float Travel { get; }
    public float KnockbackMultiplier { get; }
    public float Width { get; }

    public DominoShot(int tier, PastaType type)
    {
        Tier = tier;
        Type = type;
        Speed = type == PastaType.Penne ? 24f : 18f;
        KnockbackMultiplier = KnockbackFor(type);
        Width = type == PastaType.Lasagna ? 1.5f : 1.05f;
        switch (type)
        {
            case PastaType.Penne:
                Travel = tier == 3 ? 18f : tier == 2 ? 13f : 8f;
                break;
            case PastaType.Lasagna:
                Travel = tier == 3 ? 13f : tier == 2 ? 9f : 6f;
                break;
            default:
                Travel = tier == 3 ? 15f : tier == 2 ? 11f : 7f;
                break;
        }
    }

    public static float KnockbackFor(PastaType type) =>
        type == PastaType.Penne ? 0.9f : type == PastaType.Lasagna ? 1.3f : 1f;

    public void Register(bool collision)
    {
        Hits++;
        if (collision) Collisions++;
        // The initial hits are scored together by PastaHand after the blast resolves.
        if (collision) ScoreManager.Instance?.OnDomino(this);
    }

    public static bool SweptHit(Vector3 from, Vector3 to, Vector3 target, float radius)
    {
        from.y = to.y = target.y = 0f;
        Vector3 segment = to - from;
        float t = segment.sqrMagnitude > 0.0001f
            ? Mathf.Clamp01(Vector3.Dot(target - from, segment) / segment.sqrMagnitude) : 0f;
        return (target - (from + segment * t)).sqrMagnitude <= radius * radius;
    }
}
