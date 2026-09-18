using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable timing, reachable groups, chain timer, and off-screen threats.</summary>
public class Hud : MonoBehaviour
{
    [SerializeField] private Image healthFill;
    [SerializeField] private Text healthText, scoreText, killsText, waveText, meterText, ammoText, centerText, crosshair;
    private Text subtitle, targets, feedback, combo, weaponHelp, behind, left, right, dashHelp;
    private Text[] weapons;
    private Image marker, perfectZone, goodZone, chargeFill, damageFlash;
    private RectTransform gauge;
    private GameObject endPanel;
    private Font font;
    private Transform root;
    private readonly Color gold = new Color(1f, 0.8f, 0.34f);
    private readonly Color cream = new Color(1f, 0.95f, 0.84f);
    private readonly Color muted = new Color(0.7f, 0.74f, 0.7f);
    private readonly Color ink = new Color(0.06f, 0.095f, 0.095f, 0.87f);
    private readonly Color coral = new Color(1f, 0.38f, 0.27f);

    private void Start()
    {
        var canvas = healthFill != null ? healthFill.GetComponentInParent<Canvas>() : FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        foreach (Transform child in canvas.transform) child.gameObject.SetActive(false);
        font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic", "Meiryo", "Noto Sans CJK JP", "Arial" }, 32);
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var layer = new GameObject("Snap HUD", typeof(RectTransform));
        root = layer.transform;
        root.SetParent(canvas.transform, false);
        var rt = (RectTransform)root;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        BuildHud();
    }

    private void BuildHud()
    {
        Vector2 tl = new Vector2(0, 1), tr = Vector2.one, bc = new Vector2(0.5f, 0), tc = new Vector2(0.5f, 1);
        Box("Identity panel", root, tl, new Vector2(24, -24), new Vector2(370, 108), ink);
        Box("Green", root, tl, new Vector2(24, -24), new Vector2(123, 4), new Color(0.27f, 0.65f, 0.48f));
        Box("White", root, tl, new Vector2(147, -24), new Vector2(124, 4), cream);
        Box("Red", root, tl, new Vector2(271, -24), new Vector2(123, 4), coral);
        Label("Title", root, "PASTA LA VISTA", 32, tl, new Vector2(44, -40), new Vector2(340, 42), gold, TextAnchor.MiddleLeft);
        Label("Tagline", root, "折って、ぶつけて、大連鎖。", 19, tl, new Vector2(46, -88), new Vector2(330, 28), cream, TextAnchor.MiddleLeft);
        scoreText = Label("Score", root, "000000", 48, tr, new Vector2(-30, -27), new Vector2(350, 62), cream, TextAnchor.MiddleRight);
        killsText = Label("Kills", root, "", 20, tr, new Vector2(-34, -94), new Vector2(350, 30), gold, TextAnchor.MiddleRight);
        waveText = Label("Wave", root, "", 28, tc, new Vector2(0, -32), new Vector2(450, 42), cream);
        subtitle = Label("Wave subtitle", root, "", 18, tc, new Vector2(0, -80), new Vector2(600, 32), muted);

        var hpPanel = Box("Health panel", root, Vector2.zero, new Vector2(24, 63), new Vector2(306, 98), ink);
        healthText = Label("Health", hpPanel.transform, "", 22, tl, new Vector2(20, -12), new Vector2(260, 32), cream, TextAnchor.MiddleLeft);
        var hpBg = Box("Health track", hpPanel.transform, Vector2.zero, new Vector2(20, 20), new Vector2(266, 10), new Color(1f, 1f, 1f, 0.12f));
        healthFill = Box("Health fill", hpBg.transform, Vector2.zero, Vector2.zero, new Vector2(266, 10), gold);

        var ammoPanel = Box("Pasta menu", root, new Vector2(1, 0), new Vector2(-24, 63), new Vector2(300, 155), ink);
        weapons = new Text[3];
        string[] names = { "1  SPAGHETTI   押し出す", "2  PENNE          狙い撃ち", "3  LASAGNA     全周散らし" };
        for (int i = 0; i < 3; i++)
            weapons[i] = Label("Pasta " + i, ammoPanel.transform, names[i], 21, tl, new Vector2(18, -12 - i * 32), new Vector2(270, 30), muted, TextAnchor.MiddleLeft);
        weaponHelp = Label("Aim mode", ammoPanel.transform, "", 17, Vector2.zero, new Vector2(18, 10), new Vector2(270, 27), cream, TextAnchor.MiddleLeft);
        dashHelp = Label("Dash status", root, "", 19, Vector2.zero, new Vector2(28, 172), new Vector2(360, 32), gold, TextAnchor.MiddleLeft);

        var track = Box("Timing background", root, bc, new Vector2(0, 116), new Vector2(400, 18), new Color(0.05f, 0.07f, 0.07f, 0.95f));
        gauge = track.rectTransform;
        goodZone = Box("Good window", track.transform, Vector2.zero, new Vector2(400f * 86f / 150f, 3), new Vector2(400f * 28f / 150f, 12), new Color(0.36f, 0.6f, 0.44f));
        perfectZone = Box("Perfect window", track.transform, Vector2.zero, new Vector2(160, 3), new Vector2(400f * 26f / 150f, 12), gold);
        chargeFill = Box("Tension", track.transform, Vector2.zero, new Vector2(0, -6), new Vector2(0, 2), cream);
        marker = Box("Release needle", track.transform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(4, 30), cream);
        meterText = Label("Timing instruction", root, "", 25, bc, new Vector2(0, 150), new Vector2(700, 40), gold);
        var timingOutline = meterText.gameObject.AddComponent<Outline>();
        timingOutline.effectColor = new Color(0.025f, 0.04f, 0.03f, 0.85f);
        timingOutline.effectDistance = new Vector2(1.5f, -1.5f);
        targets = Label("Reach", root, "", 20, bc, new Vector2(0, 77), new Vector2(700, 30), cream);
        crosshair = Label("Reticle", root, "+", 28, Vector2.one * 0.5f, Vector2.zero, new Vector2(60, 60), cream);
        feedback = Label("Snap feedback", root, "", 36, Vector2.one * 0.5f, new Vector2(0, 178), new Vector2(1100, 120), gold);
        feedback.gameObject.AddComponent<Outline>().effectColor = new Color(0.04f, 0.04f, 0.02f, 0.8f);
        combo = Label("Chain", root, "", 26, tr, new Vector2(-34, -142), new Vector2(420, 45), gold, TextAnchor.MiddleRight);

        behind = Label("Behind warning", root, "", 23, bc, new Vector2(0, 230), new Vector2(550, 40), coral);
        left = Label("Left warning", root, "", 23, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(230, 60), coral, TextAnchor.MiddleLeft);
        right = Label("Right warning", root, "", 23, new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(230, 60), coral, TextAnchor.MiddleRight);
        var controlBand = Box("Control band", root, bc, Vector2.zero, new Vector2(0, 48), new Color(0.04f, 0.06f, 0.06f, 0.8f));
        controlBand.rectTransform.anchorMin = Vector2.zero;
        controlBand.rectTransform.anchorMax = new Vector2(1, 0);
        Label("Controls", root, "長押し → 離して発射   /   右クリック・E 挑発   /   WASD 移動   /   Shift ダッシュ   /   Q 切替   /   Esc ポーズ", 18,
            bc, new Vector2(0, 20), new Vector2(1750, 30), cream);
        damageFlash = Box("Damage flash", root, Vector2.zero, Vector2.zero, Vector2.zero, Color.clear);
        damageFlash.rectTransform.anchorMax = Vector2.one;
        damageFlash.rectTransform.offsetMin = damageFlash.rectTransform.offsetMax = Vector2.zero;
        endPanel = Box("End panel", root, Vector2.one * 0.5f, Vector2.zero, new Vector2(940, 410), ink).gameObject;
        centerText = Label("End message", endPanel.transform, "", 38, Vector2.one * 0.5f, Vector2.zero, new Vector2(900, 380), cream);
        endPanel.SetActive(false);
    }

