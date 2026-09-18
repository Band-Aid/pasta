using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 一人称プレイヤー。WASD/左スティックで街を歩き、マウス/右スティックで見回す。HP制。
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("視点")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float lookSensitivity = 0.10f;
    [SerializeField] private float padLookSpeed = 200f;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 78f;

    [Header("移動")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float gravity = -18f;

    [Header("HP")]
    [SerializeField] private float maxHealth = 100f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => Health > 0f;
    public bool IsDashing => Time.time < dashUntil;
    public float DashCooldown => Mathf.Max(0f, dashReadyAt - Time.time);
    private float dashUntil, dashReadyAt;
    private Vector3 dashDirection;

    private CharacterController cc;
    private float yaw;
    private float pitch;
    private float vy;
    private Vector3 startPos;
    private Vector3 cameraHome;
    private Camera playerCamera;
    private float kick, shake, damageUntil, baseFov;
    private AudioSource impactAudio;
    public float DamageFlash => Mathf.Clamp01((damageUntil - Time.unscaledTime) / 0.35f);

    private void Awake()
    {
        Instance = this;
        Health = maxHealth;
        yaw = transform.eulerAngles.y;
        startPos = transform.position;
        if (cameraPivot != null)
        {
            cameraHome = cameraPivot.localPosition;
            playerCamera = cameraPivot.GetComponent<Camera>();
            if (playerCamera != null) baseFov = playerCamera.fieldOfView;
        }
        impactAudio = gameObject.AddComponent<AudioSource>();
        impactAudio.playOnAwake = false;
        cc = GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = gameObject.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    private void Start() => LockCursor(true);

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.HasEnded) return;
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var gp = Gamepad.current;
        bool pause = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
        if (pause && gm != null) { gm.SetPaused(!gm.IsPaused); return; }
        if (gm != null && gm.IsPaused)
        {
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame)) gm.SetPaused(false);
            return;
        }
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if ((mouse != null && mouse.leftButton.wasPressedThisFrame) || gp != null) LockCursor(true);
            return;
        }
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        var mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, minPitch, maxPitch);
        }
        var gp = Gamepad.current;
        if (gp != null)
        {
            Vector2 rs = gp.rightStick.ReadValue();
            yaw += rs.x * padLookSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch - rs.y * padLookSpeed * Time.deltaTime, minPitch, maxPitch);
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMove()
    {
        if (cc == null) return;

        Vector2 mv = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) mv.y += 1f;
            if (kb.sKey.isPressed) mv.y -= 1f;
            if (kb.dKey.isPressed) mv.x += 1f;
            if (kb.aKey.isPressed) mv.x -= 1f;
        }
        var gp = Gamepad.current;
        if (gp != null) mv += gp.leftStick.ReadValue();
        if (mv.sqrMagnitude > 1f) mv.Normalize();

        Vector3 dir = transform.forward * mv.y + transform.right * mv.x;
        bool dash = (kb != null && kb.leftShiftKey.wasPressedThisFrame) || (gp != null && gp.rightShoulder.wasPressedThisFrame);
        if (dash) TryDash(dir);

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * Time.deltaTime;

        Vector3 vel = (IsDashing ? dashDirection * 19f : dir * moveSpeed) + Vector3.up * vy;
        cc.Move(vel * Time.deltaTime);
    }

    public bool TryDash(Vector3 direction)
    {
        if (DashCooldown > 0f || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return false;
        direction.y = 0f;
        dashDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
        dashUntil = Time.time + 0.18f;
        dashReadyAt = Time.time + 1.15f;
        return true;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        kick = Mathf.MoveTowards(kick, 0f, dt * 8f);
        shake = Mathf.MoveTowards(shake, 0f, dt * 0.2f);
        if (cameraPivot != null)
        {
            float t = Time.unscaledTime;
            cameraPivot.localPosition = cameraHome + new Vector3(Mathf.Sin(t * 83f), Mathf.Cos(t * 97f), 0f) * shake;
            cameraPivot.localRotation = Quaternion.Euler(pitch - kick * 1.7f, 0f, Mathf.Sin(t * 67f) * shake * 12f);
        }
        if (playerCamera != null)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFov + kick * 3f + (IsDashing ? 9f : 0f), 1f - Mathf.Exp(-15f * dt));
        }
    }

    public void OnSnap(int tier, int kills)
    {
        kick = tier == 3 ? 1f : 0.5f;
        shake = kills > 0 ? (tier == 3 ? 0.026f : 0.012f) : 0.005f;
        impactAudio.pitch = 1f;
        if (kills > 0) impactAudio.PlayOneShot(PastaAudio.Impact, tier == 3 ? 0.7f : 0.4f);
    }

    public void Heal(float amount) => Health = Mathf.Min(maxHealth, Health + Mathf.Max(0f, amount));

    public void OnDominoImpact()
    {
        shake = Mathf.Max(shake, 0.018f);
        impactAudio.pitch = Random.Range(1.15f, 1.45f);
        impactAudio.PlayOneShot(PastaAudio.Impact, 0.25f);
    }

    public void TakeDamage(float dmg)
    {
        if (!IsAlive || IsDashing) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        Health = Mathf.Max(0f, Health - dmg);
        damageUntil = Time.unscaledTime + 0.35f;
        shake = 0.022f;
        ScoreManager.Instance?.BreakCombo();
        if (Health <= 0f)
            GameManager.Instance?.OnPlayerDied();
    }

    public void ResetPlayer()
    {
        Health = maxHealth;
        kick = shake = damageUntil = 0f;
        dashUntil = dashReadyAt = 0f;
        yaw = 0f;
        pitch = 0f;
        vy = 0f;
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = startPos;
            cc.enabled = true;
        }
        if (cameraPivot != null) cameraPivot.localRotation = Quaternion.identity;
        LockCursor(true);
    }

    private void LockCursor(bool on)
    {
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }
}
