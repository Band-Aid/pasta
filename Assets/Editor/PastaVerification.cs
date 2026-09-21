using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Runs boundary checks in batch mode and builds the current scene without rebuilding it.</summary>
public static class PastaVerification
{
    [MenuItem("Pasta/Verify Blast Boundaries")]
    public static void CheckBoundaries()
    {
        var cone = new BlastSpec { kind = BlastKind.Cone, forward = Vector3.forward, radius = 10f, halfAngleDeg = 40f, tier = 3 };
        Require(Shockwave.Contains(cone, new Vector3(0, 50, 10)), "Range boundary ignores height");
        Require(!Shockwave.Contains(cone, new Vector3(0, 0, 10.01f)), "Beyond range excluded");
        Require(!Shockwave.Contains(cone, Vector3.back), "Focused shot excludes rear");
        Require(!Shockwave.Contains(cone, new Vector3(8, 0, 2)), "Focused shot excludes sides");
        Require(Shockwave.Contains(cone, Vector3.zero), "Point-blank hit");
        cone.halfAngleDeg = 180f;
        Require(Shockwave.Contains(cone, Vector3.back * 9f), "Full circle covers rear");
        var line = new BlastSpec { kind = BlastKind.Line, forward = Vector3.forward * 3f, length = 20f, halfWidth = 2f };
        Require(Shockwave.Contains(line, new Vector3(2, 0, 20)), "Line endpoint and width inclusive; direction normalized");
        Require(!Shockwave.Contains(line, new Vector3(2.01f, 0, 10)), "Line side excluded");
        Require(!Shockwave.Contains(line, new Vector3(0, 0, -0.51f)), "Line rear excluded");
        Require(!Shockwave.Contains(line, new Vector3(0, 0, 20.01f)), "Line endpoint excluded");
        line.origin = new Vector3(3f, 0, -5f);
        line.forward = Vector3.right;
        Require(Shockwave.Contains(line, new Vector3(23f, 2f, -3f)), "Translated and rotated line");
        var go = new GameObject("Timing boundary check");
        try
        {
            var pasta = go.AddComponent<SpaghettiBreaker>();
            Require(pasta.QualityAt(59.99f) < 0f, "Early release retains pasta");
            Require(pasta.QualityAt(60f) == 1f, "Perfect window starts at threshold");
            Require(pasta.QualityAt(86f) == 1f, "Perfect window end included");
            Require(PastaHand.Tier(pasta.QualityAt(86.01f)) == 2, "Late release is good");
            Require(PastaHand.Tier(pasta.QualityAt(114.01f)) == 1, "Over-bent release is weak");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        Require(DominoShot.SweptHit(Vector3.zero, Vector3.forward * 20f, new Vector3(1f, 8f, 10f), 1f), "Fast domino catches intermediate target and ignores visual height");
        Require(!DominoShot.SweptHit(Vector3.zero, Vector3.forward * 20f, new Vector3(1.01f, 0, 10f), 1f), "Domino excludes targets outside lane");
        Require(!DominoShot.SweptHit(Vector3.zero, Vector3.forward * 20f, Vector3.back * 2f, 1f), "Domino excludes target behind flight segment");
        Require(DominoShot.SweptHit(Vector3.one, Vector3.one, Vector3.one, 1f), "Stationary domino has finite point test");
        Require(DominoShot.SweptHit(new Vector3(5, 0, 3), new Vector3(15, 0, 3), new Vector3(10, 0, 4), 1f), "Translated sideways domino lane");
        Debug.Log("PASTA_CHECKS_PASSED: 21 timing, blast and domino boundary checks");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Pasta verification: " + message);
    }

    [MenuItem("Pasta/Verify Kitchen Recipes")]
    public static void CheckKitchenRecipes()
    {
        int checks = 0;
        var inventory = new KitchenInventory();
        Require(!inventory.HasMacAndCheese && !inventory.HasBritishCarbonara, "Empty kitchen has no recipe"); checks++;
        inventory.Collect(PastaIngredient.Ham);
        Require(!inventory.HasBritishCarbonara, "Ham alone is not British Carbonara"); checks++;
        inventory.Collect(PastaIngredient.Cheese);
        inventory.Collect(PastaIngredient.Cheese);
        Require(!inventory.HasMacAndCheese && inventory.MacAndCheeseParts == 1, "Duplicate cheese cannot replace macaroni"); checks++;
        inventory.Collect(PastaIngredient.Macaroni);
        Require(inventory.HasMacAndCheese && inventory.HasBritishCarbonara, "Ham collected first still evolves the completed recipe"); checks++;
        for (int a = 0; a < 3; a++)
            for (int b = 0; b < 3; b++)
            {
                if (a == b) continue;
                inventory.Reset();
                inventory.Collect((PastaIngredient)a);
                inventory.Collect((PastaIngredient)b);
                Require(!inventory.HasBritishCarbonara, "Two ingredients cannot complete British Carbonara"); checks++;
                inventory.Collect((PastaIngredient)(3 - a - b));
                Require(inventory.HasBritishCarbonara, "All ingredient orders evolve"); checks++;
            }
        inventory.Reset();
        inventory.Collect(PastaIngredient.Macaroni);
        inventory.Collect(PastaIngredient.Cheese);
        Require(inventory.HasMacAndCheese && !inventory.HasBritishCarbonara, "Italian mac and cheese unlocks separately before ham"); checks++;
        for (int i = 0; i < 40; i++) inventory.Collect(PastaIngredient.Cheese);
        Require(inventory.RecipeLevel == 3 && !inventory.HasBritishCarbonara, "Duplicate ingredients strengthen seasoning to its cap without inventing ham"); checks++;
        for (int w = 0; w < 3; w++)
        {
            for (int level = 0; level < KitchenInventory.MaxWeaponLevel; level++) inventory.Upgrade((KitchenWeapon)w);
            Require(!inventory.Upgrade((KitchenWeapon)w) && inventory.Level((KitchenWeapon)w) == 3, "Automatic pasta level is capped"); checks++;
        }
        Require(!inventory.TryRollWeapon(new System.Random(1), out _), "Maxed arsenal falls back to ingredient drops"); checks++;
        inventory.Reset();
        Require(inventory.TotalIngredients == 0 && inventory.RecipeLevel == 0 && inventory.Level(KitchenWeapon.Farfalle) == 0, "Restart clears ingredients, recipe and weapons"); checks++;
        var randomA = new System.Random(918);
        var randomB = new System.Random(918);
        bool[] seen = new bool[3];
        for (int i = 0; i < 120; i++)
        {
            var item = inventory.RollIngredient(randomA);
            Require(item == inventory.RollIngredient(randomB), "Loot rolls are reproducible with a fixed seed");
            seen[(int)item] = true;
        }
        Require(seen[0] && seen[1] && seen[2], "Every recipe ingredient can drop"); checks += 2;
        Debug.Log($"PASTA_KITCHEN_CHECKS_PASSED: {checks} recipe, upgrade, reset and loot checks");
    }

    [MenuItem("Pasta/Build Playtest")]
    public static void BuildPlaytest()
    {
        CheckBoundaries();
        CheckKitchenRecipes();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "output/DominoPlaytest/PastaLaVista.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Require(report.summary.result == BuildResult.Succeeded, "Windows playtest build");
        Debug.Log("PASTA_BUILD_PASSED: output/DominoPlaytest/PastaLaVista.exe");
    }
}
