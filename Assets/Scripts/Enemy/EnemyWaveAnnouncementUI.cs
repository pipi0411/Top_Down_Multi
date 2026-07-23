using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyWaveAnnouncementUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private float showDuration = 5f;
    [SerializeField] private float fadeDuration = 0.35f;

    private static EnemyWaveAnnouncementUI instance;
    private Coroutine showRoutine;
    private CanvasGroup canvasGroup;

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
        if (waveText != null) waveText.gameObject.SetActive(false);
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
        instance.canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        instance.waveText.gameObject.SetActive(false);
        DontDestroyOnLoad(canvasObject);
    }

    private void ShowInternal(string message)
    {
        if (waveText == null) return;

        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRoutine(message));
    }

    private IEnumerator ShowRoutine(string message)
    {
        waveText.text = message;
        waveText.color = Color.red;
        waveText.gameObject.SetActive(true);
        canvasGroup.alpha = 1f;

        yield return new WaitForSeconds(showDuration);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        waveText.gameObject.SetActive(false);
        showRoutine = null;
    }
}
