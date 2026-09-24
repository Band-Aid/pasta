using UnityEngine;
using UnityEngine.InputSystem;

public enum PastaType { Spaghetti, Penne, Lasagna }

/// <summary>Hold to bend, release in the gold window. Over-bending forces a weak snap.</summary>
public class PastaHand : MonoBehaviour
{
    public static PastaHand Instance { get; private set; }
    [SerializeField] private SpaghettiBreaker[] pastaPrefabs;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Shockwave shockwavePrefab;
    [SerializeField] private float maxBend = 150f;
    [SerializeField] private float sweepRate = 115f;
    [SerializeField] private float spaghettiRadiusTier1 = 2.5f;
    [SerializeField] private float spaghettiRadiusTier2 = 5f;
    [SerializeField] private float spaghettiRadiusTier3 = 8f;

    public SpaghettiBreaker Current { get; private set; }
    public PastaType Type { get; private set; }
    public bool Charging { get; private set; }
    public bool Overbent { get; private set; }
    public float ReloadRemaining => Mathf.Max(0f, reloadUntil - Time.time);
    public float Strain => Current != null ? Current.CurrentDiff / maxBend : 0f;
    public int TargetsInReach { get; private set; }
    public float Recoil { get; private set; }
    public float LureCooldown => Mathf.Max(0f, lureReadyAt - Time.time);
    private float lureReadyAt, lureVisibleUntil;
    private Vector3 rallyPoint;
    private LineRenderer rallyRing, launchGuide;
    private Material guideMaterial;
    private float angle, reloadUntil;
    private bool requireRelease;
    private Transform leftHand, rightHand;
    private Vector3 holdHome;
    private Quaternion holdRotation;

    private void Awake() => Instance = this;

    private void Start()
    {
        if (holdPoint != null)
        {
            holdHome = holdPoint.localPosition;
            holdRotation = holdPoint.localRotation;
            leftHand = holdPoint.Find("HandL");
            rightHand = holdPoint.Find("HandR");
            DetailGrip(leftHand);
            DetailGrip(rightHand);
        }
        if (Current == null) Spawn();
        guideMaterial = new Material(Resources.Load<Shader>("SnapWave"));
        rallyRing = CreateGuide("Rally point", new Color(0.35f, 1f, 0.85f), 0.08f);
        rallyRing.loop = true;
        rallyRing.positionCount = 48;
        launchGuide = CreateGuide("Launch direction", new Color(1f, 0.8f, 0.3f, 0.65f), 0.055f);
    }

