using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HotbarUI : MonoBehaviour
{
    const int SlotCount = 2;
    const string ResourcePrefabPath = "UI/HotbarPanel";

    [SerializeField] int selectedSlotIndex;
    [SerializeField] RectTransform[] slotRects = new RectTransform[SlotCount];
    [SerializeField] Image[] slotBackgrounds = new Image[SlotCount];
    [SerializeField] Image[] selectionBorders = new Image[SlotCount];
    [SerializeField] Image[] itemIcons = new Image[SlotCount];
    [SerializeField] TextMeshProUGUI[] quantityTexts = new TextMeshProUGUI[SlotCount];
    [SerializeField] TextMeshProUGUI[] keyTexts = new TextMeshProUGUI[SlotCount];
    [SerializeField] Sprite slotBackgroundSprite;

    [Header("Selection Visual")]
    [SerializeField] Color selectedBorderColor = new(1f, 0.82f, 0.1f, 1f);
    [SerializeField] Color selectedSlotColor = Color.white;
    [SerializeField] Color unselectedSlotColor = Color.white;
    [SerializeField] Color selectedIconColor = Color.white;
    [SerializeField] Color unselectedIconColor = new(1f, 1f, 1f, 0.58f);
    [SerializeField] Color selectedTextColor = new(1f, 0.88f, 0.18f, 1f);
    [SerializeField] Color unselectedTextColor = Color.white;
    [SerializeField] string selectedSlotLabel = "ACTIVE";
    [SerializeField] string selectedKeyPrefix = ">";
    [SerializeField] float selectedSlotScale = 1.08f;
    [SerializeField] float unselectedSlotScale = 1f;
    [SerializeField] Vector2 panelSize = new(224f, 112f);
    [SerializeField] Vector2 slotSize = new(90f, 90f);
    [SerializeField] Vector2 iconSize = new(64f, 64f);
    [SerializeField] Vector2 borderSize = new(92f, 92f);
    [SerializeField] float borderThickness = 4f;

    readonly int[] slotQuantities = new int[SlotCount];

    public static HotbarUI Instance { get; private set; }
    public int SelectedSlotIndex => selectedSlotIndex;

    public static HotbarUI Ensure(Canvas canvas, Sprite backgroundSprite = null, HotbarUI prefabOverride = null)
    {
        if (canvas == null) return null;

        Transform existingPanel = canvas.transform.Find("HotbarPanel");
        if (existingPanel != null)
        {
            HotbarUI existingHotbar = existingPanel.GetComponent<HotbarUI>();
            if (existingHotbar == null)
            {
                Debug.LogWarning("HotbarPanel exists but has no HotbarUI component. Add it to the prefab.");
                return null;
            }

            if (backgroundSprite != null)
                existingHotbar.slotBackgroundSprite = backgroundSprite;
            existingHotbar.EnsureBuilt();
            return existingHotbar;
        }

        HotbarUI prefab = prefabOverride != null ? prefabOverride : Resources.Load<HotbarUI>(ResourcePrefabPath);
        if (prefab != null)
        {
            HotbarUI hotbarInstance = Instantiate(prefab, canvas.transform, false);
            hotbarInstance.name = "HotbarPanel";
            if (backgroundSprite != null)
                hotbarInstance.slotBackgroundSprite = backgroundSprite;
            hotbarInstance.ApplyDefaultPlacement();
            hotbarInstance.EnsureBuilt();
            return hotbarInstance;
        }

        Debug.LogWarning($"Hotbar prefab not found. Create it at Resources/{ResourcePrefabPath}.prefab or assign Hotbar Prefab on InGameHUDUIManager.");
        return null;
    }

    void Awake()
    {
        Instance = this;
        EnsureBuilt();
    }

    void OnEnable()
    {
        Instance = this;
        RefreshSelection();
    }

    public void SelectSlot(int slotIndex)
    {
        selectedSlotIndex = Mathf.Clamp(slotIndex, 0, SlotCount - 1);
        RefreshSelection();
    }

    public void SetSlot(int slotIndex, Sprite icon, int quantity = 0)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return;

        if (itemIcons[slotIndex] != null)
        {
            if (icon != null)
                itemIcons[slotIndex].sprite = icon;
            itemIcons[slotIndex].enabled = itemIcons[slotIndex].sprite != null;
            itemIcons[slotIndex].preserveAspect = true;
            itemIcons[slotIndex].raycastTarget = false;
        }

        if (quantityTexts[slotIndex] != null)
        {
            slotQuantities[slotIndex] = quantity;
            quantityTexts[slotIndex].text = GetSlotLabel(slotIndex);
        }
    }

    void EnsureBuilt()
    {
        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect == null) return;
        if (panelRect.sizeDelta.x < panelSize.x || panelRect.sizeDelta.y < panelSize.y)
            panelRect.sizeDelta = panelSize;

        for (int i = 0; i < SlotCount; i++)
        {
            Transform existingSlot = transform.Find($"Slot_{i + 1}");
            if (existingSlot == null)
            {
                Debug.LogWarning($"{name} is missing Slot_{i + 1}. Please edit the HotbarPanel prefab.");
                continue;
            }

            CacheSlotReferences(existingSlot, i);
        }

        RefreshSelection();
    }

    public void ApplyDefaultPlacement()
    {
        RectTransform panelRect = GetComponent<RectTransform>();
        if (panelRect == null) return;

        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 28f);
        panelRect.sizeDelta = panelSize;
    }

    void CacheSlotReferences(Transform slot, int slotIndex)
    {
        slotRects[slotIndex] = slot.GetComponent<RectTransform>();
        if (slotRects[slotIndex] != null)
            slotRects[slotIndex].sizeDelta = slotSize;

        slotBackgrounds[slotIndex] = slot.GetComponent<Image>();
        selectionBorders[slotIndex] = slot.Find("SelectionBorder")?.GetComponent<Image>();
        itemIcons[slotIndex] = slot.Find("ItemIcon")?.GetComponent<Image>();
        quantityTexts[slotIndex] = slot.Find("QuantityText")?.GetComponent<TextMeshProUGUI>();
        keyTexts[slotIndex] = slot.Find("KeyText")?.GetComponent<TextMeshProUGUI>();

        Image slotBackground = slotBackgrounds[slotIndex];
        if (slotBackground != null && slotBackgroundSprite != null)
        {
            slotBackground.sprite = slotBackgroundSprite;
            slotBackground.type = Image.Type.Simple;
            slotBackground.preserveAspect = true;
            slotBackground.raycastTarget = false;
        }

        if (keyTexts[slotIndex] != null)
            keyTexts[slotIndex].text = GetKeyLabel(slotIndex);

        if (itemIcons[slotIndex] != null)
        {
            RectTransform iconRect = itemIcons[slotIndex].GetComponent<RectTransform>();
            if (iconRect != null)
                iconRect.sizeDelta = iconSize;
            itemIcons[slotIndex].raycastTarget = false;
            itemIcons[slotIndex].enabled = itemIcons[slotIndex].sprite != null;
        }

        if (selectionBorders[slotIndex] != null)
        {
            RectTransform borderRect = selectionBorders[slotIndex].GetComponent<RectTransform>();
            if (borderRect != null)
                borderRect.sizeDelta = borderSize;
            selectionBorders[slotIndex].color = Color.clear;
            selectionBorders[slotIndex].raycastTarget = false;
            EnsureBorderLines(selectionBorders[slotIndex].transform);
            selectionBorders[slotIndex].transform.SetAsLastSibling();
        }
    }

    void RefreshSelection()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            bool isSelected = i == selectedSlotIndex;

            if (selectionBorders[i] != null)
            {
                selectionBorders[i].enabled = isSelected;
                selectionBorders[i].color = Color.clear;
                SetBorderLinesVisible(selectionBorders[i].transform, isSelected);
                selectionBorders[i].transform.SetAsLastSibling();
            }

            if (slotRects[i] != null)
                slotRects[i].localScale = Vector3.one * (isSelected ? selectedSlotScale : unselectedSlotScale);

            if (slotBackgrounds[i] != null)
                slotBackgrounds[i].color = isSelected ? selectedSlotColor : unselectedSlotColor;

            if (itemIcons[i] != null)
                itemIcons[i].color = isSelected ? selectedIconColor : unselectedIconColor;

            if (keyTexts[i] != null)
            {
                keyTexts[i].color = isSelected ? selectedTextColor : unselectedTextColor;
                keyTexts[i].text = GetKeyLabel(i);
            }

            if (quantityTexts[i] != null)
            {
                quantityTexts[i].color = isSelected ? selectedTextColor : unselectedTextColor;
                quantityTexts[i].text = GetSlotLabel(i);
            }
        }
    }

    string GetSlotLabel(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount) return string.Empty;
        if (slotQuantities[slotIndex] > 1) return slotQuantities[slotIndex].ToString();
        return slotIndex == selectedSlotIndex ? selectedSlotLabel : string.Empty;
    }

    string GetKeyLabel(int slotIndex)
    {
        string key = (slotIndex + 1).ToString();
        return slotIndex == selectedSlotIndex ? $"{selectedKeyPrefix}{key}" : key;
    }

    void EnsureBorderLines(Transform border)
    {
        EnsureBorderLine(border, "Top", new Vector2(0.5f, 1f), new Vector2(0f, -borderThickness * 0.5f), new Vector2(borderSize.x, borderThickness));
        EnsureBorderLine(border, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, borderThickness * 0.5f), new Vector2(borderSize.x, borderThickness));
        EnsureBorderLine(border, "Left", new Vector2(0f, 0.5f), new Vector2(borderThickness * 0.5f, 0f), new Vector2(borderThickness, borderSize.y));
        EnsureBorderLine(border, "Right", new Vector2(1f, 0.5f), new Vector2(-borderThickness * 0.5f, 0f), new Vector2(borderThickness, borderSize.y));
    }

    void EnsureBorderLine(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = parent.Find(name);
        if (existing == null) return;

        RectTransform rect = existing.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        Image image = existing.GetComponent<Image>();
        if (image != null)
        {
            image.color = selectedBorderColor;
            image.raycastTarget = false;
        }
    }

    void SetBorderLinesVisible(Transform border, bool visible)
    {
        for (int i = 0; i < border.childCount; i++)
            border.GetChild(i).gameObject.SetActive(visible);
    }
}
