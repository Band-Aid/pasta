using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>Readable, fresh accents for normal weapons against the warm stone of the stages.</summary>
    public static class WeaponColors
    {
        public static readonly Color Spaghetti = new Color(0.03f, 0.7f, 0.96f);
        public static readonly Color Penne = new Color(0.13f, 0.43f, 1f);
        public static readonly Color Lasagna = new Color(0.55f, 0.35f, 0.96f);
        public static readonly Color Fusilli = new Color(0.06f, 0.79f, 0.55f);
        public static readonly Color Farfalle = new Color(0.83f, 0.33f, 0.84f);
        public static readonly Color Ketchup = new Color(1f, 0.3f, 0.42f);
        public static readonly Color Pizza = new Color(0.47f, 0.78f, 0.12f);
        public static readonly Color Cream = new Color(0.3f, 0.78f, 0.72f);
        public static readonly Color CreamWhite = new Color(0.92f, 1f, 0.98f);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
        public static Color Highlight(Color color) => Color.Lerp(color, Color.white, 0.7f);
    }
}
