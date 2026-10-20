using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The EPD readout in the bottom-left corner, styled after a Siemens EPD: a plastic casing
// around a small LCD with seven-segment digits. Builds its own UI at runtime, like Hotbar.
// Added to the player by FirstPersonPlayer.
//   R   flip between total dose and dose rate
// When the rate alarm goes off the backlight pulses amber and it jumps to the rate (R flips back to the dose).
[RequireComponent(typeof(Dosimeter))]
public class EPDDisplay : MonoBehaviour
{
    private const string FontResource = "Fonts/Digital7Mono"; // Digital-7 by Style-7, see Digital7-readme.txt
    private const float LabelWidth = 46f; // DOSE/RATE column on the left, units on the right
    private const float LabelInset = 6f;

    public KeyCode toggleKey = KeyCode.R;

    [Header("Layout")]
    public Vector2 size = new Vector2(290f, 78f);
    [Tooltip("Enlarges the whole device evenly, digits and all. Grows up and to the right from the corner")]
    [Range(0.5f, 3f)] public float scale = 1.5f;
    public Vector2 margin = new Vector2(28f, 28f); // gap from the bottom-left corner of the screen

    [Header("Colors")]
    [Tooltip("Kept dim on purpose so it doesn't glow in dark corridors")]
    public Color casingColor = new Color(0.50f, 0.49f, 0.46f);
    public Color bandColor = new Color(0.20f, 0.19f, 0.18f);
    public Color lcdColor = new Color(0.33f, 0.37f, 0.30f);
    public Color alarmColor = new Color(0.46f, 0.29f, 0.08f);
    public Color digitColor = new Color(0.06f, 0.07f, 0.05f);
    [Tooltip("How strongly unlit segments and labels show through, like a real LCD")]
    [Range(0f, 1f)] public float unlitAlpha = 0.1f;
    [Tooltip("Alarm backlight fades in and out this many times per second")]
    public float pulseSpeed = 1f;

    private static TMP_FontAsset digitFont; // built once from the .ttf and shared across scenes
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    private Dosimeter dosimeter;
    private bool showRate;
    private bool wasAlarm;
    private bool showRateBeforeAlarm;
    private bool toggledDuringAlarm;
    private string shownValue;

    private RectTransform device;
    private Image lcd;
    private TextMeshProUGUI digits;
    private TextMeshProUGUI unlitDigits;
    private TextMeshProUGUI doseLabel;
    private TextMeshProUGUI rateLabel;
    private TextMeshProUGUI alarmLabel;
    private TextMeshProUGUI unitLabel;
    private TextMeshProUGUI perHourLabel;

    void Start()
    {
        dosimeter = GetComponent<Dosimeter>();
        BuildUI();
    }

