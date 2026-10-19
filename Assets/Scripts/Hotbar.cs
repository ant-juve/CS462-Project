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

    public float pickupRange = 2.0f; // from the eyes; covers the floor about 1m ahead, within a bend and a reach
    public KeyCode pickupKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.Q;
    public float dropDistance = 1.5f;

    [Header("Held Item")]
    [Tooltip("Where held items float when there are no first-person arms to hold them")]
    public Vector3 handOffset = new Vector3(0.25f, -0.12f, 0.55f); // right, down, forward from the camera

    [Header("UI")]
    public float slotSize = 80f;
    public float slotSpacing = 8f;
    public float bottomMargin = 60f; // gap between the bar and the bottom of the screen
    public Color slotColor = new Color(0f, 0f, 0f, 0.55f);
    public Color selectedColor = new Color(0.3f, 0.8f, 1f, 1f);
    public Color borderColor = new Color(1f, 1f, 1f, 0.25f);

    private readonly PickupItem[] slots = new PickupItem[SlotCount];
    private int selectedIndex = 0;

    private Camera playerCamera;
    private Transform hand;
    private PickupItem lookedAtItem;
    private Collider lookedAtCollider; // what the last look raycast hit, so the item lookup only runs when it changes
    private string pickupPrompt = "";  // built once per item looked at, not every frame
    private readonly RaycastHit[] lookHits = new RaycastHit[8];

    private Image[] slotBorders;
    private Image[] slotIcons;
    private TextMeshProUGUI[] slotLabels;
    private TextMeshProUGUI selectedNameText;
    private TextMeshProUGUI promptText;

    public PickupItem SelectedItem => slots[selectedIndex];

    // Set by first-person arms to animate grabbing. Called with the item and a callback to run
    // when the hand reaches it; the item only goes into the hotbar at that point.
    public System.Action<PickupItem, System.Action> GrabAnimation;
    public bool IsGrabbing { get; private set; }

    void Start()
    {
        playerCamera = Camera.main;
        EnsureHand();

        BuildUI();
        OnSlotsChanged();
    }

    void EnsureHand()
    {
        if (hand != null)
            return;

        // Default: float held items in front of the camera, lower right
        hand = new GameObject("Hand").transform;
        hand.SetParent(Camera.main.transform, false);
        hand.localPosition = handOffset;
        hand.localRotation = Quaternion.Euler(0f, -15f, 0f); // angle slightly toward the center
    }

    // Lets first-person arms take over: held items follow this transform (e.g. a point in the palm)
    public void AttachHandTo(Transform grip)
    {
        EnsureHand();
        hand.SetParent(grip, false);
        hand.localPosition = Vector3.zero;
        hand.localRotation = Quaternion.identity;
        if (slotBorders != null)
            OnSlotsChanged();
    }

    void Update()
    {
        if (PauseMenuController.IsPaused)
        {
            SetPrompt("");
            return;
        }

        if (IsGrabbing)
        {
            SetPrompt("");
            return; // let the grab finish before switching, dropping or grabbing again
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
        Collider hitCollider = LookRaycast();

        // Looking at something new: find out whether it's an item, and build its prompt
        if (hitCollider != lookedAtCollider)
        {
            lookedAtCollider = hitCollider;
            lookedAtItem = hitCollider != null ? hitCollider.GetComponentInParent<PickupItem>() : null;
            pickupPrompt = lookedAtItem != null ? $"[{pickupKey}] Pick up {lookedAtItem.itemName}" : "";
        }
        else if (lookedAtItem != null && !lookedAtItem.isActiveAndEnabled)
        {
            lookedAtItem = null; // picked up since the last frame
            pickupPrompt = "";
        }

        if (lookedAtItem == null)
            SetPrompt("");
        else if (FirstEmptySlot() < 0)
            SetPrompt("Hotbar full");
        else
            SetPrompt(pickupPrompt);
    }

    // The nearest thing in front of the eyes, ignoring the player's own colliders (the camera can sit
    // right at the edge of the player's capsule, e.g. when the head tips forward to look down)
    Collider LookRaycast()
    {
        Transform eye = playerCamera.transform;
        int count = Physics.RaycastNonAlloc(eye.position, eye.forward, lookHits, pickupRange, ~0, QueryTriggerInteraction.Collide);
        Collider nearest = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = lookHits[i].collider;
            if (lookHits[i].distance < nearestDistance && !c.transform.IsChildOf(transform))
            {
                nearest = c;
                nearestDistance = lookHits[i].distance;
            }
        }
        return nearest;
    }

    // Only touches the text when it changes; setting it makes TextMeshPro rebuild its mesh
    void SetPrompt(string text)
    {
        if (!ReferenceEquals(promptText.text, text) && promptText.text != text)
            promptText.text = text;
    }

    public void SelectSlot(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, SlotCount - 1);
        OnSlotsChanged();
    }

    // Returns false if the hotbar is full or a grab is already in progress
    public bool TryPickUp(PickupItem item)
    {
        // Prefer the selected slot if it's empty, otherwise the first free one
        int slot = slots[selectedIndex] == null ? selectedIndex : FirstEmptySlot();
        if (slot < 0 || IsGrabbing)
            return false;

        // Switch to the slot the item will go into, so it ends up in the hand
        SelectSlot(slot);

        if (GrabAnimation == null)
        {
            FinishPickUp(item);
            return true;
        }

        IsGrabbing = true;
        item.BeginGrab();
        GrabAnimation(item, () =>
        {
            IsGrabbing = false;
            FinishPickUp(item);
        });
        return true;
    }

    void FinishPickUp(PickupItem item)
    {
        slots[selectedIndex] = item;
        item.OnPickedUp();
        OnSlotsChanged();
    }

    public void DropSelected()
    {
        PickupItem item = slots[selectedIndex];
        if (item == null || IsGrabbing)
            return;

        slots[selectedIndex] = null;

        Vector3 forward = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        Vector3 dropPosition = playerCamera.transform.position + forward * dropDistance;
        item.OnDropped(dropPosition, Quaternion.LookRotation(forward));

        OnSlotsChanged();
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

    void OnSlotsChanged()
    {
        UpdateHeldItem();
        RefreshUI();
    }

    // Only the selected item is shown in the hand; the rest stay hidden in the hotbar
    void UpdateHeldItem()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                continue;

            if (i == selectedIndex)
                slots[i].Hold(hand);
            else
                slots[i].Stow();
        }
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
        bar.anchoredPosition = new Vector2(0f, bottomMargin);
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
            Stretch(background, 4f);
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
        nameRect.anchoredPosition = new Vector2(0f, bottomMargin + slotSize + 8f);
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
