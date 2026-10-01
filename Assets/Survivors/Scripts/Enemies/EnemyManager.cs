using System;
using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public class EnemyManager : MonoBehaviour
    {
        public const int MaxAlive = 340;
        public readonly List<Enemy> Active = new List<Enemy>(512);
        public Enemy Boss { get; private set; }
        public int HostileCount { get; private set; }
        public int Kills;
        public float HpMul = 1f, DamageMul = 1f, BossHpMul = 1f;

        private readonly Dictionary<EnemyKind, Stack<Enemy>> pools = new Dictionary<EnemyKind, Stack<Enemy>>();
        private readonly List<Enemy> scratch = new List<Enemy>(256);
        private Transform poolRoot;
        private int nextUid = 1;
        private float buffTick, shoutBudget, flowTimer;

        // Uniform grid rebuilt every frame (counting sort).
        private const float Cell = 2.5f;
        private int gw, gh;
        private Vector2 gmin;
        private int[] cellStart, cellCount, sorted = new int[1024];

        private static readonly string[] Shouts =
        {
            "Mamma mia!", "Ma che fai?!", "Nooo!", "Vergogna!", "Basta!", "Mannaggia!", "Porca miseria!", "Che schifo!", "Aiuto!", "Madonna!"
        };

        public void Init()
        {
            poolRoot = new GameObject("Italians").transform;
            poolRoot.SetParent(transform, false);
        }

        public void ConfigureGrid(Arena arena)
        {
            gmin = arena.min - Vector2.one * 4f;
            gw = Mathf.CeilToInt((arena.max.x - arena.min.x + 8f) / Cell);
            gh = Mathf.CeilToInt((arena.max.y - arena.min.y + 8f) / Cell);
            cellStart = new int[gw * gh + 1];
            cellCount = new int[gw * gh];
        }

        // ---------------- spawning ----------------

        public Enemy Spawn(EnemyKind kind, Vector3 pos, float extraHp = 1f)
        {
            var def = GameData.Enemy(kind);
            if (!def.IsBoss && !def.IsProp && HostileCount >= MaxAlive) return null;
            if (!pools.TryGetValue(kind, out var pool)) pools[kind] = pool = new Stack<Enemy>();
            Enemy e = pool.Count > 0 ? pool.Pop() : Create(def);
            e.uid = nextUid++;
            e.active = true;
            e.fleeing = false;
            e.frozen = false;
            e.pos = new Vector3(pos.x, 0f, pos.z);
            e.scale = def.scale * (def.IsBoss || def.IsProp ? 1f : UnityEngine.Random.Range(0.94f, 1.08f));
            e.radius = def.radius * (e.scale / def.scale);
            float hpMul = def.IsProp ? 1f : (def.IsBoss ? BossHpMul : HpMul) * extraHp;
            e.maxHp = e.hp = def.hp * hpMul;
            e.damage = def.damage * DamageMul;
            e.speedMul = 1f;
            e.knock = Vector3.zero;
            e.stun = e.slow = e.buff = e.flash = e.fear = 0f;
            e.state = 0; e.pattern = -1; e.volleys = 0;
            e.stateTimer = def.IsBoss ? 2.5f : 0f;
            e.attackTimer = UnityEngine.Random.Range(0.5f, def.attackCooldown);
            e.dominoTime = 0f;
            e.lift = 0f; e.spin = 0f; e.fleeT = 0f;
            e.gesture = UnityEngine.Random.value * 10f;
            Array.Clear(e.immune, 0, e.immune.Length);
            e.SetFlash(false);
            var toPlayer = G.Player != null ? G.Player.Position - e.pos : Vector3.back;
            toPlayer.y = 0;
            e.facing = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.back;
            e.nav = e.facing;
            e.los = e.walkClear = true;
            e.rig.root.localScale = Vector3.one * e.scale;
            e.rig.root.gameObject.SetActive(true);
            e.ApplyTransform();
            Active.Add(e);
            if (!def.IsProp) HostileCount++;
            if (def.IsBoss) Boss = e;
            return e;
        }

        private Enemy Create(EnemyDef def)
        {
            var meshes = Models.Enemy(def.kind);
            var e = new Enemy { def = def };
            e.rig = Models.Build(meshes, poolRoot, def.kind.ToString());
            return e;
        }

        private void Despawn(Enemy e)
        {
            e.active = false;
            e.rig.root.gameObject.SetActive(false);
            pools[e.def.kind].Push(e);
        }

        public void Clear()
        {
            foreach (var e in Active) Despawn(e);
            Active.Clear();
            HostileCount = 0;
            Boss = null;
        }

        // ---------------- per-frame ----------------

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            BuildGrid();
            var player = G.Player;
            Vector3 pp = player != null ? player.Position : Vector3.zero;
            float pr = player != null ? player.Radius : 0.4f;
            float now = Time.time;
            shoutBudget = Mathf.Min(3f, shoutBudget + dt * 2.5f);
            flowTimer -= dt;
            if (flowTimer <= 0f && player != null && G.Arena != null)
            {
                G.Arena.UpdateFlow(pp);
                flowTimer = 0.2f;
            }
            int frame = Time.frameCount;
            buffTick -= dt;
            bool doBuff = buffTick <= 0f;
            if (doBuff) buffTick = 0.4f;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var e = Active[i];
                if (e.fleeing)
                {
                    TickFlee(e, dt);
                    if (!e.active) { Active.RemoveAt(i); }
                    continue;
                }
                if (e.frozen)
                {
                    e.Animate(dt, 0f);
                    continue;
                }
                if (e.IsProp) continue;

                e.flash -= dt;
                e.SetFlash(e.flash > 0f);
                e.stun -= dt; e.slow -= dt; e.buff -= dt; e.attackTimer -= dt; e.fear -= dt;

                Vector3 toP = pp - e.pos; toP.y = 0f;
                float dist = toP.magnitude;
                Vector3 dirP = dist > 0.001f ? toP / dist : Vector3.forward;
                Vector3 move = Vector3.zero;
                // Walk straight when the way is clear, otherwise follow the flow field around walls and over bridges.
                if (((frame + e.uid) & 3) == 0)
                {
                    e.los = G.Arena.LineOfSight(e.pos, pp);
                    e.walkClear = dist < 18f && G.Arena.WalkClear(e.pos, pp, e.radius * 0.8f);
                }
                if (dist < 2.2f || e.walkClear) e.nav = dirP;
                else
                {
                    var flow = G.Arena.FlowDir(e.pos);
                    e.nav = flow.sqrMagnitude > 0.01f ? flow : dirP;
                }
                float speed = e.def.speed * e.speedMul * (e.slow > 0f ? 0.55f : 1f) * (e.buff > 0f ? 1.4f : 1f);

                if (e.dominoTime > 0f)
                {
                    TickDomino(e, dt, now);
                }
                else if (e.stun > 0f)
                {
                    move = Vector3.zero;
                }
                else if (e.fear > 0f && !e.IsBoss)
                {
                    // Scared off by chain-store coffee: run the other way.
                    move = -dirP * speed * 1.3f;
                    e.state = 0;
                }
                else
                {
                    switch (e.def.behavior)
                    {
                        case Behavior.Thrower: move = TickThrower(e, dt, dirP, dist, speed); break;
                        case Behavior.Charger: move = TickCharger(e, dt, dirP, dist, speed); break;
                        case Behavior.Sweeper: move = TickSweeper(e, dt, dirP, dist, speed); break;
                        case Behavior.Boss: move = TickBoss(e, dt, dirP, dist, speed); break;
                        case Behavior.Elite:
                            move = e.nav * speed;
                            if (doBuff) BuffAround(e);
                            break;
                        default: move = e.nav * speed; break;
                    }
                }

                // Separation from neighbours.
                Vector3 sep = Separation(e);
                e.knock *= Mathf.Exp(-7f * dt);
                Vector3 delta = (move + e.knock + e.dominoVel * (e.dominoTime > 0f ? 1f : 0f)) * dt + sep;
                e.pos += delta;
                e.pos = G.Arena.Resolve(e.pos, e.radius);
                if (move.sqrMagnitude > 0.01f && e.state != 2) e.facing = Vector3.Slerp(e.facing, move.normalized, 1f - Mathf.Exp(-10f * dt));
                else if (e.state != 2 && dist > 0.1f) e.facing = Vector3.Slerp(e.facing, dirP, 1f - Mathf.Exp(-6f * dt));

                // Contact damage.
                if (player != null && e.dominoTime <= 0f && dist < e.radius + pr + 0.05f) player.TakeDamage(e.damage, e.pos, (e.state == 2 ? "charge:" : "touch:") + e.def.kind);

                // Stragglers get recycled ahead of the player (Vampire Survivors style).
                if (!e.IsBoss && !e.IsElite && dist > 44f && G.Waves != null)
                    e.pos = G.Waves.SpawnPoint(player != null ? player.Velocity : Vector3.zero, e.radius);

                e.lift = Mathf.MoveTowards(e.lift, 0f, dt * 4f);
                e.ApplyTransform();
                e.Animate(dt, move.magnitude);
            }
        }

        private void TickFlee(Enemy e, float dt)
        {
            if (e.IsBoss && e.def.nextPhase.HasValue) { TickMorph(e, dt); return; }
            e.fleeT += dt;
            e.SetFlash(e.fleeT < 0.08f);
            float sp = e.IsBoss ? 3f : 9f;
            e.pos += (e.fleeDir * sp + e.knock) * dt;
            e.knock *= Mathf.Exp(-5f * dt);
            e.facing = e.fleeDir;
            e.lift = Mathf.Abs(Mathf.Sin(e.fleeT * 14f)) * 0.25f;
            float life = e.IsBoss ? 2.2f : 1.0f;
            if (e.fleeT > life - 0.25f)
            {
                float k = Mathf.Clamp01((life - e.fleeT) / 0.25f);
                e.rig.root.localScale = Vector3.one * e.scale * k;
            }
            e.ApplyTransform();
            e.Animate(dt, 6f);
            if (e.fleeT >= life)
            {
                if (e.IsBoss) G.Game.OnBossGone(e);
                Despawn(e);
            }
        }

        private const float MorphTime = 1.4f;

        /// <summary>A beaten boss with another form twirls in a cloud of flour, then that form takes her place.</summary>
        private void TickMorph(Enemy e, float dt)
        {
            e.fleeT += dt;
            e.SetFlash(Mathf.Repeat(e.fleeT, 0.24f) < 0.08f);
            e.facing = Quaternion.Euler(0f, (300f + 900f * e.fleeT) * dt, 0f) * e.facing;
            e.lift = Mathf.Abs(Mathf.Sin(e.fleeT * 8f)) * 0.5f;
            if (UnityEngine.Random.value < dt * 14f)
                G.Fx.Burst(e.Center + UnityEngine.Random.insideUnitSphere * e.radius, new Color(1f, 1f, 1f, 0.7f), 3, 3f, 1.2f, FxKind.Puff);
            e.ApplyTransform();
            e.Animate(dt, 6f);
            if (e.fleeT < MorphTime) return;
            G.Fx.Burst(e.Center, new Color(1f, 1f, 1f, 0.8f), 18, 7f, 1.6f, FxKind.Puff);
            var next = Spawn(e.def.nextPhase.Value, e.pos);
            Despawn(e);
            if (next != null) G.Game.OnBossMorphed(next);
        }

        private void TickDomino(Enemy e, float dt, float now)
        {
            e.dominoTime -= dt;
            e.lift = Mathf.Sin(Mathf.Clamp01(1f - e.dominoTime / 0.45f) * Mathf.PI) * 1.2f;
            e.spin += dt * 900f;
            if (e.dominoTime <= 0f) { e.spin = 0f; e.dominoVel = Vector3.zero; }
            int n = Query(e.pos, e.radius + 0.6f, scratch);
            for (int k = 0; k < n; k++)
            {
                var o = scratch[k];
                if (o == e || o.IsBoss || o.immune[Enemy.SlotDomino] > now) continue;
                o.immune[Enemy.SlotDomino] = now + 0.6f;
                Vector3 d = (o.pos - e.pos); d.y = 0;
                var dir = (e.dominoVel.normalized + d.normalized * 0.6f).normalized;
                Damage(o, e.dominoDamage, dir, 4f, Enemy.SlotDomino);
                if (o.active && !o.fleeing && !o.IsElite && e.dominoVel.magnitude > 8f) Launch(o, dir * e.dominoVel.magnitude * 0.8f, e.dominoDamage * 0.85f);
                G.Fx.Burst(o.Center, Models.PastaGold, 5, 4f, 0.2f, FxKind.Crumb);
            }
        }

        public void Launch(Enemy e, Vector3 velocity, float damage)
        {
            if (e.IsBoss || e.IsProp || e.fleeing) return;
            e.dominoVel = velocity;
            e.dominoTime = 0.45f;
            e.dominoDamage = damage;
        }

        private Vector3 TickThrower(Enemy e, float dt, Vector3 dirP, float dist, float speed)
        {
            if (e.state == 1)
            {
                e.stateTimer -= dt;
                if (e.stateTimer <= 0f)
                {
                    // Slight lead only: throws should be readable and dodgeable by sidestepping.
                    Vector3 lead = G.Player.Velocity * Mathf.Clamp(dist / 9f, 0f, 0.3f);
                    Vector3 aim = (G.Player.Position + lead - e.pos); aim.y = 0;
                    var kind = e.def.shot;
                    float spd = kind == ShotKind.Slipper ? 7.5f : 6.5f;
                    if (G.Shots.HostileCount < 10)
                    {
                        G.Shots.FireHostile(kind, e.pos + Vector3.up * 1.5f * e.scale + e.facing * 0.4f, aim.normalized * spd, e.damage * 0.7f, 0.4f, 3f);
                        G.Sfx.Play(SfxId.Throw, 0.4f, 1.1f);
                    }
                    e.state = 0;
                    e.attackTimer = e.def.attackCooldown * UnityEngine.Random.Range(0.85f, 1.2f);
                }
                return Vector3.zero;
            }
            if (dist < e.def.attackRange && e.attackTimer <= 0f && dist > 2.5f && e.los)
            {
                e.state = 1;
                e.stateTimer = 0.55f;
                return Vector3.zero;
            }
            float keep = e.def.kind == EnemyKind.Pizzaiolo && dist < 6f ? -0.4f : dist < e.def.attackRange ? 0.6f : 1f;
            return e.nav * speed * keep;
        }

        private Vector3 TickCharger(Enemy e, float dt, Vector3 dirP, float dist, float speed)
        {
            switch (e.state)
            {
                case 1:
                    e.stateTimer -= dt;
                    e.facing = e.lockDir;
                    if (e.stateTimer <= 0f)
                    {
                        e.state = 2;
                        e.stateTimer = 1.3f;
                        G.Sfx.Play(SfxId.Vroom, 0.35f, UnityEngine.Random.Range(0.9f, 1.15f));
                    }
                    return Vector3.zero;
                case 2:
                    e.stateTimer -= dt;
                    e.facing = e.lockDir;
                    if (e.stateTimer <= 0f || !G.Arena.Inside(e.pos, e.radius + 0.3f) || G.Arena.Blocked(e.pos + e.lockDir * (e.radius + 0.35f), 0.05f))
                    {
                        e.state = 0;
                        e.attackTimer = e.def.attackCooldown;
                    }
                    return e.lockDir * 12.5f;
                default:
                    if (dist < e.def.attackRange && e.attackTimer <= 0f && e.los && e.walkClear)
                    {
                        e.state = 1;
                        e.stateTimer = 0.75f;
                        e.lockDir = dirP;
                        G.Fx.Telegraph(TeleShape.Lane, e.pos, dirP, 16f, 1.3f * e.scale, 0.75f);
                        return Vector3.zero;
                    }
                    return e.nav * speed;
            }
        }

        private Vector3 TickSweeper(Enemy e, float dt, Vector3 dirP, float dist, float speed)
        {
            switch (e.state)
            {
                case 1:
                    e.stateTimer -= dt;
                    if (e.stateTimer <= 0f)
                    {
                        SweepHit(e, e.def.attackRange + 0.4f, 130f);
                        e.state = 0;
                        e.attackTimer = e.def.attackCooldown;
                    }
                    return Vector3.zero;
                default:
                    if (dist < e.def.attackRange && e.attackTimer <= 0f && e.los)
                    {
                        e.state = 1;
                        e.stateTimer = 0.8f;
                        e.lockDir = dirP;
                        e.facing = dirP;
                        G.Fx.Telegraph(TeleShape.Sector, e.pos, dirP, e.def.attackRange + 0.4f, 130f, 0.8f);
                        return Vector3.zero;
                    }
                    return e.nav * speed;
            }
        }

        private void SweepHit(Enemy e, float radius, float angle)
        {
            var p = G.Player;
            G.Fx.Sweep(e.pos, e.lockDir, radius, angle, new Color(0.9f, 0.75f, 0.45f));
            G.Sfx.Play(SfxId.Whoosh, 0.5f, 0.8f);
            if (p == null) return;
            Vector3 d = p.Position - e.pos; d.y = 0;
            if (d.magnitude <= radius + p.Radius && Vector3.Angle(e.lockDir, d) <= angle * 0.5f + 8f)
                p.TakeDamage(e.damage, e.pos, "sweep:" + e.def.kind);
        }

        private void BuffAround(Enemy e)
        {
            int n = Query(e.pos, 6f, scratch);
            for (int k = 0; k < n; k++) if (scratch[k] != e) scratch[k].buff = 0.6f;
            if (UnityEngine.Random.value < 0.08f && shoutBudget >= 1f)
            {
                shoutBudget -= 1f;
                G.Fx.Shout(e.Center + Vector3.up * 1.4f, "Mangia!", new Color(0.5f, 1f, 0.5f));
            }
            if (e.attackTimer <= 0f)
            {
                e.attackTimer = 1.8f;
                G.Fx.Ring(e.pos + Vector3.up * 0.05f, 6f, new Color(0.4f, 1f, 0.4f, 0.3f), 0.9f, 0.25f);
            }
        }

        // ---------------- bosses ----------------

        private enum BossMove { Fan, Radial, Charge, Summon, Sweep, Ride, Stampede }
        private static readonly string[] RideShouts = { "Drin drin!", "Permesso!", "Largo, largo!" };

        private static BossMove[] Moves(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.BossNonna: return new[] { BossMove.Fan, BossMove.Summon, BossMove.Charge, BossMove.Fan, BossMove.Charge };
                case EnemyKind.BossCapitano: return new[] { BossMove.Sweep, BossMove.Charge, BossMove.Summon, BossMove.Sweep, BossMove.Radial };
                case EnemyKind.BossBikeNonna: return new[] { BossMove.Ride, BossMove.Fan, BossMove.Ride, BossMove.Stampede, BossMove.Ride, BossMove.Radial };
                default: return new[] { BossMove.Radial, BossMove.Summon, BossMove.Fan, BossMove.Charge, BossMove.Radial, BossMove.Fan };
            }
        }

        private Vector3 TickBoss(Enemy e, float dt, Vector3 dirP, float dist, float speed)
        {
            var moves = Moves(e.def.kind);
            bool enraged = e.hp < e.maxHp * 0.5f;
            float pace = enraged ? 0.7f : 1f;
            e.stateTimer -= dt;
            BossMove move = e.pattern >= 0 ? moves[e.pattern % moves.Length] : BossMove.Fan;
            switch (e.state)
            {
                case 0:
                    if (e.stateTimer <= 0f)
                    {
                        e.pattern++;
                        move = moves[e.pattern % moves.Length];
                        e.state = 1;
                        e.lockDir = dirP;
                        e.volleys = 0;
                        float wind = move == BossMove.Summon || move == BossMove.Stampede ? 0.6f : 0.9f * pace;
                        e.stateTimer = wind;
                        switch (move)
                        {
                            case BossMove.Charge: G.Fx.Telegraph(TeleShape.Lane, e.pos, dirP, 22f, e.radius * 2f, wind); break;
                            case BossMove.Ride:
                                // A run of dashes, each one rung in with the bell and re-aimed at the player.
                                e.volleys = enraged ? 3 : 2;
                                RideTelegraph(e, dirP, wind);
                                G.Fx.Shout(e.Center + Vector3.up * 3f, RideShouts[UnityEngine.Random.Range(0, RideShouts.Length)], new Color(1f, 0.8f, 0.3f));
                                break;
                            case BossMove.Sweep: G.Fx.Telegraph(TeleShape.Sector, e.pos, dirP, e.def.attackRange, 160f, wind); break;
                            case BossMove.Radial: G.Fx.Telegraph(TeleShape.Circle, e.pos, dirP, 3.5f, 0f, wind); break;
                        }
                    }
                    return e.nav * speed * (dist > 4f ? 1f : 0.3f);
                case 1:
                    e.facing = e.lockDir;
                    if (e.stateTimer <= 0f)
                    {
                        e.state = 2;
                        switch (move)
                        {
                            case BossMove.Fan: e.stateTimer = 0f; e.volleys = enraged ? 4 : 3; break;
                            case BossMove.Radial: e.stateTimer = 0f; e.volleys = enraged ? 3 : 2; break;
                            case BossMove.Charge: e.stateTimer = 1.3f; G.Sfx.Play(SfxId.Boss, 0.5f, 1.4f); G.Cam.Shake(0.3f); break;
                            case BossMove.Ride: e.stateTimer = 0.95f; G.Sfx.Play(SfxId.Vroom, 0.5f, 1.3f); break;
                            case BossMove.Stampede:
                                G.Waves.Stampede(EnemyKind.Vespista, enraged ? 10 : 7);
                                G.Fx.Shout(e.Center + Vector3.up * 3f, "Ragazzi, in sella!", new Color(1f, 0.8f, 0.3f));
                                e.stateTimer = 0.8f;
                                break;
                            case BossMove.Sweep:
                                SweepHit(e, e.def.attackRange, 160f);
                                G.Cam.Shake(0.5f);
                                e.stateTimer = 0.5f;
                                break;
                            case BossMove.Summon:
                                var minion = e.def.kind == EnemyKind.BossNonna ? EnemyKind.Tifoso : e.def.kind == EnemyKind.BossCapitano ? EnemyKind.Gondoliere : EnemyKind.Mafioso;
                                int count = e.def.kind == EnemyKind.BossNonna ? 12 : 6;
                                G.Waves.SpawnRingAround(minion, count, 9f, e.pos);
                                G.Fx.Shout(e.Center + Vector3.up * 3f, e.def.kind == EnemyKind.BossNonna ? "Nipoti! A tavola!" : "Ragazzi!", new Color(1f, 0.8f, 0.3f));
                                e.stateTimer = 0.6f;
                                break;
                        }
                    }
                    return Vector3.zero;
                default:
                    if (move == BossMove.Charge)
                    {
                        e.facing = e.lockDir;
                        if (e.stateTimer <= 0f || !G.Arena.Inside(e.pos, e.radius + 0.5f) || G.Arena.Blocked(e.pos + e.lockDir * (e.radius + 0.4f), 0.05f)) { EndBossMove(e, pace); return Vector3.zero; }
                        return e.lockDir * (enraged ? 16f : 13f);
                    }
                    if (move == BossMove.Ride)
                    {
                        e.facing = e.lockDir;
                        if (e.stateTimer <= 0f || !G.Arena.Inside(e.pos, e.radius + 0.5f) || G.Arena.Blocked(e.pos + e.lockDir * (e.radius + 0.4f), 0.05f))
                        {
                            if (e.volleys <= 0) { EndBossMove(e, pace); return Vector3.zero; }
                            // Brake, ring again and line up on the player for the next dash.
                            e.volleys--;
                            e.state = 1;
                            e.lockDir = dirP;
                            e.stateTimer = 0.6f * pace;
                            RideTelegraph(e, dirP, e.stateTimer);
                            return Vector3.zero;
                        }
                        return e.lockDir * (enraged ? 18f : 15f);
                    }
                    if (move == BossMove.Fan || move == BossMove.Radial)
                    {
                        if (e.stateTimer <= 0f)
                        {
                            if (e.volleys <= 0) { EndBossMove(e, pace); return Vector3.zero; }
                            e.volleys--;
                            e.stateTimer = move == BossMove.Fan ? 0.42f : 0.7f;
                            var shot = e.def.shot == ShotKind.Oar ? ShotKind.Meatball : e.def.shot;
                            Vector3 origin = e.pos + Vector3.up * 1.6f;
                            if (move == BossMove.Fan)
                            {
                                Vector3 aim = (G.Player.Position - e.pos); aim.y = 0; aim.Normalize();
                                int n = 5 + (enraged ? 2 : 0);
                                for (int k = 0; k < n; k++)
                                {
                                    float a = (k - (n - 1) * 0.5f) * 13f;
                                    G.Shots.FireHostile(shot, origin, Quaternion.Euler(0, a, 0) * aim * 9f, e.damage * 0.7f, 0.55f, 3.5f);
                                }
                            }
                            else
                            {
                                int n = enraged ? 22 : 16;
                                float offset = e.volleys * 7f;
                                for (int k = 0; k < n; k++)
                                {
                                    float a = k * 360f / n + offset;
                                    G.Shots.FireHostile(shot, origin, Quaternion.Euler(0, a, 0) * Vector3.forward * 7f, e.damage * 0.7f, 0.6f, 4f);
                                }
                            }
                            G.Sfx.Play(SfxId.Throw, 0.6f, 0.7f);
                        }
                        return Vector3.zero;
                    }
                    if (e.stateTimer <= 0f) EndBossMove(e, pace);
                    return Vector3.zero;
            }
        }

        private static void RideTelegraph(Enemy e, Vector3 dir, float wind)
        {
            G.Fx.Telegraph(TeleShape.Lane, e.pos, dir, 24f, e.radius * 2f, wind);
            G.Sfx.Play(SfxId.Bell, 0.7f, UnityEngine.Random.Range(0.95f, 1.05f));
        }

        private static void EndBossMove(Enemy e, float pace)
        {
            e.state = 0;
            e.stateTimer = UnityEngine.Random.Range(1.6f, 2.6f) * pace;
        }

        // ---------------- damage ----------------

        public void Damage(Enemy e, float amount, Vector3 dir, float knockback, int slot)
        {
            if (!e.active || e.fleeing) return;
            if (e.IsProp)
            {
                Defeat(e, dir);
                return;
            }
            e.hp -= amount;
            e.flash = 0.09f;
            if (G.Game != null) G.Game.RecordDamage(slot, amount);
            if (knockback > 0f && !e.IsBoss)
            {
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) e.knock += dir.normalized * knockback * (1f - e.def.knockResist);
            }
            G.Fx.Number(e.Center + Vector3.up * 0.9f * e.scale, amount, e.IsBoss);
            if (e.hp <= 0f) Defeat(e, dir);
        }

        private void Defeat(Enemy e, Vector3 dir)
        {
            if (e.IsProp)
            {
                G.Fx.Burst(e.pos + Vector3.up * 0.5f, new Color(0.55f, 0.32f, 0.16f), 14, 6f, 0.25f, FxKind.Crumb);
                G.Fx.Burst(e.pos + Vector3.up * 0.5f, new Color(0.55f, 0.05f, 0.12f, 0.8f), 8, 3f, 0.8f, FxKind.Puff);
                G.Sfx.Play(SfxId.Crate, 0.6f);
                G.Pickups.DropFromBarrel(e.pos);
                Despawn(e);
                Active.Remove(e);
                return;
            }
            e.fleeing = true;
            e.fleeT = 0f;
            e.state = 0;
            e.dominoTime = 0f;
            e.spin = 0f;
            HostileCount--;
            Kills++;
            var away = e.pos - (G.Player != null ? G.Player.Position : Vector3.zero);
            away.y = 0;
            e.fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : UnityEngine.Random.onUnitSphere.normalized;
            e.fleeDir.y = 0; e.fleeDir.Normalize();
            e.knock = dir.sqrMagnitude > 0.001f ? dir.normalized * 3f : Vector3.zero;
            G.Pickups.DropGem(e.pos, e.def.xp);
            if (e.IsElite)
            {
                G.Pickups.Drop(PickupKind.Chest, e.pos + e.fleeDir * -0.5f);
                G.Fx.Shout(e.Center + Vector3.up * 1.5f, "Che maleducato!", new Color(1f, 0.6f, 0.6f));
            }
            else if (UnityEngine.Random.value < 0.012f) G.Pickups.Drop(PickupKind.Coin, e.pos);
            else if (UnityEngine.Random.value < 0.006f) G.Pickups.Drop(PickupKind.Pizza, e.pos);
            if (e.IsBoss && e.def.nextPhase.HasValue)
            {
                // Not over yet: she stays put and changes form (TickMorph).
                e.knock = Vector3.zero;
                Boss = null;
                G.Game.OnBossMorphing(e);
            }
            else if (e.IsBoss)
            {
                G.Pickups.Drop(PickupKind.Chest, e.pos);
                G.Pickups.Drop(PickupKind.CoinBag, e.pos + Vector3.right * 1.5f);
                G.Fx.Shout(e.Center + Vector3.up * 3f, "MAMMA MIAAAA!", new Color(1f, 0.85f, 0.3f));
                G.Game.OnBossDefeated(e);
                Boss = null;
            }
            else if (shoutBudget >= 1f && UnityEngine.Random.value < 0.35f)
            {
                shoutBudget -= 1f;
                G.Fx.Shout(e.Center + Vector3.up * 1f, Shouts[UnityEngine.Random.Range(0, Shouts.Length)], new Color(1f, 0.92f, 0.85f));
            }
            G.Fx.Burst(e.Center, new Color(1f, 1f, 1f, 0.6f), 3, 2f, 0.6f, FxKind.Puff);
            G.Sfx.Play(SfxId.Poof, 0.25f, UnityEngine.Random.Range(0.85f, 1.25f));
        }

        /// <summary>Italians right next to the player flinch back when they land a hit, so you're never pinned.</summary>
        public void Shove(Vector3 center, float radius, float force)
        {
            int n = Query(center, radius, scratch);
            for (int i = 0; i < n; i++)
            {
                var e = scratch[i];
                if (e.IsBoss || e.IsProp) continue;
                var d = e.pos - center; d.y = 0f;
                e.knock += (d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward) * force * (1f - e.def.knockResist * 0.5f);
            }
        }

        /// <summary>Nonna's rosary: every non-boss Italian on screen gives up.</summary>
        public void RoutAll(Vector3 center, float radius)
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var e = Active[i];
                if (!e.active || e.fleeing || e.IsBoss || e.IsProp) continue;
                if ((e.pos - center).sqrMagnitude > radius * radius) continue;
                e.hp = 0f;
                Defeat(e, e.pos - center);
            }
        }

        // ---------------- spatial queries ----------------

        private int CellOf(Vector3 p)
        {
            int x = Mathf.Clamp((int)((p.x - gmin.x) / Cell), 0, gw - 1);
            int y = Mathf.Clamp((int)((p.z - gmin.y) / Cell), 0, gh - 1);
            return y * gw + x;
        }

        private void BuildGrid()
        {
            if (cellCount == null) return;
            Array.Clear(cellCount, 0, cellCount.Length);
            if (sorted.Length < Active.Count) sorted = new int[Active.Count * 2];
            for (int i = 0; i < Active.Count; i++)
            {
                var e = Active[i];
                if (e.fleeing) { e.cell = -1; continue; }
                e.cell = CellOf(e.pos);
                cellCount[e.cell]++;
            }
            int run = 0;
            for (int c = 0; c < cellCount.Length; c++) { cellStart[c] = run; run += cellCount[c]; }
            cellStart[cellCount.Length] = run;
            Array.Clear(cellCount, 0, cellCount.Length);
            for (int i = 0; i < Active.Count; i++)
            {
                int c = Active[i].cell;
                if (c < 0) continue;
                sorted[cellStart[c] + cellCount[c]++] = i;
            }
        }

        private Vector3 Separation(Enemy e)
        {
            if (cellCount == null || e.cell < 0 || e.IsBoss) return Vector3.zero;
            int cx = e.cell % gw, cy = e.cell / gw;
            Vector3 push = Vector3.zero;
            int checkedCount = 0;
            for (int y = Mathf.Max(0, cy - 1); y <= Mathf.Min(gh - 1, cy + 1); y++)
                for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(gw - 1, cx + 1); x++)
                {
                    int c = y * gw + x;
                    for (int k = cellStart[c]; k < cellStart[c] + cellCount[c]; k++)
                    {
                        int idx = sorted[k];
                        if (idx >= Active.Count) continue;
                        var o = Active[idx];
                        if (o == e || o.fleeing) continue;
                        float dx = e.pos.x - o.pos.x, dz = e.pos.z - o.pos.z;
                        float rr = e.radius + o.radius;
                        float d2 = dx * dx + dz * dz;
                        if (d2 >= rr * rr) continue;
                        float d = Mathf.Sqrt(d2);
                        if (d < 0.001f) { dx = (e.uid & 1) == 0 ? 0.01f : -0.01f; dz = 0.01f; d = 0.014f; }
                        float overlap = (rr - d) * (o.IsBoss || o.IsProp ? 1f : 0.5f);
                        push.x += dx / d * overlap;
                        push.z += dz / d * overlap;
                        if (++checkedCount > 12) return push * 0.8f;
                    }
                }
            return push * 0.8f;
        }

        /// <summary>Hostile, non-fleeing enemies (and breakable props) whose body overlaps the circle.</summary>
        public int Query(Vector3 c, float r, List<Enemy> results)
        {
            results.Clear();
            if (cellCount == null) return 0;
            float reach = r + 1.6f;
            int x0 = Mathf.Clamp((int)((c.x - reach - gmin.x) / Cell), 0, gw - 1);
            int x1 = Mathf.Clamp((int)((c.x + reach - gmin.x) / Cell), 0, gw - 1);
            int y0 = Mathf.Clamp((int)((c.z - reach - gmin.y) / Cell), 0, gh - 1);
            int y1 = Mathf.Clamp((int)((c.z + reach - gmin.y) / Cell), 0, gh - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int cell = y * gw + x;
                    for (int k = cellStart[cell]; k < cellStart[cell] + cellCount[cell]; k++)
                    {
                        int idx = sorted[k];
                        if (idx >= Active.Count) continue;
                        var e = Active[idx];
                        if (!e.active || e.fleeing) continue;
                        float dx = e.pos.x - c.x, dz = e.pos.z - c.z, rr = r + e.radius;
                        if (dx * dx + dz * dz <= rr * rr) results.Add(e);
                    }
                }
            // Bosses are large; check them explicitly so cell reach never misses them.
            if (Boss != null && Boss.active && !Boss.fleeing && !results.Contains(Boss))
            {
                float dx = Boss.pos.x - c.x, dz = Boss.pos.z - c.z, rr = r + Boss.radius;
                if (dx * dx + dz * dz <= rr * rr) results.Add(Boss);
            }
            return results.Count;
        }

        /// <summary>Nearest Italian; with needLos, only ones not hidden behind cover (cached per enemy).</summary>
        public Enemy Nearest(Vector3 p, float maxDist, int skipUid = -1, bool needLos = false)
        {
            Enemy best = null;
            float bestD = maxDist * maxDist;
            foreach (var e in Active)
            {
                if (!e.Targetable || e.uid == skipUid || (needLos && !e.los)) continue;
                float d = (e.pos - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = e; }
            }
            return best;
        }

        /// <summary>Up to <paramref name="count"/> distinct nearest targets (for multi-shot weapons).</summary>
        public int NearestSeveral(Vector3 p, float maxDist, int count, List<Enemy> results, bool needLos = false)
        {
            results.Clear();
            float max2 = maxDist * maxDist;
            foreach (var e in Active)
            {
                if (!e.Targetable || (needLos && !e.los)) continue;
                float d = (e.pos - p).sqrMagnitude;
                if (d > max2) continue;
                if (results.Count < count) { results.Add(e); continue; }
                int worst = 0; float worstD = -1f;
                for (int i = 0; i < results.Count; i++)
                {
                    float rd = (results[i].pos - p).sqrMagnitude;
                    if (rd > worstD) { worstD = rd; worst = i; }
                }
                if (d < worstD) results[worst] = e;
            }
            return results.Count;
        }

        public Enemy RandomNear(Vector3 p, float maxDist)
        {
            int n = 0;
            Enemy pick = null;
            float max2 = maxDist * maxDist;
            foreach (var e in Active)
            {
                if (!e.Targetable || (e.pos - p).sqrMagnitude > max2) continue;
                n++;
                if (UnityEngine.Random.Range(0, n) == 0) pick = e;
            }
            return pick;
        }

        private readonly List<Enemy> densityScratch = new List<Enemy>(64);

        /// <summary>Samples Italians in range and returns the one standing in the thickest crowd.</summary>
        public Enemy DensestNear(Vector3 p, float range, float radius, int samples, List<Vector3> avoid)
        {
            Enemy best = null;
            int bestN = 0;
            for (int s = 0; s < samples; s++)
            {
                var e = RandomNear(p, range);
                if (e == null) break;
                bool near = false;
                if (avoid != null) foreach (var a in avoid) if ((a - e.pos).sqrMagnitude < radius * radius * 2.2f) { near = true; break; }
                if (near) continue;
                int n = Query(e.pos, radius, densityScratch);
                if (n > bestN) { bestN = n; best = e; }
            }
            return best;
        }

        /// <summary>Rate-limited exclamation above an Italian.</summary>
        public void ShoutCustom(Enemy e, string text, Color color)
        {
            if (shoutBudget < 1f) return;
            shoutBudget -= 1f;
            G.Fx.Shout(e.Center + Vector3.up, text, color);
        }

        public void ShoutFrom(Enemy e)
        {
            if (shoutBudget < 1f) return;
            shoutBudget -= 1f;
            G.Fx.Shout(e.Center + Vector3.up, Shouts[UnityEngine.Random.Range(0, Shouts.Length)], Color.white);
        }
    }
}
