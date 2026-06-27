using System.Reflection;
using UnityEngine;
using UnityEngine.UI; 
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Reference")]
    [SerializeField] private PlayerConfig playerConfig;
   
    [Header("UI Elements")]
    [SerializeField] private Image healthBar;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image armorBar;
    [SerializeField] private TextMeshProUGUI armorText;
    [SerializeField] private Image energyBar;
    [SerializeField] private TextMeshProUGUI energyText;
   
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPlayerConfig(PlayerConfig newConfig)
    {
        playerConfig = newConfig;
    }

    void Update()
    {
        if (playerConfig != null)
        {
            UpdatePlayerUI();
        }
    }
    private void UpdatePlayerUI()
    {
        healthBar.fillAmount = Mathf.Lerp(healthBar.fillAmount, playerConfig.CurrentHealth / playerConfig.MaxHealth, Time.deltaTime * 10f);
        armorBar.fillAmount = Mathf.Lerp(armorBar.fillAmount, playerConfig.Armor / playerConfig.MaxArmor, Time.deltaTime * 10f);
        energyBar.fillAmount = Mathf.Lerp(energyBar.fillAmount, playerConfig.Energy / playerConfig.MaxEnergy, Time.deltaTime * 10f);

        healthText.text = $"{playerConfig.CurrentHealth}/{playerConfig.MaxHealth}";
        armorText.text = $"{playerConfig.Armor}/{playerConfig.MaxArmor}";
        energyText.text = $"{playerConfig.Energy}/{playerConfig.MaxEnergy}";
    }
}
