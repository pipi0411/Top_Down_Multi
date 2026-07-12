using UnityEngine;

public enum WeaponType
{
    Gun,
    Melee
}

public enum WeaponRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}


[CreateAssetMenu(fileName = "New Weapon", menuName = "Items/Weapon")]
public class ItemWeapon : ItemData
{
    [Header("Weapon Data")]
    public WeaponType Type;
    public WeaponRarity Rarity;

    [Header("Settings")]
    public float Damage;
    public float RequiredEnergy;
    public float TimeBetweenShots;
    public float MinSpread;
    public float MaxSpread;

    [Header("Ammo")]
    public int MagazineSize = 30;
    public int StartingReserveAmmo = 120;
    public float ReloadDuration = 1.2f;
}
