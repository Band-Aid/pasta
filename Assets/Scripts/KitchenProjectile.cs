using UnityEngine;

/// <summary>Homing bow ties, lobbed ravioli and the British Carbonara evolution.</summary>
public class KitchenProjectile : MonoBehaviour
{
    public enum Kind { Butterfly, Ravioli, Bicycle }
    public Kind Attack { get; private set; }
    private PastaKitchen owner;
    private Enemy target;
    private Vector3 direction, start, destination;
    private float age, steerAfter, impactSoundAt;
    private DominoShot bicycleShot;
    private Transform[] wheels;
    public int BicycleHits { get; private set; }
    private int level;
    private Transform model;
    private TextMesh title;

    public static KitchenProjectile Butterfly(PastaKitchen kitchen, Vector3 origin, Enemy target, float spread)
    {
        var shot = Create(kitchen, origin, Kind.Butterfly);
        shot.target = target;
        Vector3 forward = target.transform.position - origin;
        forward.y = 0f;
        shot.direction = Quaternion.Euler(0f, spread * 28f, 0f) * (forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward);
        KitchenVisuals.Farfalle(shot.model);
        return shot;
    }

    public static KitchenProjectile Ravioli(PastaKitchen kitchen, Vector3 origin, Vector3 landing, int level)
    {
        var shot = Create(kitchen, origin, Kind.Ravioli);
        shot.destination = new Vector3(landing.x, 0.18f, landing.z);
        shot.level = level;
        KitchenVisuals.Ravioli(shot.model);
        return shot;
    }

