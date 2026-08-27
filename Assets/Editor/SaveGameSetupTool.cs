#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SaveGameSetupTool
{
    const string RegistryPath = "Assets/Resources/WeaponPrefabRegistry.asset";

    [MenuItem("Tools/Top Down Multi/Save/Setup Save System")]
    public static void SetupSaveSystem()
    {
        Directory.CreateDirectory("Assets/Resources");

        WeaponPrefabRegistry registry = AssetDatabase.LoadAssetAtPath<WeaponPrefabRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<WeaponPrefabRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        SerializedObject serializedRegistry = new SerializedObject(registry);
        SerializedProperty weapons = serializedRegistry.FindProperty("weaponPrefabs");
        GameObject[] prefabs = FindPlayerWeaponPrefabs();
        weapons.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
            weapons.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        serializedRegistry.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SaveGameSetupTool] Save system ready. Weapon registry contains {prefabs.Length} player weapons.");
    }

    static GameObject[] FindPlayerWeaponPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapon" });
        List<GameObject> prefabs = new List<GameObject>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            if (prefab.name.StartsWith("Weapon_Enemy")) continue;
            if (prefab.GetComponent<Weapon>() == null) continue;
            prefabs.Add(prefab);
        }

        return prefabs.OrderBy(prefab => prefab.name).ToArray();
    }
}
#endif
