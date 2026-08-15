using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PortalMapLoadingUI : MonoBehaviour
{
    private const string LoadingSpriteResourcePath = "UI/loadnextmap";

    private static PortalMapLoadingUI instance;
    private Coroutine transitionRoutine;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Image backgroundImage;
    private TextMeshProUGUI loadingText;

    public static PortalMapLoadingUI Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("PortalMapLoadingUI");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PortalMapLoadingUI>();
                instance.BuildUI();
                instance.HideImmediate();
            }

            return instance;
        }
    }

    private void Awake()
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

    public IEnumerator ShowTransition(float beforeSwitchSeconds, System.Func<bool> switchAction, float afterSwitchSeconds)
    {
        yield return RunTransition(beforeSwitchSeconds, switchAction, afterSwitchSeconds, null);
    }

    public void PlayTransition(
        float beforeSwitchSeconds,
        System.Func<bool> switchAction,
        float afterSwitchSeconds,
        System.Action<bool> completed = null)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(RunTransition(beforeSwitchSeconds, switchAction, afterSwitchSeconds, completed));
    }

    private IEnumerator RunTransition(
        float beforeSwitchSeconds,
        System.Func<bool> switchAction,
        float afterSwitchSeconds,
        System.Action<bool> completed)
    {
        Show();
        bool switched = false;

        try
        {
            float before = Mathf.Max(0f, beforeSwitchSeconds);
            if (before > 0f)
                yield return new WaitForSecondsRealtime(before);

            switched = switchAction != null && switchAction.Invoke();

            float after = Mathf.Max(0f, afterSwitchSeconds);
            if (after > 0f)
                yield return new WaitForSecondsRealtime(after);
        }
        finally
        {
            HideImmediate();
            transitionRoutine = null;
            completed?.Invoke(switched);
        }

        if (!switched)
            Debug.LogWarning("[PortalMapLoadingUI] Portal transition finished but map switch failed.");
    }

    private void BuildUI()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("LoadNextMapImage");
        imageObject.transform.SetParent(canvasObject.transform, false);
        backgroundImage = imageObject.AddComponent<Image>();
        backgroundImage.color = Color.white;
        backgroundImage.sprite = LoadLoadingSprite();
        backgroundImage.preserveAspect = true;
        backgroundImage.raycastTarget = true;

        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        GameObject textObject = new GameObject("LoadingText");
        textObject.transform.SetParent(canvasObject.transform, false);
        loadingText = textObject.AddComponent<TextMeshProUGUI>();
        loadingText.text = "Loading next map...";
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.fontSize = 34f;
        loadingText.color = Color.white;
        loadingText.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0f);
        textRect.anchorMax = new Vector2(0.5f, 0f);
        textRect.pivot = new Vector2(0.5f, 0f);
        textRect.sizeDelta = new Vector2(900f, 90f);
        textRect.anchoredPosition = new Vector2(0f, 62f);
    }

    private Sprite LoadLoadingSprite()
    {
        Sprite sprite = Resources.Load<Sprite>(LoadingSpriteResourcePath);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(LoadingSpriteResourcePath);
        if (texture == null)
        {
            Debug.LogWarning("[PortalMapLoadingUI] Cannot find Resources/UI/loadnextmap image.");
            return null;
        }

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private void Show()
    {
        BuildUI();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        canvas.gameObject.SetActive(true);
    }

    private void HideImmediate()
    {
        if (canvasGroup == null || canvas == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvas.gameObject.SetActive(false);
    }
}
