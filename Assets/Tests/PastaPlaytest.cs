#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Opt-in standalone integration test: PastaLaVista.exe -pasta-smoke. Uses real input events.</summary>
public class PastaPlaytest : MonoBehaviour
{
    private Gamepad pad;
    private readonly List<string> checks = new List<string>();
    private readonly List<string> errors = new List<string>();
    private string output;
    private bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if ((!Debug.isDebugBuild && !Application.isEditor) || Array.IndexOf(Environment.GetCommandLineArgs(), "-pasta-smoke") < 0) return;
        new GameObject("Pasta integration verification").AddComponent<PastaPlaytest>();
    }

    private void Awake()
    {
        output = Path.Combine(Application.dataPath, "..", "Verification");
        Directory.CreateDirectory(output);
        Application.logMessageReceived += Log;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        // Keep desktop mouse movement and keyboard input out of the scripted run.
        // This executable exits when verification finishes; normal launches never disable devices.
        foreach (var device in InputSystem.devices) InputSystem.DisableDevice(device);
        pad = InputSystem.AddDevice<Gamepad>();
        Application.runInBackground = true;
        StartCoroutine(Run());
        StartCoroutine(Watchdog());
    }

    private void Log(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text);
    }

    private void Check(bool condition, string message)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) errors.Add(message);
    }

    private IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }
    private void Trigger(float value) => InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = value });
    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        yield return null;
    }

    private IEnumerator Run()
    {
        yield return Wait(1.1f);
        var gm = GameManager.Instance;
        var hand = PastaHand.Instance;
        var player = PlayerController.Instance;
        var score = ScoreManager.Instance;
        var spawner = EnemySpawner.Instance;
        var time = TimeManager.Instance;
        float normalFixedStep = Time.fixedDeltaTime;
        Check(gm != null && hand != null && player != null && score != null && time != null, "Scene services initialized including TimeManager");
        yield return new WaitForSeconds(0.9f);
        spawner.StopAll();
        gm.SetPaused(false);
        yield return Wait(0.15f);
        var prefab = (Enemy)typeof(EnemySpawner).GetField("enemyPrefab", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
        ClearEnemies();
        yield return null;
        for (int i = 0; i < 9; i++)
        {
            var enemy = Instantiate(prefab, new Vector3((i % 3 - 1) * 1.8f, 0.05f, 4.5f + i / 3 * 3.5f), Quaternion.Euler(0, 180f, 0));
            enemy.Configure(EnemyType.Normal, 0f);
        }
        yield return Wait(0.35f);
        yield return Capture("01-ready");

        // An early release neither fires nor consumes the held bundle.
        var held = hand.Current;
        Trigger(1f); yield return Wait(0.16f);
        Trigger(0f); yield return Wait(0.12f);
        Check(hand.Current == held && !held.IsBroken && Enemy.Alive.Count == 9, "Early release keeps bundle and enemies");

        Trigger(1f);
        yield return Wait(0.6f);
        Check(hand.Charging && hand.Current.QualityAt(hand.Current.CurrentDiff) == 1f, "Actual trigger reaches gold timing window");
        ValidateMesh(hand.Current);
        // Freeze only for the capture to keep the screenshot inside the timing window.
        gm.HitStop(0.15f);
        yield return Capture("02-gold-bend");
        time.ResetTime();
        Trigger(0f);
        yield return Wait(0.13f);
        Check(score.Kills >= 3, "Gold release launches the front row");
        Check(score.PerfectStreak == 1 && score.Combo == 1, "Perfect hit starts streak and combo");
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f, "A snap with enemies remaining stays at normal speed");
        Check(FindObjectsByType<PastaFragment>().Length == 3, "Snap creates three physical, capped fragments");
        yield return Capture("03-snap");
        yield return Wait(0.25f);
        yield return Capture("03a-collision");
        yield return Wait(0.85f);
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f
            && Mathf.Approximately(Time.fixedDeltaTime, normalFixedStep), "Defeats outside an active round do not trigger slow motion");
        Check(score.Kills == 9 && Enemy.Alive.Count == 0, "Flying front row reaches enemies outside the original blast");
        Check(score.DominoKills == 6 && score.BestDomino == 9, "One shared shot counts all six collisions exactly once");
        yield return Capture("03b-domino-chain");

        time.RequestSlowMotion(0.5f);
        yield return Wait(0.2f);
        float previousScale = Time.timeScale;
        time.RequestSlowMotion(0.5f);
        Check(Time.timeScale == previousScale, "Repeated slow motion does not jump back to normal speed");
        yield return Wait(0.45f);
        Check(Mathf.Approximately(Time.timeScale, 0.05f), "Repeated requests extend the slow motion hold");
        gm.HitStop(0.08f);
        yield return Wait(0.12f);
        Check(Mathf.Approximately(Time.timeScale, 0.05f), "Hit stop expiration preserves active slow motion");
        yield return Wait(0.5f);
        time.RequestSlowMotion(2f);
        yield return Wait(0.2f);
        time.enabled = false;
        Check(Time.timeScale == 1f && Mathf.Approximately(Time.fixedDeltaTime, normalFixedStep), "Disabling TimeManager restores time and physics");
        time.enabled = true;

        // Every weapon initializes and builds finite geometry with the same prefab-compatible path.
        hand.SetType(PastaType.Penne);
        yield return Wait(0.1f);
        Trigger(1f); yield return Wait(0.5f);
        ValidateMesh(hand.Current);
        yield return Capture("04-penne");
        Trigger(0f); yield return Wait(0.55f);
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f, "A missed snap does not trigger slow motion");
        hand.SetType(PastaType.Lasagna);
        yield return Wait(0.1f);
        Trigger(1f); yield return Wait(0.73f);
        ValidateMesh(hand.Current);
        yield return Capture("05-lasagna");
        Trigger(0f); yield return Wait(1f);
        hand.SetType(PastaType.Spaghetti);

        Trigger(1f); yield return Wait(0.2f);
        int beforeSwitch = score.Score;
        hand.SetType(PastaType.Penne);
        yield return Wait(0.8f);
        Check(!hand.Charging && !hand.Current.IsBroken && score.Score == beforeSwitch, "Switching cancels charge and requires fresh press");
        Trigger(0f); yield return Wait(0.1f);
        hand.SetType(PastaType.Spaghetti);

        // Holding past the limit must fire only once until the trigger is released.
        Trigger(1f); yield return Wait(1.8f);
        Check(hand.Overbent && !hand.Charging, "Overcharge forces weak snap and waits for release");
        yield return Wait(0.6f);
        Check(!hand.Charging, "Held trigger cannot auto-fire after forced snap");
        Trigger(0f); yield return Wait(0.15f);

        // Pause cancels without attacking, freezes enemies, and resumes without a held-input shot.
        Trigger(1f); yield return Wait(0.35f);
        gm.SetPaused(true);
        float health = player.Health;
        player.TakeDamage(10f);
        yield return Wait(0.12f);
        Check(Time.timeScale == 0f && !hand.Charging && player.Health == health, "Pause freezes time, cancels charge and rejects damage");
        yield return Capture("09-pause");
        gm.SetPaused(false);
        yield return Wait(0.1f);
        Check(!hand.Charging, "Resuming with held input does not fire");
        Trigger(0f); yield return Wait(0.15f);

        // Lure makes a real formation converge and cannot be spammed.
        ClearEnemies(); yield return null;
        var lureTarget = Instantiate(prefab, new Vector3(4f, 0.05f, 11f), Quaternion.identity);
        lureTarget.Configure(EnemyType.Normal, 2f);
        yield return Wait(0.1f);
        float beforeLure = Vector3.Distance(lureTarget.transform.position, new Vector3(0, 0, 6));
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
        yield return Wait(0.65f);
        Check(lureTarget.IsLured && hand.LureCooldown > 0f && !hand.TryLure(), "Actual LB triggers lure and enforces cooldown");
        Check(Vector3.Distance(lureTarget.transform.position, new Vector3(0, 0, 6)) < beforeLure - 1f, "Lure gathers enemies at the visible rally point");
        yield return Capture("07-lure");
        InputSystem.QueueStateEvent(pad, new GamepadState()); yield return null;
        Vector3 beforeDash = player.transform.position;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.RightShoulder));
        yield return Wait(0.1f);
        float dashHealth = player.Health;
        player.TakeDamage(10f);
        Check(player.IsDashing && Vector3.Distance(beforeDash, player.transform.position) > 1f, "Actual RB performs a directional dash");
        Check(player.Health == dashHealth && !player.TryDash(Vector3.right), "Dash grants brief protection and enforces cooldown");
        InputSystem.QueueStateEvent(pad, new GamepadState()); yield return Wait(0.25f);
        Check(!player.IsDashing, "Dash protection ends");
        ClearEnemies(); yield return null;
        player.ResetPlayer();

        // Telegraph gives time to react; staggering a tough enemy interrupts it.
        var tough = Instantiate(prefab, new Vector3(0, 0.05f, 1.5f), Quaternion.Euler(0, 180, 0));
        tough.Configure(EnemyType.Tough, 0f);
        yield return Wait(0.16f);
        Check(tough.IsWindingUp && player.Health == health, "Attack warning precedes damage");
        tough.Hit(1, 1, player.transform.position);
        yield return Wait(0.1f);
        Check(!tough.IsWindingUp && player.Health == health, "Stagger interrupts windup without damage");
        tough.transform.position = new Vector3(0, 0.05f, 1.5f);
        yield return Wait(0.65f);
        tough.transform.position = new Vector3(0, 0.05f, 1.5f);
        yield return Wait(0.8f);
        Check(player.Health < health, "Uninterrupted attack damages player");

        ClearEnemies();
        yield return null;
        Trigger(1f); yield return Wait(0.22f);
        var counterTarget = Instantiate(prefab, new Vector3(0, 0.05f, 1.5f), Quaternion.identity);
        counterTarget.Configure(EnemyType.Normal, 0f);
        yield return Wait(0.38f);
        int beforeCounter = score.Score;
        float counterHealth = player.Health;
        Trigger(0f); yield return Wait(0.15f);
        Check(score.Score - beforeCounter >= 375 && score.Feedback.Contains("ギリギリ"), "Gold snap during windup awards counter bonus");
        Check(player.Health == counterHealth, "Counter prevents pending damage");
        yield return new WaitForSeconds(4.7f);
        Check(score.Combo == 0 && score.PerfectStreak == 0, "Inactive chain and perfect streak expire");

        ClearEnemies(); yield return null;
        var seed = Instantiate(prefab, new Vector3(0, 0.05f, 4), Quaternion.identity);
        seed.Configure(EnemyType.Normal, 0f);
        var guard = Instantiate(prefab, new Vector3(0, 0.05f, 8), Quaternion.identity);
        guard.Configure(EnemyType.Tough, 0f);
        yield return Wait(0.35f);
        int beforeDomino = score.DominoKills;
        Shockwave.Blast(new BlastSpec { origin = Vector3.zero, forward = Vector3.forward, kind = BlastKind.Cone, radius = 5, halfAngleDeg = 32, tier = 1 }, null);
        gm.SetPaused(true);
        Vector3 frozen = seed.transform.position;
        yield return Wait(0.2f);
        Check(seed.transform.position == frozen && Enemy.Alive.Contains(guard), "Pause freezes flying enemies and delayed collision damage");
        gm.SetPaused(false);
        yield return Wait(0.5f);
        Check(!Enemy.Alive.Contains(guard) && score.DominoKills == beforeDomino + 1, "Collision defeats a heavy guard even after a weak launch");

        time.RequestSlowMotion(3f);
        yield return Wait(0.2f);
        player.TakeDamage(999f);
        yield return null;
        Check(gm.Current == GameManager.State.GameOver && Time.timeScale == 1f, "Death restores normal time and ends play");
        Vector3 stopped = player.transform.position;
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
        yield return Wait(0.2f);
        time.RequestSlowMotion(3f);
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f
            && Mathf.Approximately(Time.fixedDeltaTime, normalFixedStep), "Death cancels slow motion and rejects new requests");
        Check(Vector3.Distance(stopped, player.transform.position) < 0.01f, "Cannot move after game over");
        yield return Capture("06-game-over");
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        yield return Wait(0.2f);
        Check(gm.IsPlaying && score.Kills == 0 && player.Health == player.MaxHealth, "Gamepad retry resets run");
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f, "Retry starts at normal speed");
        Check(hand.Type == PastaType.Spaghetti && !hand.Charging, "Retry restores hand without accidental shot");
        Check(Enemy.Alive.Count == 0 && FindObjectsByType<PastaFragment>().Length == 0, "Retry clears enemies and fragments");
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return Wait(0.15f);
        Check(hand.LureCooldown == 0f && player.DashCooldown == 0f && score.DominoKills == 0, "Retry resets both abilities and domino score");

        // Emptying the field while the round is still spawning is not a round-ending defeat.
        float spawnDeadline = Time.realtimeSinceStartup + 3f;
        while (spawner.Wave == 0 && Time.realtimeSinceStartup < spawnDeadline) yield return null;
        int defeatedDuringSpawn = Enemy.Alive.Count;
        foreach (var enemy in Enemy.Alive.ToArray()) enemy.Hit(99, 3, Vector3.zero);
        Check(spawner.RemainingToSpawn > 0 && Enemy.Alive.Count == 0 && !time.IsSlowMotionActive,
            "Defeating all visible enemies during spawning does not trigger slow motion");

        // Exercise round-ending direct hits, domino collisions, and the legacy damage path.
        for (int round = 1; round <= EnemySpawner.TotalRounds; round++)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((spawner.Wave < round || spawner.RemainingToSpawn > 0) && Time.realtimeSinceStartup < deadline)
                yield return Wait(0.1f);
            Check(spawner.Wave == round && Enemy.Alive.Count == (round == 1 ? 9 - defeatedDuringSpawn : round == 2 ? 12 : 18), "Round " + round + " spawns its complete authored formation");
            var targets = Enemy.Alive.ToArray();
            for (int i = 2; i < targets.Length; i++) targets[i].Hit(99, 3, Vector3.zero);
            targets[0].Configure(EnemyType.Normal, 0f);
            targets[1].Configure(EnemyType.Tough, 0f);
            targets[0].transform.position = new Vector3(0, 0.05f, 4);
            targets[1].transform.position = new Vector3(0, 0.05f, round == 2 ? 8 : 40);
            var result = Shockwave.Blast(new BlastSpec { origin = Vector3.zero, forward = Vector3.forward, kind = BlastKind.Cone, radius = 5, halfAngleDeg = 32, tier = 1 }, null);
            score.OnBreak(result.kills, 1, 1f);
            Check(Enemy.Alive.Count == 1 && !time.IsSlowMotionActive && Time.timeScale == 1f,
                "Round " + round + " stays at normal speed until the last enemy is defeated");
            if (round == 2)
            {
                float collisionDeadline = Time.realtimeSinceStartup + 2f;
                while (Enemy.Alive.Count > 0 && Time.realtimeSinceStartup < collisionDeadline) yield return null;
            }
            else if (round == 1)
            {
                // Hitting a surviving heavy enemy must not trigger the round-ending effect.
                Shockwave.Blast(new BlastSpec { origin = Vector3.zero, forward = Vector3.forward, kind = BlastKind.Cone, radius = 100, halfAngleDeg = 180, tier = 1 }, null);
                Check(Enemy.Alive.Count == 1 && !time.IsSlowMotionActive, "A nonlethal hit on the last enemy does not trigger slow motion");
                result = Shockwave.Blast(new BlastSpec { origin = Vector3.zero, forward = Vector3.forward, kind = BlastKind.Cone, radius = 100, halfAngleDeg = 180, tier = 3 }, null);
                score.OnBreak(result.kills, 3, 1f);
            }
            else targets[1].Hit(99, 3, Vector3.zero);

            Check(Enemy.Alive.Count == 0 && time.IsSlowMotionActive,
                "Round " + round + " final defeat starts slow motion (" + (round == 2 ? "domino" : round == 1 ? "direct blast" : "damage") + ")");
            yield return Wait(0.2f);
            Check(Mathf.Approximately(Time.timeScale, 0.05f), "Round " + round + " slow motion holds at five percent speed");
            if (round == 1)
            {
                Check(Mathf.Abs(Time.fixedDeltaTime - normalFixedStep * 0.05f) < 0.000001f, "Physics timestep follows round-ending slow motion");
                gm.SetPaused(true);
                yield return Wait(0.4f);
                time.RequestSlowMotion(0.1f);
                Check(Time.timeScale == 0f, "Pause stays frozen during slow motion and ignores new requests");
                gm.SetPaused(false);
                Check(Mathf.Approximately(Time.timeScale, 0.05f), "Unpausing resumes the active slow motion");
            }
            yield return new WaitForSeconds(4f);
            Check(!time.IsSlowMotionActive && Time.timeScale == 1f
                && Mathf.Approximately(Time.fixedDeltaTime, normalFixedStep), "Round " + round + " restores normal speed and physics timestep");
        }
        Check(gm.Current == GameManager.State.Won && Time.timeScale == 1f, "Completing all three rounds reaches victory");
        float victoryHealth = player.Health;
        player.TakeDamage(999f);
        time.RequestSlowMotion(3f);
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f, "Victory rejects slow motion requests");
        Check(player.Health == victoryHealth && !hand.TryLure() && !player.TryDash(Vector3.forward), "Victory rejects damage and active abilities");
        yield return Capture("08-victory");
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        yield return Wait(0.2f);
        Check(gm.IsPlaying && spawner.Wave == 0 && score.Score == 0,
            $"Victory can restart a clean run (state={gm.Current}, paused={gm.IsPaused}, round={spawner.Wave}, score={score.Score}, pad={pad.enabled})");
        time.RequestSlowMotion(3f);
        yield return Wait(0.2f);
        gm.Win();
        yield return Wait(0.2f);
        Check(!time.IsSlowMotionActive && Time.timeScale == 1f
            && Mathf.Approximately(Time.fixedDeltaTime, normalFixedStep), "Victory cancels active slow motion and restores physics");
        Finish();
    }

    private void ValidateMesh(SpaghettiBreaker pasta)
    {
        var model = pasta.transform.Find("Continuous pasta");
        var mesh = model.GetComponent<MeshFilter>().sharedMesh;
        bool finite = mesh.vertexCount > 500;
        foreach (var vertex in mesh.vertices)
            finite &= !float.IsNaN(vertex.x) && !float.IsInfinity(vertex.y) && !float.IsNaN(vertex.z);
        Check(finite && mesh.bounds.size.x > 0.3f && mesh.bounds.size.y > 0.02f, handName(pasta) + " has curved finite mesh");
        Check(model.GetComponent<MeshRenderer>().sharedMaterial.shader.isSupported, "Pasta shader resolves");
    }

    private static string handName(SpaghettiBreaker pasta) => pasta.name.Replace("(Clone)", "");
    private void ClearEnemies()
    {
        foreach (var enemy in FindObjectsByType<Enemy>()) Destroy(enemy.gameObject);
    }
    private IEnumerator Watchdog()
    {
        yield return Wait(90f);
        if (!finished) { errors.Add("Playtest timed out"); Finish(); }
    }
    private void Finish()
    {
        if (finished) return;
        finished = true;
        File.WriteAllText(Path.Combine(output, "results.txt"), string.Join("\n", checks) + "\n\nERRORS: " + errors.Count + "\n" + string.Join("\n", errors));
        Application.logMessageReceived -= Log;
        InputSystem.RemoveDevice(pad);
        Application.Quit(errors.Count == 0 ? 0 : 1);
    }
}
#endif
