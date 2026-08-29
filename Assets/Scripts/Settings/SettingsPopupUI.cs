using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPopupUI : MonoBehaviour
{
    static SettingsPopupUI instance;

    Slider musicSlider;
    Slider sfxSlider;
    Slider mouseSlider;
    TMP_Text musicValueText;
    TMP_Text sfxValueText;
    TMP_Text mouseValueText;

    public static void Show()
    {
        EnsureInstance().Open();
    }

    static SettingsPopupUI EnsureInstance()
    {
        if (instance != null) return instance;

        GameObject root = new GameObject("SettingsPopup");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SettingsPopupUI>();
        instance.BuildUI();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Open()
    {
        gameObject.SetActive(true);
        RefreshValues();
    }

    void Close()
    {
        gameObject.SetActive(false);
    }

    void RefreshValues()
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
        if (mouseSlider != null) mouseSlider.SetValueWithoutNotify(GameSettings.MouseSensitivity);
        RefreshValueTexts();
    }

    void RefreshValueTexts()
    {
        if (musicValueText != null)
            musicValueText.text = $"{Mathf.RoundToInt(GameSettings.MusicVolume * 100f)}%";
        if (sfxValueText != null)
            sfxValueText.text = $"{Mathf.RoundToInt(GameSettings.SfxVolume * 100f)}%";
        if (mouseValueText != null)
            mouseValueText.text = $"{GameSettings.MouseSensitivity:0.00}x";
    }

    void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.62f);
        RectTransform rootRect = gameObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject panel = CreatePanel(transform, "Panel", new Vector2(780f, 730f), new Color(0.025f, 0.055f, 0.095f, 0.97f));
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(1f, 0.72f, 0.22f, 0.9f);
        panelOutline.effectDistance = new Vector2(3f, -3f);

        CreateFixedText(panel.transform, "Title", "SETTINGS", 52, Color.white, TextAlignmentOptions.Center, new Vector2(0f, 305f), new Vector2(700f, 70f));

        CreateSliderRowFixed(panel.transform, "Music Volume", GameSettings.MusicVolume, 0f, 1f, new Vector2(0f, 220f), out musicSlider, out musicValueText,
            value =>
            {
                GameSettings.SetMusicVolume(value);
                RefreshValueTexts();
            });

        CreateSliderRowFixed(panel.transform, "SFX Volume", GameSettings.SfxVolume, 0f, 1f, new Vector2(0f, 145f), out sfxSlider, out sfxValueText,
            value =>
            {
                GameSettings.SetSfxVolume(value);
                RefreshValueTexts();
            });

        CreateSliderRowFixed(panel.transform, "Mouse Speed", GameSettings.MouseSensitivity, 0.35f, 2.5f, new Vector2(0f, 70f), out mouseSlider, out mouseValueText,
            value =>
            {
                GameSettings.SetMouseSensitivity(value);
                RefreshValueTexts();
            });

        string guide =
            "WASD: Move\n" +
            "Mouse: Aim\n" +
            "Left Click: Shoot / Attack\n" +
            "1 - 2: Switch weapon\n" +
            "R: Reload gun\n" +
            "G: Drop current weapon\n" +
            "E: Open chest / interact\n" +
            "ESC: Pause game";
        GameObject guidePanel = CreatePanel(panel.transform, "GuidePanel", new Vector2(650f, 255f), new Color(0.015f, 0.08f, 0.16f, 0.86f));
        SetRect(guidePanel.GetComponent<RectTransform>(), new Vector2(0f, -125f), new Vector2(650f, 255f));
        Outline guideOutline = guidePanel.AddComponent<Outline>();
        guideOutline.effectColor = new Color(0.22f, 0.42f, 0.72f, 0.9f);
        guideOutline.effectDistance = new Vector2(2f, -2f);
        CreateFixedText(guidePanel.transform, "GuideTitle", "HOW TO PLAY", 28, new Color(1f, 0.86f, 0.32f, 1f), TextAlignmentOptions.Left, new Vector2(0f, 100f), new Vector2(585f, 40f));
        CreateFixedText(guidePanel.transform, "GuideText", guide, 23, new Color(0.94f, 0.97f, 1f, 1f), TextAlignmentOptions.Left, new Vector2(0f, -22f), new Vector2(585f, 185f));

        Button closeButton = CreateButton(panel.transform, "CloseButton", "CLOSE", new Vector2(250f, 56f));
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(0f, -320f), new Vector2(280f, 58f));
        closeButton.onClick.AddListener(Close);

        gameObject.SetActive(false);
    }

    void CreateSliderRowFixed(Transform parent, string label, float value, float minValue, float maxValue, Vector2 center, out Slider slider, out TMP_Text valueText, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject(label.Replace(" ", "") + "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetRect(row.GetComponent<RectTransform>(), center, new Vector2(650f, 58f));

        CreateFixedText(row.transform, label + "Label", label, 25, Color.white, TextAlignmentOptions.Left, new Vector2(-218f, 0f), new Vector2(205f, 46f));

        slider = CreateSlider(row.transform, value, minValue, maxValue);
        SetRect(slider.GetComponent<RectTransform>(), new Vector2(55f, 0f), new Vector2(300f, 28f));
        slider.onValueChanged.AddListener(onChanged);

        valueText = CreateFixedText(row.transform, label + "Value", "", 24, new Color(1f, 0.86f, 0.32f, 1f), TextAlignmentOptions.Right, new Vector2(275f, 0f), new Vector2(120f, 46f));
    }

    TMP_Text CreateFixedText(Transform parent, string objectName, string text, int fontSize, Color color, TextAlignmentOptions alignment, Vector2 center, Vector2 size)
    {
        TMP_Text label = CreateText(parent, objectName, text, fontSize, color, alignment, size);
        RectTransform rect = label.GetComponent<RectTransform>();
        SetRect(rect, center, size);
        LayoutElement layout = label.GetComponent<LayoutElement>();
        if (layout != null)
            Destroy(layout);
        return label;
    }

    void SetRect(RectTransform rect, Vector2 center, Vector2 size)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
    }

    GameObject CreatePanel(Transform parent, string objectName, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(parent, false);
        Image image = panel.AddComponent<Image>();
        image.color = color;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return panel;
    }

    TMP_Text CreateText(Transform parent, string objectName, string text, int fontSize, Color color, TextAlignmentOptions alignment, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        TMP_Text label = textObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;

        LayoutElement layout = textObject.AddComponent<LayoutElement>();
        layout.preferredHeight = size.y;

        return label;
    }

    void CreateSliderRow(Transform parent, string label, float value, float minValue, float maxValue, out Slider slider, out TMP_Text valueText, UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new GameObject(label.Replace(" ", "") + "Row");
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = true;

        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 54f;

        TMP_Text labelText = CreateText(row.transform, label + "Label", label, 23, Color.white, TextAlignmentOptions.Left, new Vector2(160f, 54f));
        LayoutElement labelLayout = labelText.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 180f;

        slider = CreateSlider(row.transform, value, minValue, maxValue);
        slider.onValueChanged.AddListener(onChanged);

        valueText = CreateText(row.transform, label + "Value", "", 22, new Color(1f, 0.86f, 0.32f, 1f), TextAlignmentOptions.Right, new Vector2(80f, 54f));
        LayoutElement valueLayout = valueText.GetComponent<LayoutElement>();
        valueLayout.preferredWidth = 80f;
    }

    Slider CreateSlider(Transform parent, float value, float minValue, float maxValue)
    {
        GameObject sliderObject = new GameObject("Slider");
        sliderObject.transform.SetParent(parent, false);
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.direction = Slider.Direction.LeftToRight;

        LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
        layout.preferredWidth = 260f;
        layout.preferredHeight = 28f;

        Image background = sliderObject.AddComponent<Image>();
        background.color = new Color(0.07f, 0.08f, 0.11f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5f, 5f);
        fillAreaRect.offsetMax = new Vector2(-5f, -5f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(1f, 0.75f, 0.16f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(26f, 38f);
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;
        return slider;
    }

    Button CreateButton(Transform parent, string objectName, string text, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.65f, 0.24f, 0.1f, 1f);
        Button button = buttonObject.AddComponent<Button>();

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = size.x;
        layout.preferredHeight = size.y;

        TMP_Text label = CreateText(buttonObject.transform, "Text", text, 24, Color.white, TextAlignmentOptions.Center, size);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }
}
