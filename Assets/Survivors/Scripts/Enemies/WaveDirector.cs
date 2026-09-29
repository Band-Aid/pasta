using UnityEngine;

namespace PastaSurvivors
{
    public class WaveDirector : MonoBehaviour
    {
        private StageDef stage;
        private int nextEvent;
        private float spawnTimer;
        public bool BossSpawned { get; private set; }
        public float TimeScale = 1f;   // stage duration multiplier (tests use shorter runs)

        public void Begin(StageDef def)
        {
            stage = def;
            nextEvent = 0;
            spawnTimer = 0.5f;
            BossSpawned = false;
        }

        public WavePhase CurrentPhase(float t)
        {
            var phases = stage.phases;
            var cur = phases[0];
            foreach (var p in phases) if (t >= p.start * TimeScale) cur = p;
            return cur;
        }

        public void Tick(float dt, float t)
        {
            if (stage == null) return;
            // Difficulty climbs every minute on top of the stage multiplier.
            float minutes = t / 60f / Mathf.Max(0.01f, TimeScale);
            // Later stages open gently (every run starts at level 1) and reach their full multiplier by minute 4.
            float settle = Mathf.Clamp01(minutes / 4f);
            G.Enemies.HpMul = Mathf.Lerp(1f, stage.hpMul, settle) * (1f + minutes * 0.075f);
            G.Enemies.DamageMul = Mathf.Lerp(1f, stage.damageMul, settle) * (1f + minutes * 0.04f);

            while (nextEvent < stage.events.Length && t >= stage.events[nextEvent].time * TimeScale)
            {
                Run(stage.events[nextEvent]);
                nextEvent++;
            }

            var phase = CurrentPhase(t);
            int alive = G.Enemies.HostileCount;
            spawnTimer -= dt;
            if (alive < phase.minAlive)
            {
                // Fill quickly up to the minimum, a few per frame.
                int deficit = Mathf.Min(phase.minAlive - alive, 6);
                for (int i = 0; i < deficit; i++) SpawnOne(phase);
            }
            // Periodic trickle on top of the minimum, capped so a slow start can't snowball.
            if (spawnTimer <= 0f)
            {
                spawnTimer = phase.interval;
                if (alive < phase.minAlive * 1.6f + 10)
                    for (int i = 0; i < phase.batch; i++) SpawnOne(phase);
            }
        }

        private void SpawnOne(WavePhase phase)
        {
            float total = 0f;
            foreach (var m in phase.mix) total += m.weight;
            float r = Random.value * total;
            var kind = phase.mix[0].kind;
            foreach (var m in phase.mix)
            {
                r -= m.weight;
                if (r <= 0f) { kind = m.kind; break; }
            }
            var def = GameData.Enemy(kind);
            G.Enemies.Spawn(kind, SpawnPoint(G.Player.Velocity, def.radius));
        }

        /// <summary>A point just outside the camera view, biased toward where the player is heading.</summary>
        public Vector3 SpawnPoint(Vector3 bias, float radius)
        {
            var pp = G.Player != null ? G.Player.Position : Vector3.zero;
            for (int tries = 0; tries < 24; tries++)
            {
                float angle = Random.value * 360f;
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                if (bias.sqrMagnitude > 1f && Random.value < 0.4f) dir = (bias.normalized + dir * 0.6f).normalized;
                // The view is wider than tall: push spawns further out along X.
                float dist = Random.Range(21f, 25f) * (1f + Mathf.Abs(dir.x) * 0.3f);
                var p = pp + dir * dist;
                if (!G.Arena.Inside(p, radius + 0.5f)) continue;
                if (G.Arena.Blocked(p, radius)) continue;
                if (!G.Arena.Reachable(p)) continue;
                return p;
            }
            // Fallback: clamp into the arena along a random direction.
            var fallback = pp + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * 20f;
            return G.Arena.Resolve(fallback, radius + 0.5f);
        }

        public void SpawnRingAround(EnemyKind kind, int count, float radius, Vector3 center)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                if (!G.Arena.Inside(p, 0.6f)) p = G.Arena.Clamp(p, 0.8f);
                if (G.Arena.Blocked(p, 0.5f) || !G.Arena.Reachable(p)) continue;
                G.Enemies.Spawn(kind, p);
            }
        }

        private void Run(StageEvent ev)
        {
            var pp = G.Player.Position;
            switch (ev.type)
            {
                case StageEventType.Ring:
                    SpawnRingAround(ev.kind, ev.count, 17f, pp);
                    break;
                case StageEventType.Swarm:
                    {
                        var dir = Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward;
                        var side = Vector3.Cross(Vector3.up, dir);
                        var center = pp + dir * 24f;
                        for (int i = 0; i < ev.count; i++)
                        {
                            var p = G.Arena.Clamp(center + side * Random.Range(-9f, 9f) + dir * Random.Range(-3f, 5f), 1f);
                            if (G.Arena.Blocked(p, 0.5f) || !G.Arena.Reachable(p)) p = SpawnPoint(Vector3.zero, 0.5f);
                            G.Enemies.Spawn(ev.kind, p);
                        }
                        break;
                    }
                case StageEventType.Stampede:
                    {
                        // A column of Vespas crossing the screen: they spawn in a line and start charging immediately.
                        // A wall of Vespas with one escape gap near the player: read the lanes, step into the gap.
                        var dir = Quaternion.Euler(0, Random.Range(0, 4) * 90f + 45f, 0) * Vector3.forward;
                        var side = Vector3.Cross(Vector3.up, dir);
                        var start = pp - dir * 20f;
                        float gapCenter = Random.Range(-3.5f, 3.5f);
                        const float spacing = 2.4f, gapHalf = 3f;
                        int spawned = 0;
                        for (int k = -ev.count; k <= ev.count && spawned < ev.count; k++)
                        {
                            float offset = k * spacing;
                            if (Mathf.Abs(offset - gapCenter) < gapHalf) continue;
                            if (Mathf.Abs(offset) > ev.count * spacing * 0.6f + gapHalf) continue;
                            int i = spawned++;
                            var p = G.Arena.Clamp(start + side * offset - dir * Random.Range(0f, 2f), 1f);
                            var e = G.Enemies.Spawn(ev.kind, p);
                            if (e == null) continue;
                            e.lockDir = dir;
                            e.facing = dir;
                            e.state = 1;
                            e.stateTimer = 1.3f + i * 0.04f;
                            G.Fx.Telegraph(TeleShape.Lane, p, dir, 30f, 1.2f, e.stateTimer);
                        }
                        G.Sfx.Play(SfxId.Vroom, 0.7f, 0.8f);
                        break;
                    }
                case StageEventType.Elite:
                    for (int i = 0; i < ev.count; i++) G.Enemies.Spawn(ev.kind, SpawnPoint(Vector3.zero, 0.8f));
                    break;
                case StageEventType.Boss:
                    {
                        BossSpawned = true;
                        var p = SpawnPoint(Vector3.zero, 2f);
                        G.Enemies.Spawn(ev.kind, p);
                        G.Sfx.Play(SfxId.Boss, 0.9f, 1f);
                        G.Cam.Shake(0.6f);
                        G.Game.OnBossSpawned();
                        break;
                    }
            }
            if (!string.IsNullOrEmpty(ev.banner))
                G.Fx.Banner(ev.banner, ev.type == StageEventType.Boss ? new Color(1f, 0.4f, 0.3f) : ev.type == StageEventType.Elite ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.85f, 0.4f));
        }
    }
}
