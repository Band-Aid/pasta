using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PastaSurvivors
{
    /// <summary>Instance of an animated model: torso bobs, arms hang from the torso, legs swing from the hips.</summary>
    public class Rig
    {
        public Transform root, torso, legL, legR, armL, armR, held;
        public Transform[] wheels;
        public Renderer[] renderers;
        public float armRest, wheelRadius, wheelAngle;
        public bool seated;
        public bool freeHandL, freeHandR;
    }

    public class RigMeshes
    {
        public Mesh body, leg, armL, armR;
        public float hipX = 0.13f, hipY = 0.78f, shoulderX = 0.31f, shoulderY = 1.3f, armRest;
        public bool seated;
        public bool freeHandL, freeHandR;
        /// <summary>Optional wheels (origin at the hub) that roll with the ground speed.</summary>
        public Mesh wheel;
        public Vector3[] wheelPos;
        public float wheelRadius;
    }

    public enum Hat { None, Toque, Boater, Fedora, Cap, PaperHat, Helmet, CaptainHat, Headband }
    public enum Held { None, Slipper, Ladle, Oar, Pizza, Spoon, RollingPin, Pan, SelfieStick, GoldOar }

    public class HumanSpec
    {
        public Color skin = new Color(0.96f, 0.78f, 0.62f), shirt = Color.white, pants = new Color(0.25f, 0.25f, 0.28f),
            shoes = new Color(0.18f, 0.12f, 0.1f), hair = new Color(0.12f, 0.09f, 0.08f);
        public Color? scarf, apron, skirt, stripes, jacket, tie, hatBand;
        public int hairStyle;      // 0 short, 1 bun, 2 long, 3 bald ring, 4 ponytail
        public int moustache;      // 0 none, 1 normal, 2 grand
        public int glasses;        // 0 none, 1 round, 2 sunglasses
        public bool angry = true, shorts, facePaint, backpack, belly, buttons, necklace, cigar;
        public float hunch;
        public Hat hat;
        public Color hatColor = Color.white;
        public Held heldR, heldL;
    }

    public static class Models
    {
        private static readonly Dictionary<EnemyKind, RigMeshes> enemyMeshes = new Dictionary<EnemyKind, RigMeshes>();
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
            var rig = new Rig { armRest = m.armRest, seated = m.seated, freeHandL = m.freeHandL, freeHandR = m.freeHandR };
            rig.root = new GameObject(name).transform;
            rig.root.SetParent(parent, false);
            var renderers = new List<Renderer>();
            rig.torso = Part("Torso", rig.root, Vector3.zero, m.body, renderers);
            if (m.leg != null)
            {
                rig.legL = Part("LegL", rig.root, new Vector3(-m.hipX, m.hipY, 0), m.leg, renderers);
                rig.legR = Part("LegR", rig.root, new Vector3(m.hipX, m.hipY, 0), m.leg, renderers);
            }
            rig.armL = Part("ArmL", rig.torso, new Vector3(-m.shoulderX, m.shoulderY, 0), m.armL, renderers);
            rig.armR = Part("ArmR", rig.torso, new Vector3(m.shoulderX, m.shoulderY, 0), m.armR, renderers);
            if (m.wheel != null)
            {
                rig.wheels = new Transform[m.wheelPos.Length];
                for (int i = 0; i < m.wheelPos.Length; i++) rig.wheels[i] = Part("Wheel" + i, rig.root, m.wheelPos[i], m.wheel, renderers);
                rig.wheelRadius = m.wheelRadius;
            }
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

        public static RigMeshes Enemy(EnemyKind kind)
        {
            if (enemyMeshes.TryGetValue(kind, out var cached)) return cached;
            RigMeshes m;
            switch (kind)
            {
                case EnemyKind.Signore:
                    m = Humanoid(new HumanSpec { shirt = new Color(0.95f, 0.93f, 0.88f), pants = new Color(0.28f, 0.28f, 0.32f), scarf = Tomato, moustache = 1, belly = true }, kind);
                    break;
                case EnemyKind.Tifoso:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = Azzurro, pants = Color.white, shorts = true, scarf = new Color(0.95f, 0.95f, 1f), stripes = Azzurro,
                        hair = new Color(0.36f, 0.22f, 0.12f), facePaint = true, shoes = new Color(0.1f, 0.1f, 0.12f)
                    }, kind);
                    break;
                case EnemyKind.Mamma:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.78f, 0.18f, 0.22f), skirt = new Color(0.72f, 0.16f, 0.2f), apron = new Color(0.97f, 0.96f, 0.92f),
                        hair = new Color(0.3f, 0.17f, 0.1f), hairStyle = 1, belly = true, heldR = Held.Slipper, necklace = true
                    }, kind);
                    break;
                case EnemyKind.Chef:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.97f, 0.97f, 0.95f), pants = new Color(0.3f, 0.3f, 0.33f), scarf = Tomato, hat = Hat.Toque,
                        moustache = 2, belly = true, buttons = true, heldR = Held.Ladle
                    }, kind);
                    break;
                case EnemyKind.Vespista:
                    m = Vespista();
                    break;
                case EnemyKind.Gondoliere:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = Color.white, stripes = new Color(0.1f, 0.12f, 0.3f), pants = new Color(0.08f, 0.08f, 0.1f), scarf = Tomato,
                        hat = Hat.Boater, hatColor = new Color(0.93f, 0.82f, 0.5f), hatBand = Tomato, moustache = 1, heldR = Held.Oar
                    }, kind);
                    break;
                case EnemyKind.Pizzaiolo:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.98f, 0.98f, 0.98f), pants = new Color(0.2f, 0.22f, 0.3f), apron = new Color(0.99f, 0.99f, 0.97f),
                        scarf = Tomato, hat = Hat.PaperHat, moustache = 1, heldR = Held.Pizza, skin = Tan
                    }, kind);
                    break;
                case EnemyKind.Mafioso:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.95f, 0.95f, 0.95f), jacket = new Color(0.1f, 0.1f, 0.12f), pants = new Color(0.1f, 0.1f, 0.12f),
                        tie = new Color(0.75f, 0.08f, 0.1f), hat = Hat.Fedora, hatColor = new Color(0.12f, 0.12f, 0.14f), hatBand = new Color(0.5f, 0.1f, 0.1f),
                        glasses = 2, moustache = 1, skin = Tan
                    }, kind);
                    break;
                case EnemyKind.Nonna:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.12f, 0.1f, 0.12f), skirt = new Color(0.1f, 0.09f, 0.11f), scarf = new Color(0.3f, 0.28f, 0.32f),
                        hair = new Color(0.82f, 0.82f, 0.84f), hairStyle = 1, glasses = 1, hunch = 14f, heldR = Held.Spoon, angry = true
                    }, kind);
                    break;
                case EnemyKind.BossNonna:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.14f, 0.1f, 0.16f), skirt = new Color(0.12f, 0.08f, 0.14f), apron = new Color(0.95f, 0.93f, 0.85f),
                        scarf = new Color(0.55f, 0.1f, 0.18f), hair = new Color(0.9f, 0.9f, 0.92f), hairStyle = 1, glasses = 1,
                        hunch = 10f, heldR = Held.RollingPin, necklace = true
                    }, kind);
                    break;
                case EnemyKind.BossCapitano:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = Color.white, stripes = new Color(0.55f, 0.08f, 0.12f), pants = new Color(0.08f, 0.08f, 0.1f), scarf = Azzurro,
                        hat = Hat.CaptainHat, hatColor = new Color(0.1f, 0.12f, 0.25f), moustache = 2, heldR = Held.GoldOar, belly = true
                    }, kind);
                    break;
                case EnemyKind.BossBikeNonna:
                    m = BikeNonna();
                    break;
                case EnemyKind.BossDon:
                    m = Humanoid(new HumanSpec
                    {
                        shirt = new Color(0.12f, 0.1f, 0.1f), jacket = new Color(0.95f, 0.94f, 0.9f), pants = new Color(0.95f, 0.94f, 0.9f),
                        tie = new Color(0.85f, 0.7f, 0.2f), hat = Hat.Fedora, hatColor = new Color(0.95f, 0.94f, 0.9f), hatBand = new Color(0.1f, 0.1f, 0.1f),
                        glasses = 2, moustache = 2, belly = true, cigar = true, necklace = true, heldR = Held.Pan, skin = Tan
                    }, kind);
                    break;
                default:
                    m = Barrel();
                    break;
            }
            enemyMeshes[kind] = m;
            return m;
        }

        public static RigMeshes Player(CharacterDef c, int index)
        {
            var spec = new HumanSpec
            {
                skin = c.skin, shirt = c.shirt, pants = c.pants, hair = c.hair, angry = false, shoes = new Color(0.95f, 0.95f, 0.95f)
            };
            switch (index)
            {
                case 0: spec.hat = Hat.Cap; spec.hatColor = new Color(0.15f, 0.3f, 0.7f); spec.shorts = true; spec.backpack = true; break;
                case 1: spec.hairStyle = 4; spec.skirt = c.pants; spec.scarf = new Color(0.98f, 0.98f, 0.95f); break;
                default: spec.hat = Hat.Headband; spec.glasses = 1; spec.apron = new Color(0.95f, 0.95f, 0.9f); spec.heldL = Held.SelfieStick; break;
            }
            var m = Humanoid(spec, null);
            m.armRest = -62f;
            return m;
        }

        private static RigMeshes Humanoid(HumanSpec s, EnemyKind? kind)
        {
            var m = new RigMeshes { freeHandL = s.heldL == Held.None, freeHandR = s.heldR == Held.None };
            // ---- legs (origin at hip)
            kit.Clear();
            float legLen = m.hipY;
            if (s.skirt.HasValue || s.shorts)
            {
                kit.Bar(new Vector3(0, -0.02f, 0), new Vector3(0, -legLen + 0.1f, 0), 0.13f, s.skin);
                if (s.shorts) kit.Cyl(new Vector3(0, -0.14f, 0), new Vector3(0.2f, 0.3f, 0.2f), s.pants);
            }
            else kit.Bar(new Vector3(0, 0.02f, 0), new Vector3(0, -legLen + 0.1f, 0), 0.18f, s.pants);
            kit.Box(new Vector3(0, -legLen + 0.05f, 0.05f), new Vector3(0.16f, 0.1f, 0.3f), s.shoes);
            m.leg = kit.ToMesh("Leg");

            // ---- body
            kit.Clear();
            float lean = s.hunch;
            kit.Frame = Matrix4x4.TRS(new Vector3(0, 0.75f, 0), Quaternion.Euler(lean, 0, 0), Vector3.one) * Matrix4x4.Translate(new Vector3(0, -0.75f, 0));
            var torsoColor = s.jacket ?? s.shirt;
            kit.Ball(new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.72f, 0.44f), torsoColor);
            kit.Cyl(new Vector3(0, 0.82f, 0), new Vector3(0.5f, 0.2f, 0.38f), s.skirt.HasValue ? s.skirt.Value : s.pants);
            if (s.belly) kit.Ball(new Vector3(0, 0.98f, 0.1f), new Vector3(0.5f, 0.5f, 0.4f), s.apron ?? torsoColor);
            if (s.stripes.HasValue && !s.jacket.HasValue)
                for (int i = 0; i < 4; i++)
                    kit.Cyl(new Vector3(0, 0.86f + i * 0.13f, 0), new Vector3(0.6f - Mathf.Abs(i - 1.5f) * 0.04f, 0.05f, 0.45f), s.stripes.Value);
            if (s.jacket.HasValue)
            {
                kit.Box(new Vector3(0, 1.18f, 0.2f), new Vector3(0.16f, 0.32f, 0.06f), s.shirt, new Vector3(-8, 0, 0));
                if (s.tie.HasValue) kit.Box(new Vector3(0, 1.12f, 0.235f), new Vector3(0.06f, 0.28f, 0.03f), s.tie.Value, new Vector3(-8, 0, 0));
            }
            if (s.buttons)
                for (int i = 0; i < 3; i++)
                {
                    kit.Ball(new Vector3(-0.08f, 0.98f + i * 0.13f, 0.24f), 0.05f, new Color(0.7f, 0.7f, 0.72f));
                    kit.Ball(new Vector3(0.08f, 0.98f + i * 0.13f, 0.24f), 0.05f, new Color(0.7f, 0.7f, 0.72f));
                }
            if (s.skirt.HasValue)
                kit.Cone(new Vector3(0, 0.52f, 0), new Vector3(0.78f, 0.62f, 0.62f), s.skirt.Value, default, 0.62f, 12);
            if (s.apron.HasValue)
            {
                kit.Box(new Vector3(0, 0.78f, 0.27f), new Vector3(0.44f, 0.58f, 0.04f), s.apron.Value, new Vector3(-6, 0, 0));
                kit.Box(new Vector3(0, 1.17f, 0.22f), new Vector3(0.3f, 0.26f, 0.04f), s.apron.Value, new Vector3(-12, 0, 0));
            }
            if (s.backpack)
            {
                kit.Box(new Vector3(0, 1.08f, -0.3f), new Vector3(0.42f, 0.5f, 0.24f), new Color(0.95f, 0.55f, 0.15f));
                kit.Box(new Vector3(0, 1.36f, -0.3f), new Vector3(0.3f, 0.12f, 0.2f), new Color(0.2f, 0.2f, 0.25f));
            }
            if (s.scarf.HasValue)
            {
                kit.Cyl(new Vector3(0, 1.38f, 0), new Vector3(0.38f, 0.1f, 0.34f), s.scarf.Value);
                if (s.stripes.HasValue && !s.jacket.HasValue && kind == EnemyKind.Tifoso)
                    kit.Box(new Vector3(0.1f, 1.2f, 0.2f), new Vector3(0.1f, 0.34f, 0.04f), s.stripes.Value, new Vector3(0, 0, 8));
                else kit.Box(new Vector3(0.08f, 1.26f, 0.19f), new Vector3(0.1f, 0.2f, 0.04f), s.scarf.Value, new Vector3(-10, 0, 10));
            }
            if (s.necklace)
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.Lerp(-70, 70, i / 6f) * Mathf.Deg2Rad;
                    kit.Ball(new Vector3(Mathf.Sin(a) * 0.17f, 1.33f - Mathf.Cos(a) * 0.05f, 0.14f + Mathf.Cos(a) * 0.07f), 0.05f,
                        kind == EnemyKind.BossDon ? new Color(1f, 0.82f, 0.25f) : new Color(0.97f, 0.95f, 0.9f));
                }
            Head(s, 1.66f);
            if (kind == EnemyKind.BossDon) kit.Box(new Vector3(0.24f, 1.03f, 0.23f), new Vector3(0.12f, 0.05f, 0.03f), new Color(0.95f, 0.1f, 0.1f));
            m.body = kit.ToMesh("Body");

            // ---- arms (origin at shoulder, hanging down)
            m.armL = Arm(s, s.heldL, true);
            m.armR = Arm(s, s.heldR, false);
            m.shoulderY = 1.3f;
            return m;
        }

        private static Mesh Arm(HumanSpec s, Held held, bool left)
        {
            kit.Clear();
            var sleeve = s.jacket ?? s.shirt;
            kit.Bar(new Vector3(0, 0.02f, 0), new Vector3(0, -0.5f, 0), 0.15f, sleeve);
            if (held == Held.None && s.angry)
            {
                // A joined fingertip cluster and thumb, palm up when the arm is raised to chest height.
                kit.Ball(new Vector3(0, -0.57f, 0.02f), new Vector3(0.17f, 0.13f, 0.09f), s.skin);
                kit.Cone(new Vector3(0, -0.63f, 0.08f), new Vector3(0.125f, 0.12f, 0.1f), s.skin, new Vector3(90, 0, 0), 0f, 6);
                kit.Bar(new Vector3(left ? 0.075f : -0.075f, -0.545f, 0.045f), new Vector3(0, -0.63f, 0.135f), 0.045f, s.skin);
            }
            else
            {
                kit.Ball(new Vector3(0, -0.57f, 0.02f), 0.14f, s.skin);
                kit.Cone(new Vector3(0, -0.66f, 0.03f), new Vector3(0.09f, 0.1f, 0.09f), s.skin, new Vector3(180, 0, 0), 0f, 6);
            }
            var hand = new Vector3(0, -0.58f, 0.04f);
            switch (held)
            {
                case Held.Slipper:
                    kit.Box(hand + new Vector3(0, -0.02f, 0.14f), new Vector3(0.13f, 0.035f, 0.32f), new Color(0.95f, 0.45f, 0.62f), new Vector3(-15, 0, 0));
                    kit.Box(hand + new Vector3(0, 0.02f, 0.2f), new Vector3(0.14f, 0.05f, 0.08f), new Color(0.3f, 0.55f, 0.9f), new Vector3(-15, 0, 0));
                    break;
                case Held.Ladle:
                    kit.Bar(hand + new Vector3(0, 0.05f, 0), hand + new Vector3(0, -0.02f, 0.45f), 0.035f, new Color(0.75f, 0.75f, 0.78f));
                    kit.Ball(hand + new Vector3(0, -0.03f, 0.5f), new Vector3(0.16f, 0.09f, 0.16f), new Color(0.75f, 0.75f, 0.78f));
                    break;
                case Held.Spoon:
                    kit.Bar(hand + new Vector3(0, 0.05f, 0), hand + new Vector3(0, -0.02f, 0.4f), 0.04f, new Color(0.6f, 0.4f, 0.2f));
                    kit.Ball(hand + new Vector3(0, -0.03f, 0.45f), new Vector3(0.12f, 0.05f, 0.16f), new Color(0.6f, 0.4f, 0.2f));
                    break;
                case Held.RollingPin:
                    kit.Cyl(hand + new Vector3(0, 0, 0.05f), new Vector3(0.12f, 0.6f, 0.12f), new Color(0.82f, 0.6f, 0.36f), new Vector3(90, 0, 0));
                    kit.Cyl(hand + new Vector3(0, 0, 0.42f), new Vector3(0.05f, 0.14f, 0.05f), new Color(0.6f, 0.4f, 0.22f), new Vector3(90, 0, 0));
                    break;
                case Held.Oar:
                case Held.GoldOar:
                    {
                        var c = held == Held.GoldOar ? new Color(1f, 0.8f, 0.3f) : new Color(0.55f, 0.36f, 0.18f);
                        kit.Bar(hand + new Vector3(0, 0.9f, -0.15f), hand + new Vector3(0, -0.9f, 0.35f), 0.06f, c);
                        kit.Box(hand + new Vector3(0, -1.05f, 0.4f), new Vector3(0.05f, 0.45f, 0.18f), c, new Vector3(-15, 0, 0));
                        break;
                    }
                case Held.Pizza:
                    kit.Cyl(hand + new Vector3(0, -0.02f, 0.2f), new Vector3(0.6f, 0.05f, 0.6f), new Color(0.92f, 0.72f, 0.4f), new Vector3(-70, 0, 0), 14);
                    kit.Cyl(hand + new Vector3(0, 0.01f, 0.2f), new Vector3(0.5f, 0.05f, 0.5f), Tomato, new Vector3(-70, 0, 0), 14);
                    break;
                case Held.Pan:
                    kit.Bar(hand, hand + new Vector3(0, 0, 0.3f), 0.05f, new Color(0.15f, 0.15f, 0.15f));
                    kit.Cyl(hand + new Vector3(0, 0, 0.55f), new Vector3(0.5f, 0.08f, 0.5f), new Color(0.2f, 0.2f, 0.22f), new Vector3(0, 0, 0), 14);
                    kit.Cyl(hand + new Vector3(0, 0.03f, 0.55f), new Vector3(0.42f, 0.06f, 0.42f), new Color(1f, 0.9f, 0.55f), default, 14);
                    break;
                case Held.SelfieStick:
                    kit.Bar(hand, hand + new Vector3(0, -0.1f, 0.7f), 0.03f, new Color(0.2f, 0.2f, 0.22f));
                    kit.Box(hand + new Vector3(0, -0.1f, 0.75f), new Vector3(0.1f, 0.18f, 0.02f), new Color(0.1f, 0.1f, 0.12f));
                    break;
            }
            return kit.ToMesh(left ? "ArmL" : "ArmR");
        }

        private static void Head(HumanSpec s, float y)
        {
            var hair = s.hair;
            kit.Ball(new Vector3(0, y, 0), new Vector3(0.5f, 0.5f, 0.48f), s.skin, default, true);
            // Ears
            kit.Ball(new Vector3(-0.25f, y - 0.02f, 0), new Vector3(0.08f, 0.12f, 0.08f), s.skin);
            kit.Ball(new Vector3(0.25f, y - 0.02f, 0), new Vector3(0.08f, 0.12f, 0.08f), s.skin);
            // Big comic eyes
            float ey = y + 0.04f;
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Ball(new Vector3(side * 0.1f, ey, 0.2f), new Vector3(0.14f, 0.15f, 0.1f), Color.white);
                kit.Ball(new Vector3(side * 0.095f, ey - 0.01f, 0.25f), 0.07f, new Color(0.08f, 0.06f, 0.06f));
                float browTilt = s.angry ? side * -20f : side * 8f;
                kit.Box(new Vector3(side * 0.1f, ey + 0.11f, 0.22f), new Vector3(0.14f, 0.035f, 0.04f), hair.grayscale > 0.7f ? new Color(0.6f, 0.6f, 0.62f) : hair, new Vector3(0, 0, browTilt));
            }
            kit.Ball(new Vector3(0, y - 0.04f, 0.25f), new Vector3(0.1f, 0.12f, 0.12f), s.skin * 0.95f + new Color(0.05f, 0, 0));
            // Mouth: open shouting oval.
            kit.Ball(new Vector3(0, y - 0.15f, 0.21f), new Vector3(0.12f, s.angry ? 0.07f : 0.03f, 0.04f), new Color(0.45f, 0.08f, 0.1f));
            if (s.moustache > 0)
            {
                var mc = hair.grayscale > 0.7f ? new Color(0.7f, 0.7f, 0.72f) : hair;
                float w = s.moustache == 2 ? 0.2f : 0.14f;
                kit.Box(new Vector3(-0.07f, y - 0.1f, 0.24f), new Vector3(w, 0.05f, 0.05f), mc, new Vector3(0, 0, 14));
                kit.Box(new Vector3(0.07f, y - 0.1f, 0.24f), new Vector3(w, 0.05f, 0.05f), mc, new Vector3(0, 0, -14));
                if (s.moustache == 2)
                {
                    kit.Ball(new Vector3(-0.17f, y - 0.06f, 0.22f), 0.06f, mc);
                    kit.Ball(new Vector3(0.17f, y - 0.06f, 0.22f), 0.06f, mc);
                }
            }
            if (s.facePaint)
            {
                kit.Box(new Vector3(-0.17f, y - 0.05f, 0.17f), new Vector3(0.025f, 0.08f, 0.02f), new Color(0.1f, 0.6f, 0.25f), new Vector3(0, -35, 0));
                kit.Box(new Vector3(-0.155f, y - 0.05f, 0.185f), new Vector3(0.025f, 0.08f, 0.02f), Color.white, new Vector3(0, -35, 0));
                kit.Box(new Vector3(-0.14f, y - 0.05f, 0.2f), new Vector3(0.025f, 0.08f, 0.02f), Tomato, new Vector3(0, -35, 0));
            }
            if (s.cigar)
            {
                kit.Bar(new Vector3(0.05f, y - 0.14f, 0.23f), new Vector3(0.2f, y - 0.17f, 0.36f), 0.04f, new Color(0.4f, 0.24f, 0.12f));
                kit.Ball(new Vector3(0.2f, y - 0.17f, 0.36f), 0.045f, new Color(1f, 0.4f, 0.1f));
            }
            if (s.glasses == 1)
            {
                var g = new Color(0.25f, 0.2f, 0.18f);
                kit.Cyl(new Vector3(-0.1f, ey, 0.27f), new Vector3(0.15f, 0.015f, 0.15f), g, new Vector3(90, 0, 0), 10);
                kit.Cyl(new Vector3(0.1f, ey, 0.27f), new Vector3(0.15f, 0.015f, 0.15f), g, new Vector3(90, 0, 0), 10);
            }
            else if (s.glasses == 2)
            {
                kit.Box(new Vector3(0, ey, 0.27f), new Vector3(0.38f, 0.09f, 0.03f), new Color(0.05f, 0.05f, 0.06f));
            }
            // Hair
            switch (s.hairStyle)
            {
                case 1:
                    kit.Ball(new Vector3(0, y + 0.1f, -0.03f), new Vector3(0.54f, 0.38f, 0.52f), hair);
                    kit.Ball(new Vector3(0, y + 0.22f, -0.2f), 0.24f, hair);
                    break;
                case 2:
                case 4:
                    kit.Ball(new Vector3(0, y + 0.09f, -0.03f), new Vector3(0.55f, 0.4f, 0.54f), hair);
                    if (s.hairStyle == 2) kit.Box(new Vector3(0, y - 0.15f, -0.16f), new Vector3(0.5f, 0.5f, 0.2f), hair);
                    else kit.Bar(new Vector3(0, y + 0.1f, -0.25f), new Vector3(0, y - 0.3f, -0.34f), 0.14f, hair);
                    break;
                case 3:
                    kit.Ball(new Vector3(0, y - 0.02f, -0.06f), new Vector3(0.52f, 0.2f, 0.48f), hair);
                    break;
                default:
                    kit.Ball(new Vector3(0, y + 0.1f, -0.04f), new Vector3(0.53f, 0.36f, 0.52f), hair);
                    break;
            }
            float top = y + 0.24f;
            var band = s.hatBand ?? new Color(0.2f, 0.2f, 0.2f);
            switch (s.hat)
            {
                case Hat.Toque:
                    kit.Cyl(new Vector3(0, top + 0.12f, 0), new Vector3(0.4f, 0.3f, 0.4f), s.hatColor);
                    kit.Ball(new Vector3(0, top + 0.33f, 0), new Vector3(0.55f, 0.32f, 0.55f), s.hatColor);
                    break;
                case Hat.Boater:
                    kit.Cyl(new Vector3(0, top - 0.02f, 0), new Vector3(0.78f, 0.035f, 0.78f), s.hatColor, default, 14);
                    kit.Cyl(new Vector3(0, top + 0.07f, 0), new Vector3(0.46f, 0.16f, 0.46f), s.hatColor, default, 14);
                    kit.Cyl(new Vector3(0, top + 0.03f, 0), new Vector3(0.47f, 0.06f, 0.47f), band, default, 14);
                    break;
                case Hat.Fedora:
                    kit.Cyl(new Vector3(0, top - 0.03f, 0), new Vector3(0.7f, 0.035f, 0.66f), s.hatColor, default, 14);
                    kit.Cone(new Vector3(0, top + 0.1f, 0), new Vector3(0.46f, 0.24f, 0.44f), s.hatColor, default, 0.8f, 12);
                    kit.Cyl(new Vector3(0, top + 0.02f, 0), new Vector3(0.47f, 0.06f, 0.45f), band, default, 12);
                    break;
                case Hat.Cap:
                    kit.Ball(new Vector3(0, top - 0.04f, -0.02f), new Vector3(0.54f, 0.3f, 0.54f), s.hatColor);
                    kit.Box(new Vector3(0, top - 0.1f, -0.33f), new Vector3(0.32f, 0.03f, 0.22f), s.hatColor);
                    break;
                case Hat.PaperHat:
                    kit.Cyl(new Vector3(0, top + 0.02f, 0), new Vector3(0.48f, 0.16f, 0.48f), Color.white);
                    break;
                case Hat.CaptainHat:
                    kit.Cyl(new Vector3(0, top + 0.05f, 0.02f), new Vector3(0.56f, 0.18f, 0.56f), Color.white, default, 14);
                    kit.Cyl(new Vector3(0, top - 0.02f, 0.02f), new Vector3(0.5f, 0.08f, 0.5f), s.hatColor, default, 14);
                    kit.Box(new Vector3(0, top - 0.05f, 0.27f), new Vector3(0.36f, 0.03f, 0.16f), new Color(0.05f, 0.05f, 0.08f), new Vector3(15, 0, 0));
                    kit.Ball(new Vector3(0, top + 0.04f, 0.28f), new Vector3(0.1f, 0.1f, 0.04f), new Color(1f, 0.82f, 0.3f));
                    break;
                case Hat.Headband:
                    kit.Cyl(new Vector3(0, y + 0.13f, 0), new Vector3(0.53f, 0.07f, 0.51f), Color.white, default, 14);
                    kit.Ball(new Vector3(0, y + 0.13f, 0.25f), new Vector3(0.08f, 0.08f, 0.03f), Tomato);
                    break;
            }
        }

        private static RigMeshes Vespista()
        {
            var s = new HumanSpec
            {
                shirt = new Color(0.45f, 0.28f, 0.16f), pants = new Color(0.2f, 0.25f, 0.4f), glasses = 2, hair = new Color(0.15f, 0.1f, 0.08f),
                hat = Hat.Helmet, skin = Tan
            };
            var m = new RigMeshes { seated = true, armRest = -58f, leg = null };
            kit.Clear();
            var mint = new Color(0.55f, 0.85f, 0.75f);
            // Scooter
            kit.Box(new Vector3(0, 0.45f, -0.25f), new Vector3(0.5f, 0.45f, 0.8f), mint);
            kit.Ball(new Vector3(0, 0.5f, -0.45f), new Vector3(0.6f, 0.5f, 0.7f), mint);
            kit.Box(new Vector3(0, 0.3f, 0.3f), new Vector3(0.36f, 0.12f, 0.6f), mint);
            kit.Box(new Vector3(0, 0.75f, 0.6f), new Vector3(0.42f, 0.8f, 0.1f), mint, new Vector3(-12, 0, 0));
            kit.Bar(new Vector3(-0.35f, 1.15f, 0.62f), new Vector3(0.35f, 1.15f, 0.62f), 0.05f, new Color(0.3f, 0.3f, 0.32f));
            kit.Ball(new Vector3(0, 1.1f, 0.68f), new Vector3(0.18f, 0.18f, 0.08f), new Color(1f, 0.95f, 0.7f));
            kit.Box(new Vector3(0, 0.72f, -0.3f), new Vector3(0.38f, 0.08f, 0.6f), new Color(0.35f, 0.2f, 0.12f));
            kit.Cyl(new Vector3(0, 0.2f, 0.62f), new Vector3(0.36f, 0.12f, 0.36f), new Color(0.12f, 0.12f, 0.12f), new Vector3(0, 0, 90));
            kit.Cyl(new Vector3(0, 0.2f, -0.6f), new Vector3(0.36f, 0.12f, 0.36f), new Color(0.12f, 0.12f, 0.12f), new Vector3(0, 0, 90));
            // Rider sits higher; reuse humanoid torso + head via frame offset.
            kit.Frame = Matrix4x4.Translate(new Vector3(0, 0.1f, -0.25f));
            kit.Ball(new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.72f, 0.44f), s.shirt);
            // Thighs forward, shins down
            kit.Bar(new Vector3(-0.14f, 0.78f, 0f), new Vector3(-0.16f, 0.72f, 0.45f), 0.18f, s.pants);
            kit.Bar(new Vector3(0.14f, 0.78f, 0f), new Vector3(0.16f, 0.72f, 0.45f), 0.18f, s.pants);
            kit.Bar(new Vector3(-0.16f, 0.72f, 0.45f), new Vector3(-0.17f, 0.3f, 0.5f), 0.15f, s.pants);
            kit.Bar(new Vector3(0.16f, 0.72f, 0.45f), new Vector3(0.17f, 0.3f, 0.5f), 0.15f, s.pants);
            kit.Box(new Vector3(-0.17f, 0.25f, 0.56f), new Vector3(0.14f, 0.09f, 0.26f), s.shoes);
            kit.Box(new Vector3(0.17f, 0.25f, 0.56f), new Vector3(0.14f, 0.09f, 0.26f), s.shoes);
            kit.Cyl(new Vector3(0, 1.38f, 0), new Vector3(0.36f, 0.1f, 0.32f), new Color(0.95f, 0.95f, 0.9f));
            Head(s, 1.66f);
            kit.Ball(new Vector3(0, 1.78f, -0.02f), new Vector3(0.58f, 0.42f, 0.58f), new Color(0.92f, 0.22f, 0.2f));
            kit.Box(new Vector3(0, 1.84f, 0), new Vector3(0.08f, 0.2f, 0.56f), Color.white);
            m.body = kit.ToMesh("Vespista");
            m.armL = Arm(s, Held.None, true);
            m.armR = Arm(s, Held.None, false);
            m.shoulderY = 1.4f;
            return m;
        }

        /// <summary>Grande Nonna with wheels: she would have been a bike. A celeste step-through city bike, basket of spaghetti and all.</summary>
        private static RigMeshes BikeNonna()
        {
            var s = new HumanSpec
            {
                shirt = new Color(0.14f, 0.1f, 0.16f), skirt = new Color(0.12f, 0.08f, 0.14f), apron = new Color(0.95f, 0.93f, 0.85f),
                scarf = new Color(0.55f, 0.1f, 0.18f), hair = new Color(0.9f, 0.9f, 0.92f), hairStyle = 1, glasses = 1, necklace = true
            };
            const float wheelR = 0.45f;
            var rearHub = new Vector3(0f, wheelR, -0.75f);
            var frontHub = new Vector3(0f, wheelR, 0.5f);
            var m = new RigMeshes
            {
                seated = true, armRest = -58f, leg = null, shoulderY = 1.58f,
                wheelPos = new[] { rearHub, frontHub }, wheelRadius = wheelR
            };

            // ---- wheel (origin at the hub, axle along X): tyre, rim, spokes and hub
            kit.Clear();
            var tyre = new Color(0.1f, 0.1f, 0.1f);
            var steel = new Color(0.78f, 0.8f, 0.84f);
            Ring(Vector3.zero, wheelR - 0.035f, 18, 0.07f, tyre, 0f, 360f);
            Ring(Vector3.zero, wheelR - 0.08f, 18, 0.025f, steel, 0f, 360f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                kit.Bar(Vector3.zero, new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * (wheelR - 0.08f), 0.022f, steel);
            }
            kit.Cyl(Vector3.zero, new Vector3(0.09f, 0.14f, 0.09f), steel, new Vector3(0, 0, 90), 8);
            m.wheel = kit.ToMesh("BikeWheel");

            // ---- bike frame
            kit.Clear();
            var celeste = new Color(0.5f, 0.82f, 0.76f);
            var bb = new Vector3(0f, 0.42f, -0.15f);
            var seatTop = new Vector3(0f, 0.98f, -0.2f);
            var headLow = new Vector3(0f, 0.78f, 0.36f);
            var headTop = new Vector3(0f, 1.02f, 0.33f);
            kit.Bar(bb, seatTop, 0.06f, celeste);
            kit.Bar(headLow, headTop, 0.07f, celeste);
            // Step-through: two low tubes swoop from the head tube to the bottom bracket.
            kit.Bar(headTop + new Vector3(0, -0.06f, 0), new Vector3(0f, 0.62f, 0.12f), 0.055f, celeste);
            kit.Bar(new Vector3(0f, 0.62f, 0.12f), bb, 0.055f, celeste);
            kit.Bar(headLow, new Vector3(0f, 0.5f, 0.15f), 0.05f, celeste);
            kit.Bar(new Vector3(0f, 0.5f, 0.15f), bb + new Vector3(0, 0.02f, 0.04f), 0.05f, celeste);
            for (int side = -1; side <= 1; side += 2)
            {
                var hubSide = new Vector3(side * 0.06f, 0f, 0f);
                kit.Bar(bb + hubSide, rearHub + hubSide, 0.035f, celeste);
                kit.Bar(seatTop + new Vector3(side * 0.04f, -0.06f, 0f), rearHub + hubSide, 0.035f, celeste);
                kit.Bar(headLow + hubSide, frontHub + hubSide, 0.04f, celeste);
            }
            // Mudguards
            Ring(rearHub, wheelR + 0.05f, 10, 0.05f, celeste, 30f, 200f);
            Ring(frontHub, wheelR + 0.05f, 10, 0.05f, celeste, -15f, 150f);
            // Crank, chainring and pedals
            kit.Cyl(bb + new Vector3(0.07f, 0f, 0f), new Vector3(0.22f, 0.02f, 0.22f), steel, new Vector3(0, 0, 90), 12);
            kit.Bar(bb + new Vector3(0.1f, 0f, 0f), bb + new Vector3(0.1f, -0.14f, 0.08f), 0.03f, steel);
            kit.Box(bb + new Vector3(0.15f, -0.15f, 0.08f), new Vector3(0.1f, 0.025f, 0.06f), tyre);
            kit.Bar(bb + new Vector3(-0.1f, 0f, 0f), bb + new Vector3(-0.1f, 0.14f, -0.08f), 0.03f, steel);
            // Saddle, stem, swept-back bars, bell and lamp
            kit.Box(seatTop + new Vector3(0, 0.04f, -0.02f), new Vector3(0.22f, 0.07f, 0.3f), new Color(0.45f, 0.26f, 0.14f));
            var stem = new Vector3(0f, 1.15f, 0.31f);
            kit.Bar(headTop, stem, 0.04f, steel);
            for (int side = -1; side <= 1; side += 2)
            {
                var mid = new Vector3(side * 0.2f, 1.17f, 0.37f);
                var grip = new Vector3(side * 0.31f, 1.2f, 0.5f);
                kit.Bar(stem, mid, 0.035f, steel);
                kit.Bar(mid, grip, 0.035f, steel);
                kit.Bar(grip, grip + new Vector3(side * 0.08f, 0f, 0.04f), 0.05f, new Color(0.35f, 0.2f, 0.1f));
            }
            kit.Cyl(new Vector3(0.14f, 1.22f, 0.38f), new Vector3(0.1f, 0.05f, 0.1f), new Color(1f, 0.82f, 0.3f), default, 10);
            kit.Ball(new Vector3(0f, 1.0f, 0.44f), new Vector3(0.14f, 0.14f, 0.1f), new Color(1f, 0.95f, 0.7f));
            // Wicker basket with spaghetti, tomatoes and basil
            var wicker = new Color(0.72f, 0.52f, 0.28f);
            var basket = new Vector3(0f, 1.05f, 0.66f);
            kit.Box(basket, new Vector3(0.46f, 0.3f, 0.34f), wicker);
            kit.Box(basket + new Vector3(0, 0.15f, 0), new Vector3(0.4f, 0.02f, 0.28f), new Color(0.35f, 0.22f, 0.1f));
            kit.Box(basket + new Vector3(0, 0.05f, 0), new Vector3(0.47f, 0.035f, 0.35f), wicker * 0.8f);
            kit.Box(basket + new Vector3(0, -0.06f, 0), new Vector3(0.47f, 0.035f, 0.35f), wicker * 0.8f);
            for (int i = 0; i < 6; i++)
                kit.Cyl(basket + new Vector3(-0.1f + i * 0.035f, 0.3f, -0.04f + (i % 2) * 0.05f), new Vector3(0.025f, 0.42f, 0.025f), PastaGold, new Vector3(-8f + i * 3f, 0f, 12f - i * 5f), 5);
            kit.Ball(basket + new Vector3(0.12f, 0.17f, 0.07f), 0.14f, Tomato);
            kit.Ball(basket + new Vector3(0.03f, 0.18f, 0.1f), 0.12f, Tomato);
            kit.Ball(basket + new Vector3(-0.12f, 0.17f, 0.08f), new Vector3(0.12f, 0.04f, 0.08f), Basil, new Vector3(0, 30, 20));
            // Rear rack with a little tricolore pennant
            kit.Box(new Vector3(0f, 1.0f, -0.72f), new Vector3(0.22f, 0.03f, 0.4f), new Color(0.3f, 0.3f, 0.32f));
            kit.Bar(new Vector3(0f, 1.0f, -0.88f), new Vector3(0f, 1.75f, -0.95f), 0.02f, steel);
            kit.Box(new Vector3(0f, 1.66f, -1.0f), new Vector3(0.02f, 0.14f, 0.06f), Basil);
            kit.Box(new Vector3(0f, 1.66f, -1.06f), new Vector3(0.02f, 0.14f, 0.06f), Color.white);
            kit.Box(new Vector3(0f, 1.66f, -1.12f), new Vector3(0.02f, 0.14f, 0.06f), Tomato);

            // ---- Nonna, perched on the saddle; her skirt flows down into the frame where her legs would be
            kit.Frame = Matrix4x4.Translate(new Vector3(0f, 0.28f, -0.12f));
            kit.Ball(new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.72f, 0.44f), s.shirt);
            kit.Cyl(new Vector3(0, 0.82f, 0), new Vector3(0.5f, 0.2f, 0.38f), s.skirt.Value);
            kit.Cone(new Vector3(0, 0.52f, 0.02f), new Vector3(0.7f, 0.6f, 0.66f), s.skirt.Value, default, 0.62f, 12);
            kit.Box(new Vector3(0, 1.17f, 0.22f), new Vector3(0.3f, 0.26f, 0.04f), s.apron.Value, new Vector3(-12, 0, 0));
            kit.Cyl(new Vector3(0, 1.38f, 0), new Vector3(0.38f, 0.1f, 0.34f), s.scarf.Value);
            // Scarf ends streaming out behind her
            kit.Box(new Vector3(0.06f, 1.36f, -0.3f), new Vector3(0.1f, 0.04f, 0.3f), s.scarf.Value, new Vector3(-12, 10, 0));
            kit.Box(new Vector3(-0.04f, 1.31f, -0.36f), new Vector3(0.1f, 0.04f, 0.36f), s.scarf.Value, new Vector3(-20, -8, 0));
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-70, 70, i / 6f) * Mathf.Deg2Rad;
                kit.Ball(new Vector3(Mathf.Sin(a) * 0.17f, 1.33f - Mathf.Cos(a) * 0.05f, 0.14f + Mathf.Cos(a) * 0.07f), 0.05f, new Color(0.97f, 0.95f, 0.9f));
            }
            Head(s, 1.66f);
            // Riding goggles pushed up on her forehead
            kit.Cyl(new Vector3(0f, 1.86f, -0.01f), new Vector3(0.55f, 0.05f, 0.53f), new Color(0.3f, 0.2f, 0.12f), default, 14);
            for (int side = -1; side <= 1; side += 2)
                kit.Cyl(new Vector3(side * 0.1f, 1.88f, 0.23f), new Vector3(0.15f, 0.06f, 0.15f), new Color(1f, 0.7f, 0.2f), new Vector3(70, 0, 0), 10);
            kit.Frame = Matrix4x4.identity;
            m.body = kit.ToMesh("BikeNonna");
            m.armL = Arm(s, Held.None, true);
            m.armR = Arm(s, Held.None, false);
            return m;
        }

        /// <summary>An arc of bars in the YZ plane (a wheel or mudguard seen from the side); angles from +Z toward +Y.</summary>
        private static void Ring(Vector3 center, float radius, int segments, float thickness, Color color, float fromDeg, float toDeg)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, (float)i / segments) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(fromDeg, toDeg, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                kit.Bar(center + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radius, center + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radius, thickness, color);
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
