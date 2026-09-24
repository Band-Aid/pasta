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
        var spaghetti = new DominoShot(3, PastaType.Spaghetti);
        var penne = new DominoShot(3, PastaType.Penne);
        var lasagna = new DominoShot(3, PastaType.Lasagna);
        Require(Mathf.Approximately(spaghetti.Travel, 15f) && Mathf.Approximately(spaghetti.KnockbackMultiplier, 1f), "Spaghetti is the shot baseline");
        Require(Mathf.Approximately(penne.Travel, 18f) && Mathf.Approximately(penne.KnockbackMultiplier, 0.9f) && Mathf.Approximately(penne.Speed, 24f), "Penne extends travel while keeping impact light");
        Require(Mathf.Approximately(new DominoShot(1, PastaType.Penne).Travel, 8f)
            && Mathf.Approximately(new DominoShot(2, PastaType.Penne).Travel, 13f), "Penne travel scales at lower tiers too");
        Require(Mathf.Approximately(lasagna.Travel, 13f) && Mathf.Approximately(lasagna.KnockbackMultiplier, 1.3f), "Lasagna trades travel for impact");
        Debug.Log("PASTA_CHECKS_PASSED: timing, blast, domino and weapon balance checks");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Pasta verification: " + message);
    }

    [MenuItem("Pasta/Build Playtest")]
    public static void BuildPlaytest()
    {
        CheckBoundaries();
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
