using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Normal, Fast, Tough }

/// <summary>
/// 接近・攻撃・挑発への移動。発射された敵は次の敵へ連鎖する弾になる。
/// </summary>
public class Enemy : MonoBehaviour
{
    /// <summary>生存中の敵。衝撃波の範囲判定はこのリストを走査する</summary>
    public static readonly List<Enemy> Alive = new List<Enemy>();

    [Header("移動/攻撃（Configureで上書き）")]
    public float moveSpeed = 1.8f;
    public float attackRange = 1.7f;
    public float attackDamage = 8f;
    public float attackInterval = 1.0f;

    [Header("参照（ビルダーが設定）")]
    public Transform leftEye;
    public Transform rightEye;
    public Transform head;
    public Font shoutFont;

    public EnemyType Type { get; private set; }

    private int hp = 1;
    private Transform player;
    private Rigidbody rb;
    private CapsuleCollider col;
    private AudioSource audioSource;
    private static AudioClip screamClip;
    private Renderer bodyRend;
    private bool dead;
    private float nextAttack;
    private float bobSeed;
    private float baseY;
    private float staggerUntil;
    private float windupUntil;
    private Vector3 headHome;
    private TextMesh warning;
    public bool IsWindingUp => !dead && windupUntil > 0f;
    public float AttackProgress => IsWindingUp ? 1f - Mathf.Clamp01((windupUntil - Time.time) / 0.6f) : 0f;
    private Vector3 staggerVel;
    private DominoShot shot;
    private Vector3 flightDirection;
    private Vector3 flightPosition, fullScale = Vector3.one;
    private float flightRemaining, flightElapsed;
    private float lureUntil;
    private Vector3 lurePoint;
    private static Material trailMaterial;
    public bool IsFlying => dead && flightRemaining > 0f;
    public bool IsLured => !dead && Time.time < lureUntil;
    private Vector3 eyeScale = Vector3.one;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.maxAngularVelocity = 40f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        col = GetComponent<CapsuleCollider>();
        if (!TryGetComponent(out audioSource)) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.6f;
        audioSource.maxDistance = 40f;
        if (screamClip == null) screamClip = BuildScream();
        bobSeed = Random.value * 10f;
        if (leftEye != null) eyeScale = leftEye.localScale;
        if (head != null)
        {
            var face = new GameObject("Face rig").transform;
            face.SetParent(transform, false);
            face.localPosition = head.localPosition;
            string[] parts = { "Head", "Hair", "Nose", "MoustL", "MoustR", "BrowL", "BrowR", "EyeL", "EyeR" };
            foreach (var name in parts)
            {
                var part = transform.Find(name);
                if (part != null) part.SetParent(face, true);
            }
            head = face;
        }
        var body = transform.Find("Body");
        if (body != null) bodyRend = body.GetComponent<Renderer>();
    }

    private void OnEnable() { if (!dead) Alive.Add(this); }
    private void OnDisable() { Alive.Remove(this); }

    private void Start()
    {
        baseY = transform.position.y;
        if (head != null) headHome = head.localPosition;
        var marker = new GameObject("Attack warning");
        marker.transform.SetParent(transform, false);
        marker.transform.localPosition = Vector3.up * 2.35f;
        warning = marker.AddComponent<TextMesh>();
        warning.text = "!";
        warning.font = shoutFont;
        warning.fontSize = 64;
        warning.characterSize = 0.045f;
        warning.anchor = TextAnchor.MiddleCenter;
        warning.color = new Color(1f, 0.32f, 0.1f);
        if (shoutFont != null) marker.GetComponent<MeshRenderer>().sharedMaterial = shoutFont.material;
        warning.gameObject.SetActive(false);
        StartCoroutine(SpawnPop());
    }

    /// <summary>スポナーが敵種と基準速度を渡す</summary>
    public void Configure(EnemyType type, float baseSpeed)
    {
        Type = type;
        switch (type)
        {
            case EnemyType.Fast:
                hp = 1; moveSpeed = baseSpeed * 1.7f; attackDamage = 6f; attackInterval = 0.8f;
                transform.localScale = Vector3.one * 0.9f;
                Tint(new Color(0.85f, 0.32f, 0.26f)); // 赤シャツの俊足
                break;
            case EnemyType.Tough:
                hp = 3; moveSpeed = baseSpeed * 0.62f; attackDamage = 15f; attackInterval = 1.2f;
                transform.localScale = Vector3.one * 1.3f;
                Tint(new Color(0.42f, 0.45f, 0.40f)); // でかいエプロン親父
                break;
            default:
                hp = 1; moveSpeed = baseSpeed; attackDamage = 8f; attackInterval = 1.0f;
                transform.localScale = Vector3.one;
                break;
        }
        fullScale = transform.localScale;
    }

    private void Tint(Color c)
    {
        if (bodyRend == null) return;
        var mpb = new MaterialPropertyBlock();
        bodyRend.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        bodyRend.SetPropertyBlock(mpb);
    }

    private void Update()
    {
        if (dead) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        if (player == null)
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            player = pc.transform;
        }

        // よろけ中は後退して足踏み
        if (Time.time < staggerUntil)
        {
            Vector3 sp = transform.position + staggerVel * Time.deltaTime;
            sp.y = baseY;
            transform.position = sp;
            staggerVel *= Mathf.Exp(-6f * Time.deltaTime);
            return;
        }

        Vector3 to = (IsLured ? lurePoint : player.position) - transform.position; to.y = 0f;
        float dist = to.magnitude;
        if (windupUntil > 0f)
        {
            if (Time.time >= windupUntil)
            {
                windupUntil = 0f;
                warning.gameObject.SetActive(false);
                if (dist <= attackRange + 0.35f) PlayerController.Instance?.TakeDamage(attackDamage);
                nextAttack = Time.time + attackInterval;
                if (head != null) head.localPosition = headHome;
            }
            else
            {
                if (head != null) head.localPosition = headHome + Vector3.back * AttackProgress * 0.18f;
                warning.transform.localScale = Vector3.one * (1f + AttackProgress * 0.7f);
                if (Camera.main != null) warning.transform.rotation = Camera.main.transform.rotation;
            }
            return;
        }

        if (to.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);

        if (IsLured || dist > attackRange)
        {
            Vector3 move = to.normalized * (IsLured ? Mathf.Min(dist * 3f, moveSpeed * 2.5f) : moveSpeed)
                + Separation() * (IsLured ? 0.35f : 2.2f);
            Vector3 p = transform.position + move * Time.deltaTime;
            p.y = baseY + Mathf.Abs(Mathf.Sin(Time.time * (Type == EnemyType.Fast ? 12f : 8f) + bobSeed)) * 0.09f;
            transform.position = p;
        }
        else if (Time.time >= nextAttack)
        {
            windupUntil = Time.time + 0.6f;
            warning.gameObject.SetActive(true);
        }
    }

    private Vector3 Separation()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < Alive.Count; i++)
        {
            var o = Alive[i];
            if (o == null || o == this) continue;
            Vector3 d = transform.position - o.transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m > 0.001f && m < 1.3f) sum += d / m * (1.3f - m);
        }
        return sum;
    }

    private IEnumerator SpawnPop()
    {
        Vector3 full = transform.localScale;
        transform.localScale = full * 0.05f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            transform.localScale = full * Mathf.SmoothStep(0.05f, 1f, Mathf.Clamp01(t));
            yield return null;
        }
        transform.localScale = full;
    }

    /// <summary>衝撃波の被弾。damage分HPを減らし、0以下で撃破。撃破したらtrueを返す。</summary>
    public bool Hit(int damage, int tier, Vector3 blastOrigin)
    {
        if (dead) return false;
        hp -= Mathf.Max(1, damage);
        if (hp <= 0)
        {
            Kill(tier, blastOrigin);
            return true;
        }
        Stagger(blastOrigin);
        return false;
    }

    public void Lure(Vector3 point, float duration)
    {
        if (dead) return;
        lurePoint = point;
        lureUntil = Time.time + duration;
        windupUntil = 0f;
        if (warning != null) warning.gameObject.SetActive(false);
        if (head != null) head.localPosition = headHome;
    }

    public bool Launch(DominoShot domino, Vector3 direction, bool collision = false)
    {
        if (dead) return false;
        // Heavy enemies resist weak direct shots, but any flying enemy breaks their guard.
        if (!collision && Type == EnemyType.Tough && domino.Tier < 3)
        {
            hp -= domino.Tier;
            if (hp > 0) { Stagger(transform.position - direction); return false; }
        }
        dead = true;
        shot = domino;
        Alive.Remove(this);
        StopAllCoroutines();
        transform.localScale = fullScale;
        windupUntil = 0f;
        if (warning != null) warning.gameObject.SetActive(false);
        if (head != null) head.localPosition = headHome;
        rb.isKinematic = true;
        if (col != null) col.enabled = false;
        direction.y = 0f;
        flightDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        flightRemaining = shot.Travel;
        flightElapsed = 0f;
        flightPosition = transform.position;
        flightPosition.y = baseY;
        PopEyes(2.1f);
        Scream(shot.Tier);
        if (!collision) Shout(shot.Tier);
        if (trailMaterial == null) trailMaterial = new Material(Resources.Load<Shader>("SnapWave"));
        var trail = new GameObject("Domino trail").AddComponent<TrailRenderer>();
        trail.transform.SetParent(transform, false);
        trail.transform.localPosition = Vector3.up;
        trail.sharedMaterial = trailMaterial;
        trail.time = 0.22f;
        trail.startWidth = 0.35f;
        trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.8f, 0.2f, 0.9f);
        trail.endColor = new Color(1f, 0.4f, 0.1f, 0f);
        shot.Register(collision);
        EnemySpawner.Instance?.OnEnemyDefeated();
        return true;
    }

    private void FixedUpdate()
    {
        if (!IsFlying || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
        Vector3 from = flightPosition;
        float step = Mathf.Min(flightRemaining, shot.Speed * Time.fixedDeltaTime);
        Vector3 to = from + flightDirection * step;
        // Stop at scenery; enemy colliders are intentionally handled by the swept test below.
        foreach (var hit in Physics.RaycastAll(from + Vector3.up * 0.8f, flightDirection, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Enemy>() != null
                || hit.collider.GetComponentInParent<PlayerController>() != null
                || hit.collider.GetComponentInParent<PastaFragment>() != null) continue;
            step = Mathf.Min(step, Mathf.Max(0f, hit.distance - 0.1f));
            to = from + flightDirection * step;
            flightRemaining = step;
        }
        foreach (var target in Alive.ToArray())
        {
            if (target == null || !DominoShot.SweptHit(from, to, target.transform.position, shot.Width)) continue;
            Vector3 direction = flightDirection * 0.8f + (target.transform.position - from).normalized * 0.2f;
            if (target.Launch(shot, direction, true))
            {
                PlayerController.Instance?.OnDominoImpact();
                var impact = new GameObject("Domino impact").AddComponent<Shockwave>();
                impact.Play(new BlastSpec { kind = BlastKind.Cone, origin = target.transform.position,
                    forward = flightDirection, radius = 1.8f, halfAngleDeg = 180f, tier = 3 });
            }
        }
        flightRemaining -= step;
        flightElapsed += Time.fixedDeltaTime;
        flightPosition = to;
        transform.Rotate(Vector3.right, 580f * Time.fixedDeltaTime, Space.Self);
        // Rotate around the body's centre while hit detection follows a stable ground path.
        float center = fullScale.y * 1.05f;
        float hop = Mathf.Sin(Mathf.Clamp01(flightElapsed / 0.8f) * Mathf.PI) * 0.7f;
        transform.position = to + Vector3.up * (center + hop) - transform.up * center;
        if (flightRemaining <= 0.001f)
        {
            flightRemaining = 0f;
            rb.isKinematic = false;
            if (col != null) col.enabled = true;
            rb.linearVelocity = flightDirection * 2f + Vector3.up * 3f;
            rb.angularVelocity = Random.onUnitSphere * 5f;
            StartCoroutine(Despawn(0.8f));
        }
    }

    private void Stagger(Vector3 origin)
    {
        windupUntil = 0f;
        if (warning != null) warning.gameObject.SetActive(false);
        if (head != null) head.localPosition = headHome;
        staggerUntil = Time.time + 0.35f;
        Vector3 away = transform.position - origin; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        staggerVel = away.normalized * 3.5f;
        PopEyes(1.4f);
        Scream(1);
        CancelInvoke(nameof(ResetEyes));
        Invoke(nameof(ResetEyes), 0.3f);
    }

    private void ResetEyes() { if (!dead) PopEyes(1f); }

    private void Kill(int tier, Vector3 blastOrigin)
    {
        if (dead) return;
        dead = true;
        windupUntil = 0f;
        if (warning != null) warning.gameObject.SetActive(false);
        Alive.Remove(this);
        StopAllCoroutines();

        rb.isKinematic = false;
        if (col != null) col.enabled = true;

        Vector3 away = transform.position - blastOrigin; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();

        float up = tier == 3 ? 9f : tier == 2 ? 6f : 3.5f;
        float back = tier == 3 ? 7f : tier == 2 ? 5f : 2.5f;
        rb.linearVelocity = Vector3.up * up + away * back;
        rb.angularVelocity = Random.onUnitSphere * (tier * 6f);

        PopEyes(1.5f + tier * 0.3f);
        Scream(tier);
        Shout(tier);

        StartCoroutine(Despawn(tier == 1 ? 2.5f : 3.5f));
        EnemySpawner.Instance?.OnEnemyDefeated();
    }

    private IEnumerator Despawn(float delay)
    {
        yield return new WaitForSeconds(delay);
        Vector3 s = transform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;
            transform.localScale = Vector3.Lerp(s, Vector3.zero, t);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void PopEyes(float scale)
    {
        if (leftEye != null) leftEye.localScale = eyeScale * scale;
        if (rightEye != null) rightEye.localScale = eyeScale * scale;
    }

    private void Scream(int tier)
    {
        if (audioSource == null || screamClip == null) return;
        audioSource.pitch = (tier == 3 ? 1.35f : tier == 2 ? 1.1f : 0.85f) + Random.Range(-0.06f, 0.06f);
        audioSource.PlayOneShot(screamClip, tier == 1 ? 0.55f : 1f);
    }

    private void Shout(int tier)
    {
        string text = tier == 3 ? "MAMMA MIA?!?!" : tier == 2 ? "NOOO! LA PASTA!!" : "Aiuto!";
        Color color = tier == 3 ? new Color(1f, 0.15f, 0.1f)
                    : tier == 2 ? new Color(1f, 0.5f, 0.12f)
                    : new Color(1f, 0.9f, 0.6f);
        float size = tier == 3 ? 1.3f : tier == 2 ? 1f : 0.75f;

        var go = new GameObject("Shout");
        go.transform.position = (head != null ? head.position : transform.position + Vector3.up * 1.6f) + Vector3.up * 0.4f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text; tm.color = color; tm.font = shoutFont;
        if (shoutFont != null) go.GetComponent<MeshRenderer>().material = shoutFont.material;
        tm.fontSize = 64; tm.characterSize = 0.02f * size;
        tm.anchor = TextAnchor.MiddleCenter; tm.fontStyle = FontStyle.BoldAndItalic;
        go.AddComponent<ShoutFloater>();
    }

    private static AudioClip BuildScream()
    {
        const int rate = 44100; const float dur = 0.7f;
        int n = (int)(rate * dur);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate; float u = t / dur;
            float freq = u < 0.1f ? Mathf.Lerp(420f, 900f, u / 0.1f)
                                  : Mathf.Lerp(900f, 200f, Mathf.Pow((u - 0.1f) / 0.9f, 0.8f));
            freq *= 1f + 0.09f * Mathf.Sin(2f * Mathf.PI * 6.5f * t) * Mathf.Min(1f, u * 3f);
            phase += 2f * Mathf.PI * freq / rate;
            float amp = Mathf.Clamp01(u * 12f) * Mathf.Exp(-2.6f * u);
            float s = Mathf.Sin(phase);
            s = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f);
            data[i] = s * amp * 0.8f;
        }
        var clip = AudioClip.Create("scream", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>敵の叫び文字。上へ浮かびながらフェードして消える</summary>
public class ShoutFloater : MonoBehaviour
{
    private const float Life = 1.5f;
    private float t;
    private TextMesh tm;

    private void Awake() => tm = GetComponent<TextMesh>();

    private void Update()
    {
        t += Time.deltaTime;
        transform.position += Vector3.up * 0.6f * Time.deltaTime;
        var cam = Camera.main;
        if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        if (tm != null) { var c = tm.color; c.a = 1f - Mathf.Pow(Mathf.Clamp01(t / Life), 3f); tm.color = c; }
        if (t >= Life) Destroy(gameObject);
    }
}
