using UnityEngine;

/// <summary>Continuous dry pasta while held; capped physical fragments after snapping.</summary>
public class SpaghettiBreaker : MonoBehaviour
{
    // Preserve references in existing prototype prefabs; their coarse geometry is hidden at runtime.
    [SerializeField] private Rigidbody[] segments;
    [SerializeField] private ConfigurableJoint[] joints;
    [SerializeField] private float breakAngleThreshold = 60f;
    // Diff (deg) at which the mesh reaches its full fold, so the strand visibly strains before snapping.
    [SerializeField] private float visualSnapBend = 80f;
    [SerializeField] private AudioClip snapSound;
    private PastaVisual visual;
    private AudioSource sound, creak;
    private bool readyCue;
    private float currentDiff;
    public float CurrentDiff => currentDiff;
    public float BreakAngleThreshold => breakAngleThreshold;
    public float PerfectEnd => breakAngleThreshold + 26f;
    public float GoodEnd => PerfectEnd + 28f;
    public bool IsBroken { get; private set; }
    public Vector3 LeftGrip => visual != null ? visual.Point(-1f) : Vector3.left * 0.25f;
    public Vector3 RightGrip => visual != null ? visual.Point(1f) : Vector3.right * 0.25f;

    private void Awake()
    {
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        if (segments != null)
            foreach (var segment in segments)
                if (segment != null)
                {
                    segment.isKinematic = true;
                    foreach (var collider in segment.GetComponents<Collider>()) collider.enabled = false;
                }
        sound = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 0f;
        creak = gameObject.AddComponent<AudioSource>();
        creak.playOnAwake = false;
        creak.loop = true;
        creak.clip = PastaAudio.Creak;
        creak.volume = 0f;
        creak.Play();
    }

    public void Configure(PastaType type)
    {
        visual = gameObject.AddComponent<PastaVisual>();
        visual.Initialize(type);
    }

    public void SetBend(float diff)
    {
        if (IsBroken || Mathf.Approximately(currentDiff, diff)) return;
        currentDiff = Mathf.Clamp(diff, 0f, 150f);
        // Mesh fold runs ahead of the meter: full fold at visualSnapBend, well before the snap window ends.
        visual?.Bend(Mathf.Clamp01(currentDiff / visualSnapBend));
        creak.volume = currentDiff < 8f ? 0f : Mathf.Lerp(0.025f, 0.21f, currentDiff / 150f);
        creak.pitch = Mathf.Lerp(0.7f, 1.9f, currentDiff / 150f);
        if (currentDiff >= breakAngleThreshold && !readyCue)
        {
            readyCue = true;
            sound.PlayOneShot(PastaAudio.Ready, 0.28f);
        }
        if (currentDiff < breakAngleThreshold) readyCue = false;
    }

    public float QualityAt(float angle)
    {
        if (angle < breakAngleThreshold) return -1f;
        if (angle <= PerfectEnd) return 1f;
        if (angle <= GoodEnd) return 0.78f;
        return 0.35f;
    }

    public float TryBreak()
    {
        if (IsBroken) return -1f;
        float quality = QualityAt(currentDiff);
        if (quality < 0f) return quality;
        IsBroken = true;
        creak.Stop();
        sound.pitch = Random.Range(0.94f, 1.06f);
        sound.PlayOneShot(snapSound != null ? snapSound : PastaAudio.Snap, 0.85f);
        return quality;
    }

    public void Release(Vector3 impulse)
    {
        creak.Stop();
        visual?.Shatter(impulse);
    }
}
