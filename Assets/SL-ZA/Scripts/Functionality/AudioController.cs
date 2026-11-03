using System.Collections.Generic;
using UnityEngine;
internal class AudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource gameSoundSource; // reel spin loop + its stop hit only — the one case where stopping a loop and replacing it with a one-shot is the desired behavior
    [SerializeField] private AudioSource sfxSource;        // every other one-shot SFX, via PlayOneShot so they layer instead of cutting each other off

    [Header("Background")]
    [SerializeField] private AudioClip bgMusic; // bg music.mp3

    [Header("Reel Sounds")]
    [SerializeField] private AudioClip reelSpinning;   // reel spining.mp3
    [SerializeField] private AudioClip reelStop;        // reel stop.mp3

    [Header("UI Sounds")]
    [SerializeField] private AudioClip uiButton;        // universal all button.mp3
    [SerializeField] private AudioClip maxBet;          // max bet.mp3
    [SerializeField] private AudioClip turboActivate;   // turbo rocket.mp3
    [SerializeField] private AudioClip autoplayOpen;    // hold for auto.mp3
    [SerializeField] private AudioClip autoplaySelect;  // hold for auto numbers select.mp3

    [Header("Win Line Sounds")]
    [SerializeField] private AudioClip paylineHighlight;  // paylines in slot.mp3 — plays as each line is cycled through

    [Header("Symbol Win Sounds")]
    [SerializeField] private AudioClip wildWin;   // wild win.mp3 — any line containing a wild
    [SerializeField] private AudioClip dogWin;    // dog win.mp3 — dog (4)
    [SerializeField] private AudioClip zombieWin; // zombie win.mp3 — zombie dog/man/woman (5,6,7)
    [SerializeField] private AudioClip manWin;    // manwin.mp3 — gun man, female (2,3)
    [SerializeField] private AudioClip gunWin;    // gun win.mp3 — gun, syringe, medkit (8,0,1)

    [Header("Features")]
    [SerializeField] private AudioClip wildExplosion; // wild.mp3 — syringe/medkit blast turning cells wild
    [SerializeField] private AudioClip fiveOfAKind;   // 5 of kind in or out.wav

    private bool isGameMuted = false;
    private bool isMusicMuted = false;

    private readonly Dictionary<AudioSource, bool> preFocusMuteState = new Dictionary<AudioSource, bool>();
    private bool isForceMuted = false;

    private const string PrefKeyMusicVol = "audio_music_volume";
    private const string PrefKeySfxVol = "audio_sfx_volume";

    private float _musicVolume = 0.5f;
    private float _sfxVolume = 1.0f;

    internal float MusicVolume => _musicVolume;
    internal float SfxVolume => _sfxVolume;

    private void Awake()
    {
        _musicVolume = PlayerPrefs.GetFloat(PrefKeyMusicVol, 0.5f);
        _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVol, 1.0f);
    }

    internal void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeyMusicVol, _musicVolume);
        PlayerPrefs.Save();
        if (bgMusicSource) bgMusicSource.volume = _musicVolume;
        MuteBackground(_musicVolume <= 0f);
    }

    internal void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeySfxVol, _sfxVolume);
        PlayerPrefs.Save();
        if (gameSoundSource) gameSoundSource.volume = _sfxVolume;
        if (sfxSource) sfxSource.volume = _sfxVolume;
        MuteGame(_sfxVolume <= 0f);
    }

    private void Start()
    {
        if (bgMusicSource) bgMusicSource.volume = _musicVolume;
        if (gameSoundSource) gameSoundSource.volume = _sfxVolume;
        if (sfxSource) sfxSource.volume = _sfxVolume;

        PlayBackground();
    }

    internal void PlayBackground()
    {
        if (!bgMusic) return;

        bgMusicSource.clip = bgMusic;
        bgMusicSource.loop = true;
        if (!bgMusicSource.isPlaying)
            bgMusicSource.Play();
    }

    internal void StopBackground()
    {
        bgMusicSource.Stop();
    }

    internal void PlayReelSpinning(bool loop)
    {
        PlayGame(reelSpinning, loop);
    }

    internal void PlayReelStop(bool loop = false)
    {
        PlayGame(reelStop, loop);
    }

    internal void PlayUIButton(bool loop)
    {
        PlaySfx(uiButton);
    }

    internal void PlayMaxBet()
    {
        PlaySfx(maxBet);
    }

    internal void PlayTurboActivate()
    {
        PlaySfx(turboActivate);
    }

    internal void PlayAutoplayOpen()
    {
        PlaySfx(autoplayOpen);
    }

    internal void PlayAutoplaySelect()
    {
        PlaySfx(autoplaySelect);
    }

    internal void PlayPaylineHighlight()
    {
        PlaySfx(paylineHighlight);
    }

    internal void PlayFreeSpinsWon()
    {
        //PlaySfx(freeSpinsWon);
    }

    internal void PlayFreeSpinsWinAmount()
    {
        //PlaySfx(freeSpinsWinAmount);
    }

    internal void PlayWildExplosion()
    {
        PlaySfx(wildExplosion);
    }

    internal void PlayFiveOfAKind()
    {
        PlaySfx(fiveOfAKind);
    }

    internal void PlaySymbolWin(int symbolId, bool hasWild)
    {
        if (hasWild) { PlaySfx(wildWin); return; }
        switch (symbolId)
        {
            case 4: PlaySfx(dogWin); break;
            case 5: case 6: case 7: PlaySfx(zombieWin); break;
            case 2: case 3: PlaySfx(manWin); break;
            case 0: case 1: case 8: PlaySfx(gunWin); break;
        }
    }

    // Stop-and-replace — only for the reel spin loop and its stop hit, where interrupting the
    // currently playing clip is the desired behavior (starting a new spin should cut the old one).
    private void PlayGame(AudioClip clip, bool loop)
    {
        if (!clip) return;

        gameSoundSource.Stop();
        gameSoundSource.clip = clip;
        gameSoundSource.loop = loop;
        gameSoundSource.Play();
    }

    // Fire-and-forget one-shot — layers on top of whatever else sfxSource is already playing
    // instead of cutting it off, so the frequent ludo-board/dice hits don't silence UI clicks
    // or each other.
    private void PlaySfx(AudioClip clip)
    {
        if (!clip || sfxSource == null) return;
        sfxSource.PlayOneShot(clip);
    }

    internal void StopGameAudio()
    {
        gameSoundSource.Stop();
        gameSoundSource.loop = false;
    }

    // Focus-driven — called from BOTH UIManager.OnFocusChanged (JS path) and OnApplicationFocus below.
    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        AudioSource[] sources = { bgMusicSource, gameSoundSource, sfxSource };
        foreach (var source in sources)
        {
            if (source == null) continue;
            if (forceMute)
            {
                preFocusMuteState[source] = source.mute;
                source.mute = true;
            }
            else
            {
                source.mute = preFocusMuteState.TryGetValue(source, out bool prevMuted) ? prevMuted : source.mute;
            }
        }
    }

    internal void MuteBackground(bool mute)
    {
        bgMusicSource.mute = mute;
        if (isForceMuted) preFocusMuteState[bgMusicSource] = mute;
    }

    internal void MuteGame(bool mute)
    {
        gameSoundSource.mute = mute;
        if (sfxSource != null) sfxSource.mute = mute;
        if (isForceMuted)
        {
            preFocusMuteState[gameSoundSource] = mute;
            if (sfxSource != null) preFocusMuteState[sfxSource] = mute;
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetMuteAll(!hasFocus);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        AudioListener.volume = pauseStatus ? 0.0f : 1.0f;
    }
}
