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

    readonly NetworkVariable<float> networkAimAngle = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> networkSelectedSlotIndex = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly Weapon[] slotWeapons = new Weapon[2];
    float lastSentAimAngle;
    int selectedSlotIndex;
    HotbarUI hotbarUI;

    public Weapon EquippedWeapon { get; private set; }

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

    Weapon CreateWeaponForSlot(GameObject weaponPrefab, Transform socket)
    {
        GameObject weaponObject = Instantiate(weaponPrefab, socket);
        AttachWeaponToSocket(weaponObject.transform, socket);
        ConfigureWeaponRendering(weaponObject);
        return weaponObject.GetComponent<Weapon>();
    }

    void AddWeaponToSlot(Weapon weapon)
    {
        int slotIndex = GetSlotIndexForWeapon(weapon);
        if (slotWeapons[slotIndex] == null)
            slotWeapons[slotIndex] = weapon;
        ConfigureWeaponRendering(weapon.gameObject);
    }

    public void EquipSlot(int slotIndex, bool updateNetwork = true)
    {
        RebuildSlotAssignmentsFromChildren();
        slotIndex = Mathf.Clamp(slotIndex, 0, slotWeapons.Length - 1);
        EnsureSlotWeapon(slotIndex);
        NormalizeWeaponSlots();
        if (slotWeapons[slotIndex] == null) return;

        selectedSlotIndex = slotIndex;
        EquippedWeapon = slotWeapons[slotIndex];

        for (int i = 0; i < slotWeapons.Length; i++)
            if (slotWeapons[i] != null)
                slotWeapons[i].gameObject.SetActive(i == selectedSlotIndex);

        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && EquippedWeapon != null)
            playerStats.ConfigureWeapon(EquippedWeapon.WeaponData);

        if (updateNetwork && IsSpawned && IsOwner)
            networkSelectedSlotIndex.Value = selectedSlotIndex;

        TryBindHotbar();
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

        RebuildSlotAssignmentsFromChildren();
        EnsureSlotWeapon(0);
        EnsureSlotWeapon(1);
        NormalizeWeaponSlots();

        Weapon gun = FindWeaponByType(false);
        Weapon melee = FindWeaponByType(true);
        hotbarUI.SetSlot(0, GetWeaponIcon(gun) ?? GetWeaponIcon(startingGunPrefab) ?? GetWeaponIcon(startingWeaponPrefab));
        hotbarUI.SetSlot(1, GetWeaponIcon(melee) ?? GetWeaponIcon(startingMeleeWeaponPrefab));

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
