using UnityEngine;

/// <summary>Shared synthesis, independent of gameplay's random sequence.</summary>
public static class PastaAudio
{
    private static AudioClip snap, creak, ready, impact;
    public static AudioClip Snap => snap != null ? snap : snap = Create("Dry bundle crack", 0.23f, 0);
    public static AudioClip Creak => creak != null ? creak : creak = Create("Pasta tension", 0.4f, 1);
    public static AudioClip Ready => ready != null ? ready : ready = Create("Release cue", 0.12f, 2);
    public static AudioClip Impact => impact != null ? impact : impact = Create("Snap impact", 0.3f, 3);

    private static AudioClip Create(string name, float duration, int kind)
    {
        const int rate = 44100;
        var samples = new float[Mathf.CeilToInt(duration * rate)];
        var random = new System.Random(701 + kind);
        float previous = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            float high = noise - previous * 0.85f;
            previous = noise;
            if (kind == 0)
            {
                float envelope = Mathf.Exp(-t * 85f);
                for (int j = 1; j <= 6; j++)
                {
                    float delay = t - j * 0.012f;
                    if (delay >= 0f) envelope += Mathf.Exp(-delay * 210f) * (0.55f - j * 0.055f);
                }
                samples[i] = high * envelope * 0.48f + Mathf.Sin(t * 2700f) * Mathf.Exp(-t * 65f) * 0.18f;
            }
            else if (kind == 1)
                samples[i] = (high * 0.16f + Mathf.Sin(t * Mathf.PI * 2f * 375f) * 0.13f)
                    * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f * 25f), 6f)
                    * Mathf.Sin(Mathf.PI * t / duration);
            else if (kind == 2)
                samples[i] = (Mathf.Sin(t * 2f * Mathf.PI * 1320f) + Mathf.Sin(t * 2f * Mathf.PI * 1980f) * 0.35f)
                    * Mathf.Exp(-t * 38f) * Mathf.Min(t * 800f, 1f) * 0.3f;
            else
                samples[i] = (Mathf.Sin(2f * Mathf.PI * (105f * t - 105f * t * t)) * 0.65f + noise * 0.12f)
                    * Mathf.Exp(-t * 20f) * Mathf.Min(t * 1000f, 1f);
            samples[i] = Mathf.Clamp(samples[i], -1f, 1f);
        }
        var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
