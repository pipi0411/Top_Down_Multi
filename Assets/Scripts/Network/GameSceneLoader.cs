using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSceneLoader : MonoBehaviour
{
    const float MinLoadingVisibleTime = 0.65f;
    const float ReadyWaitTimeout = 8f;
    const string DefaultIntroSceneName = "IntroStory";
    const string DefaultGameplaySceneName = "SampleScene";

    static GameSceneLoader instance;
    static string pendingGameplaySceneName;
    static bool pendingGameplayWaitForReady;

    Canvas canvas;
    CanvasGroup canvasGroup;
    Slider progressSlider;
    TextMeshProUGUI loadingText;
    TextMeshProUGUI progressText;
    bool isLoading;

    public static void LoadGameplayScene(string sceneName)
    {
        EnsureInstance().StartLoad(sceneName, true);
    }

    public static void LoadGameplaySceneAfterIntro(string sceneName, string introSceneName = DefaultIntroSceneName)
    {
        pendingGameplaySceneName = string.IsNullOrEmpty(sceneName) ? DefaultGameplaySceneName : sceneName;
        pendingGameplayWaitForReady = true;
        EnsureInstance().StartIntro(introSceneName);
    }

    public static void LoadPendingGameplaySceneOrDefault(string fallbackSceneName = DefaultGameplaySceneName)
    {
        string sceneName = string.IsNullOrEmpty(pendingGameplaySceneName) ? fallbackSceneName : pendingGameplaySceneName;
        bool waitForReady = string.IsNullOrEmpty(pendingGameplaySceneName) || pendingGameplayWaitForReady;

        pendingGameplaySceneName = null;
        pendingGameplayWaitForReady = false;

        EnsureInstance().StartLoad(sceneName, waitForReady);
    }

    public static void LoadScene(string sceneName)
    {
        EnsureInstance().StartLoad(sceneName, false);
    }

    static GameSceneLoader EnsureInstance()
    {
        if (instance != null) return instance;

        GameObject loaderObject = new GameObject("GameSceneLoader");
        DontDestroyOnLoad(loaderObject);
        instance = loaderObject.AddComponent<GameSceneLoader>();
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
        BuildUI();
        HideImmediate();
    }

    void StartLoad(string sceneName, bool waitForGameplayReady)
    {
        if (isLoading || string.IsNullOrEmpty(sceneName)) return;
        StartCoroutine(LoadSceneRoutine(sceneName, waitForGameplayReady));
    }

    void StartIntro(string introSceneName)
    {
        if (isLoading) return;

        if (string.IsNullOrEmpty(introSceneName))
        {
            LoadPendingGameplaySceneOrDefault();
            return;
        }

        SceneManager.LoadScene(introSceneName);
    }

    IEnumerator LoadSceneRoutine(string sceneName, bool waitForGameplayReady)
    {
        isLoading = true;
        Show("Đang tải màn chơi...");
        float shownAt = Time.unscaledTime;

        yield return null;

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            HideImmediate();
            isLoading = false;
            yield break;
        }

        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            SetProgress(Mathf.Clamp01(operation.progress / 0.9f), "Đang tải map...");
            yield return null;
        }

        SetProgress(0.95f, "Chuẩn bị nhân vật...");

        while (Time.unscaledTime - shownAt < MinLoadingVisibleTime)
            yield return null;

        operation.allowSceneActivation = true;

        while (!operation.isDone)
            yield return null;

        if (waitForGameplayReady)
            yield return WaitForGameplayReady();

        SetProgress(1f, "Hoàn tất!");
        yield return new WaitForSecondsRealtime(0.15f);

        HideImmediate();
        isLoading = false;
    }

    IEnumerator WaitForGameplayReady()
    {
        float deadline = Time.unscaledTime + ReadyWaitTimeout;

        while (Time.unscaledTime < deadline)
        {
            bool hasPlayer = FindAnyObjectByType<PlayerHealth>() != null;
            bool hasCamera = Camera.main != null;
            if (hasPlayer && hasCamera)
                yield break;

            SetProgress(0.98f, hasPlayer ? "Đang gắn camera..." : "Đang tạo nhân vật...");
            yield return null;
        }
    }

    void BuildUI()
    {
        if (canvas != null) return;

        GameObject canvasObject = new GameObject("LoadingCanvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObject.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.018f, 0.025f, 0.96f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        loadingText = CreateText(panel.transform, "LoadingText", "Đang tải...", 42, TextAlignmentOptions.Center);
        RectTransform loadingRect = loadingText.GetComponent<RectTransform>();
        loadingRect.anchorMin = new Vector2(0.5f, 0.5f);
        loadingRect.anchorMax = new Vector2(0.5f, 0.5f);
        loadingRect.sizeDelta = new Vector2(900f, 90f);
        loadingRect.anchoredPosition = new Vector2(0f, 70f);

        GameObject sliderObject = new GameObject("ProgressBar");
        sliderObject.transform.SetParent(panel.transform, false);
        progressSlider = sliderObject.AddComponent<Slider>();
        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.value = 0f;
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(560f, 28f);
        sliderRect.anchoredPosition = new Vector2(0f, 0f);

        Image sliderBackground = sliderObject.AddComponent<Image>();
        sliderBackground.color = new Color(0.16f, 0.14f, 0.18f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(4f, 4f);
        fillAreaRect.offsetMax = new Vector2(-4f, -4f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.95f, 0.72f, 0.22f, 1f);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        progressSlider.fillRect = fillRect;
        progressSlider.targetGraphic = fillImage;

        progressText = CreateText(panel.transform, "ProgressText", "0%", 24, TextAlignmentOptions.Center);
        RectTransform progressRect = progressText.GetComponent<RectTransform>();
        progressRect.anchorMin = new Vector2(0.5f, 0.5f);
        progressRect.anchorMax = new Vector2(0.5f, 0.5f);
        progressRect.sizeDelta = new Vector2(400f, 50f);
        progressRect.anchoredPosition = new Vector2(0f, -48f);
    }

    TextMeshProUGUI CreateText(Transform parent, string objectName, string value, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    void Show(string message)
    {
        BuildUI();
        canvas.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        SetProgress(0f, message);
    }

    void SetProgress(float progress, string message)
    {
        if (progressSlider != null)
            progressSlider.value = Mathf.Clamp01(progress);
        if (loadingText != null)
            loadingText.text = message;
        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
    }

    void HideImmediate()
    {
        if (canvas == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvas.gameObject.SetActive(false);
    }
}
