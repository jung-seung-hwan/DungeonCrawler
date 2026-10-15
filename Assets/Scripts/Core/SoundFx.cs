using System.Collections.Generic;
using UnityEngine;

public enum Sfx { Hit, Crit, Hurt, Kill, Pickup, LevelUp, Stairs, Fire, Shoot, Death, Magic }

// 사운드 파일 없이 코드로 레트로풍 효과음을 합성한다.
public static class SoundFx
{
    const int SampleRate = 44100;
    enum Wave { Sine, Square, Triangle }

    static readonly Dictionary<Sfx, AudioClip> cache = new();

    public static AudioClip Get(Sfx sfx)
    {
        if (cache.TryGetValue(sfx, out var clip)) return clip;

        float[] data = sfx switch
        {
            Sfx.Hit => Tone(0.08f, 220, 90, Wave.Square, noise: 0.6f),
            Sfx.Crit => Concat(Tone(0.05f, 900, 600, Wave.Square, noise: 0.3f), Tone(0.1f, 260, 80, Wave.Square, noise: 0.6f)),
            Sfx.Hurt => Tone(0.18f, 320, 110, Wave.Square, noise: 0.2f),
            Sfx.Kill => Tone(0.25f, 420, 60, Wave.Square, noise: 0.5f),
            Sfx.Pickup => Concat(Tone(0.06f, 660, 660, Wave.Square), Tone(0.09f, 990, 990, Wave.Square)),
            Sfx.LevelUp => Concat(Tone(0.07f, 523, 523, Wave.Square), Tone(0.07f, 659, 659, Wave.Square),
                                  Tone(0.07f, 784, 784, Wave.Square), Tone(0.18f, 1047, 1047, Wave.Square)),
            Sfx.Stairs => Tone(0.4f, 600, 150, Wave.Triangle),
            Sfx.Fire => Tone(0.35f, 300, 60, Wave.Sine, noise: 0.85f),
            Sfx.Shoot => Tone(0.07f, 1200, 500, Wave.Square, volume: 0.15f),
            Sfx.Death => Tone(0.9f, 300, 40, Wave.Triangle, noise: 0.2f),
            Sfx.Magic => Tone(0.3f, 400, 1400, Wave.Sine),
            _ => Tone(0.05f, 440, 440, Wave.Sine),
        };

        clip = AudioClip.Create(sfx.ToString(), data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        cache[sfx] = clip;
        return clip;
    }

    static float[] Tone(float duration, float freqStart, float freqEnd, Wave wave, float noise = 0f, float volume = 0.25f)
    {
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        var rng = new System.Random(n);
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float k = (float)i / n;
            phase += 2f * Mathf.PI * Mathf.Lerp(freqStart, freqEnd, k) / SampleRate;
            float s = wave switch
            {
                Wave.Square => Mathf.Sign(Mathf.Sin(phase)) * 0.6f,
                Wave.Triangle => 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase)),
                _ => Mathf.Sin(phase),
            };
            if (noise > 0f) s = Mathf.Lerp(s, (float)(rng.NextDouble() * 2 - 1), noise);

            float attack = Mathf.Min(1f, i / (SampleRate * 0.004f));
            float release = (1f - k) * (1f - k);
            data[i] = s * attack * release * volume;
        }
        return data;
    }

    static float[] Concat(params float[][] parts)
    {
        var result = new List<float>();
        foreach (var part in parts) result.AddRange(part);
        return result.ToArray();
    }
}
