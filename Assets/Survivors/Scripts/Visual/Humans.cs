using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum Hat { None, Toque, Boater, Fedora, Cap, PaperHat, Helmet, CaptainHat, Headband, Coppola, Headscarf }
    public enum Held { None, Slipper, Ladle, Oar, Dough, Spoon, RollingPin, Pan, SelfieStick, GoldOar, Flag, Newspaper, ViolinCase }

    /// <summary>Body types change height, shoulders, hips, limb thickness, belly and posture, so a crowd reads as different people.</summary>
    public enum BodyType { Average, Slim, Athletic, Stocky, Heavy, Elder, Lanky, Petite, Brute }
    public enum Sleeves { Long, Short, Rolled }
    public enum Footwear { Leather, Sneakers, Slippers, Granny }

    /// <summary>Temperament, written into the face: brows, lids, mouth, cheeks and lines.</summary>
    public enum Mood { Friendly, Furious, Yelling, Grumpy, Scolding, Haughty, Cheerful, Smug, Singing, Stern, Jolly, Wild, Cool }

    public class HumanSpec
    {
        public BodyType body;
        public bool female;
        public Mood mood = Mood.Grumpy;
        public Color skin = new Color(0.96f, 0.78f, 0.62f), shirt = Color.white, pants = new Color(0.25f, 0.25f, 0.28f),
            shoes = new Color(0.18f, 0.12f, 0.1f), hair = new Color(0.12f, 0.09f, 0.08f), eyes = new Color(0.36f, 0.22f, 0.12f);
        public Color? scarf, apron, skirt, stripes, jacket, tie, hatBand, socks, stockings, lips, shawl, suspenders, sash;
        public int hairStyle;      // 0 short, 1 bun, 2 long, 3 bald ring, 4 ponytail, 5 slicked back, 6 curly, 7 tight perm, 8 curlers
        public int moustache;      // 0 none, 1 normal, 2 grand handlebar, 3 full beard
        public int glasses;        // 0 none, 1 round, 2 sunglasses
        /// <summary>Angry people's free hands make the pinched-fingers gesture.</summary>
        public bool angry = true;
        public bool shorts, facePaint, backpack, belly, buttons, necklace, cigar, earrings, blush, wrinkles, stubble, bag, toothpick, pipe;
        public int jersey;         // number on the back of a football shirt, 0 = none
        public Sleeves sleeves;
        public Footwear footwear;
        public float hunch;
        public float eyeSpacing = 0.1f, noseSize = 1f;
        public Hat hat;
        public Color hatColor = Color.white;
        public Held heldR, heldL;
        /// <summary>Arm rest poses (Euler). Held props are modelled upright for this pose.</summary>
        public Vector3? restL, restR;
        public Color[] flag;
        public Gait gait;
    }

    /// <summary>Measurements in model space (metres before the actor's scale). Ground at y = 0, facing +Z.</summary>
    public struct BodyShape
    {
        public float hipY, hipX, shoulderY, shoulderX, neck, chestW, chestD, waistW, hipW, belly, limb, head, armLen, hunch;
        public float HeadY => shoulderY + neck + 0.25f * head;

        public static BodyShape Of(HumanSpec s)
        {
            var b = new BodyShape
            {
                hipY = 0.8f, hipX = 0.12f, shoulderY = 1.32f, shoulderX = 0.29f, neck = 0.09f,
                chestW = 0.56f, chestD = 0.36f, waistW = 0.48f, hipW = 0.46f, limb = 1f, head = 1f
            };
            switch (s.body)
            {
                case BodyType.Slim:
                    b.hipY = 0.84f; b.shoulderY = 1.36f; b.shoulderX = 0.25f; b.chestW = 0.48f; b.chestD = 0.3f; b.waistW = 0.38f; b.hipW = 0.4f;
                    b.hipX = 0.11f; b.limb = 0.82f; b.head = 0.96f; b.neck = 0.11f;
                    break;
                case BodyType.Athletic:
                    b.hipY = 0.82f; b.shoulderY = 1.36f; b.shoulderX = 0.34f; b.chestW = 0.7f; b.chestD = 0.42f; b.waistW = 0.44f; b.hipW = 0.44f;
                    b.limb = 1.14f; b.head = 0.93f;
                    break;
                case BodyType.Stocky:
                    b.hipY = 0.68f; b.shoulderY = 1.2f; b.shoulderX = 0.33f; b.chestW = 0.66f; b.chestD = 0.46f; b.waistW = 0.6f; b.hipW = 0.58f;
                    b.hipX = 0.15f; b.limb = 1.18f; b.head = 1.04f; b.neck = 0.06f; b.belly = 0.3f;
                    break;
                case BodyType.Heavy:
                    b.hipY = 0.72f; b.shoulderY = 1.27f; b.shoulderX = 0.34f; b.chestW = 0.7f; b.chestD = 0.5f; b.waistW = 0.76f; b.hipW = 0.64f;
                    b.hipX = 0.16f; b.limb = 1.24f; b.head = 1.02f; b.neck = 0.05f; b.belly = 1.1f;
                    break;
                case BodyType.Elder:
                    b.hipY = 0.64f; b.shoulderY = 1.1f; b.shoulderX = 0.25f; b.chestW = 0.5f; b.chestD = 0.4f; b.waistW = 0.54f; b.hipW = 0.58f;
                    b.hipX = 0.14f; b.limb = 0.9f; b.head = 1.08f; b.neck = 0.05f; b.belly = 0.3f; b.hunch = 13f;
                    break;
                case BodyType.Lanky:
                    b.hipY = 0.96f; b.shoulderY = 1.52f; b.shoulderX = 0.27f; b.chestW = 0.48f; b.chestD = 0.3f; b.waistW = 0.38f; b.hipW = 0.4f;
                    b.hipX = 0.11f; b.limb = 0.82f; b.head = 0.92f; b.neck = 0.13f; b.hunch = 5f;
                    break;
                case BodyType.Petite:
                    b.hipY = 0.7f; b.shoulderY = 1.17f; b.shoulderX = 0.24f; b.chestW = 0.46f; b.chestD = 0.32f; b.waistW = 0.38f; b.hipW = 0.5f;
                    b.limb = 0.86f; b.head = 1.04f; b.neck = 0.09f;
                    break;
                case BodyType.Brute:
                    b.hipY = 0.78f; b.shoulderY = 1.38f; b.shoulderX = 0.43f; b.chestW = 0.9f; b.chestD = 0.54f; b.waistW = 0.64f; b.hipW = 0.56f;
                    b.hipX = 0.15f; b.limb = 1.45f; b.head = 0.86f; b.neck = 0.04f;
                    break;
            }
            if (s.female)
            {
                b.shoulderX *= 0.93f; b.chestW *= 0.92f; b.hipW *= 1.1f;
            }
            if (s.belly) b.belly = Mathf.Max(b.belly, 0.75f);
            b.hunch += s.hunch;
            b.armLen = 0.44f * b.shoulderY;
            return b;
        }
    }

    /// <summary>
    /// People and their vehicles. Each type has its own build, posture, signature prop, face and walk, so it can be told apart
    /// from above by silhouette alone. A person is a spine mesh (body and clothes) carrying a head, arms and sometimes a belly
    /// and a prop, plus legs that swing from the hips.
    /// </summary>
    public static partial class Models
    {
        private static readonly List<(Vector3 c, Vector3 size)> shell = new List<(Vector3, Vector3)>();
        private static readonly MeshKit bellyKit = new MeshKit();
        private static float bellyLo = 1f, bellyHi = -1f;
        private static readonly Color Gold = new Color(1f, 0.8f, 0.28f);
        private static readonly Color Steel = new Color(0.78f, 0.8f, 0.84f);
        private static readonly Color Celeste = new Color(0.5f, 0.82f, 0.76f);
        private static readonly Color Ink = new Color(0.14f, 0.07f, 0.06f);
        private static readonly Color MouthDark = new Color(0.33f, 0.05f, 0.07f);
        private static readonly Color Teeth = new Color(0.98f, 0.96f, 0.9f);
        private static readonly Color Tongue = new Color(0.9f, 0.4f, 0.45f);
        // A property, not a field: static initialisers across partial files run in no fixed order.
        private static Color[] Tricolore => new[] { Basil, Color.white, Tomato };

        public static RigMeshes Enemy(EnemyKind kind, int variant = 0)
        {
            variant = Mathf.Clamp(variant, 0, EnemyVariantCount(kind) - 1);
            var key = (kind, variant);
            if (enemyMeshes.TryGetValue(key, out var cached)) return cached;
            RigMeshes m;
            switch (kind)
            {
                case EnemyKind.Signore:
                    // The indignant neighbour: flat cap, pot belly, a rolled-up Gazzetta, leaning back and stomping bow-legged.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Stocky, mood = Mood.Grumpy, shirt = new Color(0.95f, 0.93f, 0.88f), pants = new Color(0.28f, 0.28f, 0.32f),
                        scarf = Tomato, moustache = 1, belly = true, suspenders = new Color(0.55f, 0.18f, 0.16f), stubble = true,
                        hat = Hat.Coppola, hatColor = new Color(0.45f, 0.32f, 0.2f), heldL = Held.Newspaper, restL = new Vector3(6f, 0f, -8f),
                        gait = new Gait { cadence = 0.95f, stride = 30f, bob = 0.075f, lean = -5f, sway = 3f, twist = 7f, armSwing = 26f, legOut = 4f, toeOut = 12f, headNod = 4f, swingL = 0.35f }
                    }, kind, variant);
                    break;
                case EnemyKind.Tifoso:
                    // The football fan: flag held high, leaning into a bouncing sprint, yelling.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Athletic, mood = Mood.Yelling, shirt = Azzurro, pants = Color.white, shorts = true, scarf = new Color(0.95f, 0.95f, 1f),
                        stripes = Azzurro, hair = new Color(0.36f, 0.22f, 0.12f), facePaint = true, shoes = new Color(0.1f, 0.1f, 0.12f),
                        sleeves = Sleeves.Short, footwear = Footwear.Sneakers, socks = Azzurro, jersey = 10, stubble = true,
                        heldL = Held.Flag, restL = new Vector3(-166f, 0f, -12f), flag = Tricolore,
                        gait = new Gait { cadence = 1.25f, stride = 46f, bob = 0.13f, lean = 12f, twist = 12f, armSwing = 50f, headNod = 7f, swingL = 0.1f }
                    }, kind, variant);
                    break;
                case EnemyKind.Mamma:
                    // The mamma: hair in curlers, slipper raised to strike, a huge shopping bag swinging, waddling and scolding.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Stocky, female = true, mood = Mood.Scolding, shirt = new Color(0.78f, 0.18f, 0.22f), skirt = new Color(0.72f, 0.16f, 0.2f),
                        apron = new Color(0.97f, 0.96f, 0.92f), hair = new Color(0.3f, 0.17f, 0.1f), hairStyle = 8, heldR = Held.Slipper,
                        necklace = true, sleeves = Sleeves.Rolled, footwear = Footwear.Slippers, lips = new Color(0.78f, 0.12f, 0.16f),
                        earrings = true, blush = true, bag = true, restR = new Vector3(-150f, 0f, 14f),
                        gait = new Gait { cadence = 1.15f, stride = 24f, bob = 0.045f, lean = 5f, sway = 7f, twist = 5f, armSwing = 24f, toeOut = 6f, headNod = 3f, swingR = 0.12f }
                    }, kind, variant);
                    break;
                case EnemyKind.Chef:
                    // The proud chef: towering toque, enormous belly that wobbles after him, chin up, ladle held like a sceptre.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Heavy, mood = Mood.Haughty, shirt = new Color(0.97f, 0.97f, 0.95f), pants = new Color(0.3f, 0.3f, 0.33f), scarf = Tomato,
                        hat = Hat.Toque, moustache = 2, belly = true, buttons = true, heldR = Held.Ladle, blush = true, restR = new Vector3(-38f, 0f, 8f),
                        gait = new Gait { cadence = 0.82f, stride = 24f, bob = 0.06f, lean = -8f, sway = 6f, twist = 3f, armSwing = 14f, legOut = 7f, toeOut = 18f, belly = 1.2f, headNod = 2f, headPitch = -12f }
                    }, kind, variant);
                    break;
                case EnemyKind.Vespista:
                    m = Vespista(variant);
                    break;
                case EnemyKind.Gondoliere:
                    // The gondolier: wide boater, long oar over the shoulder, upright and singing as he walks.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Athletic, mood = Mood.Singing, shirt = Color.white, stripes = new Color(0.1f, 0.12f, 0.3f), pants = new Color(0.08f, 0.08f, 0.1f),
                        scarf = Tomato, hat = Hat.Boater, hatColor = new Color(0.93f, 0.82f, 0.5f), hatBand = Tomato, moustache = 1, heldR = Held.Oar,
                        sleeves = Sleeves.Short, sash = Tomato, restR = new Vector3(-55f, 0f, 6f),
                        gait = new Gait { cadence = 0.9f, stride = 36f, bob = 0.05f, lean = -3f, sway = 2f, twist = 3f, armSwing = 22f, headRoll = 9f, headPitch = -6f, swingR = 0.12f }
                    }, kind, variant);
                    break;
                case EnemyKind.Pizzaiolo:
                    // The pizzaiolo: dough spinning on one finger overhead, always grinning, trotting so it doesn't fly off.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Stocky, mood = Mood.Cheerful, shirt = new Color(0.98f, 0.98f, 0.98f), pants = new Color(0.2f, 0.22f, 0.3f),
                        apron = new Color(0.99f, 0.99f, 0.97f), scarf = Tomato, hat = Hat.PaperHat, moustache = 1, heldR = Held.Dough, skin = Tan,
                        sleeves = Sleeves.Rolled, blush = true, restR = new Vector3(-172f, 0f, 4f),
                        gait = new Gait { cadence = 1.2f, stride = 28f, bob = 0.035f, lean = 3f, sway = 2f, twist = 4f, armSwing = 26f, swingR = 0.05f, headNod = 3f }
                    }, kind, variant);
                    break;
                case EnemyKind.Mafioso:
                    // The mafioso: a wall of shoulders, chin up, violin case in hand, a slow swagger and a smirk.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Brute, mood = Mood.Smug, shirt = new Color(0.95f, 0.95f, 0.95f), jacket = new Color(0.1f, 0.1f, 0.12f), pants = new Color(0.1f, 0.1f, 0.12f),
                        tie = new Color(0.75f, 0.08f, 0.1f), hat = Hat.Fedora, hatColor = new Color(0.12f, 0.12f, 0.14f), hatBand = new Color(0.5f, 0.1f, 0.1f),
                        glasses = 2, moustache = 1, skin = Tan, hairStyle = 5, stubble = true, toothpick = true, heldL = Held.ViolinCase, restL = new Vector3(4f, 0f, -6f),
                        gait = new Gait { cadence = 0.72f, stride = 28f, bob = 0.03f, lean = -7f, sway = 2f, twist = 14f, armSwing = 9f, armOut = 12f, legOut = 2f, toeOut = 8f, headPitch = -8f, swingL = 0.5f }
                    }, kind, variant);
                    break;
                case EnemyKind.Nonna:
                    // The nonna: tiny and stooped, a big white bun, wooden spoon raised, shuffling and shaking her head.
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Elder, female = true, mood = Mood.Stern, shirt = new Color(0.12f, 0.1f, 0.12f), skirt = new Color(0.1f, 0.09f, 0.11f),
                        scarf = new Color(0.3f, 0.28f, 0.32f), hair = new Color(0.9f, 0.9f, 0.92f), hairStyle = 1, glasses = 1,
                        heldR = Held.Spoon, footwear = Footwear.Granny, stockings = new Color(0.8f, 0.66f, 0.56f), earrings = true, blush = true,
                        wrinkles = true, noseSize = 1.15f, restR = new Vector3(-122f, 0f, 12f),
                        gait = new Gait { cadence = 1.5f, stride = 18f, bob = 0.03f, lean = 3f, sway = 4f, armSwing = 6f, headShake = 12f, swingR = 0.3f }
                    }, kind, variant);
                    break;
                case EnemyKind.BossNonna:
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Elder, female = true, mood = Mood.Furious, shirt = new Color(0.14f, 0.1f, 0.16f), skirt = new Color(0.12f, 0.08f, 0.14f),
                        apron = new Color(0.95f, 0.93f, 0.85f), scarf = new Color(0.55f, 0.1f, 0.18f), hair = new Color(0.92f, 0.92f, 0.94f), hairStyle = 1,
                        glasses = 1, heldR = Held.RollingPin, necklace = true, footwear = Footwear.Granny, stockings = new Color(0.8f, 0.66f, 0.56f),
                        earrings = true, blush = true, wrinkles = true, noseSize = 1.2f, shawl = new Color(0.42f, 0.2f, 0.36f), restR = new Vector3(-155f, 0f, 16f),
                        gait = new Gait { cadence = 0.9f, stride = 30f, bob = 0.07f, lean = 2f, sway = 6f, armSwing = 14f, headShake = 9f, swingR = 0.15f }
                    }, kind);
                    break;
                case EnemyKind.BossCapitano:
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Heavy, mood = Mood.Jolly, shirt = Color.white, stripes = new Color(0.55f, 0.08f, 0.12f), pants = new Color(0.08f, 0.08f, 0.1f),
                        scarf = Azzurro, hat = Hat.CaptainHat, hatColor = new Color(0.1f, 0.12f, 0.25f), moustache = 3, hair = new Color(0.55f, 0.55f, 0.58f),
                        heldR = Held.GoldOar, belly = true, sash = new Color(0.1f, 0.12f, 0.25f), sleeves = Sleeves.Rolled, pipe = true, blush = true,
                        restR = new Vector3(-55f, 0f, 6f),
                        gait = new Gait { cadence = 0.85f, stride = 28f, bob = 0.06f, sway = 10f, legOut = 9f, toeOut = 14f, belly = 1f, armSwing = 20f, headPitch = -6f, headNod = 3f, swingR = 0.12f }
                    }, kind);
                    break;
                case EnemyKind.BossBikeNonna:
                    m = BikeNonna();
                    break;
                case EnemyKind.BossDon:
                    m = Humanoid(new HumanSpec
                    {
                        body = BodyType.Brute, mood = Mood.Smug, shirt = new Color(0.12f, 0.1f, 0.1f), jacket = new Color(0.95f, 0.94f, 0.9f), pants = new Color(0.95f, 0.94f, 0.9f),
                        tie = new Color(0.85f, 0.7f, 0.2f), hat = Hat.Fedora, hatColor = new Color(0.95f, 0.94f, 0.9f), hatBand = new Color(0.1f, 0.1f, 0.1f),
                        glasses = 2, moustache = 2, belly = true, cigar = true, necklace = true, heldR = Held.Pan, skin = Tan, hairStyle = 5,
                        restR = new Vector3(-32f, 0f, 6f),
                        gait = new Gait { cadence = 0.7f, stride = 26f, bob = 0.035f, lean = -8f, twist = 12f, armOut = 10f, belly = 0.6f, headPitch = -7f, swingR = 0.4f }
                    }, kind);
                    break;
                default:
                    m = Barrel();
                    break;
            }
            enemyMeshes[key] = m;
            return m;
        }

        private static Color VariantColor(int variant, Color first, Color second, Color third)
            => variant == 1 ? first : variant == 2 ? second : third;

        private static BodyType VariantBody(int variant, BodyType first, BodyType second, BodyType third)
            => variant == 1 ? first : variant == 2 ? second : third;

        /// <summary>Curated variants keep each type's silhouette, prop and temperament; build, colours, hair and details change. Variant zero is the base look.</summary>
        private static void VaryAppearance(HumanSpec s, EnemyKind kind, int variant)
        {
            if (variant == 0) return;
            s.skin = VariantColor(variant, Tan, new Color(0.76f, 0.53f, 0.36f), Skin);
            s.hair = VariantColor(variant, new Color(0.36f, 0.22f, 0.12f), new Color(0.11f, 0.09f, 0.08f), new Color(0.55f, 0.53f, 0.5f));
            s.eyes = VariantColor(variant, new Color(0.32f, 0.46f, 0.24f), new Color(0.2f, 0.12f, 0.08f), new Color(0.34f, 0.46f, 0.62f));
            s.eyeSpacing = variant == 1 ? 0.105f : variant == 2 ? 0.09f : 0.115f;
            s.noseSize = variant == 1 ? 0.85f : variant == 2 ? 1.25f : 1.1f;
            switch (kind)
            {
                case EnemyKind.Signore:
                    s.body = VariantBody(variant, BodyType.Slim, BodyType.Heavy, BodyType.Elder);
                    s.shirt = VariantColor(variant, new Color(0.4f, 0.58f, 0.38f), new Color(0.88f, 0.63f, 0.22f), new Color(0.58f, 0.18f, 0.24f));
                    s.pants = VariantColor(variant, new Color(0.18f, 0.25f, 0.38f), new Color(0.34f, 0.24f, 0.18f), new Color(0.2f, 0.2f, 0.24f));
                    s.scarf = VariantColor(variant, Azzurro, Basil, new Color(0.93f, 0.82f, 0.55f));
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 2 : 1;
                    s.belly = variant == 2;
                    s.suspenders = variant == 2 ? new Color(0.2f, 0.2f, 0.24f) : (Color?)null;
                    s.glasses = variant == 1 ? 1 : 0;
                    s.hairStyle = variant == 2 ? 3 : 0;
                    s.wrinkles = variant == 3;
                    s.mood = variant == 2 ? Mood.Furious : Mood.Grumpy;
                    s.hat = variant == 2 ? Hat.None : Hat.Coppola;
                    s.hatColor = VariantColor(variant, new Color(0.55f, 0.55f, 0.52f), Color.white, new Color(0.52f, 0.46f, 0.36f));
                    break;
                case EnemyKind.Tifoso:
                    s.body = VariantBody(variant, BodyType.Slim, BodyType.Heavy, BodyType.Lanky);
                    s.shirt = VariantColor(variant, new Color(0.08f, 0.22f, 0.58f), new Color(0.25f, 0.55f, 0.92f), new Color(0.1f, 0.32f, 0.72f));
                    s.stripes = VariantColor(variant, new Color(0.18f, 0.45f, 0.85f), new Color(0.08f, 0.26f, 0.6f), Color.white);
                    s.scarf = VariantColor(variant, Basil, Color.white, Tomato);
                    s.pants = variant == 2 ? new Color(0.12f, 0.2f, 0.4f) : Color.white;
                    s.socks = variant == 2 ? Color.white : s.shirt;
                    s.facePaint = variant != 2;
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 1 : 2;
                    s.stubble = variant != 1;
                    s.hairStyle = variant == 2 ? 3 : variant == 3 ? 6 : 0;
                    s.belly = variant == 2;
                    s.jersey = variant == 2 ? 0 : 10;
                    s.hat = variant == 1 ? Hat.Cap : variant == 3 ? Hat.Headband : Hat.None;
                    s.hatColor = Azzurro;
                    s.flag = variant == 1 ? new[] { Azzurro, Color.white, Azzurro }
                        : variant == 3 ? new[] { new Color(0.62f, 0.08f, 0.12f), new Color(0.98f, 0.72f, 0.1f), new Color(0.62f, 0.08f, 0.12f) } : Tricolore;
                    break;
                case EnemyKind.Mamma:
                    s.body = VariantBody(variant, BodyType.Average, BodyType.Heavy, BodyType.Petite);
                    s.shirt = VariantColor(variant, new Color(0.2f, 0.52f, 0.34f), new Color(0.2f, 0.36f, 0.7f), new Color(0.63f, 0.26f, 0.5f));
                    s.skirt = s.shirt;
                    s.apron = VariantColor(variant, new Color(0.98f, 0.9f, 0.65f), new Color(0.96f, 0.86f, 0.8f), new Color(0.92f, 0.95f, 0.9f));
                    s.hairStyle = variant == 2 ? 8 : 1;
                    s.hat = variant == 2 ? Hat.None : Hat.Headscarf;
                    s.hatColor = VariantColor(variant, Tomato, Azzurro, new Color(0.95f, 0.75f, 0.2f));
                    s.belly = variant == 2;
                    s.glasses = variant == 3 ? 1 : 0;
                    s.necklace = variant != 2;
                    s.lips = VariantColor(variant, new Color(0.85f, 0.3f, 0.35f), new Color(0.65f, 0.1f, 0.18f), new Color(0.9f, 0.4f, 0.5f));
                    break;
                case EnemyKind.Chef:
                    s.body = VariantBody(variant, BodyType.Average, BodyType.Heavy, BodyType.Stocky);
                    s.scarf = VariantColor(variant, Basil, Azzurro, new Color(0.95f, 0.72f, 0.2f));
                    s.pants = VariantColor(variant, new Color(0.1f, 0.16f, 0.27f), new Color(0.24f, 0.18f, 0.16f), new Color(0.16f, 0.17f, 0.19f));
                    if (variant != 3) s.apron = variant == 1 ? new Color(0.16f, 0.18f, 0.22f) : new Color(0.88f, 0.82f, 0.68f);
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 1 : 3;
                    s.stubble = variant == 1;
                    s.belly = variant != 1;
                    s.glasses = variant == 3 ? 1 : 0;
                    s.sleeves = variant == 1 ? Sleeves.Rolled : Sleeves.Long;
                    break;
                case EnemyKind.Gondoliere:
                    s.body = VariantBody(variant, BodyType.Lanky, BodyType.Average, BodyType.Slim);
                    s.stripes = VariantColor(variant, Tomato, new Color(0.08f, 0.24f, 0.22f), new Color(0.06f, 0.07f, 0.1f));
                    s.scarf = VariantColor(variant, Azzurro, Tomato, new Color(0.9f, 0.65f, 0.2f));
                    s.hatBand = s.scarf;
                    s.sash = s.scarf;
                    s.hatColor = VariantColor(variant, new Color(0.98f, 0.93f, 0.74f), new Color(0.76f, 0.6f, 0.32f), new Color(0.94f, 0.9f, 0.82f));
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 2 : 1;
                    s.glasses = variant == 2 ? 2 : 0;
                    s.hairStyle = variant == 3 ? 6 : 0;
                    break;
                case EnemyKind.Pizzaiolo:
                    s.body = VariantBody(variant, BodyType.Athletic, BodyType.Heavy, BodyType.Average);
                    s.apron = VariantColor(variant, Basil, Tomato, new Color(0.95f, 0.82f, 0.5f));
                    s.scarf = VariantColor(variant, Tomato, new Color(0.95f, 0.93f, 0.85f), Basil);
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 2 : 3;
                    s.stubble = variant == 1;
                    s.belly = variant == 2;
                    s.glasses = variant == 3 ? 1 : 0;
                    s.sleeves = variant == 1 ? Sleeves.Short : Sleeves.Rolled;
                    break;
                case EnemyKind.Mafioso:
                    s.body = VariantBody(variant, BodyType.Slim, BodyType.Heavy, BodyType.Athletic);
                    var suit = VariantColor(variant, new Color(0.12f, 0.18f, 0.32f), new Color(0.24f, 0.24f, 0.28f), new Color(0.32f, 0.22f, 0.18f));
                    s.jacket = suit; s.pants = suit; s.hatColor = suit;
                    s.tie = VariantColor(variant, new Color(0.92f, 0.7f, 0.24f), Azzurro, Basil);
                    s.hatBand = s.tie;
                    s.moustache = variant == 1 ? 0 : variant == 2 ? 2 : 1;
                    s.glasses = variant == 1 ? 1 : variant == 2 ? 0 : 2;
                    s.belly = variant == 2;
                    s.cigar = variant == 2;
                    s.toothpick = variant != 2;
                    break;
                case EnemyKind.Nonna:
                    s.shirt = VariantColor(variant, new Color(0.28f, 0.12f, 0.3f), new Color(0.1f, 0.24f, 0.17f), new Color(0.1f, 0.15f, 0.3f));
                    s.skirt = s.shirt;
                    s.scarf = VariantColor(variant, new Color(0.65f, 0.5f, 0.68f), new Color(0.62f, 0.72f, 0.5f), new Color(0.68f, 0.7f, 0.8f));
                    s.hair = VariantColor(variant, new Color(0.96f, 0.95f, 0.92f), new Color(0.7f, 0.7f, 0.72f), new Color(0.84f, 0.82f, 0.8f));
                    s.hairStyle = variant == 2 ? 7 : 1;
                    s.necklace = variant != 2;
                    s.belly = variant == 2;
                    s.shawl = variant == 3 ? new Color(0.78f, 0.72f, 0.62f) : (Color?)null;
                    if (variant == 2) s.apron = new Color(0.87f, 0.84f, 0.74f);
                    break;
                case EnemyKind.Vespista:
                    s.body = VariantBody(variant, BodyType.Slim, BodyType.Stocky, BodyType.Lanky);
                    s.shirt = VariantColor(variant, new Color(0.14f, 0.3f, 0.54f), new Color(0.9f, 0.62f, 0.16f), new Color(0.17f, 0.4f, 0.28f));
                    s.pants = VariantColor(variant, new Color(0.3f, 0.3f, 0.34f), new Color(0.1f, 0.16f, 0.3f), new Color(0.34f, 0.24f, 0.16f));
                    s.hatColor = VariantColor(variant, new Color(0.93f, 0.9f, 0.8f), Azzurro, new Color(0.95f, 0.74f, 0.2f));
                    s.glasses = variant == 1 ? 0 : variant == 2 ? 1 : 2;
                    s.moustache = variant == 3 ? 1 : 0;
                    break;
            }
        }

        public static RigMeshes Player(CharacterDef c, int index)
        {
            var spec = new HumanSpec
            {
                skin = c.skin, shirt = c.shirt, pants = c.pants, hair = c.hair, angry = false, mood = Mood.Friendly, shoes = new Color(0.95f, 0.95f, 0.95f),
                footwear = Footwear.Sneakers, eyes = new Color(0.2f, 0.13f, 0.08f), restL = new Vector3(-62f, 0f, -6f), restR = new Vector3(-62f, 0f, 6f)
            };
            switch (index)
            {
                case 0:
                    spec.body = BodyType.Athletic; spec.hat = Hat.Cap; spec.hatColor = new Color(0.15f, 0.3f, 0.7f); spec.shorts = true; spec.backpack = true;
                    spec.sleeves = Sleeves.Short; spec.socks = Color.white;
                    break;
                case 1:
                    spec.body = BodyType.Petite; spec.female = true; spec.hairStyle = 4; spec.skirt = c.pants; spec.scarf = new Color(0.98f, 0.98f, 0.95f);
                    spec.lips = new Color(0.9f, 0.45f, 0.5f); spec.blush = true; spec.socks = Color.white; spec.sleeves = Sleeves.Rolled;
                    break;
                default:
                    spec.body = BodyType.Stocky; spec.mood = Mood.Cheerful; spec.hat = Hat.Headband; spec.glasses = 1; spec.apron = new Color(0.95f, 0.95f, 0.9f);
                    spec.heldL = Held.SelfieStick; spec.sleeves = Sleeves.Short; spec.stubble = true;
                    break;
            }
            var m = Humanoid(spec, null);
            m.armRest = -62f;
            return m;
        }

        /// <summary>Builds tune the walk: heavy people wobble, elders shuffle, lanky ones stride.</summary>
        private static Gait Tune(Gait source, BodyShape b, BodyType type)
        {
            var g = (source ?? Gait.Plain).Clone();
            switch (type)
            {
                case BodyType.Heavy: g.cadence *= 0.9f; g.bob *= 1.1f; g.sway += 3f; break;
                case BodyType.Elder: g.stride *= 0.65f; g.cadence *= 1.15f; g.bob *= 0.6f; break;
                case BodyType.Lanky: g.stride *= 1.15f; g.cadence *= 0.9f; break;
                case BodyType.Petite: g.cadence *= 1.12f; g.stride *= 0.9f; break;
                case BodyType.Slim: g.cadence *= 1.05f; break;
                case BodyType.Brute: g.twist += 4f; g.armOut += 6f; break;
                case BodyType.Stocky: g.sway += 2f; break;
            }
            if (b.belly >= 0.6f) g.belly = Mathf.Max(g.belly, 0.7f);
            return g;
        }

        // ---------------- people ----------------

        private static RigMeshes Humanoid(HumanSpec s, EnemyKind? kind, int variant = 0)
        {
            if (kind.HasValue) VaryAppearance(s, kind.Value, variant);
            var b = BodyShape.Of(s);
            var m = new RigMeshes
            {
                freeHandL = s.heldL == Held.None, freeHandR = s.heldR == Held.None,
                hipX = b.hipX, hipY = b.hipY, shoulderX = b.shoulderX, spineY = b.hipY,
                gait = Tune(s.gait, b, s.body), restL = s.restL, restR = s.restR
            };
            m.leg = Leg(s, b);

            // Spine space: origin at the hips; the upper body stoops (or arches) from the waist.
            var pivot = new Vector3(0f, b.hipY + 0.1f, 0f);
            var frame = Matrix4x4.Translate(new Vector3(0f, -b.hipY, 0f)) * Matrix4x4.Translate(pivot)
                * Matrix4x4.Rotate(Quaternion.Euler(b.hunch, 0f, 0f)) * Matrix4x4.Translate(-pivot);
            var shoulder = frame.MultiplyPoint3x4(new Vector3(0f, b.shoulderY, 0f));
            m.shoulderY = shoulder.y;
            m.shoulderZ = shoulder.z;

            // A big belly is its own part so it can lag behind each step.
            bool splitBelly = b.belly >= 0.6f;
            var bellyC = new Vector3(0f, b.hipY + 0.24f, 0.03f + 0.08f * b.belly);
            float bellyHalf = (0.4f + 0.06f * b.belly) * 0.5f;
            bellyLo = splitBelly ? bellyC.y - bellyHalf * 0.95f : 1f;
            bellyHi = splitBelly ? bellyC.y + bellyHalf * 0.95f : -1f;
            m.bellyPos = frame.MultiplyPoint3x4(bellyC);
            kit.Clear();
            bellyKit.Clear();
            kit.Frame = frame;
            bellyKit.Frame = Matrix4x4.Translate(-m.bellyPos) * frame;
            Torso(s, b, kind);
            kit.Frame = Matrix4x4.identity;
            m.body = kit.ToMesh("Body");
            if (splitBelly) m.belly = bellyKit.ToMesh("Belly");
            bellyLo = 1f; bellyHi = -1f;

            // The head turns on the neck; a stooped head lifts back up to glare ahead.
            m.headPos = frame.MultiplyPoint3x4(new Vector3(0f, b.shoulderY + b.neck, 0f));
            var headC = new Vector3(0f, b.HeadY, 0.01f);
            kit.Clear();
            kit.Frame = Matrix4x4.Translate(-m.headPos) * frame * Matrix4x4.Translate(headC)
                * Matrix4x4.Rotate(Quaternion.Euler(-b.hunch * 0.7f, 0f, 0f)) * Matrix4x4.Translate(-headC);
            Head(s, headC, b.head);
            kit.Frame = Matrix4x4.identity;
            m.head = kit.ToMesh("Head");

            var restL = s.restL ?? new Vector3(0f, 0f, -4f);
            var restR = s.restR ?? new Vector3(0f, 0f, 4f);
            m.armL = Arm(s, b, s.heldL, true, restL);
            m.armR = Arm(s, b, s.heldR, false, restR);

            // Signature props that move on their own.
            var hand = new Vector3(0f, -b.armLen, 0.06f);
            if (s.heldL == Held.Flag)
            {
                var inv = Quaternion.Inverse(Quaternion.Euler(restL));
                m.prop = FlagCloth(s.flag ?? Tricolore);
                m.propMount = PropMount.ArmL;
                m.propMotion = PropMotion.Wave;
                m.propPos = hand + inv * new Vector3(0f, 1.3f, 0f);
                m.propRot = inv;
            }
            else if (s.heldR == Held.Dough)
            {
                var inv = Quaternion.Inverse(Quaternion.Euler(restR));
                m.prop = DoughDisc();
                m.propMount = PropMount.ArmR;
                m.propMotion = PropMotion.Spin;
                m.propPos = hand + inv * new Vector3(0f, 0.19f, 0.02f);
                m.propRot = inv;
            }
            else if (s.bag)
            {
                m.prop = ShoppingBag();
                m.propMount = PropMount.Spine;
                m.propMotion = PropMotion.Swing;
                m.propPos = frame.MultiplyPoint3x4(new Vector3(-b.shoulderX * 0.8f, b.shoulderY + 0.03f, 0f));
            }
            return m;
        }

        private static Color Tint(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        /// <summary>The kit for a detail at height y: on a wobbling belly or on the body.</summary>
        private static MeshKit On(float y) => y >= bellyLo && y <= bellyHi ? bellyKit : kit;

        private static void Shell(Vector3 c, Vector3 size, Color col, bool onBelly = false)
        {
            (onBelly ? bellyKit : kit).Ball(c, size, col);
            shell.Add((c, size));
        }

        /// <summary>Front (or back) surface depth of the torso shell at (x, y), for placing clothing details on the body.</summary>
        private static float SurfaceZ(float x, float y, bool back = false)
        {
            float best = 0f;
            bool any = false;
            foreach (var (c, size) in shell)
            {
                float dx = (x - c.x) / (size.x * 0.5f), dy = (y - c.y) / (size.y * 0.5f);
                float k = 1f - dx * dx - dy * dy;
                if (k <= 0f) continue;
                float z = back ? c.z - size.z * 0.5f * Mathf.Sqrt(k) : c.z + size.z * 0.5f * Mathf.Sqrt(k);
                if (!any || (back ? z < best : z > best)) best = z;
                any = true;
            }
            return best;
        }

        /// <summary>Elliptical outline of the torso at height y: (width, front z, back z).</summary>
        private static Vector3 Section(float y)
        {
            float w = 0f, f = 0f, bk = 0f;
            foreach (var (c, size) in shell)
            {
                float dy = (y - c.y) / (size.y * 0.5f);
                float k = 1f - dy * dy;
                if (k <= 0f) continue;
                k = Mathf.Sqrt(k);
                w = Mathf.Max(w, size.x * k);
                f = Mathf.Max(f, c.z + size.z * 0.5f * k);
                bk = Mathf.Min(bk, c.z - size.z * 0.5f * k);
            }
            return new Vector3(w, f, bk);
        }

        /// <summary>A flat cloth panel hanging from a to b (in the YZ plane), width along X.</summary>
        private static void Panel(MeshKit k, Vector3 a, Vector3 b, float width, Color col)
        {
            var d = b - a;
            k.Box((a + b) * 0.5f, new Vector3(width, d.magnitude, 0.03f), col, Quaternion.FromToRotation(Vector3.up, d.normalized).eulerAngles);
        }

        /// <summary>A band hugging the torso at height y (belts, stripes, apron strings).</summary>
        private static void Band(float y, float height, Color col, float grow = 0.012f)
        {
            var sec = Section(y);
            On(y).Cyl(new Vector3(0f, y, (sec.y + sec.z) * 0.5f), new Vector3(sec.x + grow, height, sec.y - sec.z + grow), col, default, 14);
        }

        private static void Torso(HumanSpec s, BodyShape b, EnemyKind? kind)
        {
            shell.Clear();
            float hy = b.hipY, sy = b.shoulderY, ty = sy - 0.17f;
            float nk = Mathf.Sqrt(b.limb);
            var top = s.jacket ?? s.shirt;
            var lower = s.skirt ?? s.pants;

            kit.Cyl(new Vector3(0f, sy + b.neck * 0.5f, 0f), new Vector3(0.15f * nk, b.neck + 0.14f, 0.15f * nk), s.skin);
            Shell(new Vector3(0f, hy + 0.05f, -0.01f), new Vector3(b.hipW, 0.3f, b.chestD * 0.86f), lower);
            Shell(new Vector3(0f, hy + 0.22f, 0f), new Vector3(b.waistW, 0.36f, b.chestD * 0.88f), top);
            Shell(new Vector3(0f, ty, 0.01f), new Vector3(b.chestW, 0.46f, b.chestD), top);
            Shell(new Vector3(0f, sy - 0.03f, -0.01f), new Vector3(b.shoulderX * 2f + 0.05f, 0.17f, b.chestD * 0.78f), top);
            if (s.female) Shell(new Vector3(0f, ty - 0.01f, 0.05f), new Vector3(b.chestW * 0.8f, 0.2f, b.chestD * 0.72f), top);
            if (b.belly > 0f)
                Shell(new Vector3(0f, hy + 0.24f, 0.03f + 0.08f * b.belly), new Vector3(b.waistW * 0.96f, 0.4f + 0.06f * b.belly, b.chestD * 0.72f + 0.26f * b.belly),
                    top, bellyHi > bellyLo);

            // Trousers: belt or sash with a buckle. Skirts: a flared skirt with a darker hem.
            if (s.skirt.HasValue)
            {
                float len = hy * 0.8f;
                var c = new Vector3(0f, hy + 0.08f - len * 0.5f, 0.01f);
                float w = b.hipW + 0.34f, d = b.chestD + 0.36f;
                kit.Cone(c, new Vector3(w, len, d), s.skirt.Value, default, 0.6f, 14);
                kit.Cyl(new Vector3(0f, c.y - len * 0.5f + 0.025f, 0.01f), new Vector3(w + 0.025f, 0.05f, d + 0.025f), Tint(s.skirt.Value, 0.7f), default, 14);
            }
            else
            {
                float by = b.belly > 0.5f ? hy + 0.06f : hy + 0.13f;
                if (s.sash.HasValue)
                {
                    Band(by, 0.1f, s.sash.Value, 0.03f);
                    float sz = SurfaceZ(-b.hipW * 0.36f, by);
                    On(by).Box(new Vector3(-b.hipW * 0.38f, by - 0.12f, sz), new Vector3(0.07f, 0.24f, 0.03f), s.sash.Value, new Vector3(-6f, 0f, -8f));
                }
                else if (!s.jacket.HasValue)
                {
                    Band(by, 0.055f, new Color(0.22f, 0.14f, 0.08f));
                    On(by).Box(new Vector3(0f, by, Section(by).y + 0.006f), new Vector3(0.075f, 0.055f, 0.02f), Gold);
                }
            }

            // Shirt stripes wrap the chest; a jacket covers them.
            if (s.stripes.HasValue && s.stripes.Value != s.shirt && !s.jacket.HasValue)
                for (int i = 0; i < 4; i++)
                    Band(hy + 0.24f + i * (sy - hy - 0.26f) / 3.6f, 0.045f, s.stripes.Value, 0.006f);

            float frontNeck = SurfaceZ(0f, sy - 0.04f);
            if (s.jacket.HasValue)
            {
                var jacket = s.jacket.Value;
                float fz = SurfaceZ(0f, sy - 0.14f);
                kit.Box(new Vector3(0f, sy - 0.12f, fz - 0.005f), new Vector3(0.15f, 0.3f, 0.03f), s.shirt, new Vector3(-12f, 0f, 0f));
                if (s.tie.HasValue)
                {
                    kit.Box(new Vector3(0f, sy - 0.02f, fz + 0.01f), new Vector3(0.06f, 0.05f, 0.03f), s.tie.Value);
                    kit.Box(new Vector3(0f, sy - 0.17f, fz + 0.012f), new Vector3(0.07f, 0.26f, 0.02f), s.tie.Value, new Vector3(-12f, 0f, 0f));
                }
                for (int side = -1; side <= 1; side += 2)
                    kit.Box(new Vector3(side * 0.095f, sy - 0.13f, SurfaceZ(side * 0.095f, sy - 0.13f) + 0.004f), new Vector3(0.075f, 0.32f, 0.025f),
                        Tint(jacket, 0.82f), new Vector3(-10f, side * 10f, side * -18f));
                // Jacket skirt over the hips, two buttons and a pocket square.
                kit.Cone(new Vector3(0f, hy + 0.04f, 0f), new Vector3(Mathf.Max(b.hipW, b.waistW) + 0.08f, 0.28f, b.chestD * 0.92f + b.belly * 0.14f), jacket, default, 0.94f, 14);
                for (int i = 0; i < 2; i++)
                {
                    float y = hy + 0.2f + i * 0.11f;
                    On(y).Dot(new Vector3(0.035f, y, SurfaceZ(0.035f, y) + 0.004f), 0.04f, Tint(jacket, 0.6f));
                }
                float px = -b.chestW * 0.28f, py = ty + 0.04f;
                kit.Box(new Vector3(px, py, SurfaceZ(px, py) + 0.004f), new Vector3(0.09f, 0.05f, 0.02f),
                    kind == EnemyKind.BossDon ? new Color(0.95f, 0.1f, 0.1f) : s.tie ?? Color.white, new Vector3(0f, 0f, 12f));
            }
            else if (s.jersey > 0 || (s.sleeves == Sleeves.Short && s.stripes.HasValue))
            {
                kit.Cyl(new Vector3(0f, sy + 0.035f, 0f), new Vector3(0.2f * nk + 0.07f, 0.04f, 0.2f * nk + 0.05f), s.stripes ?? Color.white, default, 12);
            }
            else
            {
                // Pointed shirt collar either side of the neck.
                for (int side = -1; side <= 1; side += 2)
                    kit.Box(new Vector3(side * 0.07f, sy + 0.02f, frontNeck - 0.02f), new Vector3(0.11f, 0.07f, 0.02f), s.shirt, new Vector3(-30f, side * -25f, side * 30f));
            }

            if (s.buttons)
                for (int i = 0; i < 3; i++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * 0.085f, y = ty - 0.12f + i * 0.12f;
                        On(y).Dot(new Vector3(x, y, SurfaceZ(x, y) + 0.005f), 0.05f, new Color(0.7f, 0.7f, 0.72f));
                    }

            if (s.suspenders.HasValue)
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * b.chestW * 0.2f, y0 = hy + 0.12f, y1 = sy + 0.02f, ym = (y0 + y1) * 0.5f;
                    var a = new Vector3(x, y0, SurfaceZ(x, y0) + 0.005f);
                    var mid = new Vector3(x, ym, SurfaceZ(x, ym) + 0.005f);
                    var e = new Vector3(side * b.shoulderX * 0.62f, y1, 0.02f);
                    On(ym).Bar(a, mid, 0.045f, s.suspenders.Value, false);
                    kit.Bar(mid, e, 0.045f, s.suspenders.Value, false);
                    On(y0).Dot(a, 0.04f, Gold);
                }

            if (s.apron.HasValue)
            {
                var ap = s.apron.Value;
                float wy = hy + 0.17f;
                Band(wy, 0.045f, ap);
                float bibTop = ty + 0.14f, bibBottom = wy + 0.02f;
                Panel(kit, new Vector3(0f, bibTop, SurfaceZ(0f, bibTop) + 0.004f), new Vector3(0f, bibBottom, SurfaceZ(0f, bibBottom) + 0.004f), b.chestW * 0.5f, ap);
                for (int side = -1; side <= 1; side += 2)
                    kit.Bar(new Vector3(side * b.chestW * 0.22f, bibTop, SurfaceZ(side * b.chestW * 0.22f, bibTop)),
                        new Vector3(side * 0.08f, sy + 0.06f, -0.02f), 0.025f, ap, false);
                // Panel from the waist down; it follows the flare of a skirt or hangs in front of the thighs.
                float len = hy * 0.62f;
                var from = new Vector3(0f, wy, SurfaceZ(0f, wy) + 0.008f);
                var to = new Vector3(0f, wy - len, 0.15f * b.limb + 0.03f);
                if (s.skirt.HasValue)
                {
                    float skirtLen = hy * 0.8f, d = b.chestD + 0.36f, bottom = hy + 0.08f - skirtLen;
                    to.z = 0.01f + d * 0.5f * Mathf.Lerp(1f, 0.6f, (to.y - bottom) / skirtLen) + 0.02f;
                }
                to.z = Mathf.Max(to.z, from.z - 0.14f);
                Panel(On(wy), from, to, Mathf.Max(b.hipW, b.waistW) * 0.8f, ap);
                float back = Section(wy).z;
                kit.Dot(new Vector3(-0.05f, wy, back - 0.01f), 0.07f, ap);
                kit.Dot(new Vector3(0.05f, wy, back - 0.01f), 0.07f, ap);
                kit.Bar(new Vector3(0f, wy, back - 0.01f), new Vector3(-0.04f, wy - 0.18f, back - 0.03f), 0.035f, ap, false);
                kit.Bar(new Vector3(0f, wy, back - 0.01f), new Vector3(0.05f, wy - 0.16f, back - 0.03f), 0.035f, ap, false);
            }

            if (s.backpack)
            {
                float bz = Section(ty).z;
                var pack = new Color(0.95f, 0.55f, 0.15f);
                kit.Box(new Vector3(0f, ty - 0.02f, bz - 0.12f), new Vector3(0.44f, 0.52f, 0.26f), pack);
                kit.Box(new Vector3(0f, ty + 0.2f, bz - 0.13f), new Vector3(0.46f, 0.12f, 0.28f), Tint(pack, 0.8f));
                kit.Box(new Vector3(0f, ty - 0.12f, bz - 0.26f), new Vector3(0.3f, 0.2f, 0.06f), Tint(pack, 0.85f));
                kit.Cyl(new Vector3(0f, ty + 0.3f, bz - 0.12f), new Vector3(0.16f, 0.5f, 0.16f), new Color(0.2f, 0.45f, 0.3f), new Vector3(0, 0, 90), 10);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 0.15f;
                    kit.Bar(new Vector3(x, sy + 0.03f, -0.02f), new Vector3(x, ty - 0.06f, SurfaceZ(x, ty - 0.06f) + 0.005f), 0.05f, new Color(0.2f, 0.2f, 0.25f), false);
                }
            }

            if (s.jersey > 0)
            {
                // "10" on the back.
                float y = ty - 0.02f, bz = SurfaceZ(0f, y, true) - 0.006f;
                var num = s.stripes.HasValue && s.stripes.Value.grayscale > 0.8f ? Azzurro : Color.white;
                kit.Box(new Vector3(0.07f, y, bz), new Vector3(0.04f, 0.18f, 0.02f), num);
                for (int i = 0; i < 8; i++)
                {
                    float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                    kit.Bar(new Vector3(-0.05f + Mathf.Sin(a0) * 0.05f, y + Mathf.Cos(a0) * 0.075f, bz), new Vector3(-0.05f + Mathf.Sin(a1) * 0.05f, y + Mathf.Cos(a1) * 0.075f, bz), 0.035f, num, false);
                }
            }

            if (s.shawl.HasValue)
            {
                kit.Cone(new Vector3(0f, sy - 0.07f, -0.01f), new Vector3(b.shoulderX * 2f + 0.22f, 0.26f, b.chestD + 0.16f), s.shawl.Value, default, 0.5f, 14);
                kit.Ball(new Vector3(0f, sy - 0.2f, SurfaceZ(0f, sy - 0.2f) + 0.03f), new Vector3(0.12f, 0.1f, 0.08f), s.shawl.Value);
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Lerp(-150f, 150f, i / 8f) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Sin(a) * (b.shoulderX + 0.1f), sy - 0.2f, Mathf.Cos(a) * (b.chestD * 0.5f + 0.07f));
                    kit.Bar(p, p + Vector3.down * 0.08f, 0.02f, Tint(s.shawl.Value, 0.8f), true, 4);
                }
            }

            if (s.scarf.HasValue)
            {
                kit.Cyl(new Vector3(0f, sy + 0.045f, 0f), new Vector3(0.15f * nk + 0.16f, 0.1f, 0.15f * nk + 0.14f), s.scarf.Value, default, 12);
                if (kind == EnemyKind.Tifoso && s.stripes.HasValue)
                {
                    // Long supporter's scarf with the club colours.
                    for (int i = 0; i < 4; i++)
                    {
                        float y = sy - 0.04f - i * 0.085f;
                        kit.Box(new Vector3(0.11f, y, SurfaceZ(0.11f, y) + 0.015f), new Vector3(0.1f, 0.085f, 0.03f), i % 2 == 0 ? s.scarf.Value : s.stripes.Value, new Vector3(-8f, 0f, 6f));
                    }
                }
                else
                {
                    float y = sy - 0.06f;
                    kit.Box(new Vector3(0.07f, y, SurfaceZ(0.07f, y) + 0.02f), new Vector3(0.09f, 0.18f, 0.03f), s.scarf.Value, new Vector3(-12f, 0f, 14f));
                    kit.Ball(new Vector3(0.04f, sy + 0.01f, frontNeck + 0.02f), new Vector3(0.09f, 0.08f, 0.06f), Tint(s.scarf.Value, 0.9f));
                }
            }

            if (s.necklace)
            {
                var bead = kind == EnemyKind.BossDon ? Gold : new Color(0.97f, 0.95f, 0.9f);
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.Lerp(-75f, 75f, i / 8f) * Mathf.Deg2Rad;
                    float x = Mathf.Sin(a) * 0.15f * nk, y = sy - 0.02f - Mathf.Cos(a) * 0.08f;
                    kit.Dot(new Vector3(x, y, SurfaceZ(x, y) + 0.01f), 0.045f, bead);
                }
                if (kind == EnemyKind.BossDon)
                    kit.Box(new Vector3(0f, sy - 0.13f, SurfaceZ(0f, sy - 0.13f) + 0.02f), new Vector3(0.08f, 0.08f, 0.02f), Gold, new Vector3(0, 0, 45));
            }
        }

        private static Mesh Arm(HumanSpec s, BodyShape b, Held held, bool left, Vector3 rest, bool grip = false)
        {
            kit.Clear();
            float L = b.armLen, t = b.limb, hs = Mathf.Sqrt(t);
            var sleeve = s.jacket ?? s.shirt;
            var elbow = new Vector3(0f, -L * 0.47f, -0.015f);
            var wrist = new Vector3(0f, -L * 0.86f, 0.05f);
            var hand = new Vector3(0f, -L, 0.06f);
            kit.Ball(Vector3.zero, new Vector3(0.155f, 0.18f, 0.155f) * t, sleeve);
            if (s.sleeves == Sleeves.Short)
            {
                kit.Taper(Vector3.zero, elbow * 0.55f, 0.16f * t, 0.15f * t, sleeve);
                kit.Taper(Vector3.zero, elbow, 0.13f * t, 0.11f * t, s.skin);
                kit.Ball(elbow, 0.11f * t, s.skin);
                kit.Taper(elbow, wrist, 0.11f * t, 0.085f * t, s.skin);
            }
            else
            {
                kit.Taper(Vector3.zero, elbow, 0.16f * t, 0.13f * t, sleeve);
                kit.Ball(elbow, 0.13f * t, sleeve);
                if (s.sleeves == Sleeves.Rolled)
                {
                    kit.Taper(elbow + new Vector3(0f, 0.03f, 0f), elbow - new Vector3(0f, 0.05f, -0.005f), 0.155f * t, 0.15f * t, Tint(sleeve, 0.92f));
                    kit.Taper(elbow, wrist, 0.115f * t, 0.09f * t, s.skin);
                }
                else
                {
                    kit.Taper(elbow, wrist, 0.13f * t, 0.11f * t, sleeve);
                    var cuff = s.jacket.HasValue ? s.shirt : Tint(sleeve, 0.9f);
                    kit.Taper(wrist - (wrist - elbow).normalized * 0.04f, wrist + (wrist - elbow).normalized * 0.015f, 0.12f * t, 0.115f * t, cuff);
                }
            }

            // Hands: a fist round a prop, the pinched-fingers "ma che vuoi" when angry, or a relaxed open hand.
            float side = left ? 1f : -1f; // towards the body
            if (held != Held.None || grip)
            {
                kit.Ball(hand, new Vector3(0.13f, 0.15f, 0.13f) * hs, s.skin);
                kit.Taper(hand + new Vector3(side * 0.05f, 0.03f, 0.02f) * hs, hand + new Vector3(side * 0.03f, -0.03f, 0.07f) * hs, 0.045f * hs, 0.04f * hs, s.skin, 6);
            }
            else if (s.angry)
            {
                kit.Ball(hand + new Vector3(0f, 0.01f, -0.01f) * hs, new Vector3(0.14f, 0.13f, 0.1f) * hs, s.skin);
                var tip = hand + new Vector3(0f, -0.1f, 0.07f) * hs;
                kit.Taper(hand + new Vector3(0f, -0.03f, 0.02f) * hs, tip, 0.11f * hs, 0.045f * hs, s.skin, 8);
                for (int i = 0; i < 3; i++)
                    kit.Taper(hand + new Vector3((i - 1f) * 0.035f, -0.04f, 0.045f) * hs, tip, 0.035f * hs, 0.025f * hs, Tint(s.skin, 0.96f), 6);
                kit.Taper(hand + new Vector3(side * 0.06f, 0.0f, 0.03f) * hs, tip, 0.042f * hs, 0.028f * hs, s.skin, 6);
                kit.Ball(tip, 0.055f * hs, s.skin);
            }
            else
            {
                kit.Ball(hand, new Vector3(0.12f, 0.12f, 0.08f) * hs, s.skin);
                kit.Ball(hand + new Vector3(0f, -0.075f, 0.01f) * hs, new Vector3(0.11f, 0.11f, 0.07f) * hs, s.skin);
                kit.Taper(hand + new Vector3(side * 0.05f, 0f, 0.02f) * hs, hand + new Vector3(side * 0.06f, -0.06f, 0.05f) * hs, 0.042f * hs, 0.035f * hs, s.skin, 6);
            }

            // Props are modelled upright (+Y up, +Z forward) around the hand as it sits in the arm's rest pose.
            kit.Frame = Matrix4x4.TRS(hand, Quaternion.Inverse(Quaternion.Euler(rest)), Vector3.one);
            var wood = new Color(0.6f, 0.4f, 0.2f);
            switch (held)
            {
                case Held.Slipper:
                    // Raised high, heel in the fist, ready to strike.
                    kit.Box(new Vector3(0f, 0.17f, -0.03f), new Vector3(0.13f, 0.34f, 0.035f), new Color(0.95f, 0.45f, 0.62f), new Vector3(-15f, 0f, 0f));
                    kit.Box(new Vector3(0f, 0.25f, 0.0f), new Vector3(0.14f, 0.09f, 0.06f), new Color(0.3f, 0.55f, 0.9f), new Vector3(-15f, 0f, 0f));
                    kit.Dot(new Vector3(0f, 0.26f, 0.04f), 0.075f, new Color(0.3f, 0.55f, 0.9f));
                    break;
                case Held.Ladle:
                    kit.Bar(new Vector3(0f, -0.06f, -0.02f), new Vector3(0f, 0.42f, 0.2f), 0.035f, Steel);
                    kit.Ball(new Vector3(0f, 0.46f, 0.23f), new Vector3(0.18f, 0.1f, 0.18f), Steel);
                    break;
                case Held.Spoon:
                    // A wooden spoon brandished at shoulder height.
                    kit.Bar(new Vector3(0f, -0.06f, 0f), new Vector3(0f, 0.36f, 0.04f), 0.04f, wood);
                    kit.Ball(new Vector3(0f, 0.43f, 0.05f), new Vector3(0.12f, 0.17f, 0.05f), wood);
                    break;
                case Held.RollingPin:
                    kit.Cyl(new Vector3(0f, 0.02f, 0f), new Vector3(0.05f, 0.16f, 0.05f), Tint(wood, 0.9f), new Vector3(-20f, 0f, 0f), 8);
                    kit.Cyl(new Vector3(0f, 0.36f, -0.12f), new Vector3(0.14f, 0.56f, 0.14f), new Color(0.85f, 0.64f, 0.4f), new Vector3(-20f, 0f, 0f), 10);
                    kit.Cyl(new Vector3(0f, 0.7f, -0.25f), new Vector3(0.05f, 0.16f, 0.05f), Tint(wood, 0.9f), new Vector3(-20f, 0f, 0f), 8);
                    break;
                case Held.Oar:
                case Held.GoldOar:
                    {
                        // A long oar resting over the shoulder, blade high behind.
                        var c = held == Held.GoldOar ? new Color(1f, 0.8f, 0.3f) : new Color(0.55f, 0.36f, 0.18f);
                        var d = new Vector3(0.05f, 0.72f, -0.69f).normalized;
                        kit.Bar(-d * 0.7f, d * 2f, 0.06f, c);
                        kit.Dot(-d * 0.72f, 0.08f, Tint(c, 0.8f));
                        kit.Box(d * 2.18f, new Vector3(0.05f, 0.52f, 0.2f), c, Quaternion.FromToRotation(Vector3.up, d).eulerAngles);
                        break;
                    }
                case Held.Dough:
                    // One finger up; the dough spinning on it is a separate prop.
                    kit.Taper(new Vector3(0f, 0.04f, 0.02f), new Vector3(0f, 0.17f, 0.02f), 0.045f * hs, 0.035f * hs, s.skin, 6);
                    break;
                case Held.Pan:
                    kit.Bar(Vector3.zero, new Vector3(0f, 0.02f, 0.3f), 0.05f, new Color(0.15f, 0.15f, 0.15f));
                    kit.Cyl(new Vector3(0f, 0.03f, 0.55f), new Vector3(0.5f, 0.08f, 0.5f), new Color(0.2f, 0.2f, 0.22f), default, 14);
                    kit.Cyl(new Vector3(0f, 0.06f, 0.55f), new Vector3(0.42f, 0.06f, 0.42f), new Color(1f, 0.9f, 0.55f), default, 14);
                    break;
                case Held.SelfieStick:
                    kit.Bar(Vector3.zero, new Vector3(0f, 0.5f, 0.45f), 0.03f, new Color(0.2f, 0.2f, 0.22f));
                    kit.Box(new Vector3(0f, 0.53f, 0.48f), new Vector3(0.1f, 0.18f, 0.02f), new Color(0.1f, 0.1f, 0.12f), new Vector3(-20f, 0f, 0f));
                    break;
                case Held.Flag:
                    kit.Bar(new Vector3(0f, -0.25f, 0f), new Vector3(0f, 1.36f, 0f), 0.035f, new Color(0.9f, 0.88f, 0.82f));
                    kit.Dot(new Vector3(0f, 1.39f, 0f), 0.07f, Gold);
                    break;
                case Held.Newspaper:
                    // A rolled-up pink Gazzetta, held like a baton.
                    kit.Cyl(new Vector3(0f, -0.02f, 0.1f), new Vector3(0.09f, 0.46f, 0.09f), new Color(0.98f, 0.7f, 0.76f), new Vector3(80f, 0f, 0f), 8);
                    kit.Cyl(new Vector3(0f, 0.0f, 0.22f), new Vector3(0.095f, 0.03f, 0.095f), new Color(0.3f, 0.28f, 0.3f), new Vector3(80f, 0f, 0f), 8);
                    kit.Cyl(new Vector3(0f, -0.02f, 0.05f), new Vector3(0.095f, 0.02f, 0.095f), new Color(0.3f, 0.28f, 0.3f), new Vector3(80f, 0f, 0f), 8);
                    break;
                case Held.ViolinCase:
                    {
                        var black = new Color(0.08f, 0.07f, 0.07f);
                        kit.Bar(new Vector3(0f, -0.03f, -0.05f), new Vector3(0f, -0.03f, 0.05f), 0.03f, black);
                        kit.Ball(new Vector3(0f, -0.18f, -0.16f), new Vector3(0.13f, 0.28f, 0.44f), black);
                        kit.Box(new Vector3(0f, -0.17f, 0.04f), new Vector3(0.12f, 0.19f, 0.3f), black);
                        kit.Ball(new Vector3(0f, -0.16f, 0.2f), new Vector3(0.12f, 0.18f, 0.34f), black);
                        for (int i = -1; i <= 1; i += 2) kit.Dot(new Vector3(0.065f, -0.08f, i * 0.12f), 0.035f, Steel);
                        break;
                    }
            }
            kit.Frame = Matrix4x4.identity;
            return kit.ToMesh(left ? "ArmL" : "ArmR");
        }

        private static Mesh Leg(HumanSpec s, BodyShape b)
        {
            kit.Clear();
            float L = b.hipY, t = b.limb;
            var knee = new Vector3(0f, -L * 0.48f, 0.025f);
            var ankle = new Vector3(0f, -L + 0.11f, 0f);
            if (s.skirt.HasValue || s.shorts)
            {
                var leg = s.stockings ?? s.skin;
                kit.Taper(new Vector3(0f, 0.02f, 0f), knee, 0.18f * t, 0.13f * t, leg);
                kit.Ball(knee, 0.13f * t, leg);
                kit.Taper(knee, ankle, 0.13f * t, 0.095f * t, leg);
                kit.Ball(Vector3.Lerp(knee, ankle, 0.3f) + new Vector3(0f, 0f, -0.02f), new Vector3(0.13f, 0.2f, 0.13f) * t, leg);
                if (s.shorts)
                {
                    kit.Taper(new Vector3(0f, 0.04f, 0f), knee * 0.62f, 0.25f * t, 0.23f * t, s.pants);
                    kit.Taper(knee * 0.6f, knee * 0.64f, 0.235f * t, 0.235f * t, Tint(s.pants, 0.85f));
                }
                if (s.socks.HasValue)
                {
                    kit.Taper(ankle + new Vector3(0f, -0.02f, 0f), ankle + new Vector3(0f, 0.14f, 0.01f), 0.115f * t, 0.12f * t, s.socks.Value);
                    kit.Taper(ankle + new Vector3(0f, 0.1f, 0.01f), ankle + new Vector3(0f, 0.13f, 0.01f), 0.125f * t, 0.125f * t, Color.white);
                }
            }
            else
            {
                kit.Taper(new Vector3(0f, 0.04f, 0f), knee, 0.22f * t, 0.17f * t, s.pants);
                kit.Ball(knee, 0.17f * t, s.pants);
                kit.Taper(knee, ankle + new Vector3(0f, -0.02f, 0f), 0.17f * t, 0.155f * t, s.pants);
                kit.Taper(ankle + new Vector3(0f, -0.03f, 0f), ankle + new Vector3(0f, 0.02f, 0f), 0.165f * t, 0.16f * t, Tint(s.pants, 0.85f));
            }
            Shoe(s, -L, Mathf.Sqrt(t));
            return kit.ToMesh("Leg");
        }

        private static void Shoe(HumanSpec s, float floor, float w)
        {
            var col = s.shoes;
            switch (s.footwear)
            {
                case Footwear.Sneakers:
                    kit.Box(new Vector3(0f, floor + 0.03f, 0.05f), new Vector3(0.16f * w, 0.06f, 0.33f), Color.white);
                    kit.Ball(new Vector3(0f, floor + 0.08f, 0.04f), new Vector3(0.155f * w, 0.12f, 0.31f), col);
                    kit.Ball(new Vector3(0f, floor + 0.06f, 0.16f), new Vector3(0.15f * w, 0.08f, 0.12f), Color.white);
                    for (int i = 0; i < 3; i++)
                        kit.Box(new Vector3(0f, floor + 0.13f - i * 0.012f, 0.03f + i * 0.04f), new Vector3(0.08f * w, 0.015f, 0.02f), Color.white, new Vector3(-20f, 0, 0));
                    break;
                case Footwear.Slippers:
                    // Fluffy house slippers matching the one she throws.
                    kit.Box(new Vector3(0f, floor + 0.02f, 0.05f), new Vector3(0.15f * w, 0.04f, 0.3f), new Color(0.95f, 0.45f, 0.62f));
                    kit.Ball(new Vector3(0f, floor + 0.06f, 0.1f), new Vector3(0.15f * w, 0.08f, 0.18f), new Color(0.95f, 0.45f, 0.62f));
                    kit.Dot(new Vector3(0f, floor + 0.11f, 0.15f), 0.08f, new Color(0.3f, 0.55f, 0.9f));
                    break;
                case Footwear.Granny:
                    kit.Box(new Vector3(0f, floor + 0.035f, -0.06f), new Vector3(0.13f * w, 0.07f, 0.1f), new Color(0.12f, 0.08f, 0.06f));
                    kit.Box(new Vector3(0f, floor + 0.015f, 0.05f), new Vector3(0.15f * w, 0.03f, 0.3f), new Color(0.12f, 0.08f, 0.06f));
                    kit.Ball(new Vector3(0f, floor + 0.07f, 0.05f), new Vector3(0.15f * w, 0.1f, 0.29f), new Color(0.1f, 0.08f, 0.08f));
                    kit.Box(new Vector3(0f, floor + 0.11f, 0.02f), new Vector3(0.155f * w, 0.025f, 0.05f), new Color(0.1f, 0.08f, 0.08f), new Vector3(-10f, 0, 0));
                    kit.Dot(new Vector3(0.07f * w, floor + 0.1f, 0.02f), 0.035f, Gold);
                    break;
                default:
                    kit.Box(new Vector3(0f, floor + 0.02f, 0.05f), new Vector3(0.15f * w, 0.04f, 0.32f), Tint(col, 0.6f));
                    kit.Box(new Vector3(0f, floor + 0.04f, -0.06f), new Vector3(0.14f * w, 0.06f, 0.09f), Tint(col, 0.6f));
                    kit.Ball(new Vector3(0f, floor + 0.07f, 0.05f), new Vector3(0.15f * w, 0.1f, 0.3f), col);
                    kit.Ball(new Vector3(0f, floor + 0.06f, 0.15f), new Vector3(0.14f * w, 0.08f, 0.12f), Tint(col, 1.25f));
                    break;
            }
        }

        // ---------------- props that move ----------------

        /// <summary>Flag cloth streaming back from the top of its pole (origin on the pole).</summary>
        private static Mesh FlagCloth(Color[] colors)
        {
            kit.Clear();
            for (int i = 0; i < 3; i++)
                kit.Box(new Vector3(0f, -0.25f, -0.16f - i * 0.26f), new Vector3(0.025f, 0.5f, 0.262f), colors[i]);
            return kit.ToMesh("FlagCloth");
        }

        /// <summary>Pizza dough with sauce and mozzarella (origin on the fingertip, spinning about Y).</summary>
        private static Mesh DoughDisc()
        {
            kit.Clear();
            kit.Cyl(Vector3.zero, new Vector3(0.62f, 0.035f, 0.62f), new Color(0.98f, 0.9f, 0.72f), default, 16);
            kit.Cyl(new Vector3(0f, 0.012f, 0f), new Vector3(0.47f, 0.03f, 0.47f), Tomato, default, 16);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f, r = 0.08f + (i % 2) * 0.08f;
                kit.Blob(new Vector3(Mathf.Cos(a) * r, 0.03f, Mathf.Sin(a) * r), new Vector3(0.09f, 0.03f, 0.09f), new Color(1f, 0.97f, 0.85f));
            }
            kit.Blob(new Vector3(0.05f, 0.035f, -0.12f), new Vector3(0.07f, 0.015f, 0.04f), Basil, new Vector3(0f, 40f, 0f));
            kit.Blob(new Vector3(-0.12f, 0.035f, 0.04f), new Vector3(0.07f, 0.015f, 0.04f), Basil, new Vector3(0f, -30f, 0f));
            return kit.ToMesh("Dough");
        }

        /// <summary>A huge raffia shopping bag with leeks and bread, hanging from the shoulder (origin at the shoulder).</summary>
        private static Mesh ShoppingBag()
        {
            kit.Clear();
            var raffia = new Color(0.82f, 0.68f, 0.42f);
            var body = new Vector3(-0.22f, -0.68f, 0.02f);
            kit.Bar(Vector3.zero, body + new Vector3(0.03f, 0.17f, 0.18f), 0.03f, Tint(raffia, 0.7f), false);
            kit.Bar(Vector3.zero, body + new Vector3(0.03f, 0.17f, -0.18f), 0.03f, Tint(raffia, 0.7f), false);
            kit.Box(body, new Vector3(0.2f, 0.36f, 0.5f), raffia);
            for (int i = 0; i < 2; i++)
                kit.Box(body + new Vector3(0f, -0.06f + i * 0.12f, 0f), new Vector3(0.205f, 0.04f, 0.505f), i == 0 ? Tomato : Basil);
            // Leeks and a loaf of bread poking out of the top.
            for (int i = 0; i < 2; i++)
            {
                var a = body + new Vector3(0f, 0.12f, 0.12f + i * 0.06f);
                var b = a + new Vector3(0.04f, 0.32f, 0.08f + i * 0.04f);
                kit.Taper(a, Vector3.Lerp(a, b, 0.55f), 0.06f, 0.055f, new Color(0.95f, 0.95f, 0.88f), 6);
                kit.Taper(Vector3.Lerp(a, b, 0.55f), b, 0.055f, 0.08f, Basil, 6, true);
            }
            kit.Ball(body + new Vector3(0f, 0.2f, -0.12f), new Vector3(0.13f, 0.13f, 0.3f), new Color(0.78f, 0.5f, 0.22f), new Vector3(-30f, 0f, 0f));
            return kit.ToMesh("ShoppingBag");
        }

        // ---------------- heads and faces ----------------

        private enum EyeShape { Open, Happy, Shut }
        private enum MouthShape { Smile, Frown, Grit, Shout, Scold, Pursed, Grin, Laugh, Smirk, Sing, GritGrin }

        /// <summary>Front of the face (skull and jaw) at (x, y), in head units.</summary>
        private static float FaceZ(float x, float y, float jawW)
            => Mathf.Max(Front(x, y, 0f, 0f, 0.25f, 0.25f, 0.24f), Front(x, y, -0.12f, 0.05f, jawW, 0.13f, 0.18f));

        private static float Front(float x, float y, float cy, float cz, float rx, float ry, float rz)
        {
            float dx = x / rx, dy = (y - cy) / ry, k = 1f - dx * dx - dy * dy;
            return k > 0f ? cz + rz * Mathf.Sqrt(k) : 0f;
        }

        private static void Head(HumanSpec s, Vector3 c, float h)
        {
            Vector3 P(float x, float y, float z) => c + new Vector3(x, y, z) * h;
            var skin = s.skin;
            var shade = Tint(skin, 0.9f);
            var hair = s.hair;
            bool heavyFace = s.body == BodyType.Heavy || s.body == BodyType.Elder || s.body == BodyType.Brute || s.belly;
            float jawW = heavyFace ? 0.22f : 0.2f;
            Vector3 F(float x, float y, float o) => P(x, y, FaceZ(x, y, jawW) + o);

            kit.Ball(c, new Vector3(0.5f, 0.5f, 0.48f) * h, skin, default, true);
            kit.Ball(P(0f, -0.12f, 0.05f), new Vector3(jawW * 2f, 0.26f, 0.36f) * h, skin);
            if (heavyFace) kit.Ball(P(0f, -0.2f, 0.06f), new Vector3(0.3f, 0.12f, 0.24f) * h, shade);
            if (s.body == BodyType.Brute) kit.Box(P(0f, -0.17f, 0.12f), new Vector3(0.3f, 0.12f, 0.2f) * h, skin);
            if (s.stubble || s.moustache == 3)
                kit.Ball(P(0f, -0.135f, 0.06f), new Vector3(jawW * 2f + 0.01f, 0.24f, 0.36f) * h, s.moustache == 3 ? hair : Color.Lerp(skin, hair, 0.35f));
            if (s.moustache == 3) kit.Ball(P(0f, -0.21f, 0.1f), new Vector3(0.3f, 0.2f, 0.24f) * h, hair);
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Blob(P(side * 0.25f, -0.02f, -0.01f), new Vector3(0.08f, 0.13f, 0.09f) * h, skin);
                kit.Dot(P(side * 0.265f, -0.02f, 0.0f), 0.04f * h, Tint(skin, 0.75f));
                if (s.earrings) kit.Dot(P(side * 0.255f, -0.1f, 0.0f), 0.045f * h, Gold);
            }

            // Nose: bridge and bulb.
            float ns = s.noseSize;
            var nose = Color.Lerp(skin, new Color(0.9f, 0.45f, 0.4f), 0.15f);
            kit.Taper(P(0f, 0.05f, 0.215f), P(0f, -0.035f, 0.255f), 0.05f * h * ns, 0.075f * h * ns, nose, 6);
            kit.Ball(P(0f, -0.045f, 0.25f + 0.01f * ns), new Vector3(0.1f, 0.095f, 0.1f) * h * ns, nose);

            Face(s, c, h, jawW);

            if (s.moustache > 0)
            {
                var mc = hair.grayscale > 0.7f ? new Color(0.72f, 0.72f, 0.74f) : hair;
                float w = s.moustache == 2 ? 0.17f : 0.14f;
                kit.Ball(F(-0.065f, -0.1f, -0.005f), new Vector3(w, 0.06f, 0.06f) * h, mc, new Vector3(0, 0, 14));
                kit.Ball(F(0.065f, -0.1f, -0.005f), new Vector3(w, 0.06f, 0.06f) * h, mc, new Vector3(0, 0, -14));
                if (s.moustache == 2)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        kit.Taper(F(side * 0.13f, -0.12f, -0.01f), F(side * 0.19f, -0.06f, 0.01f), 0.045f * h, 0.03f * h, mc, 6);
                        kit.Dot(F(side * 0.185f, -0.05f, 0.01f), 0.045f * h, mc);
                    }
            }
            if (s.facePaint)
            {
                kit.Box(P(-0.17f, -0.05f, 0.17f), new Vector3(0.025f, 0.08f, 0.02f) * h, new Color(0.1f, 0.6f, 0.25f), new Vector3(0, -35, 0));
                kit.Box(P(-0.155f, -0.05f, 0.185f), new Vector3(0.025f, 0.08f, 0.02f) * h, Color.white, new Vector3(0, -35, 0));
                kit.Box(P(-0.14f, -0.05f, 0.2f), new Vector3(0.025f, 0.08f, 0.02f) * h, Tomato, new Vector3(0, -35, 0));
            }
            if (s.cigar)
            {
                kit.Bar(F(0.05f, -0.165f, 0f), P(0.2f, -0.2f, 0.36f), 0.04f * h, new Color(0.4f, 0.24f, 0.12f));
                kit.Ball(P(0.2f, -0.2f, 0.36f), 0.045f * h, new Color(1f, 0.4f, 0.1f));
            }
            if (s.toothpick) kit.Bar(F(0.05f, -0.16f, 0f), P(0.15f, -0.19f, 0.33f), 0.012f * h, new Color(0.85f, 0.75f, 0.5f), true, 4);
            if (s.pipe)
            {
                kit.Bar(F(-0.05f, -0.165f, 0f), P(-0.13f, -0.24f, 0.36f), 0.03f * h, new Color(0.3f, 0.18f, 0.1f), true, 6);
                kit.Cyl(P(-0.14f, -0.2f, 0.37f), new Vector3(0.08f, 0.1f, 0.08f) * h, new Color(0.4f, 0.22f, 0.12f), default, 8);
            }
            float es = s.eyeSpacing, ey = 0.04f;
            if (s.glasses == 1)
            {
                var g = new Color(0.25f, 0.2f, 0.18f);
                for (int side = -1; side <= 1; side += 2)
                {
                    RingXY(F(side * es, ey, 0.045f), 0.075f * h, 8, 0.02f * h, g);
                    kit.Bar(F(side * (es + 0.075f), ey + 0.01f, 0.035f), P(side * 0.245f, ey + 0.02f, 0.02f), 0.015f * h, g, true, 4);
                }
                kit.Bar(F(-es + 0.075f, ey + 0.01f, 0.045f), F(es - 0.075f, ey + 0.01f, 0.045f), 0.016f * h, g, true, 4);
            }
            else if (s.glasses == 2)
            {
                var g = new Color(0.05f, 0.05f, 0.06f);
                for (int side = -1; side <= 1; side += 2)
                {
                    kit.Ball(F(side * (es + 0.01f), ey - 0.005f, 0.03f), new Vector3(0.16f, 0.1f, 0.03f) * h, g);
                    kit.Bar(F(side * (es + 0.08f), ey + 0.01f, 0.02f), P(side * 0.245f, ey + 0.02f, 0.02f), 0.015f * h, g, true, 4);
                }
                kit.Box(F(0f, ey + 0.025f, 0.035f), new Vector3(0.1f, 0.02f, 0.02f) * h, g);
            }

            Hair(s, c, h, hair);
            HatOn(s, c, h);
        }

        /// <summary>
        /// Brows, lids, mouth, cheeks and lines by temperament: hot-headed people scowl with lowered lids, smug ones look half-asleep
        /// and smirk to one side, cheerful ones squeeze their eyes shut over a big grin.
        /// </summary>
        private static void Face(HumanSpec s, Vector3 c, float h, float jawW)
        {
            Vector3 F(float x, float y, float o) => c + new Vector3(x, y, FaceZ(x, y, jawW) + o) * h;
            void Line(float x0, float y0, float x1, float y1, float t, Color col, float o = 0.014f)
                => kit.Bar(F(x0, y0, o), F(x1, y1, o), t * h, col, true, 6);
            void Curve(float cx, float cy, float halfW, float bend, float tilt, float t, Color col, int seg = 4)
            {
                for (int i = 0; i < seg; i++)
                {
                    float u0 = (float)i / seg * 2f - 1f, u1 = (float)(i + 1) / seg * 2f - 1f;
                    Line(cx + u0 * halfW, cy + bend * (1f - u0 * u0) + tilt * u0, cx + u1 * halfW, cy + bend * (1f - u1 * u1) + tilt * u1, t, col);
                }
            }

            var skin = s.skin;
            var lid = Tint(skin, 0.86f);
            var crease = Tint(skin, 0.6f);
            var lips = s.lips ?? Tint(Color.Lerp(skin, new Color(0.8f, 0.35f, 0.35f), 0.35f), 0.95f);
            var brow = s.hair.grayscale > 0.7f ? new Color(0.66f, 0.66f, 0.68f) : Tint(s.hair, 0.9f);
            float es = s.eyeSpacing, ey = 0.04f, my = -0.158f;

            // Temperament settings: eye shape and openness, lid slant (positive = angry), brows, mouth and extras.
            var eyes = EyeShape.Open;
            float open = 0.85f, slant = 0f, look = 0f, pupil = 1f, eyeW = 1f;
            float raiseL = 0f, raiseR = 0f, tiltL = 0f, tiltR = 0f, browT = 1f;
            var mouth = MouthShape.Smile;
            bool cheeks = false, flare = false, frownLines = false, laughLines = false;
            switch (s.mood)
            {
                case Mood.Furious:
                    open = 0.62f; slant = 24f; raiseL = raiseR = -0.02f; tiltL = tiltR = 30f; browT = 1.35f;
                    mouth = MouthShape.Grit; flare = frownLines = true;
                    break;
                case Mood.Yelling:
                    open = 1.12f; eyeW = 1.12f; pupil = 0.7f; slant = 8f; raiseL = raiseR = 0.03f; tiltL = tiltR = 22f; browT = 1.2f;
                    mouth = MouthShape.Shout;
                    break;
                case Mood.Grumpy:
                    open = 0.5f; slant = 14f; raiseL = raiseR = -0.025f; tiltL = tiltR = 20f; browT = 1.4f;
                    mouth = MouthShape.Frown; frownLines = true;
                    break;
                case Mood.Scolding:
                    open = 0.6f; slant = 6f; raiseL = 0.06f; tiltL = -14f; raiseR = -0.01f; tiltR = 22f; browT = 1.1f;
                    mouth = MouthShape.Scold;
                    break;
                case Mood.Haughty:
                    eyes = EyeShape.Shut; raiseL = raiseR = 0.045f; tiltL = tiltR = -10f;
                    mouth = MouthShape.Pursed;
                    break;
                case Mood.Cheerful:
                    eyes = EyeShape.Happy; raiseL = raiseR = 0.035f; tiltL = tiltR = -12f;
                    mouth = MouthShape.Grin; cheeks = laughLines = true;
                    break;
                case Mood.Smug:
                    open = 0.42f; slant = -6f; look = 0.03f; raiseL = 0.045f; tiltL = -4f; raiseR = -0.01f; tiltR = 14f; browT = 1.15f;
                    mouth = MouthShape.Smirk;
                    break;
                case Mood.Singing:
                    eyes = EyeShape.Shut; raiseL = raiseR = 0.05f; tiltL = tiltR = -16f;
                    mouth = MouthShape.Sing;
                    break;
                case Mood.Stern:
                    open = 0.45f; slant = 10f; raiseL = raiseR = -0.01f; tiltL = tiltR = 12f; browT = 1.3f;
                    mouth = MouthShape.Pursed; frownLines = true;
                    break;
                case Mood.Jolly:
                    eyes = EyeShape.Happy; raiseL = raiseR = 0.04f; tiltL = tiltR = -10f;
                    mouth = MouthShape.Laugh; cheeks = laughLines = true;
                    break;
                case Mood.Wild:
                    open = 1.15f; eyeW = 1.15f; pupil = 0.6f; slant = 14f; tiltL = tiltR = 32f; browT = 1.35f;
                    mouth = MouthShape.GritGrin; cheeks = true;
                    break;
                case Mood.Cool:
                    open = 0.6f; tiltL = tiltR = 6f; raiseR = 0.02f;
                    mouth = MouthShape.Grin; cheeks = true;
                    break;
                default: // Friendly
                    open = 0.9f; slant = -4f; raiseL = raiseR = 0.015f; tiltL = tiltR = -6f;
                    mouth = MouthShape.Smile; cheeks = true;
                    break;
            }

            if (cheeks)
                for (int side = -1; side <= 1; side += 2)
                    kit.Ball(F(side * 0.125f, -0.08f, -0.03f), new Vector3(0.11f, 0.08f, 0.07f) * h, skin);
            if (s.blush)
                for (int side = -1; side <= 1; side += 2)
                    kit.Blob(F(side * 0.135f, cheeks ? -0.075f : -0.07f, 0.006f), new Vector3(0.1f, 0.06f, 0.03f) * h, Color.Lerp(skin, new Color(0.95f, 0.35f, 0.4f), 0.45f));

            // Eyes. Sunglasses hide them, so only the brows show.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * es;
                if (s.glasses == 2) { }
                else if (eyes == EyeShape.Happy) Curve(x, ey - 0.012f, 0.058f, 0.032f, 0f, 0.026f, Ink);
                else if (eyes == EyeShape.Shut)
                {
                    Curve(x, ey - 0.005f, 0.06f, -0.022f, 0f, 0.024f, Ink);
                    Line(x + side * 0.055f, ey - 0.02f, x + side * 0.075f, ey - 0.035f, 0.012f, Ink);
                }
                else
                {
                    kit.Ball(F(x, ey, -0.035f), new Vector3(0.13f * eyeW, 0.145f, 0.1f) * h, Color.white);
                    float ix = x * 0.97f + look;
                    kit.Ball(F(ix, ey - 0.01f, -0.005f), new Vector3(0.078f, 0.085f, 0.04f) * h, s.eyes);
                    kit.Dot(F(ix, ey - 0.01f, 0.008f), 0.042f * pupil * h, new Color(0.05f, 0.04f, 0.04f));
                    kit.Dot(F(ix + 0.016f, ey + 0.012f, 0.016f), 0.018f * h, Color.white);
                    // Upper lid down to a slanted lash line: lowered and slanted inwards for anger, flat and heavy for smugness.
                    float half = 0.068f * eyeW, lash = ey + 0.0725f - 0.145f * (1f - Mathf.Min(open, 1f));
                    float dy = Mathf.Tan(slant * Mathf.Deg2Rad) * half;
                    float xIn = x - side * half, xOut = x + side * half;
                    if (open < 0.99f)
                    {
                        var a = F(xIn, lash - dy, 0.02f);
                        var b = F(xOut, lash + dy, 0.02f);
                        kit.Box((a + b) * 0.5f + Vector3.up * (0.045f * h), new Vector3(2.15f * half, 0.09f, 0.035f) * h, lid, new Vector3(0f, 0f, side * slant));
                    }
                    Line(xIn, lash - dy, xOut, lash + dy, 0.024f, Ink, 0.026f);
                    if (open < 0.7f) Line(x - 0.045f, ey - 0.075f, x + 0.045f, ey - 0.07f, 0.01f, crease, 0.0f);
                }
                float bx = side * (es + 0.005f);
                float raise = side < 0 ? raiseL : raiseR, tilt = side < 0 ? tiltL : tiltR;
                kit.Box(F(bx, ey + 0.12f + raise, 0.006f), new Vector3(0.155f, 0.045f * browT, 0.05f) * h, brow, new Vector3(0f, 0f, side * tilt));
                if (laughLines)
                {
                    Line(side * (es + 0.075f), ey + 0.0f, side * (es + 0.105f), ey + 0.02f, 0.009f, crease, 0.0f);
                    Line(side * (es + 0.075f), ey - 0.02f, side * (es + 0.105f), ey - 0.035f, 0.009f, crease, 0.0f);
                }
            }
            if (frownLines)
                for (int side = -1; side <= 1; side += 2)
                    Line(side * 0.018f, ey + 0.07f, side * 0.012f, ey + 0.115f, 0.009f, crease, 0.004f);
            if (s.wrinkles)
                for (int i = 0; i < 2; i++)
                    Curve(0f, 0.19f + i * 0.035f, 0.08f, 0.01f, 0f, 0.01f, crease, 3);
            if (flare)
                for (int side = -1; side <= 1; side += 2)
                    kit.Blob(F(side * 0.045f, -0.06f, 0.0f), new Vector3(0.05f, 0.045f, 0.045f) * h, Color.Lerp(skin, new Color(0.9f, 0.45f, 0.4f), 0.2f));

            switch (mouth)
            {
                case MouthShape.Smile:
                    if (s.lips.HasValue) Curve(0f, my + 0.012f, 0.07f, -0.03f, 0f, 0.04f, lips);
                    Curve(0f, my + 0.012f, 0.065f, -0.03f, 0f, 0.022f, MouthDark);
                    break;
                case MouthShape.Frown:
                    if (s.lips.HasValue) Curve(0f, my - 0.008f, 0.065f, 0.03f, 0f, 0.04f, lips);
                    Curve(0f, my - 0.01f, 0.06f, 0.03f, 0f, 0.026f, MouthDark);
                    for (int side = -1; side <= 1; side += 2) Line(side * 0.075f, my - 0.03f, side * 0.09f, my - 0.055f, 0.01f, crease, 0.0f);
                    break;
                case MouthShape.Grit:
                    kit.Ball(F(0f, my, -0.014f), new Vector3(0.16f, 0.07f, 0.04f) * h, MouthDark);
                    kit.Box(F(0f, my + 0.002f, 0.006f), new Vector3(0.13f, 0.044f, 0.02f) * h, Teeth);
                    Line(-0.062f, my + 0.002f, 0.062f, my + 0.002f, 0.007f, MouthDark, 0.018f);
                    for (int i = -1; i <= 1; i++) Line(i * 0.032f, my + 0.02f, i * 0.032f, my - 0.016f, 0.006f, MouthDark, 0.018f);
                    for (int side = -1; side <= 1; side += 2) Line(side * 0.078f, my + 0.004f, side * 0.092f, my - 0.024f, 0.016f, crease, 0.004f);
                    break;
                case MouthShape.Shout:
                    if (s.lips.HasValue) kit.Ball(F(0f, my - 0.008f, -0.02f), new Vector3(0.18f, 0.145f, 0.05f) * h, lips);
                    kit.Ball(F(0f, my - 0.008f, -0.014f), new Vector3(0.15f, 0.125f, 0.05f) * h, MouthDark);
                    kit.Box(F(0f, my + 0.04f, 0.0f), new Vector3(0.1f, 0.024f, 0.02f) * h, Teeth);
                    kit.Blob(F(0f, my - 0.045f, -0.004f), new Vector3(0.085f, 0.035f, 0.03f) * h, Tongue);
                    break;
                case MouthShape.Scold:
                    if (s.lips.HasValue) kit.Ball(F(0.012f, my, -0.018f), new Vector3(0.15f, 0.1f, 0.045f) * h, lips, new Vector3(0f, 0f, 14f));
                    kit.Ball(F(0.012f, my, -0.013f), new Vector3(0.12f, 0.075f, 0.045f) * h, MouthDark, new Vector3(0f, 0f, 14f));
                    kit.Box(F(0.012f, my + 0.022f, 0.002f), new Vector3(0.08f, 0.018f, 0.02f) * h, Teeth, new Vector3(0f, 0f, 14f));
                    break;
                case MouthShape.Pursed:
                    kit.Blob(F(0f, my, -0.002f), new Vector3(0.07f, 0.03f, 0.03f) * h, lips);
                    Line(-0.032f, my, 0.032f, my, 0.012f, MouthDark, 0.012f);
                    for (int i = -1; i <= 1; i++) Line(i * 0.02f, my + 0.02f, i * 0.024f, my + 0.04f, 0.007f, crease, 0.0f);
                    for (int side = -1; side <= 1; side += 2) Line(side * 0.034f, my, side * 0.052f, my - 0.016f, 0.011f, crease, 0.004f);
                    break;
                case MouthShape.Grin:
                case MouthShape.Laugh:
                    {
                        bool big = mouth == MouthShape.Laugh;
                        float w = big ? 0.18f : 0.16f, hh = big ? 0.12f : 0.09f;
                        if (s.lips.HasValue) kit.Ball(F(0f, my - 0.008f, -0.018f), new Vector3(w + 0.03f, hh + 0.025f, 0.045f) * h, lips);
                        kit.Ball(F(0f, my - 0.008f, -0.013f), new Vector3(w, hh, 0.045f) * h, MouthDark);
                        kit.Box(F(0f, my + hh * 0.3f, 0.004f), new Vector3(w * 0.85f, 0.03f, 0.02f) * h, Teeth);
                        kit.Blob(F(0f, my - hh * 0.32f, -0.002f), new Vector3(w * 0.5f, 0.035f, 0.03f) * h, Tongue);
                        for (int side = -1; side <= 1; side += 2)
                            Line(side * (w * 0.5f - 0.005f), my + 0.02f, side * (w * 0.5f + 0.012f), my + 0.042f, 0.016f, crease, 0.004f);
                        break;
                    }
                case MouthShape.Smirk:
                    if (s.lips.HasValue) Curve(0.005f, my, 0.065f, -0.012f, 0.018f, 0.038f, lips);
                    Curve(0.005f, my, 0.062f, -0.012f, 0.018f, 0.022f, MouthDark);
                    kit.Dot(F(0.085f, my + 0.028f, 0.004f), 0.022f * h, crease);
                    break;
                case MouthShape.Sing:
                    kit.Ball(F(0f, my - 0.012f, -0.018f), new Vector3(0.1f, 0.12f, 0.045f) * h, lips);
                    kit.Ball(F(0f, my - 0.012f, -0.012f), new Vector3(0.072f, 0.09f, 0.045f) * h, MouthDark);
                    kit.Blob(F(0f, my - 0.04f, -0.006f), new Vector3(0.045f, 0.022f, 0.02f) * h, Tongue);
                    break;
                case MouthShape.GritGrin:
                    kit.Ball(F(0f, my + 0.004f, -0.014f), new Vector3(0.21f, 0.085f, 0.04f) * h, MouthDark);
                    kit.Box(F(0f, my + 0.006f, 0.006f), new Vector3(0.18f, 0.05f, 0.02f) * h, Teeth);
                    Line(-0.085f, my + 0.006f, 0.085f, my + 0.006f, 0.007f, MouthDark, 0.018f);
                    for (int i = -2; i <= 2; i++) Line(i * 0.034f, my + 0.028f, i * 0.034f, my - 0.016f, 0.006f, MouthDark, 0.018f);
                    for (int side = -1; side <= 1; side += 2) Line(side * 0.1f, my + 0.01f, side * 0.118f, my + 0.042f, 0.016f, crease, 0.004f);
                    break;
            }
        }

        private static void Hair(HumanSpec s, Vector3 c, float h, Color hair)
        {
            Vector3 P(float x, float y, float z) => c + new Vector3(x, y, z) * h;
            var dark = Tint(hair, 0.85f);
            if (s.hairStyle != 3)
                for (int side = -1; side <= 1; side += 2)
                    kit.Box(P(side * 0.235f, 0.0f, 0.04f), new Vector3(0.04f, 0.12f, 0.08f) * h, hair);
            switch (s.hairStyle)
            {
                case 1: // a coiled bun at the back of the crown with a hairpin; elders wear a big one
                    {
                        float bun = s.body == BodyType.Elder ? 0.29f : 0.24f;
                        var at = P(0f, 0.15f + bun * 0.2f, -0.24f);
                        kit.Ball(P(0f, 0.1f, -0.03f), new Vector3(0.54f, 0.38f, 0.52f) * h, hair);
                        kit.Ball(at, new Vector3(1f, 0.85f, 1f) * bun * h, hair, new Vector3(-35f, 0f, 0f));
                        kit.Cyl(at + new Vector3(0f, 0.01f, -0.01f) * h, new Vector3(1.04f, 0.18f, 1.04f) * bun * h, dark, new Vector3(-35f, 0f, 0f), 12);
                        kit.Cyl(at + new Vector3(0f, -0.05f, 0.03f) * h, new Vector3(0.94f, 0.12f, 0.94f) * bun * h, dark, new Vector3(-35f, 0f, 0f), 12);
                        kit.Bar(at + new Vector3(-bun * 0.65f, 0.05f, 0.04f) * h, at + new Vector3(bun * 0.65f, 0.09f, -0.03f) * h, 0.015f * h, new Color(0.15f, 0.12f, 0.1f));
                        break;
                    }
                case 2: // long, falling to the shoulders
                case 4: // ponytail
                    kit.Ball(P(0f, 0.09f, -0.03f), new Vector3(0.55f, 0.4f, 0.54f) * h, hair);
                    kit.Ball(P(0.03f, 0.2f, 0.12f), new Vector3(0.38f, 0.1f, 0.2f) * h, hair, new Vector3(0, 0, -10));
                    if (s.hairStyle == 2)
                    {
                        kit.Box(P(0f, -0.15f, -0.16f), new Vector3(0.5f, 0.5f, 0.2f) * h, hair);
                        for (int side = -1; side <= 1; side += 2)
                            kit.Ball(P(side * 0.23f, -0.12f, 0.02f), new Vector3(0.1f, 0.4f, 0.16f) * h, hair);
                    }
                    else
                    {
                        kit.Cyl(P(0f, 0.1f, -0.26f), new Vector3(0.1f, 0.05f, 0.1f) * h, new Color(0.9f, 0.25f, 0.3f), new Vector3(70, 0, 0), 8);
                        kit.Taper(P(0f, 0.1f, -0.27f), P(0f, -0.3f, -0.36f), 0.16f * h, 0.06f * h, hair);
                    }
                    break;
                case 3: // bald with a ring of hair and a shiny pate
                    kit.Ball(P(0f, -0.02f, -0.06f), new Vector3(0.53f, 0.22f, 0.49f) * h, hair);
                    kit.Dot(P(0.06f, 0.22f, 0.06f), 0.06f * h, Tint(s.skin, 1.12f));
                    break;
                case 5: // slicked back
                    kit.Ball(P(0f, 0.1f, -0.05f), new Vector3(0.54f, 0.38f, 0.53f) * h, hair);
                    kit.Ball(P(0f, 0.2f, 0.06f), new Vector3(0.42f, 0.16f, 0.3f) * h, hair, new Vector3(-15, 0, 0));
                    kit.Dot(P(-0.05f, 0.27f, 0.04f), 0.05f * h, Tint(hair, 1.6f));
                    break;
                case 6: // curly
                    kit.Ball(P(0f, 0.09f, -0.04f), new Vector3(0.53f, 0.38f, 0.52f) * h, hair);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * 2.4f, r = 0.12f + (i % 3) * 0.05f;
                        kit.Blob(P(Mathf.Cos(a) * r, 0.2f + (i % 2) * 0.04f - r * 0.3f, Mathf.Sin(a) * r * 0.9f - 0.04f), Vector3.one * 0.14f * h, i % 2 == 0 ? hair : dark, new Vector3(0f, i * 40f, 0f));
                    }
                    break;
                case 7: // tight grey perm
                    kit.Ball(P(0f, 0.08f, -0.04f), new Vector3(0.54f, 0.38f, 0.52f) * h, hair);
                    for (int ring = 0; ring < 2; ring++)
                        for (int i = 0; i < 9; i++)
                        {
                            float a = i / 9f * Mathf.PI * 2f + ring * 0.35f, r = ring == 0 ? 0.22f : 0.12f;
                            kit.Dot(P(Mathf.Cos(a) * r, 0.14f + ring * 0.1f, Mathf.Sin(a) * r - 0.04f), 0.12f * h, ring == 0 ? hair : dark);
                        }
                    break;
                case 8: // rows of pink, blue and yellow curlers over the crown
                    {
                        kit.Ball(P(0f, 0.09f, -0.04f), new Vector3(0.54f, 0.38f, 0.52f) * h, hair);
                        Color[] curl = { new Color(0.97f, 0.5f, 0.7f), new Color(0.5f, 0.75f, 0.97f), new Color(0.98f, 0.85f, 0.3f) };
                        for (int i = 0; i < 5; i++)
                        {
                            float a = Mathf.Lerp(55f, 160f, i / 4f) * Mathf.Deg2Rad;
                            kit.Cyl(P(0f, Mathf.Sin(a) * 0.27f + 0.02f, Mathf.Cos(a) * 0.27f - 0.03f), new Vector3(0.1f, 0.24f, 0.1f) * h, curl[i % 3], new Vector3(0f, 0f, 90f), 8);
                        }
                        for (int side = -1; side <= 1; side += 2)
                            kit.Cyl(P(side * 0.22f, 0.13f, -0.06f), new Vector3(0.1f, 0.2f, 0.1f) * h, curl[(side + 3) % 3], new Vector3(90f, 0f, 0f), 8);
                        break;
                    }
                default: // short, side parted
                    kit.Ball(P(0f, 0.1f, -0.04f), new Vector3(0.53f, 0.36f, 0.52f) * h, hair);
                    kit.Ball(P(0.04f, 0.19f, 0.1f), new Vector3(0.38f, 0.12f, 0.22f) * h, hair, new Vector3(0, 0, -12));
                    break;
            }
        }

        private static void HatOn(HumanSpec s, Vector3 c, float h)
        {
            Vector3 P(float x, float y, float z) => c + new Vector3(x, y, z) * h;
            const float top = 0.24f;
            var band = s.hatBand ?? new Color(0.2f, 0.2f, 0.2f);
            switch (s.hat)
            {
                case Hat.Toque:
                    kit.Cyl(P(0, top + 0.14f, 0), new Vector3(0.44f, 0.34f, 0.44f) * h, s.hatColor, default, 14);
                    kit.Ball(P(0, top + 0.42f, 0), new Vector3(0.62f, 0.38f, 0.62f) * h, s.hatColor);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        kit.Ball(P(Mathf.Cos(a) * 0.19f, top + 0.46f, Mathf.Sin(a) * 0.19f), 0.22f * h, s.hatColor);
                    }
                    kit.Cyl(P(0, top - 0.01f, 0), new Vector3(0.48f, 0.06f, 0.48f) * h, Tint(s.hatColor, 0.92f), default, 14);
                    break;
                case Hat.Boater:
                    kit.Cyl(P(0, top - 0.02f, 0), new Vector3(0.95f, 0.035f, 0.95f) * h, s.hatColor, default, 18);
                    kit.Cyl(P(0, top + 0.07f, 0), new Vector3(0.48f, 0.16f, 0.48f) * h, s.hatColor, default, 16);
                    kit.Cyl(P(0, top + 0.03f, 0), new Vector3(0.49f, 0.06f, 0.49f) * h, band, default, 16);
                    kit.Box(P(0.2f, top + 0.03f, -0.1f), new Vector3(0.03f, 0.05f, 0.14f) * h, band, new Vector3(0, -30f, 0));
                    for (int side = -1; side <= 1; side += 2)
                        kit.Bar(P(side * 0.2f, top + 0.0f, -0.24f), P(side * 0.3f, top - 0.18f, -0.42f), 0.03f * h, band, false);
                    break;
                case Hat.Fedora:
                    kit.Cyl(P(0, top - 0.03f, 0), new Vector3(0.74f, 0.035f, 0.7f) * h, s.hatColor, default, 16);
                    kit.Cone(P(0, top + 0.1f, 0), new Vector3(0.46f, 0.24f, 0.44f) * h, s.hatColor, default, 0.8f, 12);
                    kit.Box(P(0, top + 0.21f, 0), new Vector3(0.06f, 0.04f, 0.28f) * h, Tint(s.hatColor, 0.75f));
                    kit.Cyl(P(0, top + 0.02f, 0), new Vector3(0.47f, 0.06f, 0.45f) * h, band, default, 12);
                    break;
                case Hat.Cap:
                    kit.Ball(P(0, top - 0.04f, -0.02f), new Vector3(0.55f, 0.3f, 0.55f) * h, s.hatColor);
                    kit.Box(P(0, top - 0.1f, 0.3f), new Vector3(0.34f, 0.03f, 0.22f) * h, s.hatColor, new Vector3(8f, 0, 0));
                    kit.Dot(P(0, top + 0.11f, -0.02f), 0.05f * h, Tint(s.hatColor, 0.7f));
                    break;
                case Hat.Coppola:
                    kit.Ball(P(0, top - 0.06f, 0.04f), new Vector3(0.6f, 0.2f, 0.64f) * h, s.hatColor, new Vector3(10f, 0, 0));
                    kit.Box(P(0, top - 0.11f, 0.3f), new Vector3(0.38f, 0.03f, 0.15f) * h, Tint(s.hatColor, 0.85f), new Vector3(14f, 0, 0));
                    kit.Dot(P(0, top + 0.03f, 0.08f), 0.045f * h, Tint(s.hatColor, 0.75f));
                    break;
                case Hat.Headscarf:
                    // A cotton headscarf over the bun, polka dots, knotted under the chin.
                    kit.Ball(P(0f, 0.09f, -0.04f), new Vector3(0.57f, 0.44f, 0.56f) * h, s.hatColor);
                    kit.Ball(P(0f, 0.25f, -0.22f), 0.26f * h, s.hatColor);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 0.9f;
                        kit.Dot(P(Mathf.Cos(a) * 0.16f, 0.27f - (i % 2) * 0.05f, Mathf.Sin(a) * 0.14f - 0.04f), 0.05f * h, Color.white);
                    }
                    for (int side = -1; side <= 1; side += 2)
                        kit.Bar(P(side * 0.24f, -0.02f, 0.02f), P(side * 0.07f, -0.27f, 0.07f), 0.05f * h, s.hatColor, false);
                    kit.Blob(P(0f, -0.28f, 0.08f), new Vector3(0.09f, 0.06f, 0.06f) * h, Tint(s.hatColor, 0.85f));
                    break;
                case Hat.PaperHat:
                    kit.Cyl(P(0, top + 0.02f, 0), new Vector3(0.5f, 0.18f, 0.5f) * h, Color.white, default, 14);
                    kit.Cyl(P(0, top - 0.04f, 0), new Vector3(0.52f, 0.04f, 0.52f) * h, Tomato, default, 14);
                    break;
                case Hat.Helmet:
                    kit.Ball(P(0, top - 0.06f, -0.02f), new Vector3(0.6f, 0.44f, 0.6f) * h, s.hatColor);
                    kit.Box(P(0, top + 0.08f, 0f), new Vector3(0.08f, 0.2f, 0.56f) * h, Color.white);
                    kit.Box(P(0, top - 0.1f, 0.3f), new Vector3(0.4f, 0.04f, 0.1f) * h, Tint(s.hatColor, 0.75f), new Vector3(15f, 0, 0));
                    for (int side = -1; side <= 1; side += 2)
                        kit.Bar(P(side * 0.25f, -0.02f, 0.02f), P(side * 0.12f, -0.27f, 0.08f), 0.02f * h, new Color(0.25f, 0.2f, 0.15f));
                    break;
                case Hat.CaptainHat:
                    kit.Cyl(P(0, top + 0.05f, 0.02f), new Vector3(0.58f, 0.18f, 0.58f) * h, Color.white, default, 14);
                    kit.Cyl(P(0, top - 0.02f, 0.02f), new Vector3(0.52f, 0.08f, 0.52f) * h, s.hatColor, default, 14);
                    kit.Box(P(0, top - 0.05f, 0.27f), new Vector3(0.36f, 0.03f, 0.16f) * h, new Color(0.05f, 0.05f, 0.08f), new Vector3(15, 0, 0));
                    kit.Ball(P(0, top + 0.04f, 0.29f), new Vector3(0.1f, 0.1f, 0.04f) * h, Gold);
                    kit.Bar(P(-0.2f, top - 0.03f, 0.25f), P(0.2f, top - 0.03f, 0.25f), 0.02f * h, Gold);
                    break;
                case Hat.Headband:
                    kit.Cyl(P(0, 0.13f, 0), new Vector3(0.54f, 0.07f, 0.52f) * h, Color.white, default, 14);
                    kit.Ball(P(0, 0.13f, 0.25f), new Vector3(0.08f, 0.08f, 0.03f) * h, Tomato);
                    break;
            }
        }

        /// <summary>A ring of bars facing +Z (spectacles).</summary>
        private static void RingXY(Vector3 center, float radius, int segments, float thickness, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                kit.Bar(center + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius, center + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius, thickness, color, true, 4);
            }
        }

        /// <summary>An arc of bars in the YZ plane (a wheel or mudguard seen from the side); angles from +Z toward +Y.</summary>
        private static void Ring(Vector3 center, float radius, int segments, float thickness, Color color, float fromDeg, float toDeg, int sides = 6)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Lerp(fromDeg, toDeg, (float)i / segments) * Mathf.Deg2Rad;
                float a1 = Mathf.Lerp(fromDeg, toDeg, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                kit.Bar(center + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radius, center + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radius, thickness, color, true, sides);
            }
        }

        // ---------------- vehicles ----------------

        private static RigMeshes Vespista(int variant)
        {
            var s = new HumanSpec
            {
                mood = Mood.Cool, shirt = new Color(0.45f, 0.28f, 0.16f), pants = new Color(0.2f, 0.25f, 0.4f), glasses = 2, hair = new Color(0.15f, 0.1f, 0.08f),
                hat = Hat.Helmet, hatColor = new Color(0.92f, 0.22f, 0.2f), skin = Tan, scarf = new Color(0.95f, 0.95f, 0.9f), stubble = true
            };
            VaryAppearance(s, EnemyKind.Vespista, variant);
            s.hunch = 10f;
            var b = BodyShape.Of(s);
            const float wr = 0.21f;
            var m = new RigMeshes
            {
                seated = true, armRest = -64f, leg = null, shoulderX = b.shoulderX,
                wheelPos = new[] { new Vector3(0f, wr, 0.62f), new Vector3(0f, wr, -0.58f) }, wheelRadius = wr,
                gait = new Gait { bob = 0.015f, roll = 7f, headNod = 2f, headSteer = 8f }
            };

            // Small pressed-steel wheel with a chrome hub and wheel nuts that show it turning.
            kit.Clear();
            kit.Cyl(Vector3.zero, new Vector3(0.42f, 0.13f, 0.42f), new Color(0.1f, 0.1f, 0.1f), new Vector3(0, 0, 90), 14);
            kit.Cyl(Vector3.zero, new Vector3(0.28f, 0.145f, 0.28f), new Color(0.72f, 0.73f, 0.76f), new Vector3(0, 0, 90), 12);
            kit.Cyl(Vector3.zero, new Vector3(0.12f, 0.17f, 0.12f), Steel, new Vector3(0, 0, 90), 8);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                    kit.Dot(new Vector3(side * 0.075f, Mathf.Sin(a) * 0.09f, Mathf.Cos(a) * 0.09f), 0.035f, new Color(0.3f, 0.3f, 0.32f));
            }
            m.wheel = kit.ToMesh("ScooterWheel");

            kit.Clear();
            var paint = variant == 0 ? new Color(0.55f, 0.85f, 0.75f) : VariantColor(variant, Tomato, Azzurro, new Color(0.95f, 0.89f, 0.72f));
            var rubber = new Color(0.14f, 0.14f, 0.15f);
            // Floorboard, curved legshield, front mudguard and headset with a round lamp.
            kit.Box(new Vector3(0f, 0.3f, 0.1f), new Vector3(0.42f, 0.07f, 0.74f), Tint(paint, 0.9f));
            for (int i = 0; i < 3; i++) kit.Box(new Vector3(-0.12f + i * 0.12f, 0.34f, 0.1f), new Vector3(0.03f, 0.02f, 0.66f), rubber);
            kit.Box(new Vector3(0f, 0.7f, 0.5f), new Vector3(0.5f, 0.76f, 0.08f), paint, new Vector3(-10f, 0f, 0f));
            for (int side = -1; side <= 1; side += 2)
                kit.Box(new Vector3(side * 0.27f, 0.7f, 0.47f), new Vector3(0.06f, 0.76f, 0.12f), paint, new Vector3(-10f, side * 30f, 0f));
            kit.Ball(new Vector3(0f, 0.38f, 0.64f), new Vector3(0.2f, 0.32f, 0.5f), paint);
            kit.Box(new Vector3(0f, 0.92f, 0.56f), new Vector3(0.12f, 0.07f, 0.02f), Steel, new Vector3(-10f, 0f, 0f));
            kit.Bar(new Vector3(0f, 0.55f, 0.6f), new Vector3(0f, 1.1f, 0.52f), 0.08f, Tint(paint, 0.85f));
            kit.Box(new Vector3(0f, 1.14f, 0.52f), new Vector3(0.46f, 0.1f, 0.16f), paint);
            kit.Cyl(new Vector3(0f, 1.15f, 0.61f), new Vector3(0.17f, 0.05f, 0.17f), Steel, new Vector3(90, 0, 0), 12);
            kit.Cyl(new Vector3(0f, 1.15f, 0.635f), new Vector3(0.13f, 0.02f, 0.13f), new Color(1f, 0.95f, 0.7f), new Vector3(90, 0, 0), 12);
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Bar(new Vector3(side * 0.22f, 1.14f, 0.52f), new Vector3(side * 0.36f, 1.14f, 0.5f), 0.05f, rubber);
                kit.Bar(new Vector3(side * 0.18f, 1.18f, 0.52f), new Vector3(side * 0.26f, 1.36f, 0.5f), 0.02f, Steel);
                kit.Cyl(new Vector3(side * 0.27f, 1.38f, 0.5f), new Vector3(0.09f, 0.02f, 0.09f), Steel, new Vector3(80, 0, 0), 10);
            }
            // Rounded side cowls, seat, rack and tail light.
            for (int side = -1; side <= 1; side += 2) kit.Ball(new Vector3(side * 0.15f, 0.5f, -0.42f), new Vector3(0.3f, 0.48f, 0.76f), paint);
            kit.Box(new Vector3(0f, 0.46f, -0.3f), new Vector3(0.32f, 0.36f, 0.66f), paint);
            kit.Box(new Vector3(0f, 0.76f, -0.32f), new Vector3(0.34f, 0.1f, 0.62f), new Color(0.35f, 0.2f, 0.12f));
            kit.Box(new Vector3(0f, 0.72f, -0.32f), new Vector3(0.36f, 0.03f, 0.64f), Steel);
            kit.Box(new Vector3(0f, 0.66f, -0.8f), new Vector3(0.14f, 0.07f, 0.05f), new Color(0.95f, 0.15f, 0.1f));
            kit.Bar(new Vector3(-0.12f, 0.8f, -0.64f), new Vector3(-0.12f, 0.8f, -0.82f), 0.025f, Steel);
            kit.Bar(new Vector3(0.12f, 0.8f, -0.64f), new Vector3(0.12f, 0.8f, -0.82f), 0.025f, Steel);
            kit.Bar(new Vector3(-0.12f, 0.8f, -0.82f), new Vector3(0.12f, 0.8f, -0.82f), 0.025f, Steel);
            kit.Cyl(new Vector3(0.18f, 0.24f, -0.55f), new Vector3(0.1f, 0.34f, 0.1f), Steel, new Vector3(90, 0, 0), 8);

            // Rider: seated torso, thighs along the seat and shins down to the floorboard; the head is its own part.
            float seatY = 0.86f, seatZ = -0.18f;
            var pivot = new Vector3(0f, seatY + 0.1f, seatZ);
            var frame = Matrix4x4.Translate(pivot) * Matrix4x4.Rotate(Quaternion.Euler(b.hunch, 0f, 0f)) * Matrix4x4.Translate(-pivot)
                * Matrix4x4.Translate(new Vector3(0f, seatY - b.hipY, seatZ));
            kit.Frame = frame;
            bellyLo = 1f; bellyHi = -1f;
            Torso(s, b, EnemyKind.Vespista);
            kit.Frame = Matrix4x4.identity;
            var shoulder = frame.MultiplyPoint3x4(new Vector3(0f, b.shoulderY, 0f));
            m.shoulderY = shoulder.y;
            m.shoulderZ = shoulder.z;
            float t = b.limb;
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * b.hipX, seatY + 0.03f, seatZ);
                var knee = new Vector3(side * (b.hipX + 0.05f), seatY + 0.02f, 0.2f);
                var ankle = new Vector3(side * (b.hipX + 0.05f), 0.44f, 0.27f);
                kit.Taper(hip, knee, 0.22f * t, 0.17f * t, s.pants);
                kit.Ball(knee, 0.17f * t, s.pants);
                kit.Taper(knee, ankle, 0.17f * t, 0.15f * t, s.pants);
                kit.Box(new Vector3(ankle.x, 0.36f, 0.33f), new Vector3(0.15f, 0.04f, 0.3f), Tint(s.shoes, 0.6f));
                kit.Ball(new Vector3(ankle.x, 0.4f, 0.33f), new Vector3(0.15f, 0.1f, 0.29f), s.shoes);
            }
            m.body = kit.ToMesh("Vespista");

            m.headPos = frame.MultiplyPoint3x4(new Vector3(0f, b.shoulderY + b.neck, 0f));
            var headC = new Vector3(0f, b.HeadY, 0.01f);
            kit.Clear();
            kit.Frame = Matrix4x4.Translate(-m.headPos) * frame * Matrix4x4.Translate(headC)
                * Matrix4x4.Rotate(Quaternion.Euler(-b.hunch * 0.8f, 0f, 0f)) * Matrix4x4.Translate(-headC);
            Head(s, headC, b.head);
            kit.Frame = Matrix4x4.identity;
            m.head = kit.ToMesh("RiderHead");

            m.armL = Arm(s, b, Held.None, true, new Vector3(-64f, 0f, 8f), true);
            m.armR = Arm(s, b, Held.None, false, new Vector3(-64f, 0f, -8f), true);
            return m;
        }

        /// <summary>
        /// "If my grandma had wheels, she would've been a bike." Grande Nonna's second form IS the bicycle: her body is the top tube,
        /// her arms are the fork, her legs are the rear stays, her skirt fills the frame, her bun carries the handlebars and her
        /// backside carries the saddle. Her head steers (basket and handlebars with it) while the whole bike weaves; wheels and
        /// pedals turn with her speed.
        /// </summary>
        private static RigMeshes BikeNonna()
        {
            var s = new HumanSpec
            {
                body = BodyType.Elder, female = true, mood = Mood.Wild, shirt = new Color(0.14f, 0.1f, 0.16f), skirt = new Color(0.12f, 0.08f, 0.14f),
                scarf = new Color(0.55f, 0.1f, 0.18f), hair = new Color(0.92f, 0.92f, 0.94f), hairStyle = 1, glasses = 1, earrings = true,
                blush = true, wrinkles = true, noseSize = 1.2f, eyeSpacing = 0.105f
            };
            const float wheelR = 0.45f;
            var rearHub = new Vector3(0f, wheelR, -0.72f);
            var frontHub = new Vector3(0f, wheelR, 0.66f);
            var bb = new Vector3(0f, 0.36f, -0.04f);
            var neckA = new Vector3(0f, 1.18f, 0.36f);
            var m = new RigMeshes
            {
                seated = true, armRest = -58f, leg = null, shoulderY = 1.1f,
                wheelPos = new[] { rearHub, frontHub }, wheelRadius = wheelR, crankPos = bb, headPos = neckA,
                gait = new Gait { bob = 0.012f, roll = 9f, headSteer = 14f, headNod = 3f }
            };
            var tyre = new Color(0.1f, 0.1f, 0.1f);
            var dress = s.shirt;
            var stockings = new Color(0.82f, 0.68f, 0.58f);
            var lace = new Color(0.96f, 0.94f, 0.88f);
            var leather = new Color(0.45f, 0.26f, 0.14f);

            // ---- wheel (origin at the hub, axle along X): tyre, rim, spokes and a lace-doily hub cap
            kit.Clear();
            Ring(Vector3.zero, wheelR - 0.035f, 20, 0.075f, tyre, 0f, 360f);
            Ring(Vector3.zero, wheelR - 0.085f, 20, 0.03f, Steel, 0f, 360f, 4);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                float off = i % 2 == 0 ? 0.03f : -0.03f;
                kit.Bar(new Vector3(off, 0f, 0f), new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * (wheelR - 0.085f), 0.018f, Steel, true, 4);
            }
            kit.Cyl(Vector3.zero, new Vector3(0.1f, 0.14f, 0.1f), Steel, new Vector3(0, 0, 90), 8);
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Cyl(new Vector3(side * 0.05f, 0f, 0f), new Vector3(0.2f, 0.012f, 0.2f), lace, new Vector3(0, 0, 90), 12);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    kit.Dot(new Vector3(side * 0.055f, Mathf.Sin(a) * 0.1f, Mathf.Cos(a) * 0.1f), 0.04f, lace);
                }
            }
            m.wheel = kit.ToMesh("NonnaWheel");

            // ---- pedal crank (origin at the bottom bracket)
            kit.Clear();
            kit.Cyl(new Vector3(0.07f, 0f, 0f), new Vector3(0.26f, 0.02f, 0.26f), Steel, new Vector3(0, 0, 90), 14);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 0.4f;
                kit.Bar(new Vector3(0.08f, 0f, 0f), new Vector3(0.08f, Mathf.Sin(a) * 0.11f, Mathf.Cos(a) * 0.11f), 0.025f, Tint(Steel, 0.8f));
            }
            kit.Cyl(Vector3.zero, new Vector3(0.05f, 0.22f, 0.05f), Steel, new Vector3(0, 0, 90), 8);
            kit.Bar(new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, -0.15f, 0f), 0.035f, Steel);
            kit.Bar(new Vector3(-0.1f, 0f, 0f), new Vector3(-0.1f, 0.15f, 0f), 0.035f, Steel);
            kit.Box(new Vector3(0.16f, -0.15f, 0f), new Vector3(0.1f, 0.03f, 0.07f), tyre);
            kit.Box(new Vector3(-0.16f, 0.15f, 0f), new Vector3(0.1f, 0.03f, 0.07f), tyre);
            m.crank = kit.ToMesh("NonnaCrank");

            kit.Clear();
            // ---- the bicycle parts that are still steel: chainstays, chain, mudguards, rear rack and pennant
            for (int side = -1; side <= 1; side += 2)
                kit.Bar(bb + new Vector3(side * 0.06f, 0f, 0f), rearHub + new Vector3(side * 0.07f, 0f, 0f), 0.04f, Celeste);
            var chain = new Color(0.25f, 0.25f, 0.27f);
            kit.Cyl(rearHub + new Vector3(0.08f, 0f, 0f), new Vector3(0.12f, 0.02f, 0.12f), Steel, new Vector3(0, 0, 90), 10);
            kit.Bar(bb + new Vector3(0.08f, 0.13f, 0f), rearHub + new Vector3(0.08f, 0.06f, 0f), 0.02f, chain);
            kit.Bar(bb + new Vector3(0.08f, -0.13f, 0f), rearHub + new Vector3(0.08f, -0.06f, 0f), 0.02f, chain);
            Ring(rearHub, wheelR + 0.05f, 10, 0.05f, Celeste, 25f, 185f);
            Ring(frontHub, wheelR + 0.05f, 10, 0.05f, Celeste, -10f, 150f);
            var rack = new Vector3(0f, 1.0f, -0.92f);
            kit.Box(rack, new Vector3(0.24f, 0.03f, 0.36f), new Color(0.3f, 0.3f, 0.32f));
            for (int side = -1; side <= 1; side += 2)
                kit.Bar(rack + new Vector3(side * 0.1f, 0f, -0.1f), rearHub + new Vector3(side * 0.09f, 0f, 0f), 0.022f, Steel);
            kit.Cyl(rack + new Vector3(0f, -0.06f, -0.2f), new Vector3(0.12f, 0.03f, 0.12f), new Color(0.95f, 0.12f, 0.1f), new Vector3(90, 0, 0), 10);
            kit.Bar(rack + new Vector3(0.06f, 0f, -0.14f), new Vector3(0.06f, 1.95f, -1.12f), 0.02f, Steel);
            kit.Box(new Vector3(0.06f, 1.86f, -1.17f), new Vector3(0.02f, 0.16f, 0.07f), Basil);
            kit.Box(new Vector3(0.06f, 1.86f, -1.24f), new Vector3(0.02f, 0.16f, 0.07f), Color.white);
            kit.Box(new Vector3(0.06f, 1.86f, -1.31f), new Vector3(0.02f, 0.16f, 0.07f), Tomato);

            // ---- her body is the top tube: lying prone from the saddle to the head
            kit.Ball(new Vector3(0f, 1.06f, -0.06f), new Vector3(0.56f, 0.44f, 0.96f), dress, new Vector3(-6f, 0f, 0f));
            kit.Ball(new Vector3(0f, 1.03f, -0.5f), new Vector3(0.6f, 0.48f, 0.5f), s.skirt.Value);
            // Knitted shawl across her back, with a fringe.
            var shawl = new Color(0.42f, 0.2f, 0.36f);
            kit.Ball(new Vector3(0f, 1.2f, 0.12f), new Vector3(0.66f, 0.26f, 0.56f), shawl, new Vector3(-8f, 0f, 0f));
            for (int i = 0; i < 11; i++)
            {
                float a = Mathf.Lerp(-140f, 140f, i / 10f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 0.33f, 1.14f + Mathf.Cos(a) * 0.04f, 0.1f - Mathf.Cos(a) * 0.27f);
                kit.Bar(p, p + new Vector3(0f, -0.09f, 0f), 0.02f, Tint(shawl, 0.8f), true, 4);
            }
            // The saddle sits on her backside: sprung leather with copper rivets.
            kit.Box(new Vector3(0f, 1.31f, -0.58f), new Vector3(0.28f, 0.07f, 0.2f), leather);
            kit.Box(new Vector3(0f, 1.31f, -0.41f), new Vector3(0.12f, 0.06f, 0.24f), leather);
            kit.Ball(new Vector3(0f, 1.35f, -0.52f), new Vector3(0.26f, 0.06f, 0.34f), Tint(leather, 1.15f));
            for (int side = -1; side <= 1; side += 2)
            {
                kit.Cyl(new Vector3(side * 0.08f, 1.24f, -0.62f), new Vector3(0.05f, 0.09f, 0.05f), Steel, default, 8);
                kit.Dot(new Vector3(side * 0.12f, 1.33f, -0.66f), 0.03f, new Color(0.8f, 0.5f, 0.25f));
            }
            // Her skirt hangs down to fill the frame like a skirt guard, lace petticoat peeking out.
            kit.Cone(new Vector3(0f, 0.8f, -0.06f), new Vector3(0.48f, 0.48f, 0.44f), s.skirt.Value, default, 0.82f, 14);
            kit.Cyl(new Vector3(0f, 0.57f, -0.06f), new Vector3(0.5f, 0.05f, 0.46f), lace, default, 14);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                kit.Dot(new Vector3(Mathf.Cos(a) * 0.25f, 0.545f, -0.06f + Mathf.Sin(a) * 0.23f), 0.05f, lace);
            }

            // ---- legs are the seat stays: thick support stockings down to the rear axle, granny shoes clamped on as dropouts
            for (int side = -1; side <= 1; side += 2)
            {
                var hip = new Vector3(side * 0.15f, 0.98f, -0.6f);
                var knee = new Vector3(side * 0.16f, 0.8f, -0.9f);
                var ankle = new Vector3(side * 0.11f, 0.54f, -0.76f);
                kit.Taper(hip, knee, 0.17f, 0.13f, stockings);
                kit.Ball(knee, 0.13f, stockings);
                kit.Taper(knee, ankle, 0.13f, 0.1f, stockings);
                var shoe = new Color(0.1f, 0.08f, 0.08f);
                kit.Ball(ankle + new Vector3(0f, -0.07f, -0.06f), new Vector3(0.14f, 0.12f, 0.26f), shoe, new Vector3(-50f, 0f, 0f));
                kit.Box(ankle + new Vector3(0f, -0.02f, -0.18f), new Vector3(0.13f, 0.06f, 0.1f), shoe, new Vector3(-50f, 0f, 0f));
                kit.Dot(ankle + new Vector3(side * 0.06f, -0.03f, -0.02f), 0.035f, Gold);
            }

            // ---- arms are the front fork: lace cuffs, fists clamped round the front axle
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = new Vector3(side * 0.24f, 1.14f, 0.3f);
                var elbow = new Vector3(side * 0.22f, 0.84f, 0.48f);
                var wrist = new Vector3(side * 0.12f, 0.56f, 0.64f);
                kit.Ball(shoulder, 0.19f, dress);
                kit.Taper(shoulder, elbow, 0.16f, 0.13f, dress);
                kit.Ball(elbow, 0.13f, dress);
                kit.Taper(elbow, wrist, 0.13f, 0.11f, dress);
                kit.Taper(wrist + new Vector3(side * 0.012f, 0.04f, -0.02f), wrist, 0.14f, 0.15f, lace);
                kit.Ball(frontHub + new Vector3(side * 0.1f, 0.03f, 0f), new Vector3(0.12f, 0.15f, 0.14f), s.skin);
                kit.Taper(frontHub + new Vector3(side * 0.1f, 0.07f, 0.06f), frontHub + new Vector3(side * 0.07f, 0.0f, 0.08f), 0.045f, 0.04f, s.skin, 6);
            }

            // ---- scarf at the neck streaming back, pearls hanging down
            kit.Cyl(neckA + new Vector3(0f, 0.06f, 0.05f), new Vector3(0.32f, 0.12f, 0.3f), s.scarf.Value, new Vector3(-50f, 0f, 0f), 12);
            for (int i = 0; i < 3; i++)
                kit.Box(new Vector3(0.1f - i * 0.05f, 1.33f - i * 0.03f, 0.18f - i * 0.17f), new Vector3(0.12f, 0.035f, 0.22f), s.scarf.Value, new Vector3(-8f + i * 4f, 14f - i * 10f, i * 6f));
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad;
                kit.Dot(new Vector3(Mathf.Sin(a) * 0.14f, 1.14f - Mathf.Cos(a) * 0.12f, 0.46f + Mathf.Cos(a) * 0.04f), 0.045f, lace);
            }
            m.body = kit.ToMesh("BikeNonna");

            // ---- head is the head tube and lamp, and it steers: handlebars through the bun, the basket hung from her neck
            kit.Clear();
            kit.Frame = Matrix4x4.Translate(-neckA);
            const float hs = 1.3f;
            var headC = new Vector3(0f, 1.52f, 0.62f);
            kit.Taper(neckA, headC + new Vector3(0f, -0.2f, -0.06f), 0.16f, 0.15f, s.skin);
            Head(s, headC, hs);
            Vector3 H(float x, float y, float z) => headC + new Vector3(x, y, z) * hs;
            // Headlamp strapped to her forehead.
            kit.Cyl(H(0f, 0.15f, -0.01f), new Vector3(0.56f, 0.05f, 0.54f) * hs, new Color(0.25f, 0.18f, 0.12f), new Vector3(-6f, 0f, 0f), 14);
            kit.Cyl(H(0f, 0.2f, 0.25f), new Vector3(0.16f, 0.12f, 0.16f) * hs, Steel, new Vector3(80f, 0f, 0f), 12);
            kit.Cyl(H(0f, 0.19f, 0.31f), new Vector3(0.13f, 0.02f, 0.13f) * hs, new Color(1f, 0.97f, 0.75f), new Vector3(80f, 0f, 0f), 12);
            // Knitting needles through the bun are the handlebars: leather grips, a brass bell and a dangling ball of yarn.
            var bun = H(0f, 0.21f, -0.24f);
            for (int side = -1; side <= 1; side += 2)
            {
                var end = bun + new Vector3(side * 0.56f, 0.06f, -0.1f);
                kit.Bar(bun, end, 0.035f, Celeste);
                kit.Taper(end - new Vector3(side * 0.15f, 0.015f, -0.03f), end + new Vector3(side * 0.02f, 0f, 0f), 0.065f, 0.07f, leather, 8, true);
                kit.Dot(end + new Vector3(side * 0.04f, 0f, 0f), 0.05f, Celeste);
            }
            var bell = bun + new Vector3(-0.3f, 0.09f, -0.04f);
            kit.Cone(bell + new Vector3(0f, 0.035f, 0f), new Vector3(0.12f, 0.07f, 0.12f), Gold, default, 0.35f, 10);
            kit.Dot(bell + new Vector3(0f, 0.08f, 0f), 0.03f, Gold);
            var yarn = bun + new Vector3(0.36f, -0.2f, -0.06f);
            kit.Bar(bun + new Vector3(0.36f, 0.035f, -0.06f), yarn, 0.008f, Tomato);
            kit.Ball(yarn, 0.12f, Tomato);
            kit.Bar(yarn + new Vector3(-0.05f, 0.03f, 0.05f), yarn + new Vector3(0.05f, -0.03f, -0.04f), 0.015f, Tint(Tomato, 0.8f));

            // Wicker basket over the front wheel: spaghetti (poking out sideways, clear of her face), tomatoes, basil and wine.
            var wicker = new Color(0.72f, 0.52f, 0.28f);
            var basket = new Vector3(0f, 0.96f, 0.98f);
            kit.Box(basket, new Vector3(0.44f, 0.24f, 0.3f), wicker);
            kit.Box(basket + new Vector3(0f, 0.115f, 0f), new Vector3(0.38f, 0.02f, 0.24f), new Color(0.35f, 0.22f, 0.1f));
            for (int i = 0; i < 3; i++)
                kit.Box(basket + new Vector3(0f, -0.08f + i * 0.08f, 0f), new Vector3(0.455f, 0.03f, 0.315f), Tint(wicker, 0.8f));
            for (int side = -1; side <= 1; side += 2)
                kit.Bar(basket + new Vector3(side * 0.18f, 0.12f, -0.14f), neckA + new Vector3(side * 0.08f, 0.04f, 0.08f), 0.025f, leather);
            for (int i = 0; i < 6; i++)
                kit.Cyl(basket + new Vector3(-0.2f, 0.2f + (i % 3) * 0.025f, -0.06f + i * 0.025f), new Vector3(0.022f, 0.46f, 0.022f), PastaGold,
                    new Vector3(i * 3f, 0f, 62f + (i % 2) * 6f), 5);
            kit.Ball(basket + new Vector3(0.1f, 0.17f, 0.06f), 0.13f, Tomato);
            kit.Ball(basket + new Vector3(0.02f, 0.17f, 0.09f), 0.11f, Tomato);
            kit.Ball(basket + new Vector3(-0.04f, 0.16f, 0.1f), new Vector3(0.12f, 0.04f, 0.08f), Basil, new Vector3(0, 30, 20));
            var bottle = basket + new Vector3(0.16f, 0.2f, -0.08f);
            kit.Cyl(bottle, new Vector3(0.08f, 0.24f, 0.08f), new Color(0.15f, 0.35f, 0.18f), new Vector3(0f, 0f, -12f), 8);
            kit.Taper(bottle + new Vector3(-0.025f, 0.12f, 0f), bottle + new Vector3(-0.04f, 0.22f, 0f), 0.035f, 0.03f, new Color(0.15f, 0.35f, 0.18f), 6);
            kit.Dot(bottle + new Vector3(-0.042f, 0.23f, 0f), 0.035f, Tomato);
            kit.Frame = Matrix4x4.identity;
            m.head = kit.ToMesh("BikeNonnaHead");
            return m;
        }
    }
}
