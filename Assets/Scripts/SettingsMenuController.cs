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

    private PlayerMovement player;

    // Applies saved settings when the scene loads, even if the panel is never opened
    public static void ApplySavedSettings(PlayerMovement player)
    {
        if (player != null)
            player.mouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey, player.mouseSensitivity);

        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
    }

    void Awake()
    {
        player = FindFirstObjectByType<PlayerMovement>();

        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    void OnEnable()
    {
        // Show the current values each time the panel opens
        float sensitivity = player != null ? player.mouseSensitivity : PlayerPrefs.GetFloat(SensitivityKey, 2f);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        UpdateLabels();
    }

    public void SetSensitivity(float value)
    {
        if (player != null)
            player.mouseSensitivity = value;

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
