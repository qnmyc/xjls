using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UIPageViewer : MonoBehaviour//翻页分页控制器
{
    [Header("分页配置")]
    [Tooltip("每页显示多少个？（背包填6，存档填8）")]
    public int itemsPerPage = 6;

    
    private int currentPage = 0;
 
    private int totalItems = 0;

    [Header("UI 引用")]
    public Button nextButton;
    public Button backButton;

    [Tooltip("可选：用来显示 1/3 这样的页码文字")]
    public Text pageInfoText;

    [Header("翻页事件（交给具体系统去刷新槽位数据）")]
    public UnityEvent<int> OnPageChanged;

    public void Setup(int totalItemCount)
    {
        totalItems = totalItemCount;
        currentPage = 0; 
        UpdateUI();
    }

    
    public void NextPage()
    {
        int totalPages = GetTotalPages();
        if (currentPage < totalPages - 1)
        {
            currentPage++;
            UpdateUI();
        }
    }

  
    public void BackPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdateUI();
        }
    }

   
    private void UpdateUI()
    {
        int totalPages = GetTotalPages();

     
        if (backButton != null) backButton.interactable = (currentPage > 0);
        if (nextButton != null) nextButton.interactable = (currentPage < totalPages - 1);

        
        if (pageInfoText != null) pageInfoText.text = $"{currentPage + 1} / {totalPages}";

        
        OnPageChanged?.Invoke(currentPage);
    }

 
    private int GetTotalPages()
    {
        
        if (totalItems == 0) return 1;

        
        return Mathf.CeilToInt((float)totalItems / itemsPerPage);
    }
}