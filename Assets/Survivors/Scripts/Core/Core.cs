using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PastaSurvivors
{
    /// <summary>Service locator for the run's systems.</summary>
    public static class G
    {
        public static SurvivorsGame Game;
        public static Player Player;
        public static EnemyManager Enemies;
        public static ShotSystem Shots;
        public static PickupManager Pickups;
        public static Fx Fx;
        public static Sfx Sfx;
        public static Arena Arena;
        public static CameraRig Cam;
        public static WaveDirector Waves;
        public static Hud Hud;
        public static StageDef Stage;
        public static float RunTime;
        public static bool Playing => Game != null && Game.State == GameState.Playing;
    }

    public static class Controls
    {
        /// <summary>Scripted input for automated verification.</summary>
        public static Vector2? TestMove;
        public static bool TestDash;

        private static float navRepeatAt;
        private static int lastNav;

        public static Vector2 Move()
        {
            if (TestMove.HasValue) return TestMove.Value;
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            }
            foreach (var pad in Gamepad.all)
            {
                var s = pad.leftStick.ReadValue();
                if (s.sqrMagnitude > 0.04f) v += s;
                v += pad.dpad.ReadValue();
            }
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static bool Dash()
        {
            if (TestDash) { TestDash = false; return true; }
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all)
                if (pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame) return true;
            return false;
        }

        public static bool Pause()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all) if (pad.startButton.wasPressedThisFrame) return true;
            return false;
        }

        public static bool Submit()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all) if (pad.buttonSouth.wasPressedThisFrame) return true;
            return false;
        }

        public static bool Cancel()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)) return true;
            foreach (var pad in Gamepad.all) if (pad.buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        public static int NumberKey()
        {
            var kb = Keyboard.current;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            return -1;
        }

        /// <summary>Menu navigation with key repeat. Returns (dx, dy) with dy positive = up.</summary>
        public static Vector2Int Nav()
        {
            Vector2 v = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1;
            }
            foreach (var pad in Gamepad.all)
            {
                v += pad.dpad.ReadValue();
                var s = pad.leftStick.ReadValue();
                if (s.magnitude > 0.6f) v += s;
            }
            int dx = Mathf.Abs(v.x) > 0.5f ? (int)Mathf.Sign(v.x) : 0;
            int dy = Mathf.Abs(v.y) > 0.5f ? (int)Mathf.Sign(v.y) : 0;
            int code = dx * 3 + dy;
            if (code == 0) { lastNav = 0; return Vector2Int.zero; }
            float now = Time.unscaledTime;
            if (code != lastNav) { lastNav = code; navRepeatAt = now + 0.35f; return new Vector2Int(dx, dy); }
            if (now >= navRepeatAt) { navRepeatAt = now + 0.12f; return new Vector2Int(dx, dy); }
            return Vector2Int.zero;
        }

        public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1, -1);
        public static bool MouseClicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public static Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        /// <summary>Scripted look input (degrees per second) for automated verification.</summary>
        public static Vector2? TestLook;
        public static int TestSwitch;

        /// <summary>First-person look delta in degrees for this frame.</summary>
        public static Vector2 Look()
        {
            if (TestLook.HasValue) return TestLook.Value * Time.unscaledDeltaTime;
            Vector2 v = Vector2.zero;
            float sens = SaveData.Sensitivity;
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked) v += Mouse.current.delta.ReadValue() * 0.09f * sens;
            foreach (var pad in Gamepad.all)
            {
                var s = pad.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.02f) v += new Vector2(s.x * 200f, s.y * 130f) * Time.unscaledDeltaTime * sens;
            }
            return v;
        }

        /// <summary>Main weapon cycling: -1 previous, +1 next, 0 none.</summary>
        public static int SwitchWeapon()
        {
            if (TestSwitch != 0) { int t = TestSwitch; TestSwitch = 0; return t; }
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.wasPressedThisFrame) return -1;
                if (kb.eKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame) return 1;
            }
            if (Mouse.current != null)
            {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (wheel > 0.1f) return -1;
                if (wheel < -0.1f) return 1;
            }
            foreach (var pad in Gamepad.all)
            {
                if (pad.leftShoulder.wasPressedThisFrame) return -1;
                if (pad.rightShoulder.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        /// <summary>Direct main weapon selection with 1-6 during play (-1 if none).</summary>
        public static int WeaponSlotKey()
        {
            var kb = Keyboard.current;
            if (kb == null) return -1;
            if (kb.digit1Key.wasPressedThisFrame) return 0;
            if (kb.digit2Key.wasPressedThisFrame) return 1;
            if (kb.digit3Key.wasPressedThisFrame) return 2;
            if (kb.digit4Key.wasPressedThisFrame) return 3;
            if (kb.digit5Key.wasPressedThisFrame) return 4;
            if (kb.digit6Key.wasPressedThisFrame) return 5;
            return -1;
        }

        public static bool ToggleView()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame) return true;
            foreach (var pad in Gamepad.all) if (pad.selectButton.wasPressedThisFrame) return true;
            return false;
        }

        public static bool DebugKey(Key key)
        {
            var kb = Keyboard.current;
            return kb != null && kb[key].wasPressedThisFrame;
        }
    }

    public static class SaveData
    {
        /// <summary>Key prefix; automated tests use their own slot so real progress is never touched.</summary>
        public static string Prefix = "ps.";
        public static int Coins;
        public static int[] Shop = new int[8];
        public static int StagesUnlocked = 1;
        public static float[] BestTime = new float[3];
        public static bool[] Cleared = new bool[3];
        public static int TotalKills;
        /// <summary>0 = top-down survivors view, 1 = first person.</summary>
        public static int ViewMode;
        public static float Sensitivity = 1f;
        public static bool FirstPerson => ViewMode == 1;

        public static void Load()
        {
            Coins = PlayerPrefs.GetInt(Prefix + "coins", 0);
            StagesUnlocked = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "unlocked", 1), 1, 3);
            TotalKills = PlayerPrefs.GetInt(Prefix + "kills", 0);
            ViewMode = PlayerPrefs.GetInt(Prefix + "view", 0);
            Sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sens", 1f), 0.2f, 3f);
            for (int i = 0; i < Shop.Length; i++) Shop[i] = PlayerPrefs.GetInt(Prefix + "shop" + i, 0);
            for (int i = 0; i < 3; i++)
            {
                BestTime[i] = PlayerPrefs.GetFloat(Prefix + "best" + i, 0f);
                Cleared[i] = PlayerPrefs.GetInt(Prefix + "clear" + i, 0) == 1;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "coins", Coins);
            PlayerPrefs.SetInt(Prefix + "unlocked", StagesUnlocked);
            PlayerPrefs.SetInt(Prefix + "kills", TotalKills);
            PlayerPrefs.SetInt(Prefix + "view", ViewMode);
            PlayerPrefs.SetFloat(Prefix + "sens", Sensitivity);
            for (int i = 0; i < Shop.Length; i++) PlayerPrefs.SetInt(Prefix + "shop" + i, Shop[i]);
            for (int i = 0; i < 3; i++)
            {
                PlayerPrefs.SetFloat(Prefix + "best" + i, BestTime[i]);
                PlayerPrefs.SetInt(Prefix + "clear" + i, Cleared[i] ? 1 : 0);
            }
            PlayerPrefs.Save();
        }

        public static void Wipe()
        {
            foreach (var key in new[] { "coins", "unlocked", "kills", "view", "sens" }) PlayerPrefs.DeleteKey(Prefix + key);
            for (int i = 0; i < Shop.Length; i++) PlayerPrefs.DeleteKey(Prefix + "shop" + i);
            for (int i = 0; i < 3; i++) { PlayerPrefs.DeleteKey(Prefix + "best" + i); PlayerPrefs.DeleteKey(Prefix + "clear" + i); }
            Load();
        }

        public static int ShopCost(int index) => GameData.Shop[index].baseCost * (Shop[index] + 1);
    }

    /// <summary>Playable area: an axis-aligned rectangle with circular obstacles. No physics engine involved.</summary>
    public class Arena
    {
        public struct Obstacle { public Vector2 c; public float r; }
        public Vector2 min, max;
        public readonly List<Obstacle> obstacles = new List<Obstacle>();

        public Arena(Vector2 min, Vector2 max) { this.min = min; this.max = max; }

        public void AddObstacle(Vector3 pos, float radius) => obstacles.Add(new Obstacle { c = new Vector2(pos.x, pos.z), r = radius });

        public bool Inside(Vector3 p, float margin)
            => p.x > min.x + margin && p.x < max.x - margin && p.z > min.y + margin && p.z < max.y - margin;

        public Vector3 Clamp(Vector3 p, float margin)
        {
            p.x = Mathf.Clamp(p.x, min.x + margin, max.x - margin);
            p.z = Mathf.Clamp(p.z, min.y + margin, max.y - margin);
            return p;
        }

        public bool Blocked(Vector3 p, float r)
        {
            foreach (var o in obstacles)
            {
                float dx = p.x - o.c.x, dz = p.z - o.c.y, rr = o.r + r;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        /// <summary>Push a circle out of obstacles and inside the bounds.</summary>
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
            return Clamp(p, r);
        }
    }
}
