using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

public class OptionsManager : MonoBehaviour
{
    public const string PreviousSceneKey = "opt_previous_scene";

    [Header("Optional UI References")]
    [SerializeField]
    private Slider sensitivitySlider = null;

    [SerializeField]
    private Slider volumeSlider = null;

    [SerializeField]
    private TMP_Dropdown qualityDropdown = null;

    [Header("Audio UI References")]
    [SerializeField]
    private Toggle musicToggle = null;

    [SerializeField]
    private Slider musicVolumeSlider = null;

    [SerializeField]
    private Toggle touchSoundsToggle = null;

    [SerializeField]
    private TextMeshProUGUI statusText = null;

    [Header("Button UI References")]
    [SerializeField]
    private Button backgroundMusicButton = null;

    [SerializeField]
    private Button touchSoundsButton = null;

    [SerializeField]
    private Button deleteDesignsButton = null;

    [SerializeField]
    private Button backButton = null;

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
        ResolveOptionsSceneControls();
        WireOptionsSceneControls();
        ApplyAllSavedSettings();
        SyncUiFromSavedValues();
    }

    public void SetBackgroundMusicEnabled(bool enabled)
    {
        AudioFeedbackManager.EnsureInstance().SetMusicEnabled(enabled);
        SyncAudioUiFromSavedValues();
    }

    public void SetBackgroundMusicVolume(float value)
    {
        AudioFeedbackManager.EnsureInstance().SetMusicVolume(value);
        SyncAudioUiFromSavedValues();
    }

    public void SetTouchSoundsEnabled(bool enabled)
    {
        AudioFeedbackManager.EnsureInstance().SetTouchSoundsEnabled(enabled);
        SyncAudioUiFromSavedValues();
    }

    public void ToggleBackgroundMusic()
    {
        AudioFeedbackManager audioManager = AudioFeedbackManager.EnsureInstance();
        audioManager.SetMusicEnabled(!audioManager.IsMusicEnabled);
        SyncAudioUiFromSavedValues();
    }

    public void CycleBackgroundMusicSetting()
    {
        AudioFeedbackManager audioManager = AudioFeedbackManager.EnsureInstance();
        if (!audioManager.IsMusicEnabled)
        {
            audioManager.SetMusicVolume(0.55f);
            audioManager.SetMusicEnabled(true);
        }
        else if (audioManager.MusicVolume < 0.7f)
        {
            audioManager.SetMusicVolume(0.85f);
        }
        else
        {
            audioManager.SetMusicEnabled(false);
        }

        SyncAudioUiFromSavedValues();
    }

    public void ToggleTouchSounds()
    {
        AudioFeedbackManager audioManager = AudioFeedbackManager.EnsureInstance();
        audioManager.SetTouchSoundsEnabled(!audioManager.IsTouchSoundsEnabled);
        SyncAudioUiFromSavedValues();
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

    public void DeleteSavedDesigns()
    {
        int deletedCount = DesignSaveManager.DeleteAllSavedDesigns();
        SetStatusText("Disenos borrados: " + deletedCount);
    }

    public void GoBack()
    {
        string previousScene = PlayerPrefs.GetString(PreviousSceneKey, "MainMenu");
        if (string.IsNullOrEmpty(previousScene) || previousScene == SceneManager.GetActiveScene().name)
        {
            previousScene = "MainMenu";
        }

        SceneManager.LoadScene(previousScene);
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
        SyncAudioUiFromSavedValues();

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

    private void SyncAudioUiFromSavedValues()
    {
        AudioFeedbackManager audioManager = AudioFeedbackManager.EnsureInstance();

        if (musicToggle != null)
        {
            musicToggle.SetIsOnWithoutNotify(audioManager.IsMusicEnabled);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.SetValueWithoutNotify(audioManager.MusicVolume);
        }

        if (touchSoundsToggle != null)
        {
            touchSoundsToggle.SetIsOnWithoutNotify(audioManager.IsTouchSoundsEnabled);
        }

        SetButtonLabel(backgroundMusicButton, GetMusicButtonLabel(audioManager));
        SetButtonLabel(
            touchSoundsButton,
            audioManager.IsTouchSoundsEnabled ? "Toques: Activados" : "Toques: Desactivados");
        SetButtonLabel(deleteDesignsButton, "Borrar disenos guardados");
        SetButtonLabel(backButton, "Volver");
    }

    private void SetStatusText(string message)
    {
        if (statusText == null)
        {
            Debug.Log(message);
            return;
        }

        statusText.text = message;
    }

    private void ResolveOptionsSceneControls()
    {
        if (SceneManager.GetActiveScene().name != "Options")
        {
            return;
        }

        if (backgroundMusicButton == null)
        {
            backgroundMusicButton = FindButton("BtnStart");
        }

        if (touchSoundsButton == null)
        {
            touchSoundsButton = FindButton("BtnARStart");
        }

        if (deleteDesignsButton == null)
        {
            deleteDesignsButton = FindButton("BtnSaved");
        }

        if (backButton == null)
        {
            backButton = FindButton("BtnOptions");
        }

        if (statusText == null)
        {
            GameObject statusObject = GameObject.Find("SubtitleText");
            if (statusObject != null)
            {
                statusText = statusObject.GetComponent<TextMeshProUGUI>();
            }
        }

        SetText("TitleText", "Opciones");
        SetText("SubtitleText", "Audio y datos guardados");
    }

    private void WireOptionsSceneControls()
    {
        WireButton(backgroundMusicButton, CycleBackgroundMusicSetting);
        WireButton(touchSoundsButton, ToggleTouchSounds);
        WireButton(deleteDesignsButton, DeleteSavedDesigns);
        WireButton(backButton, GoBack);
    }

    private static Button FindButton(string buttonName)
    {
        GameObject buttonObject = GameObject.Find(buttonName);
        return buttonObject == null ? null : buttonObject.GetComponent<Button>();
    }

    private static void WireButton(Button button, UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

    private static void SetText(string objectName, string value)
    {
        GameObject textObject = GameObject.Find(objectName);
        if (textObject == null)
        {
            return;
        }

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = value;
        }
    }

    private static string GetMusicButtonLabel(AudioFeedbackManager audioManager)
    {
        if (!audioManager.IsMusicEnabled)
        {
            return "Musica: Desactivada";
        }

        return audioManager.MusicVolume < 0.7f ? "Musica: Volumen medio" : "Musica: Volumen alto";
    }
}
