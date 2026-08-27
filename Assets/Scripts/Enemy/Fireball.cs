using UnityEngine;

public class Fireball : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 15;
    public float lifeTime = 4f;

    private Vector2 direction;
    private Rigidbody2D rb;
    private bool canHit = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifeTime);

        // Cho phép va chạm sau 0.05 giây (tránh hủy ngay khi vừa sinh ra)
        Invoke(nameof(EnableHit), 0.05f);
    }

    void EnableHit()
    {
        canHit = true;
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = direction * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canHit) return;

        // Bỏ qua Boss và Fireball khác
        if (other.GetComponent<BossManager>() != null || other.GetComponent<Fireball>() != null)
            return;

        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // Chạm tường / vật khác thì hủy
        Destroy(gameObject);
    }
}