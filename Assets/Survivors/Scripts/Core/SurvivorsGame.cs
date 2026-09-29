using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PastaSurvivors
{
    public enum GameState { Boot, Title, Playing, LevelUp, Chest, Paused, GameOver, Cleared }

    /// <summary>Entry point. The scene only contains this component; everything else is created at runtime.</summary>
    public class SurvivorsGame : MonoBehaviour
    {
        public GameState State { get; private set; } = GameState.Boot;
        public int SelectedCharacter;
        public int StageIndex { get; private set; }
        public Menus Menus { get; private set; }
        public Music Music { get; private set; }
        public static bool AutoPauseOnFocusLoss = true;
        /// <summary>Game speed while playing (1 = normal). Automated balance runs play faster.</summary>
        public static float TimeMultiplier = 1f;

        private Transform world;
        private int builtStage = -1;
        private int pendingLevelUps, pendingChests;
        private float bossDefeatedAt = -1f;
        private readonly Dictionary<int, float> damage = new Dictionary<int, float>();
        private bool ending;

        private void Awake()
        {
            G.Game = this;
            SaveData.Load();
            Mats.Init();
            UiKit.Init();
            Time.timeScale = 1f;

            var camGo = new GameObject("Camera");
            G.Cam = camGo.AddComponent<CameraRig>();
            G.Cam.Init();

            G.Sfx = Child<Sfx>("Sfx"); G.Sfx.Init();
            Music = Child<Music>("Music"); Music.Init();
            G.Enemies = Child<EnemyManager>("Enemies"); G.Enemies.Init();
            G.Shots = Child<ShotSystem>("Shots"); G.Shots.Init();
            G.Pickups = Child<PickupManager>("Pickups"); G.Pickups.Init();
            G.Fx = Child<Fx>("Fx"); G.Fx.Init();
            G.Waves = Child<WaveDirector>("Waves");
            G.Hud = Child<Hud>("Hud"); G.Hud.Build(transform);
            Menus = Child<Menus>("MenusRoot"); Menus.Build(transform);

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-stage-minutes" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m))
                    G.Waves.TimeScale = Mathf.Clamp(m / 10f, 0.05f, 3f);
        }

        private T Child<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        private void Start() => GoTitle();

        // ---------------- flow ----------------

        private void BuildWorld(int stage)
        {
            if (builtStage == stage && world != null) return;
            if (world != null) Destroy(world.gameObject);
            world = new GameObject("World").transform;
            G.Stage = GameData.Stages[stage];
            G.Arena = StageBuilder.Build(G.Stage, world);
            G.Enemies.ConfigureGrid(G.Arena);
            builtStage = stage;
            Resources.UnloadUnusedAssets();
        }

        private void ClearRun()
        {
            StopAllCoroutines();
            G.Enemies.Clear();
            G.Shots.Clear();
            G.Pickups.Clear();
            G.Fx.Clear();
            G.Hud.ClearTransient();
            if (G.Player != null)
            {
                foreach (var w in G.Player.Weapons) w.Removed();
                Destroy(G.Player.gameObject);
                G.Player = null;
            }
            foreach (var f in FindObjectsByType<PastaFragment>()) Destroy(f.gameObject);
            pendingLevelUps = pendingChests = 0;
            bossDefeatedAt = -1f;
            damage.Clear();
            G.Enemies.Kills = 0;
            G.RunTime = 0f;
            ending = false;
        }

        private Player CreatePlayer(int character)
        {
            var go = new GameObject("Player");
            var p = go.AddComponent<Player>();
            p.Setup(GameData.Characters[character], character);
            G.Player = p;
            return p;
        }

        public void GoTitle()
        {
            ClearRun();
            Time.timeScale = 1f;
            State = GameState.Title;
            BuildWorld(0);
            var p = CreatePlayer(SelectedCharacter);
            p.transform.position = new Vector3(0f, 0f, 2f);
            p.ResetRun();
            // A circle of outraged Italians for the title card.
            EnemyKind[] cast = { EnemyKind.Signore, EnemyKind.Mamma, EnemyKind.Chef, EnemyKind.Tifoso, EnemyKind.Gondoliere, EnemyKind.Nonna, EnemyKind.Pizzaiolo, EnemyKind.Mafioso, EnemyKind.Vespista, EnemyKind.Tifoso };
            for (int i = 0; i < cast.Length; i++)
            {
                float a = (i / (float)cast.Length) * Mathf.PI * 2f;
                var pos = p.Position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 4.2f;
                var e = G.Enemies.Spawn(cast[i], pos);
                if (e == null) continue;
                e.frozen = true;
                e.facing = (p.Position - pos).normalized;
                e.ApplyTransform();
            }
            G.Cam.orbit = true;
            G.Cam.firstPerson = false;
            G.Hud.SetFirstPerson(false);
            G.Cam.pitch = 32f;
            G.Cam.distance = 15f;
            G.Cam.SetFocus(p.Position + Vector3.up * 1.2f);
            G.Hud.SetVisible(false);
            Menus.ShowTitle();
            Music.PlayTheme(0);
            Music.Duck(false);
        }

        public void StartRun(int stage, int character)
        {
            ClearRun();
            StageIndex = stage;
            SelectedCharacter = character;
            BuildWorld(stage);
            var p = CreatePlayer(character);
            p.transform.position = Vector3.zero;
            p.ResetRun();
            G.Cam.orbit = false;
            G.Cam.yaw = 0f;
            G.Cam.pitch = 55f;
            G.Cam.distance = 23f;
            G.Cam.Snap(p.Position);
            ApplyViewMode();
            G.Waves.Begin(G.Stage);
            Time.timeScale = TimeMultiplier;
            State = GameState.Playing;
            Menus.Close();
            G.Hud.SetVisible(true);
            G.Hud.StageIntro(G.Stage.name, G.Stage.sub);
            Music.PlayTheme(1 + stage);
            Music.Duck(false);
        }

        /// <summary>Switches between the top-down survivors view and first person.</summary>
        public void ApplyViewMode()
        {
            bool fp = SaveData.FirstPerson && G.Player != null && State != GameState.Title && State != GameState.Boot;
            G.Cam.firstPerson = fp;
            G.Player?.SetFirstPerson(fp);
            G.Hud.SetFirstPerson(fp);
        }

        public void ToggleViewMode()
        {
            SaveData.ViewMode = SaveData.FirstPerson ? 0 : 1;
            SaveData.Save();
            ApplyViewMode();
            if (State == GameState.Playing) G.Fx.Banner(SaveData.FirstPerson ? "一人称モード（マウスで視点）" : "見下ろしモード", Color.white);
        }

        private void UpdateCursor()
        {
            bool locked = State == GameState.Playing && SaveData.FirstPerson && Application.isFocused;
            var mode = locked ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            if (Cursor.visible == locked) Cursor.visible = !locked;
        }

        private void Update()
        {
            UpdateCursor();
            if (Debug.isDebugBuild || Application.isEditor) DevKeys();
            switch (State)
            {
                case GameState.Title:
                    G.Enemies.Tick(Time.deltaTime);
                    G.Player?.Anim.Tick(Time.deltaTime, 0f, Vector3.back, false, false);
                    break;
                case GameState.Playing:
                    TickPlaying();
                    break;
                case GameState.GameOver:
                    // Keep the world moving through the slow-motion defeat.
                    if (Time.timeScale > 0f)
                    {
                        G.Enemies.Tick(Time.deltaTime);
                        G.Shots.Tick(Time.deltaTime);
                        G.Player?.Anim.Fall(Time.deltaTime);
                    }
                    break;
            }
        }

        private void TickPlaying()
        {
            if (Controls.Pause()) { Pause(); return; }
            if (Controls.ToggleView()) ToggleViewMode();
            float dt = Time.deltaTime;
            G.RunTime += dt;
            if (bossDefeatedAt < 0f) G.Waves.Tick(dt, G.RunTime);
            G.Player.Tick(dt);
            G.Enemies.Tick(dt);
            G.Shots.Tick(dt);
            G.Pickups.Tick(dt);
            if (State != GameState.Playing) return;
            if (pendingChests > 0) { ShowChest(); return; }
            if (pendingLevelUps > 0) { ShowLevelUp(); return; }
            if (bossDefeatedAt >= 0f && G.RunTime - bossDefeatedAt > 3.5f && !ending) StartCoroutine(Clear());
        }

        private void DevKeys()
        {
            if (Controls.DebugKey(Key.F3)) G.Hud.ShowFps = !G.Hud.ShowFps;
            if (State != GameState.Playing) return;
            if (Controls.DebugKey(Key.F1)) G.RunTime += 60f;
            if (Controls.DebugKey(Key.F2)) G.Player.AddXp(G.Player.XpNeeded);
            if (Controls.DebugKey(Key.F4)) G.RunTime = G.Stage.duration * G.Waves.TimeScale - 1f;
            if (Controls.DebugKey(Key.F5)) { G.Player.God = !G.Player.God; G.Fx.Banner(G.Player.God ? "無敵モード" : "無敵解除", Color.white); }
            if (Controls.DebugKey(Key.F6)) pendingChests++;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && AutoPauseOnFocusLoss && State == GameState.Playing) Pause();
            if (focus && State == GameState.Playing && SaveData.FirstPerson) Cursor.lockState = CursorLockMode.Locked;
        }

        // ---------------- pause / level up / chest ----------------

        public void Pause()
        {
            if (State != GameState.Playing && State != GameState.Paused) return;
            State = GameState.Paused;
            Time.timeScale = 0f;
            G.Hud.HideCards();
            Music.Duck(true);
            Menus.ShowPause(Resume, () => { EndRunRewards(false); GoTitle(); }, () => { ToggleViewMode(); Pause(); });
        }

        public void Resume()
        {
            State = GameState.Playing;
            Time.timeScale = TimeMultiplier;
            Music.Duck(false);
            Menus.Close();
        }

        public List<Offer> LastOffers { get; private set; }
        public void QueueLevelUp() => pendingLevelUps++;
        public void OpenChest() => pendingChests++;
        public void RecordDamage(int slot, float amount)
        {
            if (slot < 0) return;
            damage.TryGetValue(slot, out var d);
            damage[slot] = d + amount;
        }

        private void ShowLevelUp()
        {
            State = GameState.LevelUp;
            Time.timeScale = 0f;
            G.Sfx.Play(SfxId.LevelUp, 0.8f);
            LastOffers = BuildOffers();
            Menus.ShowLevelUp(LastOffers, G.Player.Level - pendingLevelUps + 1, o =>
            {
                Apply(o);
                pendingLevelUps--;
                Resume();
            });
        }

        public List<Offer> BuildOffers()
        {
            var p = G.Player;
            var pool = new List<(Offer offer, float weight)>();
            foreach (var w in p.Weapons)
                if (!w.MaxedOut)
                    pool.Add((new Offer
                    {
                        kind = Offer.Kind.UpWeapon, weapon = w.Id, title = w.DisplayName, icon = w.evolved ? w.def.evoIcon : w.def.icon,
                        tag = $"Lv{w.level} → {w.level + 1}", desc = w.def.levelText[w.level - 1], tagColor = UiKit.Gold
                    }, 1.3f));
            if (p.Weapons.Count < Player.MaxWeapons)
                foreach (var def in GameData.Weapons)
                    if (p.GetWeapon(def.id) == null)
                        pool.Add((new Offer
                        {
                            kind = Offer.Kind.NewWeapon, weapon = def.id, title = def.name, icon = def.icon, tag = "NEW!", desc = def.desc,
                            tagColor = new Color(0.5f, 1f, 0.5f)
                        }, 1f));
            foreach (var id in p.Passives)
            {
                var def = GameData.Passive(id);
                int lv = p.PassiveLevels[(int)id];
                if (lv < def.maxLevel)
                    pool.Add((new Offer
                    {
                        kind = Offer.Kind.UpPassive, passive = id, title = def.name, icon = def.icon, tag = $"Lv{lv} → {lv + 1}", desc = def.desc, tagColor = UiKit.Gold
                    }, 1f));
            }
            if (p.Passives.Count < Player.MaxPassives)
                foreach (var def in GameData.Passives)
                    if (p.PassiveLevels[(int)def.id] == 0)
                    {
                        string hint = "";
                        foreach (var w in GameData.Weapons)
                            if (w.evoPartner == def.id && p.GetWeapon(w.id) != null) hint = $"\n（{w.name}の進化に必要）";
                        pool.Add((new Offer
                        {
                            kind = Offer.Kind.NewPassive, passive = def.id, title = def.name, icon = def.icon, tag = "NEW!", desc = def.desc + hint,
                            tagColor = new Color(0.5f, 1f, 0.5f)
                        }, hint.Length > 0 ? 1.4f : 0.8f));
                    }

            var result = new List<Offer>();
            int count = G.Player.Level % 10 == 0 ? 4 : 3;
            while (result.Count < count && pool.Count > 0)
            {
                float total = 0f;
                foreach (var e in pool) total += e.weight;
                float r = Random.value * total;
                int pick = pool.Count - 1;
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= pool[i].weight;
                    if (r <= 0f) { pick = i; break; }
                }
                result.Add(pool[pick].offer);
                pool.RemoveAt(pick);
            }
            if (result.Count == 0)
            {
                result.Add(new Offer { kind = Offer.Kind.Heal, title = "ナポリピッツァ", icon = "pizzaslice", tag = "回復", desc = "HPを50回復する。", tagColor = UiKit.Basil });
                result.Add(new Offer { kind = Offer.Kind.Coins, title = "チップ", icon = "coin", tag = "€ +25", desc = "コインを25枚もらう。", tagColor = UiKit.Gold });
            }
            return result;
        }

        public void Apply(Offer o)
        {
            var p = G.Player;
            switch (o.kind)
            {
                case Offer.Kind.NewWeapon: p.AddWeapon(o.weapon); break;
                case Offer.Kind.UpWeapon:
                    {
                        var w = p.GetWeapon(o.weapon);
                        if (w != null && !w.MaxedOut) w.level++;
                        break;
                    }
                case Offer.Kind.NewPassive:
                case Offer.Kind.UpPassive: p.AddPassive(o.passive); break;
                case Offer.Kind.Evolve:
                    {
                        var w = p.GetWeapon(o.weapon);
                        if (w != null) { w.evolved = true; w.Removed(); }
                        break;
                    }
                case Offer.Kind.Heal: p.Heal(50f); break;
                case Offer.Kind.Coins: p.RunCoins += 25; break;
            }
        }

        private void ShowChest()
        {
            pendingChests--;
            State = GameState.Chest;
            Time.timeScale = 0f;
            var p = G.Player;
            var rewards = new List<Offer>();
            var evo = p.Evolvable();
            if (evo != null)
            {
                rewards.Add(new Offer
                {
                    kind = Offer.Kind.Evolve, weapon = evo.Id, title = evo.def.evoName, icon = evo.def.evoIcon, tag = "進化！", desc = evo.def.evoDesc,
                    tagColor = new Color(1f, 0.6f, 0.3f)
                });
                G.Sfx.Play(SfxId.Evolve, 0.9f);
            }
            else
            {
                float r = Random.value;
                int rolls = r < 0.68f ? 1 : r < 0.95f ? 3 : 5;
                for (int i = 0; i < rolls; i++)
                {
                    var o = RandomUpgrade();
                    if (o == null) break;
                    rewards.Add(o);
                    Apply(o);
                }
                if (rewards.Count == 0)
                {
                    var coins = new Offer { kind = Offer.Kind.Coins, title = "チップ", icon = "coin", tag = "€ +25", desc = "コインを25枚もらう。", tagColor = UiKit.Gold };
                    rewards.Add(coins);
                    Apply(coins);
                }
                G.Sfx.Play(SfxId.Chest, 0.9f);
            }
            if (evo != null) Apply(rewards[0]);
            Menus.ShowChest(rewards, Resume);
        }

        private Offer RandomUpgrade()
        {
            var p = G.Player;
            var options = new List<Offer>();
            foreach (var w in p.Weapons)
                if (!w.MaxedOut)
                    options.Add(new Offer { kind = Offer.Kind.UpWeapon, weapon = w.Id, title = w.DisplayName, icon = w.evolved ? w.def.evoIcon : w.def.icon, tag = $"Lv{w.level + 1}", desc = w.def.levelText[w.level - 1], tagColor = UiKit.Gold });
            foreach (var id in p.Passives)
            {
                var def = GameData.Passive(id);
                int lv = p.PassiveLevels[(int)id];
                if (lv < def.maxLevel)
                    options.Add(new Offer { kind = Offer.Kind.UpPassive, passive = id, title = def.name, icon = def.icon, tag = $"Lv{lv + 1}", desc = def.desc, tagColor = UiKit.Gold });
            }
            return options.Count > 0 ? options[Random.Range(0, options.Count)] : null;
        }

        // ---------------- ends ----------------

        public void OnBossSpawned() => Music.PlayTheme(4);

        public void OnBossDefeated(Enemy boss)
        {
            if (bossDefeatedAt >= 0f) return;
            bossDefeatedAt = G.RunTime;
            G.Cam.Shake(0.8f);
            G.Sfx.Play(SfxId.Evolve, 0.9f, 0.8f);
            G.Fx.Banner(G.Stage.bossName + " を追い払った！", UiKit.Gold);
            G.Enemies.RoutAll(G.Player.Position, 999f);
        }

        public void OnBossGone(Enemy boss) { }

        private IEnumerator Clear()
        {
            ending = true;
            G.Player.God = true;
            yield return new WaitForSecondsRealtime(0.5f);
            State = GameState.Cleared;
            Time.timeScale = 0f;
            SaveData.Cleared[StageIndex] = true;
            SaveData.StagesUnlocked = Mathf.Clamp(Mathf.Max(SaveData.StagesUnlocked, StageIndex + 2), 1, 3);
            int earned = EndRunRewards(true);
            Music.Duck(true);
            G.Sfx.Play(SfxId.Chest, 0.9f, 0.9f);
            System.Action next = StageIndex < GameData.Stages.Length - 1 ? () => StartRun(StageIndex + 1, SelectedCharacter) : (System.Action)null;
            Menus.ShowResult(true, earned, damage, () => StartRun(StageIndex, SelectedCharacter), next, GoTitle);
        }

        public void OnPlayerDied()
        {
            if (ending) return;
            StartCoroutine(Die());
        }

        private IEnumerator Die()
        {
            ending = true;
            State = GameState.GameOver;
            Time.timeScale = 0.25f * TimeMultiplier;
            G.Sfx.Play(SfxId.Boss, 0.8f, 0.6f);
            G.Fx.Banner("Mamma mia...", new Color(1f, 0.5f, 0.45f));
            yield return new WaitForSecondsRealtime(1.4f);
            Time.timeScale = 0f;
            int earned = EndRunRewards(false);
            Music.Duck(true);
            Menus.ShowResult(false, earned, damage, () => StartRun(StageIndex, SelectedCharacter), null, GoTitle);
        }

        /// <summary>Converts the run into permanent coins and records. Returns coins earned.</summary>
        private int EndRunRewards(bool cleared)
        {
            var p = G.Player;
            if (p == null) return 0;
            float minutes = G.RunTime / 60f;
            int earned = p.RunCoins + G.Enemies.Kills / 20 + Mathf.FloorToInt(minutes * 4f) + (cleared ? 150 + 100 * StageIndex : 0);
            earned = Mathf.RoundToInt(earned * p.GreedMul);
            SaveData.Coins += earned;
            SaveData.TotalKills += G.Enemies.Kills;
            SaveData.BestTime[StageIndex] = Mathf.Max(SaveData.BestTime[StageIndex], G.RunTime);
            SaveData.Save();
            p.RunCoins = 0;
            return earned;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
