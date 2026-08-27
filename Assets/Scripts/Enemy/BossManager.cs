using UnityEngine;
using System.Collections;

public class BossManager : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Rigidbody2D rb;
    public Transform firePoint;               // Vị trí miệng Boss (tạo Empty Object)
    public GameObject fireballPrefab;         // Prefab quả cầu lửa

    [Header("Health")]
    public int maxHealth = 50;
    public int currentHealth;
    public bool isInvincible = true;

    [Header("Ranges")]
    public float startFightRange = 12f;
    public float preferredRange = 6.5f;      // Tầm bắn lý tưởng
    public float minRange = 3.5f;            // Nếu gần hơn mức này thì lùi ra
    public float maxRange = 9f;              // Nếu xa hơn mức này thì bay lại gần

    [Header("Movement")]
    public float moveSpeed = 3.2f;
    public float retreatSpeed = 2.8f;        // Tốc độ lùi

    [Header("Attack")]
    public float lastAttackTime;
    public float attackCooldown = 2.2f;

    // Private
    private Transform player;
    private bool isFighting = false;
    private bool isDead = false;

    void Start()
    {
        currentHealth = maxHealth;

        if (animator == null) animator = GetComponent<Animator>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        // Tìm Player bằng Tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogError("Không tìm thấy object nào có Tag 'Player'!");
        }

        isInvincible = true;
        animator.SetBool("isFighting", false);
    }

    void Update()
    {
        if (isDead || player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (!isFighting && distance <= startFightRange)
        {
            StartFight();
        }

        if (!isFighting) return;

        HandleMovementAndAttack(distance);
    }

    void StartFight()
    {
        isFighting = true;
        isInvincible = false;
        animator.SetBool("isFighting", true);
    }

    void HandleMovementAndAttack(float distance)
{
    if (player == null) return;

    Vector2 directionToPlayer = (player.position - transform.position).normalized;

    // Lật sprite (đổi dấu nếu bị ngược đầu)
    if (directionToPlayer.x != 0)
    {
        Vector3 scale = transform.localScale;
        scale.x = directionToPlayer.x > 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    // --- Di chuyển giữ khoảng cách ---
    if (distance > maxRange)
    {
        // Quá xa → bay lại gần
        animator.SetBool("isAttacking", false);
        rb.MovePosition(rb.position + directionToPlayer * moveSpeed * Time.fixedDeltaTime);
    }
    else if (distance < minRange)
    {
        // Quá gần → lùi ra
        animator.SetBool("isAttacking", false);
        rb.MovePosition(rb.position - directionToPlayer * retreatSpeed * Time.fixedDeltaTime);
    }
    else
    {
        // Trong tầm lý tưởng → dừng lại (hoặc bay rất chậm) để bắn
        // rb.velocity = Vector2.zero; // nếu muốn đứng yên hoàn toàn
    }

    // --- Bắn cầu lửa ---
    if (distance <= maxRange && Time.time - lastAttackTime >= attackCooldown)
    {
        Attack();
    }


void Attack()
{
    lastAttackTime = Time.time;
    animator.SetBool("isAttacking", true);

    Debug.Log("Boss đang Attack - chuẩn bị bắn Fireball");
    StartCoroutine(ShootFireballDelay());
}

IEnumerator ShootFireballDelay()
{
    yield return new WaitForSeconds(0.4f);
    ShootFireball();
}

void ShootFireball()
{
    if (fireballPrefab == null || firePoint == null || player == null) return;

    GameObject fireball = Instantiate(fireballPrefab, firePoint.position, Quaternion.identity);

    // Bỏ qua va chạm với Boss
    Collider2D bossCol = GetComponent<Collider2D>();
    Collider2D fbCol = fireball.GetComponent<Collider2D>();
    if (bossCol != null && fbCol != null)
    {
        Physics2D.IgnoreCollision(bossCol, fbCol);
    }

    // === HƯỚNG BẮN ===
    Vector2 shootDirection = (player.position - firePoint.position).normalized;

    // Nếu vẫn bị ngược thì dùng dòng dưới này (bỏ comment dòng dưới và comment dòng trên)
    // Vector2 shootDirection = (firePoint.position - player.position).normalized;

    Fireball fb = fireball.GetComponent<Fireball>();
    if (fb != null)
    {
        fb.SetDirection(shootDirection);
    }
}
}
    public void TakeDamage(int damage)
    {
   Debug.Log("Boss nhận damage: " + damage + " | Invincible: " + isInvincible);

    if (isInvincible || isDead) return;

    currentHealth -= damage;
    animator.SetTrigger("Hurt");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        isInvincible = true;
        animator.SetTrigger("Die");

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        foreach (var col in GetComponents<Collider2D>())
        {
            col.enabled = false;
        }
    }
    }