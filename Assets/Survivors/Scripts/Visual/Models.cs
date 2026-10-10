using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>
    /// Instance of an animated model. The torso bobs (and vehicles lean) from the ground; the spine leans, sways and twists
    /// from the hips and carries the arms, head and belly; legs swing from the hips.
    /// </summary>
    public class Rig
    {
        public Transform root, torso, spine, head, belly, prop, legL, legR, armL, armR, held, crank;
        public Transform[] wheels;
        public Renderer[] renderers;
        public float armRest, wheelRadius, wheelAngle;
        public Vector3 restL, restR, bellyPos;
        public Quaternion propBase = Quaternion.identity;
        public PropMotion propMotion;
        public Gait gait = Gait.Plain;
        public bool seated;
        public bool freeHandL, freeHandR;
    }

    public enum PropMotion { None, Wave, Spin, Swing }
    public enum PropMount { Spine, ArmL, ArmR }

    /// <summary>How a character moves; every type walks differently. Angles in degrees, distances in metres before scale.</summary>
    public class Gait
    {
        public float cadence = 1f, stride = 34f, bob = 0.07f, armSwing = 28f, swingL = 1f, swingR = 1f;
        /// <summary>Upper body: forward lean (negative leans back), hip sway and shoulder twist per step, arms held out.</summary>
        public float lean, sway, twist, armOut;
        /// <summary>Legs: knees out (bow legs) and toes out.</summary>
        public float legOut, toeOut;
        /// <summary>Head: static pitch (negative = chin up), nod per step, disapproving shakes, singing roll, steering.</summary>
        public float headPitch, headNod = 2f, headShake, headRoll, headSteer;
        /// <summary>Belly wobble that lags each step, and a vehicle's side-to-side weave.</summary>
        public float belly, roll;

        public static readonly Gait Plain = new Gait();

        public Gait Clone() => (Gait)MemberwiseClone();
    }

    public class RigMeshes
    {
        public Mesh body, leg, armL, armR, head, belly, prop;
        /// <summary>Spine pivot height. Shoulders, head, belly and props are relative to the spine.</summary>
        public float spineY;
        public float hipX = 0.13f, hipY = 0.78f, shoulderX = 0.31f, shoulderY = 1.3f, shoulderZ, armRest;
        public Vector3 headPos, bellyPos, propPos;
        public Quaternion propRot = Quaternion.identity;
        public PropMount propMount;
        public PropMotion propMotion;
        /// <summary>Arm rest poses (Euler); default hangs at armRest.</summary>
        public Vector3? restL, restR;
        public Gait gait;
        public bool seated;
        public bool freeHandL, freeHandR;
        /// <summary>Optional wheels (origin at the hub) that roll with the ground speed. They ride on the torso so the body and wheels bounce together.</summary>
        public Mesh wheel;
        public Vector3[] wheelPos;
        public float wheelRadius;
        /// <summary>Optional pedal crank (origin at the bottom bracket) turning with the wheels.</summary>
        public Mesh crank;
        public Vector3 crankPos;
    }

    public static partial class Models
    {
        private static readonly Dictionary<(EnemyKind kind, int variant), RigMeshes> enemyMeshes = new Dictionary<(EnemyKind, int), RigMeshes>();
        private static readonly Dictionary<ShotKind, Mesh> shotMeshes = new Dictionary<ShotKind, Mesh>();
        private static readonly Dictionary<string, Mesh> misc = new Dictionary<string, Mesh>();
        private static readonly MeshKit kit = new MeshKit();

        public static readonly Color Skin = new Color(0.96f, 0.78f, 0.62f);
        public static readonly Color Tan = new Color(0.86f, 0.63f, 0.47f);
        public static readonly Color PastaGold = new Color(0.98f, 0.78f, 0.36f);
        public static readonly Color Tomato = new Color(0.86f, 0.16f, 0.1f);
        public static readonly Color Basil = new Color(0.22f, 0.6f, 0.2f);
        public static readonly Color Azzurro = new Color(0.12f, 0.38f, 0.86f);
        /// <summary>Dried durum-wheat pasta: the body, the paler cut faces and ridges, and the shadow inside a tube.</summary>
        public static readonly Color Semolina = new Color(0.95f, 0.79f, 0.46f), SemolinaPale = new Color(1f, 0.9f, 0.64f), SemolinaShadow = new Color(0.58f, 0.39f, 0.16f);
        public static readonly Color Mayo = new Color(1f, 0.93f, 0.68f), WhippedCream = new Color(1f, 0.98f, 0.94f), Caramel = new Color(0.8f, 0.5f, 0.18f);
        public static readonly Color FrappeBlend = new Color(0.86f, 0.73f, 0.58f), FrappeCoffee = new Color(0.62f, 0.44f, 0.3f);
        /// <summary>Length of one turn of the Frappé Beam's cream spiral.</summary>
        public const float FrappePitch = 1.6f;

        // ---------------- rigs ----------------

        public static Rig Build(RigMeshes m, Transform parent, string name)
        {
            var rig = new Rig
            {
                armRest = m.armRest, seated = m.seated, freeHandL = m.freeHandL, freeHandR = m.freeHandR,
                gait = m.gait ?? Gait.Plain, propMotion = m.propMotion, propBase = m.propRot, bellyPos = m.bellyPos,
                restL = m.restL ?? new Vector3(m.armRest, 0f, m.seated ? 8f : -4f),
                restR = m.restR ?? new Vector3(m.armRest, 0f, m.seated ? -8f : 4f)
            };
            rig.root = new GameObject(name).transform;
            rig.root.SetParent(parent, false);
            var renderers = new List<Renderer>();
            rig.torso = Part("Torso", rig.root, Vector3.zero, null, renderers);
            rig.spine = Part("Spine", rig.torso, new Vector3(0f, m.spineY, 0f), m.body, renderers);
            if (m.leg != null)
            {
                rig.legL = Part("LegL", rig.root, new Vector3(-m.hipX, m.hipY, 0), m.leg, renderers);
                rig.legR = Part("LegR", rig.root, new Vector3(m.hipX, m.hipY, 0), m.leg, renderers);
            }
            rig.armL = Part("ArmL", rig.spine, new Vector3(-m.shoulderX, m.shoulderY, m.shoulderZ), m.armL, renderers);
            rig.armR = Part("ArmR", rig.spine, new Vector3(m.shoulderX, m.shoulderY, m.shoulderZ), m.armR, renderers);
            if (m.head != null) rig.head = Part("Head", rig.spine, m.headPos, m.head, renderers);
            if (m.belly != null) rig.belly = Part("Belly", rig.spine, m.bellyPos, m.belly, renderers);
            if (m.prop != null)
            {
                var mount = m.propMount == PropMount.ArmL ? rig.armL : m.propMount == PropMount.ArmR ? rig.armR : rig.spine;
                rig.prop = Part("Prop", mount, m.propPos, m.prop, renderers);
                rig.prop.localRotation = m.propRot;
            }
            if (m.wheel != null)
            {
                rig.wheels = new Transform[m.wheelPos.Length];
                for (int i = 0; i < m.wheelPos.Length; i++) rig.wheels[i] = Part("Wheel" + i, rig.torso, m.wheelPos[i], m.wheel, renderers);
                rig.wheelRadius = m.wheelRadius;
            }
            if (m.crank != null) rig.crank = Part("Crank", rig.torso, m.crankPos, m.crank, renderers);
            rig.renderers = renderers.ToArray();
            return rig;
        }

        private static Transform Part(string name, Transform parent, Vector3 pos, Mesh mesh, List<Renderer> renderers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            if (mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Mats.Lit;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderers.Add(r);
            }
            return go.transform;
        }

        public static GameObject Static(Mesh mesh, Transform parent, string name, Material material = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material != null ? material : Mats.Lit;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        /// <summary>Ordinary Italians (including elite Nonna and scooter riders) each have four cosmetic appearances.</summary>
        public static int EnemyVariantCount(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Signore:
                case EnemyKind.Tifoso:
                case EnemyKind.Mamma:
                case EnemyKind.Chef:
                case EnemyKind.Vespista:
                case EnemyKind.Gondoliere:
                case EnemyKind.Pizzaiolo:
                case EnemyKind.Mafioso:
                case EnemyKind.Nonna:
                    return 4;
                default:
                    return 1;
            }
        }

        private static RigMeshes Barrel()
        {
            kit.Clear();
            var wood = new Color(0.55f, 0.32f, 0.16f);
            kit.Cone(new Vector3(0, 0.25f, 0), new Vector3(0.8f, 0.5f, 0.8f), wood, default, 1.18f, 14);
            kit.Cone(new Vector3(0, 0.75f, 0), new Vector3(0.94f, 0.5f, 0.94f), wood, default, 0.85f, 14);
            var band = new Color(0.25f, 0.25f, 0.27f);
            kit.Cyl(new Vector3(0, 0.12f, 0), new Vector3(0.84f, 0.06f, 0.84f), band, default, 14);
            kit.Cyl(new Vector3(0, 0.5f, 0), new Vector3(0.96f, 0.06f, 0.96f), band, default, 14);
            kit.Cyl(new Vector3(0, 0.88f, 0), new Vector3(0.84f, 0.06f, 0.84f), band, default, 14);
            kit.Cyl(new Vector3(0, 1.0f, 0), new Vector3(0.78f, 0.02f, 0.78f), new Color(0.45f, 0.08f, 0.12f), default, 14);
            return new RigMeshes { body = kit.ToMesh("Barrel"), leg = null, armL = null, armR = null };
        }

        // ---------------- projectiles & pickups ----------------

        public static Mesh Shot(ShotKind kind)
        {
            if (shotMeshes.TryGetValue(kind, out var mesh)) return mesh;
            kit.Clear();
            switch (kind)
            {
                case ShotKind.Penne:
                    {
                        // Penne rigate: a ridged tube, both ends cut on the same diagonal, hollow inside.
                        const float length = 0.6f, diameter = 0.17f;
                        var bias = Matrix4x4.identity;
                        bias.m21 = 0.85f; // z += 0.85 y slants both ends like the real cut
                        kit.Frame = bias;
                        kit.Cyl(Vector3.zero, new Vector3(diameter, length, diameter), Semolina, new Vector3(90, 0, 0), 16);
                        for (int i = 0; i < 14; i++)
                        {
                            float a = i * 360f / 14f;
                            kit.Box(Quaternion.Euler(0, 0, a) * Vector3.right * (diameter * 0.5f - 0.003f), new Vector3(0.016f, 0.014f, length), SemolinaPale, new Vector3(0, 0, a));
                        }
                        // Pale cut faces around a shadowed bore.
                        kit.Cyl(Vector3.zero, new Vector3(diameter - 0.012f, length + 0.004f, diameter - 0.012f), SemolinaPale, new Vector3(90, 0, 0), 16);
                        kit.Cyl(Vector3.zero, new Vector3(diameter * 0.62f, length + 0.008f, diameter * 0.62f), SemolinaShadow, new Vector3(90, 0, 0), 14);
                        kit.Frame = Matrix4x4.identity;
                        break;
                    }
                case ShotKind.Fusilli:
                    {
                        // Fusilli: three blades twisted two and a half turns around a thin core, with rounded edges.
                        const float length = 0.8f, radius = 0.13f, core = 0.025f, turns = 2.5f;
                        const int steps = 36;
                        kit.Cyl(Vector3.zero, new Vector3(core * 2.4f, length * 0.96f, core * 2.4f), Semolina, new Vector3(90, 0, 0), 8);
                        for (int blade = 0; blade < 3; blade++)
                        {
                            var grid = new Vector3[steps + 1, 3];
                            Vector3 prev = default;
                            for (int i = 0; i <= steps; i++)
                            {
                                float t = (float)i / steps;
                                float a = blade * Mathf.PI * 2f / 3f + t * turns * Mathf.PI * 2f;
                                float r = radius * Mathf.Lerp(0.78f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) / 0.08f));
                                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                                var along = Vector3.forward * (t - 0.5f) * length;
                                for (int j = 0; j < 3; j++) grid[i, j] = dir * Mathf.Lerp(core, r, j / 2f) + along;
                                var edge = dir * r + along;
                                if (i > 0) kit.Taper(prev, edge, 0.04f, 0.04f, SemolinaPale, 6);
                                prev = edge;
                            }
                            Sheet(grid, 0.03f, Semolina);
                        }
                        break;
                    }
                case ShotKind.Farfalle:
                    {
                        // Farfalle: a rectangle pinched into a bow; pleated, gently cupped wings with pinked ends.
                        const int nu = 17, nv = 9;
                        const float half = 0.42f, depth = 0.25f, pinch = 0.07f;
                        var grid = new Vector3[nu, nv];
                        for (int i = 0; i < nu; i++)
                            for (int j = 0; j < nv; j++)
                            {
                                float u = (float)i / (nu - 1) * 2f - 1f, v = (float)j / (nv - 1) * 2f - 1f;
                                float au = Mathf.Abs(u), near = 1f - au;
                                float y = 0.07f * near * near * v * v + 0.012f * Mathf.Sin(v * Mathf.PI * 3f) * near + 0.035f * au * au;
                                grid[i, j] = new Vector3(u * half, y, v * Mathf.Lerp(pinch, depth, Mathf.Sqrt(au)));
                            }
                        Sheet(grid, 0.022f, Semolina);
                        for (int side = -1; side <= 1; side += 2)
                            for (int k = 0; k < 6; k++)
                                kit.Box(new Vector3(side * (half + 0.006f), 0.035f, ((k + 0.5f) / 6f * 2f - 1f) * depth), new Vector3(0.03f, 0.02f, 0.03f), SemolinaPale, new Vector3(0, 45, 0));
                        // The pressed knot in the middle.
                        kit.Ball(new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.07f, 2f * pinch + 0.05f), SemolinaPale);
                        for (int k = -1; k <= 1; k += 2) kit.Box(new Vector3(k * 0.03f, 0.055f, 0f), new Vector3(0.012f, 0.03f, 2f * pinch + 0.02f), Semolina);
                        break;
                    }
                case ShotKind.Ketchup:
                    kit.Cyl(Vector3.zero, new Vector3(0.2f, 0.34f, 0.2f), Tomato);
                    kit.Cone(new Vector3(0, 0.22f, 0), new Vector3(0.2f, 0.12f, 0.2f), Tomato, default, 0.3f);
                    kit.Cyl(new Vector3(0, 0.3f, 0), new Vector3(0.08f, 0.06f, 0.08f), Color.white);
                    kit.Box(new Vector3(0, 0, 0.1f), new Vector3(0.14f, 0.12f, 0.02f), Color.white);
                    break;
                case ShotKind.Pizza:
                    kit.Cyl(Vector3.zero, new Vector3(1f, 0.06f, 1f), new Color(0.9f, 0.68f, 0.35f), default, 16);
                    kit.Cyl(new Vector3(0, 0.02f, 0), new Vector3(0.84f, 0.05f, 0.84f), Tomato, default, 16);
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 2.4f, r = 0.12f + (i % 3) * 0.1f;
                        kit.Ball(new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r), new Vector3(0.13f, 0.04f, 0.13f), new Color(1f, 0.93f, 0.6f));
                        kit.Box(new Vector3(Mathf.Cos(a + 1.2f) * r * 1.2f, 0.07f, Mathf.Sin(a + 1.2f) * r * 1.2f), new Vector3(0.08f, 0.05f, 0.08f), new Color(1f, 0.85f, 0.1f), new Vector3(0, a * 40f, 0));
                    }
                    break;
                case ShotKind.Slipper:
                    kit.Box(Vector3.zero, new Vector3(0.18f, 0.05f, 0.4f), new Color(0.95f, 0.45f, 0.62f));
                    kit.Box(new Vector3(0, 0.05f, 0.07f), new Vector3(0.19f, 0.07f, 0.1f), new Color(0.3f, 0.55f, 0.9f));
                    break;
                case ShotKind.Dough:
                    kit.Cyl(Vector3.zero, new Vector3(0.7f, 0.05f, 0.7f), new Color(0.98f, 0.93f, 0.82f), default, 14);
                    kit.Cyl(new Vector3(0, 0.02f, 0), new Vector3(0.55f, 0.04f, 0.55f), new Color(0.92f, 0.3f, 0.2f), default, 14);
                    break;
                case ShotKind.Meatball:
                    kit.Ball(Vector3.zero, 0.42f, new Color(0.45f, 0.24f, 0.14f));
                    kit.Ball(new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.2f, 0.3f), Tomato);
                    break;
                case ShotKind.Bazooka:
                    {
                        // A fat bundle of dried spaghetti, ends a little uneven, tied with red kitchen string.
                        var rng = new System.Random(7);
                        for (int i = 0; i < 37; i++)
                        {
                            float r = Mathf.Sqrt((i + 0.5f) / 37f) * 0.15f, a = i * 2.39996f;
                            float shift = ((float)rng.NextDouble() - 0.5f) * 0.08f;
                            kit.Cyl(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, shift), new Vector3(0.04f, 1.1f, 0.04f),
                                Color.Lerp(Semolina, SemolinaPale, (float)rng.NextDouble() * 0.6f), new Vector3(90, 0, 0), 6);
                        }
                        foreach (float z in new[] { -0.3f, 0.25f })
                            kit.Cyl(new Vector3(0, 0, z), new Vector3(0.36f, 0.035f, 0.36f), new Color(0.78f, 0.14f, 0.12f), new Vector3(90, 0, 0), 12);
                        break;
                    }
                case ShotKind.Drop:
                    kit.Ball(Vector3.zero, 0.26f, Tomato);
                    break;
                case ShotKind.Parmesan:
                    kit.Cyl(Vector3.zero, new Vector3(0.26f, 0.4f, 0.26f), new Color(0.2f, 0.55f, 0.3f), default, 10);
                    kit.Cyl(new Vector3(0, 0.22f, 0), new Vector3(0.27f, 0.06f, 0.27f), new Color(1f, 0.85f, 0.3f), default, 10);
                    kit.Box(new Vector3(0, 0.02f, 0.13f), new Vector3(0.18f, 0.16f, 0.02f), Color.white);
                    break;
                case ShotKind.Pineapple:
                    kit.Box(Vector3.zero, new Vector3(0.22f, 0.16f, 0.22f), new Color(1f, 0.85f, 0.1f), new Vector3(0, 45, 0));
                    break;
                default:
                    kit.Box(Vector3.zero, Vector3.one * 0.3f, Color.white);
                    break;
            }
            mesh = kit.ToMesh("Shot " + kind);
            shotMeshes[kind] = mesh;
            return mesh;
        }

        public static Mesh Get(string key)
        {
            if (misc.TryGetValue(key, out var mesh)) return mesh;
            kit.Clear();
            switch (key)
            {
                case "gem1": Gem(new Color(1f, 0.82f, 0.3f), 0.32f); break;
                case "gem2": Gem(new Color(0.35f, 0.9f, 0.35f), 0.4f); break;
                case "gem3": Gem(new Color(1f, 0.3f, 0.25f), 0.5f); break;
                case "gem4": Gem(new Color(0.75f, 0.45f, 1f), 0.62f); break;
                case "coin":
                    kit.Cyl(Vector3.zero, new Vector3(0.42f, 0.06f, 0.42f), new Color(1f, 0.8f, 0.2f), new Vector3(90, 0, 0), 14);
                    kit.Box(new Vector3(0, 0, 0.035f), new Vector3(0.18f, 0.04f, 0.02f), new Color(0.8f, 0.55f, 0.1f));
                    kit.Box(new Vector3(0, 0, -0.035f), new Vector3(0.18f, 0.04f, 0.02f), new Color(0.8f, 0.55f, 0.1f));
                    break;
                case "coinbag":
                    kit.Ball(Vector3.zero, new Vector3(0.6f, 0.55f, 0.6f), new Color(0.6f, 0.42f, 0.22f));
                    kit.Cone(new Vector3(0, 0.33f, 0), new Vector3(0.3f, 0.2f, 0.3f), new Color(0.6f, 0.42f, 0.22f), new Vector3(180, 0, 0), 0.4f);
                    kit.Cyl(new Vector3(0, 0.25f, 0), new Vector3(0.24f, 0.05f, 0.24f), new Color(1f, 0.8f, 0.2f));
                    kit.Box(new Vector3(0, 0, 0.29f), new Vector3(0.16f, 0.2f, 0.02f), new Color(1f, 0.8f, 0.2f));
                    break;
                case "pizzaslice":
                    kit.Cone(Vector3.zero, new Vector3(0.75f, 0.1f, 0.75f), new Color(0.92f, 0.7f, 0.38f), default, 1f, 3);
                    kit.Cone(new Vector3(0, 0.04f, 0), new Vector3(0.62f, 0.08f, 0.62f), Tomato, default, 1f, 3);
                    kit.Ball(new Vector3(0.05f, 0.1f, 0.05f), new Vector3(0.12f, 0.05f, 0.12f), new Color(1f, 0.95f, 0.7f));
                    kit.Ball(new Vector3(-0.1f, 0.1f, -0.02f), new Vector3(0.1f, 0.05f, 0.1f), new Color(1f, 0.95f, 0.7f));
                    kit.Ball(new Vector3(0.02f, 0.1f, -0.12f), new Vector3(0.06f, 0.03f, 0.1f), Basil);
                    break;
                case "fork":
                    kit.Box(new Vector3(0, 0, -0.2f), new Vector3(0.08f, 0.04f, 0.5f), new Color(0.85f, 0.87f, 0.9f));
                    kit.Box(new Vector3(0, 0, 0.1f), new Vector3(0.22f, 0.04f, 0.1f), new Color(0.85f, 0.87f, 0.9f));
                    for (int i = 0; i < 4; i++) kit.Box(new Vector3(-0.09f + i * 0.06f, 0, 0.25f), new Vector3(0.03f, 0.03f, 0.22f), new Color(0.85f, 0.87f, 0.9f));
                    break;
                case "rosary":
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        kit.Ball(new Vector3(Mathf.Cos(a) * 0.25f, 0, Mathf.Sin(a) * 0.25f + 0.12f), 0.08f, new Color(0.95f, 0.9f, 0.8f));
                    }
                    kit.Box(new Vector3(0, 0, -0.25f), new Vector3(0.06f, 0.05f, 0.3f), new Color(1f, 0.82f, 0.3f));
                    kit.Box(new Vector3(0, 0, -0.2f), new Vector3(0.2f, 0.05f, 0.06f), new Color(1f, 0.82f, 0.3f));
                    break;
                case "chest":
                    {
                        var wood = new Color(0.6f, 0.34f, 0.16f); var gold = new Color(1f, 0.8f, 0.25f);
                        kit.Box(new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), wood);
                        kit.Cyl(new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0.9f, 0.6f), wood, new Vector3(0, 0, 90), 12);
                        kit.Box(new Vector3(-0.3f, 0.35f, 0), new Vector3(0.08f, 0.72f, 0.64f), gold);
                        kit.Box(new Vector3(0.3f, 0.35f, 0), new Vector3(0.08f, 0.72f, 0.64f), gold);
                        kit.Box(new Vector3(0, 0.45f, 0.31f), new Vector3(0.16f, 0.2f, 0.04f), gold);
                        break;
                    }
                case "carbonara":
                    Carbonara();
                    break;
                case "car":
                    CarGeometry(kit, Matrix4x4.identity, new Color(0.85f, 0.18f, 0.15f));
                    break;
                case "sprinkler":
                    kit.Cyl(new Vector3(0, 0.1f, 0), new Vector3(0.9f, 0.2f, 0.9f), new Color(0.3f, 0.3f, 0.32f), default, 12);
                    kit.Cyl(new Vector3(0, 0.55f, 0), new Vector3(0.45f, 0.8f, 0.45f), Tomato, default, 12);
                    kit.Cone(new Vector3(0, 1.05f, 0), new Vector3(0.45f, 0.25f, 0.45f), Tomato, default, 0.3f, 12);
                    kit.Cyl(new Vector3(0, 1.22f, 0), new Vector3(0.14f, 0.1f, 0.14f), Color.white, default, 8);
                    kit.Box(new Vector3(0, 0.6f, 0.23f), new Vector3(0.3f, 0.25f, 0.02f), Color.white);
                    for (int i = 0; i < 4; i++) kit.Box(Quaternion.Euler(0, i * 90, 0) * new Vector3(0, 1.25f, 0.2f), new Vector3(0.06f, 0.06f, 0.3f), new Color(0.75f, 0.75f, 0.78f), new Vector3(0, i * 90, 0));
                    break;
                case "special_Bazooka":
                    Reuse(Shot(ShotKind.Bazooka), Matrix4x4.Rotate(Quaternion.Euler(0, 0, 25)) * Matrix4x4.Scale(Vector3.one * 1.2f));
                    break;
                case "special_MayoJet":
                    {
                        // A squeezed bottle of Japanese-style mayonnaise with a red star-nozzle cap (no brand).
                        var red = new Color(0.86f, 0.12f, 0.12f);
                        kit.Cyl(Vector3.zero, new Vector3(0.3f, 0.5f, 0.3f), Mayo, default, 16);
                        kit.Ball(new Vector3(0f, -0.25f, 0f), new Vector3(0.3f, 0.08f, 0.3f), Mayo);
                        kit.Ball(new Vector3(0f, -0.06f, 0f), new Vector3(0.35f, 0.2f, 0.25f), Mayo); // squeezed in the middle
                        kit.Cone(new Vector3(0f, 0.3f, 0f), new Vector3(0.3f, 0.1f, 0.3f), Mayo, default, 0.45f, 16);
                        kit.Cyl(new Vector3(0f, 0.1f, 0f), new Vector3(0.31f, 0.14f, 0.31f), new Color(0.98f, 0.97f, 0.94f), default, 16);
                        kit.Cyl(new Vector3(0f, 0.1f, 0f), new Vector3(0.315f, 0.03f, 0.315f), red, default, 16);
                        kit.Cyl(new Vector3(0f, 0.39f, 0f), new Vector3(0.16f, 0.1f, 0.16f), red, default, 12);
                        kit.Cone(new Vector3(0f, 0.48f, 0f), new Vector3(0.1f, 0.08f, 0.1f), red, default, 0.5f, 5);
                        kit.Dot(new Vector3(0f, 0.54f, 0f), 0.06f, Mayo);
                        break;
                    }
                case "special_Parmesan":
                    Reuse(Shot(ShotKind.Parmesan), Matrix4x4.Scale(Vector3.one * 1.6f));
                    break;
                case "special_Sprinkler":
                    Reuse(Get("sprinkler"), Matrix4x4.Scale(Vector3.one * 0.6f) * Matrix4x4.Translate(new Vector3(0, -0.6f, 0)));
                    break;
                case "special_SugarEspresso":
                    kit.Cyl(new Vector3(0, 0, 0), new Vector3(0.35f, 0.3f, 0.35f), Color.white, default, 12);
                    kit.Cyl(new Vector3(0, 0.14f, 0), new Vector3(0.3f, 0.04f, 0.3f), new Color(0.3f, 0.18f, 0.1f), default, 12);
                    kit.Cyl(new Vector3(0, -0.16f, 0), new Vector3(0.6f, 0.04f, 0.6f), Color.white, default, 14);
                    for (int i = 0; i < 5; i++) kit.Box(new Vector3(-0.3f + i * 0.15f, 0.3f + (i % 2) * 0.1f, 0.1f), Vector3.one * 0.1f, Color.white, new Vector3(20 * i, 10 * i, 0));
                    break;
                case "special_FrappeBeam":
                    {
                        // A plain iced frappé: coffee settling darker at the bottom of a clear cup, caramel running down
                        // the inside, a whipped cream dome with caramel drizzle, and a wide green straw.
                        kit.Cone(new Vector3(0f, -0.2f, 0f), new Vector3(0.34f, 0.3f, 0.34f), FrappeCoffee, default, 1.13f, 16);
                        kit.Cone(new Vector3(0f, 0.15f, 0f), new Vector3(0.384f, 0.4f, 0.384f), FrappeBlend, default, 1.15f, 16);
                        for (int i = 0; i < 5; i++)
                        {
                            float a = (i * 72f + 20f) * Mathf.Deg2Rad;
                            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                            float bottom = 0.05f - 0.04f * i;
                            kit.Bar(d * 0.224f + Vector3.up * 0.33f, d * (0.174f + (bottom + 0.35f) * 0.073f) + Vector3.up * bottom, 0.016f, Caramel);
                        }
                        kit.Cyl(new Vector3(0f, 0.355f, 0f), new Vector3(0.46f, 0.02f, 0.46f), new Color(0.88f, 0.9f, 0.92f), default, 16);
                        kit.Ball(new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.3f, 0.42f), WhippedCream);
                        kit.Cone(new Vector3(0f, 0.6f, 0f), new Vector3(0.2f, 0.14f, 0.2f), WhippedCream, default, 0.15f, 10);
                        // Caramel drizzle criss-crossing the cream.
                        for (int pass = 0; pass < 2; pass++)
                        {
                            Vector3 prev = default;
                            for (int i = 0; i <= 6; i++)
                            {
                                float u = i / 6f * 0.32f - 0.16f, w = (i % 2 == 0 ? -0.06f : 0.06f);
                                float x = pass == 0 ? u : w, z = pass == 0 ? w : u;
                                float y = 0.42f + 0.15f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x * x + z * z) / (0.21f * 0.21f))) + 0.01f;
                                var p = new Vector3(x, y, z);
                                if (i > 0) kit.Bar(prev, p, 0.02f, Caramel);
                                prev = p;
                            }
                        }
                        kit.Bar(new Vector3(0.05f, 0.3f, 0.02f), new Vector3(0.12f, 0.92f, 0.04f), 0.06f, new Color(0.12f, 0.55f, 0.32f));
                        break;
                    }
                case "pedestal":
                    kit.Cyl(new Vector3(0, 0.15f, 0), new Vector3(1.8f, 0.3f, 1.8f), new Color(0.78f, 0.74f, 0.66f), default, 16);
                    kit.Cyl(new Vector3(0, 0.34f, 0), new Vector3(1.4f, 0.1f, 1.4f), new Color(1f, 0.82f, 0.3f), default, 16);
                    break;
                case "beam":
                    kit.Cyl(new Vector3(0, 6f, 0), new Vector3(0.9f, 12f, 0.9f), Color.white, default, 12);
                    break;
                case "arrow":
                    kit.Cone(Vector3.zero, new Vector3(0.6f, 0.05f, 0.9f), Color.white, new Vector3(90, 0, 0), 0f, 3);
                    break;
                case "pasta_bundle":
                    for (int i = 0; i < 12; i++)
                        kit.Cyl(new Vector3((i % 4 - 1.5f) * 0.02f, (i / 4 - 1f) * 0.02f, 0), new Vector3(0.015f, 0.9f, 0.015f), PastaGold, new Vector3(0, 0, 90), 5);
                    break;
                case "shard":
                    kit.Box(Vector3.zero, new Vector3(0.05f, 0.05f, 0.3f), PastaGold);
                    break;
                case "lasagna":
                    {
                        // A lasagna riccia sheet: flat in the middle, ruffled along both long edges.
                        const int nu = 49, nv = 11;
                        var grid = new Vector3[nu, nv];
                        for (int i = 0; i < nu; i++)
                            for (int j = 0; j < nv; j++)
                            {
                                float x = ((float)i / (nu - 1) - 0.5f) * 0.72f, z = ((float)j / (nv - 1) - 0.5f) * 0.36f;
                                float edge = Mathf.Pow(Mathf.Abs(z) / 0.18f, 5f);
                                grid[i, j] = new Vector3(x, Mathf.Sin(x * 70f) * 0.022f * edge, z);
                            }
                        Sheet(grid, 0.03f, Semolina);
                        break;
                    }
                default:
                    kit.Box(Vector3.zero, Vector3.one * 0.2f, Color.magenta);
                    break;
            }
            mesh = kit.ToMesh(key);
            misc[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// Spaghetti twirled into a nest and coated in cream sauce, with guanciale, pecorino and black pepper, plus the
        /// crime itself: a dollop of whipped cream on top.
        /// </summary>
        private static void Carbonara()
        {
            Lathe(new[] { new Vector2(0.02f, 0f), new Vector2(0.3f, 0f), new Vector2(0.4f, 0.05f), new Vector2(0.46f, 0.07f) }, 32, 0.02f, Color.white);
            kit.Cyl(Vector3.zero, new Vector3(0.06f, 0.02f, 0.06f), Color.white, default, 8);
            kit.Ball(new Vector3(0f, 0.03f, 0f), new Vector3(0.66f, 0.05f, 0.66f), new Color(1f, 0.96f, 0.84f));
            kit.Ball(new Vector3(0f, 0.06f, 0f), new Vector3(0.6f, 0.14f, 0.6f), new Color(1f, 0.92f, 0.68f));
            var rng = new System.Random(11);
            float R() => (float)rng.NextDouble();
            // Surface of the pasta mound at a distance from the middle.
            float Top(float r) => 0.06f + 0.07f * Mathf.Sqrt(Mathf.Max(0f, 1f - r * r / 0.09f));
            var coated = new Color(1f, 0.9f, 0.62f);
            for (int s = 0; s < 9; s++)
            {
                Vector3 prev = default;
                var tint = Color.Lerp(coated, Semolina, R() * 0.4f);
                for (int i = 0; i <= 26; i++)
                {
                    float t = i / 26f, a = s * 0.7f + t * Mathf.PI * 4.2f;
                    float r = Mathf.Lerp(0.27f, 0.04f, t) + 0.015f * Mathf.Sin(s * 3f + t * 9f);
                    var p = new Vector3(Mathf.Cos(a) * r, Top(r) + 0.012f + 0.01f * Mathf.Sin(a * 2f + s), Mathf.Sin(a) * r);
                    if (i > 0) kit.Taper(prev, p, 0.028f, 0.028f, tint, 5);
                    prev = p;
                }
            }
            Vector3 OnTop(float minR, float maxR)
            {
                float a = R() * Mathf.PI * 2f, r = Mathf.Lerp(minR, maxR, R());
                return new Vector3(Mathf.Cos(a) * r, Top(r) + 0.01f, Mathf.Sin(a) * r);
            }
            for (int i = 0; i < 7; i++)
            {
                var p = OnTop(0.06f, 0.24f);
                var yaw = new Vector3(0f, R() * 180f, 0f);
                kit.Box(p, new Vector3(0.07f, 0.035f, 0.05f), new Color(0.66f, 0.3f, 0.24f), yaw);
                kit.Box(p + Vector3.up * 0.024f, new Vector3(0.07f, 0.016f, 0.05f), new Color(0.98f, 0.9f, 0.82f), yaw);
            }
            for (int i = 0; i < 14; i++) kit.Dot(OnTop(0.02f, 0.25f), 0.022f, new Color(0.98f, 0.96f, 0.88f));
            for (int i = 0; i < 30; i++) kit.Dot(OnTop(0.02f, 0.27f), 0.014f, new Color(0.1f, 0.08f, 0.07f));
            kit.Ball(new Vector3(0.06f, 0.17f, -0.03f), new Vector3(0.17f, 0.1f, 0.17f), WhippedCream);
            kit.Cone(new Vector3(0.06f, 0.245f, -0.03f), new Vector3(0.14f, 0.1f, 0.14f), WhippedCream, default, 0.1f, 10);
        }

        /// <summary>One turn of the Frappé Beam's spiral along +Z: piped whipped cream, or a thin caramel drizzle opposite it.</summary>
        public static Mesh FrappeTurn(bool cream)
        {
            string key = cream ? "frappe_cream" : "frappe_caramel";
            if (misc.TryGetValue(key, out var mesh)) return mesh;
            kit.Clear();
            int steps = cream ? 12 : 16;
            float radius = cream ? 0.62f : 0.7f, phase = cream ? 0f : Mathf.PI;
            Vector3 At(float t) => new Vector3(Mathf.Cos(phase + t * Mathf.PI * 2f) * radius, Mathf.Sin(phase + t * Mathf.PI * 2f) * radius, t * FrappePitch);
            for (int i = 0; i < steps; i++)
            {
                float t0 = (float)i / steps, t1 = (float)(i + 1) / steps;
                if (cream) kit.Ball(At((t0 + t1) * 0.5f), 0.4f, WhippedCream);
                else kit.Taper(At(t0), At(t1), 0.08f, 0.08f, Caramel, 6);
            }
            mesh = kit.ToMesh(key);
            misc[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// A thin sheet through a grid of points (rows along u, columns along v), given a front and a back face
        /// <paramref name="thickness"/> apart so it reads from either side.
        /// </summary>
        private static void Sheet(Vector3[,] grid, float thickness, Color col)
        {
            int nu = grid.GetLength(0), nv = grid.GetLength(1);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int side = 0; side < 2; side++)
            {
                int b = v.Count;
                for (int i = 0; i < nu; i++)
                    for (int j = 0; j < nv; j++)
                    {
                        var du = grid[Mathf.Min(i + 1, nu - 1), j] - grid[Mathf.Max(i - 1, 0), j];
                        var dv = grid[i, Mathf.Min(j + 1, nv - 1)] - grid[i, Mathf.Max(j - 1, 0)];
                        var normal = Vector3.Cross(du, dv);
                        normal = (normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up) * (side == 0 ? 1f : -1f);
                        v.Add(grid[i, j] + normal * thickness * 0.5f);
                        n.Add(normal);
                        uv.Add(new Vector2((float)i / (nu - 1), (float)j / (nv - 1)));
                    }
                for (int i = 0; i < nu - 1; i++)
                    for (int j = 0; j < nv - 1; j++)
                    {
                        int a = b + i * nv + j;
                        Tri(v, n, t, a, a + nv, a + nv + 1);
                        Tri(v, n, t, a, a + nv + 1, a + 1);
                    }
            }
            kit.Raw(v, n, uv, t, col);
        }

        /// <summary>A surface of revolution around Y through (radius, height) profile points, visible from both sides.</summary>
        private static void Lathe(Vector2[] profile, int sides, float thickness, Color col)
        {
            var grid = new Vector3[sides + 1, profile.Length];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                for (int j = 0; j < profile.Length; j++)
                    grid[i, j] = new Vector3(Mathf.Cos(a) * profile[j].x, profile[j].y, Mathf.Sin(a) * profile[j].x);
            }
            Sheet(grid, thickness, col);
        }

        /// <summary>Adds a triangle wound so Unity treats the side its normals point to as the front.</summary>
        private static void Tri(List<Vector3> v, List<Vector3> n, List<int> t, int a, int b, int c)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), n[a] + n[b] + n[c]) < 0f) (b, c) = (c, b);
            t.Add(a); t.Add(b); t.Add(c);
        }

        /// <summary>
        /// Starts this model from another one. The other mesh is fetched first because building it reuses the shared kit,
        /// which would otherwise leave a second, untransformed copy behind.
        /// </summary>
        private static void Reuse(Mesh mesh, Matrix4x4 m)
        {
            var sub = SubKit(mesh);
            kit.Clear();
            kit.Add(sub, m);
        }

        /// <summary>Copies a finished mesh back into a kit so it can be re-transformed.</summary>
        private static MeshKit SubKit(Mesh mesh)
        {
            var sub = new MeshKit();
            var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var t = mesh.triangles; var c = mesh.colors;
            sub.RawLinear(v, n, uv, t, c);
            return sub;
        }

        /// <summary>A small Italian city car (think Fiat 500).</summary>
        public static void CarGeometry(MeshKit k, Matrix4x4 frame, Color body)
        {
            var old = k.Frame;
            k.Frame = frame;
            k.Box(new Vector3(0, 0.55f, 0), new Vector3(1.5f, 0.6f, 3f), body);
            k.Ball(new Vector3(0, 0.95f, -0.15f), new Vector3(1.4f, 0.9f, 1.9f), body);
            k.Box(new Vector3(0, 1.05f, 0.62f), new Vector3(1.2f, 0.4f, 0.05f), new Color(0.2f, 0.3f, 0.38f), new Vector3(-30, 0, 0));
            k.Box(new Vector3(0, 1.05f, -0.95f), new Vector3(1.2f, 0.36f, 0.05f), new Color(0.2f, 0.3f, 0.38f), new Vector3(30, 0, 0));
            k.Box(new Vector3(0.71f, 1f, -0.15f), new Vector3(0.05f, 0.35f, 1.2f), new Color(0.2f, 0.3f, 0.38f));
            k.Box(new Vector3(-0.71f, 1f, -0.15f), new Vector3(0.05f, 0.35f, 1.2f), new Color(0.2f, 0.3f, 0.38f));
            foreach (var w in new[] { new Vector3(0.65f, 0.3f, 1f), new Vector3(-0.65f, 0.3f, 1f), new Vector3(0.65f, 0.3f, -1f), new Vector3(-0.65f, 0.3f, -1f) })
                k.Cyl(w, new Vector3(0.6f, 0.25f, 0.6f), new Color(0.1f, 0.1f, 0.1f), new Vector3(0, 0, 90), 10);
            k.Ball(new Vector3(0.45f, 0.65f, 1.5f), new Vector3(0.25f, 0.25f, 0.1f), new Color(1f, 0.95f, 0.75f));
            k.Ball(new Vector3(-0.45f, 0.65f, 1.5f), new Vector3(0.25f, 0.25f, 0.1f), new Color(1f, 0.95f, 0.75f));
            k.Box(new Vector3(0, 0.45f, 1.52f), new Vector3(1.4f, 0.12f, 0.08f), new Color(0.8f, 0.8f, 0.82f));
            k.Box(new Vector3(0, 0.45f, -1.52f), new Vector3(1.4f, 0.12f, 0.08f), new Color(0.8f, 0.8f, 0.82f));
            k.Frame = old;
        }

        private static void Gem(Color c, float size)
        {
            kit.Cone(new Vector3(0, size * 0.3f, 0), new Vector3(size, size * 0.6f, size), c, default, 0.45f, 6);
            kit.Cone(new Vector3(0, -size * 0.3f, 0), new Vector3(size, size * 0.6f, size), c * 0.8f, new Vector3(180, 0, 0), 0f, 6);
        }
    }
}
