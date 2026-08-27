#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class WeaponChestPrefabBuilder
{
    const string MenuPath = "Tools/Top Down Multi/Prop/Create Weapon Chest Prefab";
    const string PrefabPath = "Assets/Prefabs/Prop/WeaponChest.prefab";

    [MenuItem(MenuPath)]
    public static void CreateWeaponChestPrefab()
    {
        bool prefabExists = File.Exists(PrefabPath);
        Sprite[] selectedSprites = GetSelectedSprites();
        Sprite[] sprites = selectedSprites.Length >= 3
            ? selectedSprites
            : prefabExists
                ? new Sprite[0]
                : GetFallbackChestSprites();

        GameObject chest = prefabExists
            ? PrefabUtility.LoadPrefabContents(PrefabPath)
            : new GameObject("WeaponChest");
        try
        {
            chest.name = "WeaponChest";

            SpriteRenderer renderer = GetOrAdd<SpriteRenderer>(chest);
            if (sprites.Length > 0)
                renderer.sprite = sprites[0];
            renderer.sortingLayerID = SortingLayer.NameToID("Prop");
            renderer.sortingOrder = 3;

            BoxCollider2D collider = GetOrAdd<BoxCollider2D>(chest);
            collider.isTrigger = false;
            if (!prefabExists)
            {
                collider.size = new Vector2(0.95f, 0.75f);
                collider.offset = new Vector2(0f, -0.05f);
            }

            GetOrAdd<NetworkedWorldEntity>(chest);
            WeaponChest weaponChest = GetOrAdd<WeaponChest>(chest);
            TextMeshPro prompt = EnsurePrompt(chest.transform);

            SerializedObject serializedChest = new SerializedObject(weaponChest);
            if (sprites.Length > 0)
            {
                Assign(serializedChest, "closedSprite", sprites[0]);
                AssignArray(serializedChest, "openingFrames", sprites);
                Assign(serializedChest, "openSprite", sprites[sprites.Length - 1]);
            }

            Assign(serializedChest, "interactPrompt", prompt);
            AssignString(serializedChest, "interactPromptText", "Ấn E để mở");
            AssignVector3(serializedChest, "interactPromptOffset", new Vector3(0f, 0.85f, -0.1f));
            AssignColor(serializedChest, "interactPromptColor", Color.white);
            AssignColor(serializedChest, "interactPromptOutlineColor", new Color(0.17f, 0.02f, 0.32f, 1f));
            AssignArray(serializedChest, "weaponPrefabs", GetPlayerWeaponPrefabs());
            serializedChest.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(chest, PrefabPath);
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;

            Debug.Log($"[WeaponChestPrefabBuilder] {(prefabExists ? "Updated" : "Created")} {PrefabPath}. Select 3 chest sprites before running the tool if you want to override the visuals.");
        }
        finally
        {
            if (prefabExists)
                PrefabUtility.UnloadPrefabContents(chest);
            else
                Object.DestroyImmediate(chest);
        }
    }

    static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    static TextMeshPro EnsurePrompt(Transform parent)
    {
        Transform existing = parent.Find("InteractPrompt");
        GameObject promptObject = existing != null ? existing.gameObject : new GameObject("InteractPrompt");
        promptObject.transform.SetParent(parent, false);
        promptObject.transform.localPosition = new Vector3(0f, 0.85f, -0.1f);
        promptObject.transform.localRotation = Quaternion.identity;
        promptObject.transform.localScale = Vector3.one;

        TextMeshPro prompt = promptObject.GetComponent<TextMeshPro>();
        if (prompt == null)
            prompt = promptObject.AddComponent<TextMeshPro>();

        prompt.text = "Ấn E để mở";
        prompt.alignment = TextAlignmentOptions.Center;
        prompt.fontSize = 2.8f;
        prompt.color = Color.white;
        prompt.outlineColor = new Color(0.17f, 0.02f, 0.32f, 1f);
        prompt.outlineWidth = 0.34f;
        prompt.sortingLayerID = SortingLayer.NameToID("UI");
        prompt.sortingOrder = 50;
        prompt.textWrappingMode = TextWrappingModes.NoWrap;
        prompt.gameObject.SetActive(false);
        return prompt;
    }

    static Sprite[] GetSelectedSprites()
    {
        return Selection.objects
            .OfType<Sprite>()
            .ToArray();
    }

    static Sprite[] GetFallbackChestSprites()
    {
        string[] names =
        {
            "tilemap_packed_107",
            "tilemap_packed_108",
            "tilemap_packed_109"
        };

        List<Sprite> sprites = new List<Sprite>();
        foreach (string name in names)
        {
            Sprite sprite = FindSprite(name);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    static Sprite FindSprite(string spriteName)
    {
        string[] guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == spriteName)
                    return sprite;
        }

        return null;
    }

    static GameObject[] GetPlayerWeaponPrefabs()
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

        return prefabs
            .OrderBy(prefab => prefab.name)
            .ToArray();
    }

    static void Assign(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void AssignString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    static void AssignVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    static void AssignColor(SerializedObject serializedObject, string propertyName, Color value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.colorValue = value;
    }

    static void AssignArray(SerializedObject serializedObject, string propertyName, Sprite[] sprites)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray) return;

        property.arraySize = sprites?.Length ?? 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    static void AssignArray(SerializedObject serializedObject, string propertyName, GameObject[] prefabs)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || !property.isArray) return;

        property.arraySize = prefabs?.Length ?? 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
    }
}
#endif
