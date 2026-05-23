using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsCanvasController : MonoBehaviour
{
    private static readonly float[] SettingLevels = { 1f, 0.75f, 0.5f, 0.25f };

    [SerializeField] private Button closeButton;
    [SerializeField] private Button[] backgroundMusicButtons;
    [SerializeField] private Button[] brightnessButtons;
    [SerializeField] private Button[] soundEffectButtons;

    private SettingsManager manager;
    private readonly List<ButtonVisualState> buttonVisualStates = new List<ButtonVisualState>();

    private readonly struct ButtonVisualState
    {
        public ButtonVisualState(Button button)
        {
            Button = button;
            Colors = button.colors;
            Image = button.GetComponent<Image>();
            ImageColor = Image != null ? Image.color : Color.white;
        }

        public Button Button { get; }
        public Image Image { get; }
        public ColorBlock Colors { get; }
        public Color ImageColor { get; }
    }

    public void Initialize(SettingsManager settingsManager)
    {
        manager = settingsManager;
        CacheButtons();
        BindButtons();
        Refresh(manager.CurrentSettings);
    }

    public void Refresh(SettingsData settings)
    {
        RefreshButtonGroup(backgroundMusicButtons, settings.backgroundMusicVolume);
        RefreshButtonGroup(brightnessButtons, settings.brightness);
        RefreshButtonGroup(soundEffectButtons, settings.soundEffectVolume);
    }

    private void CacheButtons()
    {
        closeButton = closeButton != null ? closeButton : FindButton("Back");
        backgroundMusicButtons = ResolveButtonGroup(backgroundMusicButtons, "BackgroundMusic");
        brightnessButtons = ResolveButtonGroup(brightnessButtons, "Brightness");
        soundEffectButtons = ResolveButtonGroup(soundEffectButtons, "SoundEffect");

        buttonVisualStates.Clear();
        CacheButtonGroup(backgroundMusicButtons);
        CacheButtonGroup(brightnessButtons);
        CacheButtonGroup(soundEffectButtons);
    }

    private void BindButtons()
    {
        if (manager == null)
        {
            return;
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(manager.HideSettingsCanvas);
        }

        BindButtonGroup(backgroundMusicButtons, manager.SetBackgroundMusicVolume);
        BindButtonGroup(brightnessButtons, manager.SetBrightness);
        BindButtonGroup(soundEffectButtons, manager.SetSoundEffectVolume);
    }

    private void CacheButtonGroup(Button[] buttons)
    {
        if (buttons == null)
        {
            return;
        }

        foreach (Button button in buttons)
        {
            if (button != null)
            {
                buttonVisualStates.Add(new ButtonVisualState(button));
            }
        }
    }

    private void RefreshButtonGroup(Button[] buttons, float currentValue)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length && i < SettingLevels.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            ButtonVisualState state = GetButtonVisualState(button);
            bool selected = Mathf.Approximately(SettingLevels[i], currentValue);
            ApplyButtonSelectedState(state, selected);
        }
    }

    private ButtonVisualState GetButtonVisualState(Button button)
    {
        foreach (ButtonVisualState state in buttonVisualStates)
        {
            if (state.Button == button)
            {
                return state;
            }
        }

        ButtonVisualState newState = new ButtonVisualState(button);
        buttonVisualStates.Add(newState);
        return newState;
    }

    private static void ApplyButtonSelectedState(ButtonVisualState state, bool selected)
    {
        ColorBlock colors = state.Colors;
        if (selected)
        {
            Color selectedColor = state.Colors.selectedColor;
            colors.normalColor = selectedColor;
            colors.highlightedColor = selectedColor;
            colors.pressedColor = selectedColor;
            colors.selectedColor = selectedColor;

            if (state.Image != null)
            {
                state.Image.color = selectedColor;
            }
        }
        else if (state.Image != null)
        {
            state.Image.color = state.ImageColor;
        }

        state.Button.colors = colors;
    }

    private Button[] ResolveButtonGroup(Button[] currentButtons, string prefix)
    {
        if (AreButtonsAssigned(currentButtons))
        {
            return currentButtons;
        }

        return new[]
        {
            FindButton(prefix + "100%Button"),
            FindButton(prefix + "75%Button"),
            FindButton(prefix + "50%Button"),
            FindButton(prefix + "25%Button"),
        };
    }

    private static bool AreButtonsAssigned(Button[] buttons)
    {
        if (buttons == null || buttons.Length != SettingLevels.Length)
        {
            return false;
        }

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                return false;
            }
        }

        return true;
    }

    private Button FindButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static void BindButtonGroup(Button[] buttons, Action<float> handler)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Length && i < SettingLevels.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            float level = SettingLevels[i];
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => handler(level));
        }
    }
}
