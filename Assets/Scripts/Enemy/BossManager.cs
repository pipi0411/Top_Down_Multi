using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BossManager : NetworkBehaviour
{
    enum BossSkill
    {
        SingleFireball,
        AoeFireball,
        Summon,
        Dash
    }

    [Header("References")]
    public Animator animator;
    public Rigidbody2D rb;
    public Transform firePoint;
    public GameObject fireballPrefab;
    public GameObject[] summonPrefabs;

    [Header("Health")]
    public int maxHealth = 50;
    public int currentHealth;
    public bool isInvincible = true;

    [Header("Phase 2")]
    [Range(0.05f, 0.95f)]
    public float phase2HealthPercent = 0.5f;
    public float phase2MoveMultiplier = 1.25f;
    public float phase2AttackCooldownMultiplier = 0.8f;

    [Header("Ranges")]
    public float startFightRange = 12f;
    public float preferredRange = 6.5f;
    public float minRange = 3.5f;
    public float maxRange = 9f;

    [Header("Movement")]
    public float moveSpeed = 1.75f;
    public float retreatSpeed = 1.45f;

    [Header("Collision")]
    public LayerMask movementBlockMask = (1 << 6) | (1 << 7);
    public float movementSkin = 0.04f;

    [Header("Single Fireball")]
    public float lastAttackTime;
    public float attackCooldown = 2.2f;
    public float fireballDelay = 0.4f;

    [Header("AOE Fireball")]
    public float aoeCooldown = 6f;
    public int aoeFireballCount = 7;
    public float aoeSpreadAngle = 90f;
    public float aoeDelay = 0.5f;

    [Header("Summon")]
    public float summonCooldown = 9f;
    public int summonCount = 2;
    public float summonRadius = 2.2f;

    [Header("Dash")]
    public float dashCooldown = 7f;
    public float dashSpeed = 8f;
    public float dashDuration = 0.35f;
    public float dashDamage = 3f;
    public float dashHitRadius = 0.8f;

    public bool IsDead => isDead;
    public bool IsFighting => isFighting;
    public bool IsPhase2 => currentHealth <= Mathf.CeilToInt(maxHealth * phase2HealthPercent);
    public event Action<BossManager> OnDied;
    public event Action<int, int> OnHealthChanged;

    readonly NetworkVariable<int> networkHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    Transform player;
    bool isFighting;
    bool isDead;
    bool isUsingSkill;
    bool isDashing;
    bool remoteTrailActive;
    Coroutine skillRoutine;
    Vector3 initialScale;
    BossDashTrail dashTrail;
    Vector2 desiredMoveDirection;
    float desiredMoveSpeed;
    float lastAoeTime = -999f;
    float lastSummonTime = -999f;
    float lastDashTime = -999f;
    float summonReadyTime;
    Vector2 dashDirection;
    float dashEndTime;
    float nextBossStateSyncTime;
    readonly List<EnemyHealth> activeSummons = new List<EnemyHealth>();
    readonly HashSet<PlayerHealth> dashHitPlayers = new HashSet<PlayerHealth>();
    readonly RaycastHit2D[] movementHits = new RaycastHit2D[8];
    const float BossStateSyncInterval = 0.06f;

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        ConfigureRigidbody();
        initialScale = transform.localScale;
        dashTrail = GetComponent<BossDashTrail>();
        if (dashTrail == null) dashTrail = gameObject.AddComponent<BossDashTrail>();
        EnsureFirePoint();
        EnsureHealthBar();
    }

    void Start()
    {
        if (!IsSpawned)
            InitializeHealth();

        SetAnimatorBool("isFighting", false);
        SetAnimatorBool("isAttacking", false);
        RefreshTarget();
    }

    public override void OnNetworkSpawn()
    {
        networkHealth.OnValueChanged += HandleNetworkHealthChanged;
        if (IsServer)
            InitializeHealth();
        else
            ApplyHealth(networkHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkHealth.OnValueChanged -= HandleNetworkHealthChanged;
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

        if (isFighting && !isDashing)
            HandleMovementAndSkill(distance);

        TrySyncBossState();
    }

    void FixedUpdate()
    {
        if (isDead || !CanRunAuthoritativeLogic()) return;

        if (isDashing)
        {
            TickDash();
            TrySyncBossState();
            return;
        }

        if (desiredMoveSpeed <= 0f || desiredMoveDirection.sqrMagnitude <= 0.0001f) return;
        MoveBody(desiredMoveDirection.normalized * desiredMoveSpeed * Time.fixedDeltaTime);
        TrySyncBossState();
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
        if (MultiplayerGameplaySync.IsNetworkActive && !MultiplayerGameplaySync.IsServer) return;

        ApplyHealth(Mathf.Max(0, currentHealth - damage));
        EnemyDamagePopup.Show(transform.position, damage);
        GameAudioManager.Instance?.PlayMonsterScream(transform.position);
        TriggerAnimator("Hurt");

        if (IsSpawned && IsServer)
        {
            networkHealth.Value = currentHealth;
            BossHurtClientRpc(currentHealth);
        }

        if (currentHealth <= 0)
            Die();
        else if (!isFighting)
            StartFight();
    }

    void InitializeHealth()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        int initialHealth = currentHealth > 0 ? Mathf.Min(currentHealth, maxHealth) : maxHealth;
        ApplyHealth(initialHealth);
        if (IsSpawned && IsServer)
            networkHealth.Value = currentHealth;
    }

    void ApplyHealth(int value)
    {
        currentHealth = Mathf.Clamp(value, 0, Mathf.Max(1, maxHealth));
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void HandleNetworkHealthChanged(int previousValue, int newValue)
    {
        ApplyHealth(newValue);
    }

    void HandleMovementAndSkill(float distance)
    {
        Vector2 directionToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        FaceTarget(directionToPlayer);

        if (!isUsingSkill)
        {
            if (distance > maxRange)
                SetMove(directionToPlayer, EffectiveMoveSpeed(moveSpeed));
            else if (distance < minRange)
                SetMove(-directionToPlayer, EffectiveMoveSpeed(retreatSpeed));
            else
                StopMove();

            BossSkill skill = ChooseSkill(distance);
            StartSkill(skill);
        }
        else
        {
            StopMove();
        }
    }

    BossSkill ChooseSkill(float distance)
    {
        bool phase2 = IsPhase2;
        if (phase2 && distance <= maxRange + 2f && Time.time - lastDashTime >= dashCooldown)
            return BossSkill.Dash;

        if (CanSummon())
            return BossSkill.Summon;

        if (distance <= maxRange && Time.time - lastAoeTime >= aoeCooldown)
            return BossSkill.AoeFireball;

        if (distance <= maxRange && Time.time - lastAttackTime >= EffectiveAttackCooldown())
            return BossSkill.SingleFireball;

        return BossSkill.SingleFireball;
    }

    void StartSkill(BossSkill skill)
    {
        if (isUsingSkill) return;

        switch (skill)
        {
            case BossSkill.Dash:
                lastDashTime = Time.time;
                StartDash();
                break;
            case BossSkill.Summon:
                if (!CanSummon()) return;
                lastSummonTime = Time.time;
                summonReadyTime = float.PositiveInfinity;
                skillRoutine = StartCoroutine(SummonDelay());
                break;
            case BossSkill.AoeFireball:
                if (Time.time - lastAoeTime < aoeCooldown) return;
                lastAoeTime = Time.time;
                skillRoutine = StartCoroutine(AoeFireballDelay());
                break;
            default:
                if (Time.time - lastAttackTime < EffectiveAttackCooldown()) return;
                lastAttackTime = Time.time;
                skillRoutine = StartCoroutine(SingleFireballDelay());
                break;
        }
    }

    IEnumerator SingleFireballDelay()
    {
        isUsingSkill = true;
        SetAnimatorBool("isAttacking", true);
        yield return new WaitForSeconds(Mathf.Max(0f, fireballDelay));
        ShootFireballAtPlayer();
        EndSkill();
    }

    IEnumerator AoeFireballDelay()
    {
        isUsingSkill = true;
        SetAnimatorBool("isAttacking", true);
        yield return new WaitForSeconds(Mathf.Max(0f, aoeDelay));
        ShootAoeFireballs();
        EndSkill();
    }

    IEnumerator SummonDelay()
    {
        isUsingSkill = true;
        SetAnimatorBool("isAttacking", true);
        yield return new WaitForSeconds(0.45f);
        SummonMinions();
        EndSkill();
    }

    void EndSkill()
    {
        SetAnimatorBool("isAttacking", false);
        isUsingSkill = false;
        skillRoutine = null;
    }

    void ShootFireballAtPlayer()
    {
        if (player == null || firePoint == null) return;
        Vector2 shootDirection = ((Vector2)player.position - (Vector2)firePoint.position).normalized;
        SpawnFireball(shootDirection);
    }

    void ShootAoeFireballs()
    {
        if (player == null || firePoint == null) return;

        int count = Mathf.Max(3, aoeFireballCount);
        Vector2 centerDirection = ((Vector2)player.position - (Vector2)firePoint.position).normalized;
        float centerAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;
        float spread = Mathf.Clamp(aoeSpreadAngle, 0f, 360f);
        float startAngle = centerAngle - spread * 0.5f;
        float step = count <= 1 ? 0f : spread / (count - 1);

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + step * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            SpawnFireball(direction);
        }
    }

    void SpawnFireball(Vector2 shootDirection)
    {
        if (fireballPrefab == null || firePoint == null) return;

        GameObject fireball = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);
        Fireball fireballComponent = fireball.GetComponent<Fireball>();
        if (fireballComponent != null)
            fireballComponent.SetDirection(shootDirection);

        Collider2D bossCollider = GetComponent<Collider2D>();
        Collider2D fireballCollider = fireball.GetComponent<Collider2D>();
        if (bossCollider != null && fireballCollider != null)
            Physics2D.IgnoreCollision(bossCollider, fireballCollider);

        NetworkObject fireballNetworkObject = fireball.GetComponent<NetworkObject>();
        if (fireballNetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
            fireballNetworkObject.Spawn(true);
    }

    void SummonMinions()
    {
        CleanupSummons();
        if (summonPrefabs == null || summonPrefabs.Length == 0)
        {
            summonReadyTime = Time.time + Mathf.Max(0f, summonCooldown);
            return;
        }

        int spawnedCount = 0;
        int count = Mathf.Max(1, summonCount);
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = summonPrefabs[UnityEngine.Random.Range(0, summonPrefabs.Length)];
            if (prefab == null) continue;

            float angle = (360f / count) * i + UnityEngine.Random.Range(-25f, 25f);
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * summonRadius;
            GameObject minion = Instantiate(prefab, (Vector2)transform.position + offset, Quaternion.identity);

            EnemyHealth minionHealth = minion.GetComponentInChildren<EnemyHealth>();
            if (minionHealth != null)
            {
                minionHealth.OnDied -= HandleSummonedEnemyDied;
                minionHealth.OnDied += HandleSummonedEnemyDied;
                activeSummons.Add(minionHealth);
            }

            NetworkObject minionNetworkObject = minion.GetComponent<NetworkObject>();
            if (minionNetworkObject != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && NetworkManager.Singleton.IsServer)
                minionNetworkObject.Spawn(true);

            spawnedCount++;
        }

        if (spawnedCount == 0 || activeSummons.Count == 0)
            summonReadyTime = Time.time + Mathf.Max(0f, summonCooldown);
    }

    bool CanSummon()
    {
        CleanupSummons();
        return summonPrefabs != null
            && summonPrefabs.Length > 0
            && activeSummons.Count == 0
            && Time.time >= summonReadyTime;
    }

    void CleanupSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            EnemyHealth summon = activeSummons[i];
            if (summon == null || summon.IsDead)
            {
                if (summon != null)
                    summon.OnDied -= HandleSummonedEnemyDied;
                activeSummons.RemoveAt(i);
            }
        }
    }

    void HandleSummonedEnemyDied()
    {
        CleanupSummons();
        if (activeSummons.Count == 0)
            summonReadyTime = Time.time + Mathf.Max(0f, summonCooldown);
    }

    void StartDash()
    {
        if (player == null) return;

        isUsingSkill = true;
        isDashing = true;
        dashHitPlayers.Clear();
        dashDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;
        dashEndTime = Time.time + Mathf.Max(0.05f, dashDuration);
        StopMove();
        SetAnimatorBool("isAttacking", true);
        dashTrail?.Begin();
    }

    void TickDash()
    {
        MoveBody(dashDirection * Mathf.Max(0f, dashSpeed) * Time.fixedDeltaTime);
        DamagePlayersDuringDash();

        if (Time.time >= dashEndTime)
        {
            isDashing = false;
            dashTrail?.End();
            EndSkill();
        }
    }

    void DamagePlayersDuringDash()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.1f, dashHitRadius));
        foreach (Collider2D hit in hits)
        {
            PlayerHealth playerHealth = hit != null ? hit.GetComponentInParent<PlayerHealth>() : null;
            if (playerHealth == null || playerHealth.IsDead || dashHitPlayers.Contains(playerHealth)) continue;
            dashHitPlayers.Add(playerHealth);
            playerHealth.TakeDamage(dashDamage);
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        isInvincible = true;
        isFighting = false;
        StopMove();
        dashTrail?.End();
        OnDied?.Invoke(this);
        SetAnimatorBool("isAttacking", false);
        TriggerAnimator("Die");
        if (IsSpawned && IsServer)
            BossDieClientRpc();
        GameAudioManager.Instance?.PlayKill(transform.position);
        GameAudioManager.Instance?.PlayMonsterDead(transform.position);

        if (skillRoutine != null)
        {
            StopCoroutine(skillRoutine);
            skillRoutine = null;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;

        UnsubscribeSummons();
        StartCoroutine(RemoveAfterDeath());
    }

    void UnsubscribeSummons()
    {
        foreach (EnemyHealth summon in activeSummons)
        {
            if (summon != null)
                summon.OnDied -= HandleSummonedEnemyDied;
        }
        activeSummons.Clear();
    }

    IEnumerator RemoveAfterDeath()
    {
        yield return new WaitForSeconds(1.5f);
        if (NetworkObject != null && NetworkObject.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }

    float EffectiveMoveSpeed(float baseSpeed)
    {
        return IsPhase2 ? baseSpeed * Mathf.Max(1f, phase2MoveMultiplier) : baseSpeed;
    }

    float EffectiveAttackCooldown()
    {
        return IsPhase2 ? attackCooldown * Mathf.Clamp(phase2AttackCooldownMultiplier, 0.2f, 1f) : attackCooldown;
    }

    bool CanRunAuthoritativeLogic()
    {
        return !MultiplayerGameplaySync.IsNetworkActive || MultiplayerGameplaySync.IsServer;
    }

    void MoveBody(Vector2 movement)
    {
        if (movement.sqrMagnitude <= 0.000001f) return;

        if (rb == null)
        {
            transform.position += (Vector3)movement;
            return;
        }

        Vector2 direction = movement.normalized;
        float distance = movement.magnitude;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(movementBlockMask);
        filter.useTriggers = false;

        int hitCount = rb.Cast(direction, filter, movementHits, distance + movementSkin);
        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = movementHits[i];
            if (hit.collider == null || hit.collider.isTrigger) continue;
            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - movementSkin));
        }

        if (allowedDistance > 0f)
            rb.MovePosition(rb.position + direction * allowedDistance);
    }

    void SetMove(Vector2 direction, float speed)
    {
        SetAnimatorBool("isAttacking", false);
        desiredMoveDirection = direction;
        desiredMoveSpeed = Mathf.Max(0f, speed);
    }

    void StopMove()
    {
        desiredMoveDirection = Vector2.zero;
        desiredMoveSpeed = 0f;
    }

    void ConfigureRigidbody()
    {
        if (rb == null) return;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void TrySyncBossState()
    {
        if (!IsSpawned || !IsServer) return;
        if (Time.unscaledTime < nextBossStateSyncTime) return;

        nextBossStateSyncTime = Time.unscaledTime + BossStateSyncInterval;
        BossStateClientRpc(transform.position, transform.localScale.x, isFighting, isUsingSkill, isDashing, currentHealth);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BossStateClientRpc(Vector3 position, float scaleX, bool fighting, bool usingSkill, bool dashing, int health)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

        transform.position = Vector3.Lerp(transform.position, position, 0.65f);
        Vector3 scale = transform.localScale;
        float baseX = Mathf.Abs(initialScale.x) > 0.001f ? Mathf.Abs(initialScale.x) : Mathf.Abs(scale.x);
        scale.x = scaleX < 0f ? -baseX : baseX;
        transform.localScale = scale;

        isFighting = fighting;
        isUsingSkill = usingSkill;
        isDashing = dashing;
        ApplyHealth(health);
        SetAnimatorBool("isFighting", fighting);
        SetAnimatorBool("isAttacking", usingSkill || dashing);
        ApplyRemoteDashTrail(dashing);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BossHurtClientRpc(int health)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;
        ApplyHealth(health);
        TriggerAnimator("Hurt");
    }

    [Rpc(SendTo.ClientsAndHost)]
    void BossDieClientRpc()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) return;

        isDead = true;
        isDashing = false;
        ApplyRemoteDashTrail(false);
        SetAnimatorBool("isAttacking", false);
        TriggerAnimator("Die");

        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
    }

    void ApplyRemoteDashTrail(bool active)
    {
        if (dashTrail == null) return;
        if (active == remoteTrailActive) return;

        remoteTrailActive = active;
        if (active)
            dashTrail.Begin();
        else
            dashTrail.End();
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

    void EnsureHealthBar()
    {
        if (GetComponent<BossHealthBar>() == null)
            gameObject.AddComponent<BossHealthBar>();
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
