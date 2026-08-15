using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomClosureWatcher : MonoBehaviour
{
    [SerializeField] private float pollInterval = 1.25f;
    [SerializeField] private string mainMenuSceneName = "Main Manager";

    private static RoomClosureWatcher instance;
    private float nextPollTime;
    private bool requestInFlight;

    public static RoomClosureWatcher Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<RoomClosureWatcher>();
                if (instance == null)
                {
                    GameObject go = new GameObject("RoomClosureWatcher");
                    instance = go.AddComponent<RoomClosureWatcher>();
                }
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
    }

    private void OnEnable()
    {
        if (RoomClient.Instance != null)
            RoomClient.Instance.OnGetRoomDetailsComplete += HandleRoomDetails;
    }

    private void OnDisable()
    {
        if (RoomClient.Instance != null)
            RoomClient.Instance.OnGetRoomDetailsComplete -= HandleRoomDetails;
    }

    private void Update()
    {
        if (!ShouldWatch())
            return;

        if (requestInFlight || Time.unscaledTime < nextPollTime)
            return;

        requestInFlight = true;
        nextPollTime = Time.unscaledTime + pollInterval;
        RoomClient.Instance.GetRoomDetails(GameManager.Instance.CurrentRoomCode);
    }

    private bool ShouldWatch()
    {
        if (GameManager.Instance == null || RoomClient.Instance == null)
            return false;

        if (!GameManager.Instance.IsMultiplayer || GameManager.Instance.IsHost)
            return false;

        if (string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
            return false;

        GameManager.GameState state = GameManager.Instance.CurrentState;
        return state == GameManager.GameState.RoomLobby ||
               state == GameManager.GameState.GameStarting ||
               state == GameManager.GameState.InGame;
    }

    private void HandleRoomDetails(RoomClient.RoomDetailsResult result)
    {
        if (!requestInFlight)
            return;

        requestInFlight = false;

        if (!ShouldWatch())
            return;

        if (result == null)
            return;

        if (!string.IsNullOrEmpty(result.requestedRoomCode) &&
            !string.Equals(result.requestedRoomCode, GameManager.Instance.CurrentRoomCode, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!result.success)
        {
            if (IsRoomClosedError(result.error))
                ForceReturnToMainMenu("room details failed: " + result.error);

            return;
        }

        if (result.room == null)
        {
            ForceReturnToMainMenu("room details returned null room");
            return;
        }

        if (string.Equals(result.room.status, "closed", System.StringComparison.OrdinalIgnoreCase))
            ForceReturnToMainMenu("room status is closed");
    }

    private bool IsRoomClosedError(string error)
    {
        if (string.IsNullOrEmpty(error))
            return false;

        string normalized = error.ToLowerInvariant();
        return normalized.Contains("room not found") ||
               normalized.Contains("already closed") ||
               normalized.Contains("not in this room");
    }

    private void ForceReturnToMainMenu(string reason)
    {
        Debug.LogWarning("[RoomClosureWatcher] Host room closed, returning client to main menu. Reason: " + reason);

        requestInFlight = false;
        nextPollTime = 0f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (NetworkButtons.Instance != null)
            NetworkButtons.Instance.ResetNetworkStartupState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
            GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        }

        if (SceneManager.GetActiveScene().name != mainMenuSceneName)
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
