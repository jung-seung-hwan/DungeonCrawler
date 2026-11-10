using UnityEngine;

// 효과음 재생, 배경음악 전환, 음량·음소거.
// 화면 없는 배치 모드(시뮬레이션)에서는 소리를 아예 만들지 않는다.
public partial class GameManager
{
    const string VolumeKey = "Volume";
    const string MutedKey = "Muted";
    const int MaxVolume = 10;
    const int DefaultVolume = 7;
    const float MusicLevel = 0.55f;       // 효과음 대비 배경음악 크기
    const float MusicFadeOutTime = 0.5f;
    const float MusicFadeInTime = 1.2f;
    const float SoundToastTime = 1.6f;

    AudioSource audioSource;
    AudioSource musicSource;
    bool soundEnabled;
    int soundVolume;
    bool muted;
    MusicTrack currentTrack;
    float musicFade;
    float soundToastUntil;

    void SetupAudio()
    {
        soundEnabled = !Application.isBatchMode;
        if (!soundEnabled) return;

        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        audioSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.volume = 0f;

        soundVolume = Mathf.Clamp(PlayerPrefs.GetInt(VolumeKey, DefaultVolume), 0, MaxVolume);
        muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        ApplyVolume();
        MusicSynth.Prepare();
    }

    void Play(Sfx sfx, float volume = 1f)
    {
        if (soundEnabled) audioSource.PlayOneShot(SoundFx.Get(sfx), volume);
    }

    // ───────────────────────── 배경음악 ─────────────────────────

    // 지금 화면에 어울리는 곡. 보스 층은 보스가 살아 있는 동안만 보스 음악이 나온다.
    MusicTrack DesiredTrack()
    {
        if (galleryMode) return MusicTrack.None;
        if (choosingClass) return MusicTrack.Menu;
        if (gameOver) return MusicTrack.GameOver;
        return StairsSealed ? MusicTrack.Boss : MusicTrack.Dungeon;
    }

    // 매 프레임 불러서, 곡이 바뀌어야 하면 지금 곡을 줄였다가 새 곡을 서서히 키운다.
    void UpdateMusic()
    {
        if (!soundEnabled) return;

        var want = DesiredTrack();
        if (want != currentTrack)
        {
            if (musicSource.isPlaying && musicFade > 0f)
            {
                musicFade = Mathf.Max(0f, musicFade - Time.unscaledDeltaTime / MusicFadeOutTime);
            }
            else if (MusicSynth.TryGet(want, out var clip)) // 아직 합성 중이면 다음 프레임에 다시 본다.
            {
                musicSource.Stop();
                currentTrack = want;
                musicFade = 0f;
                if (clip != null)
                {
                    musicSource.clip = clip;
                    musicSource.loop = MusicSynth.Loops(want);
                    musicSource.Play();
                }
            }
        }
        else if (musicFade < 1f)
        {
            musicFade = Mathf.Min(1f, musicFade + Time.unscaledDeltaTime / MusicFadeInTime);
        }
        musicSource.volume = MusicLevel * musicFade;
    }

    // ───────────────────────── 음량 ─────────────────────────

    void ApplyVolume() => AudioListener.volume = muted ? 0f : (float)soundVolume / MaxVolume;

    // M: 음소거, [ ]: 음량. 어느 화면에서나 듣는다. 소리 관련 키였으면 true.
    bool HandleSoundInput(PlayerCommand cmd)
    {
        if (!soundEnabled) return false;
        switch (cmd.Kind)
        {
            case CommandKind.Mute:
                muted = !muted;
                break;
            case CommandKind.VolumeDown:
            case CommandKind.VolumeUp:
                soundVolume = Mathf.Clamp(soundVolume + (cmd.Kind == CommandKind.VolumeUp ? 1 : -1), 0, MaxVolume);
                muted = false; // 음량을 만지면 음소거는 풀린다.
                break;
            default:
                return false;
        }

        ApplyVolume();
        PlayerPrefs.SetInt(VolumeKey, soundVolume);
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        soundToastUntil = Time.unscaledTime + SoundToastTime;
        Play(Sfx.UiMove);
        return true;
    }

    // 음량을 바꾼 직후에는 음량 막대를, 음소거 중에는 계속 음소거 표시를 미니맵 아래에 보여준다.
    void DrawSoundStatus(float w)
    {
        if (!soundEnabled) return;
        bool toast = Time.unscaledTime < soundToastUntil;
        if (!toast && !muted) return;

        string text = muted
            ? (Ko ? "음소거  <color=#888888>M 키로 켜기</color>" : "Muted  <color=#888888>M to unmute</color>")
            : $"{(Ko ? "음량" : "Volume")}  <color=#99ddff>{new string('■', soundVolume)}</color>" +
              $"<color=#555555>{new string('□', MaxVolume - soundVolume)}</color>";

        const float bw = 236;
        var rect = new Rect(w - bw - 10, 136, bw, 28);
        DrawRect(rect, new Color(0, 0, 0, 0.6f));
        FitLabel(new Rect(rect.x + 10, rect.y + 2, bw - 20, 24), text, new GUIStyle(textStyle) { fontSize = 16 });
    }
}
