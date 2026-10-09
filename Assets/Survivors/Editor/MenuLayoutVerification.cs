using System;
using UnityEngine;
using UnityEngine.UI;

namespace PastaSurvivors.EditorTools
{
    public static class MenuLayoutVerification
    {
        public static void Check()
        {
            UiKit.Init();
            var previousManager = G.Enemies;
            var root = new GameObject("Temporary menu layout checks") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                G.Enemies = root.AddComponent<EnemyManager>();
                Vector2[] screens = { new Vector2(1592, 936), new Vector2(1920, 1080), new Vector2(1280, 720) };
                foreach (var screen in screens)
                {
                    CheckMenu(root.transform, screen, false);
                    CheckMenu(root.transform, screen, true);
                }
                Debug.Log("MENU_LAYOUT_CHECKS_PASSED: BGM, SE and gesture sliders at 0/25/40/55/75/100%, fill bounds, handle size/position, highlighted rows and panel bounds at 1592x936, 1920x1080 and 1280x720");
            }
            finally
            {
                G.Enemies = previousManager;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckMenu(Transform parent, Vector2 screen, bool gestures)
        {
            var ui = new GameObject("Menu layout");
            ui.transform.SetParent(parent, false);
            try
            {
                var menus = ui.AddComponent<Menus>();
                menus.Build(ui.transform);
                var canvas = ui.GetComponentInChildren<Canvas>();
                // Match MakeCanvas's width/height scaling without changing the Editor's screen.
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                float scale = Mathf.Sqrt(screen.x / 1920f * screen.y / 1080f);
                canvasRect.sizeDelta = screen / scale;
                if (gestures) menus.ShowGestureSettings(() => { });
                else menus.ShowAudioSettings(() => { });
                var sliders = ui.GetComponentsInChildren<Slider>();
                Require(sliders.Length == (gestures ? 1 : 2), "Actual settings contain the expected sliders");
                var panel = (RectTransform)canvas.transform.Find(gestures ? "gestures/GestureSettings" : "audio/AudioSettings");
                RequireInside(panel, canvasRect, "Settings panel stays on screen");
                foreach (var slider in sliders)
                {
                    Require(slider.minValue == 0f && slider.maxValue == 100f && slider.wholeNumbers, "Slider range is 0-100%");
                    var hitArea = (RectTransform)slider.transform;
                    var track = (RectTransform)hitArea.Find("Track");
                    var row = gestures ? panel : (RectTransform)hitArea.parent;
                    foreach (float highlight in new[] { 1f, 1.04f })
                    {
                        if (!gestures) row.localScale = Vector3.one * highlight;
                        foreach (float percent in new[] { 0f, 25f, 40f, 55f, 75f, 100f })
                        {
                            slider.SetValueWithoutNotify(percent);
                            var fill = BoundsIn(slider.fillRect, hitArea);
                            var expected = BoundsIn(track, hitArea);
                            Require(Near(fill.min, expected.min) && Near(fill.max, expected.max),
                                slider.name + " fill rectangle matches the thin track at " + percent + "% (actual " + fill + ", expected " + expected + ")");
                            var image = slider.fillRect.GetComponent<Image>();
                            Require(image.type == Image.Type.Filled && Mathf.Abs(image.fillAmount - percent / 100f) < 0.001f,
                                "Rendered fill follows the selected percentage");
                            var handle = BoundsIn(slider.handleRect, hitArea);
                            Require(Near(handle.size, new Vector2(24, 36)), "Handle stays 24x36 inside the hit area");
                            Require(Near(handle.center, new Vector2(Mathf.Lerp(expected.xMin, expected.xMax, percent / 100f), expected.center.y)),
                                "Handle follows the track from 0% through 100%");
                            RequireInside(slider.fillRect, row, "Fill stays inside its row");
                            RequireInside(slider.handleRect, row, "Handle stays inside its row at both endpoints");
                            RequireInside(slider.fillRect, panel, "Fill stays inside the panel when selected");
                            RequireInside(slider.handleRect, panel, "Handle stays inside the panel when selected");
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(ui); }
        }

        private static Rect BoundsIn(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector2 point = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void RequireInside(RectTransform child, RectTransform parent, string message)
        {
            var bounds = BoundsIn(child, parent);
            var container = parent.rect;
            Require(bounds.xMin >= container.xMin - 0.01f && bounds.xMax <= container.xMax + 0.01f
                && bounds.yMin >= container.yMin - 0.01f && bounds.yMax <= container.yMax + 0.01f, message);
        }

        private static bool Near(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 0.001f;
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Menu layout verification: " + message);
        }
    }
}
