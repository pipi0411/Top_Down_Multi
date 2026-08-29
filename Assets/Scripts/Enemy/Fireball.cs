using UnityEngine;

public class Fireball : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 15;
    public float lifeTime = 4f;

    Vector2 direction;
    Rigidbody2D rb;
    bool canHit;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Destroy(gameObject, lifeTime);
        Invoke(nameof(EnableHit), 0.05f);
    }

    void EnableHit()
    {
        canHit = true;
    }

    public void SetDirection(Vector2 dir)
    {
        direction = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void FixedUpdate()
    {
        if (rb != null)
            rb.linearVelocity = direction * speed;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!canHit || other == null) return;

        if (other.GetComponentInParent<BossManager>() != null || other.GetComponentInParent<Fireball>() != null)
            return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
