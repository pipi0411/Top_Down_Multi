using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] ItemWeapon weaponData;
    [SerializeField] Sprite projectileSprite;
    [SerializeField] Transform shootPosition;
    [SerializeField] bool automatic;
    [SerializeField] float projectileSpeed = 18f;
    [SerializeField] float projectileLifetime = 2f;
    [SerializeField] float fallbackDamage = 1f;
    [SerializeField] float fallbackShotInterval = 0.2f;

    Camera mainCamera;
    NetworkObject ownerNetworkObject;
    Vector3 spriteStartPosition;
    float currentAimAngle;
    float recoilTimer;
    float nextShotTime;
    PlayerHealth playerHealth;
    PlayerEnergy fallbackPlayerEnergy;
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
    }

    void Update()
    {
        if (!CanAnimateLocally()) return;
        if (previewFireWithLeftClick && Mouse.current != null)
        {
            bool wantsToFire = automatic ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
            if (wantsToFire) TryFire();
        }
        recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
    }

    void LateUpdate()
    {
        if (!CanAnimateLocally()) return;
        float targetAngle = currentAimAngle;
        if (aimAtMouse && Mouse.current != null)
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
                Vector2 direction = mouseWorld - transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                    targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }
        }
        currentAimAngle = Mathf.LerpAngle(currentAimAngle, targetAngle, aimSmoothSpeed * Time.deltaTime);
        float sway = Mathf.Sin(Time.time * idleSwaySpeed) * idleSwayAmount;
        transform.rotation = Quaternion.Euler(0, 0, currentAimAngle + sway + RecoilAmount() * recoilAngle);
        if (spriteRenderer != null)
            spriteRenderer.flipY = Mathf.Abs(Mathf.DeltaAngle(0, currentAimAngle)) > 90f;
        Vector3 targetPosition = spriteStartPosition + Vector3.left * (recoilDistance * RecoilAmount());
        weaponSprite.localPosition = Vector3.Lerp(weaponSprite.localPosition, targetPosition, 30f * Time.deltaTime);
    }

    public void PlayFireAnimation()
    {
        recoilTimer = recoilDuration + recoveryDuration;
    }

    public bool TryFire()
    {
        if (Time.time < nextShotTime || shootPosition == null) return false;
        if (playerHealth == null) playerHealth = FindLocalPlayerStats();
        float energyCost = weaponData != null ? weaponData.RequiredEnergy : 0f;
        if (playerHealth != null)
        {
            if (!playerHealth.TryConsumeShot(energyCost)) return false;
        }
        else
        {
            if (fallbackPlayerEnergy == null) fallbackPlayerEnergy = FindAnyObjectByType<PlayerEnergy>();
            if (fallbackPlayerEnergy == null || !fallbackPlayerEnergy.TryUseEnergy(energyCost)) return false;
        }

        float interval = weaponData != null
            ? weaponData.TimeBetweenShots : fallbackShotInterval;
        float damage = weaponData != null ? weaponData.Damage : fallbackDamage;
        float minSpread = weaponData != null ? weaponData.MinSpread : 0f;
        float maxSpread = weaponData != null ? weaponData.MaxSpread : 0f;
        float spread = Random.Range(minSpread, maxSpread);
        Vector2 direction = Quaternion.Euler(0, 0, spread) * transform.right;

        GameObject projectileObject = new GameObject();
        projectileObject.transform.position = shootPosition.position;
        Projectile projectile = projectileObject.AddComponent<Projectile>();
        Transform owner = playerHealth != null ? playerHealth.transform : ownerNetworkObject != null ? ownerNetworkObject.transform : transform.root;
        projectile.Initialize(direction, projectileSpeed, damage, projectileLifetime, owner, projectileSprite);

        nextShotTime = Time.time + Mathf.Max(0.02f, interval);
        PlayFireAnimation();
        return true;
    }

    float RecoilAmount()
    {
        if (recoilTimer <= 0) return 0;
        if (recoilTimer > recoveryDuration)
            return Mathf.Clamp01((recoilDuration - recoilTimer + recoveryDuration) / recoilDuration);
        return Mathf.Clamp01(recoilTimer / recoveryDuration);
    }

    bool CanAnimateLocally()
    {
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
