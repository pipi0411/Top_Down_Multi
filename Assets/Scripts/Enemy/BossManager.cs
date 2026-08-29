using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BossManager : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Rigidbody2D rb;
    public Transform firePoint;
    public GameObject fireballPrefab;

    [Header("Health")]
    public int maxHealth = 50;
    public int currentHealth;
    public bool isInvincible = true;

    [Header("Ranges")]
    public float startFightRange = 12f;
    public float preferredRange = 6.5f;
    public float minRange = 3.5f;
    public float maxRange = 9f;

    [Header("Movement")]
    public float moveSpeed = 3.2f;
    public float retreatSpeed = 2.8f;

    [Header("Attack")]
    public float lastAttackTime;
    public float attackCooldown = 2.2f;
    public float fireballDelay = 0.4f;

    public bool IsDead => isDead;
    public bool IsFighting => isFighting;
    public event Action<BossManager> OnDied;

    Transform player;
    bool isFighting;
    bool isDead;
    Coroutine attackRoutine;
    NetworkObject networkObject;
    Vector3 initialScale;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        networkObject = GetComponent<NetworkObject>();
        initialScale = transform.localScale;
        EnsureFirePoint();
    }

    void Start()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        isInvincible = true;
        SetAnimatorBool("isFighting", false);
        SetAnimatorBool("isAttacking", false);
        RefreshTarget();
    }

    void Update()
    {
        if (isDead) return;
        if (!CanRunAuthoritativeLogic()) return;

        if (player == null)
            RefreshTarget();

        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (!isFighting && distance <= startFightRange)
            StartFight();

        if (isFighting)
            HandleMovementAndAttack(distance);
    }

    public void StartFight()
    {
        if (isDead) return;
        isFighting = true;
        isInvincible = false;
        SetAnimatorBool("isFighting", true);
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(Mathf.RoundToInt(damage));
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || isInvincible || isDead) return;

        if (MultiplayerGameplaySync.IsNetworkActive && !MultiplayerGameplaySync.IsServer)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        EnemyDamagePopup.Show(transform.position, damage);
        GameAudioManager.Instance?.PlayMonsterScream(transform.position);
        TriggerAnimator("Hurt");

        if (currentHealth <= 0)
            Die();
        else if (!isFighting)
            StartFight();
    }

    void HandleMovementAndAttack(float distance)
    {
        if (player == null) return;

        Vector2 directionToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        FaceTarget(directionToPlayer);

        if (distance > maxRange)
            Move(directionToPlayer, moveSpeed);
        else if (distance < minRange)
            Move(-directionToPlayer, retreatSpeed);

        if (distance <= maxRange && Time.time - lastAttackTime >= attackCooldown)
            Attack();
    }

    void Move(Vector2 direction, float speed)
    {
        SetAnimatorBool("isAttacking", false);
        Vector2 movement = direction * speed * Time.deltaTime;
        if (rb != null)
            rb.MovePosition(rb.position + movement);
        else
            transform.position += (Vector3)movement;
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        SetAnimatorBool("isAttacking", true);

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(ShootFireballDelay());
    }

    IEnumerator ShootFireballDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, fireballDelay));
        ShootFireball();
        SetAnimatorBool("isAttacking", false);
        attackRoutine = null;
    }

    void ShootFireball()
    {
        if (fireballPrefab == null || firePoint == null || player == null) return;

        GameObject fireball = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        NetworkObject fireballNetworkObject = fireball.GetComponent<NetworkObject>();
        if (fireballNetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            fireballNetworkObject.Spawn(true);

        Collider2D bossCollider = GetComponent<Collider2D>();
        Collider2D fireballCollider = fireball.GetComponent<Collider2D>();
        if (bossCollider != null && fireballCollider != null)
            Physics2D.IgnoreCollision(bossCollider, fireballCollider);

        Vector2 shootDirection = ((Vector2)player.position - (Vector2)firePoint.position).normalized;
        Fireball fireballComponent = fireball.GetComponent<Fireball>();
        if (fireballComponent != null)
            fireballComponent.SetDirection(shootDirection);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isInvincible = true;
        isFighting = false;
        OnDied?.Invoke(this);
        SetAnimatorBool("isAttacking", false);
        TriggerAnimator("Die");
        GameAudioManager.Instance?.PlayKill(transform.position);
        GameAudioManager.Instance?.PlayMonsterDead(transform.position);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;

        StartCoroutine(RemoveAfterDeath());
    }

    IEnumerator RemoveAfterDeath()
    {
        yield return new WaitForSeconds(1.5f);
        if (networkObject != null && networkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            networkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    bool CanRunAuthoritativeLogic()
    {
        return !MultiplayerGameplaySync.IsNetworkActive || MultiplayerGameplaySync.IsServer;
    }

    void RefreshTarget()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        float closestDistance = float.PositiveInfinity;
        Transform closest = null;
        foreach (PlayerHealth playerHealth in players)
        {
            if (playerHealth == null || playerHealth.IsDead) continue;
            float distance = Vector2.SqrMagnitude(playerHealth.transform.position - transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = playerHealth.transform;
            }
        }

        if (closest == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            closest = taggedPlayer != null ? taggedPlayer.transform : null;
        }

        player = closest;
    }

    void FaceTarget(Vector2 directionToPlayer)
    {
        if (Mathf.Abs(directionToPlayer.x) < 0.01f) return;
        Vector3 scale = transform.localScale;
        float baseX = Mathf.Abs(initialScale.x) > 0.001f ? Mathf.Abs(initialScale.x) : Mathf.Abs(scale.x);
        scale.x = directionToPlayer.x > 0f ? -baseX : baseX;
        transform.localScale = scale;
    }

    void EnsureFirePoint()
    {
        if (firePoint != null) return;
        Transform existing = transform.Find("FirePoint");
        if (existing != null)
        {
            firePoint = existing;
            return;
        }

        GameObject point = new GameObject("FirePoint");
        point.transform.SetParent(transform, false);
        point.transform.localPosition = new Vector3(0f, -0.15f, 0f);
        firePoint = point.transform;
    }

    void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return;
        animator.SetBool(parameterName, value);
    }

    void TriggerAnimator(string parameterName)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return;
        animator.SetTrigger(parameterName);
    }
}
