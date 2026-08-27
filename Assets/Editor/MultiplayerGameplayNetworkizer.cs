#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class MultiplayerGameplayNetworkizer
{
    const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
    const string AutoConvertEditorPrefsKey = "TopDownMulti.MultiplayerGameplayNetworkizer.AutoConverted.v1";

    static readonly string[] EnemyPrefabFolders =
    {
        "Assets/Prefabs/PlayerPrefabs/EnemyPrefabs"
    };

    static readonly string[] PropPrefabPaths =
    {
        "Assets/Prefabs/Prop/Box_1.prefab",
        "Assets/Prefabs/Prop/Box_2.prefab",
        "Assets/Prefabs/Prop/Box_3.prefab",
        "Assets/Prefabs/Prop/Box_4.prefab",
        "Assets/Prefabs/Prop/BottleHealth.prefab",
        "Assets/Prefabs/Prop/BottleMana.prefab",
        "Assets/Prefabs/Prop/BottleShield.prefab",
        "Assets/Prefabs/Prop/Coin.prefab"
    };

    [MenuItem("Tools/Multiplayer/Convert Gameplay Prefabs To NetworkObjects")]
    public static void ConvertGameplayPrefabs()
    {
        List<GameObject> convertedPrefabs = new List<GameObject>();

        foreach (string folder in EnemyPrefabFolders)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = EnsureNetworkObjectOnPrefab(path);
                if (prefab != null)
                    convertedPrefabs.Add(prefab);
            }
        }

        foreach (string path in PropPrefabPaths)
        {
            GameObject prefab = EnsureNetworkObjectOnPrefab(path);
            if (prefab != null)
                convertedPrefabs.Add(prefab);
        }

        RegisterNetworkPrefabs(convertedPrefabs);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MultiplayerGameplayNetworkizer] Converted/registered {convertedPrefabs.Count} gameplay prefab(s).");
    }

    [InitializeOnLoadMethod]
    static void AutoConvertOnceAfterCompile()
    {
        if (EditorPrefs.GetBool(AutoConvertEditorPrefsKey, false))
            return;

        EditorPrefs.SetBool(AutoConvertEditorPrefsKey, true);
        EditorApplication.delayCall += ConvertGameplayPrefabs;
    }

    static GameObject EnsureNetworkObjectOnPrefab(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
            return null;

        bool dirty = false;
        if (root.GetComponent<NetworkObject>() == null)
        {
            root.AddComponent<NetworkObject>();
            dirty = true;
        }

        if (root.GetComponent<NetworkedWorldEntity>() == null)
        {
            root.AddComponent<NetworkedWorldEntity>();
            dirty = true;
        }

        if (dirty)
            PrefabUtility.SaveAsPrefabAsset(root, path);

        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static void RegisterNetworkPrefabs(IReadOnlyList<GameObject> prefabs)
    {
        ScriptableObject listAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(NetworkPrefabsListPath);
        if (listAsset == null)
        {
            Debug.LogError($"[MultiplayerGameplayNetworkizer] Cannot find {NetworkPrefabsListPath}.");
            return;
        }

        SerializedObject serializedList = new SerializedObject(listAsset);
        SerializedProperty listProperty = serializedList.FindProperty("List");
        if (listProperty == null || !listProperty.isArray)
        {
            Debug.LogError("[MultiplayerGameplayNetworkizer] NetworkPrefabsList serialized field 'List' not found.");
            return;
        }

        HashSet<GameObject> existing = new HashSet<GameObject>();
        for (int i = 0; i < listProperty.arraySize; i++)
        {
            SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
            SerializedProperty prefabProperty = element.FindPropertyRelative("Prefab");
            if (prefabProperty != null && prefabProperty.objectReferenceValue is GameObject prefab)
                existing.Add(prefab);
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null || existing.Contains(prefab))
                continue;

            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
            SerializedProperty overrideProperty = element.FindPropertyRelative("Override");
            SerializedProperty prefabProperty = element.FindPropertyRelative("Prefab");
            SerializedProperty sourcePrefabProperty = element.FindPropertyRelative("SourcePrefabToOverride");
            SerializedProperty sourceHashProperty = element.FindPropertyRelative("SourceHashToOverride");
            SerializedProperty overridingTargetProperty = element.FindPropertyRelative("OverridingTargetPrefab");

            if (overrideProperty != null) overrideProperty.boolValue = false;
            if (prefabProperty != null) prefabProperty.objectReferenceValue = prefab;
            if (sourcePrefabProperty != null) sourcePrefabProperty.objectReferenceValue = null;
            if (sourceHashProperty != null) sourceHashProperty.uintValue = 0;
            if (overridingTargetProperty != null) overridingTargetProperty.enumValueIndex = 0;

            existing.Add(prefab);
        }

        serializedList.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(listAsset);
    }
}
#endif
