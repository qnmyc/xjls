using System;
using UnityEngine;

[Serializable]
public sealed class SettingsData
{
    public const float DefaultValue = 0.75f;

    public float backgroundMusicVolume = DefaultValue;
    public float soundEffectVolume = DefaultValue;
    public float brightness = 1f;

    public SettingsData Clone()
    {
        return new SettingsData
        {
            backgroundMusicVolume = backgroundMusicVolume,
            soundEffectVolume = soundEffectVolume,
            brightness = brightness,
        };
    }

    public void Clamp()
    {
        backgroundMusicVolume = Mathf.Clamp01(backgroundMusicVolume);
        soundEffectVolume = Mathf.Clamp01(soundEffectVolume);
        brightness = Mathf.Clamp01(brightness);
    }
}
