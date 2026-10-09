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
                    kit.Cone(Vector3.zero, new Vector3(0.2f, 0.55f, 0.2f), PastaGold, new Vector3(90, 0, 0), 1f, 10);
                    for (int i = 0; i < 4; i++)
                        kit.Cyl(new Vector3(0, 0, -0.18f + i * 0.12f), new Vector3(0.21f, 0.03f, 0.21f), PastaGold * 0.85f, new Vector3(90, 0, 0), 10);
                    kit.Cyl(new Vector3(0, 0, 0.27f), new Vector3(0.12f, 0.02f, 0.12f), new Color(0.6f, 0.4f, 0.15f), new Vector3(90, 0, 0), 8);
                    break;
                case ShotKind.Fusilli:
                    for (int i = 0; i < 14; i++)
                    {
                        float t = i / 13f;
                        float a = t * Mathf.PI * 5f;
                        kit.Ball(new Vector3(Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * 0.1f, (t - 0.5f) * 0.8f), 0.14f, Color.Lerp(PastaGold, new Color(1f, 0.9f, 0.5f), t));
                    }
                    break;
                case ShotKind.Farfalle:
                    kit.Cone(new Vector3(-0.22f, 0, 0), new Vector3(0.36f, 0.4f, 0.1f), PastaGold, new Vector3(0, 0, -90), 0.1f, 8);
                    kit.Cone(new Vector3(0.22f, 0, 0), new Vector3(0.36f, 0.4f, 0.1f), PastaGold, new Vector3(0, 0, 90), 0.1f, 8);
                    kit.Ball(Vector3.zero, new Vector3(0.14f, 0.12f, 0.2f), PastaGold * 0.85f);
                    break;
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
                    for (int i = 0; i < 16; i++)
                        kit.Cyl(new Vector3((i % 4 - 1.5f) * 0.07f, (i / 4 - 1.5f) * 0.07f, 0f), new Vector3(0.06f, 1.1f, 0.06f), i % 3 == 0 ? PastaGold * 0.9f : PastaGold, new Vector3(90, 0, 0), 6);
                    kit.Cyl(new Vector3(0, 0, 0.2f), new Vector3(0.34f, 0.12f, 0.34f), Tomato, new Vector3(90, 0, 0), 10);
                    kit.Cyl(new Vector3(0, 0, -0.25f), new Vector3(0.34f, 0.12f, 0.34f), Tomato, new Vector3(90, 0, 0), 10);
                    break;
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
                    kit.Cyl(Vector3.zero, new Vector3(0.9f, 0.05f, 0.9f), Color.white, default, 16);
                    kit.Cyl(new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.05f, 0.7f), new Color(0.92f, 0.92f, 0.9f), default, 16);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 0.8f;
                        kit.Ball(new Vector3(Mathf.Cos(a) * 0.14f, 0.1f, Mathf.Sin(a) * 0.14f), new Vector3(0.22f, 0.08f, 0.1f), new Color(1f, 0.85f, 0.45f), new Vector3(0, a * 57f, 0));
                    }
                    kit.Ball(new Vector3(0.05f, 0.16f, 0f), new Vector3(0.28f, 0.14f, 0.26f), new Color(1f, 0.98f, 0.9f));
                    kit.Box(new Vector3(-0.12f, 0.14f, 0.1f), new Vector3(0.06f, 0.04f, 0.06f), new Color(0.7f, 0.3f, 0.25f));
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
                    kit.Frame = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 25));
                    kit.Add(SubKit(Shot(ShotKind.Bazooka)), Matrix4x4.Scale(Vector3.one * 1.2f));
                    kit.Frame = Matrix4x4.identity;
                    break;
                case "special_KnifeDash":
                    kit.Box(new Vector3(0, 0.3f, 0), new Vector3(0.12f, 0.7f, 0.03f), new Color(0.85f, 0.87f, 0.9f), new Vector3(0, 0, 20));
                    kit.Box(new Vector3(-0.1f, -0.15f, 0), new Vector3(0.1f, 0.3f, 0.08f), new Color(0.3f, 0.2f, 0.12f), new Vector3(0, 0, 20));
                    kit.Box(new Vector3(0.12f, 0.1f, 0.1f), new Vector3(0.6f, 0.05f, 0.05f), PastaGold, new Vector3(0, 0, -30));
                    break;
                case "special_Parmesan":
                    kit.Add(SubKit(Shot(ShotKind.Parmesan)), Matrix4x4.Scale(Vector3.one * 1.6f));
                    break;
                case "special_Sprinkler":
                    kit.Add(SubKit(Get("sprinkler")), Matrix4x4.Scale(Vector3.one * 0.6f) * Matrix4x4.Translate(new Vector3(0, -0.6f, 0)));
                    break;
                case "special_SugarEspresso":
                    kit.Cyl(new Vector3(0, 0, 0), new Vector3(0.35f, 0.3f, 0.35f), Color.white, default, 12);
                    kit.Cyl(new Vector3(0, 0.14f, 0), new Vector3(0.3f, 0.04f, 0.3f), new Color(0.3f, 0.18f, 0.1f), default, 12);
                    kit.Cyl(new Vector3(0, -0.16f, 0), new Vector3(0.6f, 0.04f, 0.6f), Color.white, default, 14);
                    for (int i = 0; i < 5; i++) kit.Box(new Vector3(-0.3f + i * 0.15f, 0.3f + (i % 2) * 0.1f, 0.1f), Vector3.one * 0.1f, Color.white, new Vector3(20 * i, 10 * i, 0));
                    break;
                case "special_StarBeam":
                    {
                        // A parody frappé: clear cup, green band with a white star emblem, whipped cream dome, green straw.
                        var green = new Color(0f, 0.45f, 0.26f);
                        kit.Cone(new Vector3(0f, 0f, 0f), new Vector3(0.34f, 0.7f, 0.34f), new Color(0.8f, 0.62f, 0.45f), default, 1.3f, 14);
                        kit.Cyl(new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.16f, 0.4f), green, default, 14);
                        kit.Cyl(new Vector3(0f, 0.02f, 0.2f), new Vector3(0.15f, 0.02f, 0.15f), Color.white, new Vector3(90, 0, 0), 12);
                        for (int i = 0; i < 5; i++)
                            kit.Box(new Vector3(0f, 0.02f, 0.215f) + Quaternion.Euler(0, 0, i * 72f) * new Vector3(0f, 0.035f, 0f), new Vector3(0.025f, 0.06f, 0.01f), green, new Vector3(0, 0, i * 72f));
                        kit.Ball(new Vector3(0f, 0.4f, 0f), new Vector3(0.46f, 0.28f, 0.46f), new Color(1f, 0.98f, 0.93f));
                        for (int i = 0; i < 4; i++)
                            kit.Box(new Vector3(0f, 0.5f, -0.12f + i * 0.08f), new Vector3(0.3f, 0.02f, 0.02f), new Color(0.75f, 0.45f, 0.15f));
                        kit.Bar(new Vector3(0.05f, 0.3f, 0f), new Vector3(0.14f, 0.85f, 0f), 0.04f, new Color(0.1f, 0.6f, 0.3f));
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
                    kit.Box(Vector3.zero, new Vector3(0.7f, 0.05f, 0.35f), PastaGold);
                    for (int i = 0; i < 6; i++)
                    {
                        kit.Ball(new Vector3(-0.3f + i * 0.12f, 0, 0.18f), new Vector3(0.12f, 0.05f, 0.06f), PastaGold);
                        kit.Ball(new Vector3(-0.3f + i * 0.12f, 0, -0.18f), new Vector3(0.12f, 0.05f, 0.06f), PastaGold);
                    }
                    break;
                default:
                    kit.Box(Vector3.zero, Vector3.one * 0.2f, Color.magenta);
                    break;
            }
            mesh = kit.ToMesh(key);
            misc[key] = mesh;
            return mesh;
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
