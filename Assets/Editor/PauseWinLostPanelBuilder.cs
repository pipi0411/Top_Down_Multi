using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PauseWinLostPanelBuilder
{
    private const string MenuPath = "Tools/Top Down Multi/UI/Create Pause Win Lost Panels";

    private const string TitlePauseSpriteName = "title_pause";
    private const string TitleLostSpriteName = "title_you_lost";
    private const string TitleWinSpriteName = "title_you_win";

    // ui_modular_sheet_0/1/2 = big frames, ui_modular_sheet_3/4/5 = long buttons.
    private const string WindowSpriteName = "ui_modular_sheet_1";
    private const string ButtonSpriteName = "ui_modular_sheet_4";

    [MenuItem(MenuPath)]
    public static void CreatePauseWinLostPanels()
    {
        Canvas canvas = FindOrCreateCanvas();
        InGameHUDUIManager hudManager = Object.FindAnyObjectByType<InGameHUDUIManager>(FindObjectsInactive.Include);

        Sprite windowSprite = FindSprite(WindowSpriteName);
        Sprite buttonSprite = FindSprite(ButtonSpriteName);
        Sprite pauseTitle = FindSprite(TitlePauseSpriteName);
        Sprite winTitle = FindSprite(TitleWinSpriteName);
        Sprite lostTitle = FindSprite(TitleLostSpriteName);

        GameObject pausePanel = CreateOrRefreshPanel(
            canvas.transform,
            "PausePanel",
            pauseTitle,
            windowSprite,
            buttonSprite,
            new[]
            {
                new PanelButton("ResumeButton", "RESUME", hudManager != null ? hudManager.ResumeGame : null),
                new PanelButton("MainMenuButton", "MAIN MENU", hudManager != null ? hudManager.ReturnToMainMenu : null)
            });

        GameObject winPanel = CreateOrRefreshPanel(
            canvas.transform,
            "WinPanel",
            winTitle,
            windowSprite,
            buttonSprite,
            new[]
            {
                new PanelButton("PlayAgainButton", "PLAY AGAIN", hudManager != null ? hudManager.RestartGame : null),
                new PanelButton("MainMenuButton", "MAIN MENU", hudManager != null ? hudManager.ReturnToMainMenu : null)
            });

        GameObject lostPanel = CreateOrRefreshPanel(
            canvas.transform,
            "LostPanel",
            lostTitle,
            windowSprite,
            buttonSprite,
            new[]
            {
                new PanelButton("RetryButton", "RETRY", hudManager != null ? hudManager.RestartGame : null),
                new PanelButton("MainMenuButton", "MAIN MENU", hudManager != null ? hudManager.ReturnToMainMenu : null)
            });

        pausePanel.SetActive(false);
        winPanel.SetActive(false);
        lostPanel.SetActive(false);

        if (hudManager != null)
        {
            SerializedObject serializedHud = new SerializedObject(hudManager);
            AssignObjectReference(serializedHud, "pausePanel", pausePanel);
            AssignObjectReference(serializedHud, "winPanel", winPanel);
            AssignObjectReference(serializedHud, "lostPanel", lostPanel);
            serializedHud.ApplyModifiedProperties();
            EditorUtility.SetDirty(hudManager);
        }

        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Selection.activeGameObject = pausePanel;
        Debug.Log("[PauseWinLostPanelBuilder] Created/updated PausePanel, WinPanel, LostPanel with project sprites.");
    }

    private static Sprite FindSprite(string spriteName)
    {
        string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
            }
        }

        string[] fallbackGuids = AssetDatabase.FindAssets(spriteName);
        foreach (string guid in fallbackGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in assets)
            {
                if (asset is Sprite sprite && (sprite.name == spriteName || sprite.name.StartsWith(spriteName + "_")))
                    return sprite;
            }
        }

        Debug.LogWarning($"[PauseWinLostPanelBuilder] Sprite '{spriteName}' not found. Fallback UI will be used.");
        return null;
    }

    private static Canvas FindOrCreateCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null)
            return canvas;

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create EventSystem");
    }

    private static GameObject CreateOrRefreshPanel(
        Transform parent,
        string panelName,
        Sprite titleSprite,
        Sprite windowSprite,
        Sprite buttonSprite,
        PanelButton[] buttons)
    {
        Transform existingPanel = parent.Find(panelName);
        GameObject panelObject = existingPanel != null
            ? existingPanel.gameObject
            : new GameObject(panelName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        if (existingPanel == null)
        {
            Undo.RegisterCreatedObjectUndo(panelObject, $"Create {panelName}");
            panelObject.transform.SetParent(parent, false);
        }

        panelObject.transform.SetAsLastSibling();

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        Stretch(panelRect);

        Image overlayImage = panelObject.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.62f);
        overlayImage.raycastTarget = true;

        ClearChildren(panelObject.transform);

        GameObject window = CreateUIObject("Window", panelObject.transform, typeof(Image), typeof(VerticalLayoutGroup));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(620f, 660f);

        Image windowImage = window.GetComponent<Image>();
        windowImage.sprite = windowSprite;
        windowImage.type = ShouldUseSlicedImage(windowSprite) ? Image.Type.Sliced : Image.Type.Simple;
        windowImage.color = windowSprite != null ? Color.white : new Color(0.12f, 0.09f, 0.07f, 0.96f);
        windowImage.raycastTarget = true;

        VerticalLayoutGroup layout = window.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(70, 70, 62, 62);
        layout.spacing = 28f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        CreateTitleImage("TitleImage", window.transform, titleSprite, panelName);
        CreateFlexibleSpace("TopSpace", window.transform, 110f);

        GameObject buttonGroup = CreateUIObject("ButtonGroup", window.transform, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        HorizontalLayoutGroup buttonLayout = buttonGroup.GetComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 22f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = false;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;

        LayoutElement buttonGroupLayout = buttonGroup.GetComponent<LayoutElement>();
        buttonGroupLayout.preferredHeight = 96f;

        foreach (PanelButton button in buttons)
        {
            CreateButton(buttonGroup.transform, button, buttonSprite);
        }

        return panelObject;
    }

    private static void CreateTitleImage(string objectName, Transform parent, Sprite sprite, string fallbackTitle)
    {
        GameObject titleObject = CreateUIObject(objectName, parent, typeof(Image), typeof(LayoutElement));
        Image titleImage = titleObject.GetComponent<Image>();
        titleImage.sprite = sprite;
        titleImage.color = sprite != null ? Color.white : Color.clear;
        titleImage.preserveAspect = true;
        titleImage.raycastTarget = false;

        LayoutElement layoutElement = titleObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 460f;
        layoutElement.preferredHeight = 135f;

        if (sprite != null)
            return;

        TextMeshProUGUI fallbackText = titleObject.AddComponent<TextMeshProUGUI>();
        fallbackText.text = fallbackTitle.Replace("Panel", "").ToUpperInvariant();
        fallbackText.fontSize = 54f;
        fallbackText.fontStyle = FontStyles.Bold;
        fallbackText.alignment = TextAlignmentOptions.Center;
        fallbackText.color = new Color(1f, 0.84f, 0.24f, 1f);
        fallbackText.raycastTarget = false;
    }

    private static void CreateFlexibleSpace(string objectName, Transform parent, float preferredHeight)
    {
        GameObject spaceObject = CreateUIObject(objectName, parent, typeof(LayoutElement));
        LayoutElement layoutElement = spaceObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.flexibleHeight = 1f;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string text, float fontSize, Color color, FontStyles style, float preferredHeight)
    {
        GameObject textObject = CreateUIObject(objectName, parent, typeof(TextMeshProUGUI));
        TextMeshProUGUI textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.fontStyle = style;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.raycastTarget = false;

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;

        return textComponent;
    }

    private static Button CreateButton(Transform parent, PanelButton spec, Sprite buttonSprite)
    {
        GameObject buttonObject = CreateUIObject(spec.ObjectName, parent, typeof(Image), typeof(Button), typeof(LayoutElement));

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = buttonSprite;
        image.type = ShouldUseSlicedImage(buttonSprite) ? Image.Type.Sliced : Image.Type.Simple;
        image.color = buttonSprite != null ? Color.white : new Color(0.36f, 0.24f, 0.13f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();

        if (spec.Action != null)
        {
            UnityEventTools.AddPersistentListener(button.onClick, spec.Action);
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = 220f;
        layoutElement.preferredHeight = 72f;

        CreateText("Text", buttonObject.transform, spec.Label, 24f, Color.white, FontStyles.Bold, 72f);
        RectTransform labelRect = buttonObject.transform.Find("Text").GetComponent<RectTransform>();
        Stretch(labelRect);

        return button;
    }

    private static GameObject CreateUIObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        foreach (System.Type component in components)
        {
            if (component != typeof(RectTransform))
                gameObject.AddComponent(component);
        }

        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static bool ShouldUseSlicedImage(Sprite sprite)
    {
        return sprite != null && sprite.border.sqrMagnitude > 0f;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static void AssignObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private readonly struct PanelButton
    {
        public readonly string ObjectName;
        public readonly string Label;
        public readonly UnityAction Action;

        public PanelButton(string objectName, string label, UnityAction action)
        {
            ObjectName = objectName;
            Label = label;
            Action = action;
        }
    }
}
