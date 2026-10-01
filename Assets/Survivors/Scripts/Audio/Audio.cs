using System.Collections.Generic;
using UnityEngine;

namespace PastaSurvivors
{
    public enum SfxId { Snap, Pop, Slam, Whoosh, Splat, Gem, Coin, Heal, Magnet, Hurt, Poof, Throw, Vroom, Boss, LevelUp, Chest, Crate, Dash, Select, Confirm, Evolve, Beam, Bell }

    /// <summary>All sound effects are synthesised at startup; a small voice pool with per-sound rate limiting keeps hordes from clipping.</summary>
    public class Sfx : MonoBehaviour
    {
        private const int Rate = 44100;
        private readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        private readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        private AudioSource[] voices;
        private int next;
        public float Volume = 0.8f;

        public void Init()
        {
            voices = new AudioSource[20];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
                voices[i].ignoreListenerPause = true;
            }
            clips[SfxId.Snap] = PastaAudio.Snap;
            clips[SfxId.Pop] = Make("pop", 0.08f, (t, n) => Mathf.Sin(2 * Mathf.PI * (900f - 3000f * t) * t) * Env(t, 0.002f, 40f));
            clips[SfxId.Slam] = Make("slam", 0.45f, (t, n) => (Mathf.Sin(2 * Mathf.PI * (70f - 40f * t) * t) * 0.9f + n * 0.5f * Mathf.Exp(-t * 18f)) * Env(t, 0.003f, 7f));
            clips[SfxId.Whoosh] = Make("whoosh", 0.3f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.5f);
            clips[SfxId.Splat] = Make("splat", 0.25f, (t, n) => (n * 0.7f + Mathf.Sin(2 * Mathf.PI * 180f * t) * 0.4f) * Env(t, 0.002f, 16f));
            clips[SfxId.Gem] = Make("gem", 0.09f, (t, n) => (Mathf.Sin(2 * Mathf.PI * 1568f * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * 3136f * t)) * Env(t, 0.001f, 45f) * 0.6f);
            clips[SfxId.Coin] = Make("coin", 0.25f, (t, n) => Mathf.Sin(2 * Mathf.PI * (t < 0.06f ? 988f : 1319f) * t) * Env(t, 0.001f, 12f) * 0.6f);
            clips[SfxId.Heal] = Make("heal", 0.4f, (t, n) => Arp(t, new[] { 523f, 659f, 784f, 1047f }, 0.08f) * 0.5f);
            clips[SfxId.Magnet] = Make("magnet", 0.5f, (t, n) => Mathf.Sin(2 * Mathf.PI * (300f + 1400f * t) * t) * Env(t, 0.01f, 5f) * 0.5f);
            clips[SfxId.Hurt] = Make("hurt", 0.2f, (t, n) => (Mathf.Sin(2 * Mathf.PI * (220f - 300f * t) * t) + n * 0.3f) * Env(t, 0.001f, 14f) * 0.8f);
            clips[SfxId.Poof] = Make("poof", 0.18f, (t, n) => (n * 0.5f + Mathf.Sin(2 * Mathf.PI * (500f + 900f * t) * t) * 0.35f) * Env(t, 0.002f, 20f));
            clips[SfxId.Throw] = Make("throw", 0.16f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.16f) * 0.45f);
            clips[SfxId.Vroom] = Make("vroom", 0.7f, (t, n) => Saw(t * (90f + 160f * t)) * 0.35f * Mathf.Sin(Mathf.PI * t / 0.7f) + n * 0.05f);
            clips[SfxId.Boss] = Make("boss", 1.2f, (t, n) => (Saw(t * 55f) * 0.4f + Saw(t * 55.7f) * 0.4f + Mathf.Sin(2 * Mathf.PI * 110f * t) * 0.3f) * Env(t, 0.05f, 2.2f));
            clips[SfxId.LevelUp] = Make("levelup", 0.7f, (t, n) => Arp(t, new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.09f) * 0.55f);
            clips[SfxId.Chest] = Make("chest", 1.4f, (t, n) => Arp(t, new[] { 392f, 523f, 659f, 784f, 659f, 784f, 1047f }, 0.12f) * 0.55f);
            clips[SfxId.Crate] = Make("crate", 0.3f, (t, n) => (n * 0.6f + Mathf.Sin(2 * Mathf.PI * 140f * t) * 0.5f) * Env(t, 0.001f, 12f));
            clips[SfxId.Dash] = Make("dash", 0.22f, (t, n) => n * Mathf.Sin(Mathf.PI * t / 0.22f) * 0.4f + Mathf.Sin(2 * Mathf.PI * (200f + 600f * t) * t) * 0.15f * Env(t, 0.01f, 8f));
            clips[SfxId.Select] = Make("select", 0.05f, (t, n) => Sq(t * 880f) * Env(t, 0.001f, 60f) * 0.25f);
            clips[SfxId.Confirm] = Make("confirm", 0.18f, (t, n) => (Sq(t * (t < 0.06f ? 660f : 990f))) * Env(t, 0.001f, 14f) * 0.3f);
            clips[SfxId.Beam] = Make("beam", 0.5f, (t, n) => (Saw(t * 180f) * 0.25f + Saw(t * 271f) * 0.2f + Mathf.Sin(2 * Mathf.PI * 880f * t) * 0.12f * Mathf.Sin(t * 60f) + n * 0.12f)
                * Mathf.Min(1f, t / 0.04f) * Mathf.Min(1f, (0.5f - t) / 0.08f));
            // Bicycle bell: two strikes of an inharmonic bell with a clapper rattle.
            clips[SfxId.Bell] = Make("bell", 0.75f, (t, n) => Bell(t) + Bell(t - 0.17f) * 0.9f);
            clips[SfxId.Evolve] = Make("evolve", 1.6f, (t, n) => Arp(t, new[] { 262f, 330f, 392f, 523f, 659f, 784f, 1047f, 1319f }, 0.1f) * 0.5f + Mathf.Sin(2 * Mathf.PI * 131f * t) * Env(t, 0.1f, 1.5f) * 0.2f);
        }

        public void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (voices == null || !clips.TryGetValue(id, out var clip)) return;
            float now = Time.unscaledTime;
            float gap = id == SfxId.Gem || id == SfxId.Poof || id == SfxId.Pop ? 0.035f : 0.05f;
            if (lastPlayed.TryGetValue(id, out var last) && now - last < gap) return;
            lastPlayed[id] = now;
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.pitch = pitch;
            v.PlayOneShot(clip, volume * Volume);
        }

        private static float Env(float t, float attack, float decay) => Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay);
        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        private static float Sq(float phase) => (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;

        private static float Bell(float t)
        {
            if (t < 0f) return 0f;
            const float f = 2093f;
            float tone = Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * 0.25f + Mathf.Sin(2 * Mathf.PI * f * 5.4f * t) * 0.1f;
            return tone * Env(t, 0.001f, 7f) * (0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * 31f * t)) * 0.6f;
        }

        private static float Arp(float t, float[] notes, float step)
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            float lt = t - i * step;
            float f = notes[i];
            return (Sq(t * f) * 0.3f + Mathf.Sin(2 * Mathf.PI * f * t) * 0.7f) * Env(lt, 0.003f, i == notes.Length - 1 ? 4f : 10f);
        }

        private static AudioClip Make(string name, float duration, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float fade = Mathf.Min(1f, (n - i) / (Rate * 0.005f));
                data[i] = Mathf.Clamp(f(t, noise) * fade, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /// <summary>A procedurally synthesised tarantella: mandolin tremolo, oom-pah bass, accordion and tambourine.</summary>
    public class Music : MonoBehaviour
    {
        private const int Rate = 32000;
        private AudioSource source;
        private readonly Dictionary<int, AudioClip> cache = new Dictionary<int, AudioClip>();
        private float targetVolume = 0.35f;
        public float Volume = 0.55f;

        // A-minor melody, 16 bars of 6 eighth notes. -1 = rest, -2 = hold previous (tremolo).
        private static readonly int[] Melody =
        {
            69, 72, 76, 81, 76, 72,
            71, 72, 74, 76, 74, 72,
            74, 77, 81, 86, 81, 77,
            76, 72, 69, 76, 72, 69,
            68, 71, 76, 80, 76, 71,
            74, 72, 71, 69, 71, 68,
            69, 72, 76, 81, 80, 81,
            76, -2, -2, 69, -2, -2,
            77, 76, 74, 81, 79, 77,
            76, 74, 73, 74, 77, 81,
            76, 74, 72, 71, 72, 69,
            72, 76, 81, 84, 83, 81,
            83, 81, 80, 77, 76, 74,
            72, 71, 69, 68, 71, 76,
            81, 76, 72, 69, 72, 76,
            81, -2, -2, 69, -2, -1,
        };
        // Chord root (MIDI) and quality per bar: 0 minor, 1 dominant 7th.
        private static readonly int[] Roots = { 45, 45, 50, 45, 40, 40, 45, 45, 50, 50, 45, 45, 40, 40, 45, 45 };
        private static readonly int[] Quality = { 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0 };

        public void Init()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
        }

        /// <summary>Theme index: 0 title, 1 Roma, 2 Venezia, 3 Napoli, 4 boss.</summary>
        public void PlayTheme(int theme)
        {
            if (!cache.TryGetValue(theme, out var clip))
            {
                int transpose = theme == 2 ? 5 : theme == 3 ? 7 : theme == 4 ? 1 : theme == 0 ? 3 : 0;
                float tempo = theme == 0 ? 92f : theme == 2 ? 100f : theme == 3 ? 122f : theme == 4 ? 132f : 112f;
                clip = Synthesize(transpose, tempo, theme);
                cache[theme] = clip;
            }
            if (source.clip == clip && source.isPlaying) return;
            source.clip = clip;
            source.volume = Volume * 0.35f;
            source.Play();
        }

        public void Stop() => source.Stop();
        public void Duck(bool ducked) => targetVolume = ducked ? 0.12f : 0.35f;

        private void Update()
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, targetVolume * Volume, Time.unscaledDeltaTime * 0.5f);
        }

        private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        private static AudioClip Synthesize(int transpose, float dottedQuarterBpm, int theme)
        {
            float eighth = 60f / (dottedQuarterBpm * 3f);
            int steps = Melody.Length;
            int total = Mathf.CeilToInt(steps * eighth * Rate);
            var buf = new float[total];
            var rng = new System.Random(17 + theme);
            int stepLen = Mathf.RoundToInt(eighth * Rate);

            for (int s = 0; s < steps; s++)
            {
                int bar = s / 6, beat = s % 6;
                int start = s * stepLen;
                // Melody (mandolin): hold values re-pluck as tremolo.
                int note = Melody[s];
                int held = note;
                if (note == -2)
                {
                    for (int k = s - 1; k >= 0; k--) if (Melody[k] >= 0) { held = Melody[k]; break; }
                }
                if (held >= 0)
                {
                    float f = Midi(held + transpose);
                    int plucks = note == -2 || theme == 0 ? 4 : 2;
                    for (int p = 0; p < plucks; p++)
                        Pluck(buf, start + p * stepLen / plucks, stepLen / plucks + stepLen / 2, f, p == 0 ? 0.26f : 0.15f);
                }
                // Bass on the two main beats.
                int root = Roots[bar] + transpose;
                if (beat == 0) Bass(buf, start, stepLen * 2, Midi(root - 12 + 12), 0.42f);
                if (beat == 3) Bass(buf, start, stepLen * 2, Midi(root - 12 + 19), 0.34f);
                // Accordion off-beat stabs.
                if (beat != 0 && beat != 3)
                {
                    int third = Quality[bar] == 1 ? 4 : 3;
                    Chord(buf, start, (int)(stepLen * 0.7f), new[] { Midi(root + 12), Midi(root + 12 + third), Midi(root + 19) }, 0.06f);
                }
                // Tambourine.
                Tamb(buf, start, beat == 0 || beat == 3 ? 0.14f : 0.06f, rng);
            }
            float peak = 0.001f;
            for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Abs(buf[i]));
            float gain = 0.9f / peak;
            for (int i = 0; i < total; i++) buf[i] *= gain;
            var clip = AudioClip.Create("Tarantella " + theme, total, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        private static void Pluck(float[] buf, int start, int len, float f, float amp)
        {
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 9f);
                float w = 0f;
                for (int h = 1; h <= 5; h++) w += Mathf.Sin(2 * Mathf.PI * f * h * t) / (h * h * 0.6f + 0.4f);
                w += Mathf.Sin(2 * Mathf.PI * f * 1.003f * t) * 0.4f;
                buf[start + i] += w * env * amp;
            }
        }

        private static void Bass(float[] buf, int start, int len, float f, float amp)
        {
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float ph = f * t;
                float tri = 4f * Mathf.Abs(ph - Mathf.Floor(ph + 0.5f)) - 1f;
                buf[start + i] += tri * Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 6f) * amp;
            }
        }

        private static void Chord(float[] buf, int start, int len, float[] freqs, float amp)
        {
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 150f) * Mathf.Min(1f, (len - i) / (Rate * 0.02f));
                float w = 0f;
                foreach (var f in freqs)
                {
                    float vib = 1f + Mathf.Sin(t * 30f) * 0.003f;
                    w += Mathf.Sin(2 * Mathf.PI * f * vib * t) + Mathf.Sin(2 * Mathf.PI * f * 3f * t) * 0.3f + Mathf.Sin(2 * Mathf.PI * f * 2.005f * t) * 0.25f;
                }
                buf[start + i] += w * env * amp;
            }
        }

        private static void Tamb(float[] buf, int start, float amp, System.Random rng)
        {
            int len = (int)(Rate * 0.08f);
            float prev = 0f;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)rng.NextDouble() * 2f - 1f;
                float hp = n - prev; prev = n;
                float jingle = Mathf.Sin(2 * Mathf.PI * 7200f * t) * Mathf.Sin(2 * Mathf.PI * 60f * t);
                buf[start + i] += (hp * 0.6f + jingle * 0.3f) * Mathf.Exp(-t * 45f) * amp;
            }
        }
    }
}
