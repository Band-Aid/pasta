using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors.EditorTools
{
    /// <summary>
    /// Renders model sheets of every character (players, Italians and their variants, bosses) to output/Gallery, posed by the
    /// real walk animation, plus frame sequences of the walk cycles (output/Gallery/walk, output/Gallery/crowd) for review
    /// without playing to the boss. Run with -executeMethod PastaSurvivors.EditorTools.ModelGallery.Render (needs a graphics device).
    /// </summary>
    public static class ModelGallery
    {
        private const string OutDir = "output/Gallery";
        private const float Dt = 1f / 30f;
        private static Transform root;
        private static readonly List<Enemy> actors = new List<Enemy>();

        private static readonly EnemyKind[] Crowd =
        {
            EnemyKind.Signore, EnemyKind.Tifoso, EnemyKind.Mamma, EnemyKind.Chef, EnemyKind.Vespista,
            EnemyKind.Gondoliere, EnemyKind.Pizzaiolo, EnemyKind.Mafioso, EnemyKind.Nonna
        };

        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Mats.Init();
            Directory.CreateDirectory(OutDir);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(50, -35, 0);
            Shader.SetGlobalColor("_PS_AmbientSky", new Color(0.62f, 0.66f, 0.72f).linear);
            Shader.SetGlobalColor("_PS_AmbientGround", new Color(0.45f, 0.38f, 0.32f).linear);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.fog = false;

            var cam = new GameObject("Gallery camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.74f, 0.68f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;

            int sheets = 0;
            try
            {
                sheets += Sheet("lineup", cam, () =>
                {
                    for (int i = 0; i < GameData.Characters.Length; i++) PlacePlayer(i, new Vector3(i * 1.6f, 0f, 0f), 25f);
                    for (int i = 0; i < Crowd.Length; i++) PlaceEnemy(Crowd[i], 0, new Vector3((i + 3) * 1.6f, 0f, 0f), 25f, 1f, 0.6f);
                }, new Vector3(9.6f, 1.9f, 10.5f), new Vector3(9.6f, 1.1f, 0f), 46f, 2400, 900);

                sheets += Sheet("variants", cam, () =>
                {
                    for (int v = 0; v < 4; v++)
                        for (int i = 0; i < Crowd.Length; i++)
                            PlaceEnemy(Crowd[i], v, new Vector3(i * 1.75f, 0f, -v * 2.6f), 25f, 1f, 0.6f + v);
                }, new Vector3(7f, 7.5f, 11f), new Vector3(7f, 0.4f, -4f), 52f, 2400, 1500);

                sheets += Sheet("faces", cam, () =>
                {
                    for (int i = 0; i < Crowd.Length; i++) PlaceEnemy(Crowd[i], 0, new Vector3(i * 0.9f, 0f, 0f), 0f, 1f, 0f, 0f);
                }, new Vector3(3.6f, 1.75f, 3.4f), new Vector3(3.6f, 1.45f, 0f), 50f, 2400, 900);

                sheets += Sheet("bosses", cam, () =>
                {
                    PlaceEnemy(EnemyKind.BossNonna, 0, new Vector3(0f, 0f, 0f), 25f, 2.9f, 0.6f);
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(6.5f, 0f, 0f), 60f, 2.2f, 0.6f);
                    PlaceEnemy(EnemyKind.BossCapitano, 0, new Vector3(13f, 0f, 0f), 25f, 2.7f, 0.6f);
                    PlaceEnemy(EnemyKind.BossDon, 0, new Vector3(19f, 0f, 0f), 25f, 2.7f, 0.6f);
                }, new Vector3(9.5f, 4.5f, 20f), new Vector3(9.5f, 2.6f, 0f), 46f, 2400, 1000);

                sheets += Sheet("bike-nonna", cam, () =>
                {
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(2.7f, 0f, 0f), 90f, 1f, 0f, 0f);
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(-0.6f, 0f, 0f), 35f, 1f, 0f, 0f);
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(-3.4f, 0f, 0.4f), 210f, 1f, 0f, 0f);
                }, new Vector3(0.4f, 1.9f, 5.4f), new Vector3(0.4f, 0.85f, 0f), 46f, 2000, 1000);

                sheets += Sheet("gameplay", cam, () => Crowd3D(), Quaternion.Euler(55f, 0f, 0f) * new Vector3(0f, 0f, -23f), Vector3.zero, 42f, 1600, 900);

                // Walk cycles: side-on parade, and the crowd from the game camera.
                Frames("walk", cam, () =>
                {
                    for (int i = 0; i < Crowd.Length; i++) PlaceEnemy(Crowd[i], 0, new Vector3(i * 1.7f, 0f, 0f), 50f, 1f, i * 0.7f);
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(-2.6f, 0f, 0f), 50f, 1f, 0f);
                }, new Vector3(5.5f, 2.0f, 10f), new Vector3(5.5f, 1.0f, 0f), 46f, 1920, 800, 60);
                // Coming straight at the camera, to show hip sway, shoulder swing, head shakes and the bike's weave.
                Frames("approach", cam, () =>
                {
                    for (int i = 0; i < Crowd.Length; i++) PlaceEnemy(Crowd[i], 0, new Vector3(i * 1.3f, 0f, 0f), 0f, 1f, i * 0.7f);
                    PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(-1.6f, 0f, 0f), 0f, 1f, 0f);
                }, new Vector3(4.5f, 2.4f, 8f), new Vector3(4.5f, 1.0f, 0f), 50f, 1920, 800, 60);
                Frames("crowd", cam, () => Crowd3D(), Quaternion.Euler(55f, 0f, 0f) * new Vector3(0f, 0f, -16f), Vector3.zero, 42f, 1280, 720, 60);

                var stats = new System.Text.StringBuilder();
                long crowd = 0;
                foreach (var k in Crowd)
                    for (int v = 0; v < Models.EnemyVariantCount(k); v++)
                    {
                        var rm = Models.Enemy(k, v);
                        int n = Vertices(rm);
                        crowd += n;
                        stats.AppendLine(k + " " + v + ": " + n + "  (body " + rm.body.vertexCount + ", head " + (rm.head != null ? rm.head.vertexCount : 0)
                            + ", leg " + (rm.leg != null ? rm.leg.vertexCount : 0) + ", arms " + (rm.armL.vertexCount + rm.armR.vertexCount)
                            + ", belly " + (rm.belly != null ? rm.belly.vertexCount : 0) + ", prop " + (rm.prop != null ? rm.prop.vertexCount : 0) + ")");
                    }
                foreach (var k in new[] { EnemyKind.BossNonna, EnemyKind.BossBikeNonna, EnemyKind.BossCapitano, EnemyKind.BossDon })
                    stats.AppendLine(k + ": " + Vertices(Models.Enemy(k)));
                stats.AppendLine("crowd average: " + crowd / (Crowd.Length * 4));
                File.WriteAllText(Path.Combine(OutDir, "vertices.txt"), stats.ToString());
                Debug.Log("MODEL_GALLERY_PASSED: " + sheets + " sheets in " + Path.GetFullPath(OutDir));
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        private static void Crowd3D()
        {
            var rnd = new System.Random(7);
            int i = 0;
            foreach (var k in Crowd)
                for (int v = 0; v < 4; v++, i++)
                {
                    float a = i * 2.4f, r = 2.5f + i * 0.22f;
                    var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    // Everyone heads for the player in the middle.
                    float yaw = Mathf.Atan2(-pos.x, -pos.z) * Mathf.Rad2Deg + (float)rnd.NextDouble() * 30f - 15f;
                    PlaceEnemy(k, v, pos, yaw, 1f, (float)rnd.NextDouble() * 6f);
                }
            PlacePlayer(0, Vector3.zero, 160f, 1.15f);
            PlaceEnemy(EnemyKind.BossBikeNonna, 0, new Vector3(-7f, 0f, 3f), 300f, 2.2f, 0f);
            Models.Static(new MeshKit().Quad(Vector3.zero, Vector2.one * 60f, new Color(0.62f, 0.58f, 0.53f)).ToMesh("Ground"), root, "Ground");
        }

        private static void Begin(string name)
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root.gameObject);
            actors.Clear();
            root = new GameObject("Gallery " + name).transform;
        }

        private static int Sheet(string name, Camera cam, Action build, Vector3 eye, Vector3 look, float fov, int w, int h)
        {
            Begin(name);
            build();
            Aim(cam, eye, look, fov);
            Capture(cam, Path.Combine(OutDir, name + ".png"), w, h);
            return 1;
        }

        /// <summary>Renders an animation as numbered frames, advancing every actor's real walk cycle.</summary>
        private static void Frames(string name, Camera cam, Action build, Vector3 eye, Vector3 look, float fov, int w, int h, int count)
        {
            Begin(name);
            build();
            Aim(cam, eye, look, fov);
            var dir = Path.Combine(OutDir, name);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            for (int i = 0; i < count; i++)
            {
                foreach (var e in actors) e.Animate(Dt, e.def.speed, false);
                Capture(cam, Path.Combine(dir, i.ToString("000") + ".png"), w, h);
            }
        }

        private static void Aim(Camera cam, Vector3 eye, Vector3 look, float fov)
        {
            cam.transform.position = eye;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
        }

        private static void Capture(Camera cam, string path, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>Vertices drawn for one actor (legs are drawn twice).</summary>
        private static int Vertices(RigMeshes m)
        {
            int n = m.body.vertexCount;
            if (m.leg != null) n += m.leg.vertexCount * 2;
            if (m.armL != null) n += m.armL.vertexCount;
            if (m.armR != null) n += m.armR.vertexCount;
            if (m.head != null) n += m.head.vertexCount;
            if (m.belly != null) n += m.belly.vertexCount;
            if (m.prop != null) n += m.prop.vertexCount;
            if (m.wheel != null) n += m.wheel.vertexCount * m.wheelPos.Length;
            if (m.crank != null) n += m.crank.vertexCount;
            return n;
        }

        /// <summary>Places an Italian posed by the game's own walk animation at a point in the cycle (speed 0 = standing).</summary>
        private static void PlaceEnemy(EnemyKind kind, int variant, Vector3 pos, float yaw, float scale, float phase, float speed = -1f)
        {
            var def = GameData.Enemy(kind);
            var rig = Models.Build(Models.Enemy(kind, variant), root, kind + " " + variant);
            Finish(rig, pos, yaw, scale);
            var e = new Enemy { def = def, rig = rig, scale = scale, active = true, uid = actors.Count + 1, walkPhase = phase, gesture = phase * 0.37f };
            e.ResetApproachGesture();
            e.Animate(Dt, speed < 0f ? def.speed : speed, false);
            actors.Add(e);
        }

        private static void PlacePlayer(int index, Vector3 pos, float yaw, float scale = 1f)
        {
            var rig = Models.Build(Models.Player(GameData.Characters[index], index), root, "Player " + index);
            Finish(rig, pos, yaw, scale);
            rig.armL.localRotation = Quaternion.Euler(rig.restL);
            rig.armR.localRotation = Quaternion.Euler(rig.restR);
        }

        private static void Finish(Rig rig, Vector3 pos, float yaw, float scale)
        {
            rig.root.position = pos;
            rig.root.rotation = Quaternion.Euler(0f, yaw, 0f); // yaw 0 faces the camera
            rig.root.localScale = Vector3.one * scale;
            foreach (var r in rig.renderers) r.shadowCastingMode = ShadowCastingMode.On;
        }
    }
}
