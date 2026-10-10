using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public abstract class Weapon
    {
        public WeaponDef def;
        public int level = 1;
        public bool evolved;
        protected float timer = 0.4f;
        protected readonly List<Enemy> hits = new List<Enemy>(128);
        private readonly List<(float at, System.Action act)> queued = new List<(float, System.Action)>();

        protected Player P => G.Player;
        public WeaponId Id => def.id;
        public int Slot => (int)def.id;
        public string DisplayName => evolved ? def.evoName : def.name;
        public bool MaxedOut => level >= WeaponDef.MaxLevel;

        public static Weapon Create(WeaponId id)
        {
            Weapon w;
            switch (id)
            {
                case WeaponId.SpaghettiSnap: w = new SpaghettiSnap(); break;
                case WeaponId.PenneShot: w = new PenneShot(); break;
                case WeaponId.LasagnaCrash: w = new LasagnaCrash(); break;
                case WeaponId.FusilliOrbit: w = new FusilliOrbit(); break;
                case WeaponId.FarfalleBoomerang: w = new FarfalleBoomerang(); break;
                case WeaponId.KetchupBomb: w = new KetchupBomb(); break;
                case WeaponId.PineapplePizza: w = new PineapplePizza(); break;
                default: w = new CarbonaraAura(); break;
            }
            w.def = GameData.Weapon(id);
            return w;
        }

        public WStats Raw
        {
            get
            {
                var s = def.baseStats;
                for (int i = 0; i < level - 1 && i < def.levels.Length; i++) s += def.levels[i];
                if (evolved) s += def.evoBonus;
                return s;
            }
        }

        public const float MainDamage = 1.4f, MainCooldown = 0.75f, MainArea = 1.15f;
        public bool IsMain => P != null && P.Main == this;
        /// <summary>The main weapon fires where the player aims (crosshair, mouse cursor or right stick).</summary>
        protected bool Aimed => IsMain && P.HasAim;

        protected float Dmg(WStats s) => s.damage * P.Might * (IsMain ? MainDamage : 1f);
        protected float Cd(WStats s) => Mathf.Max(0.06f, s.cooldown * P.CooldownMul * P.BuffCdMul * (IsMain ? MainCooldown : 1f));
        protected float Area(WStats s) => s.area * P.AreaMul * (IsMain ? MainArea : 1f);
        protected int Amount(WStats s) => s.amount + P.AmountBonus + (IsMain && MainAddsAmount ? 1 : 0);
        protected virtual bool MainAddsAmount => false;
        protected float Dur(WStats s) => s.duration * P.DurationMul;

        protected void Later(float delay, System.Action act) => queued.Add((Time.time + delay, act));

        public virtual void Tick(float dt)
        {
            for (int i = queued.Count - 1; i >= 0; i--)
                if (Time.time >= queued[i].at)
                {
                    var act = queued[i].act;
                    queued.RemoveAt(i);
                    act();
                }
            timer -= dt;
            if (timer <= 0f)
            {
                var s = Raw;
                Fire(s);
                timer = Mathf.Max(timer + NextDelay(s), 0.05f);
            }
        }

        protected virtual float NextDelay(WStats s) => Cd(s);
        protected abstract void Fire(WStats s);
        public virtual void Removed() { }

        protected Vector3 AimAtNearest(float range, out Enemy target)
        {
            target = G.Enemies.Nearest(P.Position, range, -1, true);
            if (target == null) return P.Facing;
            var d = target.pos - P.Position; d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : P.Facing;
        }
    }

    // ------------------------------------------------------------------

    public class SpaghettiSnap : Weapon
    {
        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            // Snap toward the closest Italian in reach, otherwise where we're heading.
            Vector3 f = P.Facing;
            var target = Aimed ? null : G.Enemies.Nearest(P.Position, 4.2f * Area(s) + 2.5f, -1, true);
            if (Aimed) f = P.AimDir;
            else if (target != null)
            {
                var d = target.pos - P.Position; d.y = 0f;
                if (d.sqrMagnitude > 0.01f) f = d.normalized;
            }
            for (int k = 0; k < n; k++)
            {
                Vector3 dir = k == 0 ? f : k == 1 ? -f : Quaternion.Euler(0, k == 2 ? 90 : -90, 0) * f;
                float delay = k * 0.14f;
                if (delay <= 0f) Snap(s, dir);
                else Later(delay, () => Snap(Raw, dir));
            }
        }

        private void Snap(WStats s, Vector3 dir)
        {
            float radius = 4.2f * Area(s);
            float half = 55f;
            Vector3 origin = P.Position;
            G.Fx.Snap(origin, dir, radius, half * 2f, evolved);
            P.Anim.Snap();
            G.Sfx.Play(SfxId.Snap, 0.9f, Random.Range(0.92f, 1.08f));
            if (P.FirstPerson && IsMain) G.Cam.Kick(1.2f);
            int n = G.Enemies.Query(origin + dir * radius * 0.5f, radius * 0.75f, hits);
            float dmg = Dmg(s);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                Vector3 d = e.pos - origin; d.y = 0f;
                if (d.magnitude > radius + e.radius) continue;
                if (d.magnitude > 0.6f && Vector3.Angle(dir, d) > half + 10f) continue;
                G.Enemies.Damage(e, dmg, dir + d.normalized * 0.4f, s.knockback, Slot);
                if (IsMain) e.stun = Mathf.Max(e.stun, 0.6f);
                if (evolved && e.active && !e.fleeing) G.Enemies.Launch(e, (dir + d.normalized * 0.3f).normalized * 17f, dmg * 0.7f);
            }
            if (n > 0) G.Cam.Shake(0.08f);
        }
    }

    public class PenneShot : Weapon
    {
        private readonly List<Enemy> targets = new List<Enemy>();
        private int cycle;

        protected override float NextDelay(WStats s) => evolved ? Mathf.Max(0.05f, 0.13f * P.CooldownMul * P.BuffCdMul * (IsMain ? MainCooldown : 1f)) : Cd(s);
        protected override bool MainAddsAmount => true;

        protected override void Fire(WStats s)
        {
            int n = evolved ? 1 : Amount(s);
            int found = G.Enemies.NearestSeveral(P.Position, 20f, evolved ? Mathf.Max(1, Amount(s)) : n, targets, true);
            for (int k = 0; k < n; k++)
            {
                Enemy t = found > 0 && !Aimed ? targets[(k + cycle) % found] : null;
                Vector3 dir;
                if (Aimed) dir = Quaternion.Euler(0, (k - (n - 1) * 0.5f) * 5f, 0) * P.AimDir;
                else if (t != null) { dir = t.pos - P.Position; dir.y = 0; dir.Normalize(); }
                else dir = Quaternion.Euler(0, (k - (n - 1) * 0.5f) * 12f, 0) * P.Facing;
                float delay = k * 0.07f;
                if (delay <= 0f) Shoot(s, dir);
                else Later(delay, () => Shoot(Raw, dir));
            }
            cycle++;
            if (n > 0) P.Anim.Throw();
        }

        private void Shoot(WStats s, Vector3 dir)
        {
            var shot = G.Shots.Fire(ShotKind.Penne, Motion.Straight, P.Position + Vector3.up * 1.1f + dir * 0.5f, dir * s.speed,
                Dmg(s), 0.35f * Area(s), Dur(s), Slot);
            shot.pierce = s.pierce + (IsMain ? 1 : 0);
            shot.knock = s.knockback;
            shot.spinRate = 900f;
            shot.scale = evolved ? 1.35f : 1f;
            G.Sfx.Play(SfxId.Pop, 0.22f, Random.Range(1.3f, 1.6f));
        }
    }

    public class LasagnaCrash : Weapon
    {
        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            for (int k = 0; k < n; k++)
            {
                float mul = 1f + k * 0.3f;
                if (k == 0) Slam(s, mul);
                else Later(k * 0.35f, () => Slam(Raw, mul));
            }
        }

        private void Slam(WStats s, float mul)
        {
            float radius = 3.3f * Area(s) * mul;
            Vector3 c = P.Position;
            P.Anim.Slam();
            G.Fx.Slam(c, radius);
            G.Sfx.Play(SfxId.Slam, 0.8f, Random.Range(0.9f, 1.05f));
            G.Cam.Shake(0.28f);
            Ring(s, c, 0f, radius);
            if (evolved)
            {
                Later(0.25f, () => { G.Fx.Slam(c, radius * 1.6f); Ring(Raw, c, radius * 0.9f, radius * 1.6f); });
                Later(0.5f, () => { G.Fx.Slam(c, radius * 2.2f); Ring(Raw, c, radius * 1.5f, radius * 2.2f); G.Cam.Shake(0.3f); });
            }
        }

        private void Ring(WStats s, Vector3 c, float inner, float outer)
        {
            int n = G.Enemies.Query(c, outer, hits);
            float dmg = Dmg(s);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                Vector3 d = e.pos - c; d.y = 0;
                if (d.magnitude + e.radius < inner) continue;
                G.Enemies.Damage(e, dmg, d, s.knockback * (IsMain ? 2f : 1f), Slot);
                if (evolved) e.stun = Mathf.Max(e.stun, 0.8f);
            }
        }
    }

    public class FusilliOrbit : Weapon
    {
        private readonly List<Shot> spinning = new List<Shot>();

        protected override float NextDelay(WStats s) => evolved ? 0.5f : Dur(s) + Cd(s);

        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            if (evolved)
            {
                spinning.RemoveAll(x => !x.active || x.kind != ShotKind.Fusilli);
                if (spinning.Count == n)
                {
                    foreach (var x in spinning) Configure(x, s);
                    return;
                }
            }
            foreach (var x in spinning) if (x.active && x.kind == ShotKind.Fusilli) x.life = 0f;
            spinning.Clear();
            for (int k = 0; k < n; k++)
            {
                var shot = G.Shots.Fire(ShotKind.Fusilli, Motion.Orbit, P.Position, Vector3.zero, Dmg(s), 0.55f, 1f, Slot);
                shot.angle = k * Mathf.PI * 2f / n;
                Configure(shot, s);
                spinning.Add(shot);
            }
            G.Sfx.Play(SfxId.Whoosh, 0.4f, 1.4f);
        }

        private void Configure(Shot shot, WStats s)
        {
            shot.orbitRadius = 2.6f * Area(s);
            shot.orbitSpeed = 3.3f * s.speed;
            shot.damage = Dmg(s);
            shot.radius = 0.55f * Mathf.Sqrt(Area(s));
            shot.scale = 1.2f * Mathf.Sqrt(Area(s));
            shot.pierce = 999;
            shot.hitCooldown = 0.45f;
            shot.knock = s.knockback;
            shot.spinRate = 720f;
            shot.life = shot.maxLife = evolved ? 99999f : Dur(s);
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (!IsMain) return;
            // Main trait: the spinning fusilli swat thrown slippers and dough out of the air.
            var shots = G.Shots.Active;
            foreach (var f in spinning)
            {
                if (!f.active || f.kind != ShotKind.Fusilli) continue;
                for (int i = 0; i < shots.Count; i++)
                {
                    var h = shots[i];
                    if (!h.hostile || h.life <= 0f) continue;
                    var d = h.pos - f.pos; d.y = 0f;
                    if (d.sqrMagnitude > 1.1f * 1.1f) continue;
                    h.life = 0f;
                    G.Fx.Burst(h.pos, Models.PastaGold, 6, 4f, 0.15f, FxKind.Crumb);
                    G.Sfx.Play(SfxId.Pop, 0.4f, 0.8f);
                }
            }
        }

        public override void Removed()
        {
            foreach (var x in spinning) if (x.active) x.life = 0f;
            spinning.Clear();
        }
    }

    public class FarfalleBoomerang : Weapon
    {
        protected override bool MainAddsAmount => true;

        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            Vector3 aim = Aimed ? P.AimDir : AimAtNearest(16f, out _);
            for (int k = 0; k < n; k++)
            {
                float spread = (k - (n - 1) * 0.5f) * (evolved ? 25f : 14f);
                Vector3 dir = Quaternion.Euler(0, spread, 0) * aim;
                if (k == 0) Throw(s, dir);
                else Later(k * 0.09f, () => Throw(Raw, dir));
            }
            P.Anim.Throw();
        }

        private void Throw(WStats s, Vector3 dir)
        {
            float area = Area(s);
            var shot = G.Shots.Fire(ShotKind.Farfalle, Motion.Boomerang, P.Position + Vector3.up * 0.9f, dir * s.speed, Dmg(s), 0.6f * area, 3.2f, Slot);
            shot.orbitSpeed = 0.75f;
            shot.pierce = 999;
            shot.hitCooldown = 0.35f;
            shot.knock = s.knockback;
            shot.spinRate = 900f;
            shot.scale = 1.4f * area;
            G.Sfx.Play(SfxId.Whoosh, 0.35f, 1.2f);
        }
    }

    public class KetchupBomb : Weapon
    {
        private readonly List<Vector3> volley = new List<Vector3>();

        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            volley.Clear();
            for (int k = 0; k < n; k++)
            {
                // Aim for the thickest crowd, spreading multiple bottles over different crowds.
                var t = Aimed ? null : G.Enemies.DensestNear(P.Position, 12f, 2.3f * Area(s), 14, volley);
                Vector3 target;
                if (Aimed)
                {
                    var j = Random.insideUnitCircle * (k == 0 ? 0f : 2.2f);
                    target = P.AimPoint(13f) + new Vector3(j.x, 0f, j.y);
                }
                else target = t != null ? t.pos : P.Position + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(3f, 7f);
                target = G.Arena.Clamp(target, 1f);
                volley.Add(target);
                float delay = k * 0.12f;
                if (delay <= 0f) Lob(s, target);
                else Later(delay, () => Lob(Raw, target));
            }
            P.Anim.Throw();
        }

        private void Lob(WStats s, Vector3 target)
        {
            var shot = G.Shots.Fire(ShotKind.Ketchup, Motion.Lob, P.Position + Vector3.up * 1.4f, Vector3.zero, 0f, 0.3f, 0.55f, Slot);
            shot.target = target;
            shot.spinRate = 900f;
            float area = Area(s), dmg = Dmg(s), dur = Dur(s), kb = s.knockback;
            bool evo = evolved, sticky = IsMain;
            int slot = Slot;
            shot.onEnd = x =>
            {
                float radius = 2.3f * area;
                // The bottle bursts: a burning splash on impact, then the puddle keeps cooking.
                int hit = G.Enemies.Query(x.target, radius, hits);
                for (int i = 0; i < hit; i++)
                {
                    var e = hits[i];
                    G.Enemies.Damage(e, dmg * 2f, e.pos - x.target, kb, slot);
                    e.slow = Mathf.Max(e.slow, 1f);
                }
                G.Fx.Ring(x.target + Vector3.up * 0.1f, radius, new Color(1f, 0.25f, 0.1f, 0.8f), 0.3f, 0.5f);
                var zone = G.Shots.Zone(x.target, radius, dmg, dur, 0.35f, slot, new Color(0.85f, 0.08f, 0.05f, 0.6f), 1f);
                zone.orbitSpeed = evo ? 1.8f : 0f;
                zone.sticky = sticky;
                G.Fx.Burst(x.target + Vector3.up * 0.2f, new Color(0.9f, 0.1f, 0.05f), 12, 5f, 0.18f, FxKind.Crumb);
                G.Sfx.Play(SfxId.Splat, 0.45f, Random.Range(0.9f, 1.1f));
            };
        }
    }

    public class PineapplePizza : Weapon
    {
        protected override bool MainAddsAmount => true;

        protected override void Fire(WStats s)
        {
            int n = Amount(s);
            float baseAngle = Aimed ? Quaternion.LookRotation(P.AimDir).eulerAngles.y : Random.value * 360f;
            for (int k = 0; k < n; k++)
            {
                float spread = Aimed ? (k - (n - 1) * 0.5f) * 14f : k * 360f / n + Random.Range(-20f, 20f);
                Vector3 dir = Quaternion.Euler(0, baseAngle + spread, 0) * Vector3.forward;
                float area = Area(s);
                var shot = G.Shots.Fire(ShotKind.Pizza, Motion.Bounce, P.Position + Vector3.up * 0.8f, dir * s.speed * (IsMain ? 1.3f : 1f), Dmg(s), 0.7f * area, Dur(s), Slot);
                shot.pierce = 999;
                shot.hitCooldown = 0.4f;
                shot.knock = s.knockback;
                shot.spinRate = 400f;
                shot.scale = 1.1f * area;
                if (evolved)
                {
                    shot.explodeRadius = 2.3f * area;
                    shot.explodeDamage = Dmg(s) * 1.2f;
                }
            }
            P.Anim.Throw();
            G.Sfx.Play(SfxId.Whoosh, 0.35f, 0.9f);
        }
    }

    public class CarbonaraAura : Weapon
    {
        private const float HealPerHit = 0.35f, MaxHealPerAttack = 1.5f;

        public float Radius => 2.2f * Area(Raw) * (evolved ? 1f : 1f);

        protected override void Fire(WStats s)
        {
            float radius = 2.2f * Area(s);
            int n = G.Enemies.Query(P.Position, radius, hits);
            float dmg = Dmg(s);
            bool canHeal = evolved || IsMain;
            float healBudget = MaxHealPerAttack;
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                G.Enemies.Damage(e, dmg, e.pos - P.Position, s.knockback, Slot);
                if (evolved) e.slow = Mathf.Max(e.slow, 0.6f);
                if (canHeal && healBudget > 0f)
                {
                    // Keep healing on every hit (including props), but cap the total exactly.
                    float healing = Mathf.Min(HealPerHit, healBudget);
                    P.Heal(healing, false);
                    healBudget -= healing;
                }
            }
            G.Fx.CreamWave(P.Position, radius, hits, n, evolved);
            if (n > 0) G.Sfx.Play(SfxId.Splat, 0.18f, Random.Range(1.4f, 1.6f));
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            G.Fx.Aura(P.Position, Radius, evolved);
        }

        public override void Removed() => G.Fx.Aura(Vector3.zero, 0f, false);
    }
}
