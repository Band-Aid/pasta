using System;
using UnityEditor;
using UnityEngine;

namespace PastaSurvivors.EditorTools
{
    /// <summary>
    /// Opt-in checks against the generated stage, not a duplicate collision model.
    /// Checks geometry and attack prerequisites; does not claim to measure live combat balance.
    /// </summary>
    public static class RomaRouteVerification
    {
        [MenuItem("Pasta Survivors/Verify Roma Routes")]
        public static void Check()
        {
            Require(!EditorApplication.isPlaying, "Run these geometry checks outside Play mode");
            var restoreLighting = CaptureLighting();
            var previousCamera = G.Cam;
            var randomState = UnityEngine.Random.state;
            var previousSun = UnityEngine.Object.FindAnyObjectByType<Light>();
            var root = new GameObject("Temporary Roma route checks") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                G.Cam = null; // Do not change an existing camera's background during world construction.
                Mats.Init();
                var arena = StageBuilder.Build(GameData.Stages[0], root.transform);
                if (previousSun == null)
                    UnityEngine.Object.FindAnyObjectByType<Light>()?.transform.SetParent(root.transform, false);
                arena.BuildGrid();
                arena.UpdateFlow(arena.spawn);

                CheckPair(arena, "North", StageBuilder.RomaNorthShortcut, StageBuilder.RomaNorthDetour);
                CheckPair(arena, "Orchard", StageBuilder.RomaOrchardShortcut, StageBuilder.RomaOrchardDetour);
                CheckConnectedRegion(arena, new Rect(-88f, 70f, 176f, 64f));
                CheckConnectedRegion(arena, new Rect(83f, -138f, 70f, 94f));

                // Equivalent nearby attackers: open on the shortcut, behind actual cover on the detour.
                var northCovered = Vector2.Lerp(StageBuilder.RomaNorthDetour[2], StageBuilder.RomaNorthDetour[3], 22f / 24f);
                CheckThrowCover(arena, "North", StageBuilder.RomaNorthShortcut, StageBuilder.RomaNorthDetour,
                    new Vector2(-32f, 76f), northCovered, new Vector2(4f, 14f / 3f));
                CheckThrowCover(arena, "Orchard", StageBuilder.RomaOrchardShortcut, StageBuilder.RomaOrchardDetour,
                    new Vector2(120f, -74f), new Vector2(128f, -74f), new Vector2(0f, 8f));
                CheckVespaLane(arena);
                Debug.Log("ROMA_ROUTE_CHECKS_PASSED: both directions, player/Vespa/boss clearance, connected walking grid, shared endpoints, distance/cover tradeoff and Vespa charge prerequisites. Live combat balance is not measured.");
            }
            finally
            {
                G.Cam = previousCamera;
                UnityEngine.Random.state = randomState;
                restoreLighting();
                if (previousSun == null)
                {
                    var sun = UnityEngine.Object.FindAnyObjectByType<Light>();
                    if (sun != null && !sun.transform.IsChildOf(root.transform)) UnityEngine.Object.DestroyImmediate(sun.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector3 Ground(Vector2 p) => new Vector3(p.x, 0f, p.y);

        private static void CheckPair(Arena arena, string name, Vector2[] shortcut, Vector2[] detour)
        {
            Require(shortcut[0] == detour[0] && shortcut[shortcut.Length - 1] == detour[detour.Length - 1], name + ": common endpoints");
            float shortLength = Length(shortcut), longLength = Length(detour);
            Require(longLength > shortLength * 1.2f, name + ": the exposed route has a meaningful distance advantage");
            foreach (float radius in new[] { 0.4f, GameData.Enemy(EnemyKind.Vespista).radius, GameData.Enemy(EnemyKind.BossNonna).radius })
            {
                Walk(arena, name + " shortcut", shortcut, radius, false);
                Walk(arena, name + " shortcut", shortcut, radius, true);
                Walk(arena, name + " detour", detour, radius, false);
                Walk(arena, name + " detour", detour, radius, true);
            }
            Debug.Log($"ROMA_ROUTE_LENGTH {name}: shortcut={shortLength:F2}m, detour={longLength:F2}m; both directions clear for radii 0.4/{GameData.Enemy(EnemyKind.Vespista).radius}/{GameData.Enemy(EnemyKind.BossNonna).radius}");
        }

        private static float Length(Vector2[] path)
        {
            float length = 0f;
            for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);
            return length;
        }

        private static void Walk(Arena arena, string name, Vector2[] path, float radius, bool reverse)
        {
            const float step = 5.3f / 60f; // Use small, bounded walking steps through the production resolver.
            int index = reverse ? path.Length - 1 : 0;
            int direction = reverse ? -1 : 1;
            var position = Ground(path[index]);
            Require(!arena.Blocked(position, radius), name + ": entry is clear at radius " + radius);
            while (index + direction >= 0 && index + direction < path.Length)
            {
                index += direction;
                var target = Ground(path[index]);
                int budget = Mathf.CeilToInt(Vector3.Distance(position, target) / step) + 2;
                while ((position - target).sqrMagnitude > 0.000001f)
                {
                    Require(budget-- > 0, name + ": bounded progress to the next turn");
                    var intended = Vector3.MoveTowards(position, target, step);
                    Require(!arena.Blocked(intended, radius), name + ": blocked at " + intended + ", radius=" + radius);
                    var resolved = arena.Resolve(intended, radius);
                    Require((resolved - intended).sqrMagnitude < 0.000001f, name + ": collision pushes the walker off the path at " + intended);
                    Require(arena.Reachable(resolved), name + ": disconnected navigation cell at " + resolved);
                    position = resolved;
                }
            }
        }

        private static void CheckConnectedRegion(Arena arena, Rect region)
        {
            int checkedCells = 0;
            for (int y = 0; y < arena.GridH; y++)
                for (int x = 0; x < arena.GridW; x++)
                {
                    var p = arena.CellCenter(x, y);
                    if (!region.Contains(new Vector2(p.x, p.z)) || arena.BlockedCells[y * arena.GridW + x]) continue;
                    Require(arena.Reachable(p), "No isolated walking-grid pocket in an edited district: " + p);
                    checkedCells++;
                }
            Require(checkedCells > 0, "Region contains navigable cells");
            Debug.Log("ROMA_ROUTE_CONNECTED_CELLS: " + checkedCells + " in " + region);
        }

        private static void CheckThrowCover(Arena arena, string name, Vector2[] shortcut, Vector2[] detour,
            Vector2 openTarget, Vector2 coveredTarget, Vector2 offset)
        {
            Require(OnPath(shortcut, openTarget) && OnPath(detour, coveredTarget), name + ": comparison points lie on the actual paths");
            var mamma = GameData.Enemy(EnemyKind.Mamma);
            Require(offset.magnitude < mamma.attackRange, name + ": attackers are inside throwing range");
            var openSource = Ground(openTarget + offset);
            var coveredSource = Ground(coveredTarget + offset);
            Require(!arena.Blocked(openSource, mamma.radius) && !arena.Blocked(coveredSource, mamma.radius), name + ": throwers can stand at the comparison points");
            Require(arena.LineOfSight(openSource, Ground(openTarget)), name + ": shortcut is exposed to the nearby thrower");
            Require(!arena.LineOfSight(coveredSource, Ground(coveredTarget)), name + ": physical cover blocks the detour thrower's line of sight");
            Debug.Log("ROMA_ROUTE_THROW_COVER " + name + ": equal attacker offset; shortcut exposed, detour occluded");
        }

        private static void CheckVespaLane(Arena arena)
        {
            var vespa = GameData.Enemy(EnemyKind.Vespista);
            var openTarget = Ground(new Vector2(120f, -74f));
            var coveredTarget = Ground(new Vector2(128f, -74f));
            var offset = new Vector3(0f, 0f, 12f);
            Require(vespa.behavior == Behavior.Charger && offset.magnitude < vespa.attackRange, "Vespa scenario uses the actual charge range");
            Require(!arena.Blocked(openTarget + offset, vespa.radius) && !arena.Blocked(coveredTarget + offset, vespa.radius), "Vespas can stand at both approach points");
            Require(arena.LineOfSight(openTarget + offset, openTarget)
                && arena.WalkClear(openTarget + offset, openTarget, vespa.radius), "Shortcut admits the LOS and body clearance required to start a charge");
            Require(!arena.LineOfSight(coveredTarget + offset, coveredTarget)
                && !arena.WalkClear(coveredTarget + offset, coveredTarget, vespa.radius), "Detour hedge cuts both sight and the straight charge lane");
            Debug.Log("ROMA_ROUTE_VESPA: 12m straight shortcut lane admits a charge; equivalent hedge-covered lane blocks it");
        }

        private static bool OnPath(Vector2[] path, Vector2 p)
        {
            for (int i = 1; i < path.Length; i++)
            {
                var d = path[i] - path[i - 1];
                float t = Mathf.Clamp01(Vector2.Dot(p - path[i - 1], d) / d.sqrMagnitude);
                if (Vector2.Distance(path[i - 1] + d * t, p) < 0.01f) return true;
            }
            return false;
        }

        private static Action CaptureLighting()
        {
            var light = UnityEngine.Object.FindAnyObjectByType<Light>();
            var color = light != null ? light.color : Color.white;
            float intensity = light != null ? light.intensity : 1f;
            var shadows = light != null ? light.shadows : LightShadows.None;
            float strength = light != null ? light.shadowStrength : 1f;
            var rotation = light != null ? light.transform.rotation : Quaternion.identity;
            var ambientMode = RenderSettings.ambientMode;
            var ambientLight = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            var fogMode = RenderSettings.fogMode;
            var fogColor = RenderSettings.fogColor;
            float fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance;
            var sky = Shader.GetGlobalColor("_PS_AmbientSky");
            var ground = Shader.GetGlobalColor("_PS_AmbientGround");
            return () =>
            {
                if (light != null)
                {
                    light.color = color; light.intensity = intensity; light.shadows = shadows;
                    light.shadowStrength = strength; light.transform.rotation = rotation;
                }
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
                RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
                Shader.SetGlobalColor("_PS_AmbientSky", sky);
                Shader.SetGlobalColor("_PS_AmbientGround", ground);
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Roma route verification: " + message);
        }
    }
}
