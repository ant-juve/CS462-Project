using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public class HealthBar : MonoBehaviour
{
    [Header("Layout")]
    public Vector2 size = new Vector2(320f, 32f);
    public Vector2 margin = new Vector2(28f, 28f); // gap from the top-left corner of the screen

    [Header("Colors")]
    public Color fillColor = new Color(0.20f, 0.75f, 0.25f);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.55f);
    public Color textColor = Color.white;

    private PlayerHealth health;
    private RectTransform fill;
    private TextMeshProUGUI label;
    private int shownHealth = -1;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        BuildUI();
    }

    void Update()
    {
        fill.anchorMax = new Vector2(health.Fraction, 1f);

        int rounded = Mathf.CeilToInt(health.Health);
        if (rounded != shownHealth)
        {
            shownHealth = rounded;
            label.text = rounded + " / " + Mathf.RoundToInt(health.maxHealth);
        }
    }

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("HealthCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // stay under the pause menu

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform bar = CreateRect("HealthBar", canvasObject.transform);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
        bar.anchoredPosition = new Vector2(margin.x, -margin.y);
        bar.sizeDelta = size;
        bar.gameObject.AddComponent<Image>().color = backgroundColor;

        // Shrinks from the right as health drops
        fill = CreateRect("Fill", bar);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = fillColor;

        RectTransform text = CreateRect("Label", bar);
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = text.offsetMax = Vector2.zero;
        label = text.gameObject.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = size.y * 0.6f;
        label.color = textColor;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
