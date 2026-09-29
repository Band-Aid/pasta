using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public class Player : MonoBehaviour
    {
        public const int MaxWeapons = 6, MaxPassives = 6;
        public const float DashCooldown = 1.4f;

        public CharacterDef Character { get; private set; }
        public int CharIndex { get; private set; }
        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }
        public Vector3 Facing { get; private set; } = Vector3.forward;
        public float Radius => 0.4f;
        public float Hp, MaxHp;
        public int Level = 1;
        public float Xp;
        public int XpNeeded => GameData.XpToNext(Level);
        public int RunCoins;
        public bool God;
        public bool Alive => Hp > 0f;
        public PlayerAnim Anim { get; private set; }

        public readonly List<Weapon> Weapons = new List<Weapon>();
        public readonly List<PassiveId> Passives = new List<PassiveId>();
        public readonly int[] PassiveLevels = new int[8];

        public float Might, CooldownMul, AreaMul, DurationMul, MoveSpeed, MagnetRadius, GrowthMul, GreedMul, Armor, Regen;
        public int AmountBonus, Revivals;

        private float invuln, dashCd, dashTime, hurtFlash;
        private Vector3 dashDir;
        public float DashReady => 1f - Mathf.Clamp01(dashCd / DashCooldown);
        public bool Dashing => dashTime > 0f;

        // ---- main weapon (主武器): boosted, has a trait, and is aimed by the player in first person
        public int MainIndex { get; private set; }
        public Weapon Main => MainIndex >= 0 && MainIndex < Weapons.Count ? Weapons[MainIndex] : null;

        // ---- first person
        public bool FirstPerson { get; private set; }
        public float LookYaw, LookPitch = 8f;
        public Vector3 LookForward => Quaternion.Euler(LookPitch, LookYaw, 0f) * Vector3.forward;
        /// <summary>Horizontal direction the main weapon fires in.</summary>
        public Vector3 AimDir => FirstPerson ? Quaternion.Euler(0f, LookYaw, 0f) * Vector3.forward : Facing;

        /// <summary>Where the crosshair meets the ground (first person), clamped to a throwing range.</summary>
        public Vector3 AimPoint(float maxDistance)
        {
            if (!FirstPerson) return Position + Facing * Mathf.Min(6f, maxDistance);
            var eye = Position + Vector3.up * PlayerAnim.EyeHeight;
            var f = LookForward;
            float dist = f.y < -0.05f ? Mathf.Min(maxDistance, eye.y / -f.y) : maxDistance;
            var p = eye + f * dist;
            var flat = p - Position; flat.y = 0f;
            if (flat.magnitude > maxDistance) flat = flat.normalized * maxDistance;
            return Position + flat;
        }

        public void SetFirstPerson(bool on)
        {
            FirstPerson = on;
            if (on)
            {
                LookYaw = Quaternion.LookRotation(Facing).eulerAngles.y;
                LookPitch = 8f;
            }
            Anim.SetFirstPerson(on);
        }

        public void SetMain(int index, bool announce = true)
        {
            if (index < 0 || index >= Weapons.Count || index == MainIndex) return;
            MainIndex = index;
            var w = Main;
            G.Sfx.Play(SfxId.Select, 0.8f, 0.8f);
            G.Sfx.Play(SfxId.Whoosh, 0.35f, 1.6f);
            Anim.OnSwitch();
            if (announce && G.Hud != null) G.Hud.MainSwitched(w);
        }

        public void Setup(CharacterDef c, int index)
        {
            Character = c;
            CharIndex = index;
            Anim = gameObject.AddComponent<PlayerAnim>();
            Anim.Build(c, index);
        }

        public void ResetRun()
        {
            foreach (var w in Weapons) w.Removed();
            Weapons.Clear();
            Passives.Clear();
            System.Array.Clear(PassiveLevels, 0, PassiveLevels.Length);
            Level = 1;
            Xp = 0f;
            RunCoins = 0;
            invuln = dashCd = dashTime = 0f;
            Velocity = Vector3.zero;
            Facing = Vector3.forward;
            MainIndex = 0;
            LookYaw = 0f;
            LookPitch = 8f;
            Revivals = SaveData.Shop[7];
            RecalcStats();
            Hp = MaxHp;
            AddWeapon(Character.start);
        }

        public void RecalcStats()
        {
            int P(PassiveId id) => PassiveLevels[(int)id];
            var shop = SaveData.Shop;
            float oldMax = MaxHp;
            Might = 1f + P(PassiveId.Parmigiano) * 0.1f + shop[0] * 0.05f + Character.mightBonus;
            CooldownMul = Mathf.Max(0.4f, 1f - P(PassiveId.Espresso) * 0.08f);
            AreaMul = 1f + P(PassiveId.Semolina) * 0.1f + Character.areaBonus;
            AmountBonus = P(PassiveId.BigPot);
            DurationMul = 1f + P(PassiveId.RecipeBook) * 0.05f;
            MoveSpeed = 5.3f * (1f + P(PassiveId.OliveOil) * 0.1f + shop[3] * 0.05f + Character.speedBonus);
            MagnetRadius = 3f * (1f + P(PassiveId.Fork) * 0.3f + shop[4] * 0.15f);
            MaxHp = (120f + shop[1] * 10f) * (1f + P(PassiveId.Basil) * 0.15f);
            Regen = P(PassiveId.Basil) * 0.3f;
            GrowthMul = 1f + P(PassiveId.RecipeBook) * 0.08f + shop[5] * 0.05f + Character.growthBonus;
            GreedMul = 1f + shop[6] * 0.1f;
            Armor = shop[2];
            if (oldMax > 0f && MaxHp > oldMax) Hp += MaxHp - oldMax;
            Hp = Mathf.Min(Hp, MaxHp);
        }

        // ---------------- inventory ----------------

        public Weapon GetWeapon(WeaponId id)
        {
            foreach (var w in Weapons) if (w.Id == id) return w;
            return null;
        }

        public void AddWeapon(WeaponId id)
        {
            if (GetWeapon(id) != null || Weapons.Count >= MaxWeapons) return;
            Weapons.Add(Weapon.Create(id));
        }

        public void AddPassive(PassiveId id)
        {
            if (PassiveLevels[(int)id] == 0)
            {
                if (Passives.Count >= MaxPassives) return;
                Passives.Add(id);
            }
            PassiveLevels[(int)id] = Mathf.Min(GameData.Passive(id).maxLevel, PassiveLevels[(int)id] + 1);
            RecalcStats();
        }

        /// <summary>Weapons at max level whose partner passive is owned can evolve from a chest.</summary>
        public Weapon Evolvable()
        {
            foreach (var w in Weapons)
                if (!w.evolved && w.MaxedOut && PassiveLevels[(int)w.def.evoPartner] > 0) return w;
            return null;
        }

        // ---------------- per-frame ----------------

        public void Tick(float dt)
        {
            if (!Alive) return;
            Vector2 input = Controls.Move();
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            int sw = Controls.SwitchWeapon();
            if (sw != 0 && Weapons.Count > 1) SetMain((MainIndex + sw + Weapons.Count) % Weapons.Count);
            int slot = Controls.WeaponSlotKey();
            if (slot >= 0) SetMain(slot);

            if (FirstPerson)
            {
                var look = Controls.Look();
                LookYaw = Mathf.Repeat(LookYaw + look.x, 360f);
                LookPitch = Mathf.Clamp(LookPitch - look.y, -55f, 75f);
                dir = Quaternion.Euler(0f, LookYaw, 0f) * dir;
            }

            dashCd -= dt;
            if (Controls.Dash() && dashCd <= 0f)
            {
                dashDir = dir.sqrMagnitude > 0.05f ? dir.normalized : Facing;
                dashTime = 0.2f;
                dashCd = DashCooldown;
                G.Sfx.Play(SfxId.Dash, 0.5f, 1f);
                G.Fx.Burst(Position + Vector3.up * 0.3f, new Color(1f, 1f, 1f, 0.6f), 6, 2f, 0.7f, FxKind.Puff);
            }

            Vector3 target = dir * MoveSpeed;
            if (dashTime > 0f)
            {
                dashTime -= dt;
                target = dashDir * 21f;
                Velocity = target;
                if (Random.value < 0.5f) G.Fx.Burst(Position + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.35f), 1, 0.5f, 0.6f, FxKind.Puff);
            }
            else Velocity = Vector3.MoveTowards(Velocity, target, dt * 70f);

            if (FirstPerson) Facing = AimDir;
            else if (dir.sqrMagnitude > 0.01f) Facing = Vector3.Slerp(Facing, dir.normalized, 1f - Mathf.Exp(-18f * dt)).normalized;

            var next = transform.position + Velocity * dt;
            next = G.Arena.Resolve(next, Radius);
            next.y = 0f;
            transform.position = next;

            invuln -= dt;
            hurtFlash -= dt;
            if (Regen > 0f) Heal(Regen * dt, false);

            for (int i = 0; i < Weapons.Count; i++) Weapons[i].Tick(dt);
            Anim.Tick(dt, Velocity.magnitude, Facing, hurtFlash > 0f, dashTime > 0f);
        }

        /// <summary>Damage taken per source, for balance telemetry.</summary>
        public readonly Dictionary<string, float> DamageTaken = new Dictionary<string, float>();

        public void TakeDamage(float amount, Vector3 from, string source = "other")
        {
            if (!Alive || God || invuln > 0f || dashTime > 0f || !G.Playing) return;
            amount = Mathf.Max(1f, amount - Armor);
            DamageTaken.TryGetValue(source, out var sofar);
            DamageTaken[source] = sofar + amount;
            Hp -= amount;
            invuln = 0.6f;
            hurtFlash = 0.12f;
            G.Cam.Shake(0.35f);
            G.Sfx.Play(SfxId.Hurt, 0.7f, Random.Range(0.9f, 1.1f));
            G.Fx.Number(Position + Vector3.up * 2.2f, amount, false, true);
            G.Fx.Burst(Position + Vector3.up, new Color(1f, 0.3f, 0.3f), 6, 4f, 0.15f, FxKind.Crumb);
            G.Enemies.Shove(Position, 1.8f, 7f);
            if (Hp > 0f) return;
            if (Revivals > 0)
            {
                Revivals--;
                Hp = MaxHp * 0.5f;
                invuln = 2.5f;
                G.Enemies.RoutAll(Position, 14f);
                G.Fx.Ring(Position + Vector3.up * 0.1f, 14f, new Color(1f, 0.95f, 0.6f, 0.9f), 0.8f, 0.8f);
                G.Fx.Banner("復活！ ノンナの祈りが届いた", new Color(1f, 0.9f, 0.5f));
                G.Sfx.Play(SfxId.LevelUp, 0.8f, 0.8f);
                return;
            }
            Hp = 0f;
            G.Game.OnPlayerDied();
        }

        public void Heal(float amount, bool show = true)
        {
            if (!Alive) return;
            float before = Hp;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            if (show && Hp - before >= 1f)
                G.Fx.Number(Position + Vector3.up * 2.2f, Hp - before, false, false, true);
        }

        public void AddXp(float amount)
        {
            Xp += amount * GrowthMul;
            while (Xp >= XpNeeded)
            {
                Xp -= XpNeeded;
                Level++;
                G.Game.QueueLevelUp();
            }
        }
    }
}
