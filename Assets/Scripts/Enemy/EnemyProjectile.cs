using Unity.Netcode;
using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    const int ProjectileSortingOrder = 100;
    const int FloorSortingLayerId = 673585699;
    const int WallsSortingLayerId = unchecked((int)2393433307u);

    static Sprite sharedSprite;

    Vector2 direction;
    Transform ownerRoot;
    float speed;
    float damage;
    float hitRadius;
    float remainingLife;
    Sprite bulletSprite;
    SpriteRenderer glowRenderer;
    Transform tracerTransform;
    float visualTime;

    public void Initialize(Vector2 moveDirection, float moveSpeed, float hitDamage, float projectileHitRadius, float lifetime, Transform owner, Sprite visualSprite)
    {
        direction = moveDirection.sqrMagnitude > 0.0001f ? moveDirection.normalized : Vector2.right;
        speed = Mathf.Max(0f, moveSpeed);
        damage = Mathf.Max(0f, hitDamage);
        hitRadius = Mathf.Max(0.01f, projectileHitRadius);
        remainingLife = Mathf.Max(0.05f, lifetime);
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
        TickVisual();

        float distance = speed * Time.deltaTime;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, hitRadius, direction, distance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (ownerRoot != null && hit.transform.IsChildOf(ownerRoot)) continue;

            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null)
            {
                if (door.BlocksProjectiles)
                {
                    Destroy(gameObject);
                    return;
                }

                continue;
            }

            PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                if (CanApplyDamage())
                    player.TakeDamage(damage);

                Destroy(gameObject);
                return;
            }

            BreakableBox box = hit.collider.GetComponentInParent<BreakableBox>();
            if (box != null)
            {
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
        if (remainingLife <= 0f) Destroy(gameObject);
    }

    bool CanApplyDamage()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }

    void TickVisual()
    {
        visualTime += Time.deltaTime;

        if (glowRenderer != null)
        {
            float pulse = 0.48f + Mathf.Sin(visualTime * 30f) * 0.08f;
            Color glowColor = glowRenderer.color;
            glowColor.a = pulse;
            glowRenderer.color = glowColor;
        }

        if (tracerTransform != null)
        {
            float stretch = 0.1f + Mathf.Sin(visualTime * 22f) * 0.01f;
            tracerTransform.localScale = new Vector3(stretch, 0.006f, 1f);
        }
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
            GameObject bullet = new GameObject("BulletSprite");
            bullet.transform.SetParent(transform, false);
            bullet.transform.localRotation = Quaternion.Euler(0, 0, -90f);
            float spriteLength = Mathf.Max(0.01f, bulletSprite.bounds.size.y);
            bullet.transform.localScale = Vector3.one * (0.24f / spriteLength);

            SpriteRenderer bulletRenderer = bullet.AddComponent<SpriteRenderer>();
            bulletRenderer.sprite = bulletSprite;
            bulletRenderer.sortingLayerID = SortingLayer.NameToID(nameof(Weapon));
            bulletRenderer.sortingOrder = ProjectileSortingOrder;
        }
        else
        {
            CreateLayer(Vector3.zero, new Vector3(0.2f, 0.04f, 1f), new Color(1f, 0.38f, 0.22f, 1f), 23);
            CreateLayer(new Vector3(0.012f, 0, 0), new Vector3(0.12f, 0.018f, 1f), Color.white, 24);
        }

        glowRenderer = CreateLayer(Vector3.zero, new Vector3(0.16f, 0.05f, 1f), new Color(1f, 0.15f, 0.05f, 0.32f), 22);
        SpriteRenderer tracer = CreateLayer(new Vector3(-0.12f, 0, 0), new Vector3(0.1f, 0.006f, 1f), new Color(1f, 0.32f, 0.08f, 0.18f), 21);
        tracerTransform = tracer.transform;
    }

    SpriteRenderer CreateLayer(Vector3 localPosition, Vector3 localScale, Color color, int sortingOrder)
    {
        GameObject layer = new GameObject("ProjectileVisual");
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
