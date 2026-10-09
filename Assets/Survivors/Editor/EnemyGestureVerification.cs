using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PastaSurvivors.EditorTools
{
    /// <summary>Checks the actual generated rigs without changing the open scene or running combat.</summary>
    public static class EnemyGestureVerification
    {
        private const float Dt = 1f / 60f;
        private static readonly FieldInfo Pattern = typeof(Enemy).GetField("gesturePattern", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Pasta Survivors/Verify Enemy Gestures")]
        public static void Check()
        {
            MenuLayoutVerification.Check();
            var randomState = UnityEngine.Random.state;
            var root = new GameObject("Temporary gesture checks") { hideFlags = HideFlags.HideAndDontSave };
            int gesturing = 0, quiet = 0;
            var patterns = new HashSet<int>();
            try
            {
                foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                {
                    var a = Create(kind, root.transform);
                    var b = Create(kind, root.transform);
                    bool eligible = !a.IsBoss && !a.IsProp && !a.rig.seated;
                    for (int uid = 1; uid <= 32; uid++)
                    {
                        Reset(a, uid); Reset(b, uid);
                        bool seen = false;
                        for (int frame = 0; frame < 360; frame++)
                        {
                            a.Animate(Dt, 2f, true);
                            b.Animate(Dt, 2f, false);
                            bool left = Different(a.rig.armL, b.rig.armL);
                            bool right = Different(a.rig.armR, b.rig.armR);
                            Require(!(left && right), "Only one arm gestures: " + kind);
                            Require(!left || a.rig.freeHandL, "Left held prop keeps its walk pose: " + kind);
                            Require(!right || a.rig.freeHandR, "Right held prop keeps its walk pose: " + kind);
                            Require(eligible || !(left || right), "Bosses, riders and props do not gesture: " + kind);
                            Require(!Different(a.rig.legL, b.rig.legL) && !Different(a.rig.legR, b.rig.legR)
                                && a.rig.torso.localPosition == b.rig.torso.localPosition, "Walk cycle is unchanged: " + kind);
                            Require(a.pos == b.pos && a.facing == b.facing && a.hp == b.hp
                                && a.attackTimer == b.attackTimer && a.stateTimer == b.stateTimer
                                && a.rig.root.localPosition == b.rig.root.localPosition,
                                "Gesture does not move the root or alter combat: " + kind);
                            if (left || right)
                            {
                                seen = true;
                                patterns.Add((int)Pattern.GetValue(a));
                            }
                        }
                        if (kind == EnemyKind.Signore) { if (seen) gesturing++; else quiet++; }
                    }
                    UnityEngine.Object.DestroyImmediate(a.rig.root.gameObject);
                    UnityEngine.Object.DestroyImmediate(b.rig.root.gameObject);
                }
                Require(gesturing == 32 && quiet == 0 && patterns.Count == 3, "100% includes every towns-person and all three patterns");
                CheckInterruptions(root.transform);
                CheckDensity(root.transform);
                CheckDensityDuringGesture(root.transform);
                // Sampling the cosmetic layer must never consume the gameplay random stream.
                float actual = UnityEngine.Random.value;
                UnityEngine.Random.state = randomState;
                Require(actual == UnityEngine.Random.value, "Gameplay randomness is unchanged");
                Debug.Log("ENEMY_GESTURE_CHECKS_PASSED: slider wiring, 0/25/50/75/100%, stable nested cohorts, next-start changes, patterns, free hands, walk/combat isolation, interruptions, pause and reuse");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckDensity(Transform parent)
        {
            var previousManager = G.Enemies;
            var ui = new GameObject("Density settings check");
            ui.transform.SetParent(parent, false);
            var manager = ui.AddComponent<EnemyManager>();
            var a = Create(EnemyKind.Signore, parent);
            var baseline = Create(EnemyKind.Signore, parent);
            try
            {
                G.Enemies = manager;
                Require(manager.GesturePercent == 100f, "Density defaults to 100%");
                UiKit.Init();
                var menus = ui.AddComponent<Menus>();
                menus.Build(ui.transform);
                menus.ShowGestureSettings(() => { });
                var slider = ui.GetComponentInChildren<Slider>();
                Require(slider != null && slider.minValue == 0f && slider.maxValue == 100f
                    && slider.value == 100f && slider.wholeNumbers, "Visible slider has 0-100% range and 100% default");
                float[] rates = { 0f, 25f, 50f, 75f, 100f };
                var counts = new int[rates.Length];
                const int population = 256;
                for (int uid = 1; uid <= population; uid++)
                {
                    bool previouslySelected = false;
                    for (int rate = 0; rate < rates.Length; rate++)
                    {
                        Reset(a, uid); Reset(baseline, uid);
                        slider.value = rates[rate];
                        Require(manager.GesturePercent == rates[rate], "Dragging slider updates runtime density");
                        bool selected = a.SelectedForGestures(manager.GesturePercent);
                        Require(!previouslySelected || selected, "Increasing density only adds people");
                        previouslySelected = selected;
                        if (selected) counts[rate]++;
                        bool seen = false;
                        for (int frame = 0; frame < 160; frame++)
                        {
                            a.Animate(Dt, 2f, true, manager.GesturePercent);
                            baseline.Animate(Dt, 2f, false);
                            seen |= Different(a.rig.armL, baseline.rig.armL) || Different(a.rig.armR, baseline.rig.armR);
                        }
                        Require(seen == selected, "Only selected people start gestures at " + rates[rate] + "%");
                        slider.value = 100f;
                        slider.value = 0f;
                        slider.value = rates[rate];
                        Require(a.SelectedForGestures(manager.GesturePercent) == selected,
                            "Returning to a percentage preserves membership after animation and slider changes");
                    }
                }
                Require(counts[0] == 0 && counts[4] == population, "0% selects nobody and 100% selects everybody");
                for (int i = 1; i < 4; i++)
                    Require(Mathf.Abs(counts[i] / (float)population - rates[i] / 100f) < 0.08f,
                        "Intermediate density follows the requested proportion");
                Debug.Log("GESTURE_DENSITY_COUNTS (0/25/50/75/100% of 256): " + string.Join(", ", counts));
                manager.GesturePercent = -10f;
                Require(manager.GesturePercent == 0f, "Density clamps below zero");
                manager.GesturePercent = 110f;
                Require(manager.GesturePercent == 100f, "Density clamps above 100%");
            }
            finally
            {
                G.Enemies = previousManager;
                UnityEngine.Object.DestroyImmediate(ui);
                UnityEngine.Object.DestroyImmediate(a.rig.root.gameObject);
                UnityEngine.Object.DestroyImmediate(baseline.rig.root.gameObject);
            }
        }

        private static void CheckDensityDuringGesture(Transform parent)
        {
            var a = Create(EnemyKind.Mamma, parent);
            var b = Create(EnemyKind.Mamma, parent);
            Reset(a, 1); Reset(b, 1);
            var timer = typeof(Enemy).GetField("gestureTime", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int frame = 0; frame < 180 && (float)timer.GetValue(a) < 0.25f; frame++)
            {
                a.Animate(Dt, 2f, true, 100f);
                b.Animate(Dt, 2f, true, 100f);
            }
            Require((float)timer.GetValue(a) >= 0.25f, "Gesture started before changing density");
            int remainingFrames = 0;
            do
            {
                a.Animate(Dt, 2f, true, 0f);
                b.Animate(Dt, 2f, true, 100f);
                Require(!Different(a.rig.armL, b.rig.armL) && !Different(a.rig.armR, b.rig.armR),
                    "Dropping density does not cancel or change an in-progress gesture");
                Require(++remainingFrames < 120, "In-progress gesture finishes naturally");
            } while ((float)timer.GetValue(a) > 0f);
            for (int frame = 0; frame < 240; frame++)
            {
                a.Animate(Dt, 2f, true, 0f);
                Require((float)timer.GetValue(a) == 0f, "0% prevents every subsequent gesture start");
            }
            a.Animate(Dt, 2f, true, 100f);
            Require((float)timer.GetValue(a) > 0f, "Raising density takes effect at the next eligible start without respawning");
            a.state = 1;
            a.Animate(Dt, 2f, true, 0f);
            Require((float)timer.GetValue(a) == 0f, "Attack still interrupts a gesture after density changes");
            a.state = 0;
            for (int frame = 0; frame < 180 && (float)timer.GetValue(a) <= 0f; frame++) a.Animate(Dt, 2f, true, 100f);
            Require((float)timer.GetValue(a) > 0f, "Gesture restarts after attack recovery");
            a.flash = 1f;
            a.Animate(Dt, 2f, true, 0f);
            Require((float)timer.GetValue(a) == 0f, "Being hit still interrupts a gesture after density changes");
            UnityEngine.Object.DestroyImmediate(a.rig.root.gameObject);
            UnityEngine.Object.DestroyImmediate(b.rig.root.gameObject);
        }

        private static void CheckInterruptions(Transform parent)
        {
            var a = Create(EnemyKind.Mamma, parent);
            var b = Create(EnemyKind.Mamma, parent);
            Action<Enemy>[] interrupt =
            {
                e => e.state = 1, e => e.state = 2, e => e.stun = 1f,
                e => e.flash = 1f, e => e.knock = Vector3.right,
                e => e.dominoTime = 1f, e => e.fear = 1f,
                e => e.fleeing = true, e => e.frozen = true, e => e.active = false
            };
            foreach (var stop in interrupt)
            {
                StartGesture(a, b);
                stop(a); stop(b);
                a.Animate(Dt, 2f, true); b.Animate(Dt, 2f, false);
                Require(!Different(a.rig.armL, b.rig.armL) && !Different(a.rig.armR, b.rig.armR),
                    "Attack, hit, panic, frozen or inactive pose takes priority immediately");
            }
            StartGesture(a, b);
            var left = a.rig.armL.localRotation;
            var right = a.rig.armR.localRotation;
            float clock = a.gesture;
            a.Animate(0f, 2f, true);
            Require(a.rig.armL.localRotation == left && a.rig.armR.localRotation == right && a.gesture == clock, "Zero dt pauses the gesture");
            a.Animate(Dt, 0f, true); b.Animate(Dt, 0f, false);
            Require(!Different(a.rig.armL, b.rig.armL), "Stopping movement cancels the gesture");
            StartGesture(a, b);
            a.Animate(Dt, 2f, false); b.Animate(Dt, 2f, false);
            Require(!Different(a.rig.armL, b.rig.armL), "Losing approach cancels the gesture");
            StartGesture(a, b);
            Reset(a, a.uid); Reset(b, a.uid);
            for (int i = 0; i < 180; i++)
            {
                a.Animate(Dt, 2f, true); b.Animate(Dt, 2f, true);
                Require(!Different(a.rig.armL, b.rig.armL) && !Different(a.rig.armR, b.rig.armR), "Pooled reuse resets gesture timing");
            }
        }

        private static void StartGesture(Enemy a, Enemy b)
        {
            for (int uid = 1; uid <= 32; uid++)
            {
                Reset(a, uid); Reset(b, uid);
                for (int i = 0; i < 180; i++)
                {
                    a.Animate(Dt, 2f, true); b.Animate(Dt, 2f, false);
                    if (Quaternion.Angle(a.rig.armL.localRotation, b.rig.armL.localRotation) > 45f) return;
                }
            }
            throw new InvalidOperationException("No free-hand gesture appeared");
        }

        private static Enemy Create(EnemyKind kind, Transform parent)
        {
            var def = GameData.Enemy(kind);
            return new Enemy { def = def, scale = def.scale, rig = Models.Build(Models.Enemy(kind), parent, kind.ToString()) };
        }

        private static void Reset(Enemy e, int uid)
        {
            e.uid = uid; e.active = true; e.frozen = e.fleeing = false;
            e.state = 0; e.stun = e.flash = e.fear = e.dominoTime = 0f;
            e.knock = Vector3.zero; e.walkPhase = e.gesture = 0f;
            e.hp = 10f; e.attackTimer = 0.7f; e.stateTimer = 0.3f;
            e.ResetApproachGesture();
        }

        private static bool Different(Transform a, Transform b)
            => a != null && b != null && Quaternion.Angle(a.localRotation, b.localRotation) > 0.1f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Enemy gesture verification: " + message);
        }
    }
}
