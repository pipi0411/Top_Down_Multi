using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

[System.Serializable]
public class StoryPage
{
    public Sprite illustration;
    [TextArea(5, 12)]
    public string dialogueText;
}

public class StoryManager : MonoBehaviour
{
    [Header("UI References")]
    public Image illustrationImage;
    public TextMeshProUGUI dialogueText;
    public Button skipButton;
    public Image fadeImage;                 // ← Kéo FadeImage vào đây

    [Header("Danh sách các trang")]
    public List<StoryPage> storyPages = new List<StoryPage>();

    [Header("Cài đặt Fade")]
    public float fadeDuration = 0.6f;       // Thời gian fade (giây)

    private int currentPage = 0;
    private bool isTyping = false;
    private bool isTransitioning = false;

    void Start()
    {
        if (skipButton != null)
            skipButton.onClick.AddListener(SkipStory);

        // Bắt đầu bằng fade in
        StartCoroutine(Fade(1f, 0f)); // từ đen → trong suốt

        if (storyPages.Count > 0)
            ShowPage(0, false); // false = không fade lần đầu
    }

    void Update()
    {
        if (isTransitioning || isTyping) return;

        bool clicked = false;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            clicked = true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            clicked = true;

        if (clicked)
            NextPage();
    }

    void ShowPage(int index, bool withFade = true)
    {
        if (index >= storyPages.Count)
        {
            EndStory();
            return;
        }

        currentPage = index;

        if (withFade)
            StartCoroutine(ChangePageWithFade(index));
        else
            ApplyPage(index);
    }

    IEnumerator ChangePageWithFade(int index)
    {
        isTransitioning = true;

        // Fade ra đen
        yield return StartCoroutine(Fade(0f, 1f));

        // Đổi nội dung khi đang đen
        ApplyPage(index);

        // Fade trở lại
        yield return StartCoroutine(Fade(1f, 0f));

        isTransitioning = false;
    }

    void ApplyPage(int index)
    {
        StoryPage page = storyPages[index];

        if (illustrationImage != null && page.illustration != null)
            illustrationImage.sprite = page.illustration;

        StartCoroutine(TypewriterEffect(page.dialogueText));
    }

    IEnumerator TypewriterEffect(string text)
    {
        isTyping = true;
        dialogueText.text = "";

        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(0.035f);
        }
        isTyping = false;
    }

    public void NextPage()
    {
        ShowPage(currentPage + 1);
    }

    void SkipStory()
    {
        EndStory();
    }

    void EndStory()
    {
        StartCoroutine(EndStoryWithFade());
    }

    IEnumerator EndStoryWithFade()
    {
        isTransitioning = true;
        yield return StartCoroutine(Fade(0f, 1f)); // Fade ra đen
        GameSceneLoader.LoadPendingGameplaySceneOrDefault("SampleScene");
    }

    // Hàm Fade chung
    IEnumerator Fade(float from, float to)
    {
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, timer / fadeDuration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }

        color.a = to;
        fadeImage.color = color;
    }
}
