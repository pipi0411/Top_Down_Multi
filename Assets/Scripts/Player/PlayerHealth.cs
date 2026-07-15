using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : NetworkBehaviour
{
    const int WallsSortingLayerId = unchecked((int)2393433307u);
    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] int startingAmmo = 30;
    [SerializeField] int startingReserveAmmo = 120;
    [SerializeField] float energyRegenerationDelay = 1.5f;
    [SerializeField] float energyRegenerationPerSecond = 10f;
    [SerializeField] float reloadDuration = 1.2f;
    [SerializeField] bool allowWeaponDamageFromPlayers;
    [Header("Respawn")]
    [SerializeField] float respawnDelay = 2f;
    [SerializeField] float respawnInvulnerableTime = 1.5f;
    [SerializeField] string checkpointObjectName = "Checkpoint";
    [SerializeField] string spawnPointObjectName = "SpawnPoint";

    readonly NetworkVariable<float> networkHealth = new(0);
    readonly NetworkVariable<float> networkArmor = new(0);
    readonly NetworkVariable<float> networkEnergy = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkReserveAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<bool> networkIsDead = new(false);
    float lastShotTime = float.NegativeInfinity;
    float reloadCompleteTime;
    int offlineAmmo;
    int offlineReserveAmmo;
    bool isReloading;
    bool offlineIsDead;
    bool isInvulnerable;
    Coroutine respawnRoutine;
    Collider2D[] colliders;
    SpriteRenderer[] spriteRenderers;
    Rigidbody2D rb;

    public PlayerConfig PlayerConfig => playerConfig;
    public float CurrentHealth => IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
    public float CurrentEnergy => IsSpawned ? networkEnergy.Value : playerConfig.Energy;
    public int CurrentAmmo => IsSpawned ? networkAmmo.Value : offlineAmmo;
    public int MaxAmmo => startingAmmo;
    public int CurrentReserveAmmo => IsSpawned ? networkReserveAmmo.Value : offlineReserveAmmo;
    public bool IsReloading => isReloading;
    public bool AllowWeaponDamageFromPlayers => allowWeaponDamageFromPlayers;
    public bool IsDead => IsSpawned ? networkIsDead.Value : offlineIsDead;

    void Awake()
    {
        if (playerConfig != null) playerConfig = Instantiate(playerConfig);
        offlineAmmo = startingAmmo;
        offlineReserveAmmo = startingReserveAmmo;
        colliders = GetComponentsInChildren<Collider2D>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        rb = GetComponent<Rigidbody2D>();
    }

    public override void OnNetworkSpawn()
    {
        networkIsDead.OnValueChanged += HandleDeadStateChanged;
        if (IsServer)
        {
            networkHealth.Value = playerConfig.MaxHealth;
            networkArmor.Value = playerConfig.MaxArmor;
            networkIsDead.Value = false;
        }
        if (IsOwner)
        {
            networkEnergy.Value = playerConfig.MaxEnergy;
            networkAmmo.Value = startingAmmo;
            networkReserveAmmo.Value = startingReserveAmmo;
            BindLocalUI();
        }
        SyncConfig();
        SetDeadState(networkIsDead.Value);
    }

    public override void OnNetworkDespawn()
    {
        networkIsDead.OnValueChanged -= HandleDeadStateChanged;
    }

    void Start()
    {
        if (!IsSpawned)
        {
            playerConfig.CurrentHealth = playerConfig.MaxHealth;
            playerConfig.Armor = playerConfig.MaxArmor;
            playerConfig.Energy = playerConfig.MaxEnergy;
            BindLocalUI();
        }
    }

    void Update()
    {
        SyncConfig();
        bool canControlResources = !IsSpawned || IsOwner;
        if (canControlResources)
        {
            RegenerateEnergy();
            UpdateReload();
        }
        if (!canControlResources || Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) TryStartReload();
        if (Keyboard.current.pKey.wasPressedThisFrame) RecoverHealth(1);
    }

    public bool TryConsumeShot(float energyCost)
    {
        if (IsDead || IsSpawned && !IsOwner || isReloading) return false;
        float energy = IsSpawned ? networkEnergy.Value : playerConfig.Energy;
        int ammo = IsSpawned ? networkAmmo.Value : offlineAmmo;
        if (energy < energyCost || ammo <= 0) return false;
        if (IsSpawned)
        {
            networkEnergy.Value = energy - energyCost;
            networkAmmo.Value = ammo - 1;
        }
        else
        {
            playerConfig.Energy = energy - energyCost;
            offlineAmmo = ammo - 1;
        }
        lastShotTime = Time.time;
        return true;
    }

    public void ConfigureWeapon(ItemWeapon weaponData)
    {
        if (weaponData == null) return;
        startingAmmo = Mathf.Max(1, weaponData.MagazineSize);
        startingReserveAmmo = Mathf.Max(0, weaponData.StartingReserveAmmo);
        reloadDuration = Mathf.Max(0.1f, weaponData.ReloadDuration);
        offlineAmmo = startingAmmo;
        offlineReserveAmmo = startingReserveAmmo;
        if (IsSpawned && IsOwner)
        {
            networkAmmo.Value = startingAmmo;
            networkReserveAmmo.Value = startingReserveAmmo;
        }
    }

    public bool TryStartReload()
    {
        if (IsDead || IsSpawned && !IsOwner || isReloading || CurrentAmmo >= MaxAmmo || CurrentReserveAmmo <= 0) return false;
        isReloading = true;
        reloadCompleteTime = Time.time + reloadDuration;
        return true;
    }

    void UpdateReload()
    {
        if (!isReloading || Time.time < reloadCompleteTime) return;
        int neededAmmo = MaxAmmo - CurrentAmmo;
        int loadedAmmo = Mathf.Min(neededAmmo, CurrentReserveAmmo);
        if (IsSpawned)
        {
            networkAmmo.Value += loadedAmmo;
            networkReserveAmmo.Value -= loadedAmmo;
        }
        else
        {
            offlineAmmo += loadedAmmo;
            offlineReserveAmmo -= loadedAmmo;
        }
        isReloading = false;
    }

    void RegenerateEnergy()
    {
        if (Time.time - lastShotTime < energyRegenerationDelay || playerConfig == null) return;
        float energy = IsSpawned ? networkEnergy.Value : playerConfig.Energy;
        if (energy >= playerConfig.MaxEnergy) return;
        energy = Mathf.Min(playerConfig.MaxEnergy, energy + energyRegenerationPerSecond * Time.deltaTime);
        if (IsSpawned) networkEnergy.Value = energy;
        else playerConfig.Energy = energy;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || isInvulnerable) return;
        if (IsSpawned && !IsServer) DamageServerRpc(amount);
        else ApplyDamage(amount);
    }

    public void SubmitShot(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius = 0.12f)
    {
        if (!IsSpawned || !IsOwner) return;
        ShotServerRpc(origin, direction.normalized, damage, range, hitRadius);
    }

    [Rpc(SendTo.Server)]
    void ShotServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        ResolveServerShot(origin, direction, damage, range, hitRadius);
    }

    public void ResolveServerShot(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius = 0.12f)
    {
        if (!IsServer) return;
        direction = direction.normalized;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, Mathf.Max(0.01f, hitRadius), direction, range);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(Mathf.Max(0, damage));
                return;
            }

            PlayerHealth target = hit.collider.GetComponentInParent<PlayerHealth>();
            if (target != null && target != this)
            {
                if (target.AllowWeaponDamageFromPlayers)
                    target.ApplyDamage(Mathf.Max(0, damage));
                return;
            }
            if (IsWallCollider(hit.collider)) return;
        }
    }

    bool IsWallCollider(Collider2D collider)
    {
        Renderer[] renderers = collider.GetComponentsInChildren<Renderer>();
        foreach (Renderer hitRenderer in renderers)
            if (hitRenderer.sortingLayerID == WallsSortingLayerId) return true;

        Renderer ownRenderer = collider.GetComponent<Renderer>();
        if (ownRenderer != null) return ownRenderer.sortingLayerID == WallsSortingLayerId;
        Renderer parentRenderer = collider.GetComponentInParent<Renderer>();
        return parentRenderer != null && parentRenderer.sortingLayerID == WallsSortingLayerId;
    }

    public void RecoverHealth(float amount)
    {
        if (IsSpawned && !IsServer) RecoverServerRpc(amount);
        else ApplyRecovery(amount);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void DamageServerRpc(float amount) => ApplyDamage(amount);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RecoverServerRpc(float amount) => ApplyRecovery(amount);

    void ApplyDamage(float amount)
    {
        if (IsDead || isInvulnerable) return;
        float armor = IsSpawned ? networkArmor.Value : playerConfig.Armor;
        float health = IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
        float previousTotal = armor + health;
        float remaining = Mathf.Max(0, amount - armor);
        armor = Mathf.Max(0, armor - amount);
        health = Mathf.Max(0, health - remaining);
        float appliedDamage = Mathf.Max(0f, previousTotal - armor - health);
        if (IsSpawned)
        {
            networkArmor.Value = armor;
            networkHealth.Value = health;
            if (appliedDamage > 0f && IsServer)
                ShowDamagePopupClientRpc(appliedDamage);
            if (health <= 0 && IsServer) StartRespawn();
        }
        else
        {
            playerConfig.Armor = armor;
            playerConfig.CurrentHealth = health;
            ShowDamagePopup(appliedDamage);
            if (health <= 0) StartRespawn();
        }
    }

    void StartRespawn()
    {
        if (respawnRoutine != null) return;

        if (IsSpawned)
        {
            if (!IsServer) return;
            networkIsDead.Value = true;
        }
        else
        {
            offlineIsDead = true;
            SetDeadState(true);
        }

        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        isReloading = false;
        StopMotion();

        yield return new WaitForSeconds(respawnDelay);

        Vector3 respawnPosition = GetRespawnPosition();
        TeleportTo(respawnPosition);

        ResetResources();

        if (IsSpawned)
        {
            networkIsDead.Value = false;
            TeleportClientRpc(respawnPosition);
        }
        else
        {
            offlineIsDead = false;
            SetDeadState(false);
        }

        isInvulnerable = true;
        yield return new WaitForSeconds(respawnInvulnerableTime);
        isInvulnerable = false;
        respawnRoutine = null;
    }

    void ResetResources()
    {
        if (playerConfig == null) return;

        if (IsSpawned)
        {
            networkHealth.Value = playerConfig.MaxHealth;
            networkArmor.Value = playerConfig.MaxArmor;
            networkEnergy.Value = playerConfig.MaxEnergy;
            networkAmmo.Value = startingAmmo;
            networkReserveAmmo.Value = startingReserveAmmo;
        }
        else
        {
            playerConfig.CurrentHealth = playerConfig.MaxHealth;
            playerConfig.Armor = playerConfig.MaxArmor;
            playerConfig.Energy = playerConfig.MaxEnergy;
            offlineAmmo = startingAmmo;
            offlineReserveAmmo = startingReserveAmmo;
        }
    }

    Vector3 GetRespawnPosition()
    {
        Transform checkpoint = FindRespawnTransform(checkpointObjectName) ?? FindRespawnTransform("CheckPoint");
        Transform spawnPoint = checkpoint != null ? checkpoint : FindRespawnTransform(spawnPointObjectName);
        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        if (IsSpawned)
            position += Vector3.right * ((OwnerClientId % 4) * 1.5f);

        return position;
    }

    Transform FindRespawnTransform(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName)) return null;
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    void TeleportTo(Vector3 position)
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.position = position;
        }
        transform.position = position;
    }

    [Rpc(SendTo.ClientsAndHost)]
    void TeleportClientRpc(Vector3 position)
    {
        TeleportTo(position);
    }

    void StopMotion()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    void HandleDeadStateChanged(bool previousValue, bool newValue)
    {
        SetDeadState(newValue);
    }

    void SetDeadState(bool dead)
    {
        StopMotion();

        if (colliders != null)
        {
            foreach (Collider2D playerCollider in colliders)
            {
                if (playerCollider != null)
                    playerCollider.enabled = !dead;
            }
        }

        if (spriteRenderers != null)
        {
            foreach (SpriteRenderer renderer in spriteRenderers)
            {
                if (renderer == null) continue;
                Color color = renderer.color;
                color.a = dead ? 0.35f : 1f;
                renderer.color = color;
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowDamagePopupClientRpc(float amount)
    {
        ShowDamagePopup(amount);
    }

    void ShowDamagePopup(float amount)
    {
        if (amount <= 0f) return;
        EnemyDamagePopup.Show(transform.position, amount);
    }

    void ApplyRecovery(float amount)
    {
        float value = Mathf.Min(playerConfig.MaxHealth, CurrentHealth + amount);
        if (IsSpawned) networkHealth.Value = value;
        else playerConfig.CurrentHealth = value;
    }

    void SyncConfig()
    {
        if (playerConfig == null || !IsSpawned) return;
        playerConfig.CurrentHealth = networkHealth.Value;
        playerConfig.Armor = networkArmor.Value;
        playerConfig.Energy = networkEnergy.Value;
    }

    void BindLocalUI()
    {
        if (GameManager.Instance != null) playerConfig.Name = GameManager.Instance.CurrentUsername;
        if (UIManager.Instance != null) UIManager.Instance.SetPlayerConfig(playerConfig);
    }
}
