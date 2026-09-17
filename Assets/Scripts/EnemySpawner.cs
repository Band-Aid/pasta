using System.Collections;
using UnityEngine;

/// <summary>
/// ウェーブ制のスポナー。プレイヤーを囲むリング上に敵を沸かせ、
/// ウェーブが進むと数・速度が上がり、Fast/Toughの混成率も増える。全滅で次ウェーブ。
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("参照")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Transform[] spawnPoints; // 大通りの奥（無ければリング湧き）

    [Header("湧き設定")]
    [SerializeField] private float spawnRadius = 24f;
    [SerializeField] private int baseCount = 4;
    [SerializeField] private float baseSpeed = 2.4f;
    [SerializeField] private float maxSpeed = 6f;
    [SerializeField] private float intermission = 3f;

    public int Wave { get; private set; }
    private bool running;

    private void Awake() => Instance = this;

    public void Begin()
    {
        StopAll();
        Wave = 0;
        running = true;
        StartCoroutine(Loop());
    }

    public void StopAll()
    {
        running = false;
        StopAllCoroutines();
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(1.5f);

        while (running)
        {
            Wave++;
            int count = baseCount + Wave * 2;
            float speed = Mathf.Min(maxSpeed, baseSpeed + Wave * 0.12f);
            float gap = Mathf.Lerp(0.9f, 0.25f, Wave / 15f);

            for (int i = 0; i < count && running; i++)
            {
                SpawnOne(speed);
                yield return new WaitForSeconds(gap);
            }

            while (running && Enemy.Alive.Count > 0)
                yield return new WaitForSeconds(0.4f);

            yield return new WaitForSeconds(intermission);
        }
    }

    private void SpawnOne(float speed)
    {
        if (enemyPrefab == null) return;

        Vector3 pos;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            pos = sp.position + new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-2.2f, 2.2f));
        }
        else
        {
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.Range(spawnRadius * 0.82f, spawnRadius);
            pos = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
        }
        pos.y = 0.05f;

        var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
        e.Configure(ChooseType(), speed);
    }

    private EnemyType ChooseType()
    {
        if (Wave >= 3 && Random.value < Mathf.Min(0.35f, 0.08f + 0.03f * Wave)) return EnemyType.Tough;
        if (Wave >= 2 && Random.value < Mathf.Min(0.5f, 0.12f + 0.05f * Wave)) return EnemyType.Fast;
        return EnemyType.Normal;
    }
}
