using UnityEngine;

public class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] GameObject startingWeaponPrefab;
    [SerializeField] Vector3 socketLocalPosition = new(0.2f, -0.08f, -0.1f);
    [SerializeField] Vector3 socketLocalEulerAngles;

    public Weapon EquippedWeapon { get; private set; }

    void Awake()
    {
        EquipStartingWeapon();
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
}
