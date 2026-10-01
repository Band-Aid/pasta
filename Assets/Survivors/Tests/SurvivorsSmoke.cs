#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>
    /// Opt-in integration run for development builds: PastaSurvivors.exe -survivors-smoke.
    /// Plays through title, all three stages, level-ups, chests/evolutions, bosses, pause and game over,
    /// capturing screenshots and a results file next to the executable. Uses a separate save slot.
    /// </summary>
    public class SurvivorsSmoke : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private readonly List<string> errors = new List<string>();
        private readonly StringBuilder perf = new StringBuilder();
        private string output;
        private bool done;
        private float moveAngle;
        /// <summary>Dismiss level-ups and chests that pop up while the script is doing something else.</summary>
        private bool autoDismiss, circling;

        private static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "-survivors-smoke") >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            if (!Requested) return;
            SaveData.Prefix = "pstest.";
            SaveData.Wipe();
            SurvivorsGame.AutoPauseOnFocusLoss = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!Requested) return;
            new GameObject("Survivors smoke test").AddComponent<SurvivorsSmoke>();
        }

        private void Awake()
        {
            output = Path.Combine(Application.dataPath, "..", "Verification");
            Directory.CreateDirectory(output);
            foreach (var f in Directory.GetFiles(output, "*.png")) File.Delete(f);
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            StartCoroutine(Run());
            StartCoroutine(Watchdog());
        }

        private void OnLog(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(text + "\n" + stack);
        }

        private void Check(bool ok, string what)
        {
            checks.Add((ok ? "PASS " : "FAIL ") + what);
            if (!ok) errors.Add("Check failed: " + what);
        }

        private void Update()
        {
            var game = G.Game;
            if (autoDismiss && game != null && (game.State == GameState.LevelUp || game.State == GameState.Chest) && game.Menus.Nav.items.Count > 0)
                game.Menus.Nav.items[game.State == GameState.Chest ? game.Menus.Nav.items.Count - 1 : 0].act?.Invoke();
            if (circling)
            {
                moveAngle += Time.unscaledDeltaTime * 0.7f;
                Controls.TestMove = new Vector2(Mathf.Cos(moveAngle), Mathf.Sin(moveAngle)) * 0.9f;
            }
        }

        private static IEnumerator Real(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
            yield return null;
        }

        private IEnumerator Fps(string label, float seconds)
        {
            int frames = 0;
            float start = Time.realtimeSinceStartup, worst = 0f;
            float last = start;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                frames++;
                float now = Time.realtimeSinceStartup;
                worst = Mathf.Max(worst, now - last);
                last = now;
            }
            float avg = frames / (Time.realtimeSinceStartup - start);
            perf.AppendLine($"{label}: {avg:0.0} fps avg, worst frame {worst * 1000f:0} ms, enemies {G.Enemies.HostileCount}, shots {G.Shots.Active.Count}, pickups {G.Pickups.Active.Count}");
        }

        private void Pick(int index)
        {
            var nav = G.Game.Menus.Nav;
            if (index < nav.items.Count) nav.items[index].act?.Invoke();
        }

        /// <summary>Back to a known loadout so later checks don't depend on random level-up picks.</summary>
        private static void ResetKit(WeaponId start)
        {
            var p = G.Player;
            foreach (var w in p.Weapons) w.Removed();
            p.Weapons.Clear();
            p.Passives.Clear();
            Array.Clear(p.PassiveLevels, 0, p.PassiveLevels.Length);
            p.RecalcStats();
            p.AddWeapon(start);
        }

        private void MaxOut(params WeaponId[] ids)
        {
            var p = G.Player;
            // Make room for every evolution partner (random level-up picks may have filled passive slots).
            var partners = new HashSet<PassiveId>();
            foreach (var id in ids) partners.Add(GameData.Weapon(id).evoPartner);
            foreach (var w in p.Weapons) partners.Add(w.def.evoPartner);
            for (int i = p.Passives.Count - 1; i >= 0; i--)
                if (!partners.Contains(p.Passives[i])) { p.PassiveLevels[(int)p.Passives[i]] = 0; p.Passives.RemoveAt(i); }
            p.RecalcStats();
            foreach (var id in ids)
            {
                p.AddWeapon(id);
                var w = p.GetWeapon(id);
                if (w != null) w.level = WeaponDef.MaxLevel;
                var partner = GameData.Weapon(id).evoPartner;
                if (p.PassiveLevels[(int)partner] == 0) p.AddPassive(partner);
            }
        }

        private IEnumerator Run()
        {
            yield return Real(2f);
            var game = G.Game;
            Check(game != null && game.State == GameState.Title, "Boots into the title screen");
            Check(G.Enemies.Active.Count >= 8, "Title shows a crowd of Italians");
            yield return Shot("01-title");

            game.Menus.ShowCharacters();
            yield return Real(0.6f);
            yield return Shot("02-characters");
            game.Menus.ShowStages();
            yield return Real(0.6f);
            Check(game.Menus.Nav.items.Count == 3 && game.Menus.Nav.items[0].enabled && !game.Menus.Nav.items[1].enabled, "Only stage 1 is unlocked on a fresh save");
            yield return Shot("03-stages");
            SaveData.Coins = 500;
            game.Menus.ShowShop();
            yield return Real(0.6f);
            Pick(0);
            yield return Real(0.3f);
            Check(SaveData.Shop[0] == 1 && SaveData.Coins == 440, "Shop purchase spends coins and raises the level");
            yield return Shot("04-shop");

            // First person chosen on the title screen must carry into the run.
            game.GoTitle();
            yield return Real(0.5f);
            Controls.TestToggleView = true;
            yield return Real(0.3f);
            Check(SaveData.FirstPerson && game.Menus.Current == "title", "V on the title screen selects first person");
            game.StartRun(0, 0);
            yield return Real(1f);
            Check(G.Cam.firstPerson && G.Player.FirstPerson, "Run started from the title honours the first-person setting");
            yield return Shot("04b-fps-from-title");
            Controls.TestToggleView = true;
            yield return Real(0.3f);
            Check(!G.Cam.firstPerson && !SaveData.FirstPerson, "V during play switches back to top-down");
            Controls.TestToggleView = true;
            yield return Real(0.3f);
            Check(G.Cam.firstPerson && SaveData.FirstPerson, "V during play switches to first person");
            SaveData.ViewMode = 0;
            game.ApplyViewMode();
            game.GoTitle();
            yield return Real(0.5f);

            // ---------------- Stage 1: Roma ----------------
            game.StartRun(0, 0);
            yield return Real(1.5f);
            Check(game.State == GameState.Playing && G.Player != null && G.Player.Weapons.Count == 1, "Run starts with the character's weapon");
            Check(G.Player.Might > 1.04f, "Permanent shop bonus applies to the run");
            G.Player.God = true;
            circling = true;
            Controls.TestMove = Vector2.right;
            autoDismiss = true;

            // Map strategy: cover, areas, pedestals, and Italians that path around walls.
            var arena = G.Arena;
            Check(arena.walls.Count >= 20 && arena.specialSpots.Count >= 5 && arena.areas.Exists(a => a.kind == Arena.AreaKind.Heal)
                && arena.areas.Exists(a => a.kind == Arena.AreaKind.Market) && arena.roads.Count == 1, "Roma has walls, pedestals, heal and market areas and a road");
            Check(!arena.LineOfSight(new Vector3(-40f, 0, 17f), new Vector3(-40f, 0, 33f)), "A house blocks line of sight");
            var fountain = arena.areas.Find(a => a.kind == Arena.AreaKind.Heal);
            float drawn = arena.DrawHeal(new Vector3(fountain.c.x, 0f, fountain.c.y), 1000f);
            Check(drawn > 0f && drawn <= Arena.HealCapacity && arena.DrawHeal(new Vector3(fountain.c.x, 0f, fountain.c.y), 10f) == 0f, "Healing fountain runs dry and has to refill");
            yield return Real(7f);
            Check(G.Enemies.Kills > 0, "Spaghetti snap drives Italians away (kills " + G.Enemies.Kills + ")");
            float hpAtLevel = G.Enemies.HpMul, dmgAtLevel = G.Enemies.DamageMul;
            int realLevel = G.Player.Level;
            G.Player.Level += 20;
            yield return Real(0.2f);
            Check(G.Enemies.HpMul > hpAtLevel * 1.5f && G.Enemies.DamageMul > dmgAtLevel * 1.15f && G.Enemies.BossHpMul < G.Enemies.HpMul,
                $"Italians toughen with the player's level (HP x{hpAtLevel:0.00} -> x{G.Enemies.HpMul:0.00})");
            G.Player.Level = realLevel;
            Check(G.Pickups.Active.Count > 0 || G.Player.Xp > 0 || G.Player.Level > 1, "Italians drop experience");
            yield return Shot("05-roma-early");

            // An Italian behind a house must walk around it to reach the player.
            circling = false;
            Controls.TestMove = Vector2.zero;
            G.Player.transform.position = new Vector3(-40f, 0f, 32.2f);
            var walker = G.Enemies.Spawn(EnemyKind.Tifoso, new Vector3(-40f, 0f, 16.5f), 5000f);
            float bestDist = 99f;
            for (int i = 0; i < 90 && walker != null && walker.active && bestDist > 2f; i++)
            {
                yield return Real(0.2f);
                bestDist = Mathf.Min(bestDist, Vector3.Distance(walker.pos, G.Player.Position));
            }
            Check(bestDist < 2.5f, $"Flow field routes an Italian around a house (closest {bestDist:0.0} m)");
            yield return Shot("05b-alley-pathing");

            // Mouse: aim at the cursor, hold the button to walk there.
            var start = G.Player.Position;
            Controls.TestMouseWorld = start + new Vector3(8f, 0f, 0f);
            yield return Real(0.3f);
            Check(G.Player.MouseAiming && G.Player.AimDir.x > 0.9f, "Main weapon aims at the mouse cursor");
            Controls.TestMouseHeld = true;
            yield return Real(0.8f);
            Check(G.Player.Position.x > start.x + 2f, "Holding the mouse button walks toward the cursor");
            Controls.TestMouseHeld = false;

            // Special weapons: pick each one up from a pedestal and fire it.
            foreach (SpecialId id in System.Enum.GetValues(typeof(SpecialId)))
            {
                G.Player.transform.position = new Vector3(-14f + (int)id * 7f, 0f, -14f);
                Controls.TestMouseWorld = G.Player.Position + new Vector3(0f, 0f, -9f);
                G.Pickups.SpawnSpecial(id, G.Player.Position + new Vector3(0.5f, 0f, 0f));
                Enemy victim = id == SpecialId.StarBeam ? G.Enemies.Spawn(EnemyKind.Signore, G.Player.Position + new Vector3(0f, 0f, -6f), 50f) : null;
                yield return Real(0.6f); // a freshly picked-up special arms after 0.4 s
                bool got = G.Player.Special.HasValue && G.Player.Special.Value == id;
                Controls.TestSpecial = true;
                yield return Real(0.25f);
                Check(got && G.Player.SpecialCd > 0f, "Special weapon picked up and used: " + GameData.Special(id).name);
                if (victim != null)
                {
                    yield return Real(0.4f);
                    Check(victim.fear > 0f || !victim.active || victim.fleeing, "Sta○ Beam scares the Italians it hits");
                    yield return Shot("05c-special-" + id);
                    yield return Real(2.5f);
                    continue;
                }
                yield return Real(id == SpecialId.Bazooka ? 0.3f : 0.9f);
                if (id == SpecialId.Bazooka || id == SpecialId.Sprinkler || id == SpecialId.Parmesan) yield return Shot("05c-special-" + id);
            }
            Controls.TestMouseWorld = null;

            // Ketchup bombs go for the thickest crowd.
            var crowd = new Vector3(-4f, 0f, -20f);
            G.Player.transform.position = crowd + new Vector3(6f, 0f, 0f);
            for (int i = 0; i < 7; i++) G.Enemies.Spawn(EnemyKind.Signore, crowd + new Vector3((i % 3 - 1) * 0.9f, 0f, (i / 3 - 1) * 0.9f), 50f);
            G.Enemies.Spawn(EnemyKind.Signore, crowd + new Vector3(10f, 0f, 6f), 50f);
            yield return null;
            int onCrowd = 0;
            for (int i = 0; i < 10; i++)
            {
                var pick = G.Enemies.DensestNear(G.Player.Position, 12f, 2.3f, 14, null);
                if (pick != null && Vector3.Distance(pick.pos, crowd) < 3f) onCrowd++;
            }
            Check(onCrowd >= 8, $"Ketchup bombs target the densest crowd ({onCrowd}/10)");
            circling = true;

            autoDismiss = false;
            for (int i = 0; i < 20 && game.State != GameState.Playing; i++) { Pick(0); yield return Real(0.3f); }
            G.Player.AddXp(G.Player.XpNeeded + 1);
            yield return Real(0.8f);
            Check(game.State == GameState.LevelUp && game.Menus.Nav.items.Count >= 3, "Level up offers at least three choices");
            yield return Shot("06-levelup");
            int before = G.Player.Weapons.Count + G.Player.Passives.Count;
            int lvBefore = 0; foreach (var w in G.Player.Weapons) lvBefore += w.level;
            foreach (var id in G.Player.Passives) lvBefore += G.Player.PassiveLevels[(int)id];
            Pick(0);
            yield return Real(0.4f);
            int lvAfter = 0; foreach (var w in G.Player.Weapons) lvAfter += w.level;
            foreach (var id in G.Player.Passives) lvAfter += G.Player.PassiveLevels[(int)id];
            Check(game.State == GameState.Playing && (lvAfter > lvBefore || G.Player.Weapons.Count + G.Player.Passives.Count > before), "Choosing an offer applies it and resumes");

            autoDismiss = true;
            // Every weapon type fires without errors.
            ResetKit(WeaponId.SpaghettiSnap);
            G.Player.AddWeapon(WeaponId.PenneShot);
            G.Player.AddWeapon(WeaponId.LasagnaCrash);
            G.Player.AddWeapon(WeaponId.FusilliOrbit);
            G.Player.AddWeapon(WeaponId.FarfalleBoomerang);
            G.Player.AddWeapon(WeaponId.KetchupBomb);
            Check(G.Player.Weapons.Count == 6, "Six weapon slots fill up");
            G.RunTime = 300f;
            yield return Real(6f);
            yield return Shot("07-roma-six-weapons");
            yield return Fps("Roma 5:00 six weapons", 3f);

            // Main weapon switching.
            Check(G.Player.MainIndex == 0 && G.Player.Main == G.Player.Weapons[0], "Starting weapon is the main weapon");
            Controls.TestSwitch = 1;
            yield return Real(0.3f);
            Check(G.Player.MainIndex == 1, "Next-weapon input switches the main weapon");
            Controls.TestSwitch = -1;
            yield return Real(0.3f);
            Controls.TestSwitch = -1;
            yield return Real(0.3f);
            Check(G.Player.MainIndex == G.Player.Weapons.Count - 1, "Switching wraps around the weapon slots");
            G.Player.SetMain(3);
            yield return Real(1.5f);
            Check(G.Player.Main.Id == WeaponId.FusilliOrbit, "Direct main weapon selection");
            yield return Shot("07b-main-switch");

            // First person.
            SaveData.ViewMode = 1;
            game.ApplyViewMode();
            G.Player.SetMain(0);
            Controls.TestLook = new Vector2(35f, 0f);
            yield return Real(3f);
            Check(G.Cam.firstPerson && G.Player.FirstPerson && G.Cam.Cam.fieldOfView > 70f, "First-person mode moves the camera to the player's eyes");
            yield return Shot("07c-fps-spaghetti");
            Controls.TestLook = new Vector2(-20f, 0f);
            G.Player.SetMain(1);
            yield return Real(2.5f);
            yield return Shot("07d-fps-penne");
            Controls.TestLook = null;
            game.ToggleViewMode();
            yield return Real(0.5f);
            Check(!G.Cam.firstPerson && !SaveData.FirstPerson, "View toggles back to top-down");
            G.Player.SetMain(0);

            MaxOut(WeaponId.SpaghettiSnap, WeaponId.PenneShot, WeaponId.LasagnaCrash, WeaponId.FusilliOrbit, WeaponId.FarfalleBoomerang, WeaponId.KetchupBomb);
            Check(G.Player.Evolvable() != null, "Max level weapon with its partner passive is evolvable");
            autoDismiss = false;
            while (game.State == GameState.LevelUp) { Pick(0); yield return Real(0.2f); }
            game.OpenChest();
            yield return Real(0.8f);
            while (game.State == GameState.LevelUp) { Pick(0); yield return Real(0.4f); }
            Check(game.State == GameState.Chest, "Treasure chest screen opens");
            yield return Shot("08-evolution");
            Pick(0);
            yield return Real(0.3f);
            int evolved = 0;
            for (int i = 0; i < 6; i++)
            {
                game.OpenChest();
                yield return Real(0.5f);
                Pick(0);
                yield return Real(0.3f);
            }
            autoDismiss = true;
            yield return Real(1f);
            foreach (var w in G.Player.Weapons) if (w.evolved) evolved++;
            Check(evolved == 6, "All six weapons evolve through chests (" + evolved + ")");
            G.RunTime = 480f;
            yield return Real(6f);
            yield return Shot("09-roma-evolved");
            yield return Fps("Roma 8:00 six evolved weapons", 4f);

            var nonna = G.Enemies.Spawn(EnemyKind.Nonna, G.Player.Position + Vector3.forward * 6f);
            Check(nonna != null, "Nonna elite spawns");
            yield return Real(1f);
            yield return Shot("10-nonna");

            Check(G.Hazards.CarsDriven > 0, $"Traffic drives across the Roman road ({G.Hazards.CarsDriven} cars)");
            G.RunTime = G.Stage.duration * G.Waves.TimeScale - 0.5f;
            yield return Real(3f);
            Check(G.Waves.BossSpawned && G.Enemies.Boss != null, "Boss arrives at the end of the timer");
            if (G.Enemies.Boss != null)
            {
                G.Enemies.Boss.pos = G.Player.Position + Vector3.forward * 8f;
                G.Enemies.Boss.ApplyTransform();
            }
            yield return Real(2.5f);
            yield return Shot("11-roma-boss");
            if (G.Enemies.Boss != null) G.Enemies.Damage(G.Enemies.Boss, 1e7f, Vector3.forward, 0f, 0);
            yield return Real(2.4f);
            var bike = G.Enemies.Boss;
            Check(game.State == GameState.Playing && bike != null && bike.def.kind == EnemyKind.BossBikeNonna,
                "Grande Nonna grows wheels and comes back as Bike Nonna");
            // Spaghetti and the evolved quake stun on hit and would keep her parked; Penne lets her ride.
            ResetKit(WeaponId.PenneShot);
            if (bike != null)
            {
                bike.pos = G.Player.Position + Vector3.forward * 8f;
                bike.ApplyTransform();
            }
            yield return Real(0.6f);
            yield return Shot("11b-roma-bike-nonna");
            yield return Real(3.5f);
            Check(bike != null && bike.pattern >= 0, "Bike Nonna rides at the player");
            yield return Shot("11c-roma-bike-ride");
            if (G.Enemies.Boss != null) G.Enemies.Damage(G.Enemies.Boss, 1e7f, Vector3.forward, 0f, 0);
            yield return Real(5.5f);
            Check(game.State == GameState.Cleared, "Defeating the boss clears the stage");
            Check(SaveData.StagesUnlocked >= 2 && SaveData.Cleared[0], "Clearing Roma unlocks Venezia");
            yield return Shot("12-roma-clear");

            // ---------------- Stage 2: Venezia ----------------
            game.StartRun(1, 1);
            G.Player.God = true;
            yield return Real(2.5f);
            yield return Shot("13-venezia-start");
            SaveData.ViewMode = 1;
            game.ApplyViewMode();
            Controls.TestLook = new Vector2(25f, 0f);
            yield return Real(2.5f);
            yield return Shot("13b-venezia-fps");
            Controls.TestLook = null;
            SaveData.ViewMode = 0;
            game.ApplyViewMode();
            ResetKit(WeaponId.PenneShot);
            G.Player.AddWeapon(WeaponId.PineapplePizza);
            G.Player.AddWeapon(WeaponId.CarbonaraAura);
            G.RunTime = 240f;
            yield return Real(6f);
            yield return Shot("14-venezia-pizza-cream");
            MaxOut(WeaponId.PineapplePizza, WeaponId.CarbonaraAura);
            game.OpenChest(); yield return Real(0.5f); Pick(0); yield return Real(0.3f);
            game.OpenChest(); yield return Real(0.5f); Pick(0); yield return Real(0.3f);
            Check(G.Player.GetWeapon(WeaponId.PineapplePizza).evolved && G.Player.GetWeapon(WeaponId.CarbonaraAura).evolved, "Pizza and carbonara evolve");
            yield return Real(4f);
            yield return Shot("15-venezia-evolved");
            G.RunTime = G.Stage.duration * G.Waves.TimeScale - 0.5f;
            yield return Real(3f);
            if (G.Enemies.Boss != null) { G.Enemies.Boss.pos = G.Player.Position + Vector3.forward * 7f; }
            yield return Real(2.5f);
            Check(G.Enemies.Boss != null && G.Enemies.Boss.def.kind == EnemyKind.BossCapitano, "Venezia boss is Il Capitano");
            yield return Shot("16-venezia-boss");

            // ---------------- Stage 3: Napoli ----------------
            game.StartRun(2, 2);
            G.Player.God = true;
            yield return Real(2.5f);
            yield return Shot("17-napoli-start");
            SaveData.ViewMode = 1;
            game.ApplyViewMode();
            Controls.TestLook = new Vector2(30f, 0f);
            yield return Real(2.5f);
            yield return Shot("17b-napoli-fps");
            Controls.TestLook = new Vector2(0f, 0f);
            G.Player.LookYaw = 180f;
            yield return Real(0.5f);
            yield return Shot("17c-napoli-fps-sea");
            Controls.TestLook = null;
            SaveData.ViewMode = 0;
            game.ApplyViewMode();
            G.RunTime = 420f;
            yield return Real(7f);
            yield return Shot("18-napoli-horde");
            yield return Fps("Napoli 7:00 starting kit", 3f);
            G.RunTime = G.Stage.duration * G.Waves.TimeScale - 0.5f;
            yield return Real(3f);
            if (G.Enemies.Boss != null) { G.Enemies.Boss.pos = G.Player.Position + Vector3.forward * 8f; }
            yield return Real(3f);
            Check(G.Enemies.Boss != null && G.Enemies.Boss.def.kind == EnemyKind.BossDon, "Napoli boss is Don Carbonara");
            for (int i = 0; i < 40 && G.Hazards.BombsDropped == 0; i++) yield return Real(0.5f);
            Check(G.Hazards.BombsDropped > 0, $"Vesuvius ash bombs fall in Napoli ({G.Hazards.BombsDropped})");
            yield return Shot("19-napoli-boss");

            // Game over flow.
            G.Player.God = false;
            G.Player.Revivals = 0;
            G.Player.TakeDamage(99999f, G.Player.Position);
            yield return Real(2.5f);
            Check(game.State == GameState.GameOver && game.Menus.Current == "result", "Dying shows the result screen");
            yield return Shot("20-gameover");

            // Pause, then back to title.
            Pick(0);
            yield return Real(1f);
            Check(game.State == GameState.Playing && G.Player.Level == 1, "Retry restarts the stage cleanly");
            game.Pause();
            yield return Real(0.6f);
            Check(game.State == GameState.Paused && Time.timeScale == 0f, "Pause freezes time");
            yield return Shot("21-pause");
            game.Resume();
            yield return Real(0.3f);
            Check(Time.timeScale == 1f, "Resume restores time");
            circling = false;
            Controls.TestMove = null;
            game.GoTitle();
            yield return Real(1.5f);
            Check(game.State == GameState.Title, "Returns to title");
            yield return Shot("22-title-again");
            Finish();
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(420f);
            if (!done)
            {
                errors.Add("Watchdog timeout");
                Finish();
            }
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            var sb = new StringBuilder();
            sb.AppendLine("Pasta La Vista Survivors — smoke test");
            sb.AppendLine($"Unity {Application.unityVersion}  {SystemInfo.graphicsDeviceType}  {Screen.width}x{Screen.height}");
            sb.AppendLine();
            foreach (var c in checks) sb.AppendLine(c);
            sb.AppendLine();
            sb.AppendLine("Performance");
            sb.Append(perf);
            sb.AppendLine();
            sb.AppendLine("ERRORS: " + errors.Count);
            foreach (var e in errors) sb.AppendLine(e);
            File.WriteAllText(Path.Combine(output, "results.txt"), sb.ToString());
            SaveData.Wipe();
            Application.Quit();
        }
    }
}
#endif
