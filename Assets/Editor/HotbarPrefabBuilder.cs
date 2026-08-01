using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HotbarPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/UI/HotbarPanel.prefab";
    const string SlotSpritePath = "Assets/Image/two_inventory_slots.png";

    static HotbarPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabExists;
    }

    [MenuItem("Tools/Top Down Multi/UI/Create Or Refresh Hotbar Prefab")]
    public static void CreateOrRefreshHotbarPrefab()
    {
        BuildPrefab();
        Debug.Log($"Hotbar prefab refreshed: {PrefabPath}");
    }

    static void EnsurePrefabExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (IsPrefabUpToDate()) return;

        BuildPrefab();
    }

    static bool IsPrefabUpToDate()
    {
        HotbarUI hotbar = AssetDatabase.LoadAssetAtPath<HotbarUI>(PrefabPath);
        if (hotbar == null) return false;

        RectTransform panelRect = hotbar.GetComponent<RectTransform>();
        Transform slot1 = hotbar.transform.Find("Slot_1");
        RectTransform slotRect = slot1 != null ? slot1.GetComponent<RectTransform>() : null;
        Transform borderTop = slot1 != null ? slot1.Find("SelectionBorder/Top") : null;

        SerializedObject serializedHotbar = new SerializedObject(hotbar);
        string label = serializedHotbar.FindProperty("selectedSlotLabel")?.stringValue;
        string keyPrefix = serializedHotbar.FindProperty("selectedKeyPrefix")?.stringValue;
        Color selectedSlotColor = serializedHotbar.FindProperty("selectedSlotColor")?.colorValue ?? Color.clear;

        return panelRect != null
            && slotRect != null
            && panelRect.sizeDelta.x >= 220f
            && panelRect.sizeDelta.y >= 110f
            && slotRect.sizeDelta.x >= 88f
            && slotRect.sizeDelta.y >= 88f
            && borderTop != null
            && label == "ACTIVE"
            && keyPrefix == ">"
            && selectedSlotColor == Color.white;
    }

    static void BuildPrefab()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");

        Sprite[] slotSprites = AssetDatabase.LoadAllAssetsAtPath(SlotSpritePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();

        Sprite slotSprite = slotSprites.FirstOrDefault(sprite => sprite.name.EndsWith("_0")) ?? slotSprites.FirstOrDefault();
        GameObject panelObject = new GameObject(
            "HotbarPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(HotbarUI));

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 34f);
        panelRect.sizeDelta = new Vector2(224f, 112f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.04f, 0.035f, 0.03f, 0.45f);
        panelImage.raycastTarget = false;

        HorizontalLayoutGroup layout = panelObject.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 14f;
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        RectTransform[] slotRects = new RectTransform[2];
        Image[] slotBackgrounds = new Image[2];
        Image[] borders = new Image[2];
        Image[] icons = new Image[2];
        TextMeshProUGUI[] quantities = new TextMeshProUGUI[2];
        TextMeshProUGUI[] keys = new TextMeshProUGUI[2];

        for (int i = 0; i < 2; i++)
        {
            GameObject slotObject = CreateSlot(panelObject.transform, i, slotSprite);
            slotRects[i] = slotObject.GetComponent<RectTransform>();
            slotBackgrounds[i] = slotObject.GetComponent<Image>();
            icons[i] = CreateImage(slotObject.transform, "ItemIcon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            icons[i].enabled = false;
            icons[i].preserveAspect = true;

            quantities[i] = CreateText(slotObject.transform, "QuantityText", new Vector2(0.5f, 0f), new Vector2(0f, 7f), new Vector2(80f, 22f), i == 0 ? "ACTIVE" : string.Empty, 15f, TextAlignmentOptions.Bottom);
            keys[i] = CreateText(slotObject.transform, "KeyText", new Vector2(0f, 1f), new Vector2(8f, -6f), new Vector2(36f, 24f), i == 0 ? ">1" : (i + 1).ToString(), 18f, TextAlignmentOptions.TopLeft);

            borders[i] = CreateImage(slotObject.transform, "SelectionBorder", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
            borders[i].color = Color.clear;
            borders[i].enabled = i == 0;
            CreateBorderLines(borders[i].transform, i == 0);
        }

        HotbarUI hotbar = panelObject.GetComponent<HotbarUI>();
        SerializedObject serializedHotbar = new SerializedObject(hotbar);
        serializedHotbar.FindProperty("selectedSlotIndex").intValue = 0;
        AssignArray(serializedHotbar.FindProperty("slotRects"), slotRects);
        AssignArray(serializedHotbar.FindProperty("slotBackgrounds"), slotBackgrounds);
        AssignArray(serializedHotbar.FindProperty("selectionBorders"), borders);
        AssignArray(serializedHotbar.FindProperty("itemIcons"), icons);
        AssignArray(serializedHotbar.FindProperty("quantityTexts"), quantities);
        AssignArray(serializedHotbar.FindProperty("keyTexts"), keys);
        serializedHotbar.FindProperty("slotBackgroundSprite").objectReferenceValue = slotSprite;
        serializedHotbar.FindProperty("selectedSlotColor").colorValue = Color.white;
        serializedHotbar.FindProperty("unselectedSlotColor").colorValue = Color.white;
        serializedHotbar.FindProperty("selectedSlotLabel").stringValue = "ACTIVE";
        serializedHotbar.FindProperty("selectedKeyPrefix").stringValue = ">";
        serializedHotbar.FindProperty("selectedSlotScale").floatValue = 1.08f;
        serializedHotbar.FindProperty("unselectedSlotScale").floatValue = 1f;
        serializedHotbar.FindProperty("borderThickness").floatValue = 4f;
        serializedHotbar.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(panelObject, PrefabPath);
        Object.DestroyImmediate(panelObject);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static GameObject CreateSlot(Transform parent, int slotIndex, Sprite slotSprite)
    {
        GameObject slotObject = new GameObject($"Slot_{slotIndex + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        slotObject.transform.SetParent(parent, false);

        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 90f);

        LayoutElement layoutElement = slotObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 90f;
        layoutElement.preferredHeight = 90f;
        layoutElement.minWidth = 90f;
        layoutElement.minHeight = 90f;

        Image image = slotObject.GetComponent<Image>();
        image.sprite = slotSprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return slotObject;
    }

    static Image CreateImage(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        childObject.transform.SetParent(parent, false);

        RectTransform rect = childObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = childObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject childObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        childObject.transform.SetParent(parent, false);

        RectTransform rect = childObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI label = childObject.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    static void CreateBorderLines(Transform parent, bool visible)
    {
        const float thickness = 4f;
        Vector2 borderSize = new(92f, 92f);
        CreateBorderLine(parent, "Top", new Vector2(0.5f, 1f), new Vector2(0f, -thickness * 0.5f), new Vector2(borderSize.x, thickness), visible);
        CreateBorderLine(parent, "Bottom", new Vector2(0.5f, 0f), new Vector2(0f, thickness * 0.5f), new Vector2(borderSize.x, thickness), visible);
        CreateBorderLine(parent, "Left", new Vector2(0f, 0.5f), new Vector2(thickness * 0.5f, 0f), new Vector2(thickness, borderSize.y), visible);
        CreateBorderLine(parent, "Right", new Vector2(1f, 0.5f), new Vector2(-thickness * 0.5f, 0f), new Vector2(thickness, borderSize.y), visible);
    }

    static Image CreateBorderLine(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool visible)
    {
        Image line = CreateImage(parent, name, anchor, anchoredPosition, size);
        line.color = new Color(1f, 0.82f, 0.05f, 1f);
        line.gameObject.SetActive(visible);
        return line;
    }

    static void AssignArray<T>(SerializedProperty property, T[] values) where T : Object
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
