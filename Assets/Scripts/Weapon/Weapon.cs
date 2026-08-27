using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MeleeAnimationStyle
{
    Thrust,
    Slash
}

public class Weapon : MonoBehaviour
{
    public ItemWeapon WeaponData => weaponData;
    [SerializeField] Transform weaponSprite;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] bool aimAtMouse = true;
    [SerializeField] float aimSmoothSpeed = 20f;
    [SerializeField] float idleSwayAmount = 1.25f;
    [SerializeField] float idleSwaySpeed = 2.5f;
    [SerializeField] float recoilDistance = 0.12f;
    [SerializeField] float recoilAngle = 5f;
    [SerializeField] float recoilDuration = 0.06f;
    [SerializeField] float recoveryDuration = 0.1f;
    [SerializeField] bool previewFireWithLeftClick = true;
    [SerializeField] bool mirrorSocketByAim = true;
    [SerializeField] float handSwitchDeadZone = 0.18f;
    [SerializeField] float handSwitchAngleBuffer = 10f;
    [SerializeField] ItemWeapon weaponData;
    [SerializeField] Sprite projectileSprite;
    [SerializeField] Transform shootPosition;
    [SerializeField] bool automatic;
    [SerializeField] float projectileSpeed = 18f;
    [SerializeField] float projectileHitRadius = 0.12f;
    [SerializeField] float projectileLifetime = 2f;
    [SerializeField] float fallbackDamage = 1f;
    [SerializeField] float fallbackShotInterval = 0.2f;
    [Header("Melee")]
    [SerializeField] float meleeRange = 0.85f;
    [SerializeField] float meleeHitRadius = 0.45f;
    [SerializeField] MeleeAnimationStyle meleeAnimationStyle;
    [SerializeField] float meleeThrustDistance = 0.28f;
    [SerializeField] float meleeSlashAngle = 55f;

    Camera mainCamera;
    NetworkObject ownerNetworkObject;
    Vector3 spriteStartPosition;
    float currentAimAngle;
    float recoilTimer;
    float nextShotTime;
    PlayerHealth playerHealth;
    PlayerEnergy fallbackPlayerEnergy;
    PlayerWeaponController weaponController;
    Transform weaponSocket;
    Vector3 socketRightLocalPosition;
    bool socketCached;
    bool isDropped;
    bool hasStableAimSide;
    bool stableAimingLeft;
    public bool IsMelee => weaponData != null && weaponData.Type == WeaponType.Melee;
    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (weaponSprite == null && spriteRenderer != null) weaponSprite = spriteRenderer.transform;
        if (weaponSprite == null) weaponSprite = transform;
        spriteStartPosition = weaponSprite.localPosition;
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
        currentAimAngle = transform.eulerAngles.z;
        mainCamera = Camera.main;
        playerHealth = GetComponentInParent<PlayerHealth>();
        fallbackPlayerEnergy = GetComponentInParent<PlayerEnergy>();
        weaponController = GetComponentInParent<PlayerWeaponController>();
        CacheWeaponSocket();
    }

    void OnTransformParentChanged()
    {
        socketCached = false;
        CacheWeaponSocket();
    }

    void Update()
    {
        recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
        if (isDropped) return;
        if (!CanAnimateLocally()) return;
        if (previewFireWithLeftClick && Mouse.current != null)
        {
            bool wantsToFire = automatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
            if (wantsToFire) TryFire();
        }
    }

    void LateUpdate()
    {
        if (isDropped) return;
        if (!CanAnimateLocally()) return;
        float targetAngle = currentAimAngle;
        if (aimAtMouse && Mouse.current != null)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                UpdateStableAimSideFromMouse(mouseWorld);

                Vector2 direction = mouseWorld - transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                    targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
        }
        currentAimAngle = Mathf.LerpAngle(currentAimAngle, targetAngle, aimSmoothSpeed * Time.deltaTime);
        ApplyAimPose(currentAimAngle);
    }

    public float CurrentAimAngle => currentAimAngle;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;

    public void SetDroppedState()
    {
        isDropped = true;
        recoilTimer = 0f;
        nextShotTime = Time.time;
        ownerNetworkObject = null;
        playerHealth = null;
        fallbackPlayerEnergy = null;
        weaponController = null;
        weaponSocket = null;
        socketCached = false;
        currentAimAngle = transform.eulerAngles.z;
        hasStableAimSide = false;

        if (spriteRenderer != null)
            spriteRenderer.flipY = false;
        if (weaponSprite != null)
            weaponSprite.localPosition = spriteStartPosition;
    }

    public void SetEquippedState()
    {
        isDropped = false;
        ownerNetworkObject = GetComponentInParent<NetworkObject>();
        playerHealth = GetComponentInParent<PlayerHealth>();
        fallbackPlayerEnergy = GetComponentInParent<PlayerEnergy>();
        weaponController = GetComponentInParent<PlayerWeaponController>();
        currentAimAngle = transform.eulerAngles.z;
        hasStableAimSide = false;
        CacheWeaponSocket();
    }

    public void ApplyRemoteAim(float aimAngle)
    {
        currentAimAngle = aimAngle;
        ApplyAimPose(currentAimAngle);
    }

    public void PlayFireAnimation()
    {
        recoilTimer = recoilDuration + recoveryDuration;
    }

    public bool TryFire()
    {
        if (Time.time < nextShotTime) return false;
        bool isMelee = IsMelee;
        if (!isMelee && shootPosition == null) return false;
        if (playerHealth == null) playerHealth = FindLocalPlayerStats();
        float energyCost = weaponData != null ? weaponData.RequiredEnergy : 0f;
        if (!isMelee)
        {
            if (playerHealth != null)
            {
                if (!playerHealth.TryConsumeShot(energyCost)) return false;
            }
            else
            {
                if (fallbackPlayerEnergy == null) fallbackPlayerEnergy = FindAnyObjectByType<PlayerEnergy>();
                if (fallbackPlayerEnergy == null || !fallbackPlayerEnergy.TryUseEnergy(energyCost)) return false;
            }
        }

        float interval = weaponData != null
            ? weaponData.TimeBetweenShots : fallbackShotInterval;
        float damage = weaponData != null ? weaponData.Damage : fallbackDamage;
        float minSpread = weaponData != null ? weaponData.MinSpread : 0f;
        float maxSpread = weaponData != null ? weaponData.MaxSpread : 0f;
        float spread = Random.Range(minSpread, maxSpread);
        Vector2 direction = Quaternion.Euler(0, 0, spread) * transform.right;
        bool networkShot = playerHealth != null && playerHealth.IsSpawned;

        if (isMelee)
        {
            Vector2 meleeOrigin = transform.position;
            if (networkShot)
            {
                if (weaponController == null) weaponController = GetComponentInParent<PlayerWeaponController>();
                if (weaponController != null && weaponController.IsSpawned)
                    weaponController.SubmitNetworkMelee(meleeOrigin, direction, damage, meleeRange, meleeHitRadius);
                else
                    playerHealth.SubmitMelee(meleeOrigin, direction, damage, meleeRange, meleeHitRadius);
            }
            else
            {
                ResolveLocalMelee(meleeOrigin, direction, damage, meleeRange, meleeHitRadius);
            }

            nextShotTime = Time.time + Mathf.Max(0.02f, interval);
            PlayFireAnimation();
            GameAudioManager.Instance?.PlayWeapon(true, transform.position);
            return true;
        }

        Vector2 origin = shootPosition.position;
        float range = projectileSpeed * projectileLifetime;
        if (networkShot)
        {
            if (weaponController == null) weaponController = GetComponentInParent<PlayerWeaponController>();
            if (weaponController != null && weaponController.IsSpawned)
                weaponController.SubmitNetworkFire(origin, direction, damage, range, projectileHitRadius, projectileSpeed, projectileLifetime);
            else
                playerHealth.SubmitShot(origin, direction, damage, range, projectileHitRadius);
        }

        SpawnProjectileVisual(origin, direction, damage, projectileSpeed, projectileLifetime, !networkShot);

        nextShotTime = Time.time + Mathf.Max(0.02f, interval);
        PlayFireAnimation();
        GameAudioManager.Instance?.PlayWeapon(false, transform.position);
        return true;
    }

    public void ResolveLocalMelee(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 hitCenter = origin + direction * Mathf.Max(0.05f, range);
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitCenter, Mathf.Max(0.05f, hitRadius));

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.IsChildOf(transform.root)) continue;

            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(Mathf.Max(0f, damage));
                return;
            }

            BreakableBox box = hit.GetComponentInParent<BreakableBox>();
            if (box != null)
            {
                box.TakeDamage(Mathf.Max(0f, damage));
                return;
            }
        }
    }

    public void SpawnProjectileVisual(Vector2 origin, Vector2 direction, float damage, float speed, float lifetime, bool canApplyLocalDamage)
    {
        GameObject projectileObject = new GameObject();
        projectileObject.transform.position = origin;
        Projectile projectile = projectileObject.AddComponent<Projectile>();
        Transform owner = playerHealth != null ? playerHealth.transform : ownerNetworkObject != null ? ownerNetworkObject.transform : transform.root;
        projectile.Initialize(direction, speed, damage, projectileHitRadius, lifetime, owner, projectileSprite, canApplyLocalDamage);
    }

    void ApplyAimPose(float aimAngle)
    {
        ApplySocketMirror(aimAngle);

        float sway = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        bool isMelee = IsMelee;
        float recoil = RecoilAmount();
        float attackProgress = AttackProgress();
        float attackPulse = Mathf.Sin(attackProgress * Mathf.PI);
        float meleeAngle = 0f;
        bool aimingLeft = GetStableAimingLeft(aimAngle);

        if (isMelee && recoilTimer > 0f && meleeAnimationStyle == MeleeAnimationStyle.Slash)
        {
            float slashAngle = aimingLeft ? -meleeSlashAngle : meleeSlashAngle;
            meleeAngle = Mathf.Lerp(-slashAngle, slashAngle, attackProgress);
        }

        float fireAngle = isMelee ? meleeAngle : recoil * recoilAngle;
        transform.rotation = Quaternion.Euler(0, 0, aimAngle + sway + fireAngle);
        if (spriteRenderer != null)
            spriteRenderer.flipY = aimingLeft;

        Vector3 attackOffset = isMelee
            ? Vector3.right * (meleeThrustDistance * attackPulse)
            : Vector3.left * (recoilDistance * recoil);
        Vector3 targetPosition = spriteStartPosition + attackOffset;
        weaponSprite.localPosition = Vector3.Lerp(weaponSprite.localPosition, targetPosition, 30f * Time.deltaTime);
    }

    void CacheWeaponSocket()
    {
        Transform parent = transform.parent;
        if (parent == null || parent.name != "WeaponSocket") return;

        weaponSocket = parent;
        socketRightLocalPosition = weaponSocket.localPosition;
        socketRightLocalPosition.x = Mathf.Abs(socketRightLocalPosition.x);
        socketCached = true;
    }

    void ApplySocketMirror(float aimAngle)
    {
        if (!mirrorSocketByAim) return;
        if (!socketCached || weaponSocket == null)
            CacheWeaponSocket();
        if (weaponSocket == null) return;

        bool aimingLeft = GetStableAimingLeft(aimAngle);
        Vector3 targetPosition = socketRightLocalPosition;
        targetPosition.x = aimingLeft ? -socketRightLocalPosition.x : socketRightLocalPosition.x;
        weaponSocket.localPosition = targetPosition;
    }

    void UpdateStableAimSideFromMouse(Vector3 mouseWorld)
    {
        Vector3 origin = GetAimSideOrigin();
        float deltaX = mouseWorld.x - origin.x;
        if (Mathf.Abs(deltaX) < handSwitchDeadZone)
            return;

        stableAimingLeft = deltaX < 0f;
        hasStableAimSide = true;
    }

    bool GetStableAimingLeft(float aimAngle)
    {
        if (!hasStableAimSide)
        {
            stableAimingLeft = Mathf.Abs(Mathf.DeltaAngle(0f, aimAngle)) > 90f;
            hasStableAimSide = true;
            return stableAimingLeft;
        }

        float absAngle = Mathf.Abs(Mathf.DeltaAngle(0f, aimAngle));
        float switchToLeftAngle = 90f + Mathf.Max(0f, handSwitchAngleBuffer);
        float switchToRightAngle = 90f - Mathf.Max(0f, handSwitchAngleBuffer);

        if (!stableAimingLeft && absAngle > switchToLeftAngle)
            stableAimingLeft = true;
        else if (stableAimingLeft && absAngle < switchToRightAngle)
            stableAimingLeft = false;

        return stableAimingLeft;
    }

    Vector3 GetAimSideOrigin()
    {
        if (weaponController != null)
            return weaponController.transform.position;
        if (playerHealth != null)
            return playerHealth.transform.position;
        if (ownerNetworkObject != null)
            return ownerNetworkObject.transform.position;
        return transform.root != null ? transform.root.position : transform.position;
    }

    float RecoilAmount()
    {
        if (recoilTimer <= 0) return 0;
        if (recoilTimer > recoveryDuration)
            return Mathf.Clamp01((recoilDuration - recoilTimer + recoveryDuration) / recoilDuration);
        return Mathf.Clamp01(recoilTimer / recoveryDuration);
    }

    float AttackProgress()
    {
        float totalDuration = recoilDuration + recoveryDuration;
        if (recoilTimer <= 0f || totalDuration <= 0f) return 0f;
        return Mathf.Clamp01(1f - recoilTimer / totalDuration);
    }

    bool CanAnimateLocally()
    {
        if (isDropped) return false;
        if (ownerNetworkObject != null)
            return !ownerNetworkObject.IsSpawned || ownerNetworkObject.IsOwner;
        return GetComponentInParent<PlayerWeaponController>() != null || FindAnyObjectByType<PlayerWeaponController>() == null;
    }

    PlayerHealth FindLocalPlayerStats()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        foreach (PlayerHealth player in players)
            if (!player.IsSpawned || player.IsOwner) return player;
        return null;
    }
}
