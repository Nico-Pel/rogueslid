using UnityEngine;
using System.Collections.Generic;
using YsoCorp;
using YsoCorp.GameUtils;

public enum DamageSoundType
{
    Default,
    Sword,
    ArrowHit,
    BulletHit,
    MagicHit
}

public class SoundManager : MonoBehaviour
{
    [System.Serializable]
    public class SoundInstance
    {
        public AudioSource Source { get; }
        public GameObject Owner { get; }

        public SoundInstance(AudioSource source, GameObject owner)
        {
            Source = source;
            Owner = owner;
        }
    }

    public static SoundManager Instance { get; private set; }

    [Header("Music")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip arenaMusic;
    [SerializeField] private AudioClip shopMusic;
    [SerializeField] private AudioClip victoryJingle;
    [SerializeField] private AudioClip victoryChoiceMusic;
    [SerializeField] private AudioClip loseJingle;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float jingleVolume = 1f;
    [SerializeField] private bool loopArenaMusic = true;
    [SerializeField] private bool loopVictoryChoiceMusic = true;

    [Header("General")]
    [SerializeField] [Range(0f, 1f)] private float sfxVolumeMultiplier = 1f;
    [SerializeField] [Range(0f, 1f)] private float uiVolumeMultiplier = 1f;

    [Header("Gameplay SFX")]
    [SerializeField] private AudioClip defaultHitSound;
    [SerializeField] private AudioClip arrowShotSound;
    [SerializeField] private AudioClip arrowHitSound;
    [SerializeField] private AudioClip bulletHitSound;
    [SerializeField] private AudioClip swordHitSound;
    [SerializeField] private AudioClip magicHitSound;
    [SerializeField] private AudioClip powerUpSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private AudioClip moneySound;
    [SerializeField] [Range(0f, 1f)] private float dashVolume = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 1f;

    private AudioSource musicSource;
    private AudioSource oneShotSource;
    private AudioSource jingleSource;
    private YCConfig soundConfig;
    private bool applicationPaused;
    private readonly List<ManagedSound> managedSounds = new List<ManagedSound>();

    private sealed class ManagedSound
    {
        public AudioSource Source;
        public float Volume;
        public bool Music;
        public bool PausedForAd;
    }

    private void LateUpdate() => ApplySettings();

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        ApplySettings();
        if (paused) ADataManager.ForceSave();
    }

    // Use the SDK's own preferences, including before online configuration exists.
    private float GetSettingsVolume(bool music)
    {
        var sdk = YCManager.instance != null ? YCManager.instance.soundManager : null;
        bool enabled = sdk != null
            ? (music ? sdk.IsMusicEnabled() : sdk.AreEffectsEnabled())
            : ADataManager.GetBool(music ? "SOUNDMUSIC" : "SOUNDEFFECT", true);
        float volume = sdk != null
            ? (music ? sdk.GetMusicVolume() : sdk.GetEffectsVolume())
            : ADataManager.GetFloat(music ? "SOUNDMUSIC_VOLUME" : "SOUNDEFFECT_VOLUME", 1f);
        bool configured = soundConfig == null || (music ? soundConfig.SoundMusic : soundConfig.SoundEffect);
        return enabled && configured ? Mathf.Clamp01(volume) : 0f;
    }

    private void TrackSource(AudioSource source, float volume, bool music = false)
    {
        managedSounds.Add(new ManagedSound { Source = source, Volume = volume, Music = music });
        ApplySettings();
    }

    private void ApplySettings()
    {
        float musicSetting = GetSettingsVolume(true);
        float effectsSetting = GetSettingsVolume(false);
        var ads = YCManager.instance != null ? YCManager.instance.adsManager : null;
        bool suspended = applicationPaused || (ads != null && ads.IsFullscreenAdPending);
        for (int i = managedSounds.Count - 1; i >= 0; i--)
        {
            ManagedSound sound = managedSounds[i];
            if (sound.Source == null) { managedSounds.RemoveAt(i); continue; }
            sound.Source.volume = sound.Volume * (sound.Music ? musicSetting : effectsSetting);
            sound.Source.mute = suspended || (sound.Music ? musicSetting : effectsSetting) <= 0f;
            // Keep music position through ads. Effects expire normally, avoiding stale hits on return.
            if (sound.Music && suspended && sound.Source.isPlaying)
            {
                sound.Source.Pause();
                sound.PausedForAd = true;
            }
            else if (!suspended && sound.PausedForAd)
            {
                sound.Source.UnPause();
                sound.PausedForAd = false;
            }
        }
    }

    public float UiVolumeMultiplier => uiVolumeMultiplier;
    public AudioClip ShopMusic => shopMusic;
    public AudioClip MoneySound => moneySound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        soundConfig = YCConfig.Create();
        EnsureAudioSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayArenaMusic()
    {
        PlayArenaMusic(arenaMusic);
    }

    public void PlayMainMenuMusic()
    {
        PlayMusic(mainMenuMusic, true);
    }

    public void PlayArenaMusic(AudioClip musicOverride)
    {
        AudioClip targetMusic = musicOverride != null ? musicOverride : arenaMusic;
        PlayMusic(targetMusic, loopArenaMusic);
    }

