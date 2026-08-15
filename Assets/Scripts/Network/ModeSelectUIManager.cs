using UnityEngine;
using UnityEngine.UI;

public class ModeSelectUIManager : MonoBehaviour
{
    [SerializeField] private GameObject modeSelectPanel;
    [SerializeField] private GameObject createJoinPanel;
    [SerializeField] private RoomUIManager roomUIManager;
    
    [Header("Buttons")]
    [SerializeField] private Button singlePlayerButton;
    [SerializeField] private Button multiplayerButton;
    [SerializeField] private Button backButton;

    private bool gameManagerSubscribed;
 
    private void OnEnable()
    {
        if (modeSelectPanel == null || singlePlayerButton == null || multiplayerButton == null || backButton == null)
        {
            Debug.LogError("ModeSelectUIManager is missing required references in the Inspector.");
            return;
        }

        singlePlayerButton.onClick.AddListener(OnSinglePlayerClicked);
        multiplayerButton.onClick.AddListener(OnMultiplayerClicked);
        backButton.onClick.AddListener(OnBackClicked);

        if (GameManager.Instance != null)
        {
            TrySubscribeToGameManager();
        }
    }

    private void Update()
    {
        if (!gameManagerSubscribed && GameManager.Instance != null)
        {
            TrySubscribeToGameManager();
        }
    }

    private void OnDisable()
    {
        if (singlePlayerButton != null)
            singlePlayerButton.onClick.RemoveListener(OnSinglePlayerClicked);
        if (multiplayerButton != null)
            multiplayerButton.onClick.RemoveListener(OnMultiplayerClicked);
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null && gameManagerSubscribed)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void TrySubscribeToGameManager()
    {
        if (gameManagerSubscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.OnStateChanged += HandleStateChanged;
        gameManagerSubscribed = true;
        HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void OnSinglePlayerClicked()
    {
        Debug.Log("Single Player Mode Selected");
        GameManager.Instance.SetMultiplayerMode(false);
        ShowCharacterSelectPanel();
        GameManager.Instance.ChangeState(GameManager.GameState.CharacterSelect);
    }

    private void OnMultiplayerClicked()
    {
        Debug.Log("Multiplayer Mode Selected");
        GameManager.Instance.SetMultiplayerMode(true);
        // Open the Create/Join selection panel first
        ShowCreateJoinPanel();
    }

    private void ShowCreateJoinPanel()
    {
        Debug.Log("ModeSelectUIManager: ShowCreateJoinPanel called");
        
        // Find RoomUIManager if not assigned
        if (roomUIManager == null)
        {
            roomUIManager = FindAnyObjectByType<RoomUIManager>();
            if (roomUIManager == null)
            {
                Debug.LogWarning("ModeSelectUIManager: RoomUIManager not found in scene.");
            }
        }
        
        // Prefer inspector-assigned panel
        GameObject panel = createJoinPanel;

        // Try GameObject.Find (active only) if not assigned
        if (panel == null)
            panel = GameObject.Find("CreateJoinButton");

        // If still not found, search all Transforms including inactive
        if (panel == null)
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (var t in all)
            {
                if (t == null) continue;
                if (t.name == "CreateJoinButton")
                {
                    panel = t.gameObject;
                    break;
                }
            }
        }

        if (panel == null)
        {
            Debug.LogWarning("ModeSelectUIManager: CreateJoinButton panel not found in scene.");
            return;
        }

        // Activate the create/join panel and deactivate mode select
        panel.SetActive(true);
        modeSelectPanel.SetActive(false);

        // Find create / join buttons inside the panel and wire them
        var buttons = panel.GetComponentsInChildren<Button>(true);
        Button createBtn = null;
        Button joinBtn = null;
        Button backBtn = null;
        foreach (var b in buttons)
        {
            if (b == null) continue;
            var n = b.name.ToLowerInvariant();
            if (createBtn == null && n.Contains("create")) createBtn = b;
            if (joinBtn == null && n.Contains("join")) joinBtn = b;
            if (backBtn == null && (n.Contains("back") || n.Contains("exit"))) backBtn = b;
        }

        if (createBtn != null)
        {
            createBtn.onClick.RemoveAllListeners();
            createBtn.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                
                // Call RoomUIManager to properly show CreateRoom panel
                if (roomUIManager != null)
                {
                    roomUIManager.ShowCreateRoomPanel();
                    Debug.Log("ModeSelectUIManager: Called RoomUIManager.ShowCreateRoomPanel()");
                }
                else
                {
                    Debug.LogWarning("ModeSelectUIManager: RoomUIManager not available, falling back to GameObject.Find");
                    var createRoom = GameObject.Find("CreateRoom");
                    if (createRoom != null) createRoom.SetActive(true);
                }
                Debug.Log("ModeSelectUIManager: CreateRoom opened");
            });
        }

