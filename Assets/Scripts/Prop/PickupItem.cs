using System;
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
    [SerializeField] bool destroyOnPickup = true;

    [Header("Drop Animation")]
    [SerializeField] bool animateDrop = true;
    [SerializeField] float popDuration = 0.28f;
    [SerializeField] float popHeight = 0.18f;
    [SerializeField] float popScale = 1.25f;
    [SerializeField] float idleBobHeight = 0.035f;
    [SerializeField] float idleBobSpeed = 3.5f;

    bool picked;
    Vector3 basePosition;
    Vector3 baseScale;
    float spawnTime;
    float randomPhase;

    void Awake()
    {
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
        if (player.IsSpawned && !player.IsOwner && !player.IsServer) return;
        if (!CanPickup(player)) return;

        picked = true;
        ApplyPickup(player);

        if (destroyOnPickup)
            Destroy(gameObject);
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

    void ApplyPickup(PlayerHealth player)
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
                Coins += Mathf.Max(1, coinAmount);
                OnCoinsChanged?.Invoke(Coins);
                Debug.Log($"Coin collected. Total coins: {Coins}");
                break;
        }
    }
}
