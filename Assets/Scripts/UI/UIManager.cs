using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    public GameObject hudCanvas;
    public bool IsInGame { get; private set; }

    [Header("主逻辑面板（互斥）")]
    public GameObject dialoguePanel;
    public GameObject inventoryPanel;
    public GameObject menuPanel;

    [Header("弹窗层（不互斥）")]
    public GameObject popupPanel;

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
    }

    
    public void ShowPanel(GameObject targetPanel)
    {
        if (targetPanel == null) return;

        GameObject[] exclusivePanels = { dialoguePanel, inventoryPanel, menuPanel };

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
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(false);
        if (popupPanel != null) popupPanel.SetActive(false);

        currentPanel = null;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetState(GameStateMachine.GameState.Explore);
        }
    }

    public void ShowDialoguePanel()
    {
        ShowPanel(dialoguePanel);
        GameManager.Instance?.SetState(GameStateMachine.GameState.Dialogue);
    }
    public void HideDialoguePanel() { HidePanel(dialoguePanel); }

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

    public void ShowPopupPanel() { ShowPanel(popupPanel); }
    public void HidePopupPanel() { HidePanel(popupPanel); }

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
}