using UnityEngine;

namespace PastaSurvivors
{
    /// <summary>Short, nonverbal cartoon cries. Two independent voices never compete with weapon or UI sources.</summary>
    internal sealed class EnemyCries
    {
        private const int Rate = 32000, Profiles = 4, Takes = 4;
        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly Enemy[] actors = new Enemy[2];
        private readonly int[] actorIds = new int[2];
        private readonly AudioClip[,] clips = new AudioClip[Profiles, Takes];
        private readonly int[] lastTake = { -1, -1, -1, -1 };
        private readonly System.Random random = new System.Random(918273);
        private float nextCryAt, volume;
        private bool paused;

        public float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                for (int i = 0; i < sources.Length; i++) ApplyMix(i);
            }
        }

        public EnemyCries(GameObject owner)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = owner.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                // Mix relative to the player, so top-down camera height doesn't mute voices.
                sources[i].spatialBlend = 0f;
                sources[i].volume = 0f;
            }
            for (int profile = 0; profile < Profiles; profile++)
                for (int take = 0; take < Takes; take++) clips[profile, take] = Synthesize(profile, take);
        }

        public void Play(Enemy enemy)
        {
            if (!G.Playing || Time.timeScale <= 0f || G.Player == null || volume <= 0f
                || enemy == null || !enemy.active || enemy.IsProp) return;
            Vector3 offset = enemy.pos - G.Player.Position;
            offset.y = 0f;
            float range = enemy.IsBoss ? 36f : 24f;
            if (offset.sqrMagnitude >= range * range) return;
            float now = Time.unscaledTime;
            if (!enemy.IsBoss && now < nextCryAt) return;
            // A new defeat can arrive in Update before Tick notices that a menu closed.
            SetPaused(false);
            for (int i = 0; i < sources.Length; i++)
                if (actors[i] == enemy && actorIds[i] == enemy.uid && sources[i].isPlaying) return;
            int slot = -1;
            if (enemy.IsBoss)
            {
                // Establish the cooldown before a boss's RoutAll defeats the surrounding horde.
                Clear();
                slot = 0;
            }
            else
                for (int i = 0; i < sources.Length; i++)
                    if (!sources[i].isPlaying) { slot = i; break; }
            if (slot < 0) return;

            int profile = Profile(enemy.def.kind);
            int take = random.Next(lastTake[profile] < 0 ? Takes : Takes - 1);
            if (lastTake[profile] >= 0 && take >= lastTake[profile]) take++;
            lastTake[profile] = take;
            var source = sources[slot];
            source.Stop();
            source.clip = clips[profile, take];
            // Appearance supplies a stable voice colour; each take has a small delivery variation.
            source.pitch = (enemy.IsBoss ? 0.84f : 1f) * (0.95f + enemy.appearanceVariant * 0.03f)
                * Mathf.Lerp(0.97f, 1.03f, (float)random.NextDouble());
            source.priority = enemy.IsBoss ? 60 : 140;
            actors[slot] = enemy;
            actorIds[slot] = enemy.uid;
            ApplyMix(slot);
            source.Play();
            nextCryAt = now + (enemy.IsBoss ? 1.4f : Mathf.Lerp(0.55f, 0.85f, (float)random.NextDouble()));
        }

        public void Tick()
        {
            SetPaused(!G.Playing || Time.timeScale <= 0f);
            for (int i = 0; i < sources.Length; i++)
            {
                var enemy = actors[i];
                if (enemy == null) continue;
                // Pooled objects can already be a different Italian by the time a cry ends.
                if (!enemy.active || enemy.uid != actorIds[i] || (!paused && !sources[i].isPlaying))
                {
                    sources[i].Stop();
                    actors[i] = null;
                }
                else ApplyMix(i);
            }
        }

        private void SetPaused(bool pause)
        {
            if (pause == paused) return;
            for (int i = 0; i < sources.Length; i++)
                if (pause) sources[i].Pause();
                else if (actors[i] != null) sources[i].UnPause();
            paused = pause;
        }

        private void ApplyMix(int slot)
        {
            var enemy = actors[slot];
            if (enemy == null || G.Player == null) { sources[slot].volume = 0f; return; }
            Vector3 offset = enemy.pos - G.Player.Position;
            offset.y = 0f;
            float distance = offset.magnitude;
            float falloff = 1f - Mathf.InverseLerp(4f, enemy.IsBoss ? 36f : 24f, distance);
            float gain = enemy.IsBoss ? 0.85f : enemy.IsElite ? 0.72f : 0.6f;
            sources[slot].volume = volume * gain * falloff * falloff;
            sources[slot].panStereo = G.Cam != null && distance > 0.01f
                ? Mathf.Clamp(Vector3.Dot(offset / distance, G.Cam.transform.right) * 0.65f, -0.65f, 0.65f) : 0f;
        }

        public void Clear()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
                actors[i] = null;
            }
            nextCryAt = 0f;
            paused = false;
        }

        public void Dispose()
        {
            Clear();
            foreach (var clip in clips) if (clip != null) Object.Destroy(clip);
        }

        private static int Profile(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Chef:
                case EnemyKind.Mafioso:
                case EnemyKind.BossCapitano:
                case EnemyKind.BossDon: return 0; // baritone
                case EnemyKind.Mamma: return 2;  // alto
                case EnemyKind.Nonna:
                case EnemyKind.BossNonna:
                case EnemyKind.BossBikeNonna: return 3; // raspy elder
                default: return 1; // tenor
            }
        }

        // A glottal pulse excites three mouth resonances (formants). Vowel transitions,
        // syllable accents, breath, jitter and pitch contours make a voice instead of a beep.
        // These are original synthesis, not recordings or spoken versions of the text bubbles.
        private static AudioClip Synthesize(int profile, int take)
        {
            float duration = 0.58f + take * 0.085f;
            var data = new float[Mathf.CeilToInt(duration * Rate)];
            var noise = new System.Random(7139 + profile * 101 + take * 17);
            float fundamental = profile == 0 ? 112f : profile == 1 ? 165f : profile == 2 ? 232f : 188f;
            float tract = profile == 0 ? 0.87f : profile == 2 ? 1.13f : 1f;
            int syllables = take == 1 ? 1 : take == 2 ? 3 : 2;
            float phase = 0f, previousPulse = 0f, breath = 0f, peak = 0f;
            var low = new Formant();
            var mid = new Formant();
            var high = new Formant();
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate, u = t / duration;
                float syllable = Mathf.Repeat(u * syllables, 1f);
                if (i % 128 == 0)
                {
                    float vowel = Mathf.SmoothStep(0f, 1f, syllable);
                    // Alternate ah/oh and eh/ah deliveries across the four takes.
                    float f1 = take % 2 == 0 ? Mathf.Lerp(760f, 480f, vowel) : Mathf.Lerp(530f, 760f, vowel);
                    float f2 = take % 2 == 0 ? Mathf.Lerp(1220f, 880f, vowel) : Mathf.Lerp(1840f, 1220f, vowel);
                    low.Tune(f1 * tract, 95f);
                    mid.Tune(f2 * tract, 130f);
                    high.Tune((profile == 3 ? 2800f : 2500f) * tract, 190f);
                }
                float white = (float)noise.NextDouble() * 2f - 1f;
                breath = breath * 0.75f + white * 0.25f;
                float contour = take == 1 ? 1.35f - u * 0.5f : 0.95f + 0.35f * Mathf.Sin(Mathf.PI * u) - 0.12f * u;
                float f0 = fundamental * (contour + 0.025f * Mathf.Sin(2 * Mathf.PI * 7f * t) + breath * 0.012f);
                phase = Mathf.Repeat(phase + f0 / Rate, 1f);
                float pulse = phase < 0.62f ? Mathf.Pow(Mathf.Sin(Mathf.PI * phase / 0.62f), 2f) : 0f;
                float excitation = (pulse - previousPulse) * Rate / f0 * 0.12f;
                previousPulse = pulse;
                excitation += breath * (profile == 3 ? 0.18f : 0.045f);
                if (profile == 3) excitation *= 0.8f + 0.2f * Mathf.Sin(2 * Mathf.PI * 36f * t);
                float voice = low.Process(excitation) + mid.Process(excitation) * 0.65f + high.Process(excitation) * 0.3f;
                float accent = 0.3f + 0.7f * Mathf.Pow(Mathf.Sin(Mathf.PI * syllable), 2f);
                float envelope = Mathf.Min(1f, t / 0.018f) * Mathf.Clamp01((duration - t) / 0.09f);
                data[i] = voice * accent * envelope;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            float gain = peak > 0f ? 0.72f / peak : 0f;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
            var clip = AudioClip.Create("Enemy cry " + profile + " take " + take, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private struct Formant
        {
            private float a, b, gain, previous, older;

            public void Tune(float frequency, float bandwidth)
            {
                float radius = Mathf.Exp(-Mathf.PI * bandwidth / Rate);
                a = 2f * radius * Mathf.Cos(2f * Mathf.PI * frequency / Rate);
                b = -radius * radius;
                gain = 1f - radius;
            }

            public float Process(float input)
            {
                float output = gain * input + a * previous + b * older;
                older = previous;
                previous = output;
                return output;
            }
        }
    }
}
