using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Add this to the Player. It builds its own UI at runtime, so no canvas setup is needed.
//   E          pick up the item you're looking at
//   Q          drop the selected item
//   1-5/scroll select a slot
public class Hotbar : MonoBehaviour
{
    public const int SlotCount = 5;

    public float pickupRange = 3f;
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.Q;
    public float dropDistance = 1.5f;

    [Header("UI")]
    public float slotSize = 80f;
    public float slotSpacing = 8f;
    public Color slotColor = new Color(0f, 0f, 0f, 0.55f);
    public Color selectedColor = new Color(0.3f, 0.8f, 1f, 1f);
    public Color borderColor = new Color(1f, 1f, 1f, 0.25f);

    private readonly PickupItem[] slots = new PickupItem[SlotCount];
    private int selectedIndex = 0;

    private Camera playerCamera;
    private PickupItem lookedAtItem;

    private Image[] slotBorders;
    private Image[] slotIcons;
    private TextMeshProUGUI[] slotLabels;
    private TextMeshProUGUI selectedNameText;
    private TextMeshProUGUI promptText;

    public PickupItem SelectedItem => slots[selectedIndex];
    public int SelectedIndex => selectedIndex;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        BuildUI();
        RefreshUI();
        Debug.Log($"Hotbar ready on {name} ({SlotCount} slots)");
    }

    void Update()
    {
        if (PauseMenuController.IsPaused)
        {
            promptText.text = "";
            return;
        }

        HandleSlotSelection();
        UpdateLookedAtItem();

        if (Input.GetKeyDown(pickupKey) && lookedAtItem != null)
            TryPickUp(lookedAtItem);

        if (Input.GetKeyDown(dropKey))
            DropSelected();
    }

    void HandleSlotSelection()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                SelectSlot(i);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll < 0f)
            SelectSlot((selectedIndex + 1) % SlotCount);
        else if (scroll > 0f)
            SelectSlot((selectedIndex - 1 + SlotCount) % SlotCount);
    }

    void UpdateLookedAtItem()
    {
        lookedAtItem = null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, ~0, QueryTriggerInteraction.Collide))
            lookedAtItem = hit.collider.GetComponentInParent<PickupItem>();

        if (lookedAtItem == null)
            promptText.text = "";
        else if (FirstEmptySlot() < 0)
            promptText.text = "Hotbar full";
        else
            promptText.text = $"[{pickupKey}] Pick up {lookedAtItem.itemName}";
    }

    public void SelectSlot(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, SlotCount - 1);
        RefreshUI();
    }

    // Returns false if the hotbar is full
    public bool TryPickUp(PickupItem item)
    {
        // Prefer the selected slot if it's empty, otherwise the first free one
        int slot = slots[selectedIndex] == null ? selectedIndex : FirstEmptySlot();
        if (slot < 0)
            return false;

        slots[slot] = item;
        item.OnPickedUp();
        RefreshUI();
        return true;
    }

    public void DropSelected()
    {
        PickupItem item = slots[selectedIndex];
        if (item == null)
            return;

        slots[selectedIndex] = null;

        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Vector3 dropPosition = playerCamera.transform.position + forward * dropDistance;
        item.OnDropped(dropPosition, Quaternion.LookRotation(forward));

        RefreshUI();
    }

    int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                return i;
        }
        return -1;
    }

    void RefreshUI()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            PickupItem item = slots[i];
            bool selected = i == selectedIndex;

            slotBorders[i].color = selected ? selectedColor : borderColor;

            slotIcons[i].enabled = item != null && item.icon != null;
            slotIcons[i].sprite = item != null ? item.icon : null;

            // No icon? Fall back to showing the item's name in the slot
            slotLabels[i].text = item != null && item.icon == null ? item.itemName : "";
        }

        selectedNameText.text = SelectedItem != null ? SelectedItem.itemName : "";
    }

    // ---------- UI construction ----------

    void BuildUI()
    {
        GameObject canvasObject = new GameObject("HotbarCanvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1; // stay under the pause menu

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Row of slots along the bottom center
        float totalWidth = SlotCount * slotSize + (SlotCount - 1) * slotSpacing;
        RectTransform bar = CreateRect("Hotbar", canvasObject.transform);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 30f);
        bar.sizeDelta = new Vector2(totalWidth, slotSize);

        slotBorders = new Image[SlotCount];
        slotIcons = new Image[SlotCount];
        slotLabels = new TextMeshProUGUI[SlotCount];

        for (int i = 0; i < SlotCount; i++)
        {
            RectTransform slot = CreateRect($"Slot{i + 1}", bar);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0f, 0.5f);
            slot.anchoredPosition = new Vector2(i * (slotSize + slotSpacing), 0f);
            slot.sizeDelta = new Vector2(slotSize, slotSize);

            // Border is the outer image, background sits inset on top of it
            slotBorders[i] = slot.gameObject.AddComponent<Image>();

            RectTransform background = CreateRect("Background", slot);
            Stretch(background, 3f);
            background.gameObject.AddComponent<Image>().color = slotColor;

            RectTransform icon = CreateRect("Icon", slot);
            Stretch(icon, 12f);
            slotIcons[i] = icon.gameObject.AddComponent<Image>();
            slotIcons[i].preserveAspect = true;
            slotIcons[i].raycastTarget = false;

            RectTransform label = CreateRect("Label", slot);
            Stretch(label, 6f);
            slotLabels[i] = CreateText(label, 14f, TextAlignmentOptions.Center);

            RectTransform number = CreateRect("Number", slot);
            number.anchorMin = number.anchorMax = number.pivot = new Vector2(0f, 1f);
            number.anchoredPosition = new Vector2(6f, -2f);
            number.sizeDelta = new Vector2(20f, 20f);
            CreateText(number, 14f, TextAlignmentOptions.TopLeft).text = (i + 1).ToString();
        }

        // Name of the selected item, just above the bar
        RectTransform nameRect = CreateRect("SelectedName", canvasObject.transform);
        nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.anchoredPosition = new Vector2(0f, 30f + slotSize + 8f);
        nameRect.sizeDelta = new Vector2(totalWidth, 30f);
        selectedNameText = CreateText(nameRect, 22f, TextAlignmentOptions.Center);

        // "Press E to pick up" prompt, just below the center of the screen
        RectTransform promptRect = CreateRect("PickupPrompt", canvasObject.transform);
        promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, -60f);
        promptRect.sizeDelta = new Vector2(600f, 40f);
        promptText = CreateText(promptRect, 24f, TextAlignmentOptions.Center);
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
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
}
