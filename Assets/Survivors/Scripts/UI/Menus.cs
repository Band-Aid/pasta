using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PastaSurvivors
{
    public class Offer
    {
        public enum Kind { NewWeapon, UpWeapon, NewPassive, UpPassive, Evolve, Coins, Heal }
        public Kind kind;
        public WeaponId weapon;
        public PassiveId passive;
        public string title, tag, desc, icon;
        public Color tagColor = Color.white;
    }

    public class Menus : MonoBehaviour
    {
        private RectTransform root, content;
        public readonly MenuNav Nav = new MenuNav();
        public string Current { get; private set; } = "";
        private float openedAt;
        private readonly List<RectTransform> pop = new List<RectTransform>();
        private Slider gestureSlider;
        private RectTransform gestureTrack, gestureClose;
        private bool draggingGesture;
        private System.Action closeGestureSettings;
        public bool GestureSettingsOpen => Current == "gestures";

        public void Build(Transform parent)
        {
            var canvas = UiKit.MakeCanvas("Menus", 20, parent);
            root = (RectTransform)canvas.transform;
        }

        public bool Open => Current != "";

        public void Close()
        {
            Current = "";
            gestureSlider = null;
            draggingGesture = false;
            closeGestureSettings = null;
            if (content != null) Destroy(content.gameObject);
            content = null;
            Nav.Clear();
            Nav.onCancel = null;
            pop.Clear();
        }

        private RectTransform Begin(string name, float dim)
        {
            Close();
            Current = name;
            content = UiKit.Stretch(root, name);
            if (dim > 0f)
            {
                var bg = content.gameObject.AddComponent<Image>();
                bg.sprite = UiKit.White;
                bg.color = new Color(0.05f, 0.03f, 0.04f, dim);
                bg.raycastTarget = false;
            }
            Nav.Clear();
            Nav.columns = 1;
            Nav.Arm();
            openedAt = Time.unscaledTime;
            return content;
        }

        private void Update()
        {
            if (!Open) return;
            if (GestureSettingsOpen) { UpdateGestureSettings(); return; }
            Nav.Update();
            float age = Time.unscaledTime - openedAt;
            if (age > 0.45f) return;
            for (int i = 0; i < pop.Count; i++)
            {
                if (pop[i] == null) continue;
                float k = Mathf.Clamp01((age - i * 0.05f) / 0.25f);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                pop[i].localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, e);
            }
        }

        private RectTransform Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action act, int fontSize = 34, bool enabled = true, Color? color = null)
        {
            var bg = UiKit.Img(parent, label, anchor, pos, size, color ?? new Color(0.16f, 0.12f, 0.12f, 0.92f));
            UiKit.Label(bg.transform, "Text", label, fontSize, enabled ? UiKit.Cream : new Color(0.6f, 0.55f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Nav.Add(bg.rectTransform, bg, act, enabled);
            return bg.rectTransform;
        }

        // ---------------- title ----------------

        public void ShowTitle()
        {
            var c = Begin("title", 0f);
            var shade = UiKit.Img(c, "Shade", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(900, 1200), new Color(0.06f, 0.03f, 0.03f, 0.72f), UiKit.White, new Vector2(0f, 0.5f));
            UiKit.Label(c, "Logo", "PASTA LA VISTA", 104, UiKit.Gold, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(90, -110), new Vector2(1200, 130), FontStyle.Bold, true, new Vector2(0f, 1f));
            UiKit.Label(c, "Sub", "〜 パスタ・サバイバーズ 〜", 40, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(100, -240), new Vector2(900, 60), FontStyle.Bold, true, new Vector2(0f, 1f));
            UiKit.Label(c, "Tag", "イタリア人の目の前でパスタを折って、追い払え！", 28, new Color(1f, 0.8f, 0.7f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(100, -300), new Vector2(900, 50), FontStyle.Normal, true, new Vector2(0f, 1f));
            var flag = UiKit.Rect(c, "Flag", new Vector2(0f, 1f), new Vector2(100, -360), new Vector2(240, 10), new Vector2(0f, 1f));
            UiKit.Img(flag, "G", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(80, 10), new Color(0.1f, 0.6f, 0.3f), UiKit.White, new Vector2(0f, 0.5f));
            UiKit.Img(flag, "W", new Vector2(0f, 0.5f), new Vector2(80, 0), new Vector2(80, 10), Color.white, UiKit.White, new Vector2(0f, 0.5f));
            UiKit.Img(flag, "R", new Vector2(0f, 0.5f), new Vector2(160, 0), new Vector2(80, 10), new Color(0.85f, 0.15f, 0.15f), UiKit.White, new Vector2(0f, 0.5f));

            float y = -400;
            pop.Add(Button(c, "ゲームスタート", new Vector2(0f, 1f), new Vector2(100, y), new Vector2(560, 76), () => ShowCharacters(), 36, true, new Color(0.55f, 0.16f, 0.12f, 0.95f)));
            pop.Add(Button(c, $"パワーアップ   € {SaveData.Coins}", new Vector2(0f, 1f), new Vector2(100, y - 86), new Vector2(560, 76), () => ShowShop()));
            pop.Add(Button(c, "視点：" + (SaveData.FirstPerson ? "一人称（FPS）" : "見下ろし"), new Vector2(0f, 1f), new Vector2(100, y - 172), new Vector2(560, 76), () =>
            {
                SaveData.ViewMode = SaveData.FirstPerson ? 0 : 1;
                SaveData.Save();
                ShowTitle();
                Nav.index = 2;
            }));
            pop.Add(Button(c, "遊び方", new Vector2(0f, 1f), new Vector2(100, y - 258), new Vector2(560, 76), () => ShowHowTo()));
            pop.Add(Button(c, "終了", new Vector2(0f, 1f), new Vector2(100, y - 344), new Vector2(560, 76), () => G.Game.Quit()));

            var rec = new System.Text.StringBuilder();
            for (int i = 0; i < 3; i++)
            {
                var st = GameData.Stages[i];
                string status = SaveData.Cleared[i] ? "クリア" : i < SaveData.StagesUnlocked ? (SaveData.BestTime[i] > 0 ? "ベスト " + UiKit.Clock(SaveData.BestTime[i]) : "未挑戦") : "未解放";
                rec.Append($"{i + 1}. {st.name}   {status}\n");
            }
            rec.Append($"通算撃退数  {SaveData.TotalKills}");
            UiKit.Label(c, "Records", rec.ToString(), 24, new Color(1f, 1f, 1f, 0.8f), TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(100, 70), new Vector2(700, 200), FontStyle.Normal, true, new Vector2(0f, 0f));
            UiKit.Label(c, "Hint", "移動 WASD/左クリック長押し/左スティック   ダッシュ Space/Shift/A   特殊武器 右クリック/F/Y   主武器切替 Q・E/ホイール/LB・RB   視点 V/View",
                20, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-30, 20), new Vector2(1200, 30), FontStyle.Normal, true, new Vector2(1f, 0f));
        }

        public void ShowHowTo()
        {
            var c = Begin("howto", 0.85f);
            UiKit.Label(c, "T", "遊び方", 64, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1200, 90));
            string body =
                "・移動するだけでパスタ武器が自動で発動。イタリア人は目の前でパスタを折られると逃げ出す。落とす<color=#ffd060>パスタの欠片</color>でレベルアップ。\n" +
                "・<color=#ffd060>主武器</color>（Q/E・ホイール・1〜6・LB/RB）は威力1.4倍と固有の特性付き。状況で切り替えよう。\n" +
                "・<color=#ffb060>特殊武器</color>はマップの<color=#ffb060>光の柱（台座）</color>で拾う。右クリック / F / Y で好きな時に使え、使うとクールダウン。持ち替えると元の武器は台座に残る。\n" +
                "・<color=#a0e0ff>マウス</color>：左クリック長押しでカーソルへ移動、主武器と特殊武器はカーソルへ向けて撃つ。パッドは右スティックで狙う。\n" +
                "・<color=#a0e0ff>地形を使おう</color>：壁・生垣・柱はスリッパなどの投擲を防ぐ。路地や橋に誘い込めば範囲攻撃が刺さる。\n" +
                "　回復の泉（緑の輪）は立つとHP回復、市場（橙の輪）はワイン樽がよく出る。ローマの車道とナポリの火山弾はイタリア人も巻き込む。\n" +
                "・武器をLv8にして対応するパッシブを持った状態で宝箱を開けると進化。宝箱はノンナとボスが落とす。\n" +
                "・ダッシュ（Space/Shift/A）は短時間無敵。10分生き延びるとボス。集めたコインでパワーアップ。\n" +
                "・V（View）で見下ろし/一人称を切り替え。";
            UiKit.Label(c, "B", body, 27, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(1500, 760), FontStyle.Normal);
            Button(c, "戻る", new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(360, 70), () => ShowTitle());
            Nav.onCancel = () => ShowTitle();
        }

        // ---------------- character & stage ----------------

        public void ShowCharacters()
        {
            var c = Begin("characters", 0.6f);
            UiKit.Label(c, "T", "キャラクターを選ぶ", 60, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 90));
            Nav.columns = 3;
            for (int i = 0; i < GameData.Characters.Length; i++)
            {
                var ch = GameData.Characters[i];
                int index = i;
                var card = UiKit.Img(c, ch.name, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 500, -20), new Vector2(460, 560), new Color(0.14f, 0.1f, 0.1f, 0.94f));
                var sw = UiKit.Img(card.transform, "Swatch", new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(180, 180), ch.shirt, UiKit.Round, new Vector2(0.5f, 1f));
                UiKit.Img(sw.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140), Color.white, Icons.Get(GameData.Weapon(ch.start).icon));
                UiKit.Label(card.transform, "Name", ch.name, 52, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(440, 70));
                UiKit.Label(card.transform, "Title", ch.title, 28, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -315), new Vector2(440, 40));
                UiKit.Label(card.transform, "Desc", ch.desc, 26, UiKit.Cream, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -370), new Vector2(400, 150), FontStyle.Normal);
                Nav.Add(card.rectTransform, card, () => { G.Game.SelectedCharacter = index; ShowStages(); });
                pop.Add(card.rectTransform);
            }
            Nav.index = Mathf.Clamp(G.Game.SelectedCharacter, 0, 2);
            Nav.onCancel = () => ShowTitle();
        }

        public void ShowStages()
        {
            var c = Begin("stages", 0.6f);
            UiKit.Label(c, "T", "ステージを選ぶ", 60, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 90));
            Nav.columns = 3;
            for (int i = 0; i < GameData.Stages.Length; i++)
            {
                var st = GameData.Stages[i];
                int index = i;
                bool unlocked = i < SaveData.StagesUnlocked;
                var tint = st.theme == StageTheme.Roma ? new Color(0.45f, 0.2f, 0.12f) : st.theme == StageTheme.Venezia ? new Color(0.12f, 0.25f, 0.4f) : new Color(0.45f, 0.3f, 0.1f);
                var card = UiKit.Img(c, st.name, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 540, -20), new Vector2(500, 580), unlocked ? new Color(tint.r, tint.g, tint.b, 0.95f) : new Color(0.15f, 0.15f, 0.15f, 0.9f));
                UiKit.Label(card.transform, "No", "STAGE " + (i + 1), 28, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(460, 40));
                UiKit.Label(card.transform, "Name", unlocked ? st.name : "？？？", 64, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(460, 90));
                UiKit.Label(card.transform, "Sub", unlocked ? st.sub : "前のステージをクリアで解放", 26, UiKit.Cream, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -222), new Vector2(440, 80), FontStyle.Normal);
                if (unlocked)
                {
                    string diff = new string('★', i + 1) + new string('☆', 2 - i);
                    UiKit.Label(card.transform, "Diff", "難易度  " + diff, 28, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -300), new Vector2(460, 40));
                    UiKit.Label(card.transform, "Boss", "ボス： " + st.bossName, 28, new Color(1f, 0.7f, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -350), new Vector2(460, 40));
                    string rec = SaveData.Cleared[i] ? "★ クリア済み" : SaveData.BestTime[i] > 0 ? "ベスト " + UiKit.Clock(SaveData.BestTime[i]) : "未挑戦";
                    UiKit.Label(card.transform, "Rec", rec, 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(460, 40));
                }
                Nav.Add(card.rectTransform, card, () => G.Game.StartRun(index, G.Game.SelectedCharacter), unlocked);
                pop.Add(card.rectTransform);
            }
            Nav.index = Mathf.Clamp(SaveData.StagesUnlocked - 1, 0, 2);
            Nav.onCancel = () => ShowCharacters();
        }

        // ---------------- shop ----------------

        public void ShowShop(int keepIndex = 0)
        {
            var c = Begin("shop", 0.8f);
            UiKit.Label(c, "T", "ノンナの食料庫  —  パワーアップ", 56, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1400, 80));
            UiKit.Label(c, "Coins", "所持コイン  € " + SaveData.Coins, 36, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -170), new Vector2(800, 50));
            Nav.columns = 4;
            for (int i = 0; i < GameData.Shop.Length; i++)
            {
                var def = GameData.Shop[i];
                int index = i;
                int lv = SaveData.Shop[i];
                bool max = lv >= def.maxLevel;
                int cost = SaveData.ShopCost(i);
                bool affordable = !max && SaveData.Coins >= cost;
                var card = UiKit.Img(c, def.name, new Vector2(0.5f, 0.5f), new Vector2((i % 4 - 1.5f) * 420, 90 - (i / 4) * 250), new Vector2(400, 230), new Color(0.16f, 0.12f, 0.1f, 0.95f));
                UiKit.Img(card.transform, "Icon", new Vector2(0f, 1f), new Vector2(20, -20), new Vector2(72, 72), Color.white, Icons.Get(def.icon), new Vector2(0f, 1f));
                UiKit.Label(card.transform, "Name", def.name, 34, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(110, -38), new Vector2(280, 44), FontStyle.Bold, true, new Vector2(0f, 1f));
                UiKit.Label(card.transform, "Pips", new string('●', lv) + new string('○', def.maxLevel - lv), 24, UiKit.Gold, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(110, -78), new Vector2(280, 30), FontStyle.Normal, true, new Vector2(0f, 1f));
                UiKit.Label(card.transform, "Desc", def.desc, 24, UiKit.Cream, TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(22, 60), new Vector2(360, 40), FontStyle.Normal, true, new Vector2(0f, 0f));
                UiKit.Label(card.transform, "Cost", max ? "MAX" : "€ " + cost, 30, max ? UiKit.Basil : affordable ? UiKit.Gold : new Color(0.7f, 0.4f, 0.4f), TextAnchor.MiddleRight, new Vector2(1f, 0f), new Vector2(-20, 24), new Vector2(200, 40), FontStyle.Bold, true, new Vector2(1f, 0f));
                Nav.Add(card.rectTransform, card, () =>
                {
                    if (SaveData.Shop[index] >= GameData.Shop[index].maxLevel || SaveData.Coins < SaveData.ShopCost(index))
                    {
                        G.Sfx.Play(SfxId.Hurt, 0.3f, 1.6f);
                        return;
                    }
                    SaveData.Coins -= SaveData.ShopCost(index);
                    SaveData.Shop[index]++;
                    SaveData.Save();
                    G.Sfx.Play(SfxId.Coin, 0.7f, 1f);
                    ShowShop(index);
                });
            }
            Button(c, "戻る", new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(360, 70), () => ShowTitle());
            Nav.index = keepIndex;
            if (keepIndex > 0) Nav.Arm(0.1f);
            Nav.onCancel = () => ShowTitle();
        }

        // ---------------- in-run ----------------

        public void ShowLevelUp(List<Offer> offers, int level, System.Action<Offer> choose)
        {
            var c = Begin("levelup", 0.62f);
            UiKit.Label(c, "T", "LEVEL UP!", 88, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(1200, 110));
            UiKit.Label(c, "L", "Lv " + level + "  —  ひとつ選ぼう", 32, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1200, 50));
            float top = 140 + (4 - offers.Count) * 70;
            for (int i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                var card = UiKit.Img(c, o.title, new Vector2(0.5f, 0.5f), new Vector2(0, top - i * 150), new Vector2(980, 136), new Color(0.16f, 0.11f, 0.1f, 0.96f));
                var frame = UiKit.Img(card.transform, "Frame", new Vector2(0f, 0.5f), new Vector2(18, 0), new Vector2(104, 104), new Color(0.3f, 0.22f, 0.18f, 1f), UiKit.Round, new Vector2(0f, 0.5f));
                UiKit.Img(frame.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84), Color.white, Icons.Get(o.icon));
                UiKit.Label(card.transform, "Name", o.title, 36, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(145, -12), new Vector2(560, 46), FontStyle.Bold, true, new Vector2(0f, 1f));
                UiKit.Label(card.transform, "Tag", o.tag, 28, o.tagColor, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-26, -14), new Vector2(300, 40), FontStyle.Bold, true, new Vector2(1f, 1f));
                UiKit.Label(card.transform, "Desc", o.desc, 25, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(146, -64), new Vector2(800, 64), FontStyle.Normal, true, new Vector2(0f, 1f));
                UiKit.Label(card.transform, "Key", (i + 1).ToString(), 22, new Color(1, 1, 1, 0.35f), TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-14, 8), new Vector2(40, 30), FontStyle.Bold, false, new Vector2(1f, 0f));
                var pick = o;
                Nav.Add(card.rectTransform, card, () => choose(pick));
                pop.Add(card.rectTransform);
            }
            Nav.Arm(0.35f);
        }

        public void ShowChest(List<Offer> rewards, System.Action done)
        {
            var c = Begin("chest", 0.7f);
            bool evo = rewards.Exists(r => r.kind == Offer.Kind.Evolve);
            UiKit.Label(c, "T", evo ? "進化！" : "宝箱を開けた！", 84, evo ? new Color(1f, 0.6f, 0.3f) : UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(1200, 110));
            // Reserve the bottom of the screen for OK even when a chest gives five rewards.
            float top = rewards.Count > 3 ? 220f : 150f;
            for (int i = 0; i < rewards.Count; i++)
            {
                var o = rewards[i];
                var row = UiKit.Img(c, o.title, new Vector2(0.5f, 0.5f), new Vector2(0, top - i * 120), new Vector2(900, 110), o.kind == Offer.Kind.Evolve ? new Color(0.4f, 0.2f, 0.05f, 0.96f) : new Color(0.16f, 0.11f, 0.1f, 0.96f));
                UiKit.Img(row.transform, "Icon", new Vector2(0f, 0.5f), new Vector2(24, 0), new Vector2(88, 88), Color.white, Icons.Get(o.icon), new Vector2(0f, 0.5f));
                UiKit.Label(row.transform, "Name", o.title, 36, Color.white, TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(136, -8), new Vector2(520, 44), FontStyle.Bold, true, new Vector2(0f, 1f));
                UiKit.Label(row.transform, "Tag", o.tag, 28, o.tagColor, TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-24, -12), new Vector2(280, 40), FontStyle.Bold, true, new Vector2(1f, 1f));
                UiKit.Label(row.transform, "Desc", o.desc, 24, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(136, -54), new Vector2(740, 50), FontStyle.Normal, true, new Vector2(0f, 1f));
                pop.Add(row.rectTransform);
            }
            Button(c, "OK", new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(360, 76), done);
            UiKit.Label(c, "ResumeHint", "クリック / Enter / Space / Esc で再開", 24, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0, 50), new Vector2(900, 40));
            Nav.onCancel = done;
            Nav.Arm(0.6f);
        }

        public void ShowPause(System.Action resume, System.Action quit, System.Action toggleView)
        {
            var c = Begin("pause", 0.7f);
            UiKit.Label(c, "T", "ポーズ", 80, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1200, 100));
            var p = G.Player;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#ffd060>{G.Stage.name}</color>   {UiKit.Clock(G.RunTime)}   Lv {p.Level}   撃退 {G.Enemies.Kills}\n\n");
            foreach (var w in p.Weapons)
            {
                sb.Append($"{(w == p.Main ? "<color=#ffd060>[主]</color> " : "")}{w.DisplayName}  Lv{w.level}{(w.evolved ? " ★" : "")}\n");
                if (w == p.Main) sb.Append($"<size=20>  {w.def.mainTrait}</size>\n");
            }
            sb.Append("\n");
            foreach (var id in p.Passives) sb.Append($"{GameData.Passive(id).name}  Lv{p.PassiveLevels[(int)id]}\n");
            UiKit.Label(c, "Inv", sb.ToString(), 28, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(-260, 40), new Vector2(620, 600), FontStyle.Normal);
            string stats =
                $"最大HP  {p.MaxHp:0}\nパワー  {p.Might * 100:0}%\n範囲  {p.AreaMul * 100:0}%\nクールダウン  {p.CooldownMul * 100:0}%\n" +
                $"発射数  +{p.AmountBonus}\n移動速度  {p.MoveSpeed:0.0}\n回収範囲  {p.MagnetRadius:0.0}m\n成長  {p.GrowthMul * 100:0}%\n装甲  {p.Armor:0}\n復活  {p.Revivals}";
            UiKit.Label(c, "Stats", stats, 28, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(360, 40), new Vector2(400, 600), FontStyle.Normal);
            Button(c, "再開", new Vector2(0.5f, 0f), new Vector2(-480, 110), new Vector2(300, 76), resume);
            Button(c, "視点：" + (SaveData.FirstPerson ? "一人称" : "見下ろし"), new Vector2(0.5f, 0f), new Vector2(-160, 110), new Vector2(300, 76), toggleView);
            Button(c, "身振り設定（再開）", new Vector2(0.5f, 0f), new Vector2(160, 110), new Vector2(300, 76), () => G.Game.OpenGestureSettings(), 28);
            Button(c, "タイトルへ戻る", new Vector2(0.5f, 0f), new Vector2(480, 110), new Vector2(300, 76), quit);
            Nav.columns = 4;
            Nav.onCancel = resume;
        }

        public void ShowGestureSettings(System.Action close)
        {
            var c = Begin("gestures", 0f);
            closeGestureSettings = close;
            var panel = UiKit.Img(c, "GestureSettings", new Vector2(1f, 1f), new Vector2(-24, -118), new Vector2(560, 310), UiKit.Ink);
            var label = UiKit.Label(panel.transform, "GesturePercent", "", 32, UiKit.Gold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(520, 48));
            UiKit.Label(panel.transform, "Scope", "徒歩の町の人（乗り物・ボスを除く）", 21, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -74), new Vector2(520, 32));
            gestureTrack = UiKit.Rect(panel.transform, "GestureSlider", new Vector2(0.5f, 1f), new Vector2(0, -116), new Vector2(480, 48));
            UiKit.Img(gestureTrack, "Track", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 10), new Color(0.35f, 0.3f, 0.25f));
            var fill = UiKit.Fill(UiKit.Img(gestureTrack, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 10), UiKit.Gold));
            var handle = UiKit.Img(gestureTrack, "Handle", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24, 36), UiKit.Cream, null, new Vector2(0.5f, 0.5f));
            gestureSlider = gestureTrack.gameObject.AddComponent<Slider>();
            gestureSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            gestureSlider.transition = Selectable.Transition.None;
            gestureSlider.minValue = 0f;
            gestureSlider.maxValue = 100f;
            gestureSlider.wholeNumbers = true;
            gestureSlider.fillRect = fill.rectTransform;
            gestureSlider.handleRect = handle.rectTransform;
            gestureSlider.targetGraphic = handle;
            gestureSlider.SetValueWithoutNotify(G.Enemies.GesturePercent);
            label.text = $"身振り発生率  {gestureSlider.value:0}%";
            gestureSlider.onValueChanged.AddListener(value =>
            {
                G.Enemies.GesturePercent = value;
                label.text = $"身振り発生率  {value:0}%";
            });
            UiKit.Label(panel.transform, "Minimum", "0%", 20, UiKit.Cream, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(40, -164), new Vector2(90, 30));
            UiKit.Label(panel.transform, "Maximum", "100%", 20, UiKit.Cream, TextAnchor.MiddleRight,
                new Vector2(1f, 1f), new Vector2(-40, -164), new Vector2(90, 30));
            UiKit.Label(panel.transform, "Hint", "ドラッグ / ← → で調整・次の身振りから反映\n調整中もゲームは進行します", 20, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0, -198), new Vector2(520, 50), FontStyle.Normal);
            gestureClose = UiKit.Img(panel.transform, "Close", new Vector2(0.5f, 0f), new Vector2(0, 12), new Vector2(360, 44), new Color(0.35f, 0.2f, 0.12f)).rectTransform;
            UiKit.Label(gestureClose, "Text", "閉じる（G / Esc / B）", 24, UiKit.Cream, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350, 40));
        }

        private void UpdateGestureSettings()
        {
            if (Time.unscaledTime - openedAt < 0.25f) return;
            // The existing menus poll input directly and do not use an EventSystem.
            // Keep the slider on the same path, including drag capture outside the track.
            var mouse = Controls.MousePosition;
            if (Controls.MouseClicked && RectTransformUtility.RectangleContainsScreenPoint(gestureTrack, mouse, null))
                draggingGesture = true;
            if (!Controls.MouseMoveHeld) draggingGesture = false;
            if (draggingGesture && RectTransformUtility.ScreenPointToLocalPointInRectangle(gestureTrack, mouse, null, out var local))
                gestureSlider.value = Mathf.InverseLerp(gestureTrack.rect.xMin, gestureTrack.rect.xMax, local.x) * 100f;
            gestureSlider.value += Controls.Nav().x;
            if (Controls.Submit() || (Controls.MouseClicked && RectTransformUtility.RectangleContainsScreenPoint(gestureClose, mouse, null)))
                closeGestureSettings?.Invoke();
        }

        public void ShowResult(bool cleared, int coinsEarned, Dictionary<int, float> damage, System.Action retry, System.Action next, System.Action title)
        {
            var c = Begin("result", 0.86f);
            G.Hud.SetVisible(false);
            G.Hud.ClearTransient();
            UiKit.Label(c, "T", cleared ? "ステージクリア！" : "ゲームオーバー", 92, cleared ? UiKit.Gold : new Color(1f, 0.45f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(1400, 120));
            UiKit.Label(c, "S", cleared ? $"{G.Stage.name}のイタリア人は、もうパスタの話をしない。" : "イタリア人の怒りに呑まれてしまった…", 32, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0, -205), new Vector2(1400, 50), FontStyle.Normal);
            var p = G.Player;
            string summary = $"ステージ  {G.Stage.name}\n生存時間  {UiKit.Clock(G.RunTime)}\nレベル  {p.Level}\n撃退数  {G.Enemies.Kills}\n獲得コイン  € {coinsEarned}";
            UiKit.Label(c, "Sum", summary, 34, Color.white, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(-330, 90), new Vector2(520, 400), FontStyle.Normal);
            var sb = new System.Text.StringBuilder("<color=#ffd060>武器別ダメージ</color>\n");
            foreach (var w in p.Weapons)
            {
                damage.TryGetValue(w.Slot, out var d);
                sb.Append($"{w.DisplayName}  {d:#,0}\n");
            }
            UiKit.Label(c, "Dmg", sb.ToString(), 28, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(330, 90), new Vector2(600, 400), FontStyle.Normal);
            Nav.columns = 3;
            Button(c, "リトライ", new Vector2(0.5f, 0f), new Vector2(-420, 110), new Vector2(380, 76), retry);
            Button(c, "次のステージへ", new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(380, 76), next, 34, next != null);
            Button(c, "タイトルへ", new Vector2(0.5f, 0f), new Vector2(420, 110), new Vector2(380, 76), title);
            Nav.index = next != null ? 1 : 0;
            Nav.Arm(0.8f);
        }
    }
}
