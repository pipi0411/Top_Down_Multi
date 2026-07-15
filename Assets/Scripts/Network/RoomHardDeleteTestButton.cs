using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomHardDeleteTestButton : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "Main Manager";

    private void OnEnable()
    {
        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnCloseRoomComplete += HandleCloseRoomComplete;
            RoomClient.Instance.OnLeaveRoomComplete += HandleLeaveRoomComplete;
        }
    }

    private void OnDisable()
    {
        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnCloseRoomComplete -= HandleCloseRoomComplete;
            RoomClient.Instance.OnLeaveRoomComplete -= HandleLeaveRoomComplete;
        }
    }

    public void DeleteRoomHard()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager is null");
            return;
        }

        if (RoomClient.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            Debug.LogWarning("Missing room client or room code");
            return;
        }

        if (!GameManager.Instance.IsHost)
        {
            RoomClient.Instance.LeaveRoom(GameManager.Instance.CurrentRoomCode);
            return;
        }

        RoomClient.Instance.CloseRoom(GameManager.Instance.CurrentRoomCode);
    }

    public void BackToRoomLobby()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("GameManager is null");
            return;
        }

        if (string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            Debug.LogWarning("Cannot return to room lobby because current room code is empty");
            ReturnToMainMenu();
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        GameManager.Instance.SetMultiplayerMode(true);
        GameManager.Instance.SetSuppressRoomGameplayAutoLoad(true);
        GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);

        if (!string.IsNullOrEmpty(lobbySceneName) && SceneManager.GetActiveScene().name != lobbySceneName)
        {
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    private void HandleCloseRoomComplete(RoomClient.RoomResult result)
    {
        if (result.success)
        {
            Debug.Log("Room deleted hard from API");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ClearCurrentRoom();
                GameManager.Instance.SetMultiplayerMode(false);
                GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
                GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
            }
        }
        else
        {
            Debug.LogError("Delete room failed: " + result.error);
        }
    }

    private void HandleLeaveRoomComplete(RoomClient.RoomResult result)
    {
        if (!result.success)
        {
            Debug.LogError("Leave room failed: " + result.error);
            return;
        }

        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
            GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        }
    }
}
