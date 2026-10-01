using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>
    /// Stage hazards that hurt everyone, Italians included, so luring the horde into them is a strategy:
    /// Roman traffic sweeping across the road, and Vesuvius ash bombs in Naples.
    /// </summary>
    public class Hazards : MonoBehaviour
    {
        private class Car
        {
            public GameObject go;
            public Vector3 from, dir;
            public float t, length, speed;
            public bool active;
        }

        private class Bomb { public Vector3 pos; public float t; }

        public const int Slot = 10;
        private readonly List<Car> cars = new List<Car>();
        private readonly List<Bomb> bombs = new List<Bomb>();
        private readonly List<(float at, int road, bool reverse)> pendingCars = new List<(float, int, bool)>();
        private readonly List<Enemy> hits = new List<Enemy>(64);
        private float[] roadTimers = new float[0];
        private float ashTimer;
        private Transform root;
        public int CarsDriven { get; private set; }
        public int BombsDropped { get; private set; }

        public void Begin()
        {
            if (root == null)
            {
                root = new GameObject("Hazards").transform;
                root.SetParent(transform, false);
            }
            Clear();
            var arena = G.Arena;
            roadTimers = new float[arena != null ? arena.roads.Count : 0];
            for (int i = 0; i < roadTimers.Length; i++) roadTimers[i] = 12f + i * 5f;
            ashTimer = 20f;
            CarsDriven = BombsDropped = 0;
        }

        public void Clear()
        {
            foreach (var c in cars) { c.active = false; c.go.SetActive(false); }
            bombs.Clear();
            pendingCars.Clear();
        }

        public void Tick(float dt)
        {
            var arena = G.Arena;
            if (arena == null || G.Player == null) return;
            arena.TickAreas(dt);
            float now = Time.time;

            // Traffic: a telegraphed lane, then a car sweeps the whole road.
            for (int i = 0; i < roadTimers.Length; i++)
            {
                roadTimers[i] -= dt;
                if (roadTimers[i] > 0f) continue;
                roadTimers[i] = Random.Range(9f, 15f);
                bool reverse = Random.value < 0.5f;
                var road = arena.roads[i];
                var a = reverse ? road.b : road.a;
                var b = reverse ? road.a : road.b;
                var dir = (b - a).normalized;
                G.Fx.Telegraph(TeleShape.Lane, a, dir, Vector3.Distance(a, b), road.width, 1.6f);
                pendingCars.Add((now + 1.6f, i, reverse));
                G.Sfx.Play(SfxId.Vroom, 0.3f, 0.7f);
            }
            for (int k = pendingCars.Count - 1; k >= 0; k--)
            {
                if (now < pendingCars[k].at) continue;
                var (_, i, reverse) = pendingCars[k];
                pendingCars.RemoveAt(k);
                var road = arena.roads[i];
                SpawnCar(reverse ? road.b : road.a, reverse ? road.a : road.b);
            }
            foreach (var c in cars)
            {
                if (!c.active) continue;
                c.t += dt * c.speed;
                var pos = c.from + c.dir * c.t;
                c.go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(c.dir));
                RunOver(pos, c.dir);
                if (c.t >= c.length) { c.active = false; c.go.SetActive(false); }
            }

            // Vesuvius: ash bombs land around the player (and the horde chasing them).
            if (arena.ashRain)
            {
                ashTimer -= dt;
                if (ashTimer <= 0f)
                {
                    ashTimer = Random.Range(7f, 11f);
                    var p = G.Player.Position + G.Player.Velocity * 1.2f;
                    for (int n = 0; n < 5; n++)
                    {
                        var at = n == 0 ? p : G.Player.Position + Quaternion.Euler(0, Random.value * 360f, 0) * Vector3.forward * Random.Range(4f, 13f);
                        at = arena.Clamp(at, 2f);
                        bombs.Add(new Bomb { pos = at, t = 1.5f + n * 0.12f });
                        G.Fx.Telegraph(TeleShape.Circle, at, Vector3.forward, 2.8f, 0f, 1.5f + n * 0.12f);
                    }
                    G.Fx.Banner("ヴェスヴィオの火山弾！", new Color(1f, 0.6f, 0.4f));
                }
                for (int k = bombs.Count - 1; k >= 0; k--)
                {
                    var b = bombs[k];
                    b.t -= dt;
                    if (b.t > 0f) continue;
                    bombs.RemoveAt(k);
                    Blast(b.pos, 2.8f);
                }
            }
        }

        private void SpawnCar(Vector3 from, Vector3 to)
        {
            Car car = null;
            foreach (var c in cars) if (!c.active) { car = c; break; }
            if (car == null)
            {
                car = new Car { go = Models.Static(Models.Get("car"), root, "Car", Mats.Lit, true) };
                car.go.transform.localScale = Vector3.one * 1.25f;
                cars.Add(car);
            }
            car.from = from;
            car.dir = (to - from).normalized;
            car.length = Vector3.Distance(from, to);
            car.t = 0f;
            car.speed = 26f;
            car.active = true;
            car.go.SetActive(true);
            CarsDriven++;
            G.Sfx.Play(SfxId.Vroom, 0.8f, 1.2f);
        }

        private void RunOver(Vector3 pos, Vector3 dir)
        {
            int n = G.Enemies.Query(pos, 1.6f, hits);
            float now = Time.time;
            var side = Vector3.Cross(Vector3.up, dir);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                if (e.immune[Slot] > now) continue;
                e.immune[Slot] = now + 1f;
                float sign = Mathf.Sign(Vector3.Dot(e.pos - pos, side) + 0.001f);
                G.Enemies.Damage(e, e.IsBoss ? 150f : 150f + 40f * G.Enemies.HpMul, dir * 1.5f + side * sign, 14f, Slot);
                G.Fx.Burst(e.Center, Color.white, 4, 6f, 0.5f, FxKind.Puff);
            }
            var p = G.Player;
            var d = p.Position - pos; d.y = 0f;
            if (d.magnitude < 1.5f + p.Radius) p.TakeDamage(18f, pos, "traffic");
        }

        private void Blast(Vector3 at, float radius)
        {
            G.Fx.Burst(at + Vector3.up * 0.5f, new Color(0.25f, 0.2f, 0.2f, 0.9f), 10, 4f, 1.6f, FxKind.Puff);
            G.Fx.Burst(at + Vector3.up * 0.3f, new Color(1f, 0.45f, 0.15f), 14, 8f, 0.25f, FxKind.Crumb);
            G.Fx.Ring(at + Vector3.up * 0.1f, radius, new Color(1f, 0.4f, 0.1f, 0.9f), 0.4f, 0.6f);
            G.Sfx.Play(SfxId.Slam, 0.6f, 0.7f);
            G.Cam.Shake(0.15f);
            BombsDropped++;
            int n = G.Enemies.Query(at, radius, hits);
            for (int i = 0; i < n; i++)
            {
                var e = hits[i];
                G.Enemies.Damage(e, e.IsBoss ? 60f : 70f * G.Enemies.HpMul, e.pos - at, 6f, Slot);
            }
            var p = G.Player;
            var d = p.Position - at; d.y = 0f;
            if (d.magnitude < radius + p.Radius) p.TakeDamage(14f, at, "ash");
        }
    }
}
