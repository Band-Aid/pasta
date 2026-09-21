using UnityEngine;

public class KitchenPickup : MonoBehaviour
{
    public bool IsWeapon { get; private set; }
    public KitchenWeapon Weapon { get; private set; }
    public PastaIngredient Ingredient { get; private set; }
    private PastaKitchen owner;
    private Vector3 landing, pickupStart;
    private float age, pickupAge;
    private bool collected, attracted;
    private Transform model;
    private TextMesh label;

    public static KitchenPickup SpawnWeapon(PastaKitchen kitchen, Vector3 position, KitchenWeapon weapon)
    {
        var pickup = Create(kitchen, position);
        pickup.IsWeapon = true;
        pickup.Weapon = weapon;
        if (weapon == KitchenWeapon.Fusilli) KitchenVisuals.Fusilli(pickup.model);
        else if (weapon == KitchenWeapon.Farfalle) KitchenVisuals.Farfalle(pickup.model);
        else KitchenVisuals.Ravioli(pickup.model);
        pickup.Decorate(weapon.ToString().ToUpperInvariant() + " +", KitchenVisuals.Mint);
        return pickup;
    }

    public static KitchenPickup SpawnIngredient(PastaKitchen kitchen, Vector3 position, PastaIngredient ingredient)
    {
        var pickup = Create(kitchen, position);
        pickup.Ingredient = ingredient;
        KitchenVisuals.Ingredient(pickup.model, ingredient);
        pickup.Decorate(ingredient.ToString().ToUpperInvariant(), KitchenVisuals.IngredientColor(ingredient));
        return pickup;
    }

    private static KitchenPickup Create(PastaKitchen kitchen, Vector3 position)
    {
        var go = new GameObject("Kitchen loot");
        go.transform.SetParent(kitchen.Effects, false);
        var pickup = go.AddComponent<KitchenPickup>();
        pickup.owner = kitchen;
        position.y = 0.65f;
        pickup.landing = position;
        go.transform.position = position;
        pickup.model = new GameObject("Food").transform;
        pickup.model.SetParent(go.transform, false);
        return pickup;
    }

    private void Decorate(string title, Color color)
    {
        gameObject.name = title + " drop";
        KitchenVisuals.Ring(transform, "Pickup glow", 0.46f, color).transform.localPosition = Vector3.down * 0.35f;
        label = KitchenVisuals.Label(transform, title, Vector3.up * 0.6f, color);
    }

    private void Update()
    {
        if (collected || owner == null || !owner.isActiveAndEnabled || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        var player = PlayerController.Instance;
        if (player == null) return;
        age += Time.deltaTime;
        model.localRotation = Quaternion.Euler(0f, age * 45f, Mathf.Sin(age * 2f) * 6f);
        if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        Vector3 destination = player.transform.position + Vector3.up * 1.1f;
        Vector3 delta = transform.position - player.transform.position;
        delta.y = 0f;
        label.gameObject.SetActive(!attracted && delta.sqrMagnitude < 4f * 4f);
        if (!attracted && age > 0.8f && delta.sqrMagnitude < 2.2f * 2.2f)
        {
            attracted = true;
            pickupStart = transform.position;
            pickupAge = 0f;
        }
        if (attracted)
        {
            pickupAge += Time.deltaTime;
            float t = Mathf.Clamp01(pickupAge / 0.38f);
            transform.position = Vector3.Lerp(pickupStart, destination, t * t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.55f);
            model.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.65f) * (1f - Mathf.Max(0f, t - 0.8f) * 5f);
            if (t >= 1f) Collect();
        }
        else transform.position = landing + Vector3.up * (Mathf.Sin(Mathf.Min(age, 0.8f) / 0.8f * Mathf.PI) * 1.5f + Mathf.Sin(age * 3f) * 0.08f);
    }

    private void Collect()
    {
        if (collected) return;
        collected = true;
        if (IsWeapon) owner.CollectWeapon(Weapon);
        else owner.CollectIngredient(Ingredient);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
