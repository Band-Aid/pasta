using UnityEngine;

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [SerializeField, Range(0.01f, 1f)] private float slowTimeScale = 0.05f;
    [SerializeField, Min(0f)] private float easeInDuration = 0.15f;
    [SerializeField, Min(0f)] private float easeOutDuration = 0.25f;

    public bool IsSlowMotionActive { get; private set; }
    private float baseFixedDeltaTime, elapsed, holdDuration, startScale = 1f, currentScale = 1f;
    private float hitStopRemaining;
    private bool paused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }
        Instance = this;
        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    public void RequestSlowMotion(float duration)
    {
        if (!isActiveAndEnabled || paused || duration <= 0f
            || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
        // A new request extends the effect without first jumping back to normal speed.
        startScale = currentScale;
        elapsed = 0f;
        holdDuration = duration;
        IsSlowMotionActive = true;
    }

    public void RequestHitStop(float duration)
    {
        if (!isActiveAndEnabled || paused || duration <= 0f
            || (GameManager.Instance != null && !GameManager.Instance.IsPlaying)) return;
        hitStopRemaining = Mathf.Max(hitStopRemaining, duration);
        ApplyTimeScale();
    }

    public void SetPaused(bool value)
    {
        paused = value;
        hitStopRemaining = 0f;
        ApplyTimeScale();
    }

    public void ResetTime()
    {
        IsSlowMotionActive = false;
        elapsed = holdDuration = hitStopRemaining = 0f;
        startScale = currentScale = 1f;
        paused = false;
        ApplyTimeScale();
    }

    private void Update()
    {
        if (paused) return;
        hitStopRemaining = Mathf.Max(0f, hitStopRemaining - Time.unscaledDeltaTime);
        if (IsSlowMotionActive)
        {
            elapsed += Time.unscaledDeltaTime;
            float slowScale = Mathf.Clamp(slowTimeScale, 0.01f, 1f);
            if (elapsed < easeInDuration)
                currentScale = Mathf.Lerp(startScale, slowScale, EaseOutQuad(elapsed / easeInDuration));
            else if (elapsed < easeInDuration + holdDuration)
                currentScale = slowScale;
            else if (elapsed < easeInDuration + holdDuration + easeOutDuration)
                currentScale = Mathf.Lerp(slowScale, 1f,
                    EaseInQuad((elapsed - easeInDuration - holdDuration) / easeOutDuration));
            else
            {
                currentScale = 1f;
                IsSlowMotionActive = false;
            }
        }
        ApplyTimeScale();
    }

    private void ApplyTimeScale()
    {
        float scale = paused ? 0f : hitStopRemaining > 0f ? Mathf.Min(currentScale, 0.08f) : currentScale;
        Time.timeScale = scale;
        // Keep flying enemies and rigidbody fragments smooth at 5% speed.
        Time.fixedDeltaTime = baseFixedDeltaTime * (scale > 0f ? scale : 1f);
    }

    private void OnDisable()
    {
        if (Instance == this) ResetTime();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInQuad(float t) => t * t;
}
