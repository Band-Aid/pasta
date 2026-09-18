using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int BestCombo { get; private set; }
    public int ArmLevel => 1 + PerfectStreak / 3;
    public int Combo { get; private set; }
    public int PerfectStreak { get; private set; }
    public int BestStreak { get; private set; }
    public int DominoKills { get; private set; }
    public int BestDomino { get; private set; }
    public float ComboRemaining => Mathf.Clamp01((comboUntil - Time.time) / 4.5f);
    public string Feedback { get; private set; }
    public Color FeedbackColor { get; private set; }
    public float FeedbackRemaining => Mathf.Clamp01((feedbackUntil - Time.unscaledTime) / 1.1f);
    public float Multiplier => 1f + Mathf.Min(Combo, 8) * 0.25f;
    [SerializeField] private GameObject popup;
    [SerializeField] private Text popupText;
    private float comboUntil, feedbackUntil;
    private bool inputHint;

    private void Awake() => Instance = this;
    private void Start() { if (popup != null) popup.SetActive(false); }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        if (Combo > 0 && Time.time > comboUntil) BreakCombo();
    }

    public void OnBreak(int kills, int tier, float quality, float scoreMul = 1f, int closeCalls = 0)
    {
        if (kills <= 0)
        {
            BreakCombo();
            Show(PastaHand.Instance != null && PastaHand.Instance.Overbent ? "溜めすぎ！" : "届かない！  もう少し引きつけよう", new Color(1f, 0.68f, 0.45f));
            inputHint = true;
            return;
        }
        Combo++;
        comboUntil = Time.time + 4.5f;
        PerfectStreak = tier == 3 ? PerfectStreak + 1 : 0;
        BestStreak = Mathf.Max(BestStreak, Combo);
        Kills += kills;
        BestCombo = Mathf.Max(BestCombo, kills);
        float groupBonus = 1f + (kills - 1) * 0.4f;
        int points = Mathf.RoundToInt(kills * (60 + tier * 40) * groupBonus * Multiplier * scoreMul) + closeCalls * 150;
        Score += points;
        string title = tier == 3 ? "PERFETTO!" : tier == 2 ? "BUONO!" : "CRACK!";
        if (kills >= 3) title = "MAMMA MIA!";
        string counter = closeCalls > 0 ? "  ギリギリ撃退！" : "";
        Show($"{title}  +{points:N0}\n{kills}人まとめて撃退{counter}",
            tier == 3 ? new Color(1f, 0.83f, 0.35f) : new Color(0.85f, 1f, 0.83f));
    }

    public void TooEarly()
    {
        Show("まだ折れない  →  金色で離す", new Color(1f, 0.92f, 0.72f));
        inputHint = true;
    }

    public void OnDomino(DominoShot shot)
    {
        Kills++;
        DominoKills++;
        BestDomino = Mathf.Max(BestDomino, shot.Hits);
        BestCombo = Mathf.Max(BestCombo, shot.Hits);
        int points = 200 + Mathf.Min(shot.Collisions, 12) * 100;
        Score += points;
        comboUntil = Time.time + 4.5f;
        Show($"{shot.Hits} 人連鎖！  +{points:N0}\n敵をぶつけて DOMINO!", new Color(1f, 0.83f, 0.35f));
    }

    public void Announce(string message) => Show(message, new Color(0.65f, 1f, 0.85f));

    public void ClearInputHint()
    {
        if (inputHint) feedbackUntil = 0f;
        inputHint = false;
    }

    public void BreakCombo()
    {
        Combo = 0;
        PerfectStreak = 0;
        comboUntil = 0f;
    }

    public void WaveClear(int wave)
    {
        comboUntil = Time.time + 7f;
        int bonus = wave * 200;
        Score += bonus;
        Show($"ROUND CLEAR  +{bonus:N0}\nひと息。HP +15", new Color(0.65f, 1f, 0.78f));
    }

    private void Show(string message, Color color)
    {
        inputHint = false;
        Feedback = message;
        FeedbackColor = color;
        feedbackUntil = Time.unscaledTime + 1.5f;
        // Hud presents this in one consistent location rather than stacking multiple banners.
        if (popup != null) popup.SetActive(false);
    }

    public void ResetScore()
    {
        Score = Kills = BestCombo = BestStreak = 0;
        DominoKills = BestDomino = 0;
        BreakCombo();
        Feedback = "";
        feedbackUntil = 0f;
        if (popup != null) popup.SetActive(false);
    }
}
