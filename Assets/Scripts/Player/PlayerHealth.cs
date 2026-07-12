using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] PlayerConfig playerConfig;
    [SerializeField] int startingAmmo = 30;
    [SerializeField] int startingReserveAmmo = 120;
    [SerializeField] float energyRegenerationDelay = 1.5f;
    [SerializeField] float energyRegenerationPerSecond = 10f;
    [SerializeField] float reloadDuration = 1.2f;

    readonly NetworkVariable<float> networkHealth = new(0);
    readonly NetworkVariable<float> networkArmor = new(0);
    readonly NetworkVariable<float> networkEnergy = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkReserveAmmo = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    float lastShotTime = float.NegativeInfinity;
    float reloadCompleteTime;
    int offlineAmmo;
    int offlineReserveAmmo;
    bool isReloading;

    public PlayerConfig PlayerConfig => playerConfig;
    public float CurrentHealth => IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
    public float CurrentEnergy => IsSpawned ? networkEnergy.Value : playerConfig.Energy;
    public int CurrentAmmo => IsSpawned ? networkAmmo.Value : offlineAmmo;
    public int MaxAmmo => startingAmmo;
    public int CurrentReserveAmmo => IsSpawned ? networkReserveAmmo.Value : offlineReserveAmmo;
    public bool IsReloading => isReloading;

    void Awake()
    {
        if (playerConfig != null) playerConfig = Instantiate(playerConfig);
        offlineAmmo = startingAmmo;
        offlineReserveAmmo = startingReserveAmmo;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkHealth.Value = playerConfig.MaxHealth;
            networkArmor.Value = playerConfig.MaxArmor;
        }
        if (IsOwner)
        {
            networkEnergy.Value = playerConfig.MaxEnergy;
            networkAmmo.Value = startingAmmo;
            networkReserveAmmo.Value = startingReserveAmmo;
            BindLocalUI();
        }
        SyncConfig();
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
        if (IsSpawned && !IsOwner || isReloading) return false;
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
        if (IsSpawned && !IsOwner || isReloading || CurrentAmmo >= MaxAmmo || CurrentReserveAmmo <= 0) return false;
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
        if (IsSpawned && !IsServer) DamageServerRpc(amount);
        else ApplyDamage(amount);
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
        float armor = IsSpawned ? networkArmor.Value : playerConfig.Armor;
        float health = IsSpawned ? networkHealth.Value : playerConfig.CurrentHealth;
        float remaining = Mathf.Max(0, amount - armor);
        armor = Mathf.Max(0, armor - amount);
        health = Mathf.Max(0, health - remaining);
        if (IsSpawned)
        {
            networkArmor.Value = armor;
            networkHealth.Value = health;
            if (health <= 0 && IsServer) NetworkObject.Despawn();
        }
        else
        {
            playerConfig.Armor = armor;
            playerConfig.CurrentHealth = health;
            if (health <= 0) Destroy(gameObject);
        }
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
