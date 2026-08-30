using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MultiplayerGameplaySync : MonoBehaviour
{
    const string EnemyDamageRequest = "tdm_enemy_damage_request";
    const string EnemyHealthState = "tdm_enemy_health_state";
    const string BoxDamageRequest = "tdm_box_damage_request";
    const string BoxBrokenState = "tdm_box_broken_state";
    const string PickupRequest = "tdm_pickup_request";
    const string PickupConsumedState = "tdm_pickup_consumed_state";
    const string CoinGainState = "tdm_coin_gain_state";
    const string EnemyTransformState = "tdm_enemy_transform_state";
    const string EnemyProjectileState = "tdm_enemy_projectile_state";
    const string DoorState = "tdm_door_state";
    const string WeaponChestOpenRequest = "tdm_weapon_chest_open_request";
    const string WeaponChestOpenedState = "tdm_weapon_chest_opened_state";
    const string RewardChestSpawnedState = "tdm_reward_chest_spawned_state";
    const string LoadSceneState = "tdm_load_scene_state";

    static MultiplayerGameplaySync instance;
    NetworkManager registeredManager;

    public static MultiplayerGameplaySync Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("MultiplayerGameplaySync");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MultiplayerGameplaySync>();
            }

            return instance;
        }
    }

    public static bool IsNetworkActive =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    public static bool IsServer =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    public static bool TryGetId(Component component, out string id)
    {
        id = null;
        if (component == null) return false;
        NetworkedWorldEntity entity = component.GetComponent<NetworkedWorldEntity>()
                                      ?? component.GetComponentInParent<NetworkedWorldEntity>()
                                      ?? component.GetComponentInChildren<NetworkedWorldEntity>(true);
        if (entity == null || string.IsNullOrWhiteSpace(entity.NetworkId)) return false;
        id = entity.NetworkId;
        return true;
    }

    public static void RequestEnemyDamage(EnemyHealth enemy, float amount)
    {
        if (!IsNetworkActive || enemy == null || amount <= 0f) return;
        Instance.EnsureRegistered();
        if (!TryGetId(enemy, out string id)) return;

        if (IsServer)
        {
            enemy.ApplyDamageAuthoritative(amount, true);
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(amount);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(EnemyDamageRequest, NetworkManager.ServerClientId, writer);
    }

    public static void BroadcastEnemyHealth(EnemyHealth enemy, float amount)
    {
        if (!IsNetworkActive || !IsServer || enemy == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(enemy, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(enemy.CurrentHealth);
        writer.WriteValueSafe(enemy.MaxHealthValue);
        writer.WriteValueSafe(enemy.IsDead);
        writer.WriteValueSafe(Mathf.Max(0f, amount));
        writer.WriteValueSafe(enemy.transform.position);
        Broadcast(EnemyHealthState, writer);
    }

    public static void RequestBoxDamage(BreakableBox box, float amount)
    {
        if (!IsNetworkActive || box == null || amount <= 0f) return;
        Instance.EnsureRegistered();
        if (!TryGetId(box, out string id)) return;

        if (IsServer)
        {
            box.ApplyDamageAuthoritative(amount, true);
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(amount);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(BoxDamageRequest, NetworkManager.ServerClientId, writer);
    }

    public static void BroadcastBoxBroken(BreakableBox box, int lootIndex, Vector3 dropPosition)
    {
        if (!IsNetworkActive || !IsServer || box == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(box, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(lootIndex);
        writer.WriteValueSafe(dropPosition);
        Broadcast(BoxBrokenState, writer);
    }

    public static void RequestPickup(PickupItem pickup, PlayerHealth player)
    {
        if (!IsNetworkActive || pickup == null || player == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(pickup, out string id)) return;

        ulong ownerClientId = player.IsSpawned ? player.OwnerClientId : NetworkManager.Singleton.LocalClientId;
        if (IsServer)
        {
            pickup.TryPickupAuthoritative(player, ownerClientId);
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(ownerClientId);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(PickupRequest, NetworkManager.ServerClientId, writer);
    }

    public static void BroadcastPickupConsumed(PickupItem pickup)
    {
        if (!IsNetworkActive || !IsServer || pickup == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(pickup, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        Broadcast(PickupConsumedState, writer);
    }

    public static void SendCoinGain(ulong clientId, int amount)
    {
        if (!IsNetworkActive || !IsServer || amount <= 0) return;
        Instance.EnsureRegistered();

        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(amount);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(CoinGainState, clientId, writer);
    }

    public static void DistributeCoinGain(ulong collectorClientId, int totalAmount)
    {
        if (!IsNetworkActive || !IsServer || totalAmount <= 0) return;
        Instance.EnsureRegistered();

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening) return;

        int playerCount = Mathf.Max(1, manager.ConnectedClientsIds.Count);
        int baseShare = Mathf.Max(1, totalAmount / playerCount);
        int remainder = Mathf.Max(0, totalAmount - baseShare * playerCount);

        foreach (ulong clientId in manager.ConnectedClientsIds)
        {
            int share = baseShare + (clientId == collectorClientId ? remainder : 0);
            if (share <= 0) continue;

            if (clientId == manager.LocalClientId)
                PickupItem.AddCoinsLocal(share);
            else
                SendCoinGain(clientId, share);
        }
    }

    public static void BroadcastEnemyTransform(EnemyStateMachine enemy, Vector3 position, bool facingLeft, bool moving)
    {
        if (!IsNetworkActive || !IsServer || enemy == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(enemy, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(position);
        writer.WriteValueSafe(facingLeft);
        writer.WriteValueSafe(moving);
        BroadcastToClientsOnly(EnemyTransformState, writer);
    }

    public static void BroadcastEnemyProjectile(Vector3 origin, Vector2 direction, float speed, float damage, float hitRadius, float lifetime)
    {
        if (!IsNetworkActive || !IsServer) return;
        Instance.EnsureRegistered();

        using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(origin);
        writer.WriteValueSafe(direction);
        writer.WriteValueSafe(speed);
        writer.WriteValueSafe(damage);
        writer.WriteValueSafe(hitRadius);
        writer.WriteValueSafe(lifetime);
        BroadcastToClientsOnly(EnemyProjectileState, writer);
    }

    public static void BroadcastDoorState(Door door, bool locked, bool open)
    {
        if (!IsNetworkActive || !IsServer || door == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(door, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(locked);
        writer.WriteValueSafe(open);
        BroadcastToClientsOnly(DoorState, writer);
    }

    public static void RequestWeaponChestOpen(WeaponChest chest, PlayerHealth player)
    {
        if (!IsNetworkActive || chest == null || player == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(chest, out string id)) return;

        ulong ownerClientId = player.IsSpawned ? player.OwnerClientId : NetworkManager.Singleton.LocalClientId;
        if (IsServer)
        {
            chest.TryOpenAuthoritative(player);
            return;
        }

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(ownerClientId);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(WeaponChestOpenRequest, NetworkManager.ServerClientId, writer);
    }

    public static void BroadcastWeaponChestOpened(WeaponChest chest, int weaponIndex, Vector3 dropPosition)
    {
        if (!IsNetworkActive || !IsServer || chest == null) return;
        Instance.EnsureRegistered();
        if (!TryGetId(chest, out string id)) return;

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(id);
        writer.WriteValueSafe(weaponIndex);
        writer.WriteValueSafe(dropPosition);
        BroadcastToClientsOnly(WeaponChestOpenedState, writer);
    }

    public static void BroadcastRewardChestSpawned(Room room, string chestId, Vector3 position)
    {
        if (!IsNetworkActive || !IsServer || room == null || string.IsNullOrWhiteSpace(chestId)) return;
        Instance.EnsureRegistered();
        if (!TryGetId(room, out string roomId)) return;

        using FastBufferWriter writer = new FastBufferWriter(512, Allocator.Temp);
        writer.WriteValueSafe(roomId);
        writer.WriteValueSafe(chestId);
        writer.WriteValueSafe(position);
        BroadcastToClientsOnly(RewardChestSpawnedState, writer);
    }

    public static void BroadcastLoadScene(string sceneName)
    {
        if (!IsNetworkActive || !IsServer || string.IsNullOrWhiteSpace(sceneName)) return;
        Instance.EnsureRegistered();
        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp);
        writer.WriteValueSafe(sceneName);
        BroadcastToClientsOnly(LoadSceneState, writer);
    }

    public static void Ensure()
    {
        _ = Instance;
    }

    void Update()
    {
        EnsureRegistered();
    }

    void EnsureRegistered()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening) return;
        if (registeredManager == manager) return;

        Unregister();
        registeredManager = manager;
        var messages = manager.CustomMessagingManager;
        messages.RegisterNamedMessageHandler(EnemyDamageRequest, HandleEnemyDamageRequest);
        messages.RegisterNamedMessageHandler(EnemyHealthState, HandleEnemyHealthState);
        messages.RegisterNamedMessageHandler(BoxDamageRequest, HandleBoxDamageRequest);
        messages.RegisterNamedMessageHandler(BoxBrokenState, HandleBoxBrokenState);
        messages.RegisterNamedMessageHandler(PickupRequest, HandlePickupRequest);
        messages.RegisterNamedMessageHandler(PickupConsumedState, HandlePickupConsumedState);
        messages.RegisterNamedMessageHandler(CoinGainState, HandleCoinGainState);
        messages.RegisterNamedMessageHandler(EnemyTransformState, HandleEnemyTransformState);
        messages.RegisterNamedMessageHandler(EnemyProjectileState, HandleEnemyProjectileState);
        messages.RegisterNamedMessageHandler(DoorState, HandleDoorState);
        messages.RegisterNamedMessageHandler(WeaponChestOpenRequest, HandleWeaponChestOpenRequest);
        messages.RegisterNamedMessageHandler(WeaponChestOpenedState, HandleWeaponChestOpenedState);
        messages.RegisterNamedMessageHandler(RewardChestSpawnedState, HandleRewardChestSpawnedState);
        messages.RegisterNamedMessageHandler(LoadSceneState, HandleLoadSceneState);
    }

    void OnDestroy()
    {
        Unregister();
    }

    void Unregister()
    {
        if (registeredManager == null) return;
        var messages = registeredManager.CustomMessagingManager;
        if (messages != null)
        {
            messages.UnregisterNamedMessageHandler(EnemyDamageRequest);
            messages.UnregisterNamedMessageHandler(EnemyHealthState);
            messages.UnregisterNamedMessageHandler(BoxDamageRequest);
            messages.UnregisterNamedMessageHandler(BoxBrokenState);
            messages.UnregisterNamedMessageHandler(PickupRequest);
            messages.UnregisterNamedMessageHandler(PickupConsumedState);
            messages.UnregisterNamedMessageHandler(CoinGainState);
            messages.UnregisterNamedMessageHandler(EnemyTransformState);
            messages.UnregisterNamedMessageHandler(EnemyProjectileState);
            messages.UnregisterNamedMessageHandler(DoorState);
            messages.UnregisterNamedMessageHandler(WeaponChestOpenRequest);
            messages.UnregisterNamedMessageHandler(WeaponChestOpenedState);
            messages.UnregisterNamedMessageHandler(RewardChestSpawnedState);
            messages.UnregisterNamedMessageHandler(LoadSceneState);
        }
        registeredManager = null;
    }

    void HandleEnemyDamageRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out float amount);
        if (NetworkedWorldEntity.TryFind(id, out EnemyHealth enemy))
            enemy.ApplyDamageAuthoritative(amount, true);
    }

    void HandleEnemyHealthState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out float currentHealth);
        reader.ReadValueSafe(out float maxHealth);
        reader.ReadValueSafe(out bool dead);
        reader.ReadValueSafe(out float amount);
        reader.ReadValueSafe(out Vector3 position);
        if (NetworkedWorldEntity.TryFind(id, out EnemyHealth enemy))
            enemy.ApplyRemoteState(currentHealth, maxHealth, dead, amount, position);
    }

    void HandleBoxDamageRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out float amount);
        if (NetworkedWorldEntity.TryFind(id, out BreakableBox box))
            box.ApplyDamageAuthoritative(amount, true);
    }

    void HandleBoxBrokenState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int lootIndex);
        reader.ReadValueSafe(out Vector3 dropPosition);
        if (NetworkedWorldEntity.TryFind(id, out BreakableBox box))
            box.BreakRemote(lootIndex, dropPosition);
    }

    void HandlePickupRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out ulong ownerClientId);
        if (!NetworkedWorldEntity.TryFind(id, out PickupItem pickup)) return;
        PlayerHealth player = FindPlayer(ownerClientId);
        if (player != null)
            pickup.TryPickupAuthoritative(player, ownerClientId);
    }

    void HandlePickupConsumedState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        if (NetworkedWorldEntity.TryFind(id, out PickupItem pickup))
            pickup.ConsumeRemote();
    }

    void HandleCoinGainState(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int amount);
        PickupItem.AddCoinsLocal(amount);
    }

    void HandleEnemyTransformState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out Vector3 position);
        reader.ReadValueSafe(out bool facingLeft);
        reader.ReadValueSafe(out bool moving);
        if (NetworkedWorldEntity.TryFind(id, out EnemyStateMachine enemy))
            enemy.ApplyRemoteTransform(position, facingLeft, moving);
    }

    void HandleEnemyProjectileState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out Vector3 origin);
        reader.ReadValueSafe(out Vector2 direction);
        reader.ReadValueSafe(out float speed);
        reader.ReadValueSafe(out float damage);
        reader.ReadValueSafe(out float hitRadius);
        reader.ReadValueSafe(out float lifetime);

        GameObject projectileObject = new GameObject("EnemyProjectile_RemoteVisual");
        projectileObject.transform.position = origin;
        EnemyProjectile projectile = projectileObject.AddComponent<EnemyProjectile>();
        projectile.Initialize(direction, speed, damage, hitRadius, lifetime, null, null);
    }

    void HandleDoorState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out bool locked);
        reader.ReadValueSafe(out bool open);
        if (NetworkedWorldEntity.TryFind(id, out Door door))
            door.ApplyRemoteState(locked, open);
    }

    void HandleWeaponChestOpenRequest(ulong senderClientId, FastBufferReader reader)
    {
        if (!IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out ulong ownerClientId);
        if (!NetworkedWorldEntity.TryFind(id, out WeaponChest chest)) return;
        PlayerHealth player = FindPlayer(ownerClientId);
        if (player != null)
            chest.TryOpenAuthoritative(player);
    }

    void HandleWeaponChestOpenedState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string id);
        reader.ReadValueSafe(out int weaponIndex);
        reader.ReadValueSafe(out Vector3 dropPosition);
        if (NetworkedWorldEntity.TryFind(id, out WeaponChest chest))
            chest.ApplyRemoteOpened(weaponIndex, dropPosition);
    }

    void HandleRewardChestSpawnedState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string roomId);
        reader.ReadValueSafe(out string chestId);
        reader.ReadValueSafe(out Vector3 position);
        if (NetworkedWorldEntity.TryFind(roomId, out Room room))
            room.ApplyRemoteRewardChestSpawned(chestId, position);
    }

    static PlayerHealth FindPlayer(ulong ownerClientId)
    {
        foreach (PlayerHealth player in FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude))
            if (player != null && player.IsSpawned && player.OwnerClientId == ownerClientId)
                return player;
        return null;
    }

    void HandleLoadSceneState(ulong senderClientId, FastBufferReader reader)
    {
        if (IsServer) return;
        reader.ReadValueSafe(out string sceneName);
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        if (SceneManager.GetActiveScene().name == sceneName) return;
        SceneManager.LoadScene(sceneName);
    }

    static void Broadcast(string messageName, FastBufferWriter writer)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer) return;

        foreach (ulong clientId in manager.ConnectedClientsIds)
            manager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
    }

    static void BroadcastToClientsOnly(string messageName, FastBufferWriter writer)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening || !manager.IsServer) return;

        foreach (ulong clientId in manager.ConnectedClientsIds)
        {
            if (clientId == manager.LocalClientId) continue;
            manager.CustomMessagingManager.SendNamedMessage(messageName, clientId, writer);
        }
    }
}
