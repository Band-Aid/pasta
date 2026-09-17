using UnityEngine;

/// <summary>
/// プレイヤーが持つパスタ。曲げ角(diff)は PastaHand から SetBend() で与えられ、
/// 折る瞬間の角度から品質(0..1)を返す。折れるとセグメントが物理で弾け飛ぶ。
/// </summary>
public class SpaghettiBreaker : MonoBehaviour
{
    [Header("構成")]
    [SerializeField] private Rigidbody[] segments;
    [SerializeField] private ConfigurableJoint[] joints;

    [Header("折る判定")]
    [SerializeField] private float breakAngleThreshold = 60f;

    [Header("見た目のしなり")]
    [SerializeField] private float maxFoldDeg = 52f; // 最大曲げ時の各半分の折り角

    [Header("フィードバック")]
    [SerializeField] private AudioClip snapSound;

    private AudioSource audioSource;
    private bool broken;
    private float currentDiff;
    private Quaternion[] segHomeRot;
    private Vector3[] segHomePos;

    public float CurrentDiff => currentDiff;
    public float BreakAngleThreshold => breakAngleThreshold;
    public bool IsBroken => broken;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (snapSound == null)
        {
            snapSound = AudioClip.Create("snap", 4096, 1, 44100, false, data =>
            {
                for (int i = 0; i < data.Length; i++)
                    data[i] = Random.Range(-1f, 1f) * Mathf.Exp(-(float)i / 512f);
            });
        }
        audioSource.clip = snapSound;

        if (segments != null)
        {
            segHomeRot = new Quaternion[segments.Length];
            segHomePos = new Vector3[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                segHomeRot[i] = segments[i].transform.localRotation;
                segHomePos[i] = segments[i].transform.localPosition;
                // 保持中はコライダーを切る（プレイヤーのCharacterControllerと干渉しないように）
                var col = segments[i].GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }
    }

    /// <summary>曲げ角(0..180°)を与える。見た目もしなる。</summary>
    public void SetBend(float diff)
    {
        currentDiff = Mathf.Clamp(diff, 0f, 180f);
        if (!broken) ApplyVisualBend();
    }

    /// <summary>両端（手）を保持したまま、中央がV字にたわむ折り。左右の半分を外端まわりに回す。</summary>
    private void ApplyVisualBend()
    {
        if (segments == null || segHomePos == null) return;
        int n = segments.Length;
        if (n < 2) return;

        float theta = Mathf.Clamp01(currentDiff / 150f) * maxFoldDeg;
        Vector3 leftPivot = segHomePos[0];       // 左端（左手の位置）
        Vector3 rightPivot = segHomePos[n - 1];  // 右端（右手の位置）
        int half = n / 2;

        for (int i = 0; i < n; i++)
        {
            if (segments[i] == null) continue;
            bool left = i < half;
            float sign = left ? -1f : 1f; // 内側の端が下がってV字になる
            Vector3 pivot = left ? leftPivot : rightPivot;
            Quaternion rot = Quaternion.Euler(0f, 0f, sign * theta);
            segments[i].transform.localPosition = pivot + rot * (segHomePos[i] - pivot);
            segments[i].transform.localRotation = rot * segHomeRot[i];
        }
    }

    /// <summary>今の曲げ角で折れれば品質(0..1)を返す。折れなければ-1。閾値ギリギリで1.0、180°で0.0。</summary>
    public float TryBreak()
    {
        if (broken) return -1f;
        if (currentDiff < breakAngleThreshold) return -1f;

        int idx = FindWeakestJoint();
        if (idx < 0) return -1f;

        BreakJoint(idx);
        return Mathf.Clamp01(1f - (currentDiff - breakAngleThreshold) / (180f - breakAngleThreshold));
    }

    private int FindWeakestJoint()
    {
        for (int i = 0; i < joints.Length; i++)
            if (joints[i] != null) return i;
        return -1;
    }

    private void BreakJoint(int index)
    {
        var joint = joints[index];
        if (joint == null) return;
        Destroy(joint);
        joints[index] = null;
        broken = true;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = Random.Range(1.0f, 1.25f);
            audioSource.Play();
        }
    }

    /// <summary>折った後、全セグメントを物理解放して弾き飛ばす</summary>
    public void Release(Vector3 impulse)
    {
        if (segments == null) return;
        foreach (var rb in segments)
        {
            if (rb == null) continue;
            var col = rb.GetComponent<Collider>();
            if (col != null) col.enabled = true; // 放り投げた破片は地面や建物で弾む
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate; // 飛ぶ間だけ補間
            rb.AddForce(impulse + Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 3f, ForceMode.Impulse);
        }
    }
}
