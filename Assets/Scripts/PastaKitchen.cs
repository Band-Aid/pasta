using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Enemy loot, automatic pasta weapons and ingredient recipes for one run.</summary>
public class PastaKitchen : MonoBehaviour
{
    public static PastaKitchen Instance { get; private set; }
    public KitchenInventory Inventory { get; } = new KitchenInventory();
    public Transform Effects { get; private set; }
    public string Notice { get; private set; }
    public float NoticeRemaining => Mathf.Max(0f, noticeUntil - Time.time);
    public float NoticeAge => Time.time - noticeStarted;
    public Color NoticeColor { get; private set; } = KitchenVisuals.Mint;
    public int PendingRewards => rewards.Count;
    public int DropsCreated { get; private set; }
    public int DefeatedCount => defeated;
    public int PickupsCollected { get; private set; }
    public int BicyclesFired { get; private set; }
    public float BicycleCooldown => Mathf.Max(0f, bicycleReadyAt - Time.time);
    private System.Random lootRandom;
    private float noticeUntil, butterflyReadyAt, ravioliReadyAt, sauceReadyAt, bicycleReadyAt, orbitHitAt;
    private int defeated, missedDrops, orbitLevel;
    private float nextRewardAt, noticeStarted, fusilliReadyAt;
    private readonly Queue<(bool weapon, int item)> rewards = new Queue<(bool, int)>();
    private Transform orbit;
    private readonly Transform[] spirals = new Transform[5];

    private void Awake()
    {
        Instance = this;
        var root = new GameObject("Kitchen effects");
        root.transform.SetParent(transform, false);
        Effects = root.transform;
        ResetRun();
    }

    public void ResetRun()
    {
        if (Effects != null)
            foreach (Transform child in Effects)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        Inventory.Reset();
        lootRandom = new System.Random();
        defeated = missedDrops = DropsCreated = PickupsCollected = BicyclesFired = orbitLevel = 0;
        orbit = null;
        noticeUntil = 0f;
        rewards.Clear();
        nextRewardAt = 0f;
        fusilliReadyAt = 0f;
        butterflyReadyAt = ravioliReadyAt = sauceReadyAt = bicycleReadyAt = orbitHitAt = Time.time;
        Notify("光るドロップを取りに行こう\n近づいて拾うと、パスタが育つ", 4f);
    }

    public void OnEnemyDefeated(Vector3 position)
    {
        if (!isActiveAndEnabled || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        defeated++;
        // Fewer, deliberate rewards leave room to notice and pursue each drop.
        if ((defeated == 1 || defeated % 12 == 0) && Inventory.TryRollWeapon(lootRandom, out var weapon))
        {
            KitchenPickup.SpawnWeapon(this, position, weapon);
            DropsCreated++;
            missedDrops = 0;
        }
        else if (lootRandom.NextDouble() < 0.3 || missedDrops >= 4)
        {
            KitchenPickup.SpawnIngredient(this, position, Inventory.RollIngredient(lootRandom));
            DropsCreated++;
            missedDrops = 0;
        }
        else missedDrops++;
    }

    public void CollectWeapon(KitchenWeapon weapon)
    {
        rewards.Enqueue((true, (int)weapon));
        PresentNextReward();
    }

    public void CollectIngredient(PastaIngredient ingredient)
    {
        rewards.Enqueue((false, (int)ingredient));
        PresentNextReward();
    }

    private void PresentNextReward()
    {
        if (rewards.Count == 0 || Time.time < nextRewardAt) return;
        var reward = rewards.Dequeue();
        if (reward.weapon) GrantWeapon((KitchenWeapon)reward.item);
        else GrantIngredient((PastaIngredient)reward.item);
        nextRewardAt = Time.time + NoticeRemaining;
    }

    private void GrantWeapon(KitchenWeapon weapon)
    {
        PickupsCollected++;
        NoticeColor = KitchenVisuals.Mint;
        if (Inventory.Upgrade(weapon))
        {
            string effect = weapon == KitchenWeapon.Fusilli ? "周回するパスタが、近くの敵を撃退"
                : weapon == KitchenWeapon.Farfalle ? "蝶形パスタが、敵を追いかける" : "トマトソースで、敵を減速";
            Notify($"{KitchenInventory.WeaponName(weapon)}  GET!  Lv.{Inventory.Level(weapon)}\n{effect}", 2.2f);
            if (Inventory.Level(weapon) == 1)
            {
                if (weapon == KitchenWeapon.Fusilli) fusilliReadyAt = Time.time + 1.4f;
                if (weapon == KitchenWeapon.Farfalle) butterflyReadyAt = Time.time + 1.4f;
                if (weapon == KitchenWeapon.Ravioli) ravioliReadyAt = Time.time + 1.4f;
            }
        }
        else
        {
            PlayerController.Instance?.Heal(8f);
            Notify("このパスタは最大レベル！  HP +8", 2f);
        }
        PlayPickupSound();
    }

    private void GrantIngredient(PastaIngredient ingredient)
    {
        bool macAndCheese = Inventory.HasMacAndCheese, british = Inventory.HasBritishCarbonara;
        Inventory.Collect(ingredient);
        PickupsCollected++;
        if (!macAndCheese && Inventory.HasMacAndCheese) sauceReadyAt = Time.time + 2.5f;
        NoticeColor = KitchenVisuals.IngredientColor(ingredient);
        if (!british && Inventory.HasBritishCarbonara)
        {
            bicycleReadyAt = Time.time + 2.5f;
            NoticeColor = KitchenVisuals.Pink;
            Notify($"{KitchenInventory.IngredientName(ingredient)} GET!  レシピ完成！\nBRITISH CARBONARA  —  F / Y で発射", 3.2f);
        }
        else if (!macAndCheese && Inventory.HasMacAndCheese)
        {
            sauceReadyAt = Time.time + 2.5f;
            Notify($"{KitchenInventory.IngredientName(ingredient)} GET!\nMAC & CHEESE 完成！  あとはハム…", 3.2f);
        }
        else
            Notify($"{KitchenInventory.IngredientName(ingredient)}  GET!\n{(Inventory.HasMacAndCheese ? $"味付け Lv.{Inventory.RecipeLevel}  /  素材を重ねて強化" : $"マカロニチーズまで {Inventory.MacAndCheeseParts} / 2  {(ingredient == PastaIngredient.Ham ? "ハムはとっておこう" : "あと一つ！")}")}", 1.8f);
        PlayPickupSound((!macAndCheese && Inventory.HasMacAndCheese) || (!british && Inventory.HasBritishCarbonara));
    }

    private void PlayPickupSound(bool recipe = false)
    {
        var player = PlayerController.Instance;
        if (player == null) return;
        var source = player.GetComponent<AudioSource>();
        if (source != null) source.PlayOneShot(recipe ? PastaAudio.Recipe : PastaAudio.Pickup, 0.5f);
    }

    private void Notify(string message, float duration)
    {
        Notice = message;
        noticeUntil = Time.time + duration;
        noticeStarted = Time.time;
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsPlaying || PlayerController.Instance == null) return;
        PresentNextReward();
        if ((Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)) TryFireBicycle();
        Vector3 center = PlayerController.Instance.transform.position;
        UpdateFusilli(center);
        var target = NearestEnemy(center, 19f);
        if (target == null) return;

        int level = Inventory.Level(KitchenWeapon.Farfalle);
        if (level > 0 && Time.time >= butterflyReadyAt)
        {
            butterflyReadyAt = Time.time + 3.6f - level * 0.45f;
            for (int i = 0; i < level; i++)
                KitchenProjectile.Butterfly(this, center + Vector3.up * 1.2f, target, i - (level - 1) * 0.5f);
        }
        level = Inventory.Level(KitchenWeapon.Ravioli);
        if (level > 0 && Time.time >= ravioliReadyAt)
        {
            ravioliReadyAt = Time.time + 5.6f - level * 0.5f;
            KitchenProjectile.Ravioli(this, center + Vector3.up * 1.3f, target.transform.position, level);
        }
        if (Inventory.HasMacAndCheese && Time.time >= sauceReadyAt)
        {
            sauceReadyAt = Time.time + 4.2f;
            KitchenSauce.Spawn(this, center, 3.5f + Inventory.RecipeLevel * 0.4f, 1.6f, true);
        }
    }

