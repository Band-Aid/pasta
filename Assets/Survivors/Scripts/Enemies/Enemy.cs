using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>Plain data + visual for one Italian. Behaviour lives in EnemyManager so hundreds update in one loop.</summary>
    public class Enemy
    {
        public const int ImmuneSlots = 12;
        public const int SlotDomino = 8, SlotExplosion = 9, SlotQuake = 10;

        public int uid;
        public EnemyDef def;
        public Rig rig;
        public Vector3 pos, facing = Vector3.back, knock;
        public float hp, maxHp, damage, speedMul = 1f, radius, scale;
        public float flash, stun, slow, buff;
        /// <summary>Scared (Sta○ Beam): runs away from the player with arms in the air.</summary>
        public float fear;
        public bool flashing, fleeing, frozen, active;
        public float fleeT;
        public Vector3 fleeDir;
        public readonly float[] immune = new float[ImmuneSlots];
        public float walkPhase, attackTimer, stateTimer, gesture;
        public int state, pattern, volleys;
        public Vector3 lockDir;
        public int cell;
        public float lift, spin;

        // Cosmetic randomness stays separate from combat/spawn randomness, including when pooled.
        private System.Random gestureRandom;
        private bool gestureRight;
        private double gestureRoll;
        private int gesturePattern;
        private float gestureWait, gestureTime, gestureDuration;

        public void ResetApproachGesture()
        {
            gestureRandom = new System.Random(unchecked(uid * 486187739 + 17));
            // One fixed draw per spawned person, never re-rolled when the slider changes.
            gestureRoll = gestureRandom.NextDouble();
            gestureWait = GestureRange(0.15f, 1.2f);
            gestureTime = gestureDuration = 0f;
        }

        private float GestureRange(float min, float max) => Mathf.Lerp(min, max, (float)gestureRandom.NextDouble());

        public bool SelectedForGestures(float percent) => percent > 0f
            && (percent >= 100f || gestureRoll < percent / 100.0);

        // Navigation: walking direction (flow field or straight line) and whether they can see/throw at the player.
        public Vector3 nav;
        public bool los = true, walkClear = true;

        // Spaghetti Domino: launched Italians become projectiles for a moment.
        public float dominoTime, dominoDamage;
        public Vector3 dominoVel;

        public bool IsBoss => def.behavior == Behavior.Boss;
        public bool IsElite => def.behavior == Behavior.Elite;
        public bool IsProp => def.behavior == Behavior.Prop;
        public bool Targetable => active && !fleeing && !IsProp;
        public float HpFraction => maxHp > 0 ? Mathf.Clamp01(hp / maxHp) : 0f;
        public Vector3 Center => pos + Vector3.up * (0.9f * scale);

        public void SetFlash(bool on)
        {
            if (flashing == on) return;
            flashing = on;
            var m = on ? Mats.Flash : Mats.Lit;
            foreach (var r in rig.renderers) r.sharedMaterial = m;
        }

        public void ApplyTransform()
        {
            var t = rig.root;
            t.position = pos + Vector3.up * lift;
            if (facing.sqrMagnitude > 0.0001f)
            {
                var rot = Quaternion.LookRotation(facing);
                if (spin != 0f) rot = rot * Quaternion.Euler(0f, 0f, spin);
                t.rotation = rot;
            }
        }

        public void Animate(float dt, float moveSpeed, bool approaching = false, float gesturePercent = 100f)
        {
            var r = rig;
            if (r.torso == null || dt <= 0f) return;
            bool canGesture = approaching && moveSpeed > 0.1f && active
                && !frozen && !fleeing && fear <= 0f && stun <= 0f && flash <= 0f
                && dominoTime <= 0f && knock.sqrMagnitude < 0.04f && state == 0
                && !IsBoss && !IsProp && !r.seated && (r.freeHandL || r.freeHandR);
            if (!canGesture)
            {
                // Never resume a half-finished gesture after an attack, hit or loss of approach.
                if (gestureTime > 0f) gestureWait = GestureRange(0.8f, 1.8f);
                gestureTime = 0f;
            }
            float amp = Mathf.Clamp01(moveSpeed / 2f);
            bool panic = fleeing || fear > 0f;
            float rate = panic ? 22f : 5f + moveSpeed * 2.4f;
            walkPhase += dt * rate;
            float s = Mathf.Sin(walkPhase);
            gesture += dt;
            if (r.wheels != null)
            {
                // Roll with the ground speed.
                r.wheelAngle = Mathf.Repeat(r.wheelAngle + moveSpeed / (r.wheelRadius * scale) * Mathf.Rad2Deg * dt, 360f);
                var roll = Quaternion.Euler(r.wheelAngle, 0f, 0f);
                foreach (var w in r.wheels) w.localRotation = roll;
            }
            if (r.legL != null)
            {
                float legAmp = panic ? 55f : 34f * amp;
                r.legL.localRotation = Quaternion.Euler(s * legAmp, 0, 0);
                r.legR.localRotation = Quaternion.Euler(-s * legAmp, 0, 0);
            }
            float bob = r.seated ? Mathf.Abs(Mathf.Sin(walkPhase * 0.5f)) * 0.03f : Mathf.Abs(Mathf.Cos(walkPhase)) * 0.07f * Mathf.Max(amp, panic ? 1f : 0f);
            r.torso.localPosition = new Vector3(0f, bob, 0f);
            if (r.armL == null) return;
            if (panic)
            {
                float w = Mathf.Sin(gesture * 24f) * 25f;
                r.armL.localRotation = Quaternion.Euler(-165f + w, 0f, -25f);
                r.armR.localRotation = Quaternion.Euler(-165f - w, 0f, 25f);
                return;
            }
            if (state == 1 && def.behavior != Behavior.Chase && def.behavior != Behavior.Elite)
            {
                // Wind-up: throwing arm back and up.
                r.armR.localRotation = Quaternion.Euler(-160f + Mathf.Sin(gesture * 30f) * 6f, 0f, 10f);
                r.armL.localRotation = Quaternion.Euler(r.armRest - 20f, 0f, -10f);
                return;
            }
            if (r.seated)
            {
                r.armL.localRotation = Quaternion.Euler(r.armRest, 0f, 8f);
                r.armR.localRotation = Quaternion.Euler(r.armRest, 0f, -8f);
                return;
            }
            float swing = s * 28f * amp;
            r.armL.localRotation = Quaternion.Euler(r.armRest - swing, 0f, -4f);
            r.armR.localRotation = Quaternion.Euler(r.armRest + swing, 0f, 4f);
            if (canGesture) AnimateApproachGesture(dt, gesturePercent);
        }

        private void AnimateApproachGesture(float dt, float gesturePercent)
        {
            if (gestureTime <= 0f)
            {
                gestureWait -= dt;
                if (gestureWait > 0f) return;
                // Density only gates the next start. An in-progress gesture still finishes
                // naturally unless combat or loss of approach interrupts it above.
                if (!SelectedForGestures(gesturePercent)) return;
                gesturePattern = gestureRandom.Next(3);
                gestureRight = rig.freeHandR && (!rig.freeHandL || gestureRandom.Next(2) == 0);
                gestureDuration = GestureRange(0.95f, 1.25f);
            }
            gestureTime += dt;
            if (gestureTime >= gestureDuration)
            {
                gestureTime = 0f;
                gestureWait = GestureRange(0.8f, 1.8f);
                return;
            }

            // Broad, quick shoulder-height motions with a readable 0.14s held pose.
            float phase = Mathf.Clamp01((gestureTime - 0.12f) / (gestureDuration - 0.46f));
            float wave = Mathf.Sin(phase * Mathf.PI * 4f);
            float side = gestureRight ? 1f : -1f;
            float pitch = -110f, yaw = -side * 18f;
            if (gesturePattern == 0) pitch += wave * 33f;
            else if (gesturePattern == 1) yaw += wave * 36f;
            else pitch -= Mathf.Sin(phase * Mathf.PI * 2f) * 36f;

            float weight = Mathf.SmoothStep(0f, 1f, gestureTime / 0.12f)
                * Mathf.SmoothStep(0f, 1f, (gestureDuration - gestureTime) / 0.2f);
            var arm = gestureRight ? rig.armR : rig.armL;
            arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.Euler(pitch, yaw, side * 24f), weight);
        }
    }
}
