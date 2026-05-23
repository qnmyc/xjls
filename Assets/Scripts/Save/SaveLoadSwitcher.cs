using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SaveLoadSwitcher : MonoBehaviour
{
    public GameObject savePage;
    public GameObject loadPage;


    public void ShowSavePage()
    {
        Debug.Log("======== 执行了 ShowSavePage！ ========");
        savePage.transform.SetAsLastSibling();
        loadPage.transform.SetAsFirstSibling();
    }

    public void ShowLoadPage()
    {
        Debug.Log("======== 执行了 ShowLoadPage！ ========");
        loadPage.transform.SetAsLastSibling();
        savePage.transform.SetAsFirstSibling();
    }
    
}
