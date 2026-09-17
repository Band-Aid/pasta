using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1分間タイムアタックの雛形。
/// パスタを折る → 次のパスタ出現 → スコア加算 → 60秒で終了
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("タイムアタック")]
    [SerializeField] private float gameTime = 60f;
    [SerializeField] private Text timerText;
    [SerializeField] private Text resultText;

    [Header("パスタ生成")]
    [SerializeField] private SpaghettiBreaker pastaPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float nextPastaDelay = 1.5f;

    private float remainingTime;
    private bool isPlaying;
    private SpaghettiBreaker currentPasta;

    private void Start()
    {
        StartGame();
    }

    private void Update()
    {
        if (!isPlaying) return;

        remainingTime -= Time.deltaTime;
        UpdateTimerUI();

        if (remainingTime <= 0f)
            EndGame();
    }

    private void StartGame()
    {
        remainingTime = gameTime;
        isPlaying = true;
        ScoreManager.Instance?.ResetScore();
        resultText?.gameObject.SetActive(false);
        SpawnPasta();
    }

    private void SpawnPasta()
    {
        if (pastaPrefab == null || spawnPoint == null) return;

        if (currentPasta != null)
            Destroy(currentPasta.gameObject);

        currentPasta = Instantiate(pastaPrefab, spawnPoint.position, spawnPoint.rotation);
    }

    /// <summary>
    /// SpaghettiBreakerから折れた通知を受ける想定。今はScoreManagerに任せる。
    /// 次のパスタを少し遅らせて出現させる。
    /// </summary>
    public void OnPastaBroken()
    {
        if (!isPlaying) return;
        Invoke(nameof(SpawnPasta), nextPastaDelay);
    }

    private void EndGame()
    {
        isPlaying = false;
        if (resultText != null)
        {
            resultText.text = $"Time Up!\nScore: {ScoreManager.Instance?.Score ?? 0}";
            resultText.gameObject.SetActive(true);
        }
    }

    private void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = $"{Mathf.Max(0f, remainingTime):F1}";
    }
}
