using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
        // ---- aiming (top-down): mouse cursor or twin-stick; otherwise weapons auto-target
        public Vector3 MouseGround { get; private set; }
        public bool MouseAiming => !FirstPerson && mouseAimTimer > 0f;
        public bool StickAiming => !FirstPerson && stickAimTimer > 0f;
        public bool HasAim => FirstPerson || MouseAiming || StickAiming;
        private float mouseAimTimer, stickAimTimer;
        private Vector3 stickAim = Vector3.forward;
        private Vector2 lastMousePos;
        private bool mouseMoveArmed;

        // ---- special weapon (特殊武器): picked up on the map, fired manually, then cools down
        public SpecialId? Special { get; private set; }
        public float SpecialCd { get; private set; }
        public float SpecialCdMax => Special.HasValue ? GameData.Special(Special.Value).cooldown * CooldownMul : 1f;
        public float SpecialReady => Special.HasValue ? 1f - Mathf.Clamp01(SpecialCd / SpecialCdMax) : 0f;
        public float BuffTime { get; private set; }
        /// <summary>Sugar espresso halves every weapon's cooldown while active.</summary>
        public float BuffCdMul => BuffTime > 0f ? 0.5f : 1f;
        public const int SpecialSlot = 11;
        private float knifeTime, healFx, beamTime, beamTick, beamSfx;
        private Vector3 beamDir = Vector3.forward;
        public bool Beaming => beamTime > 0f;
        private static readonly string[] BeamShouts = { "Dov'è l'espresso?!", "Che schifo!", "Caffè americano?!", "Nooo, il frappé!", "Vergogna!" };
        private readonly List<Enemy> knifeHits = new List<Enemy>(64);

        /// <summary>Horizontal direction the main weapon fires in.</summary>
        public Vector3 AimDir
        {
            get
            {
                if (FirstPerson) return Quaternion.Euler(0f, LookYaw, 0f) * Vector3.forward;
                if (StickAiming) return stickAim;
                if (MouseAiming)
                {
                    var d = MouseGround - Position; d.y = 0f;
                    if (d.sqrMagnitude > 0.04f) return d.normalized;
                }
                return Facing;
            }
        }

        /// <summary>Ground point being aimed at (crosshair, mouse cursor), clamped to a throwing range.</summary>
        public Vector3 AimPoint(float maxDistance)
        {
            if (MouseAiming)
            {
                var d = MouseGround - Position; d.y = 0f;
                return G.Arena.OnGround(Position + Vector3.ClampMagnitude(d, maxDistance));
            }
            if (!FirstPerson) return G.Arena.OnGround(Position + AimDir * Mathf.Min(7f, maxDistance));
            var eye = Position + Vector3.up * PlayerAnim.EyeHeight;
            var f = LookForward;
            var p = G.Arena.RaycastGround(new Ray(eye, f), out var hit) ? hit : eye + f * maxDistance;
            var flat = p - Position; flat.y = 0f;
            if (flat.magnitude > maxDistance) flat = flat.normalized * maxDistance;
            return G.Arena.OnGround(Position + flat);
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
            knifeTime = 0f;
            beamTime = 0f;
            BuffTime = 0f;
            Special = null;
            SpecialCd = 0f;
            mouseAimTimer = stickAimTimer = 0f;
            mouseMoveArmed = false;
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
            bool acceptInput = G.Game == null || !G.Game.GameplayInputBlocked;
            if (!acceptInput) mouseMoveArmed = false;
            Vector2 input = acceptInput ? Controls.Move() : Vector2.zero;
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            int sw = acceptInput ? Controls.SwitchWeapon() : 0;
            if (sw != 0 && Weapons.Count > 1) SetMain((MainIndex + sw + Weapons.Count) % Weapons.Count);
            int slot = acceptInput ? Controls.WeaponSlotKey() : -1;
            if (slot >= 0) SetMain(slot);

            if (FirstPerson)
            {
                var look = acceptInput ? Controls.Look() : Vector2.zero;
                LookYaw = Mathf.Repeat(LookYaw + look.x, 360f);
                LookPitch = Mathf.Clamp(LookPitch - look.y, -55f, 75f);
                dir = Quaternion.Euler(0f, LookYaw, 0f) * dir;
            }
            else if (acceptInput)
            {
                UpdateTopDownAim(dt);
                // Hold the left mouse button to walk toward the cursor.
                if (!Controls.MouseMoveHeld) mouseMoveArmed = true;
                if (dir.sqrMagnitude < 0.01f && mouseMoveArmed && Controls.MouseMoveHeld)
                {
                    var to = MouseGround - Position; to.y = 0f;
                    float d = to.magnitude;
                    if (d > 0.6f) dir = to / d * Mathf.Clamp01(d / 1.5f);
                }
            }

            SpecialCd -= dt;
            BuffTime -= dt;
            if (acceptInput && Controls.Special()) TryUseSpecial();

            dashCd -= dt;
            if (acceptInput && Controls.Dash() && dashCd <= 0f)
            {
                dashDir = dir.sqrMagnitude > 0.05f ? dir.normalized : Facing;
                dashTime = 0.2f;
                dashCd = DashCooldown;
                G.Sfx.Play(SfxId.Dash, 0.5f, 1f);
                G.Fx.Burst(Position + Vector3.up * 0.3f, new Color(1f, 1f, 1f, 0.6f), 6, 2f, 0.7f, FxKind.Puff);
            }

            Vector3 target = dir * MoveSpeed * (BuffTime > 0f ? 1.4f : 1f) * (beamTime > 0f ? 0.55f : 1f);
            if (dashTime > 0f)
            {
                dashTime -= dt;
                target = dashDir * (knifeTime > 0f ? 30f : 21f);
                if (knifeTime > 0f) KnifeSweep();
                Velocity = target;
                if (Random.value < 0.5f) G.Fx.Burst(Position + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.35f), 1, 0.5f, 0.6f, FxKind.Puff);
            }
            else Velocity = Vector3.MoveTowards(Velocity, target, dt * 70f);

            knifeTime -= dt;
            if (beamTime > 0f) TickBeam(dt);
            if (beamTime > 0f && !FirstPerson) Facing = beamDir;
            else if (FirstPerson || (HasAim && knifeTime <= 0f)) Facing = AimDir;
            else if (dir.sqrMagnitude > 0.01f) Facing = Vector3.Slerp(Facing, dir.normalized, 1f - Mathf.Exp(-18f * dt)).normalized;

            var next = transform.position + Velocity * dt;
            next = G.Arena.Resolve(next, Radius);
            transform.position = next;

            invuln -= dt;
            hurtFlash -= dt;
            if (Regen > 0f) Heal(Regen * dt, false);
            if (Hp < MaxHp && G.Arena.InArea(Position, Arena.AreaKind.Heal))
            {
                Heal(G.Arena.DrawHeal(Position, Mathf.Min(4f * dt, MaxHp - Hp)), false);
                healFx -= dt;
                if (healFx <= 0f)
                {
                    healFx = 0.35f;
                    G.Fx.Burst(Position + Vector3.up * 0.6f, new Color(0.5f, 1f, 0.6f, 0.9f), 2, 1.5f, 0.25f, FxKind.Spark);
                }
            }
            if (BuffTime > 0f && Random.value < 0.3f)
                G.Fx.Burst(Position + Vector3.up * 1.2f, new Color(1f, 0.9f, 0.6f, 0.9f), 1, 1.5f, 0.2f, FxKind.Spark);

            for (int i = 0; i < Weapons.Count; i++) Weapons[i].Tick(dt);
            Anim.Tick(dt, Velocity.magnitude, Facing, hurtFlash > 0f, dashTime > 0f);
        }

        private void UpdateTopDownAim(float dt)
        {
            mouseAimTimer -= dt;
            stickAimTimer -= dt;
            if (Controls.TestMouseWorld.HasValue)
            {
                MouseGround = Controls.TestMouseWorld.Value;
                mouseAimTimer = 3f;
            }
            else if (Mouse.current != null && G.Cam != null && G.Cam.Cam != null)
            {
                var mp = Controls.MousePosition;
                if ((mp - lastMousePos).sqrMagnitude > 4f || Controls.MouseAnyButton) mouseAimTimer = 3f;
                lastMousePos = mp;
                var ray = G.Cam.Cam.ScreenPointToRay(mp);
                if (G.Arena.RaycastGround(ray, out var ground)) MouseGround = ground;
            }
            var stick = Controls.AimStick();
            if (stick != Vector2.zero)
            {
                stickAim = new Vector3(stick.x, 0f, stick.y).normalized;
                stickAimTimer = 0.8f;
                mouseAimTimer = 0f;
            }
        }

        // ---------------- special weapons ----------------

        public void GiveSpecial(SpecialId id)
        {
            Special = id;
            SpecialCd = Mathf.Min(SpecialCd, 0.4f);
            G.Sfx.Play(SfxId.Chest, 0.6f, 1.3f);
            if (G.Hud != null) G.Hud.SpecialAcquired(id);
        }

        private void TryUseSpecial()
        {
            if (!Special.HasValue) return;
            if (SpecialCd > 0f) { G.Sfx.Play(SfxId.Select, 0.4f, 0.6f); return; }
            UseSpecial(Special.Value);
            SpecialCd = SpecialCdMax;
        }

        public void UseSpecial(SpecialId id)
        {
            var dir = AimDir;
            if (!HasAim)
            {
                // No manual aim: point it at the nearest Italian so a keyboard-only player still hits things.
                var t = G.Enemies.Nearest(Position, 16f);
                if (t != null) { var d = t.pos - Position; d.y = 0f; if (d.sqrMagnitude > 0.01f) dir = d.normalized; }
            }
            switch (id)
            {
                case SpecialId.Bazooka:
                    {
                        var s = G.Shots.Fire(ShotKind.Bazooka, Motion.Straight, Position + Vector3.up * 1.2f + dir * 0.6f, dir * 22f, 20f * Might, 0.7f, 1.3f, SpecialSlot);
                        s.pierce = 1;
                        s.scale = 1.4f;
                        s.explodeRadius = 4.8f * AreaMul;
                        s.explodeDamage = 70f * Might;
                        s.explodeKnock = 9f;
                        Anim.Throw();
                        G.Sfx.Play(SfxId.Slam, 0.6f, 1.5f);
                        G.Cam.Kick(3f);
                        break;
                    }
                case SpecialId.KnifeDash:
                    dashDir = dir;
                    dashTime = 0.3f;
                    knifeTime = 0.3f;
                    knifeHits.Clear();
                    G.Sfx.Play(SfxId.Whoosh, 0.9f, 1.8f);
                    G.Sfx.Play(SfxId.Snap, 0.6f, 1.4f);
                    break;
                case SpecialId.Parmesan:
                    {
                        var target = HasAim ? AimPoint(14f) : Position + dir * 7f;
                        target = G.Arena.Clamp(target, 1f);
                        var s = G.Shots.Fire(ShotKind.Parmesan, Motion.Lob, Position + Vector3.up * 1.4f, Vector3.zero, 0f, 0.3f, 0.6f, SpecialSlot);
                        s.target = target;
                        s.spinRate = 600f;
                        float area = AreaMul, might = Might, dur = DurationMul;
                        s.onEnd = x =>
                        {
                            var zone = G.Shots.Zone(x.target, 4.6f * area, 8f * might, 3.5f * dur, 0.5f, SpecialSlot, new Color(1f, 0.95f, 0.65f, 0.5f), 1f);
                            zone.sticky = true;
                            for (int i = 0; i < 16; i++)
                                G.Fx.Burst(x.target + Random.insideUnitSphere * 3f + Vector3.up, new Color(1f, 0.96f, 0.75f, 0.7f), 1, 1.5f, 2.2f, FxKind.Puff);
                            G.Sfx.Play(SfxId.Poof, 0.9f, 0.6f);
                        };
                        Anim.Throw();
                        break;
                    }
                case SpecialId.Sprinkler:
                    G.Shots.Turret(Position + Facing * 1.2f, 8f * DurationMul, 9f * Might, SpecialSlot);
                    G.Sfx.Play(SfxId.Splat, 0.7f, 0.8f);
                    break;
                case SpecialId.StarBeam:
                    beamTime = 2.6f * DurationMul;
                    beamDir = dir;
                    beamTick = 0f;
                    beamSfx = 0f;
                    Anim.SetBeaming(true);
                    G.Fx.Shout(Position + Vector3.up * 2.6f, "スタ〇ビーム!!", new Color(0.4f, 1f, 0.6f));
                    G.Cam.Shake(0.25f);
                    break;
                case SpecialId.SugarEspresso:
                    BuffTime = 7f * DurationMul;
                    G.Sfx.Play(SfxId.LevelUp, 0.7f, 1.5f);
                    G.Fx.Ring(Position + Vector3.up * 0.1f, 3f, new Color(1f, 0.85f, 0.4f, 0.9f), 0.5f, 0.5f);
                    G.Fx.Shout(Position + Vector3.up * 2.6f, "ZUCCHERO!!", new Color(1f, 0.9f, 0.5f));
                    break;
            }
        }

        /// <summary>Sta○ Beam: a sweeping beam that stops at cover, damages and scares everything it touches.</summary>
        private void TickBeam(float dt)
        {
            beamTime -= dt;
            Vector3 want = beamDir;
            if (HasAim) want = AimDir;
            else
            {
                var t = G.Enemies.Nearest(Position, 18f, -1, true);
                if (t != null) { var d = t.pos - Position; d.y = 0f; if (d.sqrMagnitude > 0.01f) want = d.normalized; }
            }
            beamDir = Vector3.Slerp(beamDir, want, 1f - Mathf.Exp(-7f * dt)).normalized;
            var origin = Position + Vector3.up * 1.2f + beamDir * 0.7f;
            float length = 0.5f;
            while (length < 18f && !G.Arena.ShotBlocked(origin + beamDir * length)) length += 0.5f;
            G.Fx.Beam(origin, beamDir, length, beamTime > 0f);
            beamSfx -= dt;
            if (beamSfx <= 0f) { beamSfx = 0.4f; G.Sfx.Play(SfxId.Beam, 0.55f, Random.Range(0.95f, 1.05f)); }
            beamTick -= dt;
            if (beamTick <= 0f)
            {
                beamTick = 0.12f;
                float now = Time.time;
                for (float d = 0.6f; d <= length; d += 1.2f)
                {
                    int n = G.Enemies.Query(origin + beamDir * d, 1.1f, knifeHits);
                    for (int i = 0; i < n; i++)
                    {
                        var e = knifeHits[i];
                        if (e.immune[SpecialSlot] > now) continue;
                        e.immune[SpecialSlot] = now + 0.11f;
                        G.Enemies.Damage(e, 12f * Might, beamDir, 1.5f, SpecialSlot);
                        if (!e.IsBoss && e.active && !e.fleeing)
                        {
                            if (e.fear <= 0f) G.Enemies.ShoutCustom(e, BeamShouts[Random.Range(0, BeamShouts.Length)], new Color(0.7f, 1f, 0.75f));
                            e.fear = Mathf.Max(e.fear, 2f);
                        }
                    }
                }
            }
            if (beamTime <= 0f)
            {
                G.Fx.Beam(origin, beamDir, 0f, false);
                Anim.SetBeaming(false);
            }
        }

        private void KnifeSweep()
        {
            int n = G.Enemies.Query(Position, 1.7f, knifeHits);
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var e = knifeHits[i];
                if (e.immune[SpecialSlot] > now) continue;
                e.immune[SpecialSlot] = now + 0.5f;
                var side = Vector3.Cross(Vector3.up, dashDir);
                var push = side * Mathf.Sign(Vector3.Dot(e.pos - Position, side) + 0.001f);
                G.Enemies.Damage(e, 45f * Might, push, 5f, SpecialSlot);
                G.Fx.Burst(e.Center, new Color(1f, 1f, 1f, 0.9f), 4, 5f, 0.15f, FxKind.Spark);
            }
            if (Random.value < 0.8f) G.Fx.Burst(Position + Vector3.up * 1f, new Color(0.85f, 0.9f, 1f, 0.8f), 2, 0.5f, 0.5f, FxKind.Spark);
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
