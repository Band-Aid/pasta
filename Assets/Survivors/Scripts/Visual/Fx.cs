using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum FxKind { Crumb, Puff, Spark }
    public enum TeleShape { Lane, Sector, Circle }

    public class Fx : MonoBehaviour
    {
        private class Flat
        {
            public GameObject go;
            public Transform tr;
            public MeshRenderer mr;
            public float t, duration, radius, width;
            public Color color;
            public int mode;           // 0 ring, 1 snap sector, 2 telegraph base, 3 telegraph fill, 4 sweep
            public Vector3 pos, dir;
            public float length, angle;
            public TeleShape shape;
            public string key;
        }

        private ParticleSystem crumbs, puffs, sparks;
        private readonly List<Flat> active = new List<Flat>(64);
        private readonly Dictionary<string, Stack<Flat>> pools = new Dictionary<string, Stack<Flat>>();
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private MaterialPropertyBlock mpb;
        private Transform root;
        private GameObject aura, reticle, beamOuter, beamCore;
        private MaterialPropertyBlock beamBlock;
        private MeshRenderer auraRenderer;
        private float pokiCooldown;

        public void Init()
        {
            root = new GameObject("Fx").transform;
            root.SetParent(transform, false);
            mpb = new MaterialPropertyBlock();
            var crumbMesh = new MeshKit().Box(Vector3.zero, new Vector3(0.6f, 0.6f, 1.6f), Color.white).ToMesh("Crumb");
            crumbs = MakeSystem("Crumbs", new Material(Mats.Lit), crumbMesh, 2.2f);
            puffs = MakeSystem("Puffs", Mats.Particles(false), null, -0.1f);
            sparks = MakeSystem("Sparks", Mats.Particles(true), null, 0f);
            aura = Models.Static(Mesh("disc"), root, "Aura", Mats.FxAlpha, false);
            auraRenderer = aura.GetComponent<MeshRenderer>();
            aura.SetActive(false);
            reticle = Models.Static(MeshKit.Sector(0.55f, 0.75f, 360f, 32, "Reticle"), root, "Reticle", Mats.FxAdd, false);
            var rb = new MaterialPropertyBlock();
            rb.SetColor("_BaseColor", new Color(1f, 0.9f, 0.5f, 0.9f));
            reticle.GetComponent<MeshRenderer>().SetPropertyBlock(rb);
            reticle.SetActive(false);
            // Sta○ Beam: a green shell around a white core, stretched along Z.
            var beamMesh = new MeshKit().Cyl(new Vector3(0f, 0f, 0.5f), Vector3.one, Color.white, new Vector3(90, 0, 0), 14).ToMesh("Beam");
            beamOuter = Models.Static(beamMesh, root, "Beam outer", Mats.FxAdd, false);
            beamCore = Models.Static(beamMesh, root, "Beam core", Mats.FxAdd, false);
            beamBlock = new MaterialPropertyBlock();
            beamOuter.SetActive(false);
            beamCore.SetActive(false);
        }

        public void Beam(Vector3 origin, Vector3 dir, float length, bool on)
        {
            if (!on || length <= 0f)
            {
                beamOuter.SetActive(false);
                beamCore.SetActive(false);
                return;
            }
            beamOuter.SetActive(true);
            beamCore.SetActive(true);
            var rot = Quaternion.LookRotation(dir);
            float t = Time.time;
            float wobble = 1f + Mathf.Sin(t * 40f) * 0.08f;
            beamOuter.transform.SetPositionAndRotation(origin, rot);
            beamOuter.transform.localScale = new Vector3(1.7f * wobble, 1.7f * wobble, length);
            beamCore.transform.SetPositionAndRotation(origin, rot);
            beamCore.transform.localScale = new Vector3(0.6f, 0.6f, length);
            beamBlock.SetColor("_BaseColor", new Color(0.05f, 0.65f, 0.35f, 0.55f));
            beamOuter.GetComponent<MeshRenderer>().SetPropertyBlock(beamBlock);
            beamBlock.SetColor("_BaseColor", new Color(1f, 1f, 0.95f, 0.9f));
            beamCore.GetComponent<MeshRenderer>().SetPropertyBlock(beamBlock);
            // Whipped cream and coffee spray where the beam lands.
            var end = origin + dir * length;
            if (Random.value < 0.6f) Burst(end, new Color(1f, 0.98f, 0.92f, 0.8f), 1, 2f, 0.7f, FxKind.Puff);
            if (Random.value < 0.6f) Burst(end, new Color(0.45f, 0.28f, 0.15f), 2, 5f, 0.12f, FxKind.Crumb);
            if (Random.value < 0.35f) Burst(origin + dir * Random.Range(0f, length), new Color(0.6f, 1f, 0.7f, 0.9f), 1, 1f, 0.3f, FxKind.Spark);
        }

        /// <summary>Ground marker under the mouse cursor in the top-down view.</summary>
        public void Reticle(Vector3 pos, bool show)
        {
            if (reticle.activeSelf != show) reticle.SetActive(show);
            if (!show) return;
            reticle.transform.SetPositionAndRotation(new Vector3(pos.x, 0.08f, pos.z), Quaternion.Euler(0f, Time.unscaledTime * 90f, 0f));
        }

        private ParticleSystem MakeSystem(string name, Material mat, Mesh mesh, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 3000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, mesh != null ? AnimationCurve.Linear(0, 1, 1, 0.2f) : new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1, 1.3f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(mesh != null ? 1f : 0.6f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            if (mesh != null)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                main.startRotation3D = true;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            if (mesh != null)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = mesh;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
            return ps;
        }

        public void Burst(Vector3 pos, Color color, int count, float speed, float size, FxKind kind)
        {
            var ps = kind == FxKind.Crumb ? crumbs : kind == FxKind.Puff ? puffs : sparks;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Random.insideUnitSphere * 0.2f;
                var v = Random.insideUnitSphere * speed;
                if (kind == FxKind.Crumb) v.y = Mathf.Abs(v.y) + speed * 0.5f;
                ep.velocity = v;
                ep.startSize = size * Random.Range(0.7f, 1.3f);
                ep.startLifetime = kind == FxKind.Crumb ? Random.Range(0.4f, 0.8f) : Random.Range(0.4f, 0.7f);
                ep.startColor = color;
                if (kind == FxKind.Crumb) ep.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, 0f);
                ps.Emit(ep, 1);
            }
        }

        public void Clear()
        {
            crumbs.Clear(); puffs.Clear(); sparks.Clear();
            foreach (var f in active) { f.go.SetActive(false); pools[f.key].Push(f); }
            active.Clear();
            aura.SetActive(false);
            reticle.SetActive(false);
            beamOuter.SetActive(false);
            beamCore.SetActive(false);
        }

        // ---------------- flat shapes ----------------

        private Mesh Mesh(string key)
        {
            if (meshes.TryGetValue(key, out var m)) return m;
            if (key == "disc") m = MeshKit.Sector(0f, 1f, 360f, 48, key);
            else if (key == "lane") m = MeshKit.Lane(key);
            else if (key.StartsWith("ring"))
            {
                float inner = int.Parse(key.Substring(4)) / 100f;
                m = MeshKit.Sector(inner, 1f, 360f, 56, key);
            }
            else if (key.StartsWith("sector"))
            {
                float angle = int.Parse(key.Substring(6));
                m = MeshKit.Sector(0f, 1f, angle, 32, key);
            }
            else if (key.StartsWith("arc"))
            {
                float angle = int.Parse(key.Substring(3));
                m = MeshKit.Sector(0.72f, 1f, angle, 32, key);
            }
            meshes[key] = m;
            return m;
        }

        private Flat Get(string key, bool additive)
        {
            string poolKey = key + (additive ? "+" : "");
            if (!pools.TryGetValue(poolKey, out var pool)) pools[poolKey] = pool = new Stack<Flat>();
            Flat f;
            if (pool.Count > 0) f = pool.Pop();
            else
            {
                f = new Flat { key = poolKey };
                f.go = Models.Static(Mesh(key), root, key, additive ? Mats.FxAdd : Mats.Telegraph, false);
                f.tr = f.go.transform;
                f.mr = f.go.GetComponent<MeshRenderer>();
            }
            f.t = 0f;
            f.go.SetActive(true);
            active.Add(f);
            return f;
        }

        public void Ring(Vector3 pos, float radius, Color color, float duration, float width)
        {
            float ratio = Mathf.Clamp(1f - width / Mathf.Max(radius, 0.1f), 0.5f, 0.95f);
            int q = Mathf.RoundToInt(ratio * 20f) * 5;
            var f = Get("ring" + q, true);
            f.mode = 0; f.pos = pos; f.radius = radius; f.duration = duration; f.color = color;
            f.tr.SetPositionAndRotation(pos, Quaternion.identity);
            Update1(f);
        }

        public void Snap(Vector3 origin, Vector3 dir, float radius, float angle, bool evolved)
        {
            int a = Mathf.RoundToInt(angle / 10f) * 10;
            var f = Get("sector" + a, true);
            f.mode = 1; f.pos = origin + Vector3.up * 0.08f; f.radius = radius; f.duration = 0.28f;
            f.color = evolved ? new Color(1f, 0.55f, 0.2f, 0.85f) : new Color(1f, 0.85f, 0.4f, 0.7f);
            f.tr.SetPositionAndRotation(f.pos, Quaternion.LookRotation(dir));
            Update1(f);
            var arc = Get("arc" + a, true);
            arc.mode = 1; arc.pos = f.pos + Vector3.up * 0.02f; arc.radius = radius; arc.duration = 0.34f;
            arc.color = new Color(1f, 0.95f, 0.7f, 0.95f);
            arc.tr.SetPositionAndRotation(arc.pos, Quaternion.LookRotation(dir));
            Update1(arc);
            // Pasta shards spray into the cone.
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 10; i++)
            {
                var d = Quaternion.Euler(0, Random.Range(-angle * 0.5f, angle * 0.5f), 0) * dir;
                ep.position = origin + Vector3.up * 1.1f + dir * 0.5f;
                ep.velocity = d * Random.Range(6f, 12f) + Vector3.up * Random.Range(1f, 4f);
                ep.startSize = Random.Range(0.1f, 0.18f);
                ep.startLifetime = Random.Range(0.35f, 0.6f);
                ep.startColor = Models.PastaGold;
                ep.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, 0);
                crumbs.Emit(ep, 1);
            }
            pokiCooldown -= 1f;
            if (pokiCooldown <= 0f)
            {
                pokiCooldown = 3f;
                if (G.Hud != null) G.Hud.Floating.Shout(origin + Vector3.up * 2.6f, "ポキッ!", new Color(1f, 0.9f, 0.4f), 1.1f);
            }
        }

        public void Slam(Vector3 center, float radius)
        {
            Ring(center + Vector3.up * 0.08f, radius, new Color(1f, 0.8f, 0.4f, 0.85f), 0.4f, radius * 0.25f);
            var disc = Get("disc", true);
            disc.mode = 0; disc.pos = center + Vector3.up * 0.06f; disc.radius = radius; disc.duration = 0.25f;
            disc.color = new Color(1f, 0.9f, 0.6f, 0.35f);
            disc.tr.SetPositionAndRotation(disc.pos, Quaternion.identity);
            Update1(disc);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                var p = center + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * radius * 0.85f;
                Burst(p, new Color(0.85f, 0.78f, 0.65f, 0.7f), 1, 1.2f, 1.2f, FxKind.Puff);
            }
            Burst(center + Vector3.up * 0.5f, Models.PastaGold, 12, 7f, 0.25f, FxKind.Crumb);
        }

        public void Sweep(Vector3 pos, Vector3 dir, float radius, float angle, Color color)
        {
            int a = Mathf.RoundToInt(angle / 10f) * 10;
            var f = Get("arc" + a, true);
            f.mode = 4; f.pos = pos + Vector3.up * 0.1f; f.radius = radius; f.duration = 0.25f; f.color = color;
            f.tr.SetPositionAndRotation(f.pos, Quaternion.LookRotation(dir));
            Update1(f);
        }

        public void Telegraph(TeleShape shape, Vector3 pos, Vector3 dir, float size, float widthOrAngle, float duration)
        {
            string key = shape == TeleShape.Lane ? "lane" : shape == TeleShape.Circle ? "disc" : "sector" + Mathf.RoundToInt(widthOrAngle / 10f) * 10;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            var rot = Quaternion.LookRotation(dir);
            var basePos = new Vector3(pos.x, 0.07f, pos.z);
            for (int layer = 0; layer < 2; layer++)
            {
                var f = Get(key, false);
                f.mode = layer == 0 ? 2 : 3;
                f.shape = shape; f.pos = basePos + Vector3.up * (layer * 0.01f); f.dir = dir;
                f.duration = duration; f.length = size; f.width = widthOrAngle;
                f.color = layer == 0 ? new Color(1f, 0.15f, 0.1f, 0.22f) : new Color(1f, 0.25f, 0.1f, 0.4f);
                f.tr.SetPositionAndRotation(f.pos, rot);
                Update1(f);
            }
        }

        public void Aura(Vector3 pos, float radius, bool evolved)
        {
            if (radius <= 0f) { aura.SetActive(false); return; }
            if (!aura.activeSelf) aura.SetActive(true);
            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.03f;
            aura.transform.SetPositionAndRotation(new Vector3(pos.x, 0.05f, pos.z), Quaternion.Euler(0, Time.time * 30f, 0));
            aura.transform.localScale = new Vector3(radius * pulse, 1f, radius * pulse);
            mpb.SetColor("_BaseColor", evolved ? new Color(1f, 0.92f, 0.55f, 0.32f) : new Color(1f, 0.97f, 0.85f, 0.22f));
            auraRenderer.SetPropertyBlock(mpb);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var f = active[i];
                f.t += dt;
                if (f.t >= f.duration)
                {
                    f.go.SetActive(false);
                    pools[f.key].Push(f);
                    active.RemoveAt(i);
                    continue;
                }
                Update1(f);
            }
        }

        private void Update1(Flat f)
        {
            float k = Mathf.Clamp01(f.t / Mathf.Max(0.001f, f.duration));
            Color c = f.color;
            Vector3 scale;
            switch (f.mode)
            {
                case 0:
                    {
                        float grow = 1f - (1f - k) * (1f - k);
                        float r = Mathf.Lerp(f.radius * 0.35f, f.radius, grow);
                        scale = new Vector3(r, 1f, r);
                        c.a *= 1f - k;
                        break;
                    }
                case 1:
                    {
                        float grow = 1f - Mathf.Pow(1f - k, 3f);
                        float r = Mathf.Lerp(f.radius * 0.3f, f.radius, grow);
                        scale = new Vector3(r, 1f, r);
                        c.a *= 1f - k * k;
                        break;
                    }
                case 4:
                    scale = new Vector3(f.radius, 1f, f.radius);
                    c.a *= 1f - k;
                    break;
                default:
                    {
                        float fill = f.mode == 3 ? k : 1f;
                        if (f.shape == TeleShape.Lane) scale = new Vector3(f.width, 1f, f.length * fill);
                        else scale = new Vector3(f.length * fill, 1f, f.length * fill);
                        if (f.mode == 3) c.a *= 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(f.t * 20f));
                        break;
                    }
            }
            f.tr.localScale = scale;
            mpb.SetColor("_BaseColor", c);
            f.mr.SetPropertyBlock(mpb);
        }

        // ---------------- text ----------------

        public void Number(Vector3 pos, float value, bool big, bool playerHurt = false, bool heal = false)
        {
            if (G.Hud == null) return;
            G.Hud.Floating.Number(pos, value, big, playerHurt, heal);
        }

        public void Shout(Vector3 pos, string text, Color color)
        {
            if (G.Hud == null) return;
            G.Hud.Floating.Shout(pos, text, color, 1f);
        }

        public void Banner(string text, Color color)
        {
            if (G.Hud == null) return;
            G.Hud.Banner(text, color);
        }
    }
}
