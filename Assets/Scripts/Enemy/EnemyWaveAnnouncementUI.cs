using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWaveAnnouncementUI : MonoBehaviour
{
    private const float ShowDurationSeconds = 1f;

    [SerializeField] private TextMeshProUGUI waveText;

    private static EnemyWaveAnnouncementUI instance;
    private CanvasGroup canvasGroup;
    private string currentMessage;
    private float hideAtTime;
    private bool isVisible;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        HideImmediate();
    }

    private void Update()
    {
        if (isVisible && Time.unscaledTime >= hideAtTime)
            HideImmediate();
    }

    public static void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        EnsureInstance();
        instance.ShowInternal(message);
    }

    private static void EnsureInstance()
    {
        if (instance != null) return;

        GameObject canvasObject = new GameObject("EnemyWaveAnnouncementCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        instance = canvasObject.AddComponent<EnemyWaveAnnouncementUI>();
        GameObject textObject = new GameObject("WaveText");
        textObject.transform.SetParent(canvasObject.transform, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 54f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.red;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.72f);
        rect.anchorMax = new Vector2(0.5f, 0.72f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(900f, 120f);

        instance.waveText = text;
        instance.canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        if (instance.canvasGroup == null)
            instance.canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        instance.HideImmediate();
        DontDestroyOnLoad(canvasObject);
    }

    private void ShowInternal(string message)
    {
        if (waveText == null) return;

        if (isVisible && currentMessage == message)
            return;

        currentMessage = message;
        waveText.text = message;
        waveText.color = Color.red;
        waveText.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        hideAtTime = Time.unscaledTime + ShowDurationSeconds;
        isVisible = true;
    }

    private void HideImmediate()
    {
        isVisible = false;
        currentMessage = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (waveText != null)
            waveText.gameObject.SetActive(false);
    }
}
