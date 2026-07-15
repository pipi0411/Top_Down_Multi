using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] EnemyHealth health;
    [SerializeField] Vector3 localOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] Vector2 size = new Vector2(1f, 0.14f);
    [SerializeField] Color backgroundColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
    [SerializeField] Color fillColor = new Color(0.2f, 1f, 0.25f, 0.95f);
    [SerializeField] Color lowHealthColor = new Color(1f, 0.18f, 0.12f, 0.95f);
    [SerializeField] float lowHealthThreshold = 0.3f;
    [SerializeField] int sortingOrder = 100;

    Transform barRoot;
    SpriteRenderer backgroundRenderer;
    SpriteRenderer fillRenderer;
    SpriteRenderer ownerRenderer;
    float lastFill = 1f;

    void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        ownerRenderer = GetComponentInChildren<SpriteRenderer>();
        CreateBar();
    }

    void OnEnable()
    {
        if (health != null)
            health.OnHealthChanged += HandleHealthChanged;
    }

    void Start()
    {
        if (health != null)
            UpdateBar(health.CurrentHealth, health.MaxHealthValue);
    }

    void OnDisable()
    {
        if (health != null)
            health.OnHealthChanged -= HandleHealthChanged;
    }

    void LateUpdate()
    {
        if (barRoot != null)
            barRoot.localPosition = localOffset;
    }

    void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateBar(currentHealth, maxHealth);
    }

    void UpdateBar(float currentHealth, float maxHealth)
    {
        CreateBar();

        float fill = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        lastFill = fill;

        if (fillRenderer != null)
        {
            fillRenderer.transform.localScale = new Vector3(size.x * fill, size.y, 1f);
            fillRenderer.transform.localPosition = new Vector3(-size.x * (1f - fill) * 0.5f, 0f, -0.01f);
            fillRenderer.color = fill <= lowHealthThreshold ? lowHealthColor : fillColor;
        }

        if (barRoot != null)
            barRoot.gameObject.SetActive(fill > 0f);
    }

    void CreateBar()
    {
        if (barRoot != null) return;

        barRoot = new GameObject("EnemyHealthBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = localOffset;
        barRoot.localRotation = Quaternion.identity;

        backgroundRenderer = CreateRenderer("Background", backgroundColor, 0f);
        backgroundRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        fillRenderer = CreateRenderer("Fill", fillColor, -0.01f);
        fillRenderer.transform.localScale = new Vector3(size.x * lastFill, size.y, 1f);
    }

    SpriteRenderer CreateRenderer(string objectName, Color color, float zOffset)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(barRoot, false);
        child.transform.localPosition = new Vector3(0f, 0f, zOffset);

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = color;
        if (ownerRenderer != null)
        {
            renderer.sortingLayerID = ownerRenderer.sortingLayerID;
            renderer.sortingOrder = ownerRenderer.sortingOrder + sortingOrder;
        }
        else
        {
            renderer.sortingOrder = sortingOrder;
        }
        return renderer;
    }
}
