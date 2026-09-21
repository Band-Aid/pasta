using UnityEngine;

/// <summary>Ravioli tomato puddles slow enemies; mac and cheese pulses around the player.</summary>
public class KitchenSauce : MonoBehaviour
{
    private PastaKitchen owner;
    private float radius, remaining, tickAt;
    private bool cheese;
    private Transform surface;
    private Mesh surfaceMesh;

    public static KitchenSauce Spawn(PastaKitchen kitchen, Vector3 position, float radius, float duration, bool cheese)
    {
        var go = new GameObject(cheese ? "Italian Mac & Cheese" : "Ravioli tomato sauce");
        go.transform.SetParent(kitchen.Effects, false);
        go.transform.position = new Vector3(position.x, 0.085f, position.z);
        var sauce = go.AddComponent<KitchenSauce>();
        sauce.owner = kitchen;
        sauce.radius = radius;
        sauce.remaining = duration;
        sauce.cheese = cheese;
        Color color = cheese ? new Color(1f, 0.77f, 0.3f, 0.12f) : new Color(0.84f, 0.19f, 0.1f, 0.2f);
        var disc = KitchenVisuals.SauceDisc(go.transform, radius, color);
        sauce.surface = disc.transform;
        sauce.surfaceMesh = disc.sharedMesh;
        Color border = cheese ? KitchenVisuals.Cream : KitchenVisuals.Pink;
        border.a = 0.38f;
        KitchenVisuals.Ring(go.transform, "Sauce border", radius, border, 0.025f);
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.PI * 2f / 3f;
            KitchenVisuals.Part(go.transform, cheese ? "Macaroni" : "Basil", PrimitiveType.Cube,
                new Vector3(Mathf.Cos(a) * radius * 0.65f, 0.04f, Mathf.Sin(a) * radius * 0.65f),
                new Vector3(0.28f, 0.06f, 0.16f), cheese ? KitchenVisuals.Cream : new Color(0.22f, 0.58f, 0.3f));
        }
        return sauce;
    }

    private void Update()
    {
        if (owner == null || !owner.isActiveAndEnabled || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        remaining -= Time.deltaTime;
        if (remaining <= 0f) { Destroy(gameObject); return; }
        if (cheese && PlayerController.Instance != null)
        {
            Vector3 p = PlayerController.Instance.transform.position;
            transform.position = new Vector3(p.x, 0.09f, p.z);
        }
        float fade = Mathf.Min(1f, remaining * 2f);
        transform.localScale = Vector3.one * fade;
        if (surface != null) surface.localPosition = Vector3.up * (Mathf.Sin(Time.time * 7f) * 0.015f);
        if (Time.time < tickAt) return;
        tickAt = Time.time + 0.8f;
        foreach (var enemy in Enemy.Alive.ToArray())
        {
            if (enemy == null || !DominoShot.SweptHit(transform.position, transform.position, enemy.transform.position, radius * fade)) continue;
            if (!cheese) enemy.Slow(0.45f, 1.1f);
            PastaKitchen.Damage(enemy, 1, transform.position);
        }
    }

    private void OnDestroy() { if (surfaceMesh != null) Destroy(surfaceMesh); }
}
