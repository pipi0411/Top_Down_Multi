using Unity.Netcode;
using UnityEngine;

public class EnemyWeapon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform firePoint;
    [SerializeField] Sprite projectileSprite;

    [Header("Visual")]
    [SerializeField] string weaponSortingLayer = "Weapon";
    [SerializeField] int weaponSortingOrder = 30;
    [SerializeField] bool flipSpriteWhenAimingLeft = true;
    [SerializeField] bool autoFitHeldWeapon = true;
    [SerializeField] Vector2 heldLocalPosition = new(0.12f, -0.05f);
    [SerializeField] Vector2 spriteLocalPosition = new(0.08f, 0.03f);
    [SerializeField] Vector2 firePointLocalPosition = new(0.22f, 0.04f);
    [SerializeField] float visibleLocalZ = -1f;

    [Header("Targeting")]
    [SerializeField] float detectionRange = 8f;
    [SerializeField] float fireRange = 7f;
    [SerializeField] bool rotateToTarget = true;
    [SerializeField] float rotationOffset;

    [Header("Shooting")]
    [SerializeField] float damage = 1f;
    [SerializeField] float fireInterval = 1.1f;
    [SerializeField] float projectileSpeed = 8f;
    [SerializeField] float projectileHitRadius = 0.12f;
    [SerializeField] float projectileLifetime = 3f;
    [SerializeField] float muzzleOffset = 0.15f;
    [SerializeField] float firstShotDelayMin = 0.5f;
    [SerializeField] float firstShotDelayMax = 1.4f;
    [SerializeField] float fireIntervalRandomness = 0.25f;

    EnemyHealth ownerHealth;
    Transform ownerRoot;
    NetworkObject ownerNetworkObject;
    float nextFireTime;

    void OnValidate()
    {
        ApplyVisualSorting();
    }

    void Awake()
    {
        if (firePoint == null) firePoint = transform;

        ownerHealth = GetComponentInParent<EnemyHealth>();
        ownerRoot = ownerHealth != null ? ownerHealth.transform : transform.root;
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
        nextFireTime = Time.time + Random.Range(firstShotDelayMin, Mathf.Max(firstShotDelayMin, firstShotDelayMax));
        ApplyVisualSorting();
    }

    void Update()
    {
        if (!CanRunWeapon()) return;

        PlayerHealth target = FindNearestTarget();
        if (target == null) return;

        Vector2 aimDirection = GetAimDirection(target);
        if (aimDirection.sqrMagnitude < 0.0001f) return;

        if (rotateToTarget)
        {
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + rotationOffset);
        }
        ApplyAimFlip(aimDirection);

        float distance = Vector2.Distance(firePoint.position, target.transform.position);
        if (distance > fireRange || Time.time < nextFireTime) return;

        Shoot(aimDirection.normalized);
        nextFireTime = Time.time + Mathf.Max(0.05f, fireInterval + Random.Range(0f, fireIntervalRandomness));
    }

    bool CanRunWeapon()
    {
        if (!isActiveAndEnabled) return false;
        if (ownerHealth != null && ownerHealth.IsDead) return false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
            return networkManager.IsServer;

        return true;
    }

    PlayerHealth FindNearestTarget()
    {
        PlayerHealth nearestTarget = null;
        float nearestDistanceSqr = detectionRange * detectionRange;
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();

        foreach (PlayerHealth player in players)
        {
            if (player == null || player.IsDead) continue;

            float distanceSqr = ((Vector2)player.transform.position - (Vector2)firePoint.position).sqrMagnitude;
            if (distanceSqr > nearestDistanceSqr) continue;

            nearestDistanceSqr = distanceSqr;
            nearestTarget = player;
        }

        return nearestTarget;
    }

    Vector2 GetAimDirection(PlayerHealth target)
    {
        Vector2 origin = firePoint != null ? firePoint.position : transform.position;
        Collider2D targetCollider = target.GetComponentInChildren<Collider2D>();
        Vector2 targetPosition = targetCollider != null ? targetCollider.bounds.center : target.transform.position;
        return targetPosition - origin;
    }

    void Shoot(Vector2 aimDirection)
    {
        Vector3 spawnPosition = firePoint.position + (Vector3)(aimDirection * muzzleOffset);
        GameObject projectileObject = new GameObject("EnemyProjectile");
        projectileObject.transform.position = spawnPosition;

        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(
            aimDirection,
            projectileSpeed,
            damage,
            projectileHitRadius,
            projectileLifetime,
            ownerRoot,
            projectileSprite);

        MultiplayerGameplaySync.BroadcastEnemyProjectile(
            spawnPosition,
            aimDirection,
            projectileSpeed,
            damage,
            projectileHitRadius,
            projectileLifetime);
    }

    void ApplyVisualSorting()
    {
        EnemyHealth parentEnemy = GetComponentInParent<EnemyHealth>();
        Vector3 localPosition = transform.localPosition;
        if (autoFitHeldWeapon && parentEnemy != null && ((Vector2)localPosition).sqrMagnitude > 1f)
        {
            localPosition.x = heldLocalPosition.x;
            localPosition.y = heldLocalPosition.y;
        }

        localPosition.z = visibleLocalZ;
        transform.localPosition = localPosition;

        int sortingLayerId = SortingLayer.NameToID(weaponSortingLayer);
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = weaponSortingOrder;

            if (autoFitHeldWeapon && parentEnemy != null)
            {
                Vector3 rendererLocalPosition = renderer.transform.localPosition;
                rendererLocalPosition.x = spriteLocalPosition.x;
                rendererLocalPosition.y = spriteLocalPosition.y;
                renderer.transform.localPosition = rendererLocalPosition;
            }
        }

        if (autoFitHeldWeapon && parentEnemy != null && firePoint != null)
        {
            Vector3 fireLocalPosition = firePoint.localPosition;
            fireLocalPosition.x = firePointLocalPosition.x;
            fireLocalPosition.y = firePointLocalPosition.y;
            firePoint.localPosition = fireLocalPosition;
        }
    }

    void ApplyAimFlip(Vector2 aimDirection)
    {
        if (!flipSpriteWhenAimingLeft) return;

        bool aimingLeft = aimDirection.x < -0.01f;
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.flipY = aimingLeft;
        }
    }
}
