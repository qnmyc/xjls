using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class DialogueCanvasController : MonoBehaviour, IPointerClickHandler
{
    [Header("Speaker")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text speakerNameLabel;

    [Header("Dialogue")]
    [SerializeField] private TMP_Text dialogueTextLabel;
    [SerializeField] private Button continueButton;

    [Header("Options")]
    [SerializeField] private Transform optionsContainer;
    [SerializeField] private Button optionButtonPrefab;

    private DialogueManager manager;
    private readonly List<Button> activeOptionButtons = new List<Button>();

    public void Initialize(DialogueManager dialogueManager)
    {
        manager = dialogueManager;
        BindContinueButton();
    }

    public void ShowDialogue(DialogueRecord record)
    {
        if (record == null)
        {
            return;
        }

        SetSpeaker(record.name, record.avatar);
        SetDialogueText(record.text);
        SetOptions(ParseOptions(record.option));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CanContinueFromCanvasClick())
        {
            manager?.ContinueDialogue();
        }
    }

    private void BindContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => manager?.ContinueDialogue());
        }
    }

    private bool CanContinueFromCanvasClick()
    {
        return activeOptionButtons.Count == 0 && (continueButton == null || continueButton.interactable);
    }

    private void SetSpeaker(string speakerName, string avatar)
    {
        string displayName = string.IsNullOrWhiteSpace(speakerName) ? "未知" : speakerName;

        if (speakerNameLabel != null)
        {
            speakerNameLabel.text = displayName;
        }

        // TODO: 头像切换
    }

    private void SetDialogueText(string dialogueText)
    {
        if (dialogueTextLabel != null)
        {
            dialogueTextLabel.text = string.IsNullOrWhiteSpace(dialogueText) ? string.Empty : dialogueText;
        }
    }

    private void SetOptions(IReadOnlyList<DialogueOption> options)
    {
        ClearOptions();
        bool hasOptions = options.Count > 0;

        if (continueButton != null)
        {
            continueButton.interactable = !hasOptions;
        }

        if (!hasOptions || optionsContainer == null || optionButtonPrefab == null)
        {
            return;
        }

        for (int i = 0; i < options.Count; i++)
        {
            DialogueOption option = options[i];
            Button optionButton = Instantiate(optionButtonPrefab, optionsContainer);
            if (optionButton == null)
            {
                continue;
            }

            optionButton.gameObject.SetActive(true);
            optionButton.onClick.RemoveAllListeners();
            TMP_Text optionLabel = optionButton.GetComponentInChildren<TMP_Text>(true);
            if (optionLabel != null)
            {
                optionLabel.text = option.Text;
            }

            activeOptionButtons.Add(optionButton);
            optionButton.onClick.AddListener(() => manager?.SelectOption(option.NextId));
        }
    }

    private void ClearOptions()
    {
        for (int i = activeOptionButtons.Count - 1; i >= 0; i--)
        {
            Button optionButton = activeOptionButtons[i];
            if (optionButton == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(optionButton.gameObject);
            }
            else
            {
                DestroyImmediate(optionButton.gameObject);
            }
        }

        activeOptionButtons.Clear();
    }

    private static IReadOnlyList<DialogueOption> ParseOptions(string rawOptions)
    {
        List<DialogueOption> options = new List<DialogueOption>();
        if (string.IsNullOrWhiteSpace(rawOptions))
        {
            return options;
        }

        string[] entries = rawOptions.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawEntry in entries)
        {
            string entry = rawEntry.Trim();
            if (entry.Length == 0)
            {
                continue;
            }

            string text = entry;
            int nextId = 0;
            int separatorIndex = entry.LastIndexOf(':');
            if (separatorIndex < 0)
            {
                separatorIndex = entry.LastIndexOf('：');
            }

            if (separatorIndex >= 0)
            {
                text = entry.Substring(0, separatorIndex).Trim();
                int.TryParse(entry.Substring(separatorIndex + 1).Trim(), out nextId);
            }

            options.Add(new DialogueOption(text, nextId));
        }

        return options;
    }

    private readonly struct DialogueOption
    {
        public DialogueOption(string text, int nextId)
        {
            Text = string.IsNullOrWhiteSpace(text) ? "继续" : text;
            NextId = nextId;
        }

        public string Text { get; }
        public int NextId { get; }
    }
}
