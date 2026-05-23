using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MenuController : MonoBehaviour
{
    [Header("子面板引用")]
    public GameObject MenuPanel; 
    public GameObject SaveCanvas;
    public GameObject Savescenes; 
    public GameObject Loadscenes;

    private void OnEnable()
    {
        
        ShowLotusHub();
    }

    public void ShowSavePanel()
    {
        MenuPanel.SetActive(false);
        SaveCanvas.SetActive(true);
        SwitchToSavePage();
    }

    public void ShowLotusHub()
    {
        SaveCanvas.SetActive(false);
        MenuPanel.SetActive(true); 
    }

    public void SwitchToSavePage()
    {
        if (Savescenes != null) Savescenes.SetActive(true);
        if (Loadscenes != null) Loadscenes.SetActive(false);
    }

    public void SwitchToLoadPage()
    {
        if (Savescenes != null) Savescenes.SetActive(false);
        if (Loadscenes != null) Loadscenes.SetActive(true);
    }
}

    
   
