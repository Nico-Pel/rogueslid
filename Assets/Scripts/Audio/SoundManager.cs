using UnityEngine;

public enum DamageSoundType
{
    Default,
    Sword,
    ArrowHit,
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
    [SerializeField] private AudioClip arenaMusic;
    [SerializeField] private AudioClip victoryJingle;
    [SerializeField] private AudioClip victoryChoiceMusic;
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
    [SerializeField] private AudioClip swordHitSound;
    [SerializeField] private AudioClip magicHitSound;
    [SerializeField] private AudioClip powerUpSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] [Range(0f, 1f)] private float dashVolume = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float clickVolume = 1f;

    private AudioSource musicSource;
    private AudioSource oneShotSource;

    public float UiVolumeMultiplier => uiVolumeMultiplier;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (arenaMusic == null)
        {
            return;
        }

        EnsureAudioSources();

        if (musicSource.isPlaying && musicSource.clip == arenaMusic)
        {
            return;
        }

        oneShotSource.Stop();
        musicSource.Stop();
        musicSource.clip = arenaMusic;
        musicSource.loop = loopArenaMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
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
        oneShotSource.Stop();
        oneShotSource.pitch = 1f;
        oneShotSource.PlayOneShot(victoryJingle, jingleVolume);
    }

    public void PlayVictoryChoiceMusic()
    {
        if (victoryChoiceMusic == null)
        {
            return;
        }

        EnsureAudioSources();

        if (musicSource.isPlaying && musicSource.clip == victoryChoiceMusic)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = victoryChoiceMusic;
        musicSource.loop = loopVictoryChoiceMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void PlayDamageSound(DamageSoundType soundType, Vector3 position, float volume = 1f)
    {
        AudioClip clip = soundType switch
        {
            DamageSoundType.Sword => swordHitSound != null ? swordHitSound : defaultHitSound,
            DamageSoundType.ArrowHit => arrowHitSound != null ? arrowHitSound : defaultHitSound,
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
        if (musicSource == null)
        {
            musicSource = GetOrCreateAudioSource("MusicSource");
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetOrCreateAudioSource("OneShotSource");
            oneShotSource.playOnAwake = false;
            oneShotSource.loop = false;
            oneShotSource.spatialBlend = 0f;
        }
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
