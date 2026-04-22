using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerConfig playerConfig;
    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            TakeDamage(1f);
        }
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            RecoverHealth(1f);
        }
    }
    public void RecoverHealth(float amount)
    {
        playerConfig.CurrentHealth += amount;
        if (playerConfig.CurrentHealth > playerConfig.MaxHealth)
        {
            playerConfig.CurrentHealth = playerConfig.MaxHealth;
        }
    }
    public void TakeDamage(float amount)
    {
        if (playerConfig.Armor > 0)
        {
            //Break aromor
            // Damage health
            float remainningDamage = amount - playerConfig.Armor;
            playerConfig.Armor =Mathf.Max(playerConfig.Armor - amount, 0);
            if (remainningDamage > 0)
            {
                playerConfig.CurrentHealth = Mathf.Max(playerConfig.CurrentHealth - remainningDamage, 0);
            }
        }
        else
        {
            // Damage health
            playerConfig.CurrentHealth = Mathf.Max(playerConfig.CurrentHealth - amount, 0);
        }
        if (playerConfig.CurrentHealth <= 0)
        {
            PlayerDead();
        }

    }
    private void  PlayerDead()
    {
        Destroy(gameObject);
    }

}
