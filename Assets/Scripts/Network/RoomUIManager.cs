using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;

    [SerializeField] private string gameplaySceneName = "SampleScene";

    private bool isPlayerReady = false;
    private List<GameObject> playerListItems = new List<GameObject>();

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
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            GameManager.Instance.OnError += HandleError;
            HandleStateChanged(GameManager.Instance.CurrentState);
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
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
            GameManager.Instance.OnError -= HandleError;
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

    private void ShowRoomPanel()
    {
        createRoomPanel.SetActive(false);
        joinRoomPanel.SetActive(false);
        roomPanel.SetActive(true);
        isPlayerReady = false;
        UpdateUI();
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
        isPlayerReady = !isPlayerReady;
        Debug.Log($"Player ready status: {isPlayerReady}");
        
        if (readyButton != null)
        {
            readyButton.GetComponentInChildren<Text>().text = isPlayerReady ? "Not Ready" : "Ready";
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

        GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnLeaveRoomClicked()
    {
        if (GameManager.Instance == null || string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            return;
        }

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = false;
        RoomClient.Instance.LeaveRoom(GameManager.Instance.CurrentRoomCode);
    }

    private void OnBackToModeSelect()
    {
        // Leave room if currently in one
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.CurrentRoomCode))
        {
            RoomClient.Instance.LeaveRoom(GameManager.Instance.CurrentRoomCode);
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
            if (GameManager.Instance != null)
                GameManager.Instance.ClearCurrentRoom();
            ShowCreateRoomPanel();
        }
        else
        {
            Debug.LogError("Failed to leave room: " + result.error);
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.RoomLobby)
        {
            ShowCreateRoomPanel();
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
        if (yourCharacterDisplay != null)
            yourCharacterDisplay.text = string.IsNullOrEmpty(GameManager.Instance.SelectedCharacter)
                ? "Character: Not Selected"
                : "Character: " + GameManager.Instance.SelectedCharacter;

        // Update Player List
        UpdatePlayerList();

        // Update buttons
        if (selectCharacterButton != null)
            selectCharacterButton.interactable = true;

        if (readyButton != null)
            readyButton.interactable = true;

        if (startGameButton != null)
            startGameButton.interactable = GameManager.Instance.IsHost;

        if (leaveRoomButton != null)
            leaveRoomButton.interactable = true;
    }

    private void UpdatePlayerList()
    {
        if (playerListContainer == null)
            return;

        // Clear existing items
        foreach (GameObject item in playerListItems)
        {
            Destroy(item);
        }
        playerListItems.Clear();

        // TODO: Get actual player list from room data
        if (GameManager.Instance != null)
        {
            AddPlayerToList(GameManager.Instance.CurrentUsername, isPlayerReady, true);
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

    private void AddPlayerToList(string playerName, bool isReady, bool isYou)
    {
        if (playerListItemPrefab == null || playerListContainer == null)
            return;

        GameObject item = Instantiate(playerListItemPrefab, playerListContainer);
        playerListItems.Add(item);

        Text itemText = item.GetComponentInChildren<Text>();
        if (itemText != null)
        {
            string status = isReady ? "[READY]" : "[NOT READY]";
            string marker = isYou ? " (You)" : "";
            itemText.text = $"- {playerName} {status}{marker}";
        }
    }
}