    private void Update()
    {
        if (root == null) return;
        var player = PlayerController.Instance;
        var score = ScoreManager.Instance;
        var hand = PastaHand.Instance;
        var spawner = EnemySpawner.Instance;
        bool ended = GameManager.Instance != null && GameManager.Instance.HasEnded;
        if (player != null)
        {
            float hp = Mathf.Clamp01(player.Health / player.MaxHealth);
            healthFill.rectTransform.sizeDelta = new Vector2(266f * hp, 10);
            healthFill.color = hp < 0.3f ? coral : gold;
            healthText.text = $"HP  {Mathf.CeilToInt(player.Health):000} / {player.MaxHealth:0}";
            damageFlash.color = new Color(0.9f, 0.14f, 0.04f, player.DamageFlash * 0.22f);
            dashHelp.text = player.DashCooldown > 0f ? $"ダッシュ  あと {player.DashCooldown:0.0} 秒" : "Shift / RB  ダッシュ READY";
            dashHelp.color = player.DashCooldown > 0f ? muted : gold;
        }
        if (score != null)
        {
            scoreText.text = score.Score.ToString("D6");
            killsText.text = $"{score.Kills} 人撃退   /   衝突で {score.DominoKills} 人";
            combo.text = score.BestDomino > 0 ? $"BEST  {score.BestDomino} 人連鎖" : "敵を奥の敵にぶつけよう";
            feedback.text = score.FeedbackRemaining > 0 ? score.Feedback : "";
            Color c = score.FeedbackColor; c.a = Mathf.Clamp01(score.FeedbackRemaining * 4f);
            feedback.color = c;
            feedback.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, score.FeedbackRemaining - 0.8f) * 0.3f);
        }
        if (spawner != null)
        {
            waveText.text = $"ROUND {Mathf.Max(1, spawner.Wave)} / {EnemySpawner.TotalRounds}";
            subtitle.text = spawner.Intermission ? "ラウンドクリア！" : $"{spawner.RoundName}  /  あと {Enemy.Alive.Count + spawner.RemainingToSpawn} 人";
        }
        if (hand != null) UpdateHand(hand);
        UpdateThreats(player);
        bool paused = !ended && GameManager.Instance != null && GameManager.Instance.IsPaused;
        endPanel.SetActive(ended || paused);
        centerText.fontSize = ended ? 38 : 30;
        if (ended)
        {
            bool won = GameManager.Instance.Current == GameManager.State.Won;
            centerText.text = $"{(won ? "BRAVISSIMO!  全ラウンド制覇" : "BASTA!  もう一度挑戦")}\n\nSCORE  {score?.Score ?? 0:N0}    /    {score?.Kills ?? 0} 人撃退\n最大 {score?.BestDomino ?? 0} 人連鎖    ・    衝突で {score?.DominoKills ?? 0} 人撃退\n\nR / クリック / A  もう一度";
        }
        else if (paused)
            centerText.text = "ひと休み\n\n手前の敵を飛ばし、奥の敵にぶつけよう。\n右クリック / LB で集める。Shift / RB で回り込む。\n金色なら重い敵も一撃。3ラウンドでクリア！\n\nクリック / A  で再開";
        crosshair.enabled = !ended && !paused;
    }

    private void UpdateHand(PastaHand hand)
    {
        for (int i = 0; i < weapons.Length; i++) weapons[i].color = (int)hand.Type == i ? gold : muted;
        weaponHelp.text = hand.LureCooldown > 0f ? $"挑発  あと {hand.LureCooldown:0.0} 秒" : "右クリック / LB  挑発 READY";
        weaponHelp.color = hand.LureCooldown > 0f ? muted : gold;
        float progress = hand.Strain;
        var pasta = hand.Current;
        gauge.gameObject.SetActive(pasta != null);
        if (pasta != null)
        {
            perfectZone.rectTransform.anchoredPosition = new Vector2(400f * pasta.BreakAngleThreshold / 150f, 3);
            perfectZone.rectTransform.sizeDelta = new Vector2(400f * (pasta.PerfectEnd - pasta.BreakAngleThreshold) / 150f, 12);
            goodZone.rectTransform.anchoredPosition = new Vector2(400f * pasta.PerfectEnd / 150f, 3);
            marker.rectTransform.anchoredPosition = new Vector2(400f * progress, 0);
            chargeFill.rectTransform.sizeDelta = new Vector2(400f * progress, 2);
            float quality = pasta.QualityAt(pasta.CurrentDiff);
            bool perfect = hand.Charging && quality >= 0.9f;
            marker.color = perfect ? gold : cream;
            meterText.color = perfect ? gold : cream;
            meterText.text = !hand.Charging ? "長押しで曲げる  →  金色で離す" : quality < 0 ? "もう少し曲げる…" : perfect ? "いま離す！  PERFETTO" : quality >= 0.7f ? "離そう！  BUONO" : "折れそう！  離して！";
            targets.text = hand.TargetsInReach > 0 ? $"{hand.TargetsInReach} 人を発射できる → 奥へぶつけよう" : "近づいて列の手前を狙う  /  右クリックで集める";
            targets.color = hand.TargetsInReach >= 3 ? gold : cream;
        }
        else
        {
            meterText.text = hand.Overbent ? "溜めすぎ！  次は金色で離そう" : "次のパスタ…";
            meterText.color = hand.Overbent ? coral : muted;
            targets.text = "";
        }
        crosshair.color = hand.TargetsInReach > 0 ? gold : cream;
    }

    private void UpdateThreats(PlayerController player)
    {
        if (player == null) return;
        int rear = 0, l = 0, r = 0;
        foreach (var enemy in Enemy.Alive)
        {
            if (enemy == null) continue;
            Vector3 delta = enemy.transform.position - player.transform.position;
            if (delta.sqrMagnitude > 81f) continue;
            Vector3 local = player.transform.InverseTransformDirection(delta);
            if (local.z < -1f) rear++;
            else if (local.x < -Mathf.Abs(local.z)) l++;
            else if (local.x > Mathf.Abs(local.z)) r++;
        }
        behind.text = rear > 0 ? $"↓ 背後に {rear} 人！" : "";
        left.text = l > 0 ? $"← 左に {l} 人" : "";
        right.text = r > 0 ? $"右に {r} 人 →" : "";
    }

    private static RectTransform Rect(GameObject go, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Image Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Rect(go, parent, anchor, position, size);
        var image = go.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    private Text Label(string name, Transform parent, string message, int size, Vector2 anchor, Vector2 position, Vector2 bounds, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Rect(go, parent, anchor, position, bounds);
        var text = go.AddComponent<Text>();
        text.font = font; text.fontSize = size; text.text = message;
        text.color = color; text.alignment = alignment; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontStyle = FontStyle.Bold;
        return text;
    }
}
