using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class MainMenuUIManager : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    
    [Header("Buttons")]
    [SerializeField] private Button startButton;
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
        if (startButton == null) startButton = mainMenuPanel.transform.Find("Start")?.GetComponent<Button>();
        if (settingsButton == null) settingsButton = mainMenuPanel.transform.Find("Setting")?.GetComponent<Button>();
        if (logoutButton == null) logoutButton = mainMenuPanel.transform.Find("Logout")?.GetComponent<Button>();
        if (exitButton == null) exitButton = mainMenuPanel.transform.Find("Exit")?.GetComponent<Button>();

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
        settingsButton.onClick.AddListener(OnSettingsClicked);
        logoutButton.onClick.AddListener(OnLogoutClicked);
        exitButton.onClick.AddListener(OnExitClicked);

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
        
        // Display username
        if (usernameText != null)
        {
            string username = string.IsNullOrEmpty(GameManager.Instance.CurrentUsername)
                ? "Player"
                : GameManager.Instance.CurrentUsername;
            usernameText.text = $"{username}!";
            Debug.Log($"MainMenuUIManager: Username text set to: {usernameText.text}");
        }

        Debug.Log("MainMenuUIManager: Successfully subscribed to GameManager");

        HandleStateChanged(GameManager.Instance.CurrentState);
    }

    private void OnStartClicked()
    {
        Debug.Log("Starting game - Going to Mode Select");
        GameManager.Instance.ChangeState(GameManager.GameState.ModeSelect);
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
    }
}
