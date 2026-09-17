using UnityEngine;
using UnityEngine.UI;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int Score { get; private set; }
    public int ArmLevel { get; private set; } = 1;
    private float exp = 0f;

    [Header("UI")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text armLevelText;
    [SerializeField] private GameObject mammaMiaPopup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        UpdateUI();
        if (mammaMiaPopup != null) mammaMiaPopup.SetActive(false);
    }

    /// <summary>
    /// SpaghettiBreakerから折る成功時に呼ぶ
    /// </summary>
    public void OnBreak(float breakQuality)
    {
        int points = Mathf.RoundToInt(100 * breakQuality);
        Score += points;
        exp += breakQuality;

        Debug.Log($"[折る!] +{points}点 / 合計{Score}");

        ShowMammaMia();
        UpdateUI();
        CheckArmLevelUp();

        FindObjectOfType<GameManager>()?.OnPastaBroken();
    }

    public void ResetScore()
    {
        Score = 0;
        ArmLevel = 1;
        exp = 0f;
        UpdateUI();
    }

    private void ShowMammaMia()
    {
        if (mammaMiaPopup == null) return;
        mammaMiaPopup.SetActive(true);
        CancelInvoke(nameof(HideMammaMia));
        Invoke(nameof(HideMammaMia), 1.2f);
    }

    private void HideMammaMia()
    {
        if (mammaMiaPopup != null) mammaMiaPopup.SetActive(false);
    }

    private void CheckArmLevelUp()
    {
        float threshold = ArmLevel * 5f; // 腕レベル5ごとに1段階
        if (exp >= threshold)
        {
            exp -= threshold;
            ArmLevel++;
            Debug.Log($"[腕レベルアップ!] 現在の腕レベル: {ArmLevel}");
        }
    }

    private void UpdateUI()
    {
        if (scoreText != null) scoreText.text = $"{Score}点";
        if (armLevelText != null) armLevelText.text = $"腕Lv.{ArmLevel}";
    }
}
