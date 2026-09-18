using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の状態管理。開始→ウェーブ襲来→HP0でゲームオーバー→リスタート。
/// 参照はほぼシングルトン経由で解決する。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { Playing, GameOver, Won }
    public State Current { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsPlaying => Current == State.Playing && !IsPaused;
    public bool HasEnded => Current != State.Playing;

    public void SetPaused(bool paused)
    {
        if (HasEnded) return;
        IsPaused = paused;
        hitStopUntil = 0f;
        Time.timeScale = paused ? 0f : 1f;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
        if (paused) PastaHand.Instance?.CancelCharge();
    }

    private float hitStopUntil;
    private void Awake() { Instance = this; Time.timeScale = 1f; }

    public void HitStop(float duration)
    {
        if (!IsPlaying) return;
        hitStopUntil = Time.unscaledTime + duration;
        Time.timeScale = 0.08f;
    }

    private void OnDisable() { Time.timeScale = 1f; }


    private void Start() => StartGame();

    private void Update()
    {
        if (!IsPaused && Time.timeScale != 1f && Time.unscaledTime >= hitStopUntil) Time.timeScale = 1f;
        if (!HasEnded) return;

        bool retry = false;
        var kb = Keyboard.current;
        if (kb != null) retry |= kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        if (Mouse.current != null) retry |= Mouse.current.leftButton.wasPressedThisFrame;
        if (Gamepad.current != null) retry |= Gamepad.current.buttonSouth.wasPressedThisFrame;
        if (retry) StartGame();
    }

    private void StartGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        hitStopUntil = 0f;
        // Include airborne defeated enemies and visual leftovers on retry.
        foreach (var fragment in FindObjectsByType<PastaFragment>()) Destroy(fragment.gameObject);
        foreach (var wave in FindObjectsByType<Shockwave>()) Destroy(wave.gameObject);
        foreach (var shout in FindObjectsByType<ShoutFloater>()) Destroy(shout.gameObject);
        // 残敵を一掃
        foreach (var e in FindObjectsByType<Enemy>())
            if (e != null) Destroy(e.gameObject);
        Enemy.Alive.Clear();

        ScoreManager.Instance?.ResetScore();
        PlayerController.Instance?.ResetPlayer();
        PastaHand.Instance?.ResetHand();

        Current = State.Playing;
        EnemySpawner.Instance?.Begin();
    }

    public void OnPlayerDied()
    {
        if (HasEnded) return;
        Current = State.GameOver;
        Time.timeScale = 1f;
        PastaHand.Instance?.CancelCharge();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnemySpawner.Instance?.StopAll();
    }

    public void Win()
    {
        if (HasEnded) return;
        Current = State.Won;
        IsPaused = false;
        Time.timeScale = 1f;
        hitStopUntil = 0f;
        PastaHand.Instance?.CancelCharge();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        EnemySpawner.Instance?.StopAll();
    }
}
