using TMPro;
using UnityEngine;

[RequireComponent(typeof(BossManager))]
public class BossHealthBar : MonoBehaviour
{
    [SerializeField] BossManager boss;
    [SerializeField] Vector3 localOffset = new Vector3(0f, 1.85f, 0f);
    [SerializeField] Vector2 size = new Vector2(3.2f, 0.28f);
    [SerializeField] Color borderColor = new Color(0.04f, 0.01f, 0.01f, 0.95f);
    [SerializeField] Color backgroundColor = new Color(0.12f, 0.02f, 0.02f, 0.9f);
    [SerializeField] Color fillColor = new Color(1f, 0.16f, 0.08f, 0.95f);
    [SerializeField] Color phase2Color = new Color(1f, 0.85f, 0.12f, 0.95f);
    [SerializeField] int sortingOrderOffset = 180;

    Transform root;
    SpriteRenderer borderRenderer;
    SpriteRenderer backgroundRenderer;
    SpriteRenderer fillRenderer;
    TextMeshPro hpText;
    SpriteRenderer ownerRenderer;
    float lastFill = 1f;

    void Awake()
    {
        if (boss == null) boss = GetComponent<BossManager>();
        ownerRenderer = GetComponentInChildren<SpriteRenderer>();
        CreateBar();
    }

    void OnEnable()
    {
        if (boss != null)
            boss.OnHealthChanged += HandleHealthChanged;
    }

    void Start()
    {
        if (boss != null)
            UpdateBar(boss.currentHealth, boss.maxHealth);
    }

    void OnDisable()
    {
        if (boss != null)
            boss.OnHealthChanged -= HandleHealthChanged;
    }

    void LateUpdate()
    {
        if (root != null)
        {
            root.localPosition = localOffset;
            root.localRotation = Quaternion.identity;
            root.localScale = new Vector3(
                transform.lossyScale.x < 0f ? -1f : 1f,
                transform.lossyScale.y < 0f ? -1f : 1f,
                1f);
        }
    }

    void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateBar(currentHealth, maxHealth);
    }

    void UpdateBar(int currentHealth, int maxHealth)
    {
        CreateBar();

        maxHealth = Mathf.Max(1, maxHealth);
        float fill = Mathf.Clamp01((float)currentHealth / maxHealth);
        lastFill = fill;

        if (fillRenderer != null)
        {
            fillRenderer.transform.localScale = new Vector3(size.x * fill, size.y, 1f);
            fillRenderer.transform.localPosition = new Vector3(-size.x * (1f - fill) * 0.5f, 0f, -0.02f);
            fillRenderer.color = fill <= 0.5f ? phase2Color : fillColor;
        }

        if (hpText != null)
            hpText.text = $"{currentHealth}/{maxHealth}";

        if (root != null)
            root.gameObject.SetActive(currentHealth > 0);
    }

    void CreateBar()
    {
        if (root != null) return;

        root = new GameObject("BossHealthBar").transform;
        root.SetParent(transform, false);
        root.localPosition = localOffset;

        borderRenderer = CreateRenderer("Border", borderColor, 0f, sortingOrderOffset);
        borderRenderer.transform.localScale = new Vector3(size.x + 0.14f, size.y + 0.14f, 1f);

        backgroundRenderer = CreateRenderer("Background", backgroundColor, -0.01f, sortingOrderOffset + 1);
        backgroundRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);

        fillRenderer = CreateRenderer("Fill", fillColor, -0.02f, sortingOrderOffset + 2);
        fillRenderer.transform.localScale = new Vector3(size.x * lastFill, size.y, 1f);

        GameObject textObject = new GameObject("HpText");
        textObject.transform.SetParent(root, false);
        textObject.transform.localPosition = new Vector3(0f, 0.02f, -0.04f);
        hpText = textObject.AddComponent<TextMeshPro>();
        hpText.alignment = TextAlignmentOptions.Center;
        hpText.fontSize = 2.2f;
        hpText.color = Color.white;
        hpText.textWrappingMode = TextWrappingModes.NoWrap;
        hpText.rectTransform.sizeDelta = new Vector2(size.x, size.y * 2f);
        hpText.sortingOrder = ResolveSortingOrder(sortingOrderOffset + 3);
    }

    SpriteRenderer CreateRenderer(string objectName, Color color, float zOffset, int orderOffset)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(root, false);
        child.transform.localPosition = new Vector3(0f, 0f, zOffset);

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        renderer.color = color;
        if (ownerRenderer != null)
            renderer.sortingLayerID = ownerRenderer.sortingLayerID;
        renderer.sortingOrder = ResolveSortingOrder(orderOffset);
        return renderer;
    }

    int ResolveSortingOrder(int offset)
    {
        return ownerRenderer != null ? ownerRenderer.sortingOrder + offset : offset;
    }
}
