using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スパゲッティ折る核心処理
/// CapsuleCollider×RigidbodyをConfigurableJointで連結、
/// 両手トリガーで角度超過→折る
/// </summary>
public class SpaghettiBreaker : MonoBehaviour
{
    [Header("構成")]
    [SerializeField] private Rigidbody[] segments;
    [SerializeField] private ConfigurableJoint[] joints;

    [Header("折る判定")]
    [SerializeField] private float breakAngleThreshold = 60f;
    [SerializeField] private float deadZone = 0.15f;
    [SerializeField] private float snapThreshold = 0.05f;

    [Header("フィードバック")]
    [SerializeField] private float shakeIntensity = 3f;
    [SerializeField] private AudioClip snapSound;

    private Vector2 leftStick;
    private Vector2 rightStick;
    private AudioSource audioSource;
    private float leftAngle;
    private float rightAngle;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        if (snapSound != null)
        {
            audioSource.clip = snapSound;
            audioSource.playOnAwake = false;
        }
        else
        {
            // ダミークリップ: 折れた気がするためのノイズ生成
            snapSound = AudioClip.Create("snap", 4096, 1, 44100, false, data =>
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = (Random.Range(-1f, 1f)) * Mathf.Exp(-(float)i / 512f);
            });
            audioSource.clip = snapSound;
        }
    }

    public void OnLeftStick(InputAction.CallbackContext ctx) => leftStick = ctx.ReadValue<Vector2>();
    public void OnRightStick(InputAction.CallbackContext ctx) => rightStick = ctx.ReadValue<Vector2>();

    private void Update()
    {
        leftAngle = StickToAngle(leftStick);
        rightAngle = StickToAngle(rightStick);
    }

    private float StickToAngle(Vector2 stick)
    {
        if (stick.magnitude < deadZone) return 0f;
        // デッドゾーン後の値を SNAP 的に丸める
        stick = stick.normalized * Mathf.Max(0, stick.magnitude - snapThreshold);
        return Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 固定フレームで折る判定. ExternalBreakTrigger.cs から呼ぶ
    /// </summary>
    public void TryBreak()
    {
        float diff = Mathf.Abs(leftAngle - rightAngle);
        if (diff < breakAngleThreshold) return;

        int breakIndex = FindWeakestJoint();
        if (breakIndex < 0) return;

        BreakJoint(breakIndex);

        float quality = diff / 180f;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnBreak(quality);
    }

    private int FindWeakestJoint()
    {
        // 最早のジョイントを折る
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null) return i;
        }
        return -1;
    }

    private void BreakJoint(int index)
    {
        var joint = joints[index];
        if (joint == null) return;

        // ジョイント切断
        Object.Destroy(joint);

        // カメラシェイク
        transform.localEulerAngles += Random.Range(-shakeIntensity, shakeIntensity) * Vector3.one;

        // 折れた音
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.Play();
        }

        Debug.Log($"[SpaghettiBreaker] ジョイント{index}切断! 角度差: {Mathf.Abs(leftAngle - rightAngle):F1}°");
    }
}
