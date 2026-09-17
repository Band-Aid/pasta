using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム全体の状態管理。開始→ウェーブ襲来→HP0でゲームオーバー→リスタート。
/// 参照はほぼシングルトン経由で解決する。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum State { Playing, GameOver }
    public State Current { get; private set; }
    public bool IsPlaying => Current == State.Playing;

    private void Awake() => Instance = this;

    private void Start() => StartGame();

    private void Update()
    {
        if (Current != State.GameOver) return;

        bool retry = false;
        var kb = Keyboard.current;
        if (kb != null) retry |= kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        if (Mouse.current != null) retry |= Mouse.current.leftButton.wasPressedThisFrame;
        if (Gamepad.current != null) retry |= Gamepad.current.buttonSouth.wasPressedThisFrame;
        if (retry) StartGame();
    }

    private void StartGame()
    {
        // 残敵を一掃
        foreach (var e in Enemy.Alive.ToArray())
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
        if (Current == State.GameOver) return;
        Current = State.GameOver;
        EnemySpawner.Instance?.StopAll();
    }
}
