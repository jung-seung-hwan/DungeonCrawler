using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    // 전투
    Hit, Crit, Hurt, Kill, Death, Shoot, Beam, Dodge, Split, Frenzy,
    // 스킬·소모품
    Magic, Whirl, Stealth, Fire, Heal, Teleport, MapReveal,
    // 줍기·상점
    Pickup, Coin, Equip, Buy, ShopOpen, Deny,
    // 던전
    Stairs, Start, Discover, Spot, Trap, Alarm, Bless, Curse,
    // 보스
    BossAppear, BossDown, SlamWarn, Slam, Summon, Blink,
    // 성장·화면
    LevelUp, Upgrade, UiMove, UiOpen, UiClose,
}

// 사운드 파일 없이 코드로 레트로풍 효과음을 합성한다. 배경음악은 MusicSynth.cs.
public static class SoundFx
{
    const int SampleRate = 44100;
    enum Wave { Sine, Square, Triangle }

    static readonly Dictionary<Sfx, AudioClip> cache = new();

    public static AudioClip Get(Sfx sfx)
    {
        if (cache.TryGetValue(sfx, out var clip) && clip != null) return clip;

        float[] data = Synthesize(sfx);
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

        clip = AudioClip.Create(sfx.ToString(), data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        cache[sfx] = clip;
        return clip;
    }

    static float[] Synthesize(Sfx sfx) => sfx switch
    {
        // ── 전투 ──
        Sfx.Hit => Tone(0.08f, 220, 90, Wave.Square, noise: 0.6f),
        Sfx.Crit => Concat(Tone(0.05f, 900, 600, Wave.Square, noise: 0.3f), Tone(0.1f, 260, 80, Wave.Square, noise: 0.6f)),
        Sfx.Hurt => Tone(0.18f, 320, 110, Wave.Square, noise: 0.2f),
        Sfx.Kill => Tone(0.25f, 420, 60, Wave.Square, noise: 0.5f),
        Sfx.Death => Tone(0.9f, 300, 40, Wave.Triangle, noise: 0.2f),
        Sfx.Shoot => Tone(0.07f, 1200, 500, Wave.Square, volume: 0.15f),
        Sfx.Beam => Tone(0.3f, 1500, 300, Wave.Square, noise: 0.1f, volume: 0.16f),              // 보스의 광선·숨결
        Sfx.Dodge => Tone(0.18f, 1200, 400, Wave.Sine, noise: 0.6f, volume: 0.15f),              // 내려찍기 회피
        Sfx.Split => Concat(Tone(0.08f, 300, 600, Wave.Sine), Tone(0.1f, 500, 250, Wave.Sine)),  // 슬라임 분열
        Sfx.Frenzy => Tone(0.5f, 90, 320, Wave.Square, noise: 0.35f, volume: 0.28f),             // 격노·광폭화

        // ── 스킬·소모품 ──
        Sfx.Magic => Tone(0.3f, 400, 1400, Wave.Sine),
        Sfx.Whirl => Mix(Tone(0.28f, 200, 900, Wave.Triangle, noise: 0.7f), Tone(0.28f, 900, 300, Wave.Square, noise: 0.5f, volume: 0.12f)),
        Sfx.Stealth => Tone(0.45f, 700, 180, Wave.Sine, noise: 0.35f, volume: 0.2f),
        Sfx.Fire => Tone(0.35f, 300, 60, Wave.Sine, noise: 0.85f),
        Sfx.Heal => Concat(Notes(Wave.Sine, 0.07f, 0.25f, 523, 659, 784), Bell(1047, 0.35f)),
        Sfx.Teleport => Concat(Tone(0.15f, 300, 1600, Wave.Sine), Tone(0.2f, 1600, 500, Wave.Sine, volume: 0.18f)),
        Sfx.MapReveal => Notes(Wave.Triangle, 0.05f, 0.2f, 392, 494, 587, 740, 880, 1175),

        // ── 줍기·상점 ──
        Sfx.Pickup => Concat(Tone(0.06f, 660, 660, Wave.Square), Tone(0.09f, 990, 990, Wave.Square)),
        Sfx.Coin => Concat(Tone(0.04f, 1319, 1319, Wave.Square, volume: 0.15f), Tone(0.12f, 1760, 1760, Wave.Square, volume: 0.15f)),
        Sfx.Equip => Mix(Tone(0.04f, 200, 100, Wave.Square, noise: 0.7f), Bell(1245, 0.25f), Bell(1661, 0.25f, 0.12f)),
        Sfx.Buy => Concat(Tone(0.04f, 1568, 1568, Wave.Square, volume: 0.15f), Tone(0.04f, 1319, 1319, Wave.Square, volume: 0.15f),
                          Tone(0.14f, 2093, 2093, Wave.Square, volume: 0.15f)),
        Sfx.ShopOpen => Concat(Bell(988, 0.18f), Bell(784, 0.4f)),
        Sfx.Deny => Concat(Tone(0.07f, 160, 150, Wave.Square, volume: 0.18f), Silence(0.03f), Tone(0.1f, 130, 120, Wave.Square, volume: 0.18f)),

        // ── 던전 ──
        Sfx.Stairs => Tone(0.4f, 600, 150, Wave.Triangle),
        Sfx.Start => Notes(Wave.Triangle, 0.1f, 0.22f, 294, 349, 440, 587),
        Sfx.Discover => Notes(Wave.Triangle, 0.09f, 0.22f, 587, 740, 880),
        Sfx.Spot => Notes(Wave.Square, 0.04f, 0.12f, 880, 1175),
        Sfx.Trap => Concat(Tone(0.03f, 2000, 1500, Wave.Square, noise: 0.6f, volume: 0.2f), Tone(0.1f, 500, 150, Wave.Square, noise: 0.7f)),
        Sfx.Alarm => Concat(Bell(1480, 0.12f), Bell(1175, 0.12f), Bell(1480, 0.12f), Bell(1175, 0.12f), Bell(1480, 0.3f)),
        Sfx.Bless => Mix(Notes(Wave.Sine, 0.12f, 0.2f, 523, 659, 784, 1047), Bell(1568, 0.6f, 0.1f)),
        Sfx.Curse => Mix(Tone(0.6f, 220, 70, Wave.Square, noise: 0.15f, volume: 0.18f), Tone(0.6f, 233, 74, Wave.Triangle)),

        // ── 보스 ──
        Sfx.BossAppear => Mix(Tone(1.0f, 110, 55, Wave.Square, noise: 0.1f, volume: 0.22f),
                              Concat(Silence(0.15f), Tone(0.85f, 165, 82, Wave.Triangle))),
        Sfx.BossDown => Concat(Tone(0.3f, 300, 50, Wave.Square, noise: 0.6f), Notes(Wave.Square, 0.09f, 0.2f, 392, 523, 659, 784),
                               Tone(0.4f, 1047, 1047, Wave.Square, volume: 0.2f)),
        Sfx.SlamWarn => Mix(Tone(0.5f, 80, 240, Wave.Square, noise: 0.2f, volume: 0.2f),
                            Concat(Tone(0.1f, 880, 880, Wave.Square, volume: 0.14f), Silence(0.06f), Tone(0.1f, 880, 880, Wave.Square, volume: 0.14f))),
        Sfx.Slam => Mix(Tone(0.5f, 120, 30, Wave.Sine, volume: 0.5f), Tone(0.35f, 200, 60, Wave.Square, noise: 0.8f, volume: 0.3f)),
        Sfx.Summon => Concat(Tone(0.15f, 200, 400, Wave.Square, volume: 0.12f), Tone(0.15f, 250, 500, Wave.Square, volume: 0.12f),
                             Tone(0.22f, 300, 700, Wave.Square, volume: 0.12f)),
        Sfx.Blink => Concat(Tone(0.1f, 1400, 300, Wave.Sine), Tone(0.12f, 300, 1400, Wave.Sine)),

        // ── 성장·화면 ──
        Sfx.LevelUp => Concat(Tone(0.07f, 523, 523, Wave.Square), Tone(0.07f, 659, 659, Wave.Square),
                              Tone(0.07f, 784, 784, Wave.Square), Tone(0.18f, 1047, 1047, Wave.Square)),
        Sfx.Upgrade => Concat(Notes(Wave.Triangle, 0.06f, 0.22f, 659, 880, 1109, 1319), Bell(1760, 0.5f)),
        Sfx.UiMove => Tone(0.03f, 700, 700, Wave.Square, volume: 0.1f),
        Sfx.UiOpen => Notes(Wave.Square, 0.04f, 0.1f, 500, 750),
        Sfx.UiClose => Notes(Wave.Square, 0.04f, 0.1f, 750, 500),

        _ => Tone(0.05f, 440, 440, Wave.Sine),
    };

    // 주파수가 freqStart에서 freqEnd로 미끄러지는 한 음. noise는 잡음을 섞는 비율(0~1).
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

    // 같은 길이의 음을 차례로 이어 붙인 짧은 가락.
    static float[] Notes(Wave wave, float noteDuration, float volume, params float[] freqs)
    {
        var parts = new float[freqs.Length][];
        for (int i = 0; i < freqs.Length; i++) parts[i] = Tone(noteDuration, freqs[i], freqs[i], wave, volume: volume);
        return Concat(parts);
    }

    // 종소리: 배음이 어긋난 사인파 몇 개가 빠르게 잦아든다.
    static float[] Bell(float freq, float duration, float volume = 0.2f)
    {
        int n = (int)(SampleRate * duration);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SampleRate;
            float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                      + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t)
                      + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t);
            float attack = Mathf.Min(1f, i / (SampleRate * 0.002f));
            float tail = 1f - (float)i / n;
            data[i] = s / 1.75f * attack * Mathf.Exp(-5f * i / n) * tail * volume;
        }
        return data;
    }

    static float[] Silence(float duration) => new float[(int)(SampleRate * duration)];

    static float[] Concat(params float[][] parts)
    {
        var result = new List<float>();
        foreach (var part in parts) result.AddRange(part);
        return result.ToArray();
    }

    // 여러 소리를 같은 시점에 겹친다.
    static float[] Mix(params float[][] layers)
    {
        int n = 0;
        foreach (var layer in layers) n = Mathf.Max(n, layer.Length);
        var result = new float[n];
        foreach (var layer in layers)
            for (int i = 0; i < layer.Length; i++) result[i] += layer[i];
        return result;
    }
}
