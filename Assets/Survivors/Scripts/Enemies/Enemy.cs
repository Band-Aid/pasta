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
        public int appearanceVariant;
        public Rig rig;
        public Vector3 pos, facing = Vector3.back, knock;
        public float hp, maxHp, damage, speedMul = 1f, radius, scale;
        public float flash, stun, slow, buff;
        /// <summary>Scared (Frappé Beam): runs away from the player with arms in the air.</summary>
        public float fear;
        /// <summary>Slipped on mayonnaise: time left flat on the back, and until the next slip can happen.</summary>
        public float slip, slipCooldown;
        public const float SlipTime = 0.9f;
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
        private Quaternion gestureBaseRotation;

        public void ResetApproachGesture()
        {
            gestureRandom = new System.Random(unchecked(uid * 486187739 + 17));
            // Keep each person's draw fixed while the live percentage changes.
            gestureRoll = gestureRandom.NextDouble();
            gestureWait = GestureRange(0.1f, 0.75f);
            gestureTime = gestureDuration = 0f;
            ResetArmPose(0f);
        }

        private float GestureRange(float min, float max) => Mathf.Lerp(min, max, (float)gestureRandom.NextDouble());

        public bool SelectedForGestures(float percent) => percent > 0f
            && (percent >= 100f || gestureRoll < percent / 100.0);

        public void InterruptApproachGesture()
        {
            if (gestureTime <= 0f) return;
            // Damage can arrive after Animate in the same frame. Remove its overlay immediately.
            var arm = gestureRight ? rig.armR : rig.armL;
            arm.localRotation = gestureBaseRotation;
            gestureTime = 0f;
            gestureWait = GestureRange(0.45f, 1.1f);
        }

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
            // Spawned, fleeing and recycled actors also follow the raised walking surface.
            if (G.Arena != null) pos.y = G.Arena.GroundHeight(pos);
            var t = rig.root;
            // Slipping: the feet shoot up, the body lands flat on its back, then gets up again.
            float fall = 0f, hop = 0f;
            if (slip > 0f && !fleeing)
            {
                float k = 1f - slip / SlipTime;
                fall = 80f * Mathf.Clamp01(k / 0.15f) * Mathf.Clamp01((1f - k) / 0.25f);
                hop = Mathf.Sin(Mathf.Clamp01(k / 0.3f) * Mathf.PI) * 0.35f;
            }
            t.position = pos + Vector3.up * (lift + hop);
            if (facing.sqrMagnitude > 0.0001f)
            {
                var rot = Quaternion.LookRotation(facing);
                if (spin != 0f) rot = rot * Quaternion.Euler(0f, 0f, spin);
                if (fall > 0f) rot = rot * Quaternion.Euler(-fall, 0f, 0f);
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
            // Never resume a half-finished gesture after an attack, hit or loss of approach.
            if (!canGesture) InterruptApproachGesture();
            var g = r.gait;
            float amp = Mathf.Clamp01(moveSpeed / 2f);
            bool panic = fleeing || fear > 0f;
            float move = Mathf.Max(amp, panic ? 1f : 0f);
            float rate = panic ? 22f : (5f + moveSpeed * 2.4f) * g.cadence;
            walkPhase += dt * rate;
            float s = Mathf.Sin(walkPhase), c = Mathf.Cos(walkPhase);
            gesture += dt;
            if (r.wheels != null)
            {
                // Roll with the ground speed.
                r.wheelAngle = Mathf.Repeat(r.wheelAngle + moveSpeed / (r.wheelRadius * scale) * Mathf.Rad2Deg * dt, 360f);
                var roll = Quaternion.Euler(r.wheelAngle, 0f, 0f);
                foreach (var w in r.wheels) w.localRotation = roll;
                if (r.crank != null) r.crank.localRotation = Quaternion.Euler(r.wheelAngle * 0.5f, 0f, 0f);
            }
            if (r.legL != null)
            {
                float legAmp = panic ? 55f : g.stride * amp;
                r.legL.localRotation = Quaternion.Euler(s * legAmp, -g.toeOut, -g.legOut);
                r.legR.localRotation = Quaternion.Euler(-s * legAmp, g.toeOut, g.legOut);
            }
            float bob = r.seated ? Mathf.Abs(Mathf.Sin(walkPhase * 0.5f)) * g.bob : Mathf.Abs(c) * g.bob * move;
            r.torso.localPosition = new Vector3(0f, bob, 0f);
            // Vehicles weave from side to side; people lean, sway their hips and swing their shoulders.
            if (g.roll != 0f) r.torso.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase * 0.21f) * g.roll * amp);
            if (r.spine != null) r.spine.localRotation = Quaternion.Euler(g.lean * amp + (panic ? 10f : 0f), s * g.twist * move, s * g.sway * move);
            if (r.head != null)
            {
                // Bursts of "no, no, no", a nod on each step, a singer's roll, or the handlebars steering.
                float shake = g.headShake * Mathf.Sin(gesture * 11f) * Mathf.Max(0f, Mathf.Sin(gesture * 1.7f));
                float steer = g.headSteer * Mathf.Sin(walkPhase * 0.21f + 0.6f) * amp;
                r.head.localRotation = Quaternion.Euler(g.headPitch + Mathf.Abs(c) * g.headNod * move, shake + steer, Mathf.Sin(walkPhase * 0.5f) * g.headRoll * move);
            }
            if (r.belly != null)
            {
                // The belly lags behind each step and wobbles.
                float j = Mathf.Cos(2f * walkPhase - 1.3f) * g.belly * move;
                r.belly.localPosition = r.bellyPos + new Vector3(0f, -0.025f * j, 0f);
                r.belly.localScale = new Vector3(1f + 0.04f * j, 1f - 0.05f * j, 1f + 0.04f * j);
            }
            if (r.prop != null) AnimateProp(s, c, move);
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
                r.armL.localRotation = Quaternion.Euler(r.restL.x - 20f, r.restL.y, r.restL.z - 6f);
                return;
            }
            ResetArmPose(s * g.armSwing * amp);
            if (canGesture) AnimateApproachGesture(dt, gesturePercent);
        }

        private void AnimateProp(float s, float c, float move)
        {
            var r = rig;
            switch (r.propMotion)
            {
                case PropMotion.Wave: // a flag flapping on its pole
                    r.prop.localRotation = r.propBase * Quaternion.Euler(0f, Mathf.Sin(gesture * 8f) * 22f + Mathf.Sin(gesture * 13f) * 8f, 0f);
                    break;
                case PropMotion.Spin: // pizza dough spinning on a fingertip
                    r.prop.localRotation = r.propBase * Quaternion.Euler(0f, Mathf.Repeat(gesture * 600f, 360f), 0f);
                    break;
                case PropMotion.Swing: // a heavy bag swinging at the hip
                    r.prop.localRotation = r.propBase * Quaternion.Euler(s * 14f * move, 0f, c * 6f * move);
                    break;
            }
        }

        private void ResetArmPose(float swing)
        {
            var r = rig;
            if (r.armL == null || r.armR == null) return;
            if (r.seated)
            {
                r.armL.localRotation = Quaternion.Euler(r.restL);
                r.armR.localRotation = Quaternion.Euler(r.restR);
                return;
            }
            var g = r.gait;
            r.armL.localRotation = Quaternion.Euler(r.restL.x - swing * g.swingL, r.restL.y, r.restL.z - g.armOut);
            r.armR.localRotation = Quaternion.Euler(r.restR.x + swing * g.swingR, r.restR.y, r.restR.z + g.armOut);
        }

        private void AnimateApproachGesture(float dt, float gesturePercent)
        {
            if (gestureTime <= 0f)
            {
                gestureWait = Mathf.Max(0f, gestureWait - dt);
                if (gestureWait > 0f) return;
                // A percentage change gates the next start, without cutting off a gesture.
                // Combat and loss of approach still interrupt it immediately in Animate.
                if (!SelectedForGestures(gesturePercent)) return;
                gesturePattern = gestureRandom.Next(3);
                gestureRight = rig.freeHandR && (!rig.freeHandL || gestureRandom.Next(2) == 0);
                gestureDuration = GestureRange(1.65f, 2.05f);
            }
            gestureTime += dt;
            if (gestureTime >= gestureDuration)
            {
                gestureTime = 0f;
                gestureWait = GestureRange(0.45f, 1.1f);
                return;
            }

            // Head-high pumps, a broad sideways shake, or a sweeping palm-up appeal.
            // Keep the hand outside the torso silhouette in both camera modes. Only the
            // free arm moves: the weapon hand, body, legs and vehicle keep their base pose.
            const float enter = 0.16f, hold = 0.2f, exit = 0.2f;
            float phase = Mathf.Clamp01((gestureTime - enter) / (gestureDuration - enter - hold - exit));
            float wave = Mathf.Cos(phase * Mathf.PI * 4f);
            float side = gestureRight ? 1f : -1f;
            float pitch, yaw, roll;
            if (gesturePattern == 0)
            {
                pitch = -108f - wave * 38f;
                yaw = -side * 10f;
                roll = side * 30f;
            }
            else if (gesturePattern == 1)
            {
                pitch = -105f;
                yaw = side * (20f + wave * 44f);
                roll = side * 40f;
            }
            else
            {
                float sweep = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
                pitch = -90f - sweep * 52f;
                yaw = side * 12f;
                roll = side * (60f - sweep * 25f);
            }

            float weight = Mathf.SmoothStep(0f, 1f, gestureTime / enter)
                * Mathf.SmoothStep(0f, 1f, (gestureDuration - gestureTime) / exit);
            var arm = gestureRight ? rig.armR : rig.armL;
            gestureBaseRotation = arm.localRotation;
            arm.localRotation = Quaternion.Slerp(gestureBaseRotation, Quaternion.Euler(pitch, yaw, roll), weight);
        }
    }
}
