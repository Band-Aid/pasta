using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum PickupKind { Gem, Coin, CoinBag, Pizza, Fork, Rosary, Chest, Special }

    public class Pickup
    {
        public PickupKind kind;
        public string key;
        public int value;
        public Vector3 pos;
        public bool attracted, active;
        public float speed, age, seed;
        public SpecialId special;
        /// <summary>A dropped special can't be re-collected until the player steps away.</summary>
        public bool armed = true;
        public GameObject go;
        public Transform tr;
    }

    public class PickupManager : MonoBehaviour
    {
        public const int MaxGems = 320;
        public readonly List<Pickup> Active = new List<Pickup>(512);
        private readonly Dictionary<string, Stack<Pickup>> pools = new Dictionary<string, Stack<Pickup>>();
        private Transform root;
        private int gemCount;
        private Pickup overflow;
        private float barrelTimer = 8f, gemChain, gemChainTimer, specialTimer = 10f;
        private int specialsSpawned;
        private MaterialPropertyBlock beamBlock;

        public void Init()
        {
            root = new GameObject("Pickups").transform;
            root.SetParent(transform, false);
        }

        private static string GemKey(int v) => v >= 25 ? "gem4" : v >= 5 ? "gem3" : v >= 2 ? "gem2" : "gem1";

        private Pickup Spawn(PickupKind kind, string key, Vector3 pos, int value)
        {
            if (!pools.TryGetValue(key, out var pool)) pools[key] = pool = new Stack<Pickup>();
            Pickup p;
            if (pool.Count > 0) p = pool.Pop();
            else
            {
                p = new Pickup { key = key };
                bool glow = kind == PickupKind.Gem || kind == PickupKind.Chest || kind == PickupKind.Rosary || kind == PickupKind.Special;
                p.go = Models.Static(Models.Get(key), root, key, glow ? Mats.Glow : Mats.Lit, kind == PickupKind.Chest);
                p.tr = p.go.transform;
                if (kind == PickupKind.Special)
                {
                    // A column of light so specials can be found from across the map.
                    var beam = Models.Static(Models.Get("beam"), p.tr, "Beam", Mats.FxAdd, false);
                    beamBlock ??= new MaterialPropertyBlock();
                    beamBlock.SetColor("_BaseColor", new Color(1f, 0.8f, 0.35f, 0.22f));
                    beam.GetComponent<MeshRenderer>().SetPropertyBlock(beamBlock);
                    beam.transform.localPosition = Vector3.down * 0.6f;
                }
            }
            p.kind = kind;
            p.value = value;
            p.pos = G.Arena.OnGround(pos);
            p.attracted = false;
            p.armed = true;
            p.active = true;
            p.speed = 0f;
            p.age = 0f;
            p.seed = Random.value * 10f;
            p.go.SetActive(true);
            p.tr.position = p.pos;
            p.tr.localScale = Vector3.one * (kind == PickupKind.Chest ? 1.3f : kind == PickupKind.Special ? 1.5f : 1f);
            Active.Add(p);
            if (kind == PickupKind.Gem) gemCount++;
            return p;
        }

        public void DropGem(Vector3 pos, int value)
        {
            if (value <= 0) return;
            if (gemCount >= MaxGems)
            {
                // Merge excess experience into one big gem, like the genre does.
                if (overflow == null || !overflow.active) overflow = Spawn(PickupKind.Gem, "gem4", pos, 0);
                overflow.value += value;
                return;
            }
            var jitter = Random.insideUnitCircle * 0.4f;
            Spawn(PickupKind.Gem, GemKey(value), pos + new Vector3(jitter.x, 0f, jitter.y), value);
        }

        public void Drop(PickupKind kind, Vector3 pos)
        {
            string key = kind switch
            {
                PickupKind.Coin => "coin",
                PickupKind.CoinBag => "coinbag",
                PickupKind.Pizza => "pizzaslice",
                PickupKind.Fork => "fork",
                PickupKind.Rosary => "rosary",
                PickupKind.Chest => "chest",
                _ => "gem1"
            };
            Spawn(kind, key, G.Arena.Clamp(pos, 1f), kind == PickupKind.CoinBag ? 25 : 1);
            if (kind == PickupKind.Chest) G.Fx.Ring(pos + Vector3.up * 0.1f, 2f, new Color(1f, 0.85f, 0.3f, 0.8f), 0.6f, 0.3f);
        }

        public Pickup SpawnSpecial(SpecialId id, Vector3 pos, bool armed = true)
        {
            var p = Spawn(PickupKind.Special, "special_" + id, pos, 0);
            p.special = id;
            p.armed = armed;
            G.Fx.Ring(pos + Vector3.up * 0.1f, 2.5f, new Color(1f, 0.85f, 0.3f, 0.9f), 0.8f, 0.4f);
            return p;
        }

        /// <summary>Specials appear on the stage's pedestals: first one nearby, later ones across the map.</summary>
        private void TickSpecials(float dt, Vector3 pp)
        {
            var spots = G.Arena.specialSpots;
            if (spots.Count == 0) return;
            specialTimer -= dt;
            if (specialTimer > 0f) return;
            specialTimer = Random.Range(45f, 65f);
            int onMap = 0;
            foreach (var p in Active) if (p.kind == PickupKind.Special) onMap++;
            if (onMap >= 2) return;
            Vector3 best = spots[0];
            float bestScore = float.MaxValue;
            foreach (var s in spots)
            {
                bool taken = false;
                foreach (var p in Active) if (p.kind == PickupKind.Special && (p.pos - s).sqrMagnitude < 4f) taken = true;
                if (taken) continue;
                float d = Vector3.Distance(s, pp);
                // The first special is close so players learn the mechanic; later ones pull you across the map.
                float score = specialsSpawned == 0 ? d : Mathf.Abs(d - 28f) + Random.value * 12f;
                if (score < bestScore) { bestScore = score; best = s; }
            }
            var ids = (SpecialId[])System.Enum.GetValues(typeof(SpecialId));
            var id = ids[Random.Range(0, ids.Length)];
            if (G.Player.Special.HasValue && id == G.Player.Special.Value) id = ids[((int)id + 1 + Random.Range(0, ids.Length - 1)) % ids.Length];
            SpawnSpecial(id, best);
            specialsSpawned++;
            G.Fx.Banner("特殊武器が出現！ 光の柱を目指せ：" + GameData.Special(id).name, new Color(1f, 0.85f, 0.4f));
        }

        public void DropFromBarrel(Vector3 pos)
        {
            float r = Random.value;
            var kind = r < 0.38f ? PickupKind.Pizza : r < 0.68f ? PickupKind.Coin : r < 0.8f ? PickupKind.CoinBag : r < 0.93f ? PickupKind.Fork : PickupKind.Rosary;
            Drop(kind, pos);
        }

        public void Clear()
        {
            foreach (var p in Active) Recycle(p);
            Active.Clear();
            gemCount = 0;
            overflow = null;
            barrelTimer = 8f;
            specialTimer = 10f;
            specialsSpawned = 0;
        }

        private void Recycle(Pickup p)
        {
            p.active = false;
            p.go.SetActive(false);
            pools[p.key].Push(p);
        }

        public void AttractAllGems()
        {
            foreach (var p in Active) if (p.kind == PickupKind.Gem || p.kind == PickupKind.Coin) p.attracted = true;
        }

        public void Tick(float dt)
        {
            var pl = G.Player;
            if (pl == null) return;
            Vector3 pp = pl.Position;
            float magnet2 = pl.MagnetRadius * pl.MagnetRadius;
            gemChainTimer -= dt;
            if (gemChainTimer <= 0f) gemChain = 0f;
            bool swapped = false;

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var p = Active[i];
                p.age += dt;
                Vector3 d = pp - p.pos; d.y = 0f;
                float dist2 = d.sqrMagnitude;
                if (p.kind == PickupKind.Special)
                {
                    if (!p.armed && dist2 > 2.4f * 2.4f) p.armed = true;
                    if (p.armed && !swapped && dist2 < 1.5f * 1.5f)
                    {
                        swapped = true; // one swap per frame, even if two specials overlap
                        Collect(p);
                        Recycle(p);
                        Active.RemoveAt(i);
                        continue;
                    }
                }
                else if (!p.attracted)
                {
                    float reach2 = p.kind == PickupKind.Chest ? 1.4f * 1.4f : magnet2;
                    if (dist2 < reach2) p.attracted = true;
                }
                if (p.attracted)
                {
                    p.speed = Mathf.Min(p.speed + dt * 40f, 26f);
                    float dist = Mathf.Sqrt(dist2);
                    // Brief hop away before flying in reads better.
                    float step = p.speed * dt;
                    if (dist <= step + 0.5f)
                    {
                        Collect(p);
                        Recycle(p);
                        Active.RemoveAt(i);
                        continue;
                    }
                    p.pos += d / dist * step;
                }
                float bob = p.kind == PickupKind.Chest ? 0.05f : p.kind == PickupKind.Special ? 1.3f + Mathf.Sin(p.age * 2f) * 0.2f : 0.35f + Mathf.Sin(p.age * 3f + p.seed) * 0.12f;
                p.pos = G.Arena.OnGround(p.pos);
                p.tr.position = p.pos + Vector3.up * bob;
                p.tr.rotation = Quaternion.Euler(p.kind == PickupKind.Coin ? 0f : 0f, (p.age * 120f + p.seed * 40f) % 360f, 0f);
            }

            TickSpecials(dt, pp);

            // Wine barrels to break for food and coins — markets get restocked much more often.
            barrelTimer -= dt;
            if (barrelTimer <= 0f)
            {
                barrelTimer = Random.Range(7f, 11f);
                int barrels = 0;
                bool regional = G.Arena.districts.Count > 0;
                Enemy distantBarrel = null;
                float farthest = 60f * 60f;
                foreach (var e in G.Enemies.Active)
                {
                    if (!e.IsProp) continue;
                    barrels++;
                    float distance = (e.pos - pp).sqrMagnitude;
                    if (regional && distance > farthest) { farthest = distance; distantBarrel = e; }
                }
                // Roma now has several distant markets. Supply the nearby one instead of filling the original market first.
                Arena.Area nearbyMarket = null;
                float nearest = float.PositiveInfinity;
                if (regional)
                    foreach (var a in G.Arena.areas)
                    {
                        if (a.kind != Arena.AreaKind.Market) continue;
                        float distance = (a.c - new Vector2(pp.x, pp.z)).sqrMagnitude;
                        if (distance < nearest && distance < (a.r + 24f) * (a.r + 24f))
                        { nearest = distance; nearbyMarket = a; }
                    }
                bool placed = false;
                if ((barrels < 10 || distantBarrel != null) && Random.value < 0.65f)
                {
                    foreach (var a in G.Arena.areas)
                    {
                        if (a.kind != Arena.AreaKind.Market || (regional && a != nearbyMarket) || Random.value < 0.4f) continue;
                        for (int tries = 0; tries < 10 && !placed; tries++)
                        {
                            var off = Random.insideUnitCircle * a.r;
                            var at = new Vector3(a.c.x + off.x, 0f, a.c.y + off.y);
                            if (!G.Arena.Inside(at, 1.5f) || G.Arena.Blocked(at, 0.9f)) continue;
                            RestockBarrel(at, barrels < 10 ? null : distantBarrel);
                            placed = true;
                        }
                        if (placed) break;
                    }
                }
                if (!placed && (barrels < 6 || distantBarrel != null))
                {
                    for (int tries = 0; tries < 10; tries++)
                    {
                        var at = pp + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(7f, 16f);
                        if (!G.Arena.Inside(at, 2f) || G.Arena.Blocked(at, 1.2f)) continue;
                        RestockBarrel(at, barrels < 6 ? null : distantBarrel);
                        break;
                    }
                }
            }
        }

        private static void RestockBarrel(Vector3 at, Enemy reusable)
        {
            if (reusable == null) G.Enemies.Spawn(EnemyKind.Barrel, at);
            else
            {
                // Reuse a barrel beyond sight without granting loot, increasing the prop cap, or touching the pursuing enemies.
                reusable.pos = at;
                reusable.ApplyTransform();
            }
        }

        private void Collect(Pickup p)
        {
            var pl = G.Player;
            switch (p.kind)
            {
                case PickupKind.Gem:
                    gemCount--;
                    if (p == overflow) overflow = null;
                    pl.AddXp(p.value);
                    gemChain = Mathf.Min(gemChain + 1f, 24f);
                    gemChainTimer = 0.4f;
                    G.Sfx.Play(SfxId.Gem, 0.18f, 1f + gemChain * 0.03f);
                    break;
                case PickupKind.Coin:
                    pl.RunCoins += Mathf.Max(1, Mathf.RoundToInt(1 * pl.GreedMul));
                    G.Sfx.Play(SfxId.Coin, 0.4f, 1.2f);
                    break;
                case PickupKind.CoinBag:
                    pl.RunCoins += Mathf.RoundToInt(p.value * pl.GreedMul);
                    G.Sfx.Play(SfxId.Coin, 0.6f, 0.9f);
                    G.Fx.Shout(pl.Position + Vector3.up * 2.4f, "+" + Mathf.RoundToInt(p.value * pl.GreedMul) + " €", new Color(1f, 0.85f, 0.3f));
                    break;
                case PickupKind.Pizza:
                    pl.Heal(30f);
                    G.Sfx.Play(SfxId.Heal, 0.6f);
                    break;
                case PickupKind.Fork:
                    AttractAllGems();
                    G.Sfx.Play(SfxId.Magnet, 0.6f);
                    G.Fx.Banner("マイフォーク！ 経験値を全部回収", new Color(0.8f, 0.9f, 1f));
                    break;
                case PickupKind.Rosary:
                    G.Enemies.RoutAll(pl.Position, 26f);
                    G.Fx.Ring(pl.Position + Vector3.up * 0.1f, 22f, new Color(1f, 1f, 0.8f, 0.9f), 0.9f, 1f);
                    G.Cam.Shake(0.5f);
                    G.Sfx.Play(SfxId.Boss, 0.6f, 1.6f);
                    G.Fx.Banner("ノンナのロザリオ！ 周囲のイタリア人が退散", new Color(1f, 0.95f, 0.7f));
                    break;
                case PickupKind.Chest:
                    G.Game.OpenChest();
                    break;
                case PickupKind.Special:
                    if (pl.Special.HasValue && pl.Special.Value != p.special)
                        SpawnSpecial(pl.Special.Value, p.pos, false); // swap: leave the old one on the pedestal
                    pl.GiveSpecial(p.special);
                    break;
            }
        }
    }
}
