using Unity.Netcode;
using UnityEngine;

public class PlayerWeaponController : NetworkBehaviour
{
    [SerializeField] GameObject startingWeaponPrefab;
    [SerializeField] Vector3 socketLocalPosition = new(0.2f, -0.08f, -0.1f);
    [SerializeField] Vector3 socketLocalEulerAngles;
    [SerializeField] float aimSyncThreshold = 0.5f;

    readonly NetworkVariable<float> networkAimAngle = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    float lastSentAimAngle;

    public Weapon EquippedWeapon { get; private set; }

    void Awake()
    {
        EquipStartingWeapon();
    }

    void LateUpdate()
    {
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
            EquippedWeapon.ApplyRemoteAim(networkAimAngle.Value);
        }
    }

    public void EquipStartingWeapon()
    {
        EquippedWeapon = GetComponentInChildren<Weapon>();
        if (EquippedWeapon != null || startingWeaponPrefab == null) return;

        GameObject socketObject = new GameObject("WeaponSocket");
        Transform socket = socketObject.transform;
        socket.SetParent(transform, false);
        socket.localPosition = socketLocalPosition;
        socket.localRotation = Quaternion.Euler(socketLocalEulerAngles);

        GameObject weaponObject = Instantiate(startingWeaponPrefab, socket);
        weaponObject.transform.localPosition = Vector3.zero;
        weaponObject.transform.localRotation = Quaternion.identity;
        weaponObject.transform.localScale = Vector3.one;
        EquippedWeapon = weaponObject.GetComponent<Weapon>();
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && EquippedWeapon != null)
            playerStats.ConfigureWeapon(EquippedWeapon.WeaponData);

        int weaponSortingLayer = SortingLayer.NameToID(nameof(Weapon));
        foreach (SpriteRenderer renderer in weaponObject.GetComponentsInChildren<SpriteRenderer>())
        {
            renderer.sortingLayerID = weaponSortingLayer;
            renderer.sortingOrder = 10;
        }
    }

    public void SubmitNetworkFire(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius, float speed, float lifetime)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null && playerStats.IsDead) return;
        if (!IsSpawned || !IsOwner) return;
        FireServerRpc(origin, direction.normalized, damage, range, hitRadius, speed, lifetime);
    }

    [Rpc(SendTo.Server)]
    void FireServerRpc(Vector2 origin, Vector2 direction, float damage, float range, float hitRadius, float speed, float lifetime)
    {
        PlayerHealth playerStats = GetComponent<PlayerHealth>();
        if (playerStats != null)
            playerStats.ResolveServerShot(origin, direction, damage, range, hitRadius);

        FireVisualClientRpc(origin, direction, damage, speed, lifetime);
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
}
