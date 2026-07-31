using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Lobbies.Models;

[System.Serializable]
public class CharacterDisplayData
{
    public string characterName;
    public Sprite characterIcon;
}

public class RoomUIManager : MonoBehaviour
{
    [SerializeField] private GameObject createRoomPanel;
    [SerializeField] private GameObject joinRoomPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("Create/Join Panels")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_Text createRoomStatusText;
    [SerializeField] private Button switchToJoinButton;

    [SerializeField] private TMP_InputField joinRoomCodeInput;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private TMP_Text joinRoomStatusText;
    [SerializeField] private Button switchToCreateButton;
    [SerializeField] private Button backFromCreateRoomButton;
    [SerializeField] private Button backFromJoinRoomButton;

    [Header("Room Info Panel")]
    [SerializeField] private TMP_Text roomNameDisplay;
    [SerializeField] private TMP_Text roomCodeDisplay;
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerListItemPrefab;
    
    [SerializeField] private TMP_Text yourCharacterDisplay;
    [SerializeField] private Image yourCharacterImage;
    [SerializeField] private List<CharacterDisplayData> characterIcons = new List<CharacterDisplayData>();
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;

    [SerializeField] private string gameplaySceneName = "SampleScene";
    [SerializeField] private float roomPlayersRefreshInterval = 2f;
    [SerializeField] private float roomDetailsRefreshInterval = 2f;
    [SerializeField] private float roomHeartbeatInterval = 10f;

    private bool isPlayerReady = false;
    private List<GameObject> playerListItems = new List<GameObject>();
    private RoomClient.PlayerInfo[] currentRoomPlayers = new RoomClient.PlayerInfo[0];
    private bool roomPlayersRequestInFlight = false;
    private float nextRoomPlayersRefreshTime = 0f;
    private float nextRoomDetailsRefreshTime = 0f;
    private float nextRoomHeartbeatTime = 0f;
    private bool gameplayLoadRequested = false;

    private void OnEnable()
    {
        if (createRoomPanel == null || joinRoomPanel == null || roomPanel == null)
        {
            Debug.LogError("RoomUIManager is missing required panel references in the Inspector.");
            return;
        }

        createRoomButton = createRoomButton != null ? createRoomButton : FindButton(createRoomPanel, "create");
        joinRoomButton = joinRoomButton != null ? joinRoomButton : FindButton(joinRoomPanel, "join");
        startGameButton = startGameButton != null ? startGameButton : FindButton(roomPanel, "start");
        leaveRoomButton = leaveRoomButton != null ? leaveRoomButton : FindButton(roomPanel, "leave");
        backFromCreateRoomButton = backFromCreateRoomButton != null ? backFromCreateRoomButton : FindButton(createRoomPanel, "back");
        backFromJoinRoomButton = backFromJoinRoomButton != null ? backFromJoinRoomButton : FindButton(joinRoomPanel, "back");

        // Remove before adding to avoid duplicate subscriptions if OnEnable runs more than once.
        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        }
        if (switchToJoinButton != null)
        {
            switchToJoinButton.onClick.RemoveListener(OnBackToModeSelect);
            switchToJoinButton.onClick.AddListener(OnBackToModeSelect);
        }
        if (backFromCreateRoomButton != null)
        {
            backFromCreateRoomButton.onClick.RemoveListener(OnBackToModeSelect);
            backFromCreateRoomButton.onClick.AddListener(OnBackToModeSelect);
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        }
        if (switchToCreateButton != null)
        {
            switchToCreateButton.onClick.RemoveListener(OnBackToModeSelect);
            switchToCreateButton.onClick.AddListener(OnBackToModeSelect);
        }
        if (backFromJoinRoomButton != null)
        {
            backFromJoinRoomButton.onClick.RemoveListener(OnBackToModeSelect);
            backFromJoinRoomButton.onClick.AddListener(OnBackToModeSelect);
        }

