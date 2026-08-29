using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeaponController : NetworkBehaviour
{
    [SerializeField] GameObject startingWeaponPrefab;
    [SerializeField] GameObject startingGunPrefab;
    [SerializeField] GameObject startingMeleeWeaponPrefab;
    [SerializeField] Vector3 socketLocalPosition = new(0.34f, -0.1f, -0.1f);
    [SerializeField] Vector3 socketLocalEulerAngles;
    [SerializeField] float aimSyncThreshold = 0.5f;
    [Header("Drop / Pickup")]
    [SerializeField] Key dropWeaponKey = Key.G;
    [SerializeField] float dropDistance = 0.75f;
    [SerializeField] float dropPickupDelay = 0.45f;
    [SerializeField] float droppedWeaponScale = 1.15f;
    [SerializeField] float pickupColliderRadius = 0.45f;

    readonly NetworkVariable<float> networkAimAngle = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkSelectedSlotIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly Weapon[] slotWeapons = new Weapon[2];
    float lastSentAimAngle;
    int selectedSlotIndex;
    int weaponDropSequence;
    bool suppressChildWeaponRebuild;
    HotbarUI hotbarUI;

    public Weapon EquippedWeapon { get; private set; }
    public int SelectedSlotIndex => selectedSlotIndex;

    void Awake()
    {
        EquipStartingWeapon();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        EquipSlot(IsOwner ? selectedSlotIndex : networkSelectedSlotIndex.Value);
    }

    void Update()
    {
        if (!CanControlSelection()) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            EquipSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            EquipSlot(1);
        else if (keyboard[dropWeaponKey].wasPressedThisFrame)
            DropEquippedWeapon();
    }

    void LateUpdate()
    {
        TryBindHotbar();

        if (!IsSpawned || EquippedWeapon == null) return;

        if (IsOwner)
        {
            float aimAngle = EquippedWeapon.CurrentAimAngle;
            if (Mathf.Abs(Mathf.DeltaAngle(lastSentAimAngle, aimAngle)) >= aimSyncThreshold)
            {
                networkAimAngle.Value = aimAngle;
                lastSentAimAngle = aimAngle;
            }
        }
        else
        {
            if (selectedSlotIndex != networkSelectedSlotIndex.Value)
                EquipSlot(networkSelectedSlotIndex.Value, false);

            EquippedWeapon.ApplyRemoteAim(networkAimAngle.Value);
        }
    }

    public void EquipStartingWeapon()
    {
        Transform socket = GetOrCreateWeaponSocket();

        Weapon[] existingWeapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon weapon in existingWeapons)
        {
            if (weapon == null) continue;
            AttachWeaponToSocket(weapon.transform, socket);
            AddWeaponToSlot(weapon);
        }

        GameObject gunPrefab = startingGunPrefab;
        GameObject meleePrefab = startingMeleeWeaponPrefab;
        if (startingWeaponPrefab != null)
        {
            Weapon legacyWeapon = startingWeaponPrefab.GetComponent<Weapon>();
            if (legacyWeapon != null && legacyWeapon.IsMelee)
                meleePrefab ??= startingWeaponPrefab;
            else
                gunPrefab ??= startingWeaponPrefab;
        }

        if (slotWeapons[0] == null && gunPrefab != null)
            slotWeapons[0] = CreateWeaponForSlot(gunPrefab, socket);

        if (slotWeapons[1] == null && meleePrefab != null)
            slotWeapons[1] = CreateWeaponForSlot(meleePrefab, socket);

        NormalizeWeaponSlots();
        int defaultSlot = slotWeapons[0] != null ? 0 : slotWeapons[1] != null ? 1 : 0;
        EquipSlot(defaultSlot, false);
        TryBindHotbar();
    }

    public void PickupDroppedWeapon(WeaponPickup pickup)
    {
        if (pickup == null || pickup.Weapon == null) return;
        Weapon pickedWeapon = pickup.Weapon;
        int slotIndex = GetSlotIndexForWeapon(pickedWeapon);
        string pickupId = pickup.GetComponent<NetworkedWorldEntity>()?.NetworkId;

        Weapon oldWeapon = slotWeapons[slotIndex];
        if (oldWeapon == pickedWeapon) return;

        if (oldWeapon != null)
            DropWeapon(oldWeapon, slotIndex, transform.position + GetDropDirection() * dropDistance, false, CreateDropId(slotIndex));

        PreparePickedWeapon(pickup, pickedWeapon);
        Transform socket = GetOrCreateWeaponSocket();
        AttachWeaponToSocket(pickedWeapon.transform, socket);
        PrepareWeaponForEquip(pickedWeapon);
        ConfigureWeaponRendering(pickedWeapon.gameObject);

        slotWeapons[slotIndex] = pickedWeapon;
        selectedSlotIndex = slotIndex;
        EquipSlot(slotIndex);
        SaveGameManager.AutoSave("Weapon picked up");

        if (IsSpawned && IsOwner && !string.IsNullOrWhiteSpace(pickupId))
            PickupWeaponServerRpc(pickupId, slotIndex);
    }

    Weapon CreateWeaponForSlot(GameObject weaponPrefab, Transform socket)
    {
        GameObject weaponObject = Instantiate(weaponPrefab, socket);
        AttachWeaponToSocket(weaponObject.transform, socket);
        PrepareWeaponForEquip(weaponObject.GetComponent<Weapon>());
        ConfigureWeaponRendering(weaponObject);
        return weaponObject.GetComponent<Weapon>();
    }

    void AddWeaponToSlot(Weapon weapon)
    {
        int slotIndex = GetSlotIndexForWeapon(weapon);
        if (slotWeapons[slotIndex] == null)
            slotWeapons[slotIndex] = weapon;
        PrepareWeaponForEquip(weapon);
        ConfigureWeaponRendering(weapon.gameObject);
    }

    public void EquipSlot(int slotIndex, bool updateNetwork = true)
    {
        if (!suppressChildWeaponRebuild)
            RebuildSlotAssignmentsFromChildren();

        slotIndex = Mathf.Clamp(slotIndex, 0, slotWeapons.Length - 1);
        NormalizeWeaponSlots();
        if (slotWeapons[slotIndex] == null)
        {
            selectedSlotIndex = slotIndex;
            EquippedWeapon = null;
            for (int i = 0; i < slotWeapons.Length; i++)
                if (slotWeapons[i] != null)
                    slotWeapons[i].gameObject.SetActive(false);
            HideUnslottedWeaponChildren();

            if (updateNetwork && IsSpawned && IsOwner)
                networkSelectedSlotIndex.Value = selectedSlotIndex;
            TryBindHotbar();
            SaveGameManager.AutoSave("Weapon slot changed");
            return;
        }

        selectedSlotIndex = slotIndex;
        EquippedWeapon = slotWeapons[slotIndex];

        for (int i = 0; i < slotWeapons.Length; i++)
            if (slotWeapons[i] != null)
                slotWeapons[i].gameObject.SetActive(i == selectedSlotIndex);

        HideUnslottedWeaponChildren();

        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && EquippedWeapon != null)
            playerStats.ConfigureWeapon(EquippedWeapon.WeaponData);

        if (updateNetwork && IsSpawned && IsOwner)
            networkSelectedSlotIndex.Value = selectedSlotIndex;

        TryBindHotbar();
        SaveGameManager.AutoSave("Weapon slot changed");
    }

    void DropEquippedWeapon()
    {
        if (EquippedWeapon == null) return;

        int slotIndex = GetSlotIndexForWeapon(EquippedWeapon);
        Weapon weaponToDrop = EquippedWeapon;
        Vector3 dropPosition = transform.position + GetDropDirection() * dropDistance;
        string dropId = CreateDropId(slotIndex);

        DropWeapon(weaponToDrop, slotIndex, dropPosition, true, dropId);

        if (IsSpawned && IsOwner)
            DropWeaponServerRpc(slotIndex, dropPosition, dropId);

        int fallbackSlot = slotIndex == 0 ? 1 : 0;
        if (slotWeapons[fallbackSlot] != null)
            EquipSlot(fallbackSlot);
        else
        {
            EquippedWeapon = null;
            selectedSlotIndex = slotIndex;
            if (IsSpawned && IsOwner)
                networkSelectedSlotIndex.Value = selectedSlotIndex;
            TryBindHotbar();
        }

        SaveGameManager.AutoSave("Weapon dropped");
    }

    public string GetWeaponSaveId(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 0, slotWeapons.Length - 1);
        RebuildSlotAssignmentsFromChildren();
        Weapon weapon = slotWeapons[slotIndex];
        if (weapon == null) return string.Empty;
        if (weapon.WeaponData != null)
            return SaveGameManager.NormalizeObjectId(weapon.WeaponData.name);
        return SaveGameManager.NormalizeObjectId(weapon.gameObject.name);
    }

    public void RestoreWeaponsFromSave(string gunWeaponId, string meleeWeaponId, int savedSelectedSlotIndex, Func<string, GameObject> prefabResolver)
    {
        if (prefabResolver == null) return;

        Transform socket = GetOrCreateWeaponSocket();
        suppressChildWeaponRebuild = true;
        try
        {
            ClearEquippedWeapons();

            GameObject gunPrefab = prefabResolver(gunWeaponId);
            GameObject meleePrefab = prefabResolver(meleeWeaponId);

            if (gunPrefab != null)
                slotWeapons[0] = CreateWeaponForSlot(gunPrefab, socket);

            if (meleePrefab != null)
                slotWeapons[1] = CreateWeaponForSlot(meleePrefab, socket);

            NormalizeWeaponSlots();
            int slot = Mathf.Clamp(savedSelectedSlotIndex, 0, slotWeapons.Length - 1);
            if (slotWeapons[slot] == null)
                slot = slotWeapons[0] != null ? 0 : slotWeapons[1] != null ? 1 : 0;

            EquipSlot(slot, IsSpawned && IsOwner);
        }
        finally
        {
            suppressChildWeaponRebuild = false;
        }
    }

    void ClearEquippedWeapons()
    {
        Weapon[] childWeapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon weapon in childWeapons)
        {
            if (weapon == null)
                continue;

            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (Collider2D collider in weapon.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;

            weapon.gameObject.SetActive(false);
            weapon.transform.SetParent(null, true);
            Destroy(weapon.gameObject);
        }

        for (int i = 0; i < slotWeapons.Length; i++)
            slotWeapons[i] = null;

        EquippedWeapon = null;
    }

    void DropWeapon(Weapon weapon, int slotIndex, Vector3 dropPosition, bool clearSlot, string dropId = null)
    {
        if (weapon == null) return;

        if (clearSlot && slotIndex >= 0 && slotIndex < slotWeapons.Length && slotWeapons[slotIndex] == weapon)
            slotWeapons[slotIndex] = null;

        weapon.gameObject.SetActive(true);
        weapon.transform.SetParent(null, true);
        weapon.transform.position = dropPosition;
        weapon.transform.rotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one * droppedWeaponScale;
        weapon.SetDroppedState();

        ConfigureDroppedWeapon(weapon, dropId);
    }

    void ConfigureDroppedWeapon(Weapon weapon, string dropId = null)
    {
        if (weapon == null) return;

        CircleCollider2D trigger = weapon.GetComponent<CircleCollider2D>();
        if (trigger == null)
            trigger = weapon.gameObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = pickupColliderRadius;

        Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = weapon.gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        WeaponPickup pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup == null)
            pickup = weapon.gameObject.AddComponent<WeaponPickup>();
        pickup.Initialize(weapon, dropPickupDelay);

        if (!string.IsNullOrWhiteSpace(dropId))
        {
            NetworkedWorldEntity entity = weapon.GetComponent<NetworkedWorldEntity>();
            if (entity == null)
                entity = weapon.gameObject.AddComponent<NetworkedWorldEntity>();
            entity.Initialize(dropId);
        }

        foreach (SpriteRenderer renderer in weapon.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerID = SortingLayer.NameToID(nameof(Weapon));
            renderer.sortingOrder = 8;
            renderer.flipY = false;
        }
    }

    void PreparePickedWeapon(WeaponPickup pickup, Weapon weapon)
    {
        NetworkedWorldEntity entity = weapon.GetComponent<NetworkedWorldEntity>();
        if (entity != null)
            Destroy(entity);

        if (pickup != null)
            Destroy(pickup);

        PrepareWeaponForEquip(weapon);
    }

    void PrepareWeaponForEquip(Weapon weapon)
    {
        if (weapon == null) return;

        WeaponPickup pickup = weapon.GetComponent<WeaponPickup>();
        if (pickup != null)
            Destroy(pickup);

        CircleCollider2D trigger = weapon.GetComponent<CircleCollider2D>();
        if (trigger != null)
            Destroy(trigger);

        Rigidbody2D rb = weapon.GetComponent<Rigidbody2D>();
        if (rb != null)
            Destroy(rb);

        weapon.transform.localScale = Vector3.one;
        weapon.SetEquippedState();
    }

    Vector3 GetDropDirection()
    {
        if (EquippedWeapon != null)
        {
            Vector3 direction = EquippedWeapon.transform.right;
            if (direction.sqrMagnitude > 0.001f)
                return direction.normalized;
        }

        return transform.right.sqrMagnitude > 0.001f ? transform.right.normalized : Vector3.right;
    }

    Transform GetOrCreateWeaponSocket()
    {
        Transform socket = transform.Find("WeaponSocket");
        if (socket == null)
        {
            GameObject socketObject = new GameObject("WeaponSocket");
            socket = socketObject.transform;
            socket.SetParent(transform, false);
        }

        socket.localPosition = socketLocalPosition;
        socket.localRotation = Quaternion.Euler(socketLocalEulerAngles);
        socket.localScale = Vector3.one;
        return socket;
    }

    void AttachWeaponToSocket(Transform weaponTransform, Transform socket)
    {
        if (weaponTransform == null || socket == null) return;

        weaponTransform.SetParent(socket, false);
        weaponTransform.localPosition = Vector3.zero;
        weaponTransform.localRotation = Quaternion.identity;
        weaponTransform.localScale = Vector3.one;

        Weapon weapon = weaponTransform.GetComponent<Weapon>();
        if (weapon != null)
            weapon.SetEquippedState();
    }

    void ConfigureWeaponRendering(GameObject weaponObject)
    {
        if (weaponObject == null) return;

        int weaponSortingLayer = SortingLayer.NameToID(nameof(Weapon));
        foreach (SpriteRenderer renderer in weaponObject.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingLayerID = weaponSortingLayer;
            renderer.sortingOrder = 10;
        }
    }

    void TryBindHotbar()
    {
        if (!CanControlSelection()) return;
        if (hotbarUI == null)
            hotbarUI = HotbarUI.Instance;
        if (hotbarUI == null) return;

        if (!suppressChildWeaponRebuild)
            RebuildSlotAssignmentsFromChildren();

        NormalizeWeaponSlots();

        Weapon gun = FindWeaponByType(false);
        Weapon melee = FindWeaponByType(true);
        if (gun != null)
            hotbarUI.SetSlot(0, GetWeaponIcon(gun) ?? GetWeaponIcon(startingGunPrefab) ?? GetWeaponIcon(startingWeaponPrefab));
        else
            hotbarUI.ClearSlot(0);

        if (melee != null)
            hotbarUI.SetSlot(1, GetWeaponIcon(melee) ?? GetWeaponIcon(startingMeleeWeaponPrefab));
        else
            hotbarUI.ClearSlot(1);

        hotbarUI.SelectSlot(selectedSlotIndex);
    }

    void RebuildSlotAssignmentsFromChildren()
    {
        Weapon gun = null;
        Weapon melee = null;
        Weapon[] weapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon weapon in weapons)
        {
            if (weapon == null) continue;
            if (weapon.IsMelee)
                melee ??= weapon;
            else
                gun ??= weapon;
        }

        if (gun != null)
            slotWeapons[0] = gun;
        if (melee != null)
            slotWeapons[1] = melee;

        HideUnslottedWeaponChildren();
        ApplyEquippedWeaponVisibility();
    }

    void HideUnslottedWeaponChildren()
    {
        Weapon[] weapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon weapon in weapons)
        {
            if (weapon == null) continue;
            if (weapon == slotWeapons[0] || weapon == slotWeapons[1]) continue;
            weapon.gameObject.SetActive(false);
        }
    }

    void ApplyEquippedWeaponVisibility()
    {
        for (int i = 0; i < slotWeapons.Length; i++)
            if (slotWeapons[i] != null)
                slotWeapons[i].gameObject.SetActive(i == selectedSlotIndex);
    }

    Weapon FindWeaponByType(bool melee)
    {
        for (int i = 0; i < slotWeapons.Length; i++)
            if (slotWeapons[i] != null && slotWeapons[i].IsMelee == melee)
                return slotWeapons[i];

        Weapon[] weapons = GetComponentsInChildren<Weapon>(true);
        foreach (Weapon weapon in weapons)
            if (weapon != null && weapon.IsMelee == melee)
                return weapon;

        return null;
    }

    void NormalizeWeaponSlots()
    {
        Weapon first = slotWeapons[0];
        Weapon second = slotWeapons[1];

        if (first != null && GetSlotIndexForWeapon(first) == 1 && second == null)
        {
            slotWeapons[1] = first;
            slotWeapons[0] = null;
        }

        if (second != null && GetSlotIndexForWeapon(second) == 0 && first == null)
        {
            slotWeapons[0] = second;
            slotWeapons[1] = null;
        }

        if (slotWeapons[0] != null && slotWeapons[1] != null
            && GetSlotIndexForWeapon(slotWeapons[0]) == 1
            && GetSlotIndexForWeapon(slotWeapons[1]) == 0)
        {
            (slotWeapons[0], slotWeapons[1]) = (slotWeapons[1], slotWeapons[0]);
        }
    }

    int GetSlotIndexForWeapon(Weapon weapon)
    {
        if (weapon == null) return 0;
        return weapon.IsMelee ? 1 : 0;
    }

    void EnsureSlotWeapon(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotWeapons.Length) return;
        if (slotWeapons[slotIndex] != null) return;

        GameObject prefab = GetStartingPrefabForSlot(slotIndex);
        if (prefab == null) return;

        Transform socket = GetOrCreateWeaponSocket();
        slotWeapons[slotIndex] = CreateWeaponForSlot(prefab, socket);
        if (slotWeapons[slotIndex] != null)
            slotWeapons[slotIndex].gameObject.SetActive(slotIndex == selectedSlotIndex);
    }

    GameObject GetStartingPrefabForSlot(int slotIndex)
    {
        if (slotIndex == 0)
            return startingGunPrefab != null ? startingGunPrefab : startingWeaponPrefab;
        if (slotIndex == 1)
            return startingMeleeWeaponPrefab;
        return null;
    }

    Sprite GetWeaponIcon(Weapon weapon)
    {
        if (weapon == null) return null;

        if (weapon.WeaponData != null && weapon.WeaponData.Icon != null)
            return weapon.WeaponData.Icon;

        SpriteRenderer renderer = weapon.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    Sprite GetWeaponIcon(GameObject weaponPrefab)
    {
        if (weaponPrefab == null) return null;

        Weapon weapon = weaponPrefab.GetComponent<Weapon>();
        if (weapon != null && weapon.WeaponData != null && weapon.WeaponData.Icon != null)
            return weapon.WeaponData.Icon;

        SpriteRenderer renderer = weaponPrefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    bool CanControlSelection()
    {
        return !IsSpawned || IsOwner;
    }

    string CreateDropId(int slotIndex)
    {
        ulong ownerId = IsSpawned ? OwnerClientId : 0;
        weaponDropSequence++;
        return $"WeaponDrop_{ownerId}_{slotIndex}_{weaponDropSequence}";
    }

    [Rpc(SendTo.Server)]
    void DropWeaponServerRpc(int slotIndex, Vector3 dropPosition, string dropId)
    {
        DropWeaponClientRpc(slotIndex, dropPosition, dropId);
    }

    [ClientRpc]
    void DropWeaponClientRpc(int slotIndex, Vector3 dropPosition, string dropId)
    {
        if (IsOwner) return;
        slotIndex = Mathf.Clamp(slotIndex, 0, slotWeapons.Length - 1);
        RebuildSlotAssignmentsFromChildren();
        Weapon weapon = slotWeapons[slotIndex];
        if (weapon == null) return;
        DropWeapon(weapon, slotIndex, dropPosition, true, dropId);
        if (selectedSlotIndex == slotIndex)
        {
            int fallbackSlot = slotIndex == 0 ? 1 : 0;
            if (slotWeapons[fallbackSlot] != null)
                EquipSlot(fallbackSlot, false);
            else
                EquippedWeapon = null;
        }
    }

    [Rpc(SendTo.Server)]
    void PickupWeaponServerRpc(string pickupId, int slotIndex)
    {
        PickupWeaponClientRpc(pickupId, slotIndex);
    }

    [ClientRpc]
    void PickupWeaponClientRpc(string pickupId, int slotIndex)
    {
        if (IsOwner) return;
        if (string.IsNullOrWhiteSpace(pickupId)) return;
        if (!NetworkedWorldEntity.TryFind(pickupId, out WeaponPickup pickup) || pickup.Weapon == null) return;

        Weapon pickedWeapon = pickup.Weapon;
        slotIndex = Mathf.Clamp(slotIndex, 0, slotWeapons.Length - 1);

        Weapon oldWeapon = slotWeapons[slotIndex];
        if (oldWeapon != null && oldWeapon != pickedWeapon)
            DropWeapon(oldWeapon, slotIndex, transform.position + GetDropDirection() * dropDistance, false, CreateDropId(slotIndex));

        PreparePickedWeapon(pickup, pickedWeapon);
        Transform socket = GetOrCreateWeaponSocket();
        AttachWeaponToSocket(pickedWeapon.transform, socket);
        PrepareWeaponForEquip(pickedWeapon);
        ConfigureWeaponRendering(pickedWeapon.gameObject);
        slotWeapons[slotIndex] = pickedWeapon;
        EquipSlot(slotIndex, false);
    }

    public void SubmitNetworkFire(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius, float speed, float lifetime)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && playerStats.IsDead) return;
        if (!IsSpawned || !IsOwner) return;
        FireServerRpc(origin, direction.normalized, damage, range, hitRadius, speed, lifetime);
    }

    public void SubmitNetworkMelee(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && playerStats.IsDead) return;
        if (!IsSpawned || !IsOwner) return;
        MeleeServerRpc(origin, direction.normalized, damage, range, hitRadius);
    }

    [Rpc(SendTo.Server)]
    void FireServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius, float speed, float lifetime)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null)
            playerStats.ResolveServerShot(origin, direction, damage, range, hitRadius);

        FireVisualClientRpc(origin, direction, damage, speed, lifetime);
    }

    [Rpc(SendTo.Server)]
    void MeleeServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null)
            playerStats.ResolveServerMelee(origin, direction, damage, range, hitRadius);

        MeleeVisualClientRpc();
    }

    [ClientRpc]
    void FireVisualClientRpc(Vector2 origin, Vector2 direction, float damage, float speed, float lifetime)
    {
        if (IsOwner) return;
        if (EquippedWeapon == null) EquipStartingWeapon();
        if (EquippedWeapon == null) return;

        EquippedWeapon.SpawnProjectileVisual(origin, direction, damage, speed, lifetime, false);
        EquippedWeapon.PlayFireAnimation();
    }

    [ClientRpc]
    void MeleeVisualClientRpc()
    {
        if (IsOwner) return;
        if (EquippedWeapon == null) EquipStartingWeapon();
        if (EquippedWeapon == null) return;

        EquippedWeapon.PlayFireAnimation();
    }
}
