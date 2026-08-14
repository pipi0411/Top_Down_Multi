using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InGameHUDUIManager : MonoBehaviour
{
    [SerializeField] private GameObject inGameHUD;

    [Header("HP Bar")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Mana")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Lives")]
    [SerializeField] private TextMeshProUGUI livesText;

    [Header("Hotbar")]
    [SerializeField] private HotbarUI hotbarPrefab;
    [SerializeField] private Sprite hotbarSlotSprite;

    [Header("MiniMap")]
    [SerializeField] private RawImage miniMap;

    [Header("Navigation")]
    [SerializeField] private string mainMenuSceneName = "Main Manager";

    [Header("Pause")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;

    [Header("Result Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject lostPanel;

    private float currentHP = 100f;
    private float maxHP = 100f;
    private float currentMana = 100f;
    private float maxMana = 100f;
    private int currentAmmo = 30;
    private int maxAmmo = 30;
    private int currentLives = 3;
    private int maxLives = 3;
    private bool isReloading;
    private bool currentWeaponUsesAmmo = true;
    private bool lostPanelShown;
    private PlayerHealth localPlayerStats;
    private HotbarUI hotbarUI;

    private void OnEnable()
    {
        if (inGameHUD == null)
        {
            Debug.LogError("InGameHUDUIManager is missing inGameHUD reference in the Inspector.");
            return;
        }

        if (pauseButton != null)
            pauseButton.onClick.AddListener(OnPauseClicked);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            HandleStateChanged(GameManager.Instance.CurrentState);
        }

        // Initialize HUD
        EnsureLivesText();
        UpdateHPBar();
        UpdateMana();
        UpdateLives();
        EnsureHotbar();
    }

    private void OnDisable()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(OnPauseClicked);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePausePanel();
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.InGame)
        {
            if (localPlayerStats == null)
            {
                PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
                foreach (PlayerHealth player in players)
                {
                    if (!player.IsSpawned || player.IsOwner)
                    {
                        localPlayerStats = player;
                        break;
                    }
                }
            }
            if (localPlayerStats != null)
            {
                currentHP = localPlayerStats.CurrentHealth;
                maxHP = localPlayerStats.PlayerConfig.MaxHealth;
                currentMana = localPlayerStats.CurrentEnergy;
                maxMana = localPlayerStats.PlayerConfig.MaxEnergy;
                currentAmmo = localPlayerStats.CurrentAmmo;
                maxAmmo = localPlayerStats.MaxAmmo;
                currentLives = localPlayerStats.CurrentLives;
                maxLives = localPlayerStats.MaxLives;
                isReloading = localPlayerStats.IsReloading;
                currentWeaponUsesAmmo = localPlayerStats.CurrentWeaponUsesAmmo;
                UpdateHPBar();
                UpdateMana();
                UpdateLives();

                if (!lostPanelShown && localPlayerStats.IsOutOfLives)
                {
                    lostPanelShown = true;
                    ShowLostPanel();
                }
            }
        }
    }

    private void HandleStateChanged(GameManager.GameState newState)
    {
        if (newState == GameManager.GameState.InGame)
        {
            inGameHUD.SetActive(true);
            EnsureHotbar();
            if (hotbarUI != null)
                hotbarUI.gameObject.SetActive(true);
            if (pausePanel != null)
                pausePanel.SetActive(false);
            HideResultPanels();
            lostPanelShown = false;
        }
        else
        {
            inGameHUD.SetActive(false);
            if (hotbarUI != null)
                hotbarUI.gameObject.SetActive(false);
            HideOverlayPanels();
        }
    }

    private void EnsureHotbar()
    {
        if (hotbarUI != null) return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();

        hotbarUI = HotbarUI.Ensure(canvas, hotbarSlotSprite, hotbarPrefab);
    }

    private void EnsureLivesText()
    {
        if (livesText != null) return;

        GameObject tymObject = GameObject.Find("Tym");
        if (tymObject == null)
            tymObject = GameObject.Find("Heart");

        if (tymObject != null)
            livesText = tymObject.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void UpdateHPBar(float hp = -1f)
    {
        if (hp >= 0)
            currentHP = Mathf.Clamp(hp, 0, maxHP);

        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = maxHP > 0f ? currentHP / maxHP : 0f;
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHP:F0}/{maxHP:F0} HP";
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        UpdateHPBar(currentHP);

        if (currentHP <= 0)
        {
            OnPlayerDeath();
        }
    }

    public void UpdateAmmo(int ammo = -1)
    {
        if (ammo >= 0)
            currentMana = ammo;

        UpdateMana();
    }

    public void UpdateMana()
    {
        if (ammoText != null)
        {
            if (!currentWeaponUsesAmmo)
            {
                ammoText.text = "Melee";
                return;
            }

            ammoText.text = isReloading
                ? $"Ammo: {currentAmmo}/{maxAmmo} (Reloading...)"
                : $"Ammo: {currentAmmo}/{maxAmmo}";
        }
    }

    public void UpdateLives()
    {
        EnsureLivesText();
        if (livesText != null)
            livesText.text = $"{currentLives}/{maxLives}";
    }

    public void FireWeapon()
    {
        UpdateMana();
    }

    private void OnPauseClicked()
    {
        Debug.Log("Pause button clicked");

        TogglePausePanel();
    }

    private void TogglePausePanel()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.InGame)
            return;

        if (pausePanel != null)
        {
            bool isPaused = pausePanel.activeSelf;
            pausePanel.SetActive(!isPaused);
            Time.timeScale = isPaused ? 1f : 0f;
        }
    }

    public void ResumeGame()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ShowWinPanel()
    {
        ShowResultPanel(winPanel);
    }

    public void ShowLostPanel()
    {
        ShowResultPanel(lostPanel);
    }

    public void RestartGame()
    {
        if (lostPanel != null && lostPanel.activeSelf)
        {
            RetryLostGame();
            return;
        }

        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }

    private void RetryLostGame()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsMultiplayer)
        {
            RetryMultiplayerLostGame();
            return;
        }

        RetrySinglePlayerLostGame();
    }

    private void RetrySinglePlayerLostGame()
    {
        Time.timeScale = 1f;
        lostPanelShown = false;
        localPlayerStats = null;
        HideOverlayPanels();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        if (NetworkButtons.Instance != null)
            NetworkButtons.Instance.ResetNetworkStartupState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMultiplayerMode(false);
            GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
            GameManager.Instance.ChangeState(GameManager.GameState.GameStarting);
        }

        string gameplaySceneName = SceneManager.GetActiveScene().name;
        if (string.IsNullOrWhiteSpace(gameplaySceneName) || gameplaySceneName == mainMenuSceneName)
            gameplaySceneName = "SampleScene";

        GameSceneLoader.LoadGameplayScene(gameplaySceneName);
    }

    private void RetryMultiplayerLostGame()
    {
        if (!PlayerHealth.IsSpawnedTeamLost())
        {
            Debug.Log("[Retry] Waiting: team is not fully dead yet.");
            return;
        }

        if (localPlayerStats != null && localPlayerStats.IsSpawned && localPlayerStats.IsOwner)
        {
            localPlayerStats.RequestTeamRetryToRoomLobby();
            return;
        }

        ReturnToRoomLobbyAfterTeamLost();
    }

    public void ReturnToRoomLobbyAfterTeamLost()
    {
        Time.timeScale = 1f;
        HideOverlayPanels();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (NetworkButtons.Instance != null)
            NetworkButtons.Instance.ResetNetworkStartupState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMultiplayerMode(true);
            GameManager.Instance.SetSuppressRoomGameplayAutoLoad(true);
            GameManager.Instance.ChangeState(GameManager.GameState.RoomLobby);
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName)
            && SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        HideOverlayPanels();

        if (GameManager.Instance != null)
        {
            CloseOrLeaveCurrentRoom();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }

            GameManager.Instance.ClearCurrentRoom();
            GameManager.Instance.SetMultiplayerMode(false);
            GameManager.Instance.SetSuppressRoomGameplayAutoLoad(false);
            GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
        }

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName)
            && SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void CloseOrLeaveCurrentRoom()
    {
        if (GameManager.Instance == null || RoomClient.Instance == null)
            return;

        string roomCode = GameManager.Instance.CurrentRoomCode;
        if (string.IsNullOrEmpty(roomCode))
            return;

        if (!GameManager.Instance.IsMultiplayer)
            return;

        if (GameManager.Instance.IsHost)
            RoomClient.Instance.CloseRoom(roomCode);
        else
            RoomClient.Instance.LeaveRoom(roomCode);
    }

    private void ShowResultPanel(GameObject targetPanel)
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        HideResultPanels();

        if (targetPanel != null)
        {
            targetPanel.SetActive(true);
            if (ShouldPauseForResultPanel(targetPanel))
                Time.timeScale = 0f;
        }
    }

    private bool ShouldPauseForResultPanel(GameObject targetPanel)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsMultiplayer)
            return true;

        if (targetPanel == lostPanel)
            return PlayerHealth.IsSpawnedTeamLost();

        return false;
    }

    private void HideOverlayPanels()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        HideResultPanels();
        Time.timeScale = 1f;
    }

    private void HideResultPanels()
    {
        if (winPanel != null)
            winPanel.SetActive(false);

        if (lostPanel != null)
            lostPanel.SetActive(false);
    }

    private void OnPlayerDeath()
    {
        Debug.Log("Player died!");
        // TODO: Show death screen / respawn menu
    }
}
