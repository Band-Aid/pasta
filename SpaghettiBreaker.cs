using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スパゲッティを両手掴み→角度指定→折る。
/// プロト目的: 折る瞬間の気持ちを検証する。
/// </summary>
public class SpaghettiBreaker : MonoBehaviour
{
    [Header("構成")]
    [SerializeField] Rigidbody[] segments;     // CapsuleCollider付きRigidbody達
    [SerializeField] float breakThreshold = 45f; // この角度差で折る判定
    [SerializeField] float breakForce = 500f;

    [Header("入力")]
    [SerializeField] InputActionReference leftGrip;
    [SerializeField] InputActionReference rightGrip;
    [SerializeField] InputActionReference leftStick;
    [SerializeField] InputActionReference rightStick;

    bool _leftHolding, _rightHolding;
    float _leftAngle, _rightAngle;

    // デッドゾーンとスナップ閾値。初日から入れるのはお約束通り
    const float DeadZone = 0.15f;
    const float SnapThreshold = 0.05f;

    void OnEnable()
    {
        leftGrip.action.performed += _ => _leftHolding = true;
        leftGrip.action.canceled  += _ => _leftHolding = false;
        rightGrip.action.performed += _ => _rightHolding = true;
        rightGrip.action.canceled  += _ => _rightHolding = false;
    }

    void OnDisable()
    {
        leftGrip.action.performed -= _ => _leftHolding = true;
        leftGrip.action.canceled  -= _ => _leftHolding = false;
        rightGrip.action.performed -= _ => _rightHolding = true;
        rightGrip.action.canceled  -= _ => _rightHolding = false;
    }

    void Update()
    {
        // スティック入力をデッドゾーン越しに角度に変換
        Vector2 l = ApplyDeadZone(leftStick.action.ReadValue<Vector2>());
        Vector2 r = ApplyDeadZone(rightStick.action.ReadValue<Vector2>());

        _leftAngle  = Mathf.Atan2(l.y, l.x) * Mathf.Rad2Deg;
        _rightAngle = Mathf.Rad2Deg * Mathf.Atan2(r.y, r.x);

        // 両手掴んで角度差が閾値超えたら折る
        if (_leftHolding && _rightHolding)
        {
            float diff = Mathf.Abs(_leftAngle - _rightAngle);
            if (diff > breakThreshold || diff < -breakThreshold)
                TryBreak();
        }
    }

    Vector2 ApplyDeadZone(Vector2 v)
    {
        if (v.magnitude < DeadZone) return Vector2.zero;
        // スナップ。小さめの入力を丸めてノイズ防止
        if (v.magnitude < DeadZone + SnapThreshold)
            v = v.normalized * (DeadZone + SnapThreshold);
        float mag = (v.magnitude - DeadZone) / (1f - DeadZone);
        return v.normalized * mag;
    }

    void TryBreak()
    {
        // 折る瞬間のフィードバックが全て
        //   1) 即座に音を鳴らす（遅延ゼロが正解）
        //   2) 画面振動
        //   3) ジョイント切断
        AudioSource.PlayClipAtPoint(GetBreakSound(), transform.position);
        Camera.main.transform.localEulerAngles += new Vector3(Random.Range(-3f, 3f), Random.Range(-3f, 3f), 0f);

        BreakJoints();
        enabled = false; // プロトなんで折ったら終わり
    }

    void BreakJoints()
    {
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var joint = segments[i].GetComponent<ConfigurableJoint>();
            if (joint != null)
            {
                Object.Destroy(joint);
                // 切断面にランダムな力を加えて派手に散る
                segments[i].AddForceAtPosition(
                    Random.insideUnitSphere * breakForce,
                    segments[i].transform.position,
                    ForceMode.Impulse);
            }
        }
    }

    AudioClip GetBreakSound()
    {
        // プロト中はどれでも良いが、後で「折れた時の音效リスト」に差し替え
        return AudioClip.Create("snap", 1, 1, 44100, false);
    }
}
