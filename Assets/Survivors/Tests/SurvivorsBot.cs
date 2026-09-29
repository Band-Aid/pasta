#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>
    /// Balance probe for development builds: PastaSurvivors.exe -survivors-bot [stage] [speed] [character].
    /// A simple kiting bot plays one stage without invincibility, picks upgrades with a greedy heuristic,
    /// and logs its progression so difficulty can be tuned with numbers instead of guesses.
    /// </summary>
    public class SurvivorsBot : MonoBehaviour
    {
        private int stage, character, shopLevel;
        private float speed = 4f;
        private readonly StringBuilder log = new StringBuilder();
        private float nextSample;
        private string output;
        private bool finished;
        private Vector3 smoothed;
        private float orbitSign = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-survivors-bot") < 0) return;
            SaveData.Prefix = "psbot.";
            SaveData.Wipe();
            SurvivorsGame.AutoPauseOnFocusLoss = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-survivors-bot");
            if (i < 0) return;
            var bot = new GameObject("Survivors bot").AddComponent<SurvivorsBot>();
            if (i + 1 < args.Length) int.TryParse(args[i + 1], out bot.stage);
            if (i + 2 < args.Length) float.TryParse(args[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out bot.speed);
            if (i + 3 < args.Length) int.TryParse(args[i + 3], out bot.character);
            if (i + 4 < args.Length) int.TryParse(args[i + 4], out bot.shopLevel);
        }

        private IEnumerator Start()
        {
            output = Path.Combine(Application.dataPath, "..", "Verification");
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(1.5f);
            SaveData.StagesUnlocked = 3;
            for (int s = 0; s < SaveData.Shop.Length; s++) SaveData.Shop[s] = Mathf.Min(shopLevel, GameData.Shop[s].maxLevel);
            if (shopLevel < 3) SaveData.Shop[7] = 0;
            SurvivorsGame.TimeMultiplier = speed;
            G.Game.StartRun(stage, character);
            log.AppendLine($"Bot run: stage {stage + 1} ({G.Stage.name}), character {GameData.Characters[character].name}, speed x{speed}, shop Lv{shopLevel}");
            log.AppendLine("time   lv  hp/max    kills alive  fps  weapons");
        }

        private void Update()
        {
            var game = G.Game;
            if (game == null || finished) return;
            switch (game.State)
            {
                case GameState.Playing:
                    Steer();
                    if (G.RunTime >= nextSample)
                    {
                        nextSample += 30f;
                        Sample();
                    }
                    break;
                case GameState.LevelUp:
                    ChooseOffer();
                    break;
                case GameState.Chest:
                    if (game.Menus.Nav.items.Count > 0) game.Menus.Nav.items[game.Menus.Nav.items.Count - 1].act?.Invoke();
                    break;
                case GameState.GameOver:
                case GameState.Cleared:
                    if (game.Menus.Current == "result") Finish(game.State == GameState.Cleared);
                    break;
            }
        }

        private void Sample()
        {
            var p = G.Player;
            var sb = new StringBuilder();
            foreach (var w in p.Weapons) sb.Append($"{w.def.icon}{w.level}{(w.evolved ? "*" : "")} ");
            foreach (var id in p.Passives) sb.Append($"[{GameData.Passive(id).icon}{p.PassiveLevels[(int)id]}]");
            log.AppendLine($"{UiKit.Clock(G.RunTime)}  {p.Level,3} {p.Hp,4:0}/{p.MaxHp,-4:0} {G.Enemies.Kills,6} {G.Enemies.HostileCount,5} {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime),4:0}  {sb}");
        }

        private void ChooseOffer()
        {
            var nav = G.Game.Menus.Nav;
            if (nav.items.Count == 0) return;
            var offers = G.Game.LastOffers;
            if (offers == null || offers.Count != nav.items.Count) { nav.items[0].act?.Invoke(); return; }
            var p = G.Player;
            int best = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                float score = 1f;
                switch (o.kind)
                {
                    case Offer.Kind.UpWeapon: score = 3f + p.GetWeapon(o.weapon).level * 0.15f; break;
                    case Offer.Kind.NewWeapon: score = p.Weapons.Count < 3 ? 3.5f : 1.1f; break;
                    case Offer.Kind.NewPassive:
                    case Offer.Kind.UpPassive:
                        score = 1.5f;
                        foreach (var w in p.Weapons) if (w.def.evoPartner == o.passive) score = o.kind == Offer.Kind.NewPassive ? 3.2f : 2f;
                        if (o.passive == PassiveId.Basil && p.Hp < p.MaxHp * 0.5f) score += 1f;
                        break;
                }
                if (score > bestScore) { bestScore = score; best = i; }
            }
            nav.items[best].act?.Invoke();
        }

        private void Steer()
        {
            var p = G.Player;
            Vector3 pos = p.Position;
            Vector3 flee = Vector3.zero;
            int near = 0;
            foreach (var e in G.Enemies.Active)
            {
                if (!e.active || e.fleeing || e.IsProp) continue;
                Vector3 d = pos - e.pos; d.y = 0f;
                float dist = d.magnitude;
                if (dist > 9f || dist < 0.001f) continue;
                float w = (e.IsBoss ? 5f : 1f) / Mathf.Max(0.3f, dist * dist) * (dist < 4.5f ? 1f : 0.35f);
                flee += d / dist * w;
                if (dist < 2f) near++;
            }
            // Sidestep telegraphed charges: push perpendicular out of any locked lane ahead of a charger.
            foreach (var e in G.Enemies.Active)
            {
                if (!e.active || e.fleeing || (e.state != 1 && e.state != 2)) continue;
                if (e.def.behavior == Behavior.Sweeper || (e.IsBoss && e.def.kind == EnemyKind.BossCapitano))
                {
                    // Back out of a telegraphed oar sweep.
                    Vector3 away = pos - e.pos; away.y = 0f;
                    if (away.magnitude < e.def.attackRange + 1.5f) flee += away.normalized * 8f;
                    continue;
                }
                if (e.def.behavior != Behavior.Charger && !e.IsBoss) continue;
                Vector3 rel = pos - e.pos; rel.y = 0f;
                float along = Vector3.Dot(rel, e.lockDir);
                if (along < -1f || along > 25f) continue;
                Vector3 perp = rel - e.lockDir * along;
                float half = e.radius + 1.6f;
                if (perp.magnitude < half) flee += (perp.sqrMagnitude > 0.01f ? perp.normalized : Vector3.Cross(Vector3.up, e.lockDir)) * 6f;
            }
            foreach (var s in G.Shots.Active)
            {
                if (!s.hostile) continue;
                Vector3 d = pos - s.pos; d.y = 0f;
                if (d.magnitude < 4f) flee += d.normalized * 1.5f / Mathf.Max(0.3f, d.sqrMagnitude);
            }
            // Don't get pinned against fountains, tables and walls.
            foreach (var o in G.Arena.obstacles)
            {
                Vector3 d = pos - new Vector3(o.c.x, 0f, o.c.y);
                float gap = d.magnitude - o.r;
                if (gap < 3f) flee += d.normalized * (3f - gap) * 0.6f;
            }
            foreach (var w in G.Arena.walls)
            {
                float qx = Mathf.Clamp(pos.x, w.min.x, w.max.x), qz = Mathf.Clamp(pos.z, w.min.y, w.max.y);
                var d = new Vector3(pos.x - qx, 0f, pos.z - qz);
                float gap = d.magnitude;
                if (gap < 2.5f && gap > 0.001f) flee += d / gap * (2.5f - gap) * 0.8f;
            }
            float edge = Mathf.Min(Mathf.Min(pos.x - G.Arena.min.x, G.Arena.max.x - pos.x), Mathf.Min(pos.z - G.Arena.min.y, G.Arena.max.y - pos.z));
            // Head for the nearest pickup when it's calm-ish.
            Vector3 seek = Vector3.zero;
            float bestD = 12f;
            foreach (var pk in G.Pickups.Active)
            {
                float d = Vector3.Distance(pk.pos, pos);
                float want = pk.kind == PickupKind.Chest || pk.kind == PickupKind.Pizza ? d * 0.5f : d;
                if (want < bestD) { bestD = want; seek = (pk.pos - pos).normalized; }
            }
            // Circle around the arena centre, away from walls.
            Vector3 center = G.Arena.spawn;
            Vector3 toC = center - pos; toC.y = 0f;
            Vector3 tangent = Vector3.Cross(Vector3.up, toC.normalized) * orbitSign;
            if (UnityEngine.Random.value < 0.001f) orbitSign = -orbitSign;
            Vector3 dir = flee * 2.2f + seek * (near == 0 ? 1.4f : 0.5f) + tangent * 0.6f + toC.normalized * (Mathf.Clamp01((toC.magnitude - 18f) / 20f) * 1.5f + Mathf.Clamp01((6f - edge) / 6f) * 2f);
            smoothed = Vector3.Lerp(smoothed, dir, 0.25f);
            var m = new Vector2(smoothed.x, smoothed.z);
            Controls.TestMove = m.sqrMagnitude > 0.0001f ? m.normalized : Vector2.zero;
            if (near >= 3 && p.DashReady >= 1f) Controls.TestDash = true;
        }

        private void Finish(bool cleared)
        {
            finished = true;
            Sample();
            foreach (var kv in G.Player.DamageTaken) log.AppendLine($"  damage from {kv.Key}: {kv.Value:0}");
            foreach (var w in G.Player.Weapons)
            {
                G.Game.DamageBySlot.TryGetValue(w.Slot, out var dealt);
                log.AppendLine($"  dealt by {w.def.icon}{w.level}{(w.evolved ? "*" : "")}: {dealt:#,0}");
            }
            log.AppendLine(cleared ? $"RESULT: CLEARED at {UiKit.Clock(G.RunTime)}" : $"RESULT: DIED at {UiKit.Clock(G.RunTime)}");
            File.WriteAllText(Path.Combine(output, $"bot-stage{stage + 1}-c{character}.txt"), log.ToString());
            SaveData.Wipe();
            Application.Quit();
        }
    }
}
#endif
