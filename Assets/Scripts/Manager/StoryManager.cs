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

    [Header("Scene Flow")]
    [SerializeField] private string endGameSceneName = "EndGame";
    [SerializeField] private string mainMenuSceneName = "Main Manager";

    private int currentPage = 0;
    private bool isTyping = false;
    private bool isTransitioning = false;
    private bool hasEnded = false;

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
        if (hasEnded)
            return;

        hasEnded = true;
        StartCoroutine(EndStoryWithFade());
    }

    IEnumerator EndStoryWithFade()
    {
        isTransitioning = true;
        yield return StartCoroutine(Fade(0f, 1f)); // Fade ra đen
        if (IsEndGameScene())
        {
            ReturnAfterEndGame();
            yield break;
        }

        if (GameManager.Instance != null &&
            GameManager.Instance.IsMultiplayer &&
            !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode) &&
            RoomClient.Instance != null)
        {
            if (GameManager.Instance.IsHost)
            {
                bool completed = false;
                bool success = false;

                void Handler(RoomClient.RoomResult result)
                {
                    completed = true;
                    success = result != null && result.success;
                }

                Debug.Log("[StoryManager] Host intro finished. Publishing room playing state before gameplay load.");
                RoomClient.Instance.OnStartRoomComplete += Handler;
                RoomClient.Instance.StartRoom(GameManager.Instance.CurrentRoomCode, "playing");

                float deadline = Time.unscaledTime + 5f;
                while (!completed && Time.unscaledTime < deadline)
                    yield return null;

                RoomClient.Instance.OnStartRoomComplete -= Handler;

                if (!success)
                    Debug.LogWarning("[StoryManager] StartRoom did not confirm before gameplay load. Continuing as host because Relay was already prepared.");
            }
            else
            {
                yield return WaitForHostGameplayReady();
            }
        }

        GameSceneLoader.LoadPendingGameplaySceneOrDefault("SampleScene");
    }

    private bool IsEndGameScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, endGameSceneName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ReturnAfterEndGame()
    {
        Time.timeScale = 1f;
        bool isMultiplayer = GameManager.Instance != null && GameManager.Instance.IsMultiplayer;

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            Unity.Netcode.NetworkManager.Singleton.Shutdown();

        if (NetworkButtons.Instance != null)
            NetworkButtons.Instance.ResetNetworkStartupState();

        if (GameManager.Instance != null)
        {
            if (isMultiplayer)
            {
                GameManager.Instance.SetMultiplayerMode(true);
                GameManager.Instance.SetSuppressRoomGameplayAutoLoad(true);
                GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);
            }
            else
            {
                GameManager.Instance.ClearCurrentRoom();
                GameManager.Instance.SetMultiplayerMode(false);
                GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
                GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    IEnumerator WaitForHostGameplayReady()
    {
        Debug.Log("[StoryManager] Player intro finished. Waiting for host to enter gameplay...");

        while (GameManager.Instance != null &&
               GameManager.Instance.IsMultiplayer &&
               !GameManager.Instance.IsHost &&
               !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode) &&
               RoomClient.Instance != null)
        {
            bool completed = false;
            bool shouldContinue = false;
            bool roomClosed = false;

            void Handler(RoomClient.RoomDetailsResult result)
            {
                completed = true;

                if (result == null || !result.success || result.room == null)
                {
                    string error = result != null ? result.error : string.Empty;
                    roomClosed = !string.IsNullOrEmpty(error) && error.ToLowerInvariant().Contains("room not found");
                    return;
                }

                if (!string.IsNullOrEmpty(result.room.relayJoinCode))
                    GameManager.Instance.SetRelayJoinCode(result.room.relayJoinCode, result.room.roomCode);

                shouldContinue = string.Equals(result.room.status, "playing", System.StringComparison.OrdinalIgnoreCase);
            }

            RoomClient.Instance.OnGetRoomDetailsComplete += Handler;
            RoomClient.Instance.GetRoomDetails(GameManager.Instance.CurrentRoomCode);

            float deadline = Time.unscaledTime + 1.5f;
            while (!completed && Time.unscaledTime < deadline)
                yield return null;

            RoomClient.Instance.OnGetRoomDetailsComplete -= Handler;

            if (shouldContinue)
                yield break;

            if (roomClosed)
            {
                GameManager.Instance.ClearCurrentRoom();
                GameManager.Instance.SetMultiplayerMode(false);
                GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
                SceneManager.LoadScene("Main Manager");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }
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
