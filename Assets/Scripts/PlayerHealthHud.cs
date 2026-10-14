using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthHud : MonoBehaviour
{
    private const float BarWidth = 320f;
    private const float BarHeight = 28f;
    private const float BarMargin = 24f;
    private const float BarBorder = 3f;

    private static readonly Color LowColor = new Color(0.85f, 0.15f, 0.10f);
    private static readonly Color HighColor = new Color(0.20f, 0.85f, 0.25f);

    private PlayerHealth health;
    private Image fill;
    private GameObject canvasObject;
    private Sprite fillSprite;
    private Texture2D fillTexture;

    public void Bind(PlayerHealth playerHealth)
    {
        health = playerHealth;
        health.OnHealthChanged += UpdateBar;
    }

    void Start()
    {
        Build();
        if (health != null)
            UpdateBar(health.CurrentHealth, health.MaxHealth);
    }

    void Build()
    {
        canvasObject = new GameObject("HealthHUD_Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = CreatePanel(canvasObject.transform, "HealthBar_Background",
            new Vector2(BarMargin, -BarMargin), new Vector2(BarWidth, BarHeight),
            new Color(0f, 0f, 0f, 0.55f));

        fill = CreatePanel(background.transform, "HealthBar_Fill",
            new Vector2(BarBorder, -BarBorder),
            new Vector2(BarWidth - BarBorder * 2f, BarHeight - BarBorder * 2f),
            HighColor);

        fill.sprite = GetFillSprite();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
    }

    Sprite GetFillSprite()
    {
        if (fillSprite != null)
            return fillSprite;

        fillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        fillTexture.SetPixel(0, 0, Color.white);
        fillTexture.Apply();

        fillSprite = Sprite.Create(fillTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        return fillSprite;
    }

    Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    void UpdateBar(float current, float max)
    {
        if (fill == null)
            return;

        float hRatio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        fill.fillAmount = hRatio;
        fill.color = Color.Lerp(LowColor, HighColor, hRatio);
    }

    void OnDestroy()
    {
        if (health != null)
            health.OnHealthChanged -= UpdateBar;
        if (canvasObject != null)
            Destroy(canvasObject);
        if (fillSprite != null)
            Destroy(fillSprite);
        if (fillTexture != null)
            Destroy(fillTexture);
    }
}
