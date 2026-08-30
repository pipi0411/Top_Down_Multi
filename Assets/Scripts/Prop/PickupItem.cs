using System;
using Unity.Netcode;
using UnityEngine;

public class PickupItem : MonoBehaviour
{
    public enum PickupType
    {
        Health,
        Mana,
        Shield,
        Coin
    }

    public static int Coins { get; private set; }
    public static event Action<int> OnCoinsChanged;

    [SerializeField] PickupType pickupType;
    [SerializeField] float amount = 1f;
    [SerializeField] int coinAmount = 1;
    [SerializeField] int minCoinAmount = 10;
    [SerializeField] int maxCoinAmount = 20;
    [SerializeField] bool destroyOnPickup = true;

    [Header("Drop Animation")]
    [SerializeField] bool animateDrop = true;
    [SerializeField] float popDuration = 0.28f;
    [SerializeField] float popHeight = 0.18f;
    [SerializeField] float popScale = 1.25f;
    [SerializeField] float idleBobHeight = 0.035f;
    [SerializeField] float idleBobSpeed = 3.5f;

    bool picked;
    NetworkObject networkObject;
    Vector3 basePosition;
    Vector3 baseScale;
    float spawnTime;
    float randomPhase;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        basePosition = transform.position;
        baseScale = transform.localScale;
        spawnTime = Time.time;
        randomPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        if (!animateDrop || picked) return;

        float age = Time.time - spawnTime;
        if (age < popDuration)
        {
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, popDuration));
            float arc = Mathf.Sin(t * Mathf.PI) * popHeight;
            float scalePulse = Mathf.Sin(t * Mathf.PI) * (popScale - 1f);

            transform.position = basePosition + Vector3.up * arc;
            transform.localScale = baseScale * (1f + scalePulse);
            return;
        }

        float bob = Mathf.Sin((age - popDuration) * idleBobSpeed + randomPhase) * idleBobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.localScale = baseScale;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (picked) return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null || player.IsDead) return;
        if (player.IsSpawned && !player.IsOwner) return;

        if (MultiplayerGameplaySync.IsNetworkActive)
        {
            MultiplayerGameplaySync.RequestPickup(this, player);
            return;
        }

        if (!CanPickup(player)) return;

        picked = true;
        ApplyPickup(player);

        if (destroyOnPickup)
            Destroy(gameObject);
    }

    public bool TryPickupAuthoritative(PlayerHealth player, ulong ownerClientId)
    {
        if (picked || player == null || player.IsDead || !CanPickup(player)) return false;

        picked = true;
        ApplyPickup(player, ownerClientId);

        if (destroyOnPickup)
        {
            if (networkObject != null && networkObject.IsSpawned)
                networkObject.Despawn(true);
            else
            {
                MultiplayerGameplaySync.BroadcastPickupConsumed(this);
                Destroy(gameObject);
            }
        }

        return true;
    }

    public void ConsumeRemote()
    {
        if (picked) return;
        picked = true;
        if (destroyOnPickup)
            Destroy(gameObject);
    }

    public static void AddCoinsLocal(int amount)
    {
        Coins += Mathf.Max(1, amount);
        OnCoinsChanged?.Invoke(Coins);
        SaveGameManager.AutoSave("Coin collected");
    }

    public static void SetCoinsLocal(int amount)
    {
        Coins = Mathf.Max(0, amount);
        OnCoinsChanged?.Invoke(Coins);
    }

    bool CanPickup(PlayerHealth player)
    {
        switch (pickupType)
        {
            case PickupType.Health:
                return player.CanRecoverHealth(amount);
            case PickupType.Mana:
                return player.CanRecoverEnergy(amount);
            case PickupType.Shield:
                return player.CanRecoverArmor(amount);
            case PickupType.Coin:
                return true;
            default:
                return false;
        }
    }

    void ApplyPickup(PlayerHealth player, ulong ownerClientId = ulong.MaxValue)
    {
        switch (pickupType)
        {
            case PickupType.Health:
                player.RecoverHealth(amount);
                break;
            case PickupType.Mana:
                player.RecoverEnergy(amount);
                break;
            case PickupType.Shield:
                player.RecoverArmor(amount);
                break;
            case PickupType.Coin:
                int gained = GetCoinAmount();
                if (MultiplayerGameplaySync.IsNetworkActive && MultiplayerGameplaySync.IsServer && ownerClientId != ulong.MaxValue)
                {
                    MultiplayerGameplaySync.DistributeCoinGain(ownerClientId, gained);
                }
                else
                    AddCoinsLocal(gained);
                Debug.Log($"Coin collected. Total coins: {Coins}");
                break;
        }
    }

    int GetCoinAmount()
    {
        int min = Mathf.Max(1, minCoinAmount);
        int max = Mathf.Max(min, maxCoinAmount);

        if (max > min)
            return UnityEngine.Random.Range(min, max + 1);

        return Mathf.Max(1, Mathf.Max(coinAmount, min));
    }
}
