using Unity.Netcode;
using UnityEngine;

public class Fireball : NetworkBehaviour
{
    public float speed = 8f;
    public int damage = 15;
    public float lifeTime = 4f;
    public float visualRotationOffset = 180f;

    readonly NetworkVariable<Vector2> networkDirection = new NetworkVariable<Vector2>(
        Vector2.right,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    Vector2 direction = Vector2.right;
    Rigidbody2D rb;
    bool canHit;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (CanRunAuthoritativeLogic())
            Invoke(nameof(Expire), lifeTime);
        Invoke(nameof(EnableHit), 0.05f);
    }

    public override void OnNetworkSpawn()
    {
        networkDirection.OnValueChanged += HandleDirectionChanged;
        ApplyDirection(networkDirection.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkDirection.OnValueChanged -= HandleDirectionChanged;
    }

    void EnableHit()
    {
        canHit = true;
    }

    public void SetDirection(Vector2 dir)
    {
        Vector2 newDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        ApplyDirection(newDirection);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            networkDirection.Value = newDirection;
    }

    void HandleDirectionChanged(Vector2 previousValue, Vector2 newValue)
    {
        ApplyDirection(newValue);
    }

    void ApplyDirection(Vector2 newDirection)
    {
        direction = newDirection.sqrMagnitude > 0.0001f ? newDirection.normalized : Vector2.right;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + visualRotationOffset);
    }

    void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = direction * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canHit || other == null) return;
        if (!CanRunAuthoritativeLogic()) return;

        if (other.GetComponentInParent<BossManager>() != null || other.GetComponentInParent<Fireball>() != null)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            DestroyAuthoritative();
            return;
        }

        DestroyAuthoritative();
    }

    bool CanRunAuthoritativeLogic()
    {
        return !MultiplayerGameplaySync.IsNetworkActive || MultiplayerGameplaySync.IsServer;
    }

    void DestroyAuthoritative()
    {
        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    void Expire()
    {
        DestroyAuthoritative();
    }
}
