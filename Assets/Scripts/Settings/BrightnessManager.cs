using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BrightnessManager : MonoBehaviour
{
    public static BrightnessManager Instance { get; private set; }

    [SerializeField] private int overlaySortingOrder = 200;
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Image overlayImage;

    public static BrightnessManager GetInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        BrightnessManager existingManager = FindFirstObjectByType<BrightnessManager>();
        if (existingManager != null)
        {
            return existingManager;
        }

        GameObject managerObject = new GameObject(nameof(BrightnessManager));
        return managerObject.AddComponent<BrightnessManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SettingsManager.GetInstance().RegisterBrightnessManager(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetBrightness(float value)
    {
        EnsureOverlay();
        if (overlayImage == null)
        {
            return;
        }

        float alpha = 1f - Mathf.Clamp01(value);
        overlayImage.color = new Color(0f, 0f, 0f, alpha);
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas == null || overlayImage == null)
        {
            Debug.LogError("BrightnessManager is missing overlay references. Assign the overlay canvas and image in the prefab.", this);
            return;
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = overlaySortingOrder;
        overlayImage.raycastTarget = false;
    }
}