        if (selectCharacterButton != null)
        {
            selectCharacterButton.onClick.RemoveListener(OnSelectCharacterClicked);
            selectCharacterButton.onClick.AddListener(OnSelectCharacterClicked);
        }
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyClicked);
            readyButton.onClick.AddListener(OnReadyClicked);
        }
        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }
        if (leaveRoomButton != null)
        {
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        }

        // Subscribe to events
        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnCreateRoomComplete += HandleCreateRoomComplete;
            RoomClient.Instance.OnJoinRoomComplete += HandleJoinRoomComplete;
            RoomClient.Instance.OnLeaveRoomComplete += HandleLeaveRoomComplete;
            RoomClient.Instance.OnCloseRoomComplete += HandleCloseRoomComplete;
            RoomClient.Instance.OnStartRoomComplete += HandleStartRoomComplete;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            GameManager.Instance.OnError += HandleError;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnGetPlayersComplete += HandleGetPlayersComplete;
            RoomClient.Instance.OnGetRoomDetailsComplete += HandleGetRoomDetailsComplete;
            RoomClient.Instance.OnSetPlayerStatusComplete += HandlePlayerStatusComplete;
            RoomClient.Instance.OnSetPlayerCharacterComplete += HandlePlayerDataChanged;
        }

        UpdateUI();
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
        if (switchToJoinButton != null)
            switchToJoinButton.onClick.RemoveListener(OnBackToModeSelect);
        if (backFromCreateRoomButton != null)
            backFromCreateRoomButton.onClick.RemoveListener(OnBackToModeSelect);
        if (joinRoomButton != null)
            joinRoomButton.onClick.RemoveListener(OnJoinRoomClicked);
        if (switchToCreateButton != null)
            switchToCreateButton.onClick.RemoveListener(OnBackToModeSelect);
        if (backFromJoinRoomButton != null)
            backFromJoinRoomButton.onClick.RemoveListener(OnBackToModeSelect);
        if (selectCharacterButton != null)
            selectCharacterButton.onClick.RemoveListener(OnSelectCharacterClicked);
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyClicked);
        if (startGameButton != null)
            startGameButton.onClick.RemoveListener(OnStartGameClicked);
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.RemoveListener(OnLeaveRoomClicked);

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnCreateRoomComplete -= HandleCreateRoomComplete;
            RoomClient.Instance.OnJoinRoomComplete -= HandleJoinRoomComplete;
            RoomClient.Instance.OnLeaveRoomComplete -= HandleLeaveRoomComplete;
            RoomClient.Instance.OnCloseRoomComplete -= HandleCloseRoomComplete;
            RoomClient.Instance.OnStartRoomComplete -= HandleStartRoomComplete;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnError -= HandleError;
        }

        if (RoomClient.Instance != null)
        {
            RoomClient.Instance.OnGetPlayersComplete -= HandleGetPlayersComplete;
            RoomClient.Instance.OnGetRoomDetailsComplete -= HandleGetRoomDetailsComplete;
            RoomClient.Instance.OnSetPlayerStatusComplete -= HandlePlayerStatusComplete;
            RoomClient.Instance.OnSetPlayerCharacterComplete -= HandlePlayerDataChanged;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.RoomLobby)
        {
            return;
        }

        if (string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        if (GameManager.Instance.IsHost && RoomClient.Instance != null && Time.unscaledTime >= nextRoomHeartbeatTime)
        {
            RoomClient.Instance.SendHeartbeat(GameManager.Instance.CurrentRoomCode);
            nextRoomHeartbeatTime = Time.unscaledTime + roomHeartbeatInterval;
        }

        if (!GameManager.Instance.IsHost && RoomClient.Instance != null && Time.unscaledTime >= nextRoomDetailsRefreshTime)
        {
            RoomClient.Instance.GetRoomDetails(GameManager.Instance.CurrentRoomCode);
            nextRoomDetailsRefreshTime = Time.unscaledTime + roomDetailsRefreshInterval;
        }

        if (roomPlayersRequestInFlight)
        {
            return;
        }

        if (Time.unscaledTime >= nextRoomPlayersRefreshTime)
        {
            RequestRoomPlayersRefresh();
        }
    }

    public void ShowCreateRoomPanel()
    {
        createRoomPanel.SetActive(true);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(false);
        ClearCreateRoomUI();
    }

    public void ShowJoinRoomPanel()
    {
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(true);
        roomPanel.SetActive(false);
        ClearJoinRoomUI();
    }

    public void ShowRoomPanel()
    {
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(true);
        isPlayerReady = false;
        gameplayLoadRequested = false;
        currentRoomPlayers = new RoomClient.PlayerInfo[0];
        UpdateUI();
        RequestRoomPlayersRefresh();
        nextRoomDetailsRefreshTime = Time.unscaledTime + roomDetailsRefreshInterval;
        nextRoomHeartbeatTime = Time.unscaledTime + roomHeartbeatInterval;
    }

    private void ClearCreateRoomUI()
    {
        if (roomNameInput != null)
            roomNameInput.text = "";
        if (createRoomStatusText != null)
            createRoomStatusText.text = "";
    }

    private void ClearJoinRoomUI()
    {
        if (joinRoomCodeInput != null)
            joinRoomCodeInput.text = "";
        if (joinRoomStatusText != null)
            joinRoomStatusText.text = "";
    }

    private void OnCreateRoomClicked()
    {
        if (roomNameInput == null)
        {
            Debug.LogError("roomNameInput is null");
            return;
        }

        string roomName = roomNameInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
        {
            if (createRoomStatusText != null)
            {
                createRoomStatusText.text = "Room name cannot be empty";
                createRoomStatusText.color = Color.red;
            }
            return;
        }

        if (createRoomStatusText != null)
        {
            createRoomStatusText.text = "Creating room...";
            createRoomStatusText.color = Color.yellow;
        }
        if (createRoomButton != null)
            createRoomButton.interactable = false;

        RoomClient.Instance.CreateRoom(roomName);
    }

    private void OnJoinRoomClicked()
    {
        if (joinRoomCodeInput == null)
        {
            Debug.LogError("joinRoomCodeInput is null");
            return;
        }

        string roomCode = joinRoomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(roomCode))
        {
            if (joinRoomStatusText != null)
            {
                joinRoomStatusText.text = "Room code cannot be empty";
                joinRoomStatusText.color = Color.red;
            }
            return;
        }

        if (joinRoomStatusText != null)
        {
            joinRoomStatusText.text = "Joining room...";
            joinRoomStatusText.color = Color.yellow;
        }
        if (joinRoomButton != null)
            joinRoomButton.interactable = false;

        RoomClient.Instance.JoinRoom(roomCode);
    }

    private void OnSelectCharacterClicked()
    {
        Debug.Log("Select Character clicked - returning to Character Select");
        GameManager.Instance.ChangeState(GameManager.GameState.CharacterSelect);
    }

    private void OnReadyClicked()
    {
        if (GameManager.Instance == null || RoomClient.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        isPlayerReady = !isPlayerReady;
        Debug.Log($"Player ready status: {isPlayerReady}");

        string userId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;
        if (!string.IsNullOrEmpty(userId))
        {
            RoomClient.Instance.SetPlayerReady(GameManager.Instance.CurrentRoomCode, userId, isPlayerReady);
        }
    }

    private void OnStartGameClicked()
    {
        if (GameManager.Instance != null && string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            Debug.LogWarning("No room selected");
            return;
        }

        if (!GameManager.Instance.IsHost)
        {
            Debug.LogWarning("Only host can start the game");
            return;
        }

        if (RoomClient.Instance == null)
        {
            Debug.LogError("RoomClient.Instance is null");
            return;
        }

        if (startGameButton != null)
            startGameButton.interactable = false;

        RoomClient.Instance.StartRoom(GameManager.Instance.CurrentRoomCode);
    }

    private void OnLeaveRoomClicked()
    {
        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = false;

        if (RoomClient.Instance == null)
            return;

        if (GameManager.Instance.IsHost)
        {
            RoomClient.Instance.CloseRoom(GameManager.Instance.CurrentRoomCode);
        }
        else
        {
            RoomClient.Instance.LeaveRoom(GameManager.Instance.CurrentRoomCode);
        }
    }

    private void OnBackToModeSelect()
    {
        // Leave room if currently in one
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            if (RoomClient.Instance != null)
            {
                if (GameManager.Instance.IsHost)
                {
                    RoomClient.Instance.CloseRoom(GameManager.Instance.CurrentRoomCode);
                }
                else
                {
                    RoomClient.Instance.LeaveRoom(GameManager.Instance.CurrentRoomCode);
                }
            }
        }
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            if (GameManager.Instance.CurrentState != GameManager.GameState.ModeSelect)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
            }
        }

        HideAllRoomPanels();

        ModeSelectUIManager modeSelectUIManager = FindAnyObjectByType<ModeSelectUIManager>(FindObjectsInactive.Include);
        if (modeSelectUIManager != null)
        {
            modeSelectUIManager.ShowModeSelectPanel();
        }
    }

    private void HandleCreateRoomComplete(RoomClient.RoomResult result)
    {
        if (createRoomButton != null)
            createRoomButton.interactable = true;

        if (result.success)
        {
            if (GameManager.Instance != null && result.room != null)
            {
                GameManager.Instance.SetCurrentRoom(result.room.roomCode, result.room.name, true);
            }

            if (createRoomStatusText != null)
            {
                createRoomStatusText.text = "Room created successfully!";
                createRoomStatusText.color = Color.green;
            }
            ClearCreateRoomUI();
            ShowRoomPanel();
        }
        else
        {
            if (createRoomStatusText != null)
            {
                createRoomStatusText.text = result.error;
                createRoomStatusText.color = Color.red;
            }
        }
    }

    private void HandleJoinRoomComplete(RoomClient.RoomResult result)
    {
        if (joinRoomButton != null)
            joinRoomButton.interactable = true;

        if (result.success)
        {
            if (GameManager.Instance != null && result.room != null)
            {
                GameManager.Instance.SetCurrentRoom(result.room.roomCode, result.room.name, false);
            }

            if (joinRoomStatusText != null)
            {
                joinRoomStatusText.text = "Joined room successfully!";
                joinRoomStatusText.color = Color.green;
            }
            ClearJoinRoomUI();
            ShowRoomPanel();
        }
        else
        {
            if (joinRoomStatusText != null)
            {
                joinRoomStatusText.text = result.error;
                joinRoomStatusText.color = Color.red;
            }
        }
    }

    private void HandleLeaveRoomComplete(RoomClient.RoomResult result)
    {
        if (leaveRoomButton != null)
            leaveRoomButton.interactable = true;

        if (result.success)
        {
            ReturnToModeSelectAfterLeave();
        }
        else
        {
            Debug.LogError("Failed to leave room: " + result.error);
        }
    }

    private void HandleCloseRoomComplete(RoomClient.RoomResult result)
    {
        if (leaveRoomButton != null)
            leaveRoomButton.interactable = true;

        if (result.success)
        {
            ReturnToModeSelectAfterLeave();
        }
        else
        {
            Debug.LogError("Failed to close room: " + result.error);
        }
    }

    private void HandleStartRoomComplete(RoomClient.RoomResult result)
    {
        if (startGameButton != null)
            startGameButton.interactable = true;

        if (!result.success)
        {
            Debug.LogError("Failed to start room: " + result.error);
            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
        }

        GameSceneLoader.LoadGameplaySceneAfterIntro(gameplaySceneName);
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.RoomLobby)
        {
            if (GameManager.Instance != null && (GameManager.Instance.IsMultiplayer || !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode)))
            {
                ShowRoomPanel();
                RequestRoomPlayersRefresh();
            }
            else
            {
                ShowCreateRoomPanel();
            }
        }
        else if (newState == GameManager.GameState.Auth || newState == GameManager.GameState.MainMenu || 
                 newState == GameManager.GameState.ModeSelect || newState == GameManager.GameState.CharacterSelect)
        {
            createRoomPanel.SetActive(false);
            joinRoomPanel.SetActive(false);
            roomPanel.SetActive(false);
        }
    }

    private void HandleError(string error)
    {
        Debug.LogError("Room operation error: " + error);
    }

    private void UpdateUI()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        // Update Room Info Display
        if (roomNameDisplay != null)
            roomNameDisplay.text = string.IsNullOrEmpty(GameManager.Instance.CurrentRoomName)
                ? "Room Name: None"
                : "Room Name: " + GameManager.Instance.CurrentRoomName;

        if (roomCodeDisplay != null)
            roomCodeDisplay.text = string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode) 
                ? "Room Code: None" 
                : "Room Code: " + GameManager.Instance.CurrentRoomCode;

        // Update Your Character Display
        string localCharacterName = GetLocalCharacterName();
        if (yourCharacterDisplay != null)
            yourCharacterDisplay.text = string.IsNullOrEmpty(localCharacterName)
                ? "Character: Not Selected"
                : "Character: " + localCharacterName;

        UpdateLocalPlayerUI();

        // Update Player List
        RefreshPlayerList();

        // Update buttons
        if (selectCharacterButton != null)
            selectCharacterButton.interactable = true;

        if (readyButton != null)
            readyButton.interactable = true;

        UpdateReadyButtonLabel();

        if (startGameButton != null)
        {
            bool hasLocalCharacter = !string.IsNullOrEmpty(GetLocalCharacterName());
            startGameButton.interactable = GameManager.Instance.IsHost && hasLocalCharacter;
        }

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = true;
    }

    private void RefreshPlayerList()
    {
        if (playerListContainer == null)
            return;

        // Clear existing items
        foreach (GameObject item in playerListItems)
        {
            Destroy(item);
        }
        playerListItems.Clear();

        if (GameManager.Instance == null)
        {
            return;
        }

        if (currentRoomPlayers != null && currentRoomPlayers.Length > 0)
        {
            foreach (var player in currentRoomPlayers)
            {
                if (player == null)
                    continue;

                AddPlayerToList(player.username, player.character, player.isReady, IsLocalPlayer(player), player.role);
            }
        }
        else
        {
            AddPlayerToList(GameManager.Instance.CurrentUsername, GetLocalCharacterName(), isPlayerReady, true, GameManager.Instance.IsHost ? "owner" : "player");
        }
    }

    private void HideAllRoomPanels()
    {
        if (createRoomPanel != null)
            createRoomPanel.SetActive(false);
        if (joinRoomPanel != null)
            joinRoomPanel.SetActive(false);
        if (roomPanel != null)
            roomPanel.SetActive(false);
    }

    private Button FindButton(GameObject panel, string namePart)
    {
        if (panel == null)
            return null;

        return panel.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(button => button != null && button.name.ToLowerInvariant().Contains(namePart));
    }

    private bool IsLocalPlayer(RoomClient.PlayerInfo player)
    {
        if (player == null)
            return false;

        string localUserId = AuthClient.Instance != null ? AuthClient.Instance.GetStoredUserId() : string.Empty;
        if (!string.IsNullOrEmpty(localUserId) && !string.IsNullOrEmpty(player.userId))
            return string.Equals(player.userId, localUserId, System.StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrEmpty(GameManager.Instance.CurrentUsername) &&
               !string.IsNullOrEmpty(player.username) &&
               string.Equals(player.username, GameManager.Instance.CurrentUsername, System.StringComparison.OrdinalIgnoreCase);
    }

    private string GetRoleLabel(string role)
    {
        return string.Equals(role, "owner", System.StringComparison.OrdinalIgnoreCase) ? "Host" : "Player";
    }

    private void AddPlayerToList(string playerName, string characterName, bool isReady, bool isYou, string role)
    {
        if (playerListItemPrefab == null || playerListContainer == null)
            return;

        GameObject item = Instantiate(playerListItemPrefab, playerListContainer);
        playerListItems.Add(item);

        TMP_Text[] texts = item.GetComponentsInChildren<TMP_Text>(true);
        string displayName = string.IsNullOrEmpty(playerName) ? "Unknown" : playerName;
        if (isYou)
        {
            displayName += " (You)";
        }

        string displayRole = GetRoleLabel(role);
        string displayCharacter = string.IsNullOrEmpty(characterName) ? "None" : characterName;
        string displayStatus = isReady ? "[READY]" : "[NOT READY]";

        if (texts.Length >= 3)
        {
            texts[0].text = displayName;
            texts[1].text = $"{displayRole} | {displayCharacter}";
            texts[2].text = displayStatus;
        }
        else if (texts.Length >= 2)
        {
            texts[0].text = displayName;
            texts[1].text = $"{displayRole} | {displayCharacter} {displayStatus}";
        }
        else if (texts.Length == 1)
        {
            texts[0].text = $"{displayName}\n{displayRole} | {displayCharacter}\n{displayStatus}";
        }
    }

    private void UpdateLocalPlayerUI()
    {
        if (GameManager.Instance == null)
            return;

        string characterName = GetLocalCharacterName();

        if (yourCharacterDisplay != null)
        {
            yourCharacterDisplay.text = string.IsNullOrEmpty(characterName) ? "Character: None" : "Character: " + characterName;
        }

        if (yourCharacterImage != null)
        {
            CharacterDisplayData data = characterIcons.FirstOrDefault(x => x != null && !string.IsNullOrEmpty(x.characterName) &&
                string.Equals(x.characterName, characterName, System.StringComparison.OrdinalIgnoreCase));
            if (data != null && data.characterIcon != null)
            {
                yourCharacterImage.sprite = data.characterIcon;
                yourCharacterImage.gameObject.SetActive(true);
            }
            else
            {
                yourCharacterImage.sprite = null;
                yourCharacterImage.gameObject.SetActive(false);
                if (!string.IsNullOrEmpty(characterName))
                {
                    Debug.LogWarning($"[RoomUIManager] No character icon mapping found for '{characterName}'. Check characterIcons names in Inspector.");
                }
            }
        }
    }

    private string GetLocalCharacterName()
    {
        if (GameManager.Instance == null)
            return null;

        if (!GameManager.Instance.IsMultiplayer)
            return GameManager.Instance.SelectedCharacter;

        if (currentRoomPlayers != null && currentRoomPlayers.Length > 0)
        {
            foreach (var player in currentRoomPlayers)
            {
                if (player == null || string.IsNullOrEmpty(player.username))
                    continue;

                if (!string.IsNullOrEmpty(GameManager.Instance.CurrentUsername) &&
                    string.Equals(player.username, GameManager.Instance.CurrentUsername, System.StringComparison.OrdinalIgnoreCase))
                {
                    return player.character;
                }
            }
        }

        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.RoomSelectedCharacter))
        {
            return GameManager.Instance.RoomSelectedCharacter;
        }

        return null;
    }

    private void RequestRoomPlayersRefresh()
    {
        if (GameManager.Instance == null || RoomClient.Instance == null)
            return;

        if (string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
            return;

        roomPlayersRequestInFlight = true;
        nextRoomPlayersRefreshTime = Time.unscaledTime + roomPlayersRefreshInterval;
        RoomClient.Instance.GetRoomPlayers(GameManager.Instance.CurrentRoomCode);
    }

    private void HandleGetPlayersComplete(RoomClient.RoomDetailsResult result)
    {
        roomPlayersRequestInFlight = false;

        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        if (!string.IsNullOrEmpty(result.requestedRoomCode) &&
            !string.Equals(result.requestedRoomCode, GameManager.Instance.CurrentRoomCode, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning($"Ignoring stale room players response for '{result.requestedRoomCode}'. Current room is '{GameManager.Instance.CurrentRoomCode}'.");
            return;
        }

        if (!result.success)
        {
            Debug.LogWarning("Failed to refresh room players: " + result.error);
            if (ShouldForceReturnToMainMenu(result.error))
            {
                ReturnToMainMenuAfterRoomClosed();
                return;
            }

            nextRoomPlayersRefreshTime = Time.unscaledTime + roomPlayersRefreshInterval;
            return;
        }

        if (result.room != null && GameManager.Instance != null)
        {
            string roomCode = string.IsNullOrEmpty(result.room.roomCode) ? GameManager.Instance.CurrentRoomCode : result.room.roomCode;
            string roomName = string.IsNullOrEmpty(result.room.name) ? GameManager.Instance.CurrentRoomName : result.room.name;
            GameManager.Instance.SetCurrentRoom(roomCode, roomName, GameManager.Instance.IsHost);
            if (!string.IsNullOrEmpty(result.room.relayJoinCode))
                GameManager.Instance.SetRelayJoinCode(result.room.relayJoinCode);
        }

        currentRoomPlayers = result.players ?? new RoomClient.PlayerInfo[0];
        RefreshPlayerList();
        UpdateLocalPlayerUI();
        nextRoomPlayersRefreshTime = Time.unscaledTime + roomPlayersRefreshInterval;
    }

    private void HandleGetRoomDetailsComplete(RoomClient.RoomDetailsResult result)
    {
        if (!result.success)
        {
            if (ShouldForceReturnToMainMenu(result.error))
            {
                ReturnToMainMenuAfterRoomClosed();
            }

            return;
        }

        if (result.room != null && GameManager.Instance != null)
        {
            string roomCode = string.IsNullOrEmpty(result.room.roomCode) ? GameManager.Instance.CurrentRoomCode : result.room.roomCode;
            string roomName = string.IsNullOrEmpty(result.room.name) ? GameManager.Instance.CurrentRoomName : result.room.name;
            GameManager.Instance.SetCurrentRoom(roomCode, roomName, GameManager.Instance.IsHost);

            if (GameManager.Instance.IsMultiplayer && !GameManager.Instance.IsHost &&
                !GameManager.Instance.SuppressRoomGameplayAutoLoad &&
                result.room.status == "playing" && !gameplayLoadRequested)
            {
                gameplayLoadRequested = true;
                GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
                GameSceneLoader.LoadGameplaySceneAfterIntro(gameplaySceneName);
            }
        }
    }

    private void HandlePlayerDataChanged(RoomClient.RoomResult result)
    {
        if (!result.success)
        {
            return;
        }

        RequestRoomPlayersRefresh();
    }

    private void HandlePlayerStatusComplete(RoomClient.RoomResult result)
    {
        if (readyButton != null)
            readyButton.interactable = true;

        if (!result.success)
        {
            isPlayerReady = !isPlayerReady;
            Debug.LogWarning("Failed to update ready status: " + result.error);
            return;
        }

        RequestRoomPlayersRefresh();
    }

    private void UpdateReadyButtonLabel()
    {
        if (readyButton == null)
            return;

        TMP_Text tmpLabel = readyButton.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null)
        {
            tmpLabel.text = string.Empty;
            return;
        }

        Text legacyLabel = readyButton.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.text = string.Empty;
        }
    }

    private void ReturnToModeSelectAfterLeave()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            if (GameManager.Instance.CurrentState != GameManager.GameState.ModeSelect)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
            }
        }

        currentRoomPlayers = new RoomClient.PlayerInfo[0];
        roomPlayersRequestInFlight = false;
        nextRoomPlayersRefreshTime = 0f;
        nextRoomDetailsRefreshTime = 0f;
        nextRoomHeartbeatTime = 0f;
        isPlayerReady = false;
        gameplayLoadRequested = false;
        UpdateReadyButtonLabel();

        HideAllRoomPanels();

        ModeSelectUIManager modeSelectUIManager = FindAnyObjectByType<ModeSelectUIManager>(FindObjectsInactive.Include);
        if (modeSelectUIManager != null)
        {
            modeSelectUIManager.ShowModeSelectPanel();
        }
    }

    private void ReturnToMainMenuAfterRoomClosed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            if (GameManager.Instance.CurrentState != GameManager.GameState.MainMenu)
            {
                GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
            }
        }

        currentRoomPlayers = new RoomClient.PlayerInfo[0];
        roomPlayersRequestInFlight = false;
        nextRoomPlayersRefreshTime = 0f;
        nextRoomDetailsRefreshTime = 0f;
        nextRoomHeartbeatTime = 0f;
        isPlayerReady = false;
        gameplayLoadRequested = false;
        UpdateReadyButtonLabel();

        HideAllRoomPanels();

        MainMenuUIManager mainMenuUIManager = FindAnyObjectByType<MainMenuUIManager>(FindObjectsInactive.Include);
        if (mainMenuUIManager != null)
        {
            mainMenuUIManager.ShowMainMenuUI();
        }
    }

    private bool ShouldForceReturnToMainMenu(string error)
    {
        if (GameManager.Instance == null || GameManager.Instance.IsHost || !GameManager.Instance.IsMultiplayer)
        {
            return false;
        }

        if (string.IsNullOrEmpty(error))
        {
            return false;
        }

        string normalized = error.ToLowerInvariant();
        return normalized.Contains("room not found") || normalized.Contains("already closed");
    }
}
