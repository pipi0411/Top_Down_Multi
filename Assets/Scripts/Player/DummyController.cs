using UnityEngine;
using UnityEngine.UI; // Dùng cho thanh máu

public class DummyController : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Contact Damage")]
    [SerializeField] private float contactDamage = 10f;
    [SerializeField] private float damageCooldown = 0.5f; // Ngăn spam damage

    [Header("UI")]
    [SerializeField] private Slider healthSlider; // Kéo thanh máu vào đây
    [SerializeField] private Canvas healthCanvas;

    private Rigidbody2D rb;
    private Collider2D col;
    private float lastDamageTime;

   private void Awake()
{
    rb = GetComponent<Rigidbody2D>();
    col = GetComponent<Collider2D>();
    currentHealth = maxHealth;

    if (healthCanvas != null)
    {
        healthCanvas.worldCamera = Camera.main;
        // Giữ thanh máu luôn hướng về camera (rất quan trọng)
        healthCanvas.transform.rotation = Quaternion.identity;
    }
}

// Thêm FixedUpdate hoặc LateUpdate để theo dõi tốt hơn
private void LateUpdate()
{
    if (healthCanvas != null)
    {
        healthCanvas.transform.rotation = Quaternion.identity; // Giữ ngang
    }
}
    private void Start()
    {
        UpdateHealthUI();
    }

    // Nhận damage từ đạn của Player
    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth / maxHealth;
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.time - lastDamageTime < damageCooldown) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(contactDamage);
                lastDamageTime = Time.time;
            }
        }
    }

    private void Die()
    {
        Debug.Log("Dummy đã chết!");
        // Có thể destroy hoặc reset ở đây
        gameObject.SetActive(false);
        // Destroy(gameObject, 1f); // Uncomment nếu muốn xóa hẳn
    }

    // Reset Dummy (gọi từ Inspector hoặc script khác)
    public void ResetDummy()
    {
        currentHealth = maxHealth;
        gameObject.SetActive(true);
        UpdateHealthUI();
    }
}