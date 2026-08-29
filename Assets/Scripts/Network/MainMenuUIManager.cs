using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class MainMenuUIManager : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    
    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private Button exitButton;
    
    [Header("UI Elements")]
    [SerializeField] private TMP_Text usernameText;

    private void Awake()
    {
        Debug.Log($"MainMenuUIManager.Awake called. Script is on: {gameObject.name}, active: {gameObject.activeInHierarchy}");
    }

    private void OnEnable()
    {
        Debug.Log($"MainMenuUIManager.OnEnable called. mainMenuPanel ref: {mainMenuPanel}");

        // Auto-find mainMenuPanel if not assigned
        if (mainMenuPanel == null)
        {
            mainMenuPanel = transform.parent != null 
                ? transform.parent.Find("MainMenu")?.gameObject 
                : GameObject.Find("Canvas/Root/MainMenu");
            
            if (mainMenuPanel == null)
            {
                mainMenuPanel = gameObject;  // Assume this IS the main menu panel
            }
            Debug.Log($"Auto-found mainMenuPanel: {mainMenuPanel}");
        }

        // Auto-find buttons if not assigned
        if (startButton == null) startButton = FindButtonInPanel(mainMenuPanel, "start");
        if (continueButton == null) continueButton = FindButtonInPanel(mainMenuPanel, "continue");
        if (settingsButton == null) settingsButton = FindButtonInPanel(mainMenuPanel, "setting", "settings");
        if (logoutButton == null) logoutButton = FindButtonInPanel(mainMenuPanel, "logout");
        if (exitButton == null) exitButton = FindButtonInPanel(mainMenuPanel, "exit");

        if (mainMenuPanel == null || startButton == null || settingsButton == null || logoutButton == null || exitButton == null)
        {
            Debug.LogError("MainMenuUIManager is missing required references in the Inspector.");
            Debug.LogError($"  mainMenuPanel: {mainMenuPanel}");
            Debug.LogError($"  startButton: {startButton}");
            Debug.LogError($"  settingsButton: {settingsButton}");
            Debug.LogError($"  logoutButton: {logoutButton}");
            Debug.LogError($"  exitButton: {exitButton}");
            return;
        }

        Debug.Log("MainMenuUIManager: OnEnable - all references found");

        startButton.onClick.AddListener(OnStartClicked);
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        logoutButton.onClick.AddListener(OnLogoutClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        RefreshContinueButton();

        // GameManager might not be initialized yet, so wait for it
        if (GameManager.Instance != null)
        {
            Debug.Log("MainMenuUIManager: GameManager found immediately, subscribing");
            SubscribeToGameManager();
        }
        else
        {
            Debug.LogWarning("MainMenuUIManager: GameManager.Instance is null, will retry in Update");
        }
    }

    private void OnDisable()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
        if (logoutButton != null)
            logoutButton.onClick.RemoveListener(OnLogoutClicked);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitClicked);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private bool gameManagerSubscribed = false;

    private void Update()
    {
        // Retry subscription if GameManager wasn't ready during OnEnable
        if (!gameManagerSubscribed && GameManager.Instance != null)
        {
            Debug.Log("MainMenuUIManager: GameManager is now available, subscribing");
            SubscribeToGameManager();
        }
    }

    private void SubscribeToGameManager()
    {
        if (gameManagerSubscribed)
            return;

        GameManager.Instance.OnStateChanged += HandleStateChanged;
        gameManagerSubscribed = true;

        RefreshUsernameText();

        Debug.Log("MainMenuUIManager: Successfully subscribed to GameManager");

        HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void OnStartClicked()
    {
        Debug.Log("Starting game - Going to Mode Select");
        GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
    }

    private void OnContinueClicked()
    {
        Debug.Log("Continue single-player save");
        SaveGameManager.ContinueSinglePlayer();
    }

    public void ShowMainMenuUI()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(true);
            Debug.Log("MainMenuUIManager: ShowMainMenuUI called");
        }
    }

    public void HideMainMenuUI()
    {
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(false);
            Debug.Log("MainMenuUIManager: HideMainMenuUI called");
        }
    }

    private void OnSettingsClicked()
    {
        Debug.Log("Settings clicked (not implemented yet)");
        // TODO: Implement settings panel
    }

    private void OnLogoutClicked()
    {
        Debug.Log("Logging out");
        GameManager.Instance.Logout();
    }

    private void OnExitClicked()
    {
        Debug.Log("Exiting game");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        Debug.Log($"MainMenuUIManager: State changed to {newState}");
        if (mainMenuPanel != null)
        {
            mainMenuPanel.SetActive(newState == GameManager.GameState.MainMenu);
        }

        if (newState == GameManager.GameState.MainMenu)
        {
            RefreshUsernameText();
            RefreshContinueButton();
        }
    }

    private void RefreshUsernameText()
    {
        if (usernameText == null || GameManager.Instance == null)
            return;

        string username = string.IsNullOrEmpty(GameManager.Instance.CurrentUsername)
            ? "Player"
            : GameManager.Instance.CurrentUsername;

        usernameText.text = $"{username}!";
        Debug.Log($"MainMenuUIManager: Username text set to: {usernameText.text}");
    }

    private void RefreshContinueButton()
    {
        if (continueButton != null)
            continueButton.gameObject.SetActive(SaveGameManager.HasSingleSave);
    }

    private Button FindButtonInPanel(GameObject panel, params string[] keywords)
    {
        if (panel == null || keywords == null)
            return null;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            string objectName = button.name.ToLowerInvariant();
            string labelText = button.GetComponentInChildren<TMP_Text>(true)?.text?.ToLowerInvariant() ?? string.Empty;

            foreach (string keyword in keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                string lowerKeyword = keyword.ToLowerInvariant();
                if (objectName.Contains(lowerKeyword) || labelText.Contains(lowerKeyword))
                    return button;
            }
        }

        return null;
    }
    private void OnDestroy()
    {
        // Gỡ đăng ký với cả GameManager và RoomClient để an toàn tuyệt đối
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }
}
