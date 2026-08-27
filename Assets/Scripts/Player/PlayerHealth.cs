using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : NetworkBehaviour
{
    const int WallsSortingLayerId = unchecked((int)2393433307u);
    static float nextServerPortalRequestTime;
    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] int magazineSize = 30;
    [SerializeField] float reloadDuration = 1.2f;
    [SerializeField] float energyRegenerationDelay = 15f;
    [SerializeField] float energyRegenerationPerSecond = 1f;
    [SerializeField] float healthRegenerationPerSecond = 1f;
    [SerializeField] float armorRegenerationPerSecond = 0.2f;
    [SerializeField] bool allowWeaponDamageFromPlayers;
    [Header("Respawn")]
    [SerializeField] int maxLives = 3;
    [SerializeField] float respawnDelay = 2f;
    [SerializeField] float respawnInvulnerableTime = 1.5f;
    [SerializeField] string checkpointObjectName = "Checkpoint";
    [SerializeField] string spawnPointObjectName = "SpawnPoint";

    readonly NetworkVariable<float> networkHealth = new(0);
    readonly NetworkVariable<float> networkArmor = new(0);
    readonly NetworkVariable<float> networkEnergy = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkMagazineAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkLives = new(0);
    readonly NetworkVariable<bool> networkIsDead = new(false);
    float lastShotTime = float.NegativeInfinity;
    float lastCombatEndTime = float.NegativeInfinity;
    float reloadCompleteTime;
    int offlineMagazineAmmo;
    int offlineLives;
    bool isReloading;
    bool currentWeaponUsesAmmo = true;
    bool isInCombat;
    bool offlineIsDead;
    bool isInvulnerable;
    Coroutine respawnRoutine;
    Collider2D[] colliders;
    SpriteRenderer[] spriteRenderers;
    Rigidbody2D rb;

    public PlayerConfig PlayerConfig => playerConfig;
    public float CurrentHealth => IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
    public float CurrentArmor => IsSpawned ? networkArmor.Value : playerConfig.Armor;
    public float CurrentEnergy => IsSpawned ? networkEnergy.Value : playerConfig.Energy;
    public int CurrentAmmo => IsSpawned ? networkMagazineAmmo.Value : offlineMagazineAmmo;
    public int MaxAmmo => magazineSize;
    public int CurrentReserveAmmo => 0;
    public int CurrentLives => IsSpawned ? networkLives.Value : offlineLives;
    public int MaxLives => Mathf.Max(1, maxLives);
    public bool IsReloading => isReloading;
    public bool CurrentWeaponUsesAmmo => currentWeaponUsesAmmo;
    public bool AllowWeaponDamageFromPlayers => allowWeaponDamageFromPlayers;
    public bool IsDead => IsSpawned ? networkIsDead.Value : offlineIsDead;
    public bool IsOutOfLives => CurrentLives <= 0 && IsDead;

    public static bool AreAllSpawnedPlayersOutOfLives()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool foundPlayer = false;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned) continue;

            foundPlayer = true;
            if (!player.IsOutOfLives)
                return false;
        }

        return foundPlayer;
    }

    public static bool AreAllSpawnedPlayersDead()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool foundPlayer = false;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned) continue;

            foundPlayer = true;
            if (!player.IsDead)
                return false;
        }

        return foundPlayer;
    }

    public static bool HasAnySpawnedPlayerOutOfLives()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        foreach (PlayerHealth player in players)
        {
            if (player != null && player.IsSpawned && player.IsOutOfLives)
                return true;
        }

        return false;
    }

    public static bool IsSpawnedTeamLost()
    {
        return AreAllSpawnedPlayersOutOfLives();
    }

    void Awake()
    {
        if (playerConfig != null) playerConfig = Instantiate(playerConfig);
        offlineMagazineAmmo = magazineSize;
        offlineLives = MaxLives;
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
            networkLives.Value = MaxLives;
            networkIsDead.Value = false;
        }
        if (IsOwner)
        {
            networkEnergy.Value = playerConfig.MaxEnergy;
            networkMagazineAmmo.Value = magazineSize;
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
            offlineLives = MaxLives;
            BindLocalUI();
        }
    }

    void Update()
    {
        SyncConfig();
        bool canControlOwnerResources = !IsSpawned || IsOwner;
        if (canControlOwnerResources)
        {
            RegenerateEnergy();
            UpdateReload();
        }

        if (!IsSpawned || IsServer)
            RegenerateHealthAndArmor();

        if (!canControlOwnerResources || Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame) TryStartReload();
        if (Keyboard.current.pKey.wasPressedThisFrame) RecoverHealth(1);
    }

    void OnEnable()
    {
        Room.OnCombatStartedEvent += HandleCombatStarted;
        Room.OnCombatEndedEvent += HandleCombatEnded;
    }

    void OnDisable()
    {
        Room.OnCombatStartedEvent -= HandleCombatStarted;
        Room.OnCombatEndedEvent -= HandleCombatEnded;
    }

    public bool TryConsumeShot(float energyCost)
    {
        if (IsDead || IsSpawned && !IsOwner || isReloading) return false;
        float energy = IsSpawned ? networkEnergy.Value : playerConfig.Energy;
        int ammo = IsSpawned ? networkMagazineAmmo.Value : offlineMagazineAmmo;
        if (energy < energyCost || ammo <= 0) return false;
        if (IsSpawned)
        {
            networkEnergy.Value = energy - energyCost;
            networkMagazineAmmo.Value = ammo - 1;
        }
        else
        {
            playerConfig.Energy = energy - energyCost;
            offlineMagazineAmmo = ammo - 1;
        }
        lastShotTime = Time.time;
        return true;
    }

    public void ConfigureWeapon(ItemWeapon weaponData)
    {
        if (weaponData == null) return;

        currentWeaponUsesAmmo = weaponData.Type != WeaponType.Melee;
        isReloading = false;
        magazineSize = currentWeaponUsesAmmo ? Mathf.Max(1, weaponData.MagazineSize) : 0;
        reloadDuration = Mathf.Max(0.1f, weaponData.ReloadDuration);
        offlineMagazineAmmo = currentWeaponUsesAmmo ? magazineSize : 0;

        if (IsSpawned && IsOwner)
            networkMagazineAmmo.Value = currentWeaponUsesAmmo ? magazineSize : 0;
    }

    public bool TryStartReload()
    {
        if (!currentWeaponUsesAmmo) return false;
        if (IsDead || IsSpawned && !IsOwner || isReloading || CurrentAmmo >= MaxAmmo) return false;

        isReloading = true;
        reloadCompleteTime = Time.time + reloadDuration;
        return true;
    }

    void UpdateReload()
    {
        if (!isReloading || Time.time < reloadCompleteTime) return;

        if (IsSpawned) networkMagazineAmmo.Value = magazineSize;
        else offlineMagazineAmmo = magazineSize;

        isReloading = false;
    }

    void RegenerateEnergy()
    {
        if (playerConfig == null || isInCombat) return;
        if (Time.time - lastCombatEndTime < energyRegenerationDelay) return;
        if (Time.time - lastShotTime < energyRegenerationDelay) return;

        float energy = IsSpawned ? networkEnergy.Value : playerConfig.Energy;
        if (energy >= playerConfig.MaxEnergy) return;
        energy = Mathf.Min(playerConfig.MaxEnergy, energy + energyRegenerationPerSecond * Time.deltaTime);
        if (IsSpawned) networkEnergy.Value = energy;
        else playerConfig.Energy = energy;
    }

    void RegenerateHealthAndArmor()
    {
        if (playerConfig == null || isInCombat || IsDead) return;
        if (Time.time - lastCombatEndTime < energyRegenerationDelay) return;
        if (Time.time - lastShotTime < energyRegenerationDelay) return;

        float health = IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
        float armor = IsSpawned ? networkArmor.Value : playerConfig.Armor;

        bool changed = false;
        if (health < playerConfig.MaxHealth)
        {
            health = Mathf.Min(playerConfig.MaxHealth, health + healthRegenerationPerSecond * Time.deltaTime);
            changed = true;
        }

        if (armor < playerConfig.MaxArmor)
        {
            armor = Mathf.Min(playerConfig.MaxArmor, armor + armorRegenerationPerSecond * Time.deltaTime);
            changed = true;
        }

        if (!changed) return;

        if (IsSpawned)
        {
            networkHealth.Value = health;
            networkArmor.Value = armor;
        }
        else
        {
            playerConfig.CurrentHealth = health;
            playerConfig.Armor = armor;
        }
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

    public void SubmitMelee(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        if (!IsSpawned || !IsOwner) return;
        MeleeServerRpc(origin, direction.normalized, damage, range, hitRadius);
    }

    [Rpc(SendTo.Server)]
    void ShotServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        ResolveServerShot(origin, direction, damage, range, hitRadius);
    }

    [Rpc(SendTo.Server)]
    void MeleeServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        ResolveServerMelee(origin, direction, damage, range, hitRadius);
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

            Door door = hit.collider.GetComponentInParent<Door>();
            if (door != null && door.BlocksProjectiles) return;

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

            BreakableBox box = hit.collider.GetComponentInParent<BreakableBox>();
            if (box != null)
            {
                box.TakeDamage(Mathf.Max(0, damage));
                return;
            }

            if (IsWallCollider(hit.collider)) return;
        }
    }

    public void ResolveServerMelee(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        if (!IsServer) return;

        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector2 hitCenter = origin + direction * Mathf.Max(0.05f, range);
        Collider2D[] hits = Physics2D.OverlapCircleAll(hitCenter, Mathf.Max(0.05f, hitRadius));

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.IsChildOf(transform)) continue;

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

    public bool CanRecoverHealth(float amount = 0.01f)
    {
        return playerConfig != null && !IsDead && CurrentHealth < playerConfig.MaxHealth - Mathf.Max(0.01f, amount * 0.001f);
    }

    public void RecoverEnergy(float amount)
    {
        if (IsSpawned && !IsServer) RecoverEnergyServerRpc(amount);
        else ApplyEnergyRecovery(amount);
    }

    public bool CanRecoverEnergy(float amount = 0.01f)
    {
        return playerConfig != null && !IsDead && CurrentEnergy < playerConfig.MaxEnergy - Mathf.Max(0.01f, amount * 0.001f);
    }

    public void RecoverArmor(float amount)
    {
        if (IsSpawned && !IsServer) RecoverArmorServerRpc(amount);
        else ApplyArmorRecovery(amount);
    }

    public bool CanRecoverArmor(float amount = 0.01f)
    {
        return playerConfig != null && !IsDead && CurrentArmor < playerConfig.MaxArmor - Mathf.Max(0.01f, amount * 0.001f);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void DamageServerRpc(float amount) => ApplyDamage(amount);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RecoverServerRpc(float amount) => ApplyRecovery(amount);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RecoverEnergyServerRpc(float amount) => ApplyEnergyRecovery(amount);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    void RecoverArmorServerRpc(float amount) => ApplyArmorRecovery(amount);

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
        if (respawnRoutine != null || IsDead) return;

        if (IsSpawned)
        {
            if (!IsServer) return;
            networkLives.Value = Mathf.Max(0, networkLives.Value - 1);
            networkIsDead.Value = true;

            if (AllNetworkPlayersOutOfLives())
            {
                ShowTeamLostPanelClientRpc();
                return;
            }

            if (networkLives.Value <= 0)
                return;

            if (!AllNetworkPlayersDead())
                return;

            if (LevelManager.Instance != null)
                LevelManager.Instance.ResetCurrentRoomEncounter();

            RespawnAllDeadNetworkPlayers();
            return;
        }
        else
        {
            offlineLives = Mathf.Max(0, offlineLives - 1);
            offlineIsDead = true;
            SetDeadState(true);

            if (offlineLives <= 0)
            {
                ShowLocalLostPanel();
                return;
            }

            if (LevelManager.Instance != null)
                LevelManager.Instance.ResetCurrentRoomEncounter();
        }

        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    bool AllNetworkPlayersDead()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool foundPlayer = false;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned) continue;

            foundPlayer = true;
            if (!player.IsDead)
                return false;
        }

        return foundPlayer;
    }

    bool AllNetworkPlayersOutOfLives()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        bool foundPlayer = false;

        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned) continue;

            foundPlayer = true;
            if (!player.IsOutOfLives)
                return false;
        }

        return foundPlayer;
    }

    void RespawnAllDeadNetworkPlayers()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.IsSpawned || !player.IsDead) continue;
            if (player.IsOutOfLives) continue;
            player.StartTeamRespawn();
        }
    }

    void StartTeamRespawn()
    {
        if (IsOutOfLives) return;
        if (respawnRoutine != null) return;
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
            networkMagazineAmmo.Value = magazineSize;
        }
        else
        {
            playerConfig.CurrentHealth = playerConfig.MaxHealth;
            playerConfig.Armor = playerConfig.MaxArmor;
            playerConfig.Energy = playerConfig.MaxEnergy;
            offlineMagazineAmmo = magazineSize;
        }
    }

    void HandleCombatStarted(Room room)
    {
        isInCombat = true;
    }

    void HandleCombatEnded(Room room)
    {
        isInCombat = false;
        lastCombatEndTime = Time.time;
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

        if (dead && IsSpawned && IsOwner && IsOutOfLives)
            FollowAliveTeammateCamera();
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

    void ApplyEnergyRecovery(float amount)
    {
        float currentEnergy = IsSpawned ? networkEnergy.Value : playerConfig.Energy;
        float value = Mathf.Min(playerConfig.MaxEnergy, currentEnergy + Mathf.Max(0f, amount));
        if (IsSpawned) networkEnergy.Value = value;
        else playerConfig.Energy = value;
    }

    void ApplyArmorRecovery(float amount)
    {
        float currentArmor = IsSpawned ? networkArmor.Value : playerConfig.Armor;
        float value = Mathf.Min(playerConfig.MaxArmor, currentArmor + Mathf.Max(0f, amount));
        if (IsSpawned) networkArmor.Value = value;
        else playerConfig.Armor = value;
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

    public void RequestTeamRetryToRoomLobby()
    {
        if (!IsSpawned || !IsOwner) return;
        RequestTeamRetryToRoomLobbyServerRpc();
    }

    public void RequestPortalTeleport(
        Vector3 playerArrivalOffset,
        bool showLoadingScreen,
        float loadingBeforeMapSwitch,
        float loadingAfterMapSwitch)
    {
        if (!IsSpawned)
        {
            PlayPortalTeleportLocally(playerArrivalOffset, showLoadingScreen, loadingBeforeMapSwitch, loadingAfterMapSwitch);
            return;
        }

        if (!IsOwner) return;

        RequestPortalTeleportServerRpc(
            playerArrivalOffset,
            showLoadingScreen,
            loadingBeforeMapSwitch,
            loadingAfterMapSwitch);
    }

    [Rpc(SendTo.Server)]
    void RequestPortalTeleportServerRpc(
        Vector3 playerArrivalOffset,
        bool showLoadingScreen,
        float loadingBeforeMapSwitch,
        float loadingAfterMapSwitch)
    {
        if (Time.unscaledTime < nextServerPortalRequestTime)
            return;

        nextServerPortalRequestTime = Time.unscaledTime + 1.25f;
        PortalTeleportClientRpc(
            playerArrivalOffset,
            showLoadingScreen,
            loadingBeforeMapSwitch,
            loadingAfterMapSwitch);
    }

    [Rpc(SendTo.ClientsAndHost)]
    void PortalTeleportClientRpc(
        Vector3 playerArrivalOffset,
        bool showLoadingScreen,
        float loadingBeforeMapSwitch,
        float loadingAfterMapSwitch)
    {
        PlayPortalTeleportLocally(playerArrivalOffset, showLoadingScreen, loadingBeforeMapSwitch, loadingAfterMapSwitch);
    }

    void PlayPortalTeleportLocally(
        Vector3 playerArrivalOffset,
        bool showLoadingScreen,
        float loadingBeforeMapSwitch,
        float loadingAfterMapSwitch)
    {
        if (LevelManager.Instance == null)
            return;

        if (showLoadingScreen)
        {
            PortalMapLoadingUI.Instance.PlayTransition(
                loadingBeforeMapSwitch,
                () => LevelManager.Instance != null &&
                      LevelManager.Instance.LoadNextDungeonFromPortal(playerArrivalOffset),
                loadingAfterMapSwitch);
        }
        else
        {
            LevelManager.Instance.LoadNextDungeonFromPortal(playerArrivalOffset);
        }
    }

    [Rpc(SendTo.Server)]
    void RequestTeamRetryToRoomLobbyServerRpc()
    {
        if (!AllNetworkPlayersOutOfLives())
            return;

        ReturnTeamToRoomLobbyClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShowTeamLostPanelClientRpc()
    {
        ShowLocalLostPanel();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ReturnTeamToRoomLobbyClientRpc()
    {
        InGameHUDUIManager hud = FindAnyObjectByType<InGameHUDUIManager>(FindObjectsInactive.Include);
        if (hud != null)
            hud.ReturnToRoomLobbyAfterTeamLost();
    }

    void ShowLocalLostPanel()
    {
        InGameHUDUIManager hud = FindAnyObjectByType<InGameHUDUIManager>(FindObjectsInactive.Include);
        if (hud != null)
            hud.ShowLostPanel();
    }

    void FollowAliveTeammateCamera()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>();
        foreach (PlayerHealth player in players)
        {
            if (player == null || player == this || !player.IsSpawned) continue;
            if (player.IsOutOfLives || player.IsDead) continue;

            PlayerMovement.FollowWithCamera(player.transform);
            return;
        }
    }
}
