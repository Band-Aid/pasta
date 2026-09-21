#if UNITY_EDITOR || DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>Opt-in player integration test for the actual pickup, weapon and recipe systems.</summary>
public class KitchenPlaytest : MonoBehaviour
{
    private readonly List<string> checks = new List<string>();
    private readonly List<string> errors = new List<string>();
    private PastaKitchen kitchen;
    private GameManager game;
    private PlayerController player;
    private Enemy prefab;
    private Gamepad pad;
    private string output;
    private bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if ((!Debug.isDebugBuild && !Application.isEditor) || Array.IndexOf(Environment.GetCommandLineArgs(), "-pasta-kitchen-smoke") < 0) return;
        new GameObject("Kitchen integration verification").AddComponent<KitchenPlaytest>();
    }

    private void Awake()
    {
        output = Path.Combine(Application.dataPath, "..", "KitchenVerification");
        Directory.CreateDirectory(output);
        Application.logMessageReceived += Log;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        foreach (var device in InputSystem.devices) InputSystem.DisableDevice(device);
        pad = InputSystem.AddDevice<Gamepad>();
        Application.runInBackground = true;
        StartCoroutine(Run());
        StartCoroutine(Watchdog());
    }

    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
    }

    private void Check(bool condition, string message)
    {
        checks.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) errors.Add(message);
    }

    private static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

    private IEnumerator DrainRewards()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (kitchen.PendingRewards > 0 && Time.realtimeSinceStartup < deadline) yield return Wait(0.1f);
        Check(kitchen.PendingRewards == 0, "Collected rewards finish their individual presentations");
    }

    private IEnumerator Clean()
    {
        kitchen.ResetRun();
        foreach (var enemy in FindObjectsByType<Enemy>()) Destroy(enemy.gameObject);
        foreach (var wave in FindObjectsByType<Shockwave>()) Destroy(wave.gameObject);
        yield return null;
        ScoreManager.Instance.ResetScore();
        player.ResetPlayer();
    }

    private Enemy Spawn(Vector3 position, EnemyType type = EnemyType.Normal)
    {
        var enemy = Instantiate(prefab, position, Quaternion.identity);
        enemy.Configure(type, 0f);
        enemy.attackRange = 0.1f;
        return enemy;
    }

    private IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        yield return null;
    }

    private IEnumerator Run()
    {
        yield return Wait(1.4f);
        game = GameManager.Instance;
        kitchen = PastaKitchen.Instance;
        player = PlayerController.Instance;
        EnemySpawner.Instance.StopAll();
        prefab = (Enemy)typeof(EnemySpawner).GetField("enemyPrefab", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(EnemySpawner.Instance);
        Check(kitchen != null && kitchen.isActiveAndEnabled, "Kitchen is installed in the existing scene without rebuilding it");
        yield return Clean();

        // Nonlethal attacks and duplicate hits must never create extra loot or score.
        var tough = Spawn(new Vector3(0, 0.05f, 8), EnemyType.Tough);
        Check(PastaKitchen.Damage(tough, 1, Vector3.zero) && Enemy.Alive.Contains(tough), "Kitchen damage can hit without killing a heavy enemy");
        Check(kitchen.DropsCreated == 0 && ScoreManager.Instance.Kills == 0, "Nonlethal hit creates neither loot nor kill score");
        PastaKitchen.Damage(tough, 2, Vector3.zero);
        Check(kitchen.DropsCreated == 1 && ScoreManager.Instance.KitchenKills == 1, "A defeated enemy drops loot and awards one automatic kill");
        Check(!PastaKitchen.Damage(tough, 5, Vector3.zero) && kitchen.DropsCreated == 1 && ScoreManager.Instance.Kills == 1, "Repeated damage cannot double-drop or double-score");
        Check(FindObjectsByType<KitchenPickup>().Length == 1 && FindObjectsByType<KitchenPickup>()[0].IsWeapon, "First defeat guarantees an automatic pasta drop");
        var seed = Spawn(new Vector3(0, 0.05f, 12));
        var chainTarget = Spawn(new Vector3(0, 0.05f, 16));
        var lastTarget = Spawn(new Vector3(0, 0.05f, 20));
        Shockwave.Blast(new BlastSpec { kind = BlastKind.Cone, origin = new Vector3(0, 0, 10), forward = Vector3.forward, radius = 3f, halfAngleDeg = 30f, tier = 3 }, null);
        yield return Wait(0.85f);
        Check(!Enemy.Alive.Contains(chainTarget) && !Enemy.Alive.Contains(lastTarget) && kitchen.DefeatedCount == 4, "Manual launch and delayed domino collisions each enter the loot table once");
        yield return Capture("01-enemy-drops");
        yield return Clean();

        // Test actual falling loot, attraction and paused collection, not just inventory mutation.
        var pickup = KitchenPickup.SpawnIngredient(kitchen, new Vector3(0, 0, 4), PastaIngredient.Ham);
        game.SetPaused(true);
        Vector3 frozen = pickup.transform.position;
        yield return Wait(0.2f);
        Check(kitchen.Inventory.Count(PastaIngredient.Ham) == 0 && pickup.transform.position == frozen, "Pause freezes pickup flight and inventory collection");
        game.SetPaused(false);
        yield return Wait(1.3f);
        Check(kitchen.Inventory.Count(PastaIngredient.Ham) == 0 && pickup != null, "Distant loot remains on the ground instead of vacuuming into the player");
        InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.up });
        yield return Wait(0.45f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return Wait(0.6f);
        Check(kitchen.Inventory.Count(PastaIngredient.Ham) == 1 && pickup == null, "A nearby ingredient falls, attracts and collects exactly once");
        Check(!kitchen.Inventory.HasBritishCarbonara, "Early ham alone does not activate the recipe");
        yield return Capture("02a-item-get");
        KitchenPickup.SpawnIngredient(kitchen, new Vector3(1, 0, 2), PastaIngredient.Macaroni);
        KitchenPickup.SpawnIngredient(kitchen, new Vector3(-1, 0, 2), PastaIngredient.Cheese);
        yield return Wait(1.4f);
        Check(kitchen.PendingRewards > 0 && !kitchen.Inventory.HasBritishCarbonara, "Simultaneous pickups queue instead of unlocking everything at once");
        yield return DrainRewards();
        Check(kitchen.Inventory.HasMacAndCheese && kitchen.Inventory.HasBritishCarbonara, "Macaroni plus cheese plus early ham completes British Carbonara through real pickups");
        Check(kitchen.Inventory.Count(PastaIngredient.Ham) == 1 && kitchen.BicyclesFired == 0, "Materials persist and the bicycle waits for a target");
        yield return Capture("02-recipe-complete");
        yield return Clean();

        // Each automatic pasta has an independently observable attack.
        kitchen.CollectWeapon(KitchenWeapon.Fusilli);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Spawn(new Vector3(Mathf.Cos(a) * 2.8f, 0.05f, Mathf.Sin(a) * 2.8f));
        }
        yield return Wait(0.3f);
        Check(ScoreManager.Instance.KitchenKills == 0, "A new weapon is presented before it starts attacking");
        yield return Wait(1.6f);
        Check(ScoreManager.Instance.KitchenKills > 0, "Orbiting fusilli hits enemies around the player without attack input");
        yield return Capture("03-fusilli");
        yield return Clean();

        kitchen.CollectWeapon(KitchenWeapon.Farfalle);
        var butterflyTarget = Spawn(new Vector3(0, 0.05f, 9));
        yield return Wait(1.6f);
        var projectiles = FindObjectsByType<KitchenProjectile>();
        Check(projectiles.Length > 0 && projectiles[0].Attack == KitchenProjectile.Kind.Butterfly, "Farfalle fires a visible homing bow tie");
        game.SetPaused(true);
        frozen = projectiles[0].transform.position;
        yield return Wait(0.2f);
        Check(projectiles[0].transform.position == frozen && Enemy.Alive.Contains(butterflyTarget), "Pause freezes automatic projectiles and damage");
        game.SetPaused(false);
        yield return Wait(0.95f);
        Check(!Enemy.Alive.Contains(butterflyTarget) && ScoreManager.Instance.KitchenKills == 1, "Farfalle homes into its target and scores exactly once");
        yield return Clean();

        kitchen.CollectWeapon(KitchenWeapon.Ravioli);
        var sauceTarget = Spawn(new Vector3(0, 0.05f, 9), EnemyType.Tough);
        yield return Wait(2.55f);
        Check(FindObjectsByType<KitchenSauce>().Length > 0 && sauceTarget.IsSlowed && Enemy.Alive.Contains(sauceTarget), "Ravioli lands as a slowing tomato puddle and damages a heavy enemy nonlethally");
        yield return Capture("04-ravioli");
        yield return Wait(1.8f);
        Check(!Enemy.Alive.Contains(sauceTarget) && ScoreManager.Instance.KitchenKills == 1, "Repeated sauce damage defeats the guard without duplicate credit");
        yield return Clean();

        kitchen.CollectIngredient(PastaIngredient.Macaroni);
        kitchen.CollectIngredient(PastaIngredient.Cheese);
        yield return DrainRewards();
        var cheeseTarget = Spawn(new Vector3(0, 0.05f, 3));
        yield return Wait(2.8f);
        Check(kitchen.Inventory.HasMacAndCheese && !kitchen.Inventory.HasBritishCarbonara && !Enemy.Alive.Contains(cheeseTarget), "Italian mac and cheese has its own cheese aura before adding ham");
        yield return Capture("05-mac-and-cheese");
        yield return Clean();

        kitchen.CollectIngredient(PastaIngredient.Macaroni);
        kitchen.CollectIngredient(PastaIngredient.Cheese);
        kitchen.CollectIngredient(PastaIngredient.Ham);
        yield return DrainRewards();
        var bikeTarget = Spawn(new Vector3(0, 0.05f, 13));
        var nearby = Spawn(new Vector3(2, 0.05f, 13), EnemyType.Tough);
        var farther = Spawn(new Vector3(0, 0.05f, 21), EnemyType.Tough);
        yield return Wait(2.7f);
        Check(kitchen.BicyclesFired == 0, "Completing the recipe arms the ultimate without firing it unexpectedly");
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
        yield return Wait(0.2f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        KitchenProjectile bicycle = null;
        foreach (var shot in FindObjectsByType<KitchenProjectile>())
            if (shot.Attack == KitchenProjectile.Kind.Bicycle) bicycle = shot;
        Check(bicycle != null && kitchen.BicyclesFired == 1 && Enemy.Alive.Contains(bikeTarget), "Gamepad Y summons the bicycle with an entrance before it charges");
        yield return Wait(0.7f);
        game.SetPaused(true);
        float cooldown = kitchen.BicycleCooldown;
        frozen = bicycle.transform.position;
        yield return Wait(0.2f);
        Check(bicycle.transform.position == frozen && Mathf.Abs(cooldown - kitchen.BicycleCooldown) < 0.01f, "Pause freezes bicycle movement and its cooldown");

        // Record the whole visual from the side so both wheels, glasses and rider are reviewable.
        var camera = Camera.main;
        var savedPosition = camera.transform.position;
        var savedRotation = camera.transform.rotation;
        var bikeModel = bicycle.transform.Find("Pasta model");
        camera.transform.position = bicycle.transform.position + new Vector3(4.2f, 2.2f, 1.6f);
        camera.transform.LookAt(bicycle.transform.position + Vector3.up * 1.3f);
        // PlayerController updates the camera pose in LateUpdate; temporarily disable it for this capture.
        player.enabled = false;
        var canvas = FindAnyObjectByType<Canvas>();
        canvas.enabled = false;
        yield return Capture("06-granny-bicycle");
        canvas.enabled = true;
        camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
        player.enabled = true;
        Check(bikeModel != null && bikeModel.Find("Granny face") != null && bikeModel.Find("Granny wheel") != null, "Bicycle contains a modeled granny and wheels");
        game.SetPaused(false);
        yield return Wait(2.4f);
        Check(!Enemy.Alive.Contains(bikeTarget) && !Enemy.Alive.Contains(nearby), "Granny impact launches the target and a nearby heavy enemy");
        Check(bicycle != null && bicycle.BicycleHits >= 2 && Vector3.Dot(bicycle.transform.up, Vector3.up) > 0.99f,
            "Granny stays upright and keeps driving after hitting multiple enemies");
        Check(!Enemy.Alive.Contains(farther), "The ultimate clears the next row through its continued charge or domino chain");
        Check(kitchen.BicyclesFired == 1 && kitchen.BicycleCooldown > 8f, "The recipe has a cooldown instead of firing every frame");
        yield return Capture("07-british-impact");

        // Death, paused attacks, and retry must leave no automatic effects or permanent unlocks.
        player.TakeDamage(999f);
        int killsAtEnd = ScoreManager.Instance.Kills;
        var afterEnd = Spawn(new Vector3(0, 0.05f, 2));
        Check(!PastaKitchen.Damage(afterEnd, 5, Vector3.zero), "Automatic damage is rejected after the run ends");
        yield return Wait(0.3f);
        Check(ScoreManager.Instance.Kills == killsAtEnd, "No late automatic damage scores after game over");
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        yield return Wait(0.2f);
        InputSystem.QueueStateEvent(pad, new GamepadState());
        Check(game.IsPlaying && kitchen.Inventory.TotalIngredients == 0 && !kitchen.Inventory.HasBritishCarbonara, "Gamepad retry clears ingredients and recipe evolution");
        Check(kitchen.Inventory.Level(KitchenWeapon.Fusilli) == 0 && kitchen.BicyclesFired == 0 && ScoreManager.Instance.KitchenKills == 0, "Retry clears weapon upgrades, cooldown state and kitchen score");
        Check(FindObjectsByType<KitchenProjectile>().Length == 0 && FindObjectsByType<KitchenPickup>().Length == 0 && FindObjectsByType<KitchenSauce>().Length == 0, "Retry removes leftover food, sauce and projectiles");
        Finish();
    }

    private IEnumerator Watchdog()
    {
        yield return Wait(90f);
        if (!finished) { errors.Add("Kitchen playtest timed out"); Finish(); }
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
