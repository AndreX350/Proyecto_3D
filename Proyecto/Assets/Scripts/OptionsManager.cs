using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsManager : MonoBehaviour
{
    [Header("Optional UI References")]
    [SerializeField]
    private Slider sensitivitySlider;

    [SerializeField]
    private Slider volumeSlider;

    [SerializeField]
    private TMP_Dropdown qualityDropdown;

    [Header("Sensitivity Range")]
    [SerializeField]
    private float minSensitivity = 0.4f;

    [SerializeField]
    private float maxSensitivity = 4f;

    private const string SensitivityKey = "opt_camera_sensitivity";
    private const string VolumeKey = "opt_master_volume";
    private const string QualityKey = "opt_quality_level";

    private const float DefaultSensitivity = 1.6f;
    private const float DefaultVolume = 1f;

    private void Awake()
    {
        ApplyAllSavedSettings();
        SyncUiFromSavedValues();
    }

    public void SetSensitivity(float value)
    {
        float clamped = Mathf.Clamp(value, minSensitivity, maxSensitivity);
        PlayerPrefs.SetFloat(SensitivityKey, clamped);
        PlayerPrefs.Save();
        ApplySensitivity(clamped);
    }

    public void SetMasterVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, clamped);
        PlayerPrefs.Save();
        ApplyMasterVolume(clamped);
    }

    public void SetQualityLevel(int qualityIndex)
    {
        int clamped = Mathf.Clamp(qualityIndex, 0, QualitySettings.names.Length - 1);
        PlayerPrefs.SetInt(QualityKey, clamped);
        PlayerPrefs.Save();
        ApplyQuality(clamped);
    }

    public void ResetOptions()
    {
        PlayerPrefs.DeleteKey(SensitivityKey);
        PlayerPrefs.DeleteKey(VolumeKey);
        PlayerPrefs.DeleteKey(QualityKey);
        PlayerPrefs.Save();

        ApplyAllSavedSettings();
        SyncUiFromSavedValues();
    }

    public float GetSavedSensitivity()
    {
        return PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
    }

    public float GetSavedVolume()
    {
        return PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
    }

    public int GetSavedQualityLevel()
    {
        int currentQuality = QualitySettings.GetQualityLevel();
        return PlayerPrefs.GetInt(QualityKey, currentQuality);
    }

    private void ApplyAllSavedSettings()
    {
        ApplySensitivity(GetSavedSensitivity());
        ApplyMasterVolume(GetSavedVolume());
        ApplyQuality(GetSavedQualityLevel());
    }

    private void ApplySensitivity(float sensitivity)
    {
        RoomDemoCameraController[] cameras = FindObjectsOfType<RoomDemoCameraController>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].SetLookSensitivity(sensitivity);
            }
        }
    }

    private static void ApplyMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
    }

    private static void ApplyQuality(int index)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
        {
            return;
        }

        int clamped = Mathf.Clamp(index, 0, QualitySettings.names.Length - 1);
        QualitySettings.SetQualityLevel(clamped, true);
    }

    private void SyncUiFromSavedValues()
    {
        float sensitivity = GetSavedSensitivity();
        float volume = GetSavedVolume();
        int quality = GetSavedQualityLevel();

        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = minSensitivity;
            sensitivitySlider.maxValue = maxSensitivity;
            sensitivitySlider.SetValueWithoutNotify(sensitivity);
        }

        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.SetValueWithoutNotify(volume);
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            qualityDropdown.SetValueWithoutNotify(Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1));
            qualityDropdown.RefreshShownValue();
        }
    }
}
