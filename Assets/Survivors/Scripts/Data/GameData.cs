using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum WeaponId { SpaghettiSnap, PenneShot, LasagnaCrash, FusilliOrbit, FarfalleBoomerang, KetchupBomb, PineapplePizza, CarbonaraAura }
    public enum PassiveId { Parmigiano, Espresso, Semolina, BigPot, OliveOil, Fork, Basil, RecipeBook }

    public struct WStats
    {
        public float damage, cooldown, area, speed, duration, knockback;
        public int amount, pierce;

        public static WStats operator +(WStats a, WStats b) => new WStats
        {
            damage = a.damage + b.damage, cooldown = a.cooldown + b.cooldown, area = a.area + b.area,
            speed = a.speed + b.speed, duration = a.duration + b.duration, knockback = a.knockback + b.knockback,
            amount = a.amount + b.amount, pierce = a.pierce + b.pierce
        };
    }

    public class WeaponDef
    {
        public const int MaxLevel = 8;
        public WeaponId id;
        public string name, evoName, desc, evoDesc, icon, evoIcon;
        /// <summary>Bonus trait while this is the selected main weapon.</summary>
        public string mainTrait;
        public PassiveId evoPartner;
        public WStats baseStats, evoBonus;
        public WStats[] levels;      // levels[0] is applied when reaching Lv2
        public string[] levelText;
    }

    public class PassiveDef
    {
        public PassiveId id;
        public string name, desc, icon;
        public int maxLevel;
    }

    public enum Behavior { Chase, Thrower, Charger, Sweeper, Elite, Boss, Prop }
    public enum EnemyKind { Signore, Tifoso, Mamma, Chef, Vespista, Gondoliere, Pizzaiolo, Mafioso, Nonna, BossNonna, BossCapitano, BossDon, BossBikeNonna, Barrel }
    public enum ShotKind { Penne, Fusilli, Farfalle, Ketchup, Pizza, Slipper, Dough, Meatball, Pineapple, Oar, Bazooka, Drop, Parmesan }

    /// <summary>Special weapons: picked up on the map, fired manually, then cool down.</summary>
    public enum SpecialId { Bazooka, KnifeDash, Parmesan, Sprinkler, SugarEspresso, StarBeam }

    public class SpecialDef
    {
        public SpecialId id;
        public string name, desc, icon;
        public float cooldown;
    }

    public class EnemyDef
    {
        public EnemyKind kind;
        public string name;
        public float hp, speed, damage, radius = 0.42f, scale = 1f, knockResist, attackRange, attackCooldown = 3f;
        public int xp;
        public Behavior behavior;
        public ShotKind shot;
        /// <summary>A boss that comes back as this kind when beaten instead of clearing the stage.</summary>
        public EnemyKind? nextPhase;
        /// <summary>Title-card line and banner shown when this boss arrives as a later phase.</summary>
        public string entranceLine, entranceBanner;
        public bool IsBoss => behavior == Behavior.Boss;
        public bool IsProp => behavior == Behavior.Prop;
    }

    public enum StageTheme { Roma, Venezia, Napoli }

    public struct WavePhase
    {
        public float start;
        public int minAlive;
        public float interval;
        public int batch;
        public (EnemyKind kind, float weight)[] mix;
    }

    public enum StageEventType { Ring, Stampede, Swarm, Elite, Boss, Music }

    public struct StageEvent
    {
        public float time;
        public StageEventType type;
        public EnemyKind kind;
        public int count;
        public string banner;
        /// <summary>Music events: the track in Resources/Music to crossfade to.</summary>
        public string music;
    }

    public class StageDef
    {
        public int index;
        public StageTheme theme;
        public string name, sub, bossName;
        public float duration = 600f, hpMul = 1f, damageMul = 1f;
        /// <summary>
        /// Enemy scaling on top of the stage multiplier: HP and damage grow with elapsed minutes and with the
        /// player's level, so a strong build still meets resistance late in the run. Bosses use half the level term.
        /// </summary>
        public float hpPerMinute = 0.075f, hpPerLevel = 0.03f, damagePerMinute = 0.04f, damagePerLevel = 0.01f;
        public EnemyKind boss;
        /// <summary>
        /// Track in Resources/Music (file name without extension) that opens the stage; change it mid-stage with a
        /// Music event. Without one the stage plays its synthesised theme, and the boss the synthesised boss theme.
        /// </summary>
        public string music, bossMusic;
        public WavePhase[] phases;
        public StageEvent[] events;
    }

    public class CharacterDef
    {
        public string name, title, desc;
        public WeaponId start;
        public Color shirt, pants, hair, skin;
        public float areaBonus, speedBonus, growthBonus, mightBonus;
    }

    public class ShopDef
    {
        public string name, desc, icon;
        public int maxLevel, baseCost;
    }

    public static class GameData
    {
        public static readonly WeaponDef[] Weapons;
        public static readonly PassiveDef[] Passives;
        public static readonly Dictionary<EnemyKind, EnemyDef> Enemies = new Dictionary<EnemyKind, EnemyDef>();
        public static readonly StageDef[] Stages;
        public static readonly CharacterDef[] Characters;
        public static readonly ShopDef[] Shop;
        public static readonly SpecialDef[] Specials;
        public static SpecialDef Special(SpecialId id) => Specials[(int)id];

        public static WeaponDef Weapon(WeaponId id) => Weapons[(int)id];
        public static PassiveDef Passive(PassiveId id) => Passives[(int)id];
        public static EnemyDef Enemy(EnemyKind kind) => Enemies[kind];

        private static WStats S(float dmg = 0, float cd = 0, float area = 0, float spd = 0, float dur = 0,
            float kb = 0, int amt = 0, int pierce = 0) => new WStats
        {
            damage = dmg, cooldown = cd, area = area, speed = spd, duration = dur, knockback = kb, amount = amt, pierce = pierce
        };

        public static int XpToNext(int level)
        {
            // Quick early levels, then the classic survivors ramp.
            if (level <= 1) return 5;
            if (level < 20) return 5 + (level - 1) * 8;
            if (level < 40) return 157 + (level - 20) * 12;
            return 397 + (level - 40) * 16;
        }

        static GameData()
        {
            Weapons = new[]
            {
                new WeaponDef
                {
                    id = WeaponId.SpaghettiSnap, mainTrait = "主武器：当たったイタリア人を0.6秒気絶させる（群れの足止め）", name = "スパゲッティ折り", icon = "spaghetti", evoIcon = "domino",
                    desc = "向いている方向でスパゲッティを真っ二つ。扇状の衝撃波でイタリア人を吹き飛ばす。",
                    evoName = "スパゲッティ・ドミノ", evoPartner = PassiveId.Parmigiano,
                    evoDesc = "吹き飛んだイタリア人が弾丸になり、ぶつかった仲間も巻き込む。",
                    baseStats = S(dmg: 11, cd: 1.3f, area: 1f, kb: 3f, amt: 1),
                    evoBonus = S(dmg: 12, area: 0.25f, kb: 2f),
                    levels = new[] { S(amt: 1), S(dmg: 6), S(area: 0.15f, kb: 0.5f), S(dmg: 6), S(cd: -0.15f), S(area: 0.2f), S(dmg: 10) },
                    levelText = new[] { "背後にも折る", "ダメージ +6", "範囲 +15%", "ダメージ +6", "クールダウン -0.15秒", "範囲 +20%", "ダメージ +10" }
                },
                new WeaponDef
                {
                    id = WeaponId.PenneShot, mainTrait = "主武器：発射数+1・貫通+1（ボスやエリートに）", name = "ペンネ・ショット", icon = "penne", evoIcon = "rigatoni",
                    desc = "折ったペンネを一番近いイタリア人へ撃ち出す。",
                    evoName = "リガトーニ・ガトリング", evoPartner = PassiveId.Espresso,
                    evoDesc = "リガトーニを途切れなく連射する。貫通 +2。",
                    baseStats = S(dmg: 9, cd: 1.1f, area: 1f, spd: 17f, dur: 1.5f, kb: 1f, amt: 1, pierce: 1),
                    evoBonus = S(dmg: 4, pierce: 2),
                    levels = new[] { S(amt: 1), S(cd: -0.15f), S(amt: 1), S(dmg: 6), S(amt: 1), S(pierce: 1), S(dmg: 6) },
                    levelText = new[] { "発射数 +1", "クールダウン -0.15秒", "発射数 +1", "ダメージ +6", "発射数 +1", "貫通 +1", "ダメージ +6" }
                },
                new WeaponDef
                {
                    id = WeaponId.LasagnaCrash, mainTrait = "主武器：吹き飛ばし2倍（囲まれた時の脱出に）", name = "ラザニア叩き割り", icon = "lasagna", evoIcon = "quake",
                    desc = "ラザニアの板を地面に叩きつけて割る。周囲を大きく吹き飛ばす。",
                    evoName = "ラザニア大地震", evoPartner = PassiveId.Semolina,
                    evoDesc = "三重の地割れが広がり、当たった敵を気絶させる。",
                    baseStats = S(dmg: 20, cd: 3.0f, area: 1f, kb: 6f, amt: 1),
                    evoBonus = S(dmg: 10, area: 0.2f),
                    levels = new[] { S(dmg: 8), S(area: 0.2f), S(cd: -0.4f), S(dmg: 8), S(area: 0.2f), S(amt: 1), S(dmg: 14) },
                    levelText = new[] { "ダメージ +8", "範囲 +20%", "クールダウン -0.4秒", "ダメージ +8", "範囲 +20%", "二度叩き", "ダメージ +14" }
                },
                new WeaponDef
                {
                    id = WeaponId.FusilliOrbit, mainTrait = "主武器：飛んでくるスリッパや生地を叩き落とす（投擲対策）", name = "フジッリ旋風", icon = "fusilli", evoIcon = "tornado",
                    desc = "折れたフジッリが周囲をぐるぐる回る。",
                    evoName = "フジッリ・トルネード", evoPartner = PassiveId.Basil,
                    evoDesc = "フジッリが止まらなくなる。範囲とダメージも上昇。",
                    baseStats = S(dmg: 9, cd: 4.0f, area: 1f, spd: 1f, dur: 3f, kb: 1.5f, amt: 1),
                    evoBonus = S(dmg: 10, area: 0.25f, spd: 0.3f),
                    levels = new[] { S(amt: 1), S(spd: 0.3f, area: 0.25f), S(dur: 0.5f, dmg: 5), S(amt: 1), S(spd: 0.3f, area: 0.25f), S(dur: 0.5f, dmg: 5), S(amt: 1) },
                    levelText = new[] { "数 +1", "回転速度・範囲アップ", "持続 +0.5秒、ダメージ +5", "数 +1", "回転速度・範囲アップ", "持続 +0.5秒、ダメージ +5", "数 +1" }
                },
                new WeaponDef
                {
                    id = WeaponId.FarfalleBoomerang, mainTrait = "主武器：発射数+1（行列をまとめて）", name = "ファルファッレ・ブーメラン", icon = "farfalle", evoIcon = "hurricane",
                    desc = "蝶ネクタイ型パスタを投げる。戻ってくる時も当たる。",
                    evoName = "ファルファッレ・ハリケーン", evoPartner = PassiveId.Fork,
                    evoDesc = "巨大化したファルファッレが群れで舞う。",
                    baseStats = S(dmg: 13, cd: 2.2f, area: 1f, spd: 13f, kb: 2f, amt: 1, pierce: 999),
                    evoBonus = S(dmg: 8, area: 0.4f, amt: 2, spd: 3f),
                    levels = new[] { S(dmg: 6), S(area: 0.2f), S(amt: 1), S(spd: 3f), S(dmg: 6), S(amt: 1), S(dmg: 10) },
                    levelText = new[] { "ダメージ +6", "範囲 +20%", "発射数 +1", "速度アップ", "ダメージ +6", "発射数 +1", "ダメージ +10" }
                },
                new WeaponDef
                {
                    id = WeaponId.KetchupBomb, mainTrait = "主武器：踏んだ敵がベタベタで動けない（足止め）", name = "ケチャップ爆弾", icon = "ketchup", evoIcon = "flood",
                    desc = "パスタにケチャップ!? 敵の密集地にボトルを投げつける。割れた瞬間に周囲を焼き、水たまりが足止めしながら焼き続ける。",
                    evoName = "ケチャップ大洪水", evoPartner = PassiveId.BigPot,
                    evoDesc = "巨大な水たまりが敵を追いかけて広がる。",
                    baseStats = S(dmg: 9, cd: 3.3f, area: 1f, dur: 3.2f, kb: 2f, amt: 1),
                    evoBonus = S(dmg: 3, area: 0.2f, dur: 1f),
                    levels = new[] { S(amt: 1), S(dmg: 4, dur: 0.5f), S(area: 0.2f), S(amt: 1), S(dur: 0.5f, dmg: 4), S(area: 0.2f), S(amt: 1, dmg: 5) },
                    levelText = new[] { "発射数 +1", "ダメージ +4、持続 +0.5秒", "範囲 +20%", "発射数 +1", "持続 +0.5秒、ダメージ +4", "範囲 +20%", "発射数 +1、ダメージ +5" }
                },
                new WeaponDef
                {
                    id = WeaponId.PineapplePizza, mainTrait = "主武器：枚数+1・速度アップ（画面全体を掃除）", name = "パイナップルピザ", icon = "pizza", evoIcon = "meteor",
                    desc = "禁断のピザが画面内を跳ね回る。イタリア人は激怒して逃げ出す。",
                    evoName = "ハワイアン・メテオ", evoPartner = PassiveId.OliveOil,
                    evoDesc = "跳ねるたびにパイナップルが爆発する。",
                    baseStats = S(dmg: 10, cd: 3.2f, area: 1f, spd: 9f, dur: 2.5f, kb: 1.5f, amt: 1, pierce: 999),
                    evoBonus = S(dmg: 6, dur: 1f),
                    levels = new[] { S(amt: 1), S(dmg: 5), S(spd: 2f, dur: 0.5f), S(amt: 1), S(dmg: 5), S(dur: 0.8f), S(amt: 1) },
                    levelText = new[] { "枚数 +1", "ダメージ +5", "速度・持続アップ", "枚数 +1", "ダメージ +5", "持続 +0.8秒", "枚数 +1" }
                },
                new WeaponDef
                {
                    id = WeaponId.CarbonaraAura, mainTrait = "主武器：命中時にHP回復（攻撃1回につき最大1.5HP）", name = "生クリームカルボナーラ", icon = "carbonara", evoIcon = "cream",
                    desc = "カルボナーラに生クリーム!? 周囲のイタリア人にダメージを与え続ける。",
                    evoName = "クリーム地獄", evoPartner = PassiveId.RecipeBook,
                    evoDesc = "範囲が広がり、命中時にHP回復（攻撃1回につき最大1.5HP）。敵を遅くする。",
                    baseStats = S(dmg: 5, cd: 0.9f, area: 1f, kb: 0.6f, amt: 1),
                    evoBonus = S(dmg: 4, area: 0.4f),
                    levels = new[] { S(dmg: 2, area: 0.1f), S(cd: -0.1f), S(area: 0.15f), S(dmg: 2), S(cd: -0.1f), S(area: 0.15f), S(dmg: 3) },
                    levelText = new[] { "ダメージ +2、範囲 +10%", "間隔 -0.1秒", "範囲 +15%", "ダメージ +2", "間隔 -0.1秒", "範囲 +15%", "ダメージ +3" }
                },
            };

            Passives = new[]
            {
                new PassiveDef { id = PassiveId.Parmigiano, name = "パルミジャーノ", icon = "cheese", maxLevel = 5, desc = "ダメージ +10%" },
                new PassiveDef { id = PassiveId.Espresso, name = "エスプレッソ", icon = "espresso", maxLevel = 5, desc = "クールダウン -8%" },
                new PassiveDef { id = PassiveId.Semolina, name = "硬質セモリナ", icon = "semolina", maxLevel = 5, desc = "攻撃範囲 +10%" },
                new PassiveDef { id = PassiveId.BigPot, name = "大鍋", icon = "pot", maxLevel = 2, desc = "発射数 +1" },
                new PassiveDef { id = PassiveId.OliveOil, name = "オリーブオイル", icon = "oil", maxLevel = 5, desc = "移動速度 +10%" },
                new PassiveDef { id = PassiveId.Fork, name = "マイフォーク", icon = "fork", maxLevel = 5, desc = "回収範囲 +30%" },
                new PassiveDef { id = PassiveId.Basil, name = "バジル", icon = "basil", maxLevel = 5, desc = "最大HP +15%、毎秒0.3回復" },
                new PassiveDef { id = PassiveId.RecipeBook, name = "ノンナのレシピ帳", icon = "book", maxLevel = 5, desc = "経験値 +8%、持続 +5%" },
            };

            void E(EnemyKind kind, string name, float hp, float speed, float dmg, int xp, Behavior behavior,
                float radius = 0.42f, float scale = 1f, float kr = 0f, float range = 0f, float cd = 3f, ShotKind shot = ShotKind.Slipper)
                => Enemies[kind] = new EnemyDef
                {
                    kind = kind, name = name, hp = hp, speed = speed, damage = dmg, xp = xp, behavior = behavior, radius = radius,
                    scale = scale, knockResist = kr, attackRange = range, attackCooldown = cd, shot = shot
                };
            E(EnemyKind.Signore, "シニョーレ", 10, 2.6f, 5, 1, Behavior.Chase);
            E(EnemyKind.Tifoso, "ティフォーゾ", 6, 3.7f, 3, 1, Behavior.Chase, 0.38f, 0.9f);
            E(EnemyKind.Mamma, "マンマ", 22, 2.3f, 6, 2, Behavior.Thrower, 0.45f, 1f, 0.2f, 9f, 6.5f, ShotKind.Slipper);
            E(EnemyKind.Chef, "シェフ", 48, 2.1f, 9, 3, Behavior.Chase, 0.5f, 1.15f, 0.5f);
            E(EnemyKind.Vespista, "ベスパ乗り", 16, 2.4f, 10, 2, Behavior.Charger, 0.55f, 1f, 0.3f, 13f, 3.2f);
            E(EnemyKind.Gondoliere, "ゴンドリエーレ", 34, 2.4f, 10, 3, Behavior.Sweeper, 0.45f, 1.05f, 0.35f, 3.2f, 3.6f, ShotKind.Oar);
            E(EnemyKind.Pizzaiolo, "ピッツァイオーロ", 30, 2.1f, 9, 3, Behavior.Thrower, 0.45f, 1f, 0.3f, 10f, 5f, ShotKind.Dough);
            E(EnemyKind.Mafioso, "マフィオーソ", 75, 2.6f, 11, 5, Behavior.Chase, 0.5f, 1.1f, 0.65f);
            E(EnemyKind.Nonna, "ノンナ", 420, 1.8f, 12, 25, Behavior.Elite, 0.65f, 1.35f, 0.85f);
            E(EnemyKind.BossNonna, "グランデ・ノンナ", 2200, 2.1f, 22, 200, Behavior.Boss, 1.5f, 2.9f, 1f, 14f, 3.4f, ShotKind.Slipper);
            // Grande Nonna's second form: beaten once, she grows wheels and comes back as a bicycle.
            E(EnemyKind.BossBikeNonna, "バイクばばぁ", 2400, 3.2f, 24, 250, Behavior.Boss, 1.5f, 2.2f, 1f, 14f, 3.0f, ShotKind.Slipper);
            Enemies[EnemyKind.BossNonna].nextPhase = EnemyKind.BossBikeNonna;
            Enemies[EnemyKind.BossBikeNonna].entranceLine = "“If my grandma had wheels, she would've been a bike.”<size=24>  — Gino D'Acampo</size>";
            Enemies[EnemyKind.BossBikeNonna].entranceBanner = "グランデ・ノンナに車輪が生えた！";
            E(EnemyKind.BossCapitano, "イル・カピターノ", 4200, 2.3f, 26, 200, Behavior.Boss, 1.4f, 2.7f, 1f, 7f, 3.0f, ShotKind.Oar);
            E(EnemyKind.BossDon, "ドン・カルボナーラ", 5600, 2.2f, 30, 200, Behavior.Boss, 1.4f, 2.7f, 1f, 16f, 2.8f, ShotKind.Meatball);
            E(EnemyKind.Barrel, "ワイン樽", 1, 0, 0, 0, Behavior.Prop, 0.55f, 1f, 1f);

            (EnemyKind, float)[] M(params (EnemyKind, float)[] mix) => mix;
            Stages = new[]
            {
                new StageDef
                {
                    index = 0, theme = StageTheme.Roma, name = "ローマ", sub = "トラステヴェレの広場", boss = EnemyKind.BossNonna,
                    bossName = "グランデ・ノンナ", hpMul = 1f, damageMul = 1f, music = "Pasta la vista",
                    hpPerLevel = 0.02f, damagePerLevel = 0.006f, // the first stage stays forgiving for new players
                    phases = new[]
                    {
                        new WavePhase { start = 0, minAlive = 10, interval = 1.5f, batch = 2, mix = M((EnemyKind.Signore, 10)) },
                        new WavePhase { start = 40, minAlive = 18, interval = 1.2f, batch = 2, mix = M((EnemyKind.Signore, 8), (EnemyKind.Tifoso, 4)) },
                        new WavePhase { start = 90, minAlive = 30, interval = 1f, batch = 3, mix = M((EnemyKind.Signore, 6), (EnemyKind.Tifoso, 6), (EnemyKind.Mamma, 1.2f)) },
                        new WavePhase { start = 150, minAlive = 42, interval = 0.9f, batch = 3, mix = M((EnemyKind.Signore, 5), (EnemyKind.Tifoso, 5), (EnemyKind.Mamma, 1.8f), (EnemyKind.Vespista, 1.5f)) },
                        new WavePhase { start = 240, minAlive = 60, interval = 0.8f, batch = 4, mix = M((EnemyKind.Signore, 4), (EnemyKind.Mamma, 2.5f), (EnemyKind.Chef, 2), (EnemyKind.Vespista, 2), (EnemyKind.Tifoso, 5)) },
                        new WavePhase { start = 330, minAlive = 80, interval = 0.7f, batch = 4, mix = M((EnemyKind.Chef, 3), (EnemyKind.Mamma, 4), (EnemyKind.Signore, 3), (EnemyKind.Tifoso, 5), (EnemyKind.Vespista, 3)) },
                        new WavePhase { start = 420, minAlive = 110, interval = 0.6f, batch = 5, mix = M((EnemyKind.Chef, 4), (EnemyKind.Mamma, 4), (EnemyKind.Tifoso, 6), (EnemyKind.Vespista, 3), (EnemyKind.Signore, 3)) },
                        new WavePhase { start = 510, minAlive = 150, interval = 0.5f, batch = 6, mix = M((EnemyKind.Chef, 5), (EnemyKind.Mamma, 4), (EnemyKind.Tifoso, 8), (EnemyKind.Vespista, 4)) },
                        new WavePhase { start = 600, minAlive = 40, interval = 1.5f, batch = 3, mix = M((EnemyKind.Tifoso, 6), (EnemyKind.Signore, 4)) },
                    },
                    events = new[]
                    {
                        new StageEvent { time = 60, type = StageEventType.Ring, kind = EnemyKind.Tifoso, count = 20, banner = "ティフォシに囲まれた！" },
                        new StageEvent { time = 120, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 1, banner = "ノンナが来た！ 倒すと宝箱" },
                        new StageEvent { time = 180, type = StageEventType.Stampede, kind = EnemyKind.Vespista, count = 8, banner = "ベスパの暴走族！" },
                        new StageEvent { time = 270, type = StageEventType.Swarm, kind = EnemyKind.Tifoso, count = 40, banner = "サッカー帰りの大群！" },
                        new StageEvent { time = 300, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 1, banner = "ノンナが来た！" },
                        new StageEvent { time = 390, type = StageEventType.Ring, kind = EnemyKind.Signore, count = 30, banner = "包囲された！" },
                        new StageEvent { time = 450, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナ姉妹！" },
                        new StageEvent { time = 480, type = StageEventType.Stampede, kind = EnemyKind.Vespista, count = 12, banner = "ベスパの暴走族！" },
                        new StageEvent { time = 540, type = StageEventType.Ring, kind = EnemyKind.Chef, count = 24, banner = "シェフたちの包囲網！" },
                        new StageEvent { time = 570, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 1, banner = "ノンナが来た！" },
                        new StageEvent { time = 600, type = StageEventType.Boss, kind = EnemyKind.BossNonna, count = 1, banner = "グランデ・ノンナ降臨！" },
                    }
                },
                new StageDef
                {
                    index = 1, theme = StageTheme.Venezia, name = "ヴェネツィア", sub = "サン・マルコの運河広場", boss = EnemyKind.BossCapitano,
                    bossName = "イル・カピターノ", hpMul = 1.7f, damageMul = 1.15f, music = "Pasta la vista",
                    phases = new[]
                    {
                        new WavePhase { start = 0, minAlive = 12, interval = 1.3f, batch = 2, mix = M((EnemyKind.Signore, 8), (EnemyKind.Tifoso, 3)) },
                        new WavePhase { start = 45, minAlive = 22, interval = 1.1f, batch = 3, mix = M((EnemyKind.Signore, 6), (EnemyKind.Gondoliere, 2), (EnemyKind.Tifoso, 4)) },
                        new WavePhase { start = 120, minAlive = 40, interval = 0.9f, batch = 3, mix = M((EnemyKind.Gondoliere, 4), (EnemyKind.Mamma, 3), (EnemyKind.Tifoso, 5), (EnemyKind.Signore, 3)) },
                        new WavePhase { start = 210, minAlive = 60, interval = 0.8f, batch = 4, mix = M((EnemyKind.Gondoliere, 5), (EnemyKind.Mamma, 3), (EnemyKind.Chef, 2), (EnemyKind.Tifoso, 5)) },
                        new WavePhase { start = 300, minAlive = 80, interval = 0.7f, batch = 4, mix = M((EnemyKind.Gondoliere, 5), (EnemyKind.Chef, 3), (EnemyKind.Pizzaiolo, 2), (EnemyKind.Tifoso, 6), (EnemyKind.Vespista, 2)) },
                        new WavePhase { start = 400, minAlive = 110, interval = 0.6f, batch = 5, mix = M((EnemyKind.Gondoliere, 6), (EnemyKind.Chef, 4), (EnemyKind.Mamma, 3), (EnemyKind.Tifoso, 6), (EnemyKind.Mafioso, 1)) },
                        new WavePhase { start = 500, minAlive = 160, interval = 0.5f, batch = 6, mix = M((EnemyKind.Gondoliere, 6), (EnemyKind.Chef, 4), (EnemyKind.Pizzaiolo, 3), (EnemyKind.Tifoso, 8), (EnemyKind.Mafioso, 2)) },
                        new WavePhase { start = 600, minAlive = 50, interval = 1.2f, batch = 3, mix = M((EnemyKind.Gondoliere, 4), (EnemyKind.Tifoso, 6)) },
                    },
                    events = new[]
                    {
                        new StageEvent { time = 60, type = StageEventType.Swarm, kind = EnemyKind.Tifoso, count = 30, banner = "観光客の波！… ではなくティフォシ！" },
                        new StageEvent { time = 110, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 1, banner = "ノンナが来た！" },
                        new StageEvent { time = 180, type = StageEventType.Ring, kind = EnemyKind.Gondoliere, count = 20, banner = "ゴンドラ漕ぎに包囲された！" },
                        new StageEvent { time = 260, type = StageEventType.Stampede, kind = EnemyKind.Vespista, count = 10, banner = "水上ベスパ!?" },
                        new StageEvent { time = 300, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナ姉妹！" },
                        new StageEvent { time = 380, type = StageEventType.Swarm, kind = EnemyKind.Tifoso, count = 50, banner = "カーニバルの大群！" },
                        new StageEvent { time = 450, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナ姉妹！" },
                        new StageEvent { time = 520, type = StageEventType.Ring, kind = EnemyKind.Chef, count = 28, banner = "シェフたちの包囲網！" },
                        new StageEvent { time = 570, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナが来た！" },
                        new StageEvent { time = 600, type = StageEventType.Boss, kind = EnemyKind.BossCapitano, count = 1, banner = "イル・カピターノ見参！" },
                    }
                },
                new StageDef
                {
                    index = 2, theme = StageTheme.Napoli, name = "ナポリ", sub = "ヴェスヴィオを望むピッツェリア通り", boss = EnemyKind.BossDon,
                    bossName = "ドン・カルボナーラ", hpMul = 2.4f, damageMul = 1.3f, music = "Pasta la vista",
                    phases = new[]
                    {
                        new WavePhase { start = 0, minAlive = 14, interval = 1.2f, batch = 2, mix = M((EnemyKind.Signore, 6), (EnemyKind.Tifoso, 5)) },
                        new WavePhase { start = 45, minAlive = 24, interval = 1f, batch = 3, mix = M((EnemyKind.Pizzaiolo, 2), (EnemyKind.Tifoso, 5), (EnemyKind.Vespista, 2), (EnemyKind.Signore, 4)) },
                        new WavePhase { start = 120, minAlive = 48, interval = 0.8f, batch = 4, mix = M((EnemyKind.Pizzaiolo, 4), (EnemyKind.Mafioso, 2), (EnemyKind.Tifoso, 5), (EnemyKind.Vespista, 3)) },
                        new WavePhase { start = 210, minAlive = 70, interval = 0.7f, batch = 4, mix = M((EnemyKind.Pizzaiolo, 4), (EnemyKind.Mafioso, 3), (EnemyKind.Chef, 3), (EnemyKind.Tifoso, 6), (EnemyKind.Vespista, 3)) },
                        new WavePhase { start = 300, minAlive = 95, interval = 0.6f, batch = 5, mix = M((EnemyKind.Mafioso, 4), (EnemyKind.Pizzaiolo, 4), (EnemyKind.Mamma, 3), (EnemyKind.Tifoso, 7), (EnemyKind.Vespista, 3)) },
                        new WavePhase { start = 400, minAlive = 130, interval = 0.5f, batch = 6, mix = M((EnemyKind.Mafioso, 5), (EnemyKind.Chef, 4), (EnemyKind.Pizzaiolo, 4), (EnemyKind.Tifoso, 8), (EnemyKind.Vespista, 3)) },
                        new WavePhase { start = 500, minAlive = 180, interval = 0.45f, batch = 7, mix = M((EnemyKind.Mafioso, 6), (EnemyKind.Chef, 4), (EnemyKind.Pizzaiolo, 4), (EnemyKind.Tifoso, 8), (EnemyKind.Gondoliere, 3)) },
                        new WavePhase { start = 600, minAlive = 60, interval = 1f, batch = 3, mix = M((EnemyKind.Mafioso, 4), (EnemyKind.Tifoso, 6)) },
                    },
                    events = new[]
                    {
                        new StageEvent { time = 50, type = StageEventType.Stampede, kind = EnemyKind.Vespista, count = 10, banner = "ナポリ名物、ベスパの洪水！" },
                        new StageEvent { time = 100, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 1, banner = "ノンナが来た！" },
                        new StageEvent { time = 170, type = StageEventType.Ring, kind = EnemyKind.Mafioso, count = 16, banner = "ファミリーに囲まれた！" },
                        new StageEvent { time = 240, type = StageEventType.Swarm, kind = EnemyKind.Tifoso, count = 60, banner = "ナポリのティフォシ大行進！" },
                        new StageEvent { time = 290, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナ姉妹！" },
                        new StageEvent { time = 360, type = StageEventType.Stampede, kind = EnemyKind.Vespista, count = 16, banner = "ベスパの洪水！" },
                        new StageEvent { time = 420, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 3, banner = "ノンナ三姉妹！" },
                        new StageEvent { time = 480, type = StageEventType.Ring, kind = EnemyKind.Pizzaiolo, count = 30, banner = "ピッツァ職人の包囲網！" },
                        new StageEvent { time = 550, type = StageEventType.Swarm, kind = EnemyKind.Mafioso, count = 30, banner = "ファミリー総出！" },
                        new StageEvent { time = 575, type = StageEventType.Elite, kind = EnemyKind.Nonna, count = 2, banner = "ノンナが来た！" },
                        new StageEvent { time = 600, type = StageEventType.Boss, kind = EnemyKind.BossDon, count = 1, banner = "ドン・カルボナーラ登場！" },
                    }
                },
            };

            Characters = new[]
            {
                new CharacterDef
                {
                    name = "ケンジ", title = "バックパッカー", start = WeaponId.SpaghettiSnap, areaBonus = 0.1f,
                    desc = "スパゲッティ折りで開始。攻撃範囲 +10%。",
                    shirt = new Color(0.86f, 0.2f, 0.18f), pants = new Color(0.2f, 0.28f, 0.5f), hair = new Color(0.1f, 0.08f, 0.07f), skin = new Color(0.98f, 0.8f, 0.64f)
                },
                new CharacterDef
                {
                    name = "ミナ", title = "料理留学生", start = WeaponId.PenneShot, speedBonus = 0.1f,
                    desc = "ペンネ・ショットで開始。移動速度 +10%。",
                    shirt = new Color(0.98f, 0.78f, 0.2f), pants = new Color(0.9f, 0.45f, 0.55f), hair = new Color(0.35f, 0.2f, 0.12f), skin = new Color(1f, 0.84f, 0.7f)
                },
                new CharacterDef
                {
                    name = "タロウ", title = "料理系配信者", start = WeaponId.KetchupBomb, growthBonus = 0.15f,
                    desc = "ケチャップ爆弾で開始。経験値 +15%。",
                    shirt = new Color(0.25f, 0.62f, 0.32f), pants = new Color(0.25f, 0.22f, 0.2f), hair = new Color(0.12f, 0.1f, 0.1f), skin = new Color(0.92f, 0.74f, 0.58f)
                },
            };

            Specials = new[]
            {
                new SpecialDef { id = SpecialId.Bazooka, name = "乾麺バズーカ", icon = "bazooka", cooldown = 7f,
                    desc = "スパゲッティの束を撃ち込み、着弾点でまとめて折る大爆発。" },
                new SpecialDef { id = SpecialId.KnifeDash, name = "ナイフで一刀両断", icon = "knife", cooldown = 5f,
                    desc = "パスタをナイフで切る禁忌の突進。進路上のイタリア人を斬り抜ける（無敵）。" },
                new SpecialDef { id = SpecialId.Parmesan, name = "粉チーズ手榴弾", icon = "parmesan", cooldown = 11f,
                    desc = "偽物の粉チーズの雲。範囲内のイタリア人が呆然と立ち尽くす。" },
                new SpecialDef { id = SpecialId.Sprinkler, name = "ケチャップ・スプリンクラー", icon = "sprinkler", cooldown = 14f,
                    desc = "その場に設置。8秒間ケチャップを撒き散らす。通路の入口に置くと強い。" },
                new SpecialDef { id = SpecialId.SugarEspresso, name = "砂糖10杯エスプレッソ", icon = "sugar", cooldown = 18f,
                    desc = "7秒間、移動速度+40%・全武器のクールダウン半減。" },
                new SpecialDef { id = SpecialId.StarBeam, name = "スタ〇ビーム", icon = "starbeam", cooldown = 16f,
                    desc = "巨大フラペ〇ーノから緑と白のビームを照射。チェーン店のコーヒーに耐えられないイタリア人は恐怖で逃げ惑う。" },
            };

            Shop = new[]
            {
                new ShopDef { name = "パワー", desc = "ダメージ +5%", icon = "cheese", maxLevel = 5, baseCost = 60 },
                new ShopDef { name = "タフネス", desc = "最大HP +10", icon = "basil", maxLevel = 5, baseCost = 50 },
                new ShopDef { name = "アルデンテ", desc = "被ダメージ -1", icon = "shield", maxLevel = 3, baseCost = 120 },
                new ShopDef { name = "早足", desc = "移動速度 +5%", icon = "oil", maxLevel = 3, baseCost = 80 },
                new ShopDef { name = "磁力", desc = "回収範囲 +15%", icon = "fork", maxLevel = 3, baseCost = 60 },
                new ShopDef { name = "成長", desc = "経験値 +5%", icon = "book", maxLevel = 5, baseCost = 70 },
                new ShopDef { name = "強欲", desc = "コイン獲得 +10%", icon = "coin", maxLevel = 5, baseCost = 50 },
                new ShopDef { name = "復活", desc = "一度だけHP半分で復活", icon = "heart", maxLevel = 1, baseCost = 600 },
            };
        }
    }
}
