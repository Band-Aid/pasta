using System.Collections;
using UnityEngine;

/// <summary>Three authored formations: learn to launch, pierce a guard, then fight on two fronts.</summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }
    public const int TotalRounds = 3;
    [SerializeField] private Enemy enemyPrefab;
    // Kept for existing scene and builder references.
    [SerializeField] private Transform[] spawnPoints;
    public int Wave { get; private set; }
    public bool Intermission { get; private set; }
    public int RemainingToSpawn { get; private set; }
    public string RoundName => Wave == 1 ? "列の手前を狙え" : Wave == 2 ? "重い敵には、人をぶつけろ" : "挑発で集めて、一網打尽";
    private bool running;

    private void Awake() => Instance = this;

    public void Begin()
    {
        StopAll();
        Wave = 0;
        Intermission = false;
        running = true;
        StartCoroutine(Loop());
    }

    public void StopAll()
    {
        running = false;
        RemainingToSpawn = 0;
        StopAllCoroutines();
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(0.75f);
        while (running && Wave < TotalRounds)
        {
            Wave++;
            int count = Wave == 1 ? 9 : Wave == 2 ? 12 : 18;
            RemainingToSpawn = count;
            Intermission = false;
            // Face the first formation toward the player; later rounds use the open plaza streets.
            Vector3 center = PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero;
            center.x = Mathf.Clamp(center.x, -8f, 8f);
            center.z = Mathf.Clamp(center.z, -8f, 8f);
            center.y = 0.05f;
            for (int i = 0; i < count && running; i++)
            {
                SpawnOne(center, i);
                RemainingToSpawn--;
                yield return new WaitForSeconds(0.06f);
            }
            while (running && (Enemy.Alive.Count > 0 || AnyFlying()))
                yield return new WaitForSeconds(0.2f);
            if (!running) yield break;
            Intermission = true;
            ScoreManager.Instance?.WaveClear(Wave);
            PlayerController.Instance?.Heal(15f);
            if (Wave == TotalRounds)
            {
                yield return new WaitForSeconds(0.8f);
                GameManager.Instance?.Win();
                yield break;
            }
            yield return new WaitForSeconds(2.4f);
        }
    }

    private static bool AnyFlying()
    {
        foreach (var enemy in FindObjectsByType<Enemy>())
            if (enemy.IsFlying) return true;
        return false;
    }

    private void SpawnOne(Vector3 center, int index)
    {
        if (enemyPrefab == null) return;
        int formationIndex = Wave == 3 ? index % 9 : index;
        int columns = Wave == 2 ? 4 : 3;
        int row = formationIndex / columns;
        int column = formationIndex % columns;
        Vector3 direction = Wave == 3 && index >= 9 ? Vector3.right : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction);
        Vector3 pos = center + direction * (7.5f + row * 2.7f) + side * ((column - (columns - 1) * 0.5f) * 1.8f);
        var enemy = Instantiate(enemyPrefab, pos, Quaternion.LookRotation(-direction));
        EnemyType type = Wave >= 2 && row == 2 ? EnemyType.Tough
            : Wave == 3 && row == 0 ? EnemyType.Fast : EnemyType.Normal;
        enemy.Configure(type, Wave == 1 ? 1.3f : Wave == 2 ? 1.65f : 1.9f);
    }
}
