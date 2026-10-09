using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>
    /// Playable area without a physics engine: bounds, circular obstacles, box walls, water, and areas with rules.
    /// A 1 m walkability grid feeds a flow field so Italians route around walls and over bridges to reach the player.
    /// </summary>
    public class Arena
    {
        public struct Obstacle { public Vector2 c; public float r; public bool blocksShots; }
        public struct Wall { public Vector2 min, max; public bool blocksShots, water; }
        public enum AreaKind { Heal, Market }
        public class Area
        {
            public AreaKind kind;
            public Vector2 c;
            public float r;
            public string name;
            /// <summary>Heal areas hold a limited reservoir that refills slowly, so they're a pit stop rather than a camp.</summary>
            public float charge, capacity;
            public MeshRenderer ring;
        }
        public const float HealCapacity = 45f, HealRefill = 0.5f;
        public struct Road { public Vector3 a, b; public float width; }

        /// <summary>Local terrain identity and normal-wave modifiers; scheduled events remain stage-wide.</summary>
        public class District
        {
            public Rect bounds;
            public string name, hint;
            public Color color;
            public float population, interval;
            public (EnemyKind kind, float weight)[] preferences;

            public float Weight(EnemyKind kind)
            {
                foreach (var p in preferences) if (p.kind == kind) return p.weight;
                return 1f;
            }
        }

        /// <summary>The same piecewise-linear arch is used for the rendered deck and its walking surface.</summary>
        public struct BridgeSurface
        {
            public const int Segments = 24;
            public Vector3 center;
            public Vector2 size;
            public float rise;
            public bool AlongX => size.x >= size.y;
            public float Length => AlongX ? size.x : size.y;
            public float Width => AlongX ? size.y : size.x;
            public float Along(Vector3 p) => AlongX ? p.x - center.x : p.z - center.z;
            public bool Contains(Vector3 p) => Mathf.Abs(p.x - center.x) <= size.x * 0.5f
                && Mathf.Abs(p.z - center.z) <= size.y * 0.5f;

            public float NodeHeight(int node)
            {
                float u = 2f * node / Segments - 1f;
                return center.y + rise * (1f - u * u);
            }

            public float Height(Vector3 p)
            {
                float t = Mathf.Clamp01(Along(p) / Length + 0.5f) * Segments;
                int node = Mathf.Min(Mathf.FloorToInt(t), Segments - 1);
                return Mathf.Lerp(NodeHeight(node), NodeHeight(node + 1), t - node);
            }
        }

        public Vector2 min, max;
        public readonly List<Obstacle> obstacles = new List<Obstacle>();
        public readonly List<Wall> walls = new List<Wall>();
        public readonly List<Area> areas = new List<Area>();
        public readonly List<Road> roads = new List<Road>();
        public readonly List<District> districts = new List<District>();
        private readonly List<BridgeSurface> bridges = new List<BridgeSurface>();
        /// <summary>Pedestals where special weapons appear.</summary>
        public readonly List<Vector3> specialSpots = new List<Vector3>();
        public bool ashRain;
        /// <summary>Where the player starts.</summary>
        public Vector3 spawn;

        public Arena(Vector2 min, Vector2 max) { this.min = min; this.max = max; }

        public void AddDistrict(Rect bounds, string name, string hint, Color color, float population, float interval,
            params (EnemyKind kind, float weight)[] preferences)
            => districts.Add(new District { bounds = bounds, name = name, hint = hint, color = color,
                population = population, interval = interval, preferences = preferences });

        /// <summary>Earlier districts take priority where regions overlap.</summary>
        public District DistrictAt(Vector3 p)
        {
            var point = new Vector2(p.x, p.z);
            foreach (var d in districts) if (d.bounds.Contains(point)) return d;
            return null;
        }

        public void AddObstacle(Vector3 pos, float radius, bool blocksShots = false)
            => obstacles.Add(new Obstacle { c = new Vector2(pos.x, pos.z), r = radius, blocksShots = blocksShots });

        /// <summary>Box wall; size.x along X, size.y along Z.</summary>
        public void AddWall(Vector3 center, Vector2 size, bool blocksShots = true)
            => walls.Add(new Wall { min = new Vector2(center.x, center.z) - size * 0.5f, max = new Vector2(center.x, center.z) + size * 0.5f, blocksShots = blocksShots });

        /// <summary>Water blocks walking but not throws or sight.</summary>
        public void AddWater(Vector3 center, Vector2 size)
            => walls.Add(new Wall { min = new Vector2(center.x, center.z) - size * 0.5f, max = new Vector2(center.x, center.z) + size * 0.5f, water = true });

        public BridgeSurface AddBridge(Vector3 center, Vector2 size, float rise)
        {
            var bridge = new BridgeSurface { center = center, size = size, rise = rise };
            bridges.Add(bridge);
            return bridge;
        }

        public float GroundHeight(Vector3 p)
        {
            float height = 0f;
            foreach (var bridge in bridges)
                if (bridge.Contains(p)) height = Mathf.Max(height, bridge.Height(p));
            return height;
        }

        public Vector3 OnGround(Vector3 p)
        {
            p.y = GroundHeight(p);
            return p;
        }

        /// <summary>Pick the closest walking surface, so mouse movement/aiming also works on raised bridges.</summary>
        public bool RaycastGround(Ray ray, out Vector3 point)
        {
            float nearest = float.PositiveInfinity;
            if (ray.direction.y < -0.0001f && ray.origin.y >= 0f)
                nearest = -ray.origin.y / ray.direction.y;
            foreach (var bridge in bridges)
            {
                float step = bridge.Length / BridgeSurface.Segments;
                float origin = bridge.Along(ray.origin);
                float direction = bridge.AlongX ? ray.direction.x : ray.direction.z;
                for (int i = 0; i < BridgeSurface.Segments; i++)
                {
                    float start = -bridge.Length * 0.5f + i * step;
                    float slope = (bridge.NodeHeight(i + 1) - bridge.NodeHeight(i)) / step;
                    float denominator = ray.direction.y - slope * direction;
                    if (Mathf.Abs(denominator) < 0.0001f) continue;
                    float t = (bridge.NodeHeight(i) + slope * (origin - start) - ray.origin.y) / denominator;
                    if (t < 0f || t >= nearest) continue;
                    var hit = ray.GetPoint(t);
                    float along = bridge.Along(hit);
                    if (bridge.Contains(hit) && along >= start - 0.0001f && along <= start + step + 0.0001f)
                        nearest = t;
                }
            }
            point = float.IsPositiveInfinity(nearest) ? Vector3.zero : ray.GetPoint(nearest);
            return !float.IsPositiveInfinity(nearest);
        }

        public Area AddArea(AreaKind kind, Vector3 c, float r, string name)
        {
            var a = new Area { kind = kind, c = new Vector2(c.x, c.z), r = r, name = name, capacity = HealCapacity, charge = HealCapacity };
            areas.Add(a);
            return a;
        }

        /// <summary>Takes up to <paramref name="want"/> HP from a heal area under the point.</summary>
        public float DrawHeal(Vector3 p, float want)
        {
            foreach (var a in areas)
            {
                if (a.kind != AreaKind.Heal) continue;
                float dx = p.x - a.c.x, dz = p.z - a.c.y;
                if (dx * dx + dz * dz > a.r * a.r) continue;
                float got = Mathf.Min(want, a.charge);
                a.charge -= got;
                return got;
            }
            return 0f;
        }

        private static MaterialPropertyBlock areaBlock;

        public void TickAreas(float dt)
        {
            areaBlock ??= new MaterialPropertyBlock();
            foreach (var a in areas)
            {
                if (a.kind != AreaKind.Heal) continue;
                a.charge = Mathf.Min(a.capacity, a.charge + HealRefill * dt);
                if (a.ring == null) continue;
                float k = a.charge / a.capacity;
                areaBlock.SetColor("_BaseColor", Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.25f), new Color(0.35f, 1f, 0.5f, 0.6f), k));
                a.ring.SetPropertyBlock(areaBlock);
            }
        }

        public bool Inside(Vector3 p, float margin)
            => p.x > min.x + margin && p.x < max.x - margin && p.z > min.y + margin && p.z < max.y - margin;

        public Vector3 Clamp(Vector3 p, float margin)
        {
            p.x = Mathf.Clamp(p.x, min.x + margin, max.x - margin);
            p.z = Mathf.Clamp(p.z, min.y + margin, max.y - margin);
            return p;
        }

        public bool InArea(Vector3 p, AreaKind kind)
        {
            foreach (var a in areas)
            {
                if (a.kind != kind) continue;
                float dx = p.x - a.c.x, dz = p.z - a.c.y;
                if (dx * dx + dz * dz <= a.r * a.r) return true;
            }
            return false;
        }

        public bool Blocked(Vector3 p, float r)
        {
            foreach (var o in obstacles)
            {
                float dx = p.x - o.c.x, dz = p.z - o.c.y, rr = o.r + r;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            foreach (var w in walls)
                if (p.x > w.min.x - r && p.x < w.max.x + r && p.z > w.min.y - r && p.z < w.max.y + r) return true;
            return false;
        }

        /// <summary>Push a circle out of obstacles and walls and keep it inside the bounds.</summary>
        public Vector3 Resolve(Vector3 p, float r)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                float dx = p.x - o.c.x, dz = p.z - o.c.y, rr = o.r + r;
                float d2 = dx * dx + dz * dz;
                if (d2 >= rr * rr) continue;
                float d = Mathf.Sqrt(d2);
                if (d < 0.0001f) { dx = 1f; dz = 0f; d = 1f; }
                float push = rr - d;
                p.x += dx / d * push;
                p.z += dz / d * push;
            }
            for (int i = 0; i < walls.Count; i++)
            {
                var w = walls[i];
                if (p.x <= w.min.x - r || p.x >= w.max.x + r || p.z <= w.min.y - r || p.z >= w.max.y + r) continue;
                float qx = Mathf.Clamp(p.x, w.min.x, w.max.x), qz = Mathf.Clamp(p.z, w.min.y, w.max.y);
                float dx = p.x - qx, dz = p.z - qz;
                float d2 = dx * dx + dz * dz;
                if (d2 > 0.000001f)
                {
                    if (d2 >= r * r) continue;
                    float d = Mathf.Sqrt(d2);
                    p.x += dx / d * (r - d);
                    p.z += dz / d * (r - d);
                }
                else
                {
                    // Centre inside the box: leave along the shallowest side.
                    float left = p.x - w.min.x, right = w.max.x - p.x, down = p.z - w.min.y, up = w.max.y - p.z;
                    float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
                    if (m == left) p.x = w.min.x - r;
                    else if (m == right) p.x = w.max.x + r;
                    else if (m == down) p.z = w.min.y - r;
                    else p.z = w.max.y + r;
                }
            }
            return OnGround(Clamp(p, r));
        }

        /// <summary>True if a thrown object at this point hits cover.</summary>
        public bool ShotBlocked(Vector3 p)
        {
            foreach (var w in walls)
                if (w.blocksShots && p.x > w.min.x && p.x < w.max.x && p.z > w.min.y && p.z < w.max.y) return true;
            foreach (var o in obstacles)
            {
                if (!o.blocksShots) continue;
                float dx = p.x - o.c.x, dz = p.z - o.c.y;
                if (dx * dx + dz * dz < o.r * o.r) return true;
            }
            return false;
        }

        /// <summary>Whether cover blocks the straight line between two points.</summary>
        public bool LineOfSight(Vector3 a, Vector3 b)
        {
            Vector2 p0 = new Vector2(a.x, a.z), p1 = new Vector2(b.x, b.z), d = p1 - p0;
            foreach (var w in walls)
            {
                if (!w.blocksShots) continue;
                float t0 = 0f, t1 = 1f;
                if (Clip(-d.x, p0.x - w.min.x, ref t0, ref t1) && Clip(d.x, w.max.x - p0.x, ref t0, ref t1)
                    && Clip(-d.y, p0.y - w.min.y, ref t0, ref t1) && Clip(d.y, w.max.y - p0.y, ref t0, ref t1)) return false;
            }
            float len2 = Mathf.Max(0.0001f, d.sqrMagnitude);
            foreach (var o in obstacles)
            {
                if (!o.blocksShots) continue;
                float t = Mathf.Clamp01(Vector2.Dot(o.c - p0, d) / len2);
                if ((p0 + d * t - o.c).sqrMagnitude < o.r * o.r) return false;
            }
            return true;
        }

        /// <summary>Whether a body of radius r can walk straight from a to b (walls, water and obstacles).</summary>
        public bool WalkClear(Vector3 a, Vector3 b, float r)
        {
            Vector2 p0 = new Vector2(a.x, a.z), p1 = new Vector2(b.x, b.z), d = p1 - p0;
            foreach (var w in walls)
            {
                float t0 = 0f, t1 = 1f;
                if (Clip(-d.x, p0.x - (w.min.x - r), ref t0, ref t1) && Clip(d.x, (w.max.x + r) - p0.x, ref t0, ref t1)
                    && Clip(-d.y, p0.y - (w.min.y - r), ref t0, ref t1) && Clip(d.y, (w.max.y + r) - p0.y, ref t0, ref t1)) return false;
            }
            float len2 = Mathf.Max(0.0001f, d.sqrMagnitude);
            foreach (var o in obstacles)
            {
                if (o.r < 0.6f) continue;
                float t = Mathf.Clamp01(Vector2.Dot(o.c - p0, d) / len2);
                float rr = o.r + r;
                if ((p0 + d * t - o.c).sqrMagnitude < rr * rr) return false;
            }
            return true;
        }

        private static bool Clip(float den, float num, ref float t0, ref float t1)
        {
            if (Mathf.Abs(den) < 1e-6f) return num >= 0f;
            float t = num / den;
            if (den > 0f) { if (t < t0) return false; if (t < t1) t1 = t; }
            else { if (t > t1) return false; if (t > t0) t0 = t; }
            return true;
        }

        // ---------------- flow field ----------------

        public const float Cell = 1f;
        private const int Unreached = int.MaxValue;
        public int GridW { get; private set; }
        public int GridH { get; private set; }
        public bool[] BlockedCells { get; private set; }
        public bool[] WaterCells { get; private set; }
        private int[] dist;
        private int[] heap, heapKey;
        private int heapCount;
        private int flowTarget = -1;

        public void BuildGrid()
        {
            flowTarget = -1;
            GridW = Mathf.CeilToInt((max.x - min.x) / Cell);
            GridH = Mathf.CeilToInt((max.y - min.y) / Cell);
            int n = GridW * GridH;
            BlockedCells = new bool[n];
            WaterCells = new bool[n];
            dist = new int[n];
            heap = new int[n * 8 + 16];
            heapKey = new int[n * 8 + 16];
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    var c = CellCenter(x, y);
                    int i = y * GridW + x;
                    BlockedCells[i] = Blocked(c, 0.3f);
                    foreach (var w in walls)
                        if (w.water && c.x > w.min.x && c.x < w.max.x && c.z > w.min.y && c.z < w.max.y) WaterCells[i] = true;
                }
            for (int i = 0; i < n; i++) dist[i] = Unreached;
        }

        public Vector3 CellCenter(int x, int y) => new Vector3(min.x + (x + 0.5f) * Cell, 0f, min.y + (y + 0.5f) * Cell);

        public int CellIndex(Vector3 p)
        {
            int x = Mathf.Clamp((int)((p.x - min.x) / Cell), 0, GridW - 1);
            int y = Mathf.Clamp((int)((p.z - min.y) / Cell), 0, GridH - 1);
            return y * GridW + x;
        }

        public bool Reachable(Vector3 p)
        {
            if (dist == null) return true;
            int i = CellIndex(p);
            return !BlockedCells[i] && (flowTarget < 0 || dist[i] != Unreached);
        }

        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly int[] StepCost = { 10, 10, 10, 10, 14, 14, 14, 14 };

        /// <summary>Dijkstra from the player's cell (orthogonal 10, diagonal 14; no corner cutting).</summary>
        public void UpdateFlow(Vector3 target)
        {
            if (dist == null) return;
            int start = CellIndex(target);
            if (BlockedCells[start])
            {
                // Standing hard against a wall: seed from the nearest open neighbour.
                for (int k = 0; k < 8; k++)
                {
                    int nx = start % GridW + DX[k], ny = start / GridW + DY[k];
                    if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                    int ni = ny * GridW + nx;
                    if (!BlockedCells[ni]) { start = ni; break; }
                }
            }
            // Geometry is static after BuildGrid; keep the full field while the target cell is unchanged.
            if (flowTarget == start) return;
            flowTarget = start;
            for (int i = 0; i < dist.Length; i++) dist[i] = Unreached;
            heapCount = 0;
            dist[start] = 0;
            Push(start, 0);
            while (heapCount > 0)
            {
                int c = Pop(out int key);
                if (key > dist[c]) continue; // stale entry
                int cx = c % GridW, cy = c / GridW, dc = dist[c];
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                    int ni = ny * GridW + nx;
                    if (BlockedCells[ni]) continue;
                    if (k >= 4 && (BlockedCells[cy * GridW + nx] || BlockedCells[ny * GridW + cx])) continue;
                    int nd = dc + StepCost[k];
                    if (nd >= dist[ni]) continue;
                    dist[ni] = nd;
                    Push(ni, nd);
                }
            }
        }

        /// <summary>Direction to walk toward the player along the flow field (zero if unknown).</summary>
        public Vector3 FlowDir(Vector3 p)
        {
            if (dist == null || flowTarget < 0) return Vector3.zero;
            int c = CellIndex(p);
            int cx = c % GridW, cy = c / GridW;
            if (BlockedCells[c] || dist[c] == Unreached)
            {
                // Pushed against a wall: head for the best open neighbour.
                int best = -1, bestD = Unreached;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                    int ni = ny * GridW + nx;
                    if (!BlockedCells[ni] && dist[ni] < bestD) { bestD = dist[ni]; best = ni; }
                }
                if (best < 0) return Vector3.zero;
                var to = CellCenter(best % GridW, best / GridW) - p; to.y = 0f;
                return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
            }
            float gx = 0f, gz = 0f;
            int dc = dist[c];
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + DX[k], ny = cy + DY[k];
                if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                int ni = ny * GridW + nx;
                if (BlockedCells[ni] || dist[ni] >= dc) continue;
                if (k >= 4 && (BlockedCells[cy * GridW + nx] || BlockedCells[ny * GridW + cx])) continue;
                float w = (dc - dist[ni]) / (k >= 4 ? 1.414f : 1f);
                gx += DX[k] * w;
                gz += DY[k] * w;
            }
            var g = new Vector3(gx, 0f, gz);
            return g.sqrMagnitude > 0.0001f ? g.normalized : Vector3.zero;
        }

        // Binary heap with keys fixed at push time (lazy deletion of stale entries).
        private void Push(int i, int key)
        {
            if (heapCount >= heap.Length) return;
            int k = heapCount++;
            heap[k] = i;
            heapKey[k] = key;
            while (k > 0)
            {
                int parent = (k - 1) >> 1;
                if (heapKey[parent] <= heapKey[k]) break;
                Swap(parent, k);
                k = parent;
            }
        }

        private int Pop(out int key)
        {
            int top = heap[0];
            key = heapKey[0];
            heapCount--;
            heap[0] = heap[heapCount];
            heapKey[0] = heapKey[heapCount];
            int k = 0;
            while (true)
            {
                int l = k * 2 + 1, r = l + 1, s = k;
                if (l < heapCount && heapKey[l] < heapKey[s]) s = l;
                if (r < heapCount && heapKey[r] < heapKey[s]) s = r;
                if (s == k) break;
                Swap(s, k);
                k = s;
            }
            return top;
        }

        private void Swap(int a, int b)
        {
            (heap[a], heap[b]) = (heap[b], heap[a]);
            (heapKey[a], heapKey[b]) = (heapKey[b], heapKey[a]);
        }
    }
}
