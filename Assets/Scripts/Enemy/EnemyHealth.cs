using System;
using Unity.Netcode;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] EnemyData enemyData;
    [SerializeField] float maxHealth = 5f;
    [SerializeField] float destroyDelay = 0.8f;

    NetworkObject networkObject;
    float currentHealth;
    bool isDead;

    public event Action OnDied;
    public event Action<float, float> OnHealthChanged;
    public float CurrentHealth => currentHealth;
    public float MaxHealthValue => MaxHealth;
    public bool IsNetworkSpawned => networkObject != null && networkObject.IsSpawned;
    public bool IsDead => isDead;
    public EnemyData EnemyData => enemyData;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        EnsureHealthBar();
        ApplyEnemyDataToInspector();
        currentHealth = MaxHealth;
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    void Start()
    {
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
    }

    void OnValidate()
    {
        ApplyEnemyDataToInspector();
    }

    public void SetEnemyData(EnemyData data, bool resetHealth = true)
    {
        enemyData = data;
        ApplyEnemyDataToInspector();
        if (resetHealth)
        {
            isDead = false;
            currentHealth = MaxHealth;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;
        if (MultiplayerGameplaySync.IsNetworkActive && !MultiplayerGameplaySync.IsServer)
        {
            MultiplayerGameplaySync.RequestEnemyDamage(this, amount);
            return;
        }

        ApplyDamageAuthoritative(amount, true);
    }

    public void ApplyDamageAuthoritative(float amount, bool broadcast)
    {
        if (isDead || amount <= 0f) return;
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        EnemyDamagePopup.Show(transform.position, amount);
        GameAudioManager.Instance?.PlayMonsterScream(transform.position);
        if (broadcast)
            MultiplayerGameplaySync.BroadcastEnemyHealth(this, amount);
        if (currentHealth <= 0f)
            Die();
    }

    public void ApplyRemoteState(float health, float maxHealthValue, bool dead, float damageAmount, Vector3 popupPosition)
    {
        if (isDead) return;
        currentHealth = Mathf.Clamp(health, 0f, Mathf.Max(1f, maxHealthValue));
        OnHealthChanged?.Invoke(currentHealth, Mathf.Max(1f, maxHealthValue));
        if (damageAmount > 0f)
        {
            EnemyDamagePopup.Show(popupPosition, damageAmount);
            GameAudioManager.Instance?.PlayMonsterScream(popupPosition);
        }
        if (dead || currentHealth <= 0f)
            Die();
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        OnDied?.Invoke();
        GameAudioManager.Instance?.PlayKill(transform.position);
        GameAudioManager.Instance?.PlayMonsterDead(transform.position);

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
            col.enabled = false;

        if (networkObject != null && networkObject.IsSpawned)
            networkObject.Despawn(true);
        else
            Destroy(gameObject, DestroyDelay);
    }

    float MaxHealth => enemyData != null ? Mathf.Max(1f, enemyData.MaxHealth) : Mathf.Max(1f, maxHealth);
    float DestroyDelay => enemyData != null ? Mathf.Max(0f, enemyData.DestroyDelay) : Mathf.Max(0f, destroyDelay);

    void ApplyEnemyDataToInspector()
    {
        if (enemyData == null) return;
        maxHealth = Mathf.Max(1f, enemyData.MaxHealth);
        destroyDelay = Mathf.Max(0f, enemyData.DestroyDelay);
    }

    void EnsureHealthBar()
    {
        if (GetComponent<EnemyHealthBar>() == null)
            gameObject.AddComponent<EnemyHealthBar>();
    }
}