        if (joinBtn != null)
        {
            joinBtn.onClick.RemoveAllListeners();
            joinBtn.onClick.AddListener(() =>
            {
                panel.SetActive(false);
                
                // Call RoomUIManager to properly show JoinRoom panel
                if (roomUIManager != null)
                {
                    roomUIManager.ShowJoinRoomPanel();
                    Debug.Log("ModeSelectUIManager: Called RoomUIManager.ShowJoinRoomPanel()");
                }
                else
                {
                    Debug.LogWarning("ModeSelectUIManager: RoomUIManager not available, falling back to GameObject.Find");
                    var joinRoom = GameObject.Find("JoinRoom");
                    if (joinRoom != null) joinRoom.SetActive(true);
                }
                Debug.Log("ModeSelectUIManager: JoinRoom opened");
            });
        }

        if (backBtn != null)
        {
            backBtn.onClick.RemoveAllListeners();
            backBtn.onClick.AddListener(() =>
            {
                Debug.Log("ModeSelectUIManager: Create/Join back to ModeSelect");
                GameManager.Instance.SetMultiplayerMode(false);
                panel.SetActive(false);
                ShowModeSelectPanel();
                GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
            });
        }
    }

    private void OnBackClicked()
    {
        Debug.Log("Back to Main Menu");
        GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (modeSelectPanel == null)
        {
            return;
        }

        bool showModeSelect = newState == GameManager.GameState.ModeSelect;
        modeSelectPanel.SetActive(showModeSelect);

        if (!showModeSelect && createJoinPanel != null)
        {
            createJoinPanel.SetActive(false);
        }
    }

    public void ShowModeSelectPanel()
    {
        if (modeSelectPanel != null)
        {
            modeSelectPanel.SetActive(true);
        }

        if (createJoinPanel != null)
        {
            createJoinPanel.SetActive(false);
        }
    }

    private void ShowCharacterSelectPanel()
    {
        Debug.Log("ModeSelectUIManager: ShowCharacterSelectPanel called");
        var characterSelectManagers = FindObjectsByType<CharacterSelectUIManager>(FindObjectsInactive.Include);
        
        Debug.Log($"ModeSelectUIManager: Found {(characterSelectManagers != null ? characterSelectManagers.Length : 0)} CharacterSelectUIManager instances");
        
        if (characterSelectManagers == null || characterSelectManagers.Length == 0)
        {
            Debug.LogWarning("ModeSelectUIManager: CharacterSelectUIManager not found.");
            return;
        }

        foreach (var manager in characterSelectManagers)
        {
            if (manager != null && manager.gameObject != null)
            {
                Debug.Log($"ModeSelectUIManager: Activating CharacterSelectUIManager on GameObject: {manager.gameObject.name}");
                manager.gameObject.SetActive(true);
                Debug.Log("ModeSelectUIManager: Activated CharacterSelectUIManager gameObject.");
                break;
            }
        }
    }
}