    public bool TryFireBicycle()
    {
        if (!isActiveAndEnabled || !Inventory.HasBritishCarbonara || BicycleCooldown > 0f
            || GameManager.Instance == null || !GameManager.Instance.IsPlaying || PlayerController.Instance == null) return false;
        var player = PlayerController.Instance;
        Vector3 center = player.transform.position;
        var target = NearestEnemy(center + player.transform.forward * 8f, 25f);
        if (target == null) return false;
        Vector3 direction = target.transform.position - center;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = player.transform.forward;
        bicycleReadyAt = Time.time + 20f - Inventory.RecipeLevel * 2f;
        KitchenProjectile.Bicycle(this, center + Vector3.up * 0.15f, target, direction.normalized);
        BicyclesFired++;
        return true;
    }

    private void UpdateFusilli(Vector3 center)
    {
        int level = Inventory.Level(KitchenWeapon.Fusilli);
        if (level == 0 || Time.time < fusilliReadyAt) return;
        if (level != orbitLevel || orbit == null)
        {
            if (orbit != null) Destroy(orbit.gameObject);
            orbit = new GameObject("Orbiting fusilli").transform;
            orbit.SetParent(Effects, false);
            orbitLevel = level;
            for (int i = 0; i < level + 2; i++)
            {
                spirals[i] = KitchenVisuals.Fusilli(orbit);
                spirals[i].localScale = Vector3.one * 0.65f;
            }
        }
        orbit.position = center + Vector3.up * 0.8f;
        for (int i = 0; i < level + 2; i++)
        {
            float angle = Time.time * 2.8f + i * Mathf.PI * 2f / (level + 2);
            spirals[i].localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.8f;
            spirals[i].localRotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 60f);
        }
        if (Time.time < orbitHitAt) return;
        orbitHitAt = Time.time + 0.45f;
        foreach (var enemy in Enemy.Alive.ToArray())
        {
            if (enemy == null) continue;
            for (int i = 0; i < level + 2; i++)
                if (DominoShot.SweptHit(spirals[i].position, spirals[i].position, enemy.transform.position, 1.15f))
                {
                    Damage(enemy, 1, center);
                    break;
                }
        }
    }

    public static Enemy NearestEnemy(Vector3 origin, float range)
    {
        Enemy nearest = null;
        float distance = range * range;
        foreach (var enemy in Enemy.Alive)
        {
            if (enemy == null) continue;
            Vector3 delta = enemy.transform.position - origin;
            delta.y = 0f;
            if (delta.sqrMagnitude >= distance) continue;
            distance = delta.sqrMagnitude;
            nearest = enemy;
        }
        return nearest;
    }

    public static bool Damage(Enemy enemy, int damage, Vector3 origin)
    {
        if (enemy == null || !Enemy.Alive.Contains(enemy) || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return false;
        if (enemy.Hit(damage, 2, origin, false)) ScoreManager.Instance?.OnKitchenKills(1);
        return true;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
