using UnityEngine;
using UnityEngine.InputSystem;

public enum PastaType { Spaghetti, Penne, Lasagna }

/// <summary>
/// プレイヤーの手元。カメラ前にパスタを保持。
/// 折る操作は「押している間に曲げ角がせり上がり、離した角度で折る」チャージ式。
///   移動:WASD/左スティック（PlayerController）  視点:マウス/右スティック
///   折る:左クリック長押し→離す（Space・トリガーも可）  弾種:Q・1/2/3・□  狙撃:右クリック・Tab・L1
/// </summary>
public class PastaHand : MonoBehaviour
{
    public static PastaHand Instance { get; private set; }

    [Header("参照（0=Spaghetti,1=Penne,2=Lasagna）")]
    [SerializeField] private SpaghettiBreaker[] pastaPrefabs;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Shockwave shockwavePrefab;

    [Header("チャージ（曲げ）")]
    [SerializeField] private float maxBend = 150f;      // 押しっぱなしで到達する最大曲げ角
    [SerializeField] private float sweepRate = 115f;    // 曲げ角のせり上がり速度(°/s)

    [Header("スパゲッティ半径（ティア別）")]
    [SerializeField] private float radiusTier1 = 4f;
    [SerializeField] private float radiusTier2 = 7.5f;
    [SerializeField] private float radiusTier3 = 12f;

    public SpaghettiBreaker Current { get; private set; }
    public PastaType Type { get; private set; } = PastaType.Spaghetti;
    public bool Aimed { get; private set; }
    public bool Charging { get; private set; }

    private float chargeStart;

    private void Awake() => Instance = this;

    private void Start() => Spawn();

    private void Update()
    {
        HandleModeInput();
        HandleCharge();
    }

    // ---- 折るチャージ ----

    private void HandleCharge()
    {
        if (Current == null) return;

        bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        bool held = playing && Cursor.lockState == CursorLockMode.Locked && BreakHeld();

        if (held && !Charging)
        {
            Charging = true;
            chargeStart = Time.time;
        }

        if (Charging)
        {
            float diff = Mathf.Min(maxBend, (Time.time - chargeStart) * sweepRate);
            Current.SetBend(diff);

            if (!held) // 離した瞬間に折る
            {
                Charging = false;
                DoBreak();
            }
        }
    }

    private bool BreakHeld()
    {
        var mo = Mouse.current;
        if (mo != null && mo.leftButton.isPressed) return true;
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.isPressed || kb.enterKey.isPressed)) return true;
        var gp = Gamepad.current;
        if (gp != null && (gp.rightTrigger.ReadValue() > 0.5f || gp.leftTrigger.ReadValue() > 0.5f || gp.buttonSouth.isPressed)) return true;
        return false;
    }

    private void DoBreak()
    {
        float quality = Current.TryBreak();
        if (quality < 0f) { Current.SetBend(0f); return; } // 角度不足＝空曲げ。パスタは保持したまま

        var tossed = Current;
        Current = null;
        Vector3 fwd = holdPoint != null ? holdPoint.forward : transform.forward;
        tossed.transform.SetParent(null, true);
        tossed.Release(fwd * 2.5f + Vector3.down * 1f);
        Destroy(tossed.gameObject, 1.6f);

        Snap(quality);
        Invoke(nameof(Spawn), ReloadDelay());
    }

    private void Snap(float quality)
    {
        int tier = quality > 0.9f ? 3 : quality > 0.7f ? 2 : 1;
        Vector3 origin = PlayerController.Instance != null
            ? PlayerController.Instance.transform.position
            : transform.position;

        BlastSpec s = default;
        s.tier = tier;
        s.origin = origin;
        s.forward = CamForward();
        float scoreMul = 1f;

        switch (Type)
        {
            case PastaType.Penne:
                s.kind = BlastKind.Line;
                s.length = tier == 3 ? 24f : tier == 2 ? 18f : 12f;
                s.halfWidth = tier == 3 ? 2.3f : tier == 2 ? 1.9f : 1.5f;
                if (Aimed) { s.length *= 1.3f; scoreMul = 1.4f; }
                break;

            case PastaType.Lasagna:
                s.kind = BlastKind.Cone;
                s.radius = TierRadius(tier) * 1.7f;
                if (Aimed) { s.halfAngleDeg = 75f; s.radius *= 1.3f; scoreMul = 1.4f; }
                else s.halfAngleDeg = 180f;
                break;

            default:
                s.kind = BlastKind.Cone;
                s.radius = TierRadius(tier);
                if (Aimed) { s.halfAngleDeg = 45f; s.radius *= 1.4f; scoreMul = 1.5f; }
                else s.halfAngleDeg = 180f;
                break;
        }

        int kills = Shockwave.Blast(s, shockwavePrefab);
        ScoreManager.Instance?.OnBreak(kills, tier, quality, scoreMul);
    }

    private float TierRadius(int tier) => tier == 3 ? radiusTier3 : tier == 2 ? radiusTier2 : radiusTier1;
    private float ReloadDelay() => Type == PastaType.Lasagna ? 1.15f : Type == PastaType.Penne ? 0.6f : 0.7f;

    private Vector3 CamForward()
    {
        var c = Camera.main;
        if (c == null) return transform.forward;
        Vector3 f = c.transform.forward; f.y = 0f;
        return f.sqrMagnitude < 0.0001f ? Vector3.forward : f.normalized;
    }

    // ---- 弾種/狙撃 ----

    private void HandleModeInput()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) SetType((PastaType)(((int)Type + 1) % 3));
            if (kb.digit1Key.wasPressedThisFrame) SetType(PastaType.Spaghetti);
            if (kb.digit2Key.wasPressedThisFrame) SetType(PastaType.Penne);
            if (kb.digit3Key.wasPressedThisFrame) SetType(PastaType.Lasagna);
            if (kb.tabKey.wasPressedThisFrame) Aimed = !Aimed;
        }
        var mo = Mouse.current;
        if (mo != null && mo.rightButton.wasPressedThisFrame) Aimed = !Aimed;
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonWest.wasPressedThisFrame) SetType((PastaType)(((int)Type + 1) % 3));
            if (gp.leftShoulder.wasPressedThisFrame) Aimed = !Aimed;
        }
    }

    private void SetType(PastaType t)
    {
        if (t == Type) return;
        Type = t;
        Charging = false;
        if (Current != null && !Current.IsBroken) Spawn();
    }

    private void Spawn()
    {
        if (holdPoint == null || pastaPrefabs == null || pastaPrefabs.Length == 0) return;
        var prefab = pastaPrefabs[Mathf.Clamp((int)Type, 0, pastaPrefabs.Length - 1)];
        if (prefab == null) return;
        if (Current != null) Destroy(Current.gameObject);
        Current = Instantiate(prefab, holdPoint.position, holdPoint.rotation, holdPoint);
        Current.transform.localPosition = Vector3.zero;
        Current.transform.localRotation = Quaternion.identity;
    }

    public void ResetHand()
    {
        CancelInvoke(nameof(Spawn));
        Charging = false;
        Spawn();
    }
}
