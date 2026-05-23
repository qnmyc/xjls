using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InteractionHUD : MonoBehaviour //控制按键相关的UI显示
{
    [Header("UI 引用")]
    public GameObject promptPanel;          // 整个提示面板
    public Image keyIconImage;              //按键图标
    public Text keyText;
    public Text promptText;
    public Image circularProgress;          // 环形进度条（用 fillAmount 控制）

    void Start()
    {
        if (promptPanel != null)
            promptPanel.SetActive(false);

        InteractionManager.Instance.OnPromptChanged += UpdatePrompt;
        InteractionManager.Instance.OnHoldProgressChanged += UpdateProgress;
    }

    void UpdatePrompt(UIPromptData data)
    {
        if (promptPanel == null) return;
        promptPanel.SetActive(data.isVisible);

        if (!data.isVisible) return;

        if (keyIconImage != null && data.keyIcon != null)
        {
            keyIconImage.sprite = data.keyIcon;
            keyIconImage.enabled = true;
        }
        else if (keyIconImage != null)
        {
            keyIconImage.enabled = false; // 没有图标则隐藏
        }

        if (keyText != null)
            keyText.text = data.keyText;

        if (promptText != null)
            promptText.text = data.promptText;

        if (circularProgress != null)
        {
            circularProgress.gameObject.SetActive(data.showProgress);
            if (data.showProgress)
                circularProgress.fillAmount = data.progressValue;
        }
    }

    void UpdateProgress(float progress)
    {
        if (circularProgress != null && circularProgress.gameObject.activeSelf)
            circularProgress.fillAmount = progress;
    }

    void OnDestroy()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.OnPromptChanged -= UpdatePrompt;
            InteractionManager.Instance.OnHoldProgressChanged -= UpdateProgress;
        }
    }
}