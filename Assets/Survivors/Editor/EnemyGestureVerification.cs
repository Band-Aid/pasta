using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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
                Require(gesturing > 0 && quiet > 0 && patterns.Count == 3, "Crowd contains quiet individuals and all three patterns");
                CheckInterruptions(root.transform);
                // Sampling the cosmetic layer must never consume the gameplay random stream.
                float actual = UnityEngine.Random.value;
                UnityEngine.Random.state = randomState;
                Require(actual == UnityEngine.Random.value, "Gameplay randomness is unchanged");
                Debug.Log("ENEMY_GESTURE_CHECKS_PASSED: patterns, free hands, walk/combat isolation, interruptions, pause and reuse");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(root);
            }
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
