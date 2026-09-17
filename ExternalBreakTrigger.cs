using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力トリガーでSpaghettiBreaker.TryBreak()を呼ぶ中継。
/// Player Inputからのメッセージ or 直接InputActionから使える。
/// </summary>
public class ExternalBreakTrigger : MonoBehaviour
{
    [SerializeField] private SpaghettiBreaker breaker;

    private void Awake()
    {
        if (breaker == null) breaker = FindObjectOfType<SpaghettiBreaker>();
    }

    public void OnBreak(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && breaker != null)
            breaker.TryBreak();
    }

    // 旧InputやUIボタン用
    public void TriggerBreak()
    {
        breaker?.TryBreak();
    }
}