    public static KitchenProjectile Bicycle(PastaKitchen kitchen, Vector3 origin, Enemy target, Vector3 direction)
    {
        var shot = Create(kitchen, origin + direction * 2.8f, Kind.Bicycle);
        shot.target = target;
        shot.direction = direction;
        KitchenVisuals.GrannyBicycle(shot.model);
        shot.title = shot.model.GetComponentInChildren<TextMesh>();
        shot.bicycleShot = new DominoShot(3, PastaType.Penne);
        var wheelList = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in shot.model) if (child.name == "Granny wheel") wheelList.Add(child);
        shot.wheels = wheelList.ToArray();
        shot.model.localScale = Vector3.zero;
        shot.transform.rotation = Quaternion.LookRotation(direction);
        return shot;
    }

    private static KitchenProjectile Create(PastaKitchen kitchen, Vector3 origin, Kind kind)
    {
        var go = new GameObject(kind == Kind.Bicycle ? "British Carbonara - Granny Bicycle" : kind.ToString());
        go.transform.SetParent(kitchen.Effects, false);
        go.transform.position = origin;
        var shot = go.AddComponent<KitchenProjectile>();
        shot.Attack = kind;
        shot.owner = kitchen;
        shot.start = origin;
        shot.model = new GameObject("Pasta model").transform;
        shot.model.SetParent(go.transform, false);
        return shot;
    }

    private void Update()
    {
        if (owner == null || !owner.isActiveAndEnabled || GameManager.Instance == null || !GameManager.Instance.IsPlaying) return;
        age += Time.deltaTime;
        if (Attack == Kind.Bicycle)
        {
            DriveBicycle();
            return;
        }
        if (Attack == Kind.Ravioli)
        {
            float t = Mathf.Clamp01(age / 0.85f);
            Vector3 next = Vector3.Lerp(start, destination, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 3.2f);
            bool wall = TrySceneryHit(transform.position, next, out var collision);
            transform.position = wall ? collision : next;
            model.Rotate(130f * Time.deltaTime, 220f * Time.deltaTime, 0f);
            if (t >= 1f || wall)
            {
                KitchenSauce.Spawn(owner, transform.position, 2.1f + level * 0.35f, 3.5f + level * 0.4f, false);
                Destroy(gameObject);
            }
            return;
        }

        if (target == null || !Enemy.Alive.Contains(target)) target = PastaKitchen.NearestEnemy(transform.position, 22f);
        if (target != null)
        {
            Vector3 desired = target.transform.position - transform.position;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.01f)
                direction = Vector3.RotateTowards(direction, desired.normalized, Time.deltaTime * 5f, 0f);
        }
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        Vector3 from = transform.position;
        Vector3 to = from + direction.normalized * (Time.deltaTime * 12f);
        bool blocked = TrySceneryHit(from, to, out var hitPoint);
        if (blocked) to = hitPoint;
        transform.position = to;
        transform.rotation = Quaternion.LookRotation(direction);
        model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 22f) * 30f);

        Enemy struck = null;
        float closest = float.PositiveInfinity;
        foreach (var enemy in Enemy.Alive)
        {
            if (enemy == null || !DominoShot.SweptHit(from, to, enemy.transform.position, 0.65f)) continue;
            float distance = (enemy.transform.position - from).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            struck = enemy;
        }
        if (struck != null)
        {
            PastaKitchen.Damage(struck, 1, from);
            Destroy(gameObject);
        }
        else if (blocked || age >= 3.5f)
        {
            Destroy(gameObject);
        }
    }

    private void DriveBicycle()
    {
        const float entrance = 0.85f, lifetime = 6.85f;
        if (age >= lifetime) { Destroy(gameObject); return; }
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.5f));
        float exit = Mathf.Clamp01((lifetime - age) / 0.45f);
        model.localScale = Vector3.one * (1.2f * reveal * exit);
        model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 9f) * 2f);
        if (title != null) title.gameObject.SetActive(age < 2.3f);
        foreach (var wheel in wheels) wheel.Rotate(Vector3.right, -Time.deltaTime * (age < entrance ? 100f : 950f), Space.Self);
        if (age < entrance) return;

        if (target == null || !Enemy.Alive.Contains(target)) target = PastaKitchen.NearestEnemy(transform.position + direction * 5f, 18f);
        if (target != null && age >= steerAfter)
        {
            Vector3 desired = target.transform.position - transform.position;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.01f) direction = Vector3.RotateTowards(direction, desired.normalized, Time.deltaTime * 1.5f, 0f);
        }
        Vector3 from = transform.position;
        Vector3 to = from + direction * (Time.deltaTime * Mathf.Lerp(8f, 12f, Mathf.Clamp01(age - entrance)));
        if (TrySceneryHit(from + Vector3.up, to + Vector3.up, out var wall))
        {
            to = wall - Vector3.up;
            direction = Quaternion.Euler(0, 90f, 0) * direction;
            steerAfter = age + 0.8f;
        }
        transform.position = to;
        transform.rotation = Quaternion.LookRotation(direction);
        int kills = 0;
        foreach (var enemy in Enemy.Alive.ToArray())
        {
            if (enemy == null || !DominoShot.SweptHit(from, to, enemy.transform.position, 2.15f)) continue;
            if (enemy.Launch(bicycleShot, direction, false, false)) kills++;
        }
        BicycleHits += kills;
        ScoreManager.Instance?.OnKitchenKills(kills);
        if (kills > 0 && age > impactSoundAt)
        {
            impactSoundAt = age + 0.25f;
            PlayerController.Instance?.OnDominoImpact();
        }
    }

    private void LateUpdate()
    {
        if (title != null && Camera.main != null) title.transform.rotation = Camera.main.transform.rotation;
    }

    private static bool TrySceneryHit(Vector3 from, Vector3 to, out Vector3 point)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        point = to;
        if (length < 0.0001f) return false;
        bool found = false;
        foreach (var hit in Physics.RaycastAll(from, delta / length, length, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Enemy>() != null || hit.collider.GetComponentInParent<PlayerController>() != null
                || hit.collider.GetComponentInParent<PastaFragment>() != null) continue;
            if (hit.distance > length) continue;
            length = hit.distance;
            point = hit.point - delta.normalized * 0.08f;
            found = true;
        }
        return found;
    }
}
