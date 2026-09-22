using System.Collections;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [SerializeField] private float slowTimeScale = 0.05f;
    [SerializeField] private float easeInDuration = 0.15f;
    [SerializeField] private float easeOutDuration = 0.25f;

    private Coroutine active;

    private void Awake() => Instance = this;

    public void RequestSlowMotion(float duration)
    {
        if (active != null) StopCoroutine(active);
        active = StartCoroutine(RunSlowMotion(duration));
    }

    private IEnumerator RunSlowMotion(float duration)
    {
        float elapsed = 0f;
        while (elapsed < easeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseOutQuad(Mathf.Clamp01(elapsed / easeInDuration));
            Time.timeScale = Mathf.Lerp(1f, slowTimeScale, t);
            yield return null;
        }
        Time.timeScale = slowTimeScale;

        yield return new WaitForSecondsRealtime(duration);

        elapsed = 0f;
        while (elapsed < easeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = EaseInQuad(Mathf.Clamp01(elapsed / easeOutDuration));
            Time.timeScale = Mathf.Lerp(slowTimeScale, 1f, t);
            yield return null;
        }
        Time.timeScale = 1f;
        active = null;
    }

    private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private static float EaseInQuad(float t) => t * t;
}
