using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponPickupPrefabBuilder
{
    const string WeaponPrefabFolder = "Assets/Prefabs/Weapon";
    const float PickupRadius = 0.45f;

    static readonly string[] PlayerWeaponPrefabNames =
    {
        "Weapon_Glock",
        "Weapon_Lewis",
        "Weapon_LMG",
        "Weapon_MP5",
        "Weapon_Physics_1",
        "Weapon_Physics_2",
        "Weapon_RPK",
        "Weapon_Smith",
        "Weapon_Sten",
        "Weapon_Thompson"
    };

    static WeaponPickupPrefabBuilder()
    {
        EditorApplication.delayCall += EnsureWeaponPrefabsReady;
    }

    [MenuItem("Tools/Top Down Multi/Weapons/Add Pickup To Player Weapon Prefabs")]
    public static void EnsureWeaponPrefabsReady()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool changedAny = false;
        foreach (string prefabName in PlayerWeaponPrefabNames)
        {
            string path = $"{WeaponPrefabFolder}/{prefabName}.prefab";
            if (!File.Exists(path)) continue;
            changedAny |= ConfigureWeaponPrefab(path);
        }

        if (changedAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Player weapon prefabs now have pickup components for map placement.");
        }
    }

    static bool ConfigureWeaponPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool changed = false;

        try
        {
            Weapon weapon = root.GetComponent<Weapon>();
            if (weapon == null) return false;

            CircleCollider2D trigger = root.GetComponent<CircleCollider2D>();
            if (trigger == null)
            {
                trigger = root.AddComponent<CircleCollider2D>();
                changed = true;
            }
            if (!trigger.isTrigger)
            {
                trigger.isTrigger = true;
                changed = true;
            }
            if (!Mathf.Approximately(trigger.radius, PickupRadius))
            {
                trigger.radius = PickupRadius;
                changed = true;
            }

            Rigidbody2D rb = root.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = root.AddComponent<Rigidbody2D>();
                changed = true;
            }
            if (rb.bodyType != RigidbodyType2D.Kinematic)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                changed = true;
            }
            if (!Mathf.Approximately(rb.gravityScale, 0f))
            {
                rb.gravityScale = 0f;
                changed = true;
            }

            WeaponPickup pickup = root.GetComponent<WeaponPickup>();
            if (pickup == null)
            {
                pickup = root.AddComponent<WeaponPickup>();
                changed = true;
            }

            SerializedObject serializedPickup = new SerializedObject(pickup);
            SerializedProperty weaponProperty = serializedPickup.FindProperty("weapon");
            if (weaponProperty != null && weaponProperty.objectReferenceValue != weapon)
            {
                weaponProperty.objectReferenceValue = weapon;
                serializedPickup.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return changed;
    }
}
