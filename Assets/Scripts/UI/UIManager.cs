using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public GameObject hudCanvas;
    public bool IsInGame { get; private set; }

    

    private GameObject inventoryPanel;
    private GameObject menuPanel;
    private GameObject savePanel;
    private GameObject loadPanel;
    private Transform uiRoot; 
    private GameObject currentPanel;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (hudCanvas != null)
            hudCanvas.SetActive(false);
        if (InputManager.Instance != null)
        {
            InputManager.Instance.OnCancelPressed += HandleCancel;
            InputManager.Instance.OnInventoryKeyPressed += HandleInventoryToggle;
            InputManager.Instance.OnSavePressed += HandleSave;
            InputManager.Instance.OnSkipPressed += HandleSkip;
        }
        uiRoot = GameObject.Find("UIRoot")?.transform;

        inventoryPanel = LoadPanel("Prefabs/UI/InventoryCanvas");
        menuPanel = LoadPanel("Prefabs/UI/MenuPanel");
        savePanel = LoadPanel("Prefabs/UI/SavePanel");
        loadPanel = LoadPanel("Prefabs/UI/LoadPanel");

        // 初始全部隐藏
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (savePanel != null) savePanel.SetActive(false);
        if (loadPanel != null) loadPanel.SetActive(false);
    }

    
    public void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        GameObject[] exclusivePanels = {  inventoryPanel, menuPanel };

        bool isExclusive = System.Array.Exists(exclusivePanels, p => p == targetPanel);

        if (isExclusive)
        {
         
            foreach (GameObject p in exclusivePanels)
            {
                if (p != null)
                {
                    p.SetActive(p == targetPanel);
                }
            }
            currentPanel = targetPanel; 
        }
        else
        {
            targetPanel.SetActive(true);
        }
    }

   
    public void HidePanel(GameObject panel)
    {
        if (panel == null) return;
        panel.SetActive(false);

        if (currentPanel == panel)
        {
            currentPanel = null;
        }
    }


    public void HideAllPanels()
    {
        if (inventoryPanel) inventoryPanel.SetActive(false);
        if (menuPanel) menuPanel.SetActive(false);
        if (savePanel) savePanel.SetActive(false);
        if (loadPanel) loadPanel.SetActive(false);

        currentPanel = null;

        if (GameManager.Instance != null)
            GameManager.Instance.SetState(GameStateMachine.GameState.Explore);
    }

    

    public void ShowDialogue(int dialogueId)
    {
        DialogueManager.Instance?.StartDialogue(dialogueId);
    }

    public void ShowInventoryPanel()
    {
        ShowPanel(inventoryPanel);
        GameManager.Instance?.SetState(GameStateMachine.GameState.Inventory);
    }
    public void HideInventoryPanel() { HidePanel(inventoryPanel); }

    public void ShowMenuPanel()
    {
        ShowPanel(menuPanel);
        GameManager.Instance?.SetState(GameStateMachine.GameState.Paused);
    }
    public void HideMenuPanel() { HidePanel(menuPanel); }


    public void ShowPopup(string imagePath, string title, string body)
    {
        PopupManager.GetInstance()?.ShowPopupCanvas(imagePath, title, body);
    }

    public void HUDOpenMenu()
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
        {
            Debug.Log("背包正开着，不能开菜单");
            return;
        }

        ShowMenuPanel();
    }

    public void HUDOpenInventory()
    {
      
        if (menuPanel != null && menuPanel.activeSelf)
        {
            Debug.Log("菜单正开着，不能开背包");
            return;
        }

        ShowInventoryPanel();
    }
    public void OpenSettingsCanvas()
    {
        SettingsManager.GetInstance().ShowSettingsCanvas();
    }
    public void CloseSettingsCanvas()
    {
        SettingsManager.GetInstance().HideSettingsCanvas();
    }
    public void ShowHUD()
    {
        if (hudCanvas != null)
            hudCanvas.SetActive(true);
    }
    public void HideHUD()
    {
        if (hudCanvas != null)
            hudCanvas.SetActive(false);
    }
    public void EnterGame()
    {
        IsInGame = true;
        ShowHUD();
    }
    public void ExitGame()
    {
        IsInGame = false;
        HideHUD();
    }

    private void HandleCancel()
    {
        CloseSettingsCanvas();

        if (TryCloseTopPanel())
            return;

        ShowMenuPanel();
    }



    private bool TryCloseTopPanel()
    {
        
        if (GameManager.Instance?.GetState() == GameStateMachine.GameState.Dialogue)
        {
            DialogueManager.Instance?.EndDialogue();
            return true;
        }

        PopupManager.GetInstance()?.HidePopupCanvas();

        
        var orderedPanels = new (GameObject panel, System.Action hideAction)[]
        {
        (inventoryPanel, HideInventoryPanel),
        (menuPanel,      HideMenuPanel)
        };

        foreach (var (panel, hideAction) in orderedPanels)
        {
            if (panel != null && panel.activeSelf)
            {
                hideAction?.Invoke();
                GameManager.Instance?.SetState(GameStateMachine.GameState.Explore);
                return true;
            }
        }
        return false;
    }



    private void HandleInventoryToggle()
    {
        HUDOpenInventory();
    }
    private void HandleSave()
    {
        Debug.Log("F3 存档快捷键 - 待 SaveManager 实现");
    }
    private void HandleSkip()
    {
        Debug.Log("Space 跳过 - 待对接对话系统");
    }


    private GameObject LoadPanel(string resourcePath)
    {
        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogError($"UIManager: 无法加载预制体 {resourcePath}");
            return null;
        }
        GameObject instance = Instantiate(prefab, uiRoot);
        instance.name = prefab.name;
        return instance;
    }

}