    public void StopArenaMusic()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.Stop();
    }

    public void PlayVictoryJingle()
    {
        StopArenaMusic();

        if (victoryJingle == null)
        {
            return;
        }

        EnsureAudioSources();
        jingleSource.Stop();
        jingleSource.PlayOneShot(victoryJingle, jingleVolume);
        ApplySettings();
    }

    public void PlayVictoryChoiceMusic()
    {
        PlayMusic(victoryChoiceMusic, loopVictoryChoiceMusic, false);
    }

    public void PlayLoseJingle()
    {
        StopArenaMusic();

        if (loseJingle == null)
        {
            return;
        }

        EnsureAudioSources();
        jingleSource.Stop();
        jingleSource.PlayOneShot(loseJingle, jingleVolume);
        ApplySettings();
    }

    public void PlayDamageSound(DamageSoundType soundType, Vector3 position, float volume = 1f)
    {
        AudioClip clip = soundType switch
        {
            DamageSoundType.Sword => swordHitSound != null ? swordHitSound : defaultHitSound,
            DamageSoundType.ArrowHit => arrowHitSound != null ? arrowHitSound : defaultHitSound,
            DamageSoundType.BulletHit => bulletHitSound != null
                ? bulletHitSound
                : (arrowHitSound != null ? arrowHitSound : defaultHitSound),
            DamageSoundType.MagicHit => magicHitSound != null ? magicHitSound : defaultHitSound,
            _ => defaultHitSound
        };

        PlayWorldSfx(clip, position, volume);
    }

    public void PlayArrowShot(Vector3 position, float volume = 1f)
    {
        PlayWorldSfx(arrowShotSound, position, volume);
    }

    public void PlayPowerUp(Vector3 position, float volume = 1f)
    {
        PlayWorldSfx(powerUpSound, position, volume);
    }

    public void PlayHeal(Vector3 position, float volume = 1f)
    {
        PlayWorldSfx(healSound, position, volume);
    }

    public void PlayDash(Vector3 position)
    {
        PlayWorldSfx(dashSound, position, dashVolume);
    }

    public void PlayClick()
    {
        PlayUiSound(clickSound, clickVolume, 1f);
    }

    public SoundInstance Play2DSound(
        AudioClip clip,
        float volume = 1f,
        float pitch = 1f,
        float duration = -1f,
        bool loop = false)
    {
        if (clip == null)
        {
            return null;
        }

        GameObject owner = new GameObject($"Sound2D_{clip.name}");
        Transform soundAnchor = GetSoundAnchor();
        owner.transform.SetParent(soundAnchor, false);
        owner.transform.localPosition = Vector3.zero;

        AudioSource source = owner.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * sfxVolumeMultiplier);
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = 0f;
        TrackSource(source, source.volume);
        source.Play();

        float destroyDelay = duration > 0f
            ? duration
            : (loop ? -1f : clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));

        if (destroyDelay > 0f)
        {
            Destroy(owner, destroyDelay);
        }

        return new SoundInstance(source, owner);
    }

    public SoundInstance PlaySound(
        AudioClip clip,
        Vector3 position,
        float volume = 1f,
        float pitch = 1f,
        float duration = -1f,
        float range = 15f,
        bool loop = false,
        Transform sourceParent = null)
    {
        if (clip == null)
        {
            return null;
        }

        GameObject owner = new GameObject($"Sound_{clip.name}");
        Transform soundAnchor = GetSoundAnchor();
        owner.transform.SetParent(soundAnchor, false);
        owner.transform.localPosition = Vector3.zero;

        if (sourceParent != null)
        {
            owner.transform.SetParent(sourceParent, true);
        }

        AudioSource source = owner.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume * sfxVolumeMultiplier);
        source.pitch = pitch;
        source.loop = loop;
        source.spatialBlend = 0f;
        TrackSource(source, source.volume);
        source.Play();

        float destroyDelay = duration > 0f
            ? duration
            : (loop ? -1f : clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)));

        if (destroyDelay > 0f)
        {
            Destroy(owner, destroyDelay);
        }

        return new SoundInstance(source, owner);
    }

    public void PlayUiSound(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();
        oneShotSource.pitch = pitch;
        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volume * uiVolumeMultiplier));
    }

    private void PlayWorldSfx(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null)
        {
            return;
        }

        PlaySound(clip, position, volume, 1f);
    }

    private void EnsureAudioSources()
    {
        EnsureJingleSource();
        if (musicSource == null)
        {
            musicSource = GetOrCreateAudioSource("MusicSource");
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            TrackSource(musicSource, musicVolume, true);
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetOrCreateAudioSource("OneShotSource");
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;
            TrackSource(oneShotSource, 1f);
        }
    }

    private void EnsureJingleSource()
    {
        if (jingleSource != null) return;
        jingleSource = GetOrCreateAudioSource("JingleSource");
        jingleSource.playOnAwake = false;
        jingleSource.loop = false;
        jingleSource.spatialBlend = 0f;
        TrackSource(jingleSource, 1f, true);
    }

    private void PlayMusic(AudioClip clip, bool loop, bool stopOneShot = true)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSources();

        if (musicSource.isPlaying && musicSource.clip == clip)
        {
            musicSource.loop = loop;
            ApplySettings();
            return;
        }

        if (stopOneShot)
        {
            oneShotSource.Stop();
            jingleSource.Stop();
        }

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
        ApplySettings();
    }

    private Transform GetSoundAnchor()
    {
        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener != null)
        {
            return listener.transform;
        }

        return transform;
    }

    private AudioSource GetOrCreateAudioSource(string childName)
    {
        Transform anchor = GetSoundAnchor();
        Transform child = anchor.Find(childName);
        GameObject target = child != null ? child.gameObject : new GameObject(childName);
        if (child == null)
        {
            target.transform.SetParent(anchor, false);
        }

        AudioSource source = target.GetComponent<AudioSource>();
        if (source == null)
        {
            source = target.AddComponent<AudioSource>();
        }

        return source;
    }
}
