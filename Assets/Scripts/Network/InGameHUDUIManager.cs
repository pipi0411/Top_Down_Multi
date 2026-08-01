using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InGameHUDUIManager : MonoBehaviour
{
    [SerializeField] private GameObject inGameHUD;

    [Header("HP Bar")]
    [SerializeField] private Image hpBarFill;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("Mana")]
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Hotbar")]
    [SerializeField] private HotbarUI hotbarPrefab;
    [SerializeField] private Sprite hotbarSlotSprite;

    [Header("MiniMap")]
    [SerializeField] private RawImage miniMap;

    [Header("Pause")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;

    private float currentHP = 100f;
    private float maxHP = 100f;
    private float currentMana = 100f;
    private float maxMana = 100f;
    private int currentAmmo = 30;
    private int maxAmmo = 30;
    private bool isReloading;
    private bool currentWeaponUsesAmmo = true;
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
        UpdateHPBar();
        UpdateMana();
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
                isReloading = localPlayerStats.IsReloading;
                currentWeaponUsesAmmo = localPlayerStats.CurrentWeaponUsesAmmo;
                UpdateHPBar();
                UpdateMana();
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
        }
        else
        {
            inGameHUD.SetActive(false);
            if (hotbarUI != null)
                hotbarUI.gameObject.SetActive(false);
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

    public void UpdateHPBar(float hp = -1f)
    {
        if (hp >= 0)
            currentHP = Mathf.Clamp(hp, 0, maxHP);

        if (hpBarFill != null)
        {
            hpBarFill.fillAmount = currentHP / maxHP;
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

    public void FireWeapon()
    {
        UpdateMana();
    }

    private void OnPauseClicked()
    {
        Debug.Log("Pause button clicked");
        
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

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        GameManager.Instance.ChangeState(GameManager.GameState.MainMenu);
    }

    private void OnPlayerDeath()
    {
        Debug.Log("Player died!");
        // TODO: Show death screen / respawn menu
    }
}
