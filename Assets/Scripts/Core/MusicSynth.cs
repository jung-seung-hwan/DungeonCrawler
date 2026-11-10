using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public enum MusicTrack { None, Menu, Dungeon, Boss, GameOver }

// 배경음악을 코드로 합성한다. 곡은 "음이름:길이" 글자로 적는다(길이 단위는 16분음표).
//   "D4:4"       D4를 4칸(4분음표) 동안
//   "D3+A3:32"   화음
//   "-:8"        쉼표
//   "40:2"       음이름 대신 MIDI 번호
// 한 성부의 악보가 곡보다 짧으면 처음부터 되풀이한다. 타악기는 한 칸에 한 글자(k 킥, s 스네어, h 하이햇, t 낮은 북, . 쉼).
// 합성은 시간이 걸려 백그라운드 스레드에서 미리 해 두고(Prepare), 끝난 곡만 AudioClip으로 만든다(TryGet).
public static class MusicSynth
{
    const int SampleRate = 22050;
    enum Wave { Sine, Triangle, Square, Pulse }

    class Voice
    {
        public Wave Wave = Wave.Triangle;
        public float Volume = 0.1f;
        public float Attack = 0.01f;   // 음이 커지는 시간(초)
        public float Decay;            // 음이 잦아드는 시간 상수(초). 0이면 길이만큼 그대로 유지
        public float Release = 0.1f;   // 음이 끝난 뒤 꼬리(초)
        public float Lowpass = 1f;     // 1이면 그대로, 작을수록 어둡고 부드러운 소리
        public float DetuneCents;      // 살짝 어긋난 음을 겹쳐 두껍게 만든다
        public string Notes;
    }

    class Song
    {
        public float Bpm;
        public int Steps;              // 곡 길이(16분음표 수). 한 마디 = 16
        public bool Loop = true;
        public Voice[] Voices;
        public string Drums;
        public float DrumVolume = 0.2f;
    }

    static readonly MusicTrack[] Tracks = { MusicTrack.Menu, MusicTrack.Dungeon, MusicTrack.Boss, MusicTrack.GameOver };
    static readonly Dictionary<MusicTrack, Task<float[]>> jobs = new();
    static readonly Dictionary<MusicTrack, AudioClip> clips = new();

    public static bool Loops(MusicTrack track) => track != MusicTrack.GameOver;

    public static void Prepare()
    {
        foreach (var track in Tracks)
        {
            if (jobs.ContainsKey(track)) continue;
            var t = track;
            jobs[t] = Task.Run(() => Render(Compose(t)));
        }
    }

