using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyStateMachine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] EnemyData enemyData;
    [SerializeField] Rigidbody2D rb;
    [SerializeField] Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] EnemyHealth health;

    [Header("Detection")]
    [SerializeField] float detectionRange = 6f;
    [SerializeField] float attackRange = 1.15f;
    [SerializeField] LayerMask lineOfSightMask = ~0;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 2.2f;
    [SerializeField] float idleWanderRadius = 1.2f;
    [SerializeField] float idleWanderInterval = 1.4f;

    [Header("Attack")]
    [SerializeField] float attackDamage = 1f;
    [SerializeField] float attackCooldown = 1f;

    [Header("Animator Parameters")]
    [SerializeField] string movingParameter = "IsMoving";
    [SerializeField] string attackTrigger = "Attack";
    [SerializeField] string deathTrigger = "Die";

    NetworkObject networkObject;
    IEnemyState currentState;
    EnemyIdleState idleState;
    EnemyChaseState chaseState;
    EnemyAttackState attackState;
    EnemyDeadState deadState;
    Vector2 moveDirection;
    float nextAttackTime;
    bool hasRangedWeapon;
    Collider2D bodyCollider;
    float nextTransformSyncTime;
    bool remoteMoving;
    const float meleeContactAttackPadding = 0.25f;
    const float transformSyncInterval = 0.08f;

    public PlayerHealth Target { get; private set; }
    public float IdleWanderRadius => enemyData != null ? enemyData.IdleWanderRadius : idleWanderRadius;
    public float IdleWanderInterval => enemyData != null ? enemyData.IdleWanderInterval : idleWanderInterval;
    public bool CanAttack => Time.time >= nextAttackTime;
    public bool CanSimulate
    {
        get
        {
            NetworkManager manager = NetworkManager.Singleton;
            return manager == null || !manager.IsListening || manager.IsServer;
        }
    }
    public string DeathTrigger => deathTrigger;

    void OnValidate()
    {
        CacheReferences();
        ApplyEnemyDataToInspector();
    }

    void Awake()
    {
        CacheReferences();
        networkObject = GetComponent<NetworkObject>();
        if (enemyData == null && health != null) enemyData = health.EnemyData;
        ApplyEnemyDataToInspector();
        if (health != null && enemyData != null) health.SetEnemyData(enemyData);
        hasRangedWeapon = GetComponentInChildren<EnemyWeapon>(false) != null;
        bodyCollider = GetComponent<Collider2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        idleState = new EnemyIdleState(this);
        chaseState = new EnemyChaseState(this);
        attackState = new EnemyAttackState(this);
        deadState = new EnemyDeadState(this);
    }

    void OnEnable()
    {
        if (health != null)
            health.OnDied += HandleDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDied -= HandleDied;
    }

    void Start()
    {
        ChangeState(idleState);
    }

    void Update()
    {
        if (!CanSimulate) return;
        currentState?.Tick();
        TryBroadcastTransform();
    }

    void FixedUpdate()
    {
        if (!CanSimulate) return;
        currentState?.FixedTick();
        TryBroadcastTransform();
    }

    public void ChangeState(IEnemyState nextState)
    {
        if (currentState == nextState) return;
        currentState?.Exit();
        currentState = nextState;
        currentState?.Enter();
    }

    public void GoIdle() => ChangeState(idleState);
    public void ChaseTarget() => ChangeState(chaseState);
    public void AttackTarget() => ChangeState(attackState);

    public void SetEnemyData(EnemyData data)
    {
        enemyData = data;
        ApplyEnemyDataToInspector();
        if (health != null)
            health.SetEnemyData(data);
    }

    public bool TryFindTarget()
    {
        PlayerHealth closest = null;
        float closestDistance = float.MaxValue;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        foreach (PlayerHealth player in players)
        {
            if (player == null || player.CurrentHealth <= 0f) continue;

            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance > DetectionRange || distance >= closestDistance) continue;
            if (!HasLineOfSight(player.transform)) continue;

            closest = player;
            closestDistance = distance;
        }

        Target = closest;
        return Target != null;
    }

    public bool TargetInAttackRange()
    {
        if (Target == null) return false;

        float centerDistance = Vector2.Distance(transform.position, Target.transform.position);
        if (centerDistance <= AttackRange) return true;

        if (hasRangedWeapon) return false;

        return GetColliderDistanceToTarget(Target) <= meleeContactAttackPadding;
    }

    public void MoveTowards(Vector2 worldPosition)
    {
        Vector2 currentPosition = rb.position;
        moveDirection = (worldPosition - currentPosition).normalized;
        rb.MovePosition(currentPosition + moveDirection * (MoveSpeed * Time.fixedDeltaTime));
        SetMoving(true);
        FaceDirection(moveDirection);
    }

    public void MoveInDirection(Vector2 direction)
    {
        moveDirection = direction.normalized;
        rb.MovePosition(rb.position + moveDirection * (MoveSpeed * Time.fixedDeltaTime));
        SetMoving(moveDirection.sqrMagnitude > 0.001f);
        FaceDirection(moveDirection);
    }

    public void StopMoving()
    {
        moveDirection = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        SetMoving(false);
    }

    public void DealAttackDamage()
    {
        if (hasRangedWeapon) return;
        if (Target == null || !CanAttack || !TargetInAttackRange()) return;
        nextAttackTime = Time.time + AttackCooldown;
        SetTrigger(attackTrigger);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !NetworkManager.Singleton.IsServer)
            return;

        Target.TakeDamage(AttackDamage);
    }

    public void ApplyRemoteTransform(Vector3 worldPosition, bool facingLeft, bool moving)
    {
        if (CanSimulate) return;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = worldPosition;
        }
        else
        {
            transform.position = worldPosition;
        }

        ApplyFacing(facingLeft);
        if (remoteMoving != moving)
        {
            remoteMoving = moving;
            SetMoving(moving);
        }
    }

    void TryBroadcastTransform()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer) return;
        if (Time.unscaledTime < nextTransformSyncTime) return;

        nextTransformSyncTime = Time.unscaledTime + transformSyncInterval;
        MultiplayerGameplaySync.BroadcastEnemyTransform(this, transform.position, IsFacingLeft(), moveDirection.sqrMagnitude > 0.001f);
    }

    bool HasLineOfSight(Transform target)
    {
        Vector2 origin = transform.position;
        Vector2 targetPosition = target.position;
        Vector2 direction = targetPosition - origin;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, direction.magnitude, lineOfSightMask);
        return hit.collider == null || hit.collider.GetComponentInParent<PlayerHealth>() != null;
    }

    float GetColliderDistanceToTarget(PlayerHealth target)
    {
        if (target == null) return float.MaxValue;
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();

        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        if (bodyCollider == null || targetCollider == null)
            return Vector2.Distance(transform.position, target.transform.position);

        ColliderDistance2D distance = bodyCollider.Distance(targetCollider);
        return Mathf.Max(0f, distance.distance);
    }

    void HandleDied()
    {
        ChangeState(deadState);
    }

    void FaceDirection(Vector2 direction)
    {
        if (spriteRenderer == null || Mathf.Abs(direction.x) < 0.05f) return;
        ApplyFacing(direction.x < 0f);
    }

    void ApplyFacing(bool facingLeft)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = facingLeft;
    }

    bool IsFacingLeft()
    {
        return spriteRenderer != null && spriteRenderer.flipX;
    }

    void SetMoving(bool moving)
    {
        if (animator != null && HasAnimatorParameter(movingParameter, AnimatorControllerParameterType.Bool))
            animator.SetBool(movingParameter, moving);
    }

    public void SetTrigger(string triggerName)
    {
        if (animator != null && HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(triggerName);
    }

    bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(parameterName) || animator == null) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.type == type && parameter.name == parameterName) return true;
        return false;
    }

    float DetectionRange => enemyData != null ? enemyData.DetectionRange : detectionRange;
    float AttackRange => enemyData != null ? enemyData.AttackRange : attackRange;
    float MoveSpeed => enemyData != null ? enemyData.MoveSpeed : moveSpeed;
    float AttackDamage => enemyData != null ? enemyData.AttackDamage : attackDamage;
    float AttackCooldown => enemyData != null ? enemyData.AttackCooldown : attackCooldown;

    void CacheReferences()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (health == null) health = GetComponent<EnemyHealth>();
    }

    void ApplyEnemyDataToInspector()
    {
        if (enemyData == null) return;

        detectionRange = Mathf.Max(0f, enemyData.DetectionRange);
        attackRange = Mathf.Max(0f, enemyData.AttackRange);
        moveSpeed = Mathf.Max(0f, enemyData.MoveSpeed);
        idleWanderRadius = Mathf.Max(0f, enemyData.IdleWanderRadius);
        idleWanderInterval = Mathf.Max(0f, enemyData.IdleWanderInterval);
        attackDamage = Mathf.Max(0f, enemyData.AttackDamage);
        attackCooldown = Mathf.Max(0f, enemyData.AttackCooldown);

        if (health != null && health.EnemyData != enemyData)
            health.SetEnemyData(enemyData, false);
    }
}
