using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PastaSurvivors.EditorTools
{
    /// <summary>Creates the Survivors scene (a single bootstrap object) and builds Windows players.</summary>
    public static class SurvivorsBuild
    {
        public const string ScenePath = "Assets/Scenes/Survivors.unity";
        private const string LegacyScene = "Assets/Scenes/Main.unity";

        [MenuItem("Pasta Survivors/Build Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("PastaSurvivors");
            go.AddComponent<SurvivorsGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(LegacyScene, false),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVORS_SCENE_BUILT: " + ScenePath);
        }

        [MenuItem("Pasta Survivors/Build Windows Playtest (Development)")]
        public static void BuildDevelopment() => Build("output/Survivors/PastaSurvivors.exe", BuildOptions.Development);

        [MenuItem("Pasta Survivors/Build Windows Release")]
        public static void BuildRelease() => Build("output/SurvivorsRelease/PastaSurvivors.exe", BuildOptions.None);

        private static void Build(string path, BuildOptions options)
        {
            BuildScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Survivors build failed: " + report.summary.result);
            Debug.Log("SURVIVORS_BUILD_PASSED: " + path);
        }
    }
}
