using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum PickupKind { Gem, Coin, CoinBag, Pizza, Fork, Rosary, Chest }

    public class Pickup
    {
        public PickupKind kind;
        public string key;
        public int value;
        public Vector3 pos;
        public bool attracted, active;
        public float speed, age, seed;
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
        private float barrelTimer = 8f, gemChain, gemChainTimer;

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
                bool glow = kind == PickupKind.Gem || kind == PickupKind.Chest || kind == PickupKind.Rosary;
                p.go = Models.Static(Models.Get(key), root, key, glow ? Mats.Glow : Mats.Lit, kind == PickupKind.Chest);
                p.tr = p.go.transform;
            }
            p.kind = kind;
            p.value = value;
            p.pos = new Vector3(pos.x, 0f, pos.z);
            p.attracted = false;
            p.active = true;
            p.speed = 0f;
            p.age = 0f;
            p.seed = Random.value * 10f;
            p.go.SetActive(true);
            p.tr.localScale = Vector3.one * (kind == PickupKind.Chest ? 1.3f : 1f);
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

            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var p = Active[i];
                p.age += dt;
                Vector3 d = pp - p.pos; d.y = 0f;
                float dist2 = d.sqrMagnitude;
                if (!p.attracted)
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
                float bob = p.kind == PickupKind.Chest ? 0.05f : 0.35f + Mathf.Sin(p.age * 3f + p.seed) * 0.12f;
                p.tr.position = new Vector3(p.pos.x, bob, p.pos.z);
                p.tr.rotation = Quaternion.Euler(p.kind == PickupKind.Coin ? 0f : 0f, (p.age * 120f + p.seed * 40f) % 360f, 0f);
            }

            // Wine barrels to break for food and coins.
            barrelTimer -= dt;
            if (barrelTimer <= 0f)
            {
                barrelTimer = Random.Range(14f, 22f);
                int barrels = 0;
                foreach (var e in G.Enemies.Active) if (e.IsProp) barrels++;
                if (barrels < 5)
                {
                    for (int tries = 0; tries < 10; tries++)
                    {
                        var at = pp + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(7f, 16f);
                        if (!G.Arena.Inside(at, 2f) || G.Arena.Blocked(at, 1.2f)) continue;
                        G.Enemies.Spawn(EnemyKind.Barrel, at);
                        break;
                    }
                }
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
            }
        }
    }
}