    // 아직 합성 중이면 false. true인데 clip이 null이면 그 곡은 없다(합성 실패 등).
    public static bool TryGet(MusicTrack track, out AudioClip clip)
    {
        if (clips.TryGetValue(track, out clip) && clip != null) return true;
        clip = null;
        if (!jobs.TryGetValue(track, out var job)) return true;
        if (!job.IsCompleted) return false;

        if (job.Status == TaskStatus.RanToCompletion)
        {
            var data = job.Result;
            clip = AudioClip.Create("Music " + track, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clips[track] = clip;
        }
        else
        {
            Debug.LogWarning($"[MUSIC] {track} 합성 실패: {job.Exception?.GetBaseException().Message}");
            jobs.Remove(track);
        }
        return true;
    }

    // ───────────────────────── 곡 ─────────────────────────

    static Song Compose(MusicTrack track) => track switch
    {
        MusicTrack.Menu => Menu(),
        MusicTrack.Boss => Boss(),
        MusicTrack.GameOver => GameOver(),
        _ => Dungeon(),
    };

    // 직업 선택 화면: 느린 가단조. 잔잔한 화음 위에 종소리 가락.
    static Song Menu() => new()
    {
        Bpm = 66,
        Steps = 8 * 16,
        Voices = new[]
        {
            new Voice
            {
                Wave = Wave.Triangle, Volume = 0.075f, Attack = 0.4f, Release = 0.9f, Lowpass = 0.3f, DetuneCents = 7,
                Notes = "A2+E3+C4:16 F2+C3+A3:16 C3+G3+E4:16 G2+D3+B3:16 A2+E3+C4:16 F2+C3+A3:16 D3+A3+F4:16 E2+B2+G#3:16",
            },
            new Voice
            {
                Wave = Wave.Triangle, Volume = 0.06f, Attack = 0.005f, Decay = 0.5f, Release = 0.3f, Lowpass = 0.45f,
                Notes = "A3:4 E4:4 A4:4 E4:4 | F3:4 C4:4 F4:4 C4:4 | G3:4 C4:4 E4:4 C4:4 | G3:4 D4:4 G4:4 D4:4 |" +
                        "A3:4 E4:4 A4:4 E4:4 | F3:4 C4:4 F4:4 C4:4 | A3:4 D4:4 F4:4 D4:4 | G#3:4 B3:4 E4:4 B3:4",
            },
            new Voice
            {
                Wave = Wave.Sine, Volume = 0.11f, Attack = 0.003f, Decay = 1.1f, Release = 1.0f,
                Notes = "E5:6 C5:2 A4:8 | C5:6 A4:2 F4:8 | E5:4 G5:4 E5:8 | D5:6 B4:2 G4:8 |" +
                        "E5:6 C5:2 A4:4 C5:4 | F5:8 E5:4 C5:4 | D5:6 F5:2 A5:8 | G#5:8 E5:8",
            },
        },
    };

    // 일반 층: 어둡고 잔잔한 라단조. 낮은 베이스, 느린 아르페지오, 드문 종소리, 멀리서 울리는 북.
    static Song Dungeon() => new()
    {
        Bpm = 76,
        Steps = 16 * 16,
        Voices = new[]
        {
            new Voice
            {
                Wave = Wave.Triangle, Volume = 0.22f, Attack = 0.03f, Release = 0.4f, Lowpass = 0.3f,
                Notes = "D2:16 D2:16 Bb1:16 Bb1:16 G1:16 G1:16 A1:16 A1:16 D2:16 D2:16 F2:16 F2:16 G1:16 G1:16 A1:16 A1:16",
            },
            new Voice
            {
                Wave = Wave.Sine, Volume = 0.065f, Attack = 0.8f, Release = 1.0f, DetuneCents = 6,
                Notes = "D3+A3:32 D3+Bb3:32 D3+G3:32 E3+A3:32 D3+A3:32 C3+A3:32 D3+G3:32 C#3+A3:32",
            },
            new Voice
            {
                Wave = Wave.Triangle, Volume = 0.1f, Attack = 0.005f, Decay = 0.45f, Release = 0.3f, Lowpass = 0.5f,
                Notes = "D4:4 A4:4 F4:4 A4:4 | D4:4 A4:4 E4:4 A4:4 | D4:4 Bb4:4 F4:4 Bb4:4 | D4:4 Bb4:4 F4:4 D4:4 |" +
                        "D4:4 G4:4 Bb4:4 G4:4 | D4:4 G4:4 Bb4:4 D5:4 | E4:4 A4:4 C#5:4 A4:4 | E4:4 A4:4 C#5:4 E4:4 |" +
                        "D4:4 A4:4 F4:4 A4:4 | D4:4 A4:4 F4:4 D5:4 | C4:4 A4:4 F4:4 A4:4 | C4:4 A4:4 F4:4 C5:4 |" +
                        "D4:4 G4:4 Bb4:4 G4:4 | Bb3:4 G4:4 D4:4 G4:4 | A3:4 E4:4 C#4:4 E4:4 | A3:4 E4:4 C#4:4 -:4",
            },
            new Voice
            {
                Wave = Wave.Sine, Volume = 0.07f, Attack = 0.003f, Decay = 1.0f, Release = 1.2f,
                Notes = "-:24 A5:8 | F5:16 D5:16 | -:16 G5:8 Bb5:8 | A5:16 E5:16 |" +
                        "D5:8 F5:8 A5:16 | C6:8 A5:8 F5:16 | G5:8 Bb5:8 D6:16 | C#6:16 A5:8 -:8",
            },
        },
        Drums = "t.....t.........................",
        DrumVolume = 0.25f,
    };

    // 보스 층: 빠른 마단조. 몰아치는 베이스와 북, 5번째 마디부터 주선율.
    static Song Boss()
    {
        const int E = 40, F = 41, G = 43, A = 45, B = 47;
        string bass = string.Join(" ",
            Riff(E, 3), Riff(E, 3), Riff(F, 4), Riff(E, 3),
            Riff(E, 3), Riff(E, 3), Riff(G, 4), Riff(F, 4),
            Riff(A, 3), Riff(A, 3), Riff(G, 4), Riff(G, 4),
            Riff(F, 4), Riff(F, 4), Riff(B, 4), Riff(B, 4));

        const string intro = "k...k...k...k...", beat = "k.h.s.h.k.k.s.h.", fill = "k.h.s.h.k.k.s.ss";
        string drums = intro + intro + intro + "k...k...s.s.ssss"
                       + beat + beat + beat + fill + beat + beat + beat + fill + beat + beat + beat + fill;

        return new Song
        {
            Bpm = 138,
            Steps = 16 * 16,
            Voices = new[]
            {
                new Voice { Wave = Wave.Square, Volume = 0.13f, Attack = 0.003f, Decay = 0.18f, Release = 0.03f, Lowpass = 0.35f, Notes = bass },
                new Voice
                {
                    Wave = Wave.Triangle, Volume = 0.05f, Attack = 0.05f, Release = 0.2f, Lowpass = 0.5f,
                    Notes = "E3+B3:64 E3+B3:64 A3+E4:32 G3+D4:32 F3+C4:32 B3+F#4:32",
                },
                new Voice
                {
                    Wave = Wave.Pulse, Volume = 0.08f, Attack = 0.005f, Decay = 0.6f, Release = 0.08f, Lowpass = 0.6f,
                    Notes = "-:64 |" +
                            "E4:6 G4:2 B4:8 | A4:4 G4:4 F#4:4 E4:4 | D4:6 G4:2 B4:8 | C5:4 A4:4 F4:8 |" +
                            "E5:6 C5:2 A4:8 | E5:4 D5:4 C5:4 A4:4 | D5:6 B4:2 G4:8 | D5:4 B4:4 G4:4 B4:4 |" +
                            "C5:6 A4:2 F4:8 | A4:4 C5:4 F5:8 | F#5:6 D#5:2 B4:8 | D#5:4 F#5:4 B5:8",
                },
            },
            Drums = drums,
            DrumVolume = 0.2f,
        };
    }

    // 한 마디짜리 8분음표 베이스 리프. third는 3도(단조 3, 장조 4).
    static string Riff(int root, int third) =>
        string.Join(" ", new[] { 0, 0, 12, 0, 0, third, 0, -2 }.Select(offset => $"{root + offset}:2"));

    // 게임 오버: 한 번만 나오는 짧고 느린 하강 가락.
    static Song GameOver() => new()
    {
        Bpm = 60,
        Steps = 4 * 16,
        Loop = false,
        Voices = new[]
        {
            new Voice
            {
                Wave = Wave.Triangle, Volume = 0.09f, Attack = 0.3f, Release = 1.0f, Lowpass = 0.3f, DetuneCents = 6,
                Notes = "A2+E3:16 F2+C3:16 D2+A2:16 A1+E2+A2:12 -:4",
            },
            new Voice
            {
                Wave = Wave.Sine, Volume = 0.12f, Attack = 0.003f, Decay = 1.2f, Release = 1.0f,
                Notes = "A4:8 G4:8 F4:8 E4:8 D4:8 C4:8 A3:12 -:4",
            },
        },
    };

    // ───────────────────────── 합성 ─────────────────────────

    static float[] Render(Song song)
    {
        double stepSamples = 60.0 / song.Bpm / 4 * SampleRate;
        var buffer = new float[(int)Math.Round(song.Steps * stepSamples)];
        var separators = new[] { ' ', '|', '\n', '\r', '\t' };

        foreach (var voice in song.Voices)
        {
            var tokens = voice.Notes.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            int step = 0;
            for (int i = 0; step < song.Steps; i++)
            {
                string token = tokens[i % tokens.Length];
                int colon = token.LastIndexOf(':');
                int length = Math.Max(1, int.Parse(token.Substring(colon + 1)));
                string names = token.Substring(0, colon);
                if (names != "-")
                {
                    int start = (int)Math.Round(step * stepSamples);
                    int hold = (int)Math.Round(length * stepSamples);
                    foreach (var name in names.Split('+'))
                        AddNote(buffer, start, hold, Frequency(name), voice, song.Loop);
                }
                step += length;
            }
        }

        if (song.Drums != null) AddDrums(buffer, song.Drums, stepSamples, song.Steps, song.DrumVolume, song.Loop);

        // 여러 성부가 겹쳐 커진 부분을 부드럽게 눌러 준다.
        for (int i = 0; i < buffer.Length; i++) buffer[i] = (float)Math.Tanh(buffer[i]);
        return buffer;
    }

    static void AddNote(float[] buffer, int start, int hold, double freq, Voice voice, bool loop)
    {
        int n = buffer.Length;
        int attack = Math.Max(1, (int)(voice.Attack * SampleRate));
        int release = Math.Max(1, (int)(voice.Release * SampleRate));
        double step = 2 * Math.PI * freq / SampleRate;
        double step2 = step * Math.Pow(2, voice.DetuneCents / 1200.0);
        double phase = 0, phase2 = 0, filtered = 0;

        for (int i = 0; i < hold + release; i++)
        {
            int index = start + i;
            if (index >= n)
            {
                if (!loop) break;
                index %= n; // 반복 곡은 꼬리를 곡 앞머리로 넘겨 이음매가 끊기지 않게 한다.
            }

            double s = Oscillate(voice.Wave, phase);
            if (voice.DetuneCents != 0) s = 0.5 * (s + Oscillate(voice.Wave, phase2));
            phase += step;
            phase2 += step2;
            filtered += voice.Lowpass * (s - filtered);

            double env = Math.Min(1.0, (double)i / attack);
            if (voice.Decay > 0) env *= Math.Exp(-i / (voice.Decay * SampleRate));
            if (i >= hold) env *= 1.0 - (double)(i - hold) / release;
            buffer[index] += (float)(filtered * env * voice.Volume);
        }
    }

    static double Oscillate(Wave wave, double phase) => wave switch
    {
        Wave.Triangle => 2 / Math.PI * Math.Asin(Math.Sin(phase)),
        Wave.Square => Math.Sin(phase) >= 0 ? 0.6 : -0.6,
        Wave.Pulse => phase % (2 * Math.PI) < Math.PI / 2 ? 0.6 : -0.6,
        _ => Math.Sin(phase),
    };

    static void AddDrums(float[] buffer, string pattern, double stepSamples, int steps, float volume, bool loop)
    {
        pattern = pattern.Replace(" ", "").Replace("|", "");
        var rng = new System.Random(7);
        int n = buffer.Length;

        for (int step = 0; step < steps; step++)
        {
            char kind = pattern[step % pattern.Length];
            double duration = kind switch { 'k' => 0.16, 's' => 0.14, 'h' => 0.04, 't' => 0.5, _ => 0 };
            if (duration <= 0) continue;

            int start = (int)Math.Round(step * stepSamples);
            int length = (int)(duration * SampleRate);
            double phase = 0, previous = 0;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= n)
                {
                    if (!loop) break;
                    index %= n;
                }

                double t = (double)i / length;
                double env = (1 - t) * (1 - t) * Math.Min(1.0, i / 40.0);
                double noise = rng.NextDouble() * 2 - 1;
                double s;
                switch (kind)
                {
                    case 'k':
                        phase += 2 * Math.PI * (120 - 75 * t) / SampleRate;
                        s = Math.Sin(phase);
                        break;
                    case 's':
                        phase += 2 * Math.PI * 190 / SampleRate;
                        s = noise * 0.7 + Math.Sin(phase) * 0.3;
                        break;
                    case 'h':
                        s = (noise - previous) * 0.35; // 잡음의 차이만 남겨 높은 소리만 낸다.
                        previous = noise;
                        break;
                    default:
                        phase += 2 * Math.PI * (75 - 20 * t) / SampleRate;
                        s = Math.Sin(phase);
                        break;
                }
                buffer[index] += (float)(s * env * volume);
            }
        }
    }

    // "C#3", "Bb2" 같은 음이름이나 MIDI 번호를 주파수로 바꾼다.
    static double Frequency(string name)
    {
        int midi;
        if (char.IsDigit(name[0]))
        {
            midi = int.Parse(name);
        }
        else
        {
            int semitone = "C D EF G A B".IndexOf(char.ToUpperInvariant(name[0]));
            int pos = 1;
            if (name[pos] == '#') { semitone++; pos++; }
            else if (name[pos] == 'b') { semitone--; pos++; }
            midi = (int.Parse(name.Substring(pos)) + 1) * 12 + semitone;
        }
        return 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    }
}
