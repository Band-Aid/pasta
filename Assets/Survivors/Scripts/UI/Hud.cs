using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PastaSurvivors
{
    public class Hud : MonoBehaviour
    {
        public FloatingText Floating { get; private set; }
        private RectTransform root;
        private Image xpFill, hpFill, dashFill, bossFill, bannerBg;
        private Text levelText, timerText, killsText, coinsText, bossName, bannerText, titleText, subText, fpsText, threatText;
        private RectTransform hpRoot, bossRoot, bannerRoot, titleRoot;
        private readonly Image[] weaponIcons = new Image[6], passiveIcons = new Image[6];
        private readonly Text[] weaponLevels = new Text[6], passiveLevels = new Text[6];
        private readonly Image[] weaponFrames = new Image[6];
        private float bannerT, titleT, titleDuration = 3.2f, fpsAcc;
        private int fpsFrames;
        public bool ShowFps;
        private readonly Queue<(string, Color)> bannerQueue = new Queue<(string, Color)>();
        private Image mainFrame, crossDot, crossRing, radarBg;
        private Text mainText, switchHint, mainTag;
        private RectTransform radar;
        private readonly List<Image> radarDots = new List<Image>();
        private float mainT;
        private bool firstPerson;

        // Special weapon slot, minimap and off-screen pointers.
        private Image specialFrame, specialIcon, specialCd;
        private Text specialName, specialKey;
        private RectTransform minimap;
        private RawImage minimapImage;
        private Texture2D minimapTex;
        private readonly List<Image> mapDots = new List<Image>();
        private Image mapPlayer;
        private RectTransform districtRoot;
        private Text districtName, districtHint;
        private class Pointer { public RectTransform rt; public Image arrow, icon; }
        private readonly List<Pointer> pointers = new List<Pointer>();

        public void Build(Transform parent)
        {
            var canvas = UiKit.MakeCanvas("HUD", 10, parent);
            root = (RectTransform)canvas.transform;
            var ft = UiKit.MakeCanvas("Floating", 5, parent);
            Floating = ft.gameObject.AddComponent<FloatingText>();
            Floating.Init((RectTransform)ft.transform);

            // XP bar across the top.
            var xpBg = UiKit.Img(root, "XP", new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(1900, 26), UiKit.Ink, UiKit.Pill);
            xpFill = UiKit.Fill(UiKit.Img(xpBg.transform, "Fill", new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(1892, 18), new Color(0.4f, 0.75f, 1f), null, new Vector2(0f, 0.5f)));
            levelText = UiKit.Label(xpBg.transform, "Level", "Lv 1", 22, Color.white, TextAnchor.MiddleRight, new Vector2(1f, 0.5f), new Vector2(-14, 0), new Vector2(200, 30));

            timerText = UiKit.Label(root, "Timer", "00:00", 46, Color.white, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(400, 60));
            threatText = UiKit.Label(root, "Threat", "", 18, new Color(1f, 0.8f, 0.75f, 0.8f), TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0, -88), new Vector2(400, 26), FontStyle.Normal);
            killsText = UiKit.Label(root, "Kills", "0", 26, new Color(1f, 0.92f, 0.85f), TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-24, -40), new Vector2(400, 36));
            coinsText = UiKit.Label(root, "Coins", "0", 26, UiKit.Gold, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-24, -74), new Vector2(400, 36));
            fpsText = UiKit.Label(root, "Fps", "", 18, new Color(1, 1, 1, 0.6f), TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-10, 8), new Vector2(300, 24), FontStyle.Normal);

            for (int i = 0; i < 6; i++)
            {
                weaponFrames[i] = UiKit.Img(root, "W" + i, new Vector2(0f, 1f), new Vector2(18 + i * 58, -40), new Vector2(52, 52), UiKit.Ink, null, new Vector2(0f, 1f));
                weaponIcons[i] = UiKit.Img(weaponFrames[i].transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42), Color.white, UiKit.White);
                weaponLevels[i] = UiKit.Label(weaponFrames[i].transform, "Lv", "", 16, Color.white, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-2, 0), new Vector2(40, 20), FontStyle.Bold, true, new Vector2(1f, 0f));
                var pf = UiKit.Img(root, "P" + i, new Vector2(0f, 1f), new Vector2(18 + i * 58, -98), new Vector2(52, 52), new Color(0.12f, 0.1f, 0.1f, 0.7f), null, new Vector2(0f, 1f));
                passiveIcons[i] = UiKit.Img(pf.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38), Color.white, UiKit.White);
                passiveLevels[i] = UiKit.Label(pf.transform, "Lv", "", 16, Color.white, TextAnchor.LowerRight, new Vector2(1f, 0f), new Vector2(-2, 0), new Vector2(40, 20), FontStyle.Bold, true, new Vector2(1f, 0f));
            }

            // Main weapon: gold frame over its slot, trait notice on switching.
            mainFrame = UiKit.Img(root, "MainFrame", new Vector2(0f, 1f), new Vector2(14, -36), new Vector2(60, 60), new Color(1f, 0.8f, 0.3f, 0.95f), UiKit.Round, new Vector2(0f, 1f));
            mainFrame.transform.SetSiblingIndex(0);
            mainTag = UiKit.Label(mainFrame.transform, "Tag", "主", 16, new Color(0.25f, 0.12f, 0.02f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(3, 1), new Vector2(30, 22), FontStyle.Bold, false, new Vector2(0f, 1f));
            switchHint = UiKit.Label(root, "SwitchHint", "Q/E 主武器切替　V 視点　G/R3 身振り設定　左クリック長押し 移動", 17, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20, -154), new Vector2(600, 26), FontStyle.Normal, true, new Vector2(0f, 1f));
            mainText = UiKit.Label(root, "MainText", "", 24, UiKit.Gold, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(20, -182), new Vector2(380, 120), FontStyle.Bold, true, new Vector2(0f, 1f));

            // First person: crosshair and a radar for Italians sneaking up from behind.
            crossRing = UiKit.Img(root, "CrossRing", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34), new Color(1f, 1f, 1f, 0.5f), UiKit.Pill);
            crossDot = UiKit.Img(root, "CrossDot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8), new Color(1f, 0.9f, 0.5f, 0.95f), UiKit.Pill);
            radar = UiKit.Rect(root, "Radar", new Vector2(1f, 0f), new Vector2(-30, 40), new Vector2(240, 240), new Vector2(1f, 0f));
            radarBg = UiKit.Img(radar, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 240), new Color(0.05f, 0.04f, 0.04f, 0.55f), UiKit.Pill);
            UiKit.Img(radar, "Ring", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120), new Color(1f, 1f, 1f, 0.08f), UiKit.Pill);
            var cone = UiKit.Img(radar, "View", new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(6, 60), new Color(1f, 0.9f, 0.5f, 0.35f), UiKit.White);
            UiKit.Img(radar, "Me", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12), UiKit.Gold, UiKit.Pill);
            _ = cone;
            for (int i = 0; i < 160; i++)
            {
                var d = UiKit.Img(radar, "Dot", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7), Color.white, UiKit.Pill);
                d.enabled = false;
                radarDots.Add(d);
            }

            // Special weapon slot (bottom-left): manual, with a cooldown dial.
            specialFrame = UiKit.Img(root, "Special", new Vector2(0f, 0f), new Vector2(28, 30), new Vector2(124, 124), UiKit.Ink, UiKit.Round, new Vector2(0f, 0f));
            specialIcon = UiKit.Img(specialFrame.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 92), Color.white, UiKit.White);
            specialCd = UiKit.Img(specialFrame.transform, "Cooldown", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112, 112), new Color(0f, 0f, 0f, 0.65f), UiKit.Pill);
            specialCd.type = Image.Type.Filled;
            specialCd.fillMethod = Image.FillMethod.Radial360;
            specialCd.fillOrigin = (int)Image.Origin360.Top;
            specialCd.fillClockwise = false;
            specialName = UiKit.Label(root, "SpecialName", "", 22, UiKit.Gold, TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(162, 94), new Vector2(520, 60), FontStyle.Bold, true, new Vector2(0f, 0f));
            specialKey = UiKit.Label(root, "SpecialKey", "", 18, new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(162, 34), new Vector2(520, 56), FontStyle.Normal, true, new Vector2(0f, 0f));

            // Minimap (top-down): walls, water, areas, pedestals and points of interest.
            minimap = UiKit.Rect(root, "Minimap", new Vector2(1f, 0f), new Vector2(-24, 30), new Vector2(300, 300), new Vector2(1f, 0f));
            var mapBg = UiKit.Img(minimap, "Frame", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(308, 308), UiKit.Ink, UiKit.Round);
            _ = mapBg;
            var mapGo = new GameObject("Map", typeof(RectTransform));
            var mapRt = (RectTransform)mapGo.transform;
            mapRt.SetParent(minimap, false);
            mapRt.anchorMin = mapRt.anchorMax = new Vector2(0.5f, 0.5f);
            mapRt.sizeDelta = new Vector2(300, 300);
            minimapImage = mapGo.AddComponent<RawImage>();
            minimapImage.raycastTarget = false;
            for (int i = 0; i < 40; i++)
            {
                var d = UiKit.Img(mapRt, "Dot", new Vector2(0f, 0f), Vector2.zero, new Vector2(8, 8), Color.white, UiKit.Pill, new Vector2(0.5f, 0.5f));
                d.enabled = false;
                mapDots.Add(d);
            }
            mapPlayer = UiKit.Img(mapRt, "Me", new Vector2(0f, 0f), Vector2.zero, new Vector2(14, 14), UiKit.Gold, UiKit.Pill, new Vector2(0.5f, 0.5f));

            // Above both the minimap and first-person radar, with room for a two-line tactical hint.
            districtRoot = UiKit.Rect(root, "District", new Vector2(1f, 0f), new Vector2(-24, 346), new Vector2(400, 98), new Vector2(1f, 0f));
            UiKit.Img(districtRoot, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 98), UiKit.Ink, UiKit.Round);
            districtName = UiKit.Label(districtRoot, "Name", "", 24, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(12, -8), new Vector2(376, 34), pivot: new Vector2(0f, 1f));
            districtHint = UiKit.Label(districtRoot, "Hint", "", 18, UiKit.Cream, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(12, -42), new Vector2(376, 48), FontStyle.Normal, pivot: new Vector2(0f, 1f));
            districtName.verticalOverflow = districtHint.verticalOverflow = VerticalWrapMode.Truncate;
            districtRoot.gameObject.SetActive(false);

            // Screen-edge pointers toward specials, chests and the boss.
            for (int i = 0; i < 8; i++)
            {
                var rt = UiKit.Rect(root, "Pointer", new Vector2(0f, 0f), Vector2.zero, new Vector2(60, 60), new Vector2(0.5f, 0.5f));
                var arrow = UiKit.Img(rt, "Arrow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40), Color.white, Icons.Get("arrow"));
                var icon = UiKit.Img(rt, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40), Color.white, UiKit.White);
                rt.gameObject.SetActive(false);
                pointers.Add(new Pointer { rt = rt, arrow = arrow, icon = icon });
            }

            // HP bar that follows the player (Vampire Survivors style).
            hpRoot = UiKit.Rect(root, "PlayerHP", new Vector2(0f, 0f), Vector2.zero, new Vector2(90, 18), new Vector2(0.5f, 0.5f));
            var hpBg = UiKit.Img(hpRoot, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 12), UiKit.Ink, UiKit.White);
            hpFill = UiKit.Fill(UiKit.Img(hpBg.transform, "Fill", new Vector2(0f, 0.5f), new Vector2(2, 0), new Vector2(86, 8), new Color(0.95f, 0.2f, 0.2f), null, new Vector2(0f, 0.5f)));
            var dashBg = UiKit.Img(hpRoot, "DashBg", new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(90, 6), new Color(0, 0, 0, 0.6f), UiKit.White);
            dashFill = UiKit.Fill(UiKit.Img(dashBg.transform, "Fill", new Vector2(0f, 0.5f), new Vector2(1, 0), new Vector2(88, 4), new Color(0.6f, 0.9f, 1f), null, new Vector2(0f, 0.5f)));

            // Boss bar
            bossRoot = UiKit.Rect(root, "Boss", new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(900, 50));
            var bossBg = UiKit.Img(bossRoot, "Bg", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(900, 22), UiKit.Ink, UiKit.Pill);
            bossFill = UiKit.Fill(UiKit.Img(bossBg.transform, "Fill", new Vector2(0f, 0.5f), new Vector2(4, 0), new Vector2(892, 14), new Color(0.85f, 0.15f, 0.35f), null, new Vector2(0f, 0.5f)));
            bossName = UiKit.Label(bossRoot, "Name", "", 24, new Color(1f, 0.85f, 0.85f), TextAnchor.LowerCenter, new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(900, 30));
            bossRoot.gameObject.SetActive(false);

            // Event banner
            bannerRoot = UiKit.Rect(root, "Banner", new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(1100, 64));
            bannerBg = UiKit.Img(bannerRoot, "Bg", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 60), new Color(0, 0, 0, 0.55f), UiKit.Pill);
            bannerText = UiKit.Label(bannerRoot, "Text", "", 34, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080, 60));
            bannerRoot.gameObject.SetActive(false);

            // Stage title card
            titleRoot = UiKit.Rect(root, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(1400, 200));
            titleText = UiKit.Label(titleRoot, "Name", "", 96, UiKit.Gold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 120));
            subText = UiKit.Label(titleRoot, "Sub", "", 36, UiKit.Cream, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(1400, 50));
            titleRoot.gameObject.SetActive(false);
            SetFirstPerson(false);
        }

        /// <summary>Paints the stage layout into the minimap texture.</summary>
        public void BuildMinimap(Arena arena)
        {
            if (arena == null || arena.BlockedCells == null) return;
            int w = arena.GridW, h = arena.GridH;
            if (minimapTex != null) Destroy(minimapTex);
            minimapTex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = arena.CellCenter(x, y);
                    Color col = new Color(0.3f, 0.27f, 0.24f, 0.8f);
                    var district = arena.DistrictAt(c);
                    if (district != null) col = Color.Lerp(col, district.color, 0.4f);
                    foreach (var a in arena.areas)
                        if (new Vector2(c.x - a.c.x, c.z - a.c.y).magnitude < a.r)
                            col = a.kind == Arena.AreaKind.Heal ? new Color(0.25f, 0.55f, 0.3f, 0.9f) : new Color(0.55f, 0.4f, 0.2f, 0.85f);
                    foreach (var r in arena.roads)
                    {
                        var d = r.b - r.a;
                        float t = Mathf.Clamp01(Vector3.Dot(c - r.a, d) / d.sqrMagnitude);
                        if ((r.a + d * t - c).magnitude < r.width * 0.5f) col = new Color(0.12f, 0.12f, 0.14f, 0.9f);
                    }
                    if (arena.WaterCells[i]) col = new Color(0.2f, 0.45f, 0.62f, 0.95f);
                    else if (arena.BlockedCells[i]) col = new Color(0.72f, 0.66f, 0.58f, 0.95f);
                    px[i] = col;
                }
            minimapTex.SetPixels32(px);
            minimapTex.Apply();
            minimapImage.texture = minimapTex;
            float aspect = (float)h / w;
            var size = w >= h ? new Vector2(300f, 300f * aspect) : new Vector2(300f / aspect, 300f);
            minimapImage.rectTransform.sizeDelta = size;
            ((RectTransform)minimap.GetChild(0)).sizeDelta = size + Vector2.one * 8f;
        }

        public void SpecialAcquired(SpecialId id)
        {
            var def = GameData.Special(id);
            Banner("特殊武器を入手：" + def.name + "（右クリック / F / Y）", new Color(1f, 0.85f, 0.4f));
        }

        private void UpdateDistrict(Player p)
        {
            bool show = G.Arena != null && G.Arena.districts.Count > 0;
            if (districtRoot.gameObject.activeSelf != show) districtRoot.gameObject.SetActive(show);
            if (!show) return;
            var district = G.Arena.DistrictAt(p.Position);
            districtName.text = district != null ? district.name : "ローマの街路";
            districtName.color = district != null ? Color.Lerp(district.color, UiKit.Cream, 0.55f) : UiKit.Cream;
            districtHint.text = district != null ? district.hint : "広場や泉へ抜ける道を探そう";
        }

        public void SetFirstPerson(bool on)
        {
            firstPerson = on;
            if (minimap != null) minimap.gameObject.SetActive(!on);
            crossRing.gameObject.SetActive(on);
            crossDot.gameObject.SetActive(on);
            radar.gameObject.SetActive(on);
            hpRoot.localScale = Vector3.one * (on ? 2.4f : 1f);
        }

        public void MainSwitched(Weapon w)
        {
            if (w == null) return;
            mainText.text = $"主武器：{w.DisplayName}\n<size=19><color=#fff2d0>{w.def.mainTrait}</color></size>";
            mainT = 3f;
        }

        public void SetVisible(bool on)
        {
            root.gameObject.SetActive(on);
            Floating.gameObject.SetActive(on);
        }

        public void Banner(string text, Color color)
        {
            if (bannerT > 0.6f) { bannerQueue.Enqueue((text, color)); return; }
            bannerText.text = text;
            bannerText.color = color;
            bannerT = 3f;
            bannerRoot.gameObject.SetActive(true);
        }

        public void StageIntro(string name, string sub, float duration = 3.2f)
        {
            titleText.text = name;
            subText.text = sub;
            titleT = titleDuration = duration;
            titleRoot.gameObject.SetActive(true);
        }

        /// <summary>Hide the stage title card and event banner (e.g. behind the pause menu).</summary>
        public void HideCards()
        {
            bannerQueue.Clear();
            bannerT = titleT = 0f;
            bannerRoot.gameObject.SetActive(false);
            titleRoot.gameObject.SetActive(false);
        }

        public void ClearTransient()
        {
            bannerQueue.Clear();
            bannerT = titleT = 0f;
            bannerRoot.gameObject.SetActive(false);
            titleRoot.gameObject.SetActive(false);
            bossRoot.gameObject.SetActive(false);
            Floating.Clear();
        }

        private void UpdateSpecial(Player p)
        {
            if (p.Special.HasValue)
            {
                var def = GameData.Special(p.Special.Value);
                specialIcon.enabled = true;
                specialIcon.sprite = Icons.Get(def.icon);
                float remaining = Mathf.Clamp01(p.SpecialCd / p.SpecialCdMax);
                specialCd.fillAmount = remaining;
                specialFrame.color = remaining <= 0f ? new Color(0.45f, 0.3f, 0.05f, 0.95f) : UiKit.Ink;
                specialName.text = def.name + (p.BuffTime > 0f ? $"  <color=#fff0a0>効果中 {p.BuffTime:0.0}</color>" : "");
                specialKey.text = remaining <= 0f ? "右クリック / F / Y で使う" : $"再使用まで {p.SpecialCd:0.0} 秒";
            }
            else
            {
                specialIcon.enabled = false;
                specialCd.fillAmount = 0f;
                specialFrame.color = new Color(0.08f, 0.06f, 0.07f, 0.45f);
                specialName.text = "特殊武器なし";
                specialKey.text = "マップの光の柱（台座）で拾おう";
            }
        }

        private Vector2 MapPos(Vector3 world)
        {
            var a = G.Arena;
            var size = minimapImage.rectTransform.sizeDelta;
            return new Vector2((world.x - a.min.x) / (a.max.x - a.min.x) * size.x, (world.z - a.min.y) / (a.max.y - a.min.y) * size.y);
        }

        private void UpdateMinimap(Player p)
        {
            if (G.Arena == null || minimapTex == null) return;
            // Dots are children of the map image, whose pivot is the centre; offset by half the size.
            var half = minimapImage.rectTransform.sizeDelta * 0.5f;
            mapPlayer.rectTransform.anchoredPosition = MapPos(p.Position) - half;
            int n = 0;
            void Dot(Vector3 at, Color c, float size)
            {
                if (n >= mapDots.Count) return;
                var d = mapDots[n++];
                d.enabled = true;
                d.color = c;
                d.rectTransform.sizeDelta = new Vector2(size, size);
                d.rectTransform.anchoredPosition = MapPos(at) - half;
            }
            float pulse = 10f + Mathf.Sin(Time.unscaledTime * 6f) * 3f;
            foreach (var s in G.Arena.specialSpots) Dot(s, new Color(0.9f, 0.85f, 0.7f, 0.5f), 6f);
            foreach (var pk in G.Pickups.Active)
            {
                if (pk.kind == PickupKind.Special) Dot(pk.pos, new Color(1f, 0.8f, 0.2f), pulse);
                else if (pk.kind == PickupKind.Chest) Dot(pk.pos, new Color(1f, 0.95f, 0.4f), 10f);
            }
            foreach (var e in G.Enemies.Active)
            {
                if (!e.active || e.fleeing) continue;
                if (e.IsBoss) Dot(e.pos, new Color(1f, 0.2f, 0.3f), 16f);
                else if (e.IsElite) Dot(e.pos, new Color(0.4f, 1f, 0.4f), 10f);
            }
            for (int i = n; i < mapDots.Count; i++) { if (!mapDots[i].enabled) break; mapDots[i].enabled = false; }
        }

        private void UpdatePointers(Player p)
        {
            var cam = G.Cam != null ? G.Cam.Cam : null;
            int n = 0;
            void Point(Vector3 world, Sprite icon, Color color)
            {
                if (cam == null || n >= pointers.Count) return;
                var sp = cam.WorldToScreenPoint(world + Vector3.up);
                bool behind = sp.z < 0f;
                if (behind) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, 0f);
                float margin = 70f * root.localScale.x;
                bool onScreen = !behind && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;
                if (onScreen) return;
                var center = new Vector2(Screen.width, Screen.height) * 0.5f;
                var dir = new Vector2(sp.x, sp.y) - center;
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                float sx = (center.x - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.x));
                float sy = (center.y - margin) / Mathf.Max(0.001f, Mathf.Abs(dir.y));
                var pos = center + dir * Mathf.Min(sx, sy);
                var ptr = pointers[n++];
                ptr.rt.gameObject.SetActive(true);
                ptr.rt.position = pos;
                ptr.arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                ptr.arrow.rectTransform.anchoredPosition = dir.normalized * 22f;
                ptr.arrow.color = color;
                ptr.icon.sprite = icon;
            }
            foreach (var pk in G.Pickups.Active)
            {
                if (pk.kind == PickupKind.Special) Point(pk.pos, Icons.Get(GameData.Special(pk.special).icon), UiKit.Gold);
                else if (pk.kind == PickupKind.Chest) Point(pk.pos, Icons.Get("coin"), new Color(1f, 0.95f, 0.5f));
            }
            var boss = G.Enemies.Boss;
            if (boss != null && boss.active && !boss.fleeing) Point(boss.pos, Icons.Get("heart"), new Color(1f, 0.3f, 0.3f));
            for (int i = n; i < pointers.Count; i++) if (pointers[i].rt.gameObject.activeSelf) pointers[i].rt.gameObject.SetActive(false);
        }

        private void UpdateRadar(Player p)
        {
            const float range = 28f, half = 110f;
            var inv = Quaternion.Euler(0f, -p.LookYaw, 0f);
            int n = 0;
            foreach (var e in G.Enemies.Active)
            {
                if (n >= radarDots.Count) break;
                if (!e.active || e.fleeing) continue;
                var d = inv * (e.pos - p.Position);
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist > range) continue;
                var dot = radarDots[n++];
                dot.enabled = true;
                dot.rectTransform.anchoredPosition = new Vector2(d.x, d.z) / range * half;
                dot.color = RadarColors.For(e);
                float size = e.IsBoss ? 18f : e.IsElite ? 12f : 7f;
                dot.rectTransform.sizeDelta = new Vector2(size, size);
            }
            foreach (var pk in G.Pickups.Active)
            {
                if (n >= radarDots.Count) break;
                if (pk.kind != PickupKind.Chest && pk.kind != PickupKind.Pizza && pk.kind != PickupKind.Special) continue;
                var d = inv * (pk.pos - p.Position);
                if (new Vector2(d.x, d.z).magnitude > range) continue;
                var dot = radarDots[n++];
                dot.enabled = true;
                dot.rectTransform.anchoredPosition = new Vector2(d.x, d.z) / range * half;
                dot.color = pk.kind == PickupKind.Chest ? UiKit.Gold : pk.kind == PickupKind.Special ? new Color(1f, 0.6f, 0.1f) : new Color(0.4f, 1f, 0.6f);
                dot.rectTransform.sizeDelta = pk.kind == PickupKind.Special ? new Vector2(16f, 16f) : new Vector2(12f, 12f);
            }
            for (int i = n; i < radarDots.Count; i++)
            {
                if (!radarDots[i].enabled) break;
                radarDots[i].enabled = false;
            }
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            fpsAcc += dt; fpsFrames++;
            if (fpsAcc >= 0.5f)
            {
                fpsText.text = ShowFps ? $"{fpsFrames / fpsAcc:0} fps  敵 {G.Enemies?.HostileCount}" : "";
                fpsAcc = 0f; fpsFrames = 0;
            }
            if (bannerT > 0f)
            {
                bannerT -= dt;
                float a = Mathf.Clamp01(bannerT / 0.4f) * Mathf.Clamp01((3f - bannerT) / 0.15f);
                bannerText.canvasRenderer.SetAlpha(a);
                bannerBg.canvasRenderer.SetAlpha(a);
                bannerRoot.localScale = Vector3.one * (1f + Mathf.Clamp01((bannerT - 2.8f) / 0.2f) * 0.15f);
                if (bannerT <= 0f)
                {
                    bannerRoot.gameObject.SetActive(false);
                    if (bannerQueue.Count > 0) { var (t, c) = bannerQueue.Dequeue(); Banner(t, c); }
                }
            }
            if (titleT > 0f)
            {
                titleT -= dt;
                float a = Mathf.Clamp01(titleT / 0.6f) * Mathf.Clamp01((titleDuration - titleT) / 0.3f);
                titleText.canvasRenderer.SetAlpha(a);
                subText.canvasRenderer.SetAlpha(a);
                if (titleT <= 0f) titleRoot.gameObject.SetActive(false);
            }

            var p = G.Player;
            if (p == null || !root.gameObject.activeSelf) return;
            xpFill.fillAmount = Mathf.Clamp01(p.Xp / p.XpNeeded);
            levelText.text = "Lv " + p.Level;
            float remaining = G.Stage != null ? G.RunTime : 0f;
            timerText.text = UiKit.Clock(remaining);
            if (G.Enemies != null) threatText.text = $"敵 HP×{G.Enemies.HpMul:0.0}  攻撃×{G.Enemies.DamageMul:0.0}";
            killsText.text = "撃退 " + (G.Enemies != null ? G.Enemies.Kills : 0);
            coinsText.text = "€ " + p.RunCoins;

            for (int i = 0; i < 6; i++)
            {
                if (i < p.Weapons.Count)
                {
                    var w = p.Weapons[i];
                    weaponIcons[i].enabled = true;
                    weaponIcons[i].sprite = Icons.Get(w.evolved ? w.def.evoIcon : w.def.icon);
                    weaponLevels[i].text = w.evolved ? "★" : w.level.ToString();
                    weaponFrames[i].color = w.evolved ? new Color(0.5f, 0.3f, 0.05f, 0.9f) : UiKit.Ink;
                }
                else { weaponIcons[i].enabled = false; weaponLevels[i].text = ""; weaponFrames[i].color = new Color(0.08f, 0.06f, 0.07f, 0.45f); }
                if (i < p.Passives.Count)
                {
                    var id = p.Passives[i];
                    passiveIcons[i].enabled = true;
                    passiveIcons[i].sprite = Icons.Get(GameData.Passive(id).icon);
                    passiveLevels[i].text = p.PassiveLevels[(int)id].ToString();
                }
                else { passiveIcons[i].enabled = false; passiveLevels[i].text = ""; }
            }

            int mainIdx = p.MainIndex;
            mainFrame.enabled = mainIdx < p.Weapons.Count;
            mainTag.enabled = mainFrame.enabled;
            if (mainFrame.enabled)
                mainFrame.rectTransform.anchoredPosition = new Vector2(18 + mainIdx * 58 - 4, -36);
            switchHint.enabled = true;
            if (mainT > 0f)
            {
                mainT -= dt;
                mainText.canvasRenderer.SetAlpha(Mathf.Clamp01(mainT / 0.5f));
            }
            else if (mainText.text.Length > 0) mainText.text = "";
            if (firstPerson) UpdateRadar(p);
            else UpdateMinimap(p);
            UpdateDistrict(p);
            UpdateSpecial(p);
            UpdatePointers(p);
            if (G.Fx != null) G.Fx.Reticle(p.MouseGround, !firstPerson && p.MouseAiming && G.Playing);

            var cam = G.Cam != null ? G.Cam.Cam : null;
            if (cam != null)
            {
                var sp = firstPerson ? new Vector3(Screen.width * 0.5f, 70f * root.localScale.y, 0f) : cam.WorldToScreenPoint(p.Position + Vector3.down * 0.25f);
                hpRoot.position = sp + Vector3.down * 16f * root.localScale.y;
                hpFill.fillAmount = Mathf.Clamp01(p.Hp / p.MaxHp);
                dashFill.fillAmount = p.DashReady;
                dashFill.color = p.DashReady >= 1f ? new Color(0.6f, 0.9f, 1f) : new Color(0.4f, 0.5f, 0.6f);
            }

            var boss = G.Enemies != null ? G.Enemies.Boss : null;
            bool showBoss = boss != null && boss.active && !boss.fleeing;
            if (bossRoot.gameObject.activeSelf != showBoss) bossRoot.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                bossFill.fillAmount = boss.HpFraction;
                bossName.text = boss.def.name + (boss.hp < boss.maxHp * 0.5f ? "  —  激怒！" : "");
            }
        }
    }

    /// <summary>Damage numbers and Italian exclamations projected from world space.</summary>
    public static class RadarColors
    {
        public static Color For(Enemy e) =>
            e.IsBoss ? new Color(1f, 0.2f, 0.3f) : e.IsElite ? new Color(0.4f, 1f, 0.4f) : e.IsProp ? new Color(0.7f, 0.45f, 0.2f) :
            e.def.behavior == Behavior.Thrower ? new Color(1f, 0.6f, 0.8f) : e.def.behavior == Behavior.Charger ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.95f, 0.9f);
    }

    public class FloatingText : MonoBehaviour
    {
        private class Item
        {
            public Text text;
            public RectTransform rt;
            public Vector3 world;
            public float t, life, rise, scale;
            public bool active;
        }

        private readonly List<Item> items = new List<Item>();
        private RectTransform root;
        private int cursor;
        private const int Capacity = 90;

        public void Init(RectTransform r)
        {
            root = r;
            for (int i = 0; i < Capacity; i++)
            {
                var t = UiKit.Label(root, "F", "", 26, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 0f), Vector2.zero, new Vector2(400, 60), FontStyle.Bold, true, new Vector2(0.5f, 0.5f));
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.gameObject.SetActive(false);
                items.Add(new Item { text = t, rt = t.rectTransform });
            }
        }

        private Item Next()
        {
            for (int k = 0; k < Capacity; k++)
            {
                var it = items[(cursor + k) % Capacity];
                if (!it.active) { cursor = (cursor + k + 1) % Capacity; return it; }
            }
            var oldest = items[cursor];
            cursor = (cursor + 1) % Capacity;
            return oldest;
        }

        private float budget = 30f;

        public void Number(Vector3 world, float value, bool big, bool hurt, bool heal)
        {
            // Hordes can produce hundreds of hits a second; keep the numbers readable.
            if (!hurt && !heal && !big)
            {
                if (budget < 1f) return;
                budget -= 1f;
            }
            var it = Next();
            it.world = world + Random.insideUnitSphere * 0.3f;
            it.t = 0f; it.life = hurt ? 0.9f : 0.55f; it.rise = 60f; it.scale = big ? 1.25f : 1f;
            it.text.text = heal ? "+" + Mathf.RoundToInt(value) : Mathf.Max(1, Mathf.RoundToInt(value)).ToString();
            it.text.fontSize = hurt ? 34 : big ? 32 : 24;
            it.text.color = hurt ? new Color(1f, 0.3f, 0.3f) : heal ? new Color(0.5f, 1f, 0.5f) : Color.white;
            it.active = true;
            it.text.gameObject.SetActive(true);
        }

        public void Shout(Vector3 world, string msg, Color color, float scale)
        {
            var it = Next();
            it.world = world;
            it.t = 0f; it.life = 1.1f; it.rise = 40f; it.scale = scale;
            it.text.text = msg;
            it.text.fontSize = 30;
            it.text.color = color;
            it.active = true;
            it.text.gameObject.SetActive(true);
        }

        public void Clear()
        {
            foreach (var it in items) { it.active = false; it.text.gameObject.SetActive(false); }
        }

        private void LateUpdate()
        {
            var cam = G.Cam != null ? G.Cam.Cam : null;
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            budget = Mathf.Min(30f, budget + dt * 45f);
            float ui = root.localScale.y;
            foreach (var it in items)
            {
                if (!it.active) continue;
                if (Time.timeScale > 0f) it.t += dt;
                if (it.t >= it.life)
                {
                    it.active = false;
                    it.text.gameObject.SetActive(false);
                    continue;
                }
                float k = it.t / it.life;
                var sp = cam.WorldToScreenPoint(it.world);
                if (sp.z < 0f) { it.text.gameObject.SetActive(false); continue; }
                it.rt.position = sp + Vector3.up * (it.rise * ui * (1f - (1f - k) * (1f - k)));
                float pop = k < 0.15f ? Mathf.Lerp(1.5f, 1f, k / 0.15f) : 1f;
                it.rt.localScale = Vector3.one * it.scale * pop;
                it.text.canvasRenderer.SetAlpha(k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f);
            }
        }
    }
}
