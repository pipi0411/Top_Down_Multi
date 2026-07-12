using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerEnergy : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerConfig playerConfig;

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
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            UseEnergy(1f);
        }
        if (Keyboard.current.jKey.wasPressedThisFrame)
        {
            RecoverEnergy(1f);
        }
    }
    public void UseEnergy(float amount)
    {
        playerConfig.Energy -= amount;
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
        playerConfig.Energy += amount;
        if (playerConfig.Energy > playerConfig.MaxEnergy)
        {
            playerConfig.Energy = playerConfig.MaxEnergy;
        }
    }
}
