using UnityEngine;

public class Projectile : MonoBehaviour
{
    const int ProjectileSortingOrder = 100;
    const int FloorSortingLayerId = 673585699;
    const int WallsSortingLayerId = unchecked((int)2393433307u);
    static Sprite sharedSprite;
    Vector2 direction;
    Transform ownerRoot;
    float speed;
    float damage;
    float remainingLife;
    Sprite bulletSprite;
    float visualTime;
    SpriteRenderer glowRenderer;
    Transform tracerTransform;

    public void Initialize(Vector2 moveDirection, float moveSpeed, float hitDamage, float lifetime, Transform owner, Sprite visualSprite)
    {
        direction = moveDirection.normalized;
        speed = moveSpeed;
        damage = hitDamage;
        remainingLife = lifetime;
        ownerRoot = owner;
        bulletSprite = visualSprite;
        Vector3 visiblePosition = transform.position;
        visiblePosition.z = -2f;
        transform.position = visiblePosition;
        transform.right = direction;
        CreateVisual();
    }

    void Update()
    {
        visualTime += Time.deltaTime;
        if (glowRenderer != null)
        {
            float pulse = 0.52f + Mathf.Sin(visualTime * 35f) * 0.08f;
            Color glowColor = glowRenderer.color;
            glowColor.a = pulse;
            glowRenderer.color = glowColor;
        }
        if (tracerTransform != null)
        {
            float stretch = 0.12f + Mathf.Sin(visualTime * 24f) * 0.01f;
            tracerTransform.localScale = new Vector3(stretch, 0.007f, 1f);
        }

        float distance = speed * Time.deltaTime;
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (ownerRoot != null && hit.transform.IsChildOf(ownerRoot)) continue;
            PlayerHealth health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
            if (IsWallCollider(hit.collider))
            {
                Destroy(gameObject);
                return;
            }
        }
        transform.position += (Vector3)(direction * distance);
        remainingLife -= Time.deltaTime;
        if (remainingLife <= 0) Destroy(gameObject);
    }

    bool IsWallCollider(Collider2D collider)
    {
        Renderer[] renderers = collider.GetComponentsInChildren<Renderer>();
        foreach (Renderer hitRenderer in renderers)
        {
            if (hitRenderer.sortingLayerID == FloorSortingLayerId) return false;
            if (hitRenderer.sortingLayerID == WallsSortingLayerId) return true;
        }
        Renderer ownRenderer = collider.GetComponent<Renderer>();
        if (ownRenderer != null) return ownRenderer.sortingLayerID == WallsSortingLayerId;
        Renderer parentRenderer = collider.GetComponentInParent<Renderer>();
        return parentRenderer != null && parentRenderer.sortingLayerID == WallsSortingLayerId;
    }

    void CreateVisual()
    {
        if (sharedSprite == null)
            sharedSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);

        transform.localScale = Vector3.one;
        if (bulletSprite != null)
        {
            GameObject bullet = new GameObject();
            bullet.transform.SetParent(transform, false);
            bullet.transform.localRotation = Quaternion.Euler(0, 0, -90f);
            float spriteLength = Mathf.Max(0.01f, bulletSprite.bounds.size.y);
            bullet.transform.localScale = Vector3.one * (0.29f / spriteLength);
            SpriteRenderer bulletRenderer = bullet.AddComponent<SpriteRenderer>();
            bulletRenderer.sprite = bulletSprite;
            bulletRenderer.sortingLayerID = SortingLayer.NameToID(nameof(Weapon));
            bulletRenderer.sortingOrder = ProjectileSortingOrder;
        }
        else
        {
            CreateLayer(Vector3.zero, new Vector3(0.24f, 0.045f, 1f), new Color(1f, 0.93f, 0.62f, 1f), 23);
            CreateLayer(new Vector3(0.015f, 0, 0), new Vector3(0.15f, 0.022f, 1f), Color.white, 24);
        }
        glowRenderer = CreateLayer(Vector3.zero, new Vector3(0.18f, 0.055f, 1f), new Color(1f, 0.45f, 0.08f, 0.35f), 22);
        SpriteRenderer tracer = CreateLayer(new Vector3(-0.14f, 0, 0), new Vector3(0.12f, 0.007f, 1f), new Color(1f, 0.68f, 0.18f, 0.2f), 21);
        tracerTransform = tracer.transform;
    }

    SpriteRenderer CreateLayer(Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
    {
        GameObject layer = new GameObject();
        layer.transform.SetParent(transform, false);
        layer.transform.localPosition = localPosition;
        layer.transform.localScale = localScale;
        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sharedSprite;
        renderer.color = color;
        renderer.sortingLayerID = SortingLayer.NameToID(nameof(Weapon));
        renderer.sortingOrder = ProjectileSortingOrder + sortingOrder;
        return renderer;
    }
}
