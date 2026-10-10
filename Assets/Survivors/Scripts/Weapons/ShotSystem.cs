using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    public enum Motion { Straight, Boomerang, Bounce, Orbit, Lob, Zone, Seek, Turret }

    public class Shot
    {
        public bool active, hostile;
        public ShotKind kind;
        public string visualKey;
        public GameObject go;
        public Transform tr;
        public MeshRenderer mr;
        public Vector3 pos, vel, dir, origin, target;
        public float radius, damage, knock, life, maxLife, speed, spinRate, spin, scale = 1f;
        public int pierce, slot, hitCount;
        public float hitCooldown;       // >0: re-hit the same Italian after this long; 0: once per shot
        public readonly int[] hitUids = new int[24];
        public Motion motion;
        public float angle, orbitRadius, orbitSpeed;
        public float heightAboveGround;
        public float tickTimer, tickInterval, slow;
        public float explodeRadius, explodeDamage, explodeKnock = 3f;
        public bool sticky;
        /// <summary>Zone of mayonnaise: Italians who step in it slip and fall.</summary>
        public bool slippery;
        public int bounces;
        public Action<Shot> onEnd;
    }

    public class ShotSystem : MonoBehaviour
    {
        public readonly List<Shot> Active = new List<Shot>(256);
        private readonly Dictionary<string, Stack<Shot>> pools = new Dictionary<string, Stack<Shot>>();
        private readonly List<Enemy> hits = new List<Enemy>(64);
        private Transform root;
        private MaterialPropertyBlock mpb;
        private static Mesh splatMesh;

        public void Init()
        {
            root = new GameObject("Shots").transform;
            root.SetParent(transform, false);
            mpb = new MaterialPropertyBlock();
        }

        private Shot Get(string key, ShotKind kind, bool zone)
        {
            if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = new Stack<Shot>();
            Shot s;
            if (pool.Count > 0) s = pool.Pop();
            else
            {
                s = new Shot { visualKey = key };
                var mesh = zone ? (splatMesh ??= MakeSplat()) : Models.Shot(kind);
                s.go = Models.Static(mesh, root, key, zone ? Mats.FxAlpha : Mats.Lit, !zone);
                s.tr = s.go.transform;
                s.mr = s.go.GetComponent<MeshRenderer>();
            }
            s.kind = kind;
            s.active = true;
            s.hostile = false;
            s.hitCount = 0;
            s.bounces = 0;
            s.onEnd = null;
            s.explodeRadius = 0f;
            s.explodeKnock = 3f;
            s.slow = 0f;
            s.sticky = false;
            s.slippery = false;
            s.spin = UnityEngine.Random.value * 360f;
            s.scale = 1f;
            s.go.SetActive(true);
            Active.Add(s);
            return s;
        }

        public Shot Fire(ShotKind kind, Motion motion, Vector3 pos, Vector3 vel, float damage, float radius, float life, int slot)
        {
            var s = Get(kind.ToString(), kind, false);
            s.motion = motion;
            s.pos = s.origin = pos;
            s.heightAboveGround = pos.y - G.Arena.GroundHeight(pos);
            s.vel = vel;
            s.speed = vel.magnitude;
            s.dir = s.speed > 0.001f ? vel / s.speed : Vector3.forward;
            s.damage = damage;
            s.radius = radius;
            s.life = s.maxLife = life;
            s.slot = slot;
            s.pierce = 1;
            s.knock = 1f;
            s.hitCooldown = 0f;
            s.spinRate = 0f;
            Place(s);
            return s;
        }

        public int HostileCount
        {
            get
            {
                int n = 0;
                foreach (var s in Active) if (s.hostile) n++;
                return n;
            }
        }

        public Shot FireHostile(ShotKind kind, Vector3 pos, Vector3 vel, float damage, float radius, float life)
        {
            var s = Fire(kind, Motion.Straight, pos, vel, damage, radius, life, -1);
            s.hostile = true;
            s.spinRate = kind == ShotKind.Meatball ? 0f : 720f;
            s.scale = kind == ShotKind.Meatball ? 1.4f : 1.3f;
            return s;
        }

        /// <summary>A placed ketchup sprinkler: sprays droplets in a rotating pattern.</summary>
        public Shot Turret(Vector3 pos, float duration, float damage, int slot)
        {
            var s = Get("turret", ShotKind.Ketchup, false);
            if (s.mr.GetComponent<MeshFilter>().sharedMesh != Models.Get("sprinkler"))
                s.mr.GetComponent<MeshFilter>().sharedMesh = Models.Get("sprinkler");
            s.motion = Motion.Turret;
            s.pos = G.Arena.Resolve(new Vector3(pos.x, 0f, pos.z), 0.5f);
            s.vel = Vector3.zero;
            s.damage = damage;
            s.radius = 0f;
            s.life = s.maxLife = duration;
            s.tickInterval = 0.07f;
            s.tickTimer = 0f;
            s.slot = slot;
            s.angle = 0f;
            s.scale = 1.3f;
            s.spinRate = 0f;
            s.tr.SetPositionAndRotation(s.pos, Quaternion.identity);
            s.tr.localScale = Vector3.one * s.scale;
            return s;
        }

        public Shot Zone(Vector3 pos, float radius, float damage, float duration, float tick, int slot, Color color, float slow)
        {
            var s = Get("zone", ShotKind.Ketchup, true);
            s.motion = Motion.Zone;
            s.pos = G.Arena.OnGround(pos) + Vector3.up * 0.06f;
            s.vel = Vector3.zero;
            s.radius = radius;
            s.damage = damage;
            s.life = s.maxLife = duration;
            s.tickInterval = tick;
            s.tickTimer = 0f;
            s.slot = slot;
            s.slow = slow;
            s.knock = 0f;
            s.hitCooldown = tick;
            mpb.SetColor("_BaseColor", color);
            s.mr.SetPropertyBlock(mpb);
            s.tr.localScale = Vector3.one * radius;
            s.tr.SetPositionAndRotation(s.pos, Quaternion.Euler(0, UnityEngine.Random.value * 360f, 0));
            return s;
        }

        public void Clear()
        {
            foreach (var s in Active) Recycle(s);
            Active.Clear();
        }

        private void Recycle(Shot s)
        {
            s.active = false;
            s.go.SetActive(false);
            pools[s.visualKey].Push(s);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            var player = G.Player;
            Vector3 pp = player != null ? player.Position : Vector3.zero;
            float now = Time.time;
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var s = Active[i];
                s.life -= dt;
                bool end = s.life <= 0f;
                switch (s.motion)
                {
                    case Motion.Straight:
                        s.pos += s.vel * dt;
                        if (G.Arena.ShotBlocked(s.pos)) { end = true; G.Fx.Burst(s.pos, s.hostile ? new Color(0.9f, 0.85f, 0.8f) : Models.PastaGold, 4, 3f, 0.15f, FxKind.Crumb); }
                        break;
                    case Motion.Turret:
                        {
                            s.tickTimer -= dt;
                            s.angle += dt * 4.2f;
                            s.tr.rotation = Quaternion.Euler(0f, s.angle * Mathf.Rad2Deg, 0f);
                            while (s.tickTimer <= 0f)
                            {
                                s.tickTimer += s.tickInterval;
                                for (int k = 0; k < 2; k++)
                                {
                                    float a = s.angle + k * Mathf.PI;
                                    var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                                    var drop = Fire(ShotKind.Drop, Motion.Straight, s.pos + Vector3.up * 1.4f + d * 0.4f, d * 13f, s.damage, 0.35f, 0.75f, s.slot);
                                    drop.knock = 1.2f;
                                }
                            }
                            break;
                        }
                    case Motion.Seek:
                        {
                            var t = G.Enemies.Nearest(s.pos, 12f);
                            if (t != null)
                            {
                                var want = (t.pos - s.pos); want.y = 0;
                                s.vel = Vector3.MoveTowards(s.vel, want.normalized * s.speed, dt * 8f);
                            }
                            s.pos += s.vel * dt;
                            break;
                        }
                    case Motion.Boomerang:
                        {
                            float t = s.maxLife - s.life;
                            float outTime = s.orbitSpeed;
                            if (t < outTime)
                            {
                                s.pos += s.dir * s.speed * (1f - t / outTime) * dt;
                                if (G.Arena.ShotBlocked(s.pos)) s.life = s.maxLife - outTime - 0.001f; // hit cover: come back early
                            }
                            else
                            {
                                var back = pp + Vector3.up * 0.8f - s.pos;
                                back.y = 0f;
                                float k = Mathf.Clamp01((t - outTime) / 0.6f);
                                s.pos += back.normalized * s.speed * (0.2f + k * 1.1f) * dt;
                                if (back.magnitude < 0.8f) end = true;
                            }
                            break;
                        }
                    case Motion.Bounce:
                        {
                            var before = s.pos;
                            s.pos += s.vel * dt;
                            if (G.Arena.ShotBlocked(s.pos))
                            {
                                // Ricochet off cover: flip the axis that caused the hit.
                                if (G.Arena.ShotBlocked(new Vector3(s.pos.x, s.pos.y, before.z))) s.vel.x = -s.vel.x;
                                else s.vel.z = -s.vel.z;
                                s.pos = before;
                                OnBounce(s);
                            }
                            // Bounce inside the visible play window around the player.
                            float hx = 15f, hz = 10f;
                            if (s.pos.x > pp.x + hx && s.vel.x > 0 || s.pos.x < pp.x - hx && s.vel.x < 0) { s.vel.x = -s.vel.x; OnBounce(s); }
                            if (s.pos.z > pp.z + hz && s.vel.z > 0 || s.pos.z < pp.z - hz && s.vel.z < 0) { s.vel.z = -s.vel.z; OnBounce(s); }
                            break;
                        }
                    case Motion.Orbit:
                        s.angle += s.orbitSpeed * dt;
                        s.pos = pp + new Vector3(Mathf.Cos(s.angle), 0f, Mathf.Sin(s.angle)) * s.orbitRadius + Vector3.up * 0.9f;
                        break;
                    case Motion.Lob:
                        {
                            float t = 1f - Mathf.Clamp01(s.life / s.maxLife);
                            var flat = Vector3.Lerp(s.origin, s.target, t);
                            flat.y = Mathf.Lerp(s.origin.y, G.Arena.GroundHeight(s.target) + 0.2f, t) + Mathf.Sin(t * Mathf.PI) * 3.5f;
                            s.pos = flat;
                            break;
                        }
                    case Motion.Zone:
                        {
                            if (s.orbitSpeed > 0f)
                            {
                                var t = G.Enemies.Nearest(s.pos, 14f);
                                if (t != null)
                                {
                                    var d = t.pos - s.pos; d.y = 0;
                                    s.pos += d.normalized * Mathf.Min(d.magnitude, s.orbitSpeed * dt);
                                }
                            }
                            s.tickTimer -= dt;
                            if (s.tickTimer <= 0f)
                            {
                                s.tickTimer = s.tickInterval;
                                ZoneHit(s, now);
                            }
                            float fade = Mathf.Clamp01(s.life / 0.4f) * Mathf.Clamp01((s.maxLife - s.life) / 0.12f);
                            s.tr.localScale = new Vector3(s.radius, 1f, s.radius) * Mathf.Lerp(0.6f, 1f, fade);
                            s.pos = G.Arena.OnGround(s.pos) + Vector3.up * 0.06f;
                            s.tr.position = s.pos;
                            break;
                        }
                }

                // Hit detection uses XZ distances; keep horizontal projectiles visible above the deck on slopes.
                if (s.motion == Motion.Straight || s.motion == Motion.Seek || s.motion == Motion.Bounce || s.motion == Motion.Boomerang)
                    s.pos.y = G.Arena.GroundHeight(s.pos) + s.heightAboveGround;

                if (s.motion != Motion.Zone && s.motion != Motion.Lob && s.motion != Motion.Turret)
                {
                    if (s.hostile)
                    {
                        if (player != null)
                        {
                            var d = s.pos - pp; d.y = 0;
                            if (d.magnitude < s.radius + player.Radius)
                            {
                                player.TakeDamage(s.damage, s.pos, "shot:" + s.kind);
                                end = true;
                            }
                        }
                    }
                    else if (HitEnemies(s, now)) end = true;
                    if (!G.Arena.Inside(s.pos, -3f)) end = true;
                }

                if (s.motion != Motion.Zone && s.motion != Motion.Turret) Place(s, dt);
                if (end)
                {
                    if (s.explodeRadius > 0f) Explode(s.pos, s.explodeRadius, s.explodeDamage, s.slot, s.explodeKnock, s.kind == ShotKind.Bazooka);
                    s.onEnd?.Invoke(s);
                    Recycle(s);
                    Active.RemoveAt(i);
                }
            }
        }

        private void OnBounce(Shot s)
        {
            s.bounces++;
            if (s.explodeRadius > 0f) Explode(s.pos, s.explodeRadius, s.explodeDamage, s.slot, s.explodeKnock, false);
        }

        public void Explode(Vector3 pos, float radius, float damage, int slot, float knock = 3f, bool big = false)
        {
            var ground = G.Arena.OnGround(pos);
            G.Fx.Ring(ground + Vector3.up * 0.1f, radius, new Color(1f, 0.85f, 0.2f, 0.8f), 0.35f, 0.35f);
            G.Fx.Burst(pos, new Color(1f, 0.85f, 0.15f), big ? 40 : 10, big ? 12f : 7f, 0.22f, FxKind.Crumb);
            if (big)
            {
                G.Fx.Slam(ground, radius);
                G.Sfx.Play(SfxId.Snap, 1f, 0.7f);
                G.Sfx.Play(SfxId.Slam, 0.9f, 0.9f);
                G.Cam.Shake(0.5f);
            }
            else G.Sfx.Play(SfxId.Pop, 0.35f, 1.2f);
            int n = G.Enemies.Query(pos, radius, hits);
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                if (e.immune[Enemy.SlotExplosion] > now) continue;
                e.immune[Enemy.SlotExplosion] = now + 0.3f;
                G.Enemies.Damage(e, damage, e.pos - pos, knock, slot);
            }
        }

        private bool HitEnemies(Shot s, float now)
        {
            int n = G.Enemies.Query(s.pos, s.radius, hits);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                if (s.hitCooldown > 0f)
                {
                    if (e.immune[s.slot] > now) continue;
                    e.immune[s.slot] = now + s.hitCooldown;
                }
                else
                {
                    bool seen = false;
                    for (int k = 0; k < s.hitCount; k++) if (s.hitUids[k] == e.uid) { seen = true; break; }
                    if (seen) continue;
                    if (s.hitCount < s.hitUids.Length) s.hitUids[s.hitCount++] = e.uid;
                }
                var push = s.motion == Motion.Orbit ? (e.pos - G.Player.Position) : s.vel;
                G.Enemies.Damage(e, s.damage, push, s.knock, s.slot);
                G.Fx.Burst(s.pos, Models.PastaGold, 3, 3f, 0.15f, FxKind.Crumb);
                if (s.pierce < 900 && --s.pierce <= 0) return true;
            }
            return false;
        }

        private void ZoneHit(Shot s, float now)
        {
            int n = G.Enemies.Query(s.pos, s.radius, hits);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                if (s.slow > 0f) e.slow = Mathf.Max(e.slow, s.tickInterval + 0.1f);
                if (s.sticky && !e.IsBoss) e.stun = Mathf.Max(e.stun, s.tickInterval + 0.05f);
                if (s.slippery) G.Enemies.Slip(e);
                if (e.immune[s.slot] > now) continue;
                e.immune[s.slot] = now + s.tickInterval * 0.9f;
                G.Enemies.Damage(e, s.damage, Vector3.zero, 0f, s.slot);
            }
        }

        private void Place(Shot s, float dt = 0f)
        {
            s.spin += s.spinRate * dt;
            Quaternion rot;
            switch (s.kind)
            {
                case ShotKind.Penne:
                case ShotKind.Bazooka:
                    rot = Quaternion.LookRotation(s.vel.sqrMagnitude > 0.01f ? s.vel : Vector3.forward) * Quaternion.Euler(0, 0, s.spin);
                    break;
                case ShotKind.Fusilli:
                    {
                        var tangent = new Vector3(-Mathf.Sin(s.angle), 0, Mathf.Cos(s.angle));
                        rot = Quaternion.LookRotation(tangent) * Quaternion.Euler(0, 0, s.spin);
                        break;
                    }
                case ShotKind.Ketchup:
                case ShotKind.Parmesan:
                    rot = Quaternion.Euler(s.spin, s.spin * 0.3f, 0);
                    break;
                case ShotKind.Meatball:
                    rot = Quaternion.Euler(s.spin, 0, 0);
                    break;
                default:
                    rot = Quaternion.Euler(0, s.spin, 0);
                    break;
            }
            s.tr.SetPositionAndRotation(s.pos, rot);
            s.tr.localScale = Vector3.one * s.scale;
        }

        private static Mesh MakeSplat()
        {
            const int n = 28;
            var v = new List<Vector3> { Vector3.zero };
            var c = new List<Color> { new Color(1, 1, 1, 0.9f) };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var t = new List<int>();
            var rng = new System.Random(3);
            for (int i = 0; i <= n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float r = i == n ? 1f : 0.85f + (float)rng.NextDouble() * 0.25f;
                v.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                c.Add(new Color(1, 1, 1, 0.75f));
                uv.Add(new Vector2(0.5f, 0.5f));
            }
            for (int i = 1; i <= n; i++) { t.Add(0); t.Add(i + 1 > n ? 1 : i + 1); t.Add(i); }
            var m = new Mesh { name = "Splat" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
