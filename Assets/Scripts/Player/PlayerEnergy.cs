using UnityEngine;
public class PlayerEnergy : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerConfig playerConfig;
    [SerializeField] private float regenerationDelayAfterCombat = 15f;
    [SerializeField] private float regenerationPerSecond = 1f;

    private bool isInCombat;
    private float lastCombatEndTime = float.NegativeInfinity;
    private float lastEnergyUseTime = float.NegativeInfinity;

    private void OnEnable()
    {
        Room.OnCombatStartedEvent += HandleCombatStarted;
        Room.OnCombatEndedEvent += HandleCombatEnded;
    }

    private void OnDisable()
    {
        Room.OnCombatStartedEvent -= HandleCombatStarted;
        Room.OnCombatEndedEvent -= HandleCombatEnded;
    }

    private void Start()
    {
        // Đồng bộ bản sao PlayerConfig đã được tạo ở PlayerHealth
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null && health.PlayerConfig != null)
        {
            playerConfig = health.PlayerConfig;
        }

        if (playerConfig != null)
        {
            playerConfig.Energy = playerConfig.MaxEnergy;
        }
    }

    private void Update()
    {
        RegenerateEnergyOutsideCombat();
    }
    public void UseEnergy(float amount)
    {
        if (playerConfig == null) return;
        playerConfig.Energy -= amount;
        lastEnergyUseTime = Time.time;
        if (playerConfig.Energy < 0)
        {
            playerConfig.Energy = 0;
        }
    }
    public bool TryUseEnergy(float amount)
    {
        if (playerConfig == null || playerConfig.Energy < amount)
        {
            return false;
        }
        UseEnergy(amount);
        return true;
    }
    public void RecoverEnergy(float amount)
    {
        if (playerConfig == null) return;
        playerConfig.Energy += amount;
        if (playerConfig.Energy > playerConfig.MaxEnergy)
        {
            playerConfig.Energy = playerConfig.MaxEnergy;
        }
    }

    private void RegenerateEnergyOutsideCombat()
    {
        if (playerConfig == null || isInCombat) return;
        if (Time.time - lastCombatEndTime < regenerationDelayAfterCombat) return;
        if (Time.time - lastEnergyUseTime < regenerationDelayAfterCombat) return;
        RecoverEnergy(regenerationPerSecond * Time.deltaTime);
    }

    private void HandleCombatStarted(Room room)
    {
        isInCombat = true;
    }

    private void HandleCombatEnded(Room room)
    {
        isInCombat = false;
        lastCombatEndTime = Time.time;
    }
}
