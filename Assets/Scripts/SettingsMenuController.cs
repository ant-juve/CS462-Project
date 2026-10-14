using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    public const string SensitivityKey = "MouseSensitivity";
    public const string VolumeKey = "MasterVolume";

    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;

    private FirstPersonPlayer player;

    // Applies saved settings when the scene loads, even if the panel is never opened
    public static void ApplySavedSettings(FirstPersonPlayer player)
    {
        if (player != null)
            player.MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, player.MouseSensitivity);

        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
    }

    void Awake()
    {
        player = FindFirstObjectByType<FirstPersonPlayer>();

        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    void OnEnable()
    {
        // Show the current values each time the panel opens
        float sensitivity = player != null ? player.MouseSensitivity : PlayerPrefs.GetFloat(SensitivityKey, 2f);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        UpdateLabels();
    }

    public void SetSensitivity(float value)
    {
        if (player != null)
            player.MouseSensitivity = value;

        PlayerPrefs.SetFloat(SensitivityKey, value);
        UpdateLabels();
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
        UpdateLabels();
    }

    void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void UpdateLabels()
    {
        sensitivityValueText.text = sensitivitySlider.value.ToString("0.0");
        volumeValueText.text = Mathf.RoundToInt(volumeSlider.value * 100f) + "%";
    }
}
