using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Auth,
        TokenValidating,
        MainMenu,
        ModeSelect,
        CharacterSelect,
        RoomLobby,
        GameStarting,
        InGame
    }

    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.Auth;
    public string CurrentRoomCode { get; private set; }
    public string CurrentRoomName { get; private set; }
    public string CurrentRelayJoinCode { get; private set; }
    public string CurrentRelayRoomCode { get; private set; }
    public bool IsHost { get; private set; }
    public string CurrentUsername { get; private set; }
    public bool IsMultiplayer { get; private set; } = false;
    public bool SuppressRoomGameplayAutoLoad { get; private set; } = false;
    public string SelectedCharacter { get; private set; }
    public string RoomSelectedCharacter { get; private set; }

    public event Action<GameState> OnStateChanged;
    public event Action<string> OnError;

    private void Awake()
    {
        // Kiểm tra xem đã có Instance nào tồn tại chưa
        if (Instance != null && Instance != this)
        {
            // [THAY ĐỔI Ở ĐÂY]: Xóa toàn bộ Main Manager mới tạo nếu quay về từ Scene Game
            transform.root.gameObject.SetActive(false);
            Destroy(transform.root.gameObject);
            return;
        }

        Instance = this;
        // [THAY ĐỔI Ở ĐÂY]: Giữ lại toàn bộ Main Manager (gồm GameManager, Canvas, EventSystem...)
        DontDestroyOnLoad(transform.root.gameObject);
        Debug.Log("=== GameManager Initialized ===");

        InitializeClients();

        string storedToken = AuthClient.Instance != null ? AuthClient.Instance.GetStoredToken() : string.Empty;
        string storedUsername = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUsername() : string.Empty;
        
        Debug.Log($"[Startup] Stored Token: {(string.IsNullOrEmpty(storedToken) ? "EMPTY" : "EXISTS")}");
        Debug.Log($"[Startup] Stored Username: {storedUsername}");

        if (string.IsNullOrEmpty(storedToken))
        {
            // No token - go to Auth (Login screen)
            Debug.Log("[Startup Flow] No token found. Going to Auth state (Login screen).");
            CurrentUsername = null;
            ChangeState(GameState.Auth);
        }
        else
        {
            // Token exists - validate it with server
            Debug.Log("[Startup Flow] Token found. Validating with server...");
            CurrentUsername = storedUsername;
            ChangeState(GameState.TokenValidating);
            
            AuthClient.Instance.ValidateToken(validation =>
            {
                Debug.Log($"[Token Validation] Result: {(validation != null && validation.isValid ? "VALID" : "INVALID")}");
                if (validation != null && validation.isValid)
                {
                    Debug.Log($"[Token Validation] ✓ Token is valid. Syncing profile and entering MainMenu for {CurrentUsername}");
                    FetchAndSyncProfile();
                    ChangeState(GameState.MainMenu);
                }
                else
                {
                    string validationError = validation != null ? validation.error : "Token validation failed.";
                    Debug.Log($"[Token Validation] ✗ {validationError} Returning to Auth state.");
                    CurrentUsername = null;
                    ChangeState(GameState.Auth);
                    if (!string.IsNullOrEmpty(validationError))
                    {
                        OnError?.Invoke(validationError);
                    }
                }
            });
        }
    }

    private void InitializeClients()
    {
        // Ensure clients exist
        if (AuthClient.Instance == null)
            Debug.LogWarning("AuthClient not initialized");

        if (RoomClient.Instance == null)
            Debug.LogWarning("RoomClient not initialized");

        // Ensure CharacterPrefabManager exists for character→prefab mapping
        if (CharacterPrefabManager.Instance == null)
            Debug.LogWarning("CharacterPrefabManager not initialized");

        // Ensure NetworkButtons exists for Netcode game startup
        if (NetworkButtons.Instance == null)
            Debug.LogWarning("NetworkButtons not initialized");

        // Keep watching room closure even after loading IntroStory/SampleScene.
        _ = RoomClosureWatcher.Instance;

        // Subscribe to events
        AuthClient.Instance.OnLoginComplete += HandleLoginComplete;
        RoomClient.Instance.OnCreateRoomComplete += HandleCreateRoomComplete;
        RoomClient.Instance.OnJoinRoomComplete += HandleJoinRoomComplete;
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState)
        {
            Debug.LogWarning($"[ChangeState] Already in state {newState}, ignoring.");
            return;
        }

        Debug.Log($"[ChangeState] {CurrentState} → {newState}");
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
    
    private void EnsureUIManagersActive()
    {
        // This is now handled by ActivateUIPanel
    }

    public void SetCurrentRoom(string roomCode, string roomName, bool isHost)
    {
        if (!string.Equals(CurrentRoomCode, roomCode, StringComparison.OrdinalIgnoreCase))
        {
            RoomSelectedCharacter = null;
            CurrentRelayJoinCode = null;
            CurrentRelayRoomCode = null;
            SuppressRoomGameplayAutoLoad = false;
            Debug.Log($"[GameManager] Room changed to '{roomCode}'. Cleared cached Relay join code.");
        }

        CurrentRoomCode = roomCode;
        CurrentRoomName = roomName;
        IsHost = isHost;
    }

    public void ClearCurrentRoom()
    {
        CurrentRoomCode = null;
        CurrentRoomName = null;
        CurrentRelayJoinCode = null;
        CurrentRelayRoomCode = null;
        IsHost = false;
        RoomSelectedCharacter = null;
        SuppressRoomGameplayAutoLoad = false;
    }

    public void SetRelayJoinCode(string joinCode)
    {
        SetRelayJoinCode(joinCode, CurrentRoomCode);
    }

    public void SetRelayJoinCode(string joinCode, string roomCode)
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            CurrentRelayJoinCode = null;
            CurrentRelayRoomCode = null;
            return;
        }

        string targetRoomCode = string.IsNullOrEmpty(roomCode) ? CurrentRoomCode : roomCode;
        if (!string.IsNullOrEmpty(CurrentRoomCode) &&
            !string.IsNullOrEmpty(targetRoomCode) &&
            !string.Equals(CurrentRoomCode, targetRoomCode, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"[GameManager] Ignored Relay join code for stale room '{targetRoomCode}'. Current room is '{CurrentRoomCode}'.");
            return;
        }

        CurrentRelayJoinCode = joinCode;
        CurrentRelayRoomCode = targetRoomCode;
    }

    public void SetMultiplayerMode(bool isMultiplayer)
    {
        IsMultiplayer = isMultiplayer;
    }

    public void SetSuppressRoomGameplayAutoLoad(bool suppress)
    {
        SuppressRoomGameplayAutoLoad = suppress;
    }

    public void SetSelectedCharacter(string character)
    {
        SelectedCharacter = character;
    }

    public void SetRoomSelectedCharacter(string character)
    {
        RoomSelectedCharacter = character;
    }

    private void HandleLoginComplete(AuthClient.AuthResult result)
    {
        Debug.Log($"[HandleLoginComplete] Success: {result.success}");
        
        if (result.success)
        {
            string storedToken = AuthClient.Instance.GetStoredToken();
            if (string.IsNullOrEmpty(storedToken))
            {
                Debug.LogError("[Login Flow] ✗ Login response succeeded but token was not saved locally.");
                OnError?.Invoke("Login failed to persist token. Please try again.");
                ChangeState(GameState.Auth);
                return;
            }

            CurrentUsername = AuthClient.Instance.GetStoredUsername();
            Debug.Log($"[Login Flow] ✓ Login successful for user: {CurrentUsername}");
            Debug.Log($"[Login Flow] → Syncing profile and changing to MainMenu state");
            FetchAndSyncProfile();
            ChangeState(GameState.MainMenu);
        }
        else
        {
            Debug.LogError($"[Login Flow] ✗ Login failed: {result.error}");
            OnError?.Invoke(result.error);
        }
    }

    private void HandleCreateRoomComplete(RoomClient.RoomResult result)
    {
        if (result.success)
        {
            SetCurrentRoom(result.room.roomCode, result.room.name, isHost: true);
            ChangeState(GameState.RoomLobby);
            Debug.Log($"Room created: {result.room.roomCode}");
        }
        else
        {
            OnError?.Invoke(result.error);
        }
    }

    private void HandleJoinRoomComplete(RoomClient.RoomResult result)
    {
        if (result.success)
        {
            SetCurrentRoom(result.room.roomCode, result.room.name, isHost: false);
            ChangeState(GameState.RoomLobby);
            Debug.Log($"Joined room: {result.room.roomCode}");
        }
        else
        {
            OnError?.Invoke(result.error);
        }
    }

    public void Logout()
    {
        Debug.Log("[Logout Flow] Starting logout...");
        AuthClient.Instance.Logout();
        ClearCurrentRoom();
        CurrentUsername = null;
        IsMultiplayer = false;
        SuppressRoomGameplayAutoLoad = false;
        SelectedCharacter = null;
        RoomSelectedCharacter = null;
        Debug.Log("[Logout Flow] ✓ Cleared all data, returning to Auth state (Login screen)");
        ChangeState(GameState.Auth);
    }

    public void FetchAndSyncProfile()
    {
        string userId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;
        if (string.IsNullOrEmpty(userId) || CharactersClient.Instance == null)
            return;

        Debug.Log($"[Profile Sync] Requesting profile for user ID: {userId}");
        CharactersClient.Instance.OnGetUserProfileComplete += HandleUserProfileComplete;
        CharactersClient.Instance.GetUserProfile(userId);
    }

    private void HandleUserProfileComplete(CharactersClient.UserProfileResult result)
    {
        if (CharactersClient.Instance != null)
        {
            CharactersClient.Instance.OnGetUserProfileComplete -= HandleUserProfileComplete;
        }

        if (result.success)
        {
            Debug.Log($"[Profile Sync] ✓ Profile synchronized. Selected character from Database: {result.selectedCharacter}");
            SelectedCharacter = result.selectedCharacter;
        }
        else
        {
            Debug.LogError($"[Profile Sync] ✗ Failed to sync profile: {result.error}");
        }
    }

    private void OnDestroy()
    {
        if (AuthClient.Instance != null)
            AuthClient.Instance.OnLoginComplete -= HandleLoginComplete;

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnCreateRoomComplete -= HandleCreateRoomComplete;
            RoomClient.Instance.OnJoinRoomComplete -= HandleJoinRoomComplete;
        }
    }
}
