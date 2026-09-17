using UnityEngine;

public enum BlastKind { Cone, Line }

/// <summary>衝撃波の形。Cone=円/扇（半径＋半角）、Line=貫通ビーム（長さ＋半幅）</summary>
public struct BlastSpec
{
    public BlastKind kind;
    public Vector3 origin;
    public Vector3 forward;
    public float radius;
    public float halfAngleDeg; // 180で全周
    public float length;
    public float halfWidth;
    public int tier;
}

/// <summary>
/// パスタ折りの衝撃波。範囲内のイタリアンを撃破し、拡がるリング/前方ビームを描く。
/// </summary>
public class Shockwave : MonoBehaviour
{
    [SerializeField] private float duration = 0.4f;

    private Transform ring, beam;
    private Renderer activeRend;
    private MaterialPropertyBlock mpb;
    private Color color;
    private float t;
    private BlastKind kind;
    private float targetDiameter, targetLength, beamWidth;
    private float ringBias; // 狙い撃ちの扇はリングを前方へ寄せて方向を示す
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>範囲内の敵にダメージ（damage=tier）。撃破数を返す。エフェクトも出す。</summary>
    public static int Blast(BlastSpec s, Shockwave prefab)
    {
        int kills = 0;
        var snapshot = Enemy.Alive.ToArray();
        foreach (var e in snapshot)
        {
            if (e == null) continue;
            Vector3 d = e.transform.position - s.origin; d.y = 0f;
            bool hit = false;

            if (s.kind == BlastKind.Cone)
            {
                if (d.magnitude <= s.radius)
                    hit = s.halfAngleDeg >= 180f || d.sqrMagnitude < 0.0001f
                        || Vector3.Angle(s.forward, d) <= s.halfAngleDeg;
            }
            else // Line（貫通）
            {
                float proj = Vector3.Dot(d, s.forward);
                if (proj >= -0.5f && proj <= s.length)
                    hit = (d - s.forward * proj).magnitude <= s.halfWidth;
            }

            if (hit && e.Hit(s.tier, s.tier, s.origin)) kills++;
        }

        if (prefab != null)
        {
            var w = Instantiate(prefab, s.origin + Vector3.up * 0.15f, Quaternion.LookRotation(s.forward));
            w.Play(s);
        }
        return kills;
    }

    public void Play(BlastSpec s)
    {
        kind = s.kind;
        color = s.tier == 3 ? new Color(1f, 0.25f, 0.1f)
              : s.tier == 2 ? new Color(1f, 0.55f, 0.12f)
              : new Color(1f, 0.85f, 0.3f);
        ring = transform.Find("Ring");
        beam = transform.Find("Beam");
        mpb = new MaterialPropertyBlock();

        if (kind == BlastKind.Cone)
        {
            if (beam != null) beam.gameObject.SetActive(false);
            if (ring != null) { ring.gameObject.SetActive(true); activeRend = ring.GetComponent<Renderer>(); }
            targetDiameter = s.radius * 2f;
            ringBias = s.halfAngleDeg < 180f ? 0.3f : 0f; // localスケールに対する比率（前方寄せ）
            transform.localScale = new Vector3(0.1f, 0.03f, 0.1f);
        }
        else
        {
            if (ring != null) ring.gameObject.SetActive(false);
            if (beam != null) { beam.gameObject.SetActive(true); activeRend = beam.GetComponent<Renderer>(); }
            targetLength = s.length;
            beamWidth = s.halfWidth * 2f;
        }
    }

    private void Update()
    {
        t += Time.deltaTime / duration;
        float e = Mathf.Clamp01(t);

        if (kind == BlastKind.Cone)
        {
            float dia = Mathf.SmoothStep(0.1f, targetDiameter, e);
            transform.localScale = new Vector3(dia, 0.03f, dia);
            if (ring != null) ring.localPosition = new Vector3(0f, 0f, ringBias); // 前方バイアス（local比率なので定数）
        }
        else if (beam != null)
        {
            float len = Mathf.SmoothStep(0.1f, targetLength, e);
            beam.localPosition = new Vector3(0f, 0f, len * 0.5f);
            beam.localScale = new Vector3(beamWidth, 0.06f, len);
        }

        if (activeRend != null)
        {
            Color c = color; c.a = (1f - e) * 0.6f;
            activeRend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            activeRend.SetPropertyBlock(mpb);
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
