using UnityEngine;

[CreateAssetMenu(fileName = "WeaponPrefabRegistry", menuName = "Top Down Multi/Save/Weapon Prefab Registry")]
public class WeaponPrefabRegistry : ScriptableObject
{
    [SerializeField] GameObject[] weaponPrefabs;

    public GameObject FindWeaponPrefab(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId) || weaponPrefabs == null)
            return null;

        string normalizedId = SaveGameManager.NormalizeObjectId(weaponId);
        foreach (GameObject prefab in weaponPrefabs)
        {
            if (prefab == null) continue;
            if (SaveGameManager.NormalizeObjectId(prefab.name) == normalizedId)
                return prefab;

            Weapon weapon = prefab.GetComponent<Weapon>();
            if (weapon != null
                && weapon.WeaponData != null
                && SaveGameManager.NormalizeObjectId(weapon.WeaponData.name) == normalizedId)
            {
                return prefab;
            }
        }

        return null;
    }
}