    void Update()
    {
        bool alarm = dosimeter.RateAlarm;
        UpdateAlarmSwitch(alarm);

        if (!PauseMenuController.IsPaused && Input.GetKeyDown(toggleKey))
        {
            showRate = !showRate;
            if (alarm)
                toggledDuringAlarm = true;
        }

        bool rateMode = showRate;
        float value = rateMode ? dosimeter.CurrentRate : dosimeter.TotalDose;
        device.localScale = Vector3.one * scale; // every frame so it can be tuned in Play mode

        string text = Format(value, out bool inRem);
        if (text != shownValue)
        {
            shownValue = text;
            digits.text = text;
            unlitDigits.text = UnlitPattern(text);
        }

        SetLit(doseLabel, !rateMode);
        SetLit(rateLabel, rateMode);
        SetLit(perHourLabel, rateMode);
        SetLit(alarmLabel, dosimeter.Level != Dosimeter.DoseLevel.Normal);
        unitLabel.text = inRem ? "rem" : "mrem";

        if (alarm)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f);
            lcd.color = Color.Lerp(lcdColor, alarmColor, pulse);
        }
        else
        {
            lcd.color = lcdColor;
        }
    }

    // Jumps to the rate once when the alarm starts. R still flips back to the dose during the alarm,
    // and when the alarm ends it goes back to what was showing before, unless R was pressed meanwhile
    void UpdateAlarmSwitch(bool alarm)
    {
        if (alarm == wasAlarm)
            return;

        wasAlarm = alarm;
        if (alarm)
        {
            showRateBeforeAlarm = showRate;
            toggledDuringAlarm = false;
            showRate = true;
        }
        else if (!toggledDuringAlarm)
        {
            showRate = showRateBeforeAlarm;
        }
    }

    // Four digits like the real device: mrem up to 9999, then rem
    static string Format(float mrem, out bool inRem)
    {
        if (mrem < 9999.5f)
        {
            inRem = false;
            return Mathf.RoundToInt(mrem).ToString("0000", CultureInfo.InvariantCulture);
        }

        inRem = true;
        float rem = mrem / 1000f;
        if (rem < 99.95f)
            return rem.ToString("00.0", CultureInfo.InvariantCulture);
        return Mathf.Min(Mathf.RoundToInt(rem), 9999).ToString("0000", CultureInfo.InvariantCulture);
    }

    // Every segment lit ("8"), keeping the decimal point where it is, for the faint background
    static string UnlitPattern(string text)
    {
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (char.IsDigit(chars[i]))
                chars[i] = '8';
        return new string(chars);
    }

    void SetLit(TextMeshProUGUI label, bool lit)
    {
        label.color = lit ? digitColor : Unlit(digitColor);
    }

    Color Unlit(Color color) => new Color(color.r, color.g, color.b, unlitAlpha);

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("EPDCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // stay under the pause menu

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Casing
        device = CreateRect("EPD", canvasObject.transform);
        device.anchorMin = device.anchorMax = device.pivot = Vector2.zero;
        device.anchoredPosition = margin;
        device.sizeDelta = size;
        CreateImage(device, casingColor, RoundedSprite());

        // Dark band along the top edge
        RectTransform band = CreateRect("Band", device);
        band.anchorMin = new Vector2(0f, 1f);
        band.anchorMax = Vector2.one;
        band.offsetMin = new Vector2(16f, -5f);
        band.offsetMax = new Vector2(-16f, 0f);
        CreateImage(band, bandColor, null);

        // Round button on the right
        RectTransform button = CreateRect("Button", device);
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 0.5f);
        button.anchoredPosition = new Vector2(-12f, -2f);
        button.sizeDelta = new Vector2(20f, 20f);
        CreateImage(button, bandColor, CircleSprite());

        // LCD, with a darker frame around it
        RectTransform frame = CreateRect("LCDFrame", device);
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(10f, 10f);
        frame.offsetMax = new Vector2(-42f, -13f);
        CreateImage(frame, bandColor, RoundedSprite());

        RectTransform screen = CreateRect("LCD", frame);
        Stretch(screen, 2f);
        lcd = CreateImage(screen, lcdColor, RoundedSprite());

        // Labels on the left: what's shown, and the alarm indicator
        doseLabel = CreateLabel(screen, "DOSE", 0f, 0.78f, TextAlignmentOptions.Left);
        rateLabel = CreateLabel(screen, "RATE", 0f, 0.50f, TextAlignmentOptions.Left);
        alarmLabel = CreateLabel(screen, "ALM", 0f, 0.22f, TextAlignmentOptions.Left);

        // Units on the right
        unitLabel = CreateLabel(screen, "mrem", 1f, 0.64f, TextAlignmentOptions.Right);
        perHourLabel = CreateLabel(screen, "/h", 1f, 0.36f, TextAlignmentOptions.Right);

        // Digits between the label columns, with every segment drawn faintly behind them
        RectTransform digitRect = CreateRect("Digits", screen);
        digitRect.anchorMin = Vector2.zero;
        digitRect.anchorMax = Vector2.one;
        digitRect.offsetMin = new Vector2(LabelInset + LabelWidth, 0f);
        digitRect.offsetMax = new Vector2(-(LabelInset + LabelWidth), 0f);
        unlitDigits = CreateDigits(digitRect, "Unlit");
        unlitDigits.color = Unlit(digitColor);
        digits = CreateDigits(digitRect, "Lit");
        digits.color = digitColor;
    }

    TextMeshProUGUI CreateDigits(RectTransform parent, string name)
    {
        RectTransform rect = CreateRect(name, parent);
        Stretch(rect, 0f);
        TextMeshProUGUI text = CreateText(rect, 46f, TextAlignmentOptions.Center);
        TMP_FontAsset font = DigitFont();
        if (font != null)
            text.font = font;
        return text;
    }

    TextMeshProUGUI CreateLabel(RectTransform parent, string label, float side, float y, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(label, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(side, y);
        rect.pivot = new Vector2(side, 0.5f);
        rect.anchoredPosition = new Vector2(side == 0f ? LabelInset : -LabelInset, 0f);
        rect.sizeDelta = new Vector2(LabelWidth, 15f);
        // Regular weight with a little letter spacing, like the printed labels on a real LCD;
        // bold at this size turns into blobs
        TextMeshProUGUI text = CreateText(rect, 13f, alignment);
        text.text = label;
        text.characterSpacing = 4f;
        return text;
    }

    static TMP_FontAsset DigitFont()
    {
        if (digitFont == null)
        {
            Font font = Resources.Load<Font>(FontResource);
            if (font == null)
            {
                Debug.LogWarning($"EPDDisplay: couldn't find Resources/{FontResource}, using the default font");
                return null;
            }
            digitFont = TMP_FontAsset.CreateFontAsset(font);
        }
        return digitFont;
    }

    // Rounded corners for the casing and LCD, nine-sliced so it stretches to any size
    static Sprite RoundedSprite()
    {
        if (roundedSprite == null)
            roundedSprite = MakeRoundedSprite(10);
        return roundedSprite;
    }

    static Sprite CircleSprite()
    {
        if (circleSprite == null)
            circleSprite = MakeRoundedSprite(16);
        return circleSprite;
    }

    static Sprite MakeRoundedSprite(int radius)
    {
        int size = radius * 2 + 2;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];
        float max = size - 1 - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from the nearest corner circle's center; 1px of anti-aliasing at the edge
                float dx = Mathf.Max(radius - x, x - max, 0f);
                float dy = Mathf.Max(radius - y, y - max, 0f);
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
    }

    static Image CreateImage(RectTransform rect, Color color, Sprite sprite)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }
        return image;
    }

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static TextMeshProUGUI CreateText(RectTransform rect, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }
}
