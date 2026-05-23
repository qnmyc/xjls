using UnityEngine;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource backgroundMusicSource;
    [SerializeField] private AudioSource soundEffectSource;

    public static AudioManager GetInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        AudioManager existingManager = FindFirstObjectByType<AudioManager>();
        if (existingManager != null)
        {
            return existingManager;
        }

        GameObject managerObject = new GameObject(nameof(AudioManager));
        return managerObject.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        EnsureChannels();
        ConfigureChannels();
        SettingsManager.GetInstance().RegisterAudioManager(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        EnsureChannels();
        ConfigureChannels();
    }

    public void PlayBackgroundMusic(AudioClip clip)
    {
        if (!TryGetSource(backgroundMusicSource, "background music"))
        {
            return;
        }

        backgroundMusicSource.Stop();
        backgroundMusicSource.clip = clip;

        if (clip != null)
        {
            backgroundMusicSource.Play();
        }
    }

    public void PlaySoundEffect(AudioClip clip)
    {
        if (!TryGetSource(soundEffectSource, "sound effect"))
        {
            return;
        }

        soundEffectSource.Stop();
        soundEffectSource.clip = clip;

        if (clip != null)
        {
            soundEffectSource.Play();
        }
    }

    public void SetBackgroundMusicVolume(float volume)
    {
        if (!TryGetSource(backgroundMusicSource, "background music"))
        {
            return;
        }

        backgroundMusicSource.volume = Mathf.Clamp01(volume);
    }

    public void SetSoundEffectVolume(float volume)
    {
        if (!TryGetSource(soundEffectSource, "sound effect"))
        {
            return;
        }

        soundEffectSource.volume = Mathf.Clamp01(volume);
    }

    private void ConfigureChannels()
    {
        if (backgroundMusicSource != null)
        {
            backgroundMusicSource.loop = true;
            backgroundMusicSource.playOnAwake = false;
        }

        if (soundEffectSource != null)
        {
            soundEffectSource.loop = false;
            soundEffectSource.playOnAwake = false;
        }
    }

    private void EnsureChannels()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        }

        if (soundEffectSource == null)
        {
            soundEffectSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        }

        if (backgroundMusicSource == soundEffectSource)
        {
            soundEffectSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private bool TryGetSource(AudioSource source, string channelName)
    {
        if (source != null)
        {
            return true;
        }

        Debug.LogError($"AudioManager is missing the {channelName} AudioSource reference.", this);
        return false;
    }
}
