using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [Header("子面板引用")]
    public GameObject MenuPanel; 
    public GameObject SaveCanvas;
    public GameObject LoadCanvas;

    private void OnEnable()
    {
        
        ShowMenu();
    }

    public void ShowSavePanel()
    {
        MenuPanel.SetActive(false);
        SaveCanvas.SetActive(true);

    }
    public void ShowLoadPanel()
    {
        MenuPanel.SetActive(false);
        LoadCanvas.SetActive(true);

    }

    public void ShowMenu()
    {
        SaveCanvas.SetActive(false);
        MenuPanel.SetActive(true); 
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
                Application.Quit();
        #endif
    }

}

    
   
