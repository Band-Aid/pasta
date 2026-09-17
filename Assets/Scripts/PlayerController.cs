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
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private float gravity = -18f;

    [Header("HP")]
    [SerializeField] private float maxHealth = 100f;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => Health > 0f;

    private CharacterController cc;
    private float yaw;
    private float pitch;
    private float vy;
    private Vector3 startPos;

    private void Awake()
    {
        Instance = this;
        Health = maxHealth;
        yaw = transform.eulerAngles.y;
        startPos = transform.position;
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
        HandleLook();
        HandleMove();

        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            LockCursor(false);
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

        bool sprint = kb != null && kb.leftShiftKey.isPressed;
        float speed = moveSpeed * (sprint ? sprintMultiplier : 1f);

        Vector3 dir = transform.forward * mv.y + transform.right * mv.x;

        if (cc.isGrounded && vy < 0f) vy = -2f;
        vy += gravity * Time.deltaTime;

        Vector3 vel = dir * speed + Vector3.up * vy;
        cc.Move(vel * Time.deltaTime);
    }

    public void TakeDamage(float dmg)
    {
        if (!IsAlive) return;
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;

        Health = Mathf.Max(0f, Health - dmg);
        if (Health <= 0f)
            GameManager.Instance?.OnPlayerDied();
    }

    public void ResetPlayer()
    {
        Health = maxHealth;
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
