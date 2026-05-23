using UnityEngine;

[DisallowMultipleComponent]
public sealed class PopupManager : MonoBehaviour
{
    private const string DefaultPopupCanvasResourcePath = "Prefabs/PopupCanvas";

    public static PopupManager Instance { get; private set; }

    [SerializeField] private GameObject popupCanvasRoot;
    [SerializeField] private GameObject popupCanvasPrefab;

    private PopupCanvasController activeCanvasController;
    private GameObject activeCanvasInstance;

    public static PopupManager GetInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        PopupManager existingManager = FindFirstObjectByType<PopupManager>();
        if (existingManager != null)
        {
            return existingManager;
        }

        GameObject managerObject = new GameObject(nameof(PopupManager));
        return managerObject.AddComponent<PopupManager>();
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
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 获取物品弹窗提示
    /// </summary>
    /// <param name="imageResourcePath">图片资源路径</param>
    /// <param name="titleText">标题文本</param>
    /// <param name="bodyText">内容文本</param>
    public void ShowPopupCanvas(string imageResourcePath, string titleText, string bodyText)
    {
        LoadPopupCanvas();

        if (activeCanvasController == null)
        {
            return;
        }

        activeCanvasController.Show(imageResourcePath, titleText, bodyText);
    }

    public void HidePopupCanvas()
    {
        UnloadPopupCanvas();
    }

    private void LoadPopupCanvas()
    {
        GameObject canvasInstance = ResolvePopupCanvasInstance();
        if (canvasInstance == null)
        {
            Debug.LogError("PopupManager could not find or create the popup canvas instance.", this);
            return;
        }

        if (!canvasInstance.activeSelf)
        {
            canvasInstance.SetActive(true);
        }

        activeCanvasInstance = canvasInstance;
        activeCanvasController = GetOrCreateCanvasController(canvasInstance);
        activeCanvasController.Initialize();
    }

    private void UnloadPopupCanvas()
    {
        if (activeCanvasInstance == null)
        {
            activeCanvasController = null;
            return;
        }

        GameObject canvasInstance = activeCanvasInstance;
        activeCanvasController = null;
        activeCanvasInstance = null;
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

    private GameObject ResolvePopupCanvasInstance()
    {
        if (activeCanvasInstance != null)
        {
            return activeCanvasInstance;
        }

        Transform root = GetPopupCanvasRoot();
        if (root == null)
        {
            Debug.LogError("PopupCanvasRoot is not assigned.", this);
            return null;
        }

        GameObject prefab = popupCanvasPrefab != null ? popupCanvasPrefab : Resources.Load<GameObject>(DefaultPopupCanvasResourcePath);
        if (prefab == null)
        {
            return null;
        }

        GameObject canvasInstance = Instantiate(prefab, root);
        canvasInstance.name = prefab.name;
        return canvasInstance;
    }

    private Transform GetPopupCanvasRoot()
    {
        return popupCanvasRoot != null ? popupCanvasRoot.transform : null;
    }

    private static PopupCanvasController GetOrCreateCanvasController(GameObject canvasInstance)
    {
        PopupCanvasController controller = canvasInstance.GetComponent<PopupCanvasController>();
        if (controller == null)
        {
            controller = canvasInstance.AddComponent<PopupCanvasController>();
        }

        return controller;
    }
}