    private LineRenderer CreateGuide(string name, Color color, float width)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.useWorldSpace = true;
        line.sharedMaterial = guideMaterial;
        line.startColor = line.endColor = color;
        line.widthMultiplier = width;
        return line;
    }

    private void OnDestroy()
    {
        if (guideMaterial != null) Destroy(guideMaterial);
    }

    private void Update()
    {
        bool held = BreakHeld();
        if (!held) requireRelease = false;
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        if (!playing || Cursor.lockState != CursorLockMode.Locked)
        {
            CancelCharge();
            if (held) requireRelease = true;
            return;
        }
        HandleModeInput();
        if (Current == null)
        {
            if (Time.time >= reloadUntil) Spawn();
            return;
        }
        if (held && !requireRelease && !Charging)
        {
            Charging = true;
            Overbent = false;
            angle = 0f;
            ScoreManager.Instance?.ClearInputHint();
        }
        if (Charging)
        {
            float speed = sweepRate * (Type == PastaType.Penne ? 1.15f : Type == PastaType.Lasagna ? 0.78f : 1f);
            angle = Mathf.Min(maxBend, angle + Time.deltaTime * speed);
            Current.SetBend(angle);
            if (angle >= maxBend)
            {
                Overbent = true;
                requireRelease = true;
                DoBreak();
            }
            else if (!held) DoBreak();
        }
        var preview = CreateBlast(Current != null && Current.CurrentDiff >= Current.BreakAngleThreshold
            ? Tier(Current.QualityAt(Current.CurrentDiff)) : 3);
        TargetsInReach = Shockwave.CountTargets(preview);
    }

    private void LateUpdate()
    {
        if (holdPoint == null) return;
        Recoil = Mathf.MoveTowards(Recoil, 0f, Time.unscaledDeltaTime * 5f);
        float strain = Strain;
        float tremble = Charging ? Mathf.Sin(Time.unscaledTime * 74f) * strain * strain * 0.0015f : 0f;
        float reloadDip = Current == null ? Mathf.Sin(Mathf.Clamp01(ReloadRemaining / ReloadDelay()) * Mathf.PI) * 0.15f : 0f;
        holdPoint.localPosition = holdHome + new Vector3(tremble, strain * 0.035f - Recoil * 0.045f - reloadDip,
            -Recoil * 0.075f);
        holdPoint.localRotation = holdRotation * Quaternion.Euler(-Recoil * 9f, 0f, tremble * 300f);
        PoseHand(leftHand, -1f);
        PoseHand(rightHand, 1f);
        UpdateGuides();
    }

    private void UpdateGuides()
    {
        if (rallyRing == null) return;
        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        rallyRing.enabled = playing && Time.time < lureVisibleUntil;
        if (rallyRing.enabled)
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48;
                rallyRing.SetPosition(i, rallyPoint + new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)) * 1.3f);
            }
        launchGuide.enabled = playing && Current != null && Type != PastaType.Lasagna;
        if (!launchGuide.enabled) return;
        var spec = CreateBlast(3);
        Vector3 forward = spec.forward, side = Vector3.Cross(Vector3.up, forward);
        Vector3 start = transform.position + Vector3.up * 0.12f + forward * 1.2f;
        Vector3 end = transform.position + Vector3.up * 0.12f + forward * 17f;
        launchGuide.positionCount = 5;
        launchGuide.SetPosition(0, start);
        launchGuide.SetPosition(1, end);
        launchGuide.SetPosition(2, end - forward * 0.9f + side * 0.6f);
        launchGuide.SetPosition(3, end);
        launchGuide.SetPosition(4, end - forward * 0.9f - side * 0.6f);
    }

    public bool TryLure()
    {
        if (LureCooldown > 0f || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return false;
        Vector3 forward = CreateBlast(3).forward;
        rallyPoint = transform.position + forward * 6f;
        // A rally point must stay on the player's side of solid scenery.
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up, forward, 6f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponentInParent<Enemy>() == null && hit.collider.GetComponentInParent<PlayerController>() == null
                && hit.collider.GetComponentInParent<PastaFragment>() == null)
                rallyPoint = transform.position + forward * Mathf.Min(Vector3.Distance(transform.position, rallyPoint), Mathf.Max(1f, hit.distance - 1f));
        rallyPoint.y = 0.05f;
        int count = 0;
        foreach (var enemy in Enemy.Alive)
            if (enemy != null && (enemy.transform.position - transform.position).sqrMagnitude < 18f * 18f)
            {
                enemy.Lure(rallyPoint, 2.6f);
                count++;
            }
        if (count == 0) { ScoreManager.Instance?.Announce("もう少し近づいて挑発しよう"); return false; }
        lureReadyAt = Time.time + 5f;
        lureVisibleUntil = Time.time + 2.6f;
        ScoreManager.Instance?.Announce($"こっちだ！  {count} 人を集める\n横へ回って、奥の敵にぶつけよう");
        return true;
    }

    private static void DetailGrip(Transform hand)
    {
        if (hand == null) return;
        var fingers = hand.Find("Fingers");
        var palm = hand.Find("Palm");
        if (fingers == null || palm == null) return;
        fingers.gameObject.SetActive(false);
        var material = palm.GetComponent<Renderer>().sharedMaterial;
        palm.localScale = new Vector3(0.115f, 0.09f, 0.10f);
        var thumb = hand.Find("Thumb");
        if (thumb != null)
        {
            var p = thumb.localPosition; p.y = 0.025f; p.z = -0.06f; thumb.localPosition = p;
            thumb.localScale = new Vector3(0.032f, 0.031f, 0.032f);
            thumb.localRotation = Quaternion.Euler(25f, 0f, hand.name == "HandL" ? -100f : 100f);
        }
        for (int i = 0; i < 4; i++)
        {
            var finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            finger.name = "Curled finger " + i;
            finger.transform.SetParent(hand, false);
            finger.transform.localPosition = new Vector3(-0.042f + i * 0.028f, 0.027f, -0.057f);
            finger.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            finger.transform.localScale = new Vector3(0.028f, 0.032f - Mathf.Abs(1.5f - i) * 0.003f, 0.028f);
            var collider = finger.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            finger.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    private void PoseHand(Transform hand, float side)
    {
        if (hand == null) return;
        Vector3 grip = Current != null ? (side < 0 ? Current.LeftGrip : Current.RightGrip) : Vector3.right * side * 0.295f;
        grip += new Vector3(side * Recoil * 0.095f, -0.025f - Recoil * 0.045f, 0f);
        hand.localPosition = Vector3.Lerp(hand.localPosition, grip, 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime));
        hand.localRotation = Quaternion.Euler(0f, side * Recoil * 18f, -side * (Strain * 66f + Recoil * 23f));
    }

    private static bool BreakHeld()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) return true;
        var gp = Gamepad.current;
        return gp != null && (gp.rightTrigger.ReadValue() > 0.5f || gp.buttonSouth.isPressed);
    }

    private void DoBreak()
    {
        Charging = false;
        float quality = Current.TryBreak();
        if (quality < 0f)
        {
            Current.SetBend(0f);
            ScoreManager.Instance?.TooEarly();
            return;
        }
        var tossed = Current;
        Current = null;
        tossed.transform.SetParent(null, true);
        tossed.Release(holdPoint.forward * 1.8f);
        Destroy(tossed.gameObject, 0.7f);
        int tier = Tier(quality);
        BlastSpec spec = CreateBlast(tier);
        int closeCalls = 0;
        foreach (var enemy in Enemy.Alive)
            if (enemy != null && enemy.IsWindingUp && Shockwave.Contains(spec, enemy.transform.position)) closeCalls++;
        var result = Shockwave.Blast(spec, shockwavePrefab);
        ScoreManager.Instance?.OnBreak(result.kills, tier, quality, 1f, closeCalls);
        Recoil = tier == 3 ? 1f : tier == 2 ? 0.7f : 0.45f;
        PlayerController.Instance?.OnSnap(tier, result.kills);
        if (result.hits > 0) TimeManager.Instance?.RequestSlowMotion(1.5f);
        reloadUntil = Time.time + ReloadDelay() * (tier == 3 && Type != PastaType.Lasagna ? 0.72f : 1f);
        TargetsInReach = 0;
    }

    public BlastSpec CreateBlast(int tier)
    {
        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        var spec = new BlastSpec
        {
            tier = tier, origin = transform.position, forward = forward.normalized,
            kind = BlastKind.Cone, halfAngleDeg = 32f, pasta = Type
        };
        float rhythm = 1f + Mathf.Min(ScoreManager.Instance != null ? ScoreManager.Instance.PerfectStreak : 0, 4) * 0.05f;
        float spaghettiRadius = (tier == 3 ? spaghettiRadiusTier3 * 0.5f
            : tier == 2 ? spaghettiRadiusTier2 * 0.65f : spaghettiRadiusTier1) * rhythm;
        switch (Type)
        {
            case PastaType.Penne:
                spec.kind = BlastKind.Line;
                spec.length = (tier == 3 ? 10f : tier == 2 ? 8f : 6f) * rhythm;
                spec.halfWidth = 0.65f;
                break;
            case PastaType.Lasagna:
                spec.radius = (tier == 3 ? 5.5f : tier == 2 ? 4f : 2.5f) * rhythm;
                spec.halfAngleDeg = 180f;
                spec.radialLaunch = true;
                break;
            default:
                spec.radius = spaghettiRadius;
                break;
        }
        return spec;
    }

    public static int Tier(float quality) => quality >= 0.9f ? 3 : quality >= 0.7f ? 2 : 1;
    private float ReloadDelay() => Type == PastaType.Lasagna ? 2.5f : Type == PastaType.Penne ? 0.38f : 0.48f;

    private void HandleModeInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) SetType((PastaType)(((int)Type + 1) % 3));
            if (kb.digit1Key.wasPressedThisFrame) SetType(PastaType.Spaghetti);
            if (kb.digit2Key.wasPressedThisFrame) SetType(PastaType.Penne);
            if (kb.digit3Key.wasPressedThisFrame) SetType(PastaType.Lasagna);
            if (kb.eKey.wasPressedThisFrame) TryLure();
        }
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) TryLure();
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonWest.wasPressedThisFrame) SetType((PastaType)(((int)Type + 1) % 3));
            if (gp.leftShoulder.wasPressedThisFrame) TryLure();
        }
    }

    public void CancelCharge()
    {
        Charging = false;
        angle = 0f;
        if (Current != null) Current.SetBend(0f);
    }

    public void SetType(PastaType type)
    {
        if (type == Type) return;
        CancelCharge();
        Type = type;
        requireRelease = BreakHeld();
        if (Current != null) Spawn();
    }

    private void Spawn()
    {
        if (holdPoint == null || pastaPrefabs == null || pastaPrefabs.Length == 0) return;
        var prefab = pastaPrefabs[Mathf.Clamp((int)Type, 0, pastaPrefabs.Length - 1)];
        if (prefab == null) return;
        if (Current != null) Destroy(Current.gameObject);
        Current = Instantiate(prefab, holdPoint, false);
        Current.transform.localPosition = Vector3.zero;
        Current.transform.localRotation = Quaternion.identity;
        Current.Configure(Type);
        angle = 0f;
    }

    public void ResetHand()
    {
        CancelInvoke();
        Charging = false;
        Type = PastaType.Spaghetti;
        Recoil = 0f;
        Overbent = false;
        reloadUntil = 0f;
        lureReadyAt = lureVisibleUntil = 0f;
        TargetsInReach = 0;
        requireRelease = BreakHeld();
        Spawn();
    }
}
