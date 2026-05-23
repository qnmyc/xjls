using System;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SettingsManager : MonoBehaviour
{
    private const string SettingsFileName = "settings.json";

    public static SettingsManager Instance { get; private set; }

    public SettingsData CurrentSettings => currentSettings.Clone();

    private SettingsData currentSettings = new SettingsData();
    [SerializeField] private GameObject settingsCanvasRoot;
    [SerializeField] private GameObject settingsCanvasPrefab;

    private AudioManager audioManager;
    private BrightnessManager brightnessManager;
    private SettingsCanvasController activeCanvasController;

    private static string SettingsFilePath => Path.Combine(Application.persistentDataPath, SettingsFileName);

    public static SettingsManager GetInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SettingsManager existingManager = FindFirstObjectByType<SettingsManager>();
        if (existingManager != null)
        {
            return existingManager;
        }

        GameObject managerObject = new GameObject(nameof(SettingsManager));
        return managerObject.AddComponent<SettingsManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        currentSettings = LoadSettings();
        ApplyCurrentSettings();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterAudioManager(AudioManager manager)
    {
        audioManager = manager;
        ApplyAudioSettings();
    }

    public void RegisterBrightnessManager(BrightnessManager manager)
    {
        brightnessManager = manager;
        ApplyBrightnessSetting();
    }

    private void LoadSettingsCanvas()
    {
        if (settingsCanvasRoot == null)
        {
            Debug.LogError("SettingsCanvasRoot is not assigned.", this);
            return;
        }

        GameObject canvasInstance = ResolveSettingsCanvasInstance(settingsCanvasRoot);
        if (canvasInstance == null)
        {
            Debug.LogError("SettingsManager could not find or create the settings canvas instance.", this);
            return;
        }

        if (!settingsCanvasRoot.activeSelf)
        {
            settingsCanvasRoot.SetActive(true);
        }

        if (!canvasInstance.activeSelf)
        {
            canvasInstance.SetActive(true);
        }

        activeCanvasController = GetOrCreateCanvasController(canvasInstance);
        activeCanvasController.Initialize(this);
    }

    public void ShowSettingsCanvas()
    {
        LoadSettingsCanvas();
    }

    private void UnloadSettingsCanvas()
    {
        GameObject canvasInstance = FindSettingsCanvasInstance();
        if (canvasInstance == null)
        {
            return;
        }

        if (activeCanvasController != null && activeCanvasController.gameObject == canvasInstance)
        {
            activeCanvasController = null;
        }

        canvasInstance.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(canvasInstance);
        }
        else
        {
            DestroyImmediate(canvasInstance);
        }
    }

    public void HideSettingsCanvas()
    {
        UnloadSettingsCanvas();
    }

    public void SetBackgroundMusicVolume(float value)
    {
        currentSettings.backgroundMusicVolume = Mathf.Clamp01(value);
        ApplyAudioSettings();
        RefreshSettingsCanvas();
        SaveSettings();
    }

    public void SetSoundEffectVolume(float value)
    {
        currentSettings.soundEffectVolume = Mathf.Clamp01(value);
        ApplyAudioSettings();
        RefreshSettingsCanvas();
        SaveSettings();
    }

    public void SetBrightness(float value)
    {
        currentSettings.brightness = Mathf.Clamp01(value);
        ApplyBrightnessSetting();
        RefreshSettingsCanvas();
        SaveSettings();
    }

    private void ApplyCurrentSettings()
    {
        ApplyAudioSettings();
        ApplyBrightnessSetting();
        RefreshSettingsCanvas();
    }

    private void ApplyAudioSettings()
    {
        if (audioManager == null)
        {
            return;
        }

        audioManager.SetBackgroundMusicVolume(currentSettings.backgroundMusicVolume);
        audioManager.SetSoundEffectVolume(currentSettings.soundEffectVolume);
    }

    private void ApplyBrightnessSetting()
    {
        if (brightnessManager == null)
        {
            return;
        }

        brightnessManager.SetBrightness(currentSettings.brightness);
    }

    private void RefreshSettingsCanvas()
    {
        if (activeCanvasController == null)
        {
            return;
        }

        activeCanvasController.Refresh(currentSettings);
    }

    private GameObject ResolveSettingsCanvasInstance(GameObject root)
    {
        GameObject existingInstance = FindSettingsCanvasInstance(root.transform);
        if (existingInstance != null)
        {
            return existingInstance;
        }

        if (settingsCanvasPrefab == null)
        {
            return null;
        }

        if (settingsCanvasPrefab.scene.IsValid())
        {
            Debug.LogWarning("Settings Canvas Prefab is referencing a scene object. Assign the prefab asset instead.", this);
        }

        GameObject canvasInstance = Instantiate(settingsCanvasPrefab, root.transform);
        canvasInstance.name = settingsCanvasPrefab.name;
        return canvasInstance;
    }

    private GameObject FindSettingsCanvasInstance()
    {
        return settingsCanvasRoot != null ? FindSettingsCanvasInstance(settingsCanvasRoot.transform) : null;
    }

    private static GameObject FindSettingsCanvasInstance(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.GetComponent<Canvas>() != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static SettingsCanvasController GetOrCreateCanvasController(GameObject canvasInstance)
    {
        SettingsCanvasController controller = canvasInstance.GetComponent<SettingsCanvasController>();
        return controller != null ? controller : canvasInstance.AddComponent<SettingsCanvasController>();
    }

    private SettingsData LoadSettings()
    {
        SettingsData defaults = new SettingsData();
        if (!File.Exists(SettingsFilePath))
        {
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(SettingsFilePath);
            JsonUtility.FromJsonOverwrite(json, defaults);
            defaults.Clamp();
            return defaults;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load settings from '{SettingsFilePath}'. Falling back to defaults. {exception.Message}");
            return new SettingsData();
        }
    }

    private void SaveSettings()
    {
        currentSettings.Clamp();

        try
        {
            string directory = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(currentSettings, true);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save settings to '{SettingsFilePath}'. {exception.Message}");
        }
    }
}
