using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PopupCanvasController : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private Image popupImage;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;

    [Header("Input")]
    [SerializeField] private Button clickCatcherButton;

    public void Initialize()
    {
        BindButtons();
    }

    public void Show(string imageResourcePath, string titleText, string bodyText)
    {
        if (popupImage != null)
        {
            SetImage(imageResourcePath);
        }

        if (titleLabel != null)
        {
            titleLabel.text = string.IsNullOrWhiteSpace(titleText) ? string.Empty : titleText;
        }

        if (bodyLabel != null)
        {
            bodyLabel.text = string.IsNullOrWhiteSpace(bodyText) ? string.Empty : bodyText;
        }
    }

    private void BindButtons()
    {
        if (clickCatcherButton == null)
        {
            return;
        }

        clickCatcherButton.onClick.RemoveAllListeners();
        clickCatcherButton.onClick.AddListener(() => PopupManager.GetInstance().HidePopupCanvas());
    }

    private void SetImage(string imageResourcePath)
    {
        if (popupImage == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(imageResourcePath))
        {
            popupImage.sprite = null;
            popupImage.enabled = false;
            return;
        }

        string resourcePath = imageResourcePath.Trim();
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Debug.LogWarning($"PopupCanvasController could not load image resource '{resourcePath}'.", this);
            popupImage.sprite = null;
            popupImage.enabled = false;
            return;
        }

        popupImage.sprite = sprite;
        popupImage.enabled = true;
        popupImage.preserveAspect = true;
    }
}
