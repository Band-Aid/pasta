using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD描画。シングルトンをポーリングしてHP/スコア/撃破/ウェーブ/折り精度メーターを更新。
/// ウェーブ開始バナーとゲームオーバー画面もここが出す。
/// </summary>
public class Hud : MonoBehaviour
{
    [Header("ステータス")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text waveText;

    [Header("折り精度メーター")]
    [SerializeField] private Text meterText;

    [Header("弾種/モード")]
    [SerializeField] private Text ammoText;

    [Header("中央表示")]
    [SerializeField] private Text centerText;
    [SerializeField] private Text crosshair;

    private int lastWave;
    private float waveBannerUntil;

    private void Update()
    {
        UpdateHealth();
        UpdateStats();
        UpdateMeter();
        UpdateAmmo();
        UpdateCenter();
    }

    private void UpdateAmmo()
    {
        if (ammoText == null) return;
        var ph = PastaHand.Instance;
        if (ph == null) { ammoText.text = ""; return; }

        string name; Color col;
        switch (ph.Type)
        {
            case PastaType.Penne: name = "ペンネ 貫通"; col = new Color(0.95f, 0.7f, 0.4f); break;
            case PastaType.Lasagna: name = "ラザニア 広範囲"; col = new Color(0.95f, 0.85f, 0.5f); break;
            default: name = "スパゲッティ"; col = new Color(0.95f, 0.8f, 0.45f); break;
        }
        string mode = ph.Aimed ? "狙撃" : "円";
        ammoText.text = $"{name}  [{mode}]";
        ammoText.color = ph.Aimed ? new Color(1f, 0.5f, 0.3f) : col;
    }

    private void UpdateHealth()
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        float ratio = Mathf.Clamp01(p.Health / p.MaxHealth);
        if (healthFill != null)
        {
            healthFill.fillAmount = ratio;
            healthFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.8f, 0.3f), ratio);
        }
        if (healthText != null) healthText.text = $"HP {Mathf.CeilToInt(p.Health)}";
    }

    private void UpdateStats()
    {
        var s = ScoreManager.Instance;
        if (s != null)
        {
            if (scoreText != null) scoreText.text = $"{s.Score}";
            if (killsText != null) killsText.text = $"撃破 {s.Kills}";
        }
        var sp = EnemySpawner.Instance;
        if (sp != null && waveText != null)
        {
            waveText.text = $"WAVE {sp.Wave}";
            if (sp.Wave != lastWave)
            {
                lastWave = sp.Wave;
                if (sp.Wave > 0) waveBannerUntil = Time.time + 2f;
            }
        }
    }

    private void UpdateMeter()
    {
        if (meterText == null) return;
        var pasta = PastaHand.Instance != null ? PastaHand.Instance.Current : null;
        if (pasta == null || pasta.IsBroken) { meterText.text = ""; return; }

        float diff = pasta.CurrentDiff;
        float th = pasta.BreakAngleThreshold;
        float d3 = th + 0.1f * (180f - th);
        float d2 = th + 0.3f * (180f - th);

        if (diff < th)
        {
            meterText.text = $"Δ{diff:F0}°";
            meterText.color = new Color(1f, 1f, 1f, 0.45f);
        }
        else if (diff <= d3) { meterText.text = $"Δ{diff:F0}°  PERFETTO ×3"; meterText.color = new Color(1f, 0.78f, 0.15f); }
        else if (diff <= d2) { meterText.text = $"Δ{diff:F0}°  BUONO ×2"; meterText.color = new Color(0.5f, 1f, 0.5f); }
        else { meterText.text = $"Δ{diff:F0}°  ×1"; meterText.color = Color.white; }
    }

    private void UpdateCenter()
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.Current == GameManager.State.GameOver)
        {
            if (crosshair != null) crosshair.enabled = false;
            if (centerText != null)
            {
                var s = ScoreManager.Instance;
                centerText.text = $"GAME OVER\n撃破 {s?.Kills ?? 0}  /  SCORE {s?.Score ?? 0}\n最大コンボ ×{s?.BestCombo ?? 0}\n\n[R] RISTART";
                centerText.color = new Color(1f, 0.3f, 0.2f);
                centerText.fontSize = 46;
                centerText.enabled = true;
            }
            return;
        }

        if (crosshair != null) crosshair.enabled = true;
        if (centerText != null)
        {
            if (Time.time < waveBannerUntil)
            {
                centerText.text = $"WAVE {lastWave}";
                centerText.color = new Color(1f, 0.9f, 0.7f);
                centerText.fontSize = 72;
                centerText.enabled = true;
            }
            else centerText.enabled = false;
        }
    }
}
