using UnityEngine;

public static class GameSettings
{
    const string MusicVolumeKey = "settings_music_volume";
    const string SfxVolumeKey = "settings_sfx_volume";
    const string MouseSensitivityKey = "settings_mouse_sensitivity";

    public const float DefaultMusicVolume = 0.55f;
    public const float DefaultSfxVolume = 0.85f;
    public const float DefaultMouseSensitivity = 1f;

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
    public static float MouseSensitivity => PlayerPrefs.GetFloat(MouseSensitivityKey, DefaultMouseSensitivity);

    public static void SetMusicVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, value);
        PlayerPrefs.Save();
        GameAudioManager.Instance?.SetMusicVolume(value);
    }

    public static void SetSfxVolume(float value)
    {
        value = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, value);
        PlayerPrefs.Save();
        GameAudioManager.Instance?.SetSfxVolume(value);
    }

    public static void SetMouseSensitivity(float value)
    {
        value = Mathf.Clamp(value, 0.35f, 2.5f);
        PlayerPrefs.SetFloat(MouseSensitivityKey, value);
        PlayerPrefs.Save();
    }

    public static void ApplyAll()
    {
        GameAudioManager.Instance?.SetMusicVolume(MusicVolume);
        GameAudioManager.Instance?.SetSfxVolume(SfxVolume);
    }
}
