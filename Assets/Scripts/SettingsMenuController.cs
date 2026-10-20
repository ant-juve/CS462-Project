using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuController : MonoBehaviour
{
    public const string SensitivityKey = "MouseSensitivity";
    public const string VolumeKey = "MasterVolume";
    public const string RadiationVolumeKey = "RadiationVolume";

    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValueText;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TextMeshProUGUI volumeValueText;
    [Tooltip("Leave empty to copy the volume row at runtime")]
    [SerializeField] private Slider radiationSlider;
    [SerializeField] private TextMeshProUGUI radiationValueText;

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

        if (radiationSlider == null)
            BuildRadiationRow();

        sensitivitySlider.onValueChanged.AddListener(SetSensitivity);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        radiationSlider.onValueChanged.AddListener(SetRadiationVolume);
    }

    // Copies the volume row below itself as "Radiation", so the scene doesn't need editing
    void BuildRadiationRow()
    {
        Transform volumeRow = volumeSlider.transform.parent;
        GameObject row = Instantiate(volumeRow.gameObject, volumeRow.parent);
        row.name = "RadiationRow";
        row.transform.SetSiblingIndex(volumeRow.GetSiblingIndex() + 1);

        TMP_Text label = row.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
            label.text = "Radiation";
        radiationSlider = row.GetComponentInChildren<Slider>();
        radiationValueText = row.transform.Find("Value").GetComponent<TextMeshProUGUI>();

        // The panel is a fixed-height vertical layout, so grow it by one row
        RectTransform panel = (RectTransform)volumeRow.parent;
        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 0f;
        panel.sizeDelta += new Vector2(0f, ((RectTransform)volumeRow).sizeDelta.y + spacing);
    }

    void OnEnable()
    {
        // Show the current values each time the panel opens
        float sensitivity = player != null ? player.MouseSensitivity : PlayerPrefs.GetFloat(SensitivityKey, 2f);
        sensitivitySlider.SetValueWithoutNotify(sensitivity);
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        radiationSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(RadiationVolumeKey, 1f));
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

    public void SetRadiationVolume(float value)
    {
        GeigerCounter geiger = FindFirstObjectByType<GeigerCounter>();
        if (geiger != null)
            geiger.volume = value;

        PlayerPrefs.SetFloat(RadiationVolumeKey, value);
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
        radiationValueText.text = Mathf.RoundToInt(radiationSlider.value * 100f) + "%";
    }
}
