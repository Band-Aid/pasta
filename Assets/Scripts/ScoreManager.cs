using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スコア・撃破数・コンボ・腕レベルを管理。1回の衝撃波の撃破数でボーナスが伸びる。
/// UIの数値表示はHudが行い、ここは状態管理と「MAMMA MIA!」ポップアップを担う。
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int Score { get; private set; }
    public int Kills { get; private set; }
    public int BestCombo { get; private set; }
    public int ArmLevel { get; private set; } = 1;
    private float exp;

    [Header("ポップアップ")]
    [SerializeField] private GameObject popup;
    [SerializeField] private Text popupText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (popup != null) popup.SetActive(false);
    }

    /// <summary>PastaHandから折る成功時に呼ぶ。kills=この衝撃波の撃破数、scoreMul=狙い撃ち等のボーナス倍率</summary>
    public void OnBreak(int kills, int tier, float quality, float scoreMul = 1f)
    {
        exp += quality;
        CheckArmLevelUp();

        if (kills <= 0)
        {
            ShowPopup("空振り…", new Color(1f, 1f, 1f, 0.6f), 0.7f);
            return;
        }

        Kills += kills;
        if (kills > BestCombo) BestCombo = kills;

        // 撃破数が多いほどコンボ倍率が伸びる（1体=1.0、2体=1.5、3体=2.0…）×狙い撃ちボーナス
        float comboMul = 1f + (kills - 1) * 0.5f;
        int points = Mathf.RoundToInt(kills * (60 + tier * 40) * comboMul * scoreMul);
        Score += points;

        if (kills >= 5) ShowPopup($"MAMMA MIA!!! ×{kills}", new Color(1f, 0.15f, 0.1f), 1.2f);
        else if (kills >= 3) ShowPopup($"MAMMA MIA! ×{kills}", new Color(1f, 0.45f, 0.1f), 1.1f);
        else if (kills == 2) ShowPopup("Doppio! ×2", new Color(0.5f, 1f, 0.5f), 1f);
        else ShowPopup("Colpito!", Color.white, 0.9f);
    }

    public void ResetScore()
    {
        Score = 0;
        Kills = 0;
        BestCombo = 0;
        ArmLevel = 1;
        exp = 0f;
        if (popup != null) popup.SetActive(false);
    }

    private void ShowPopup(string text, Color color, float scale)
    {
        if (popup == null) return;
        if (popupText != null)
        {
            popupText.text = text;
            popupText.color = color;
            popupText.transform.localScale = Vector3.one * scale;
        }
        popup.SetActive(true);
        CancelInvoke(nameof(HidePopup));
        Invoke(nameof(HidePopup), 1.1f);
    }

    private void HidePopup()
    {
        if (popup != null) popup.SetActive(false);
    }

    private void CheckArmLevelUp()
    {
        float threshold = ArmLevel * 5f;
        if (exp >= threshold)
        {
            exp -= threshold;
            ArmLevel++;
        }
    }
}
