using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuController : MonoBehaviour
{ 
    [SerializeField] private GameObject aboutUsCanvas;

    public void StartGame()
    {
        UIManager.Instance?.EnterGame();
        SceneFlowManager.Instance?.SwitchChapter("Chapter1_Main"); 
    }

    public void ShowAboutUsCanvas()
    {
        aboutUsCanvas.SetActive(true);
    }
    
    public void CloseAboutUsCanvas()
    {
        aboutUsCanvas.SetActive(false);
    }

    public void ShowSettingCanvas()
    {
        SettingsManager.GetInstance().ShowSettingsCanvas();
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